using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using AutoDaily.Core.Models;
using AutoDaily.Core.Native;
using ActionModel = AutoDaily.Core.Models.Action;

namespace AutoDaily.Core.Engine
{
    public class Recorder : IDisposable
    {
        private IntPtr _mouseHook = IntPtr.Zero;
        private IntPtr _keyboardHook = IntPtr.Zero;
        private User32.LowLevelProc _mouseProc;
        private User32.LowLevelProc _keyboardProc;
        private List<ActionModel> _actions = new List<ActionModel>();
        private DateTime _lastActionTime = DateTime.Now;
        private const int DEBOUNCE_MS = 50;
        private bool _isRecording = false;
        private WindowInfo _targetWindow;

        public event System.Action<List<ActionModel>, WindowInfo> OnRecordingComplete;
        public event Action<string> OnStatusUpdate;

        public bool IsRecording => _isRecording;

        public void StartRecording()
        {
            if (_isRecording)
                return;

            _isRecording = true;
            _actions.Clear();
            _lastActionTime = DateTime.Now;

            // 获取当前活动窗口信息
            CaptureCurrentWindow();

            // 安装钩子
            _mouseProc = MouseHookProc;
            _keyboardProc = KeyboardHookProc;

            _mouseHook = User32.SetWindowsHookEx(
                User32.WH_MOUSE_LL,
                _mouseProc,
                Kernel32.GetModuleHandle(null),
                0);

            _keyboardHook = User32.SetWindowsHookEx(
                User32.WH_KEYBOARD_LL,
                _keyboardProc,
                Kernel32.GetModuleHandle(null),
                0);

            OnStatusUpdate?.Invoke("录制已开始，请开始操作...");
        }

        public void StopRecording()
        {
            if (!_isRecording)
                return;

            _isRecording = false;

            if (_mouseHook != IntPtr.Zero)
            {
                User32.UnhookWindowsHookEx(_mouseHook);
                _mouseHook = IntPtr.Zero;
            }

            if (_keyboardHook != IntPtr.Zero)
            {
                User32.UnhookWindowsHookEx(_keyboardHook);
                _keyboardHook = IntPtr.Zero;
            }

            OnStatusUpdate?.Invoke("录制完成");
            OnRecordingComplete?.Invoke(_actions, _targetWindow);
        }

        private void CaptureCurrentWindow()
        {
            var hwnd = User32.GetForegroundWindow();
            if (hwnd != IntPtr.Zero)
            {
                if (_targetWindow == null)
                {
                    _targetWindow = new WindowInfo();
                }
                
                User32.GetWindowRect(hwnd, out var rect);
                if (_targetWindow.Rect == null)
                {
                    _targetWindow.Rect = new WindowRect();
                }
                
                // 保存窗口的完整位置和大小信息
                _targetWindow.Rect.Width = rect.Right - rect.Left;
                _targetWindow.Rect.Height = rect.Bottom - rect.Top;
                _targetWindow.WindowLeft = rect.Left;
                _targetWindow.WindowTop = rect.Top;

                // 获取窗口标题
                int length = User32.GetWindowTextLength(hwnd);
                if (length > 0)
                {
                    StringBuilder sb = new StringBuilder(length + 1);
                    User32.GetWindowText(hwnd, sb, sb.Capacity);
                    _targetWindow.Title = sb.ToString();
                }

                // 获取进程名
                try
                {
                    GetWindowThreadProcessId(hwnd, out uint processId);
                    var process = Process.GetProcessById((int)processId);
                    _targetWindow.ProcessName = process.ProcessName;
                }
                catch { }
            }
        }

        private IntPtr MouseHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && _isRecording)
            {
                var timeSinceLastAction = (DateTime.Now - _lastActionTime).TotalMilliseconds;

                // 获取鼠标位置
                User32.GetCursorPos(out var point);
                var hwnd = User32.WindowFromPoint(point);
                
                if (hwnd == IntPtr.Zero)
                {
                    hwnd = User32.GetForegroundWindow();
                }
                
                if (hwnd != IntPtr.Zero)
                {
                    User32.GetWindowRect(hwnd, out var rect);
                    
                    // 使用目标窗口的位置
                    if (_targetWindow != null && _targetWindow.WindowLeft != 0 && _targetWindow.WindowTop != 0)
                    {
                        rect.Left = _targetWindow.WindowLeft;
                        rect.Top = _targetWindow.WindowTop;
                        rect.Right = rect.Left + _targetWindow.Rect.Width;
                        rect.Bottom = rect.Top + _targetWindow.Rect.Height;
                    }
                    
                    int relX = point.X - rect.Left;
                    int relY = point.Y - rect.Top;

                    // 处理鼠标移动（记录hover轨迹，但降低频率避免过多数据）
                    if (wParam == (IntPtr)User32.WM_MOUSEMOVE && timeSinceLastAction > 200)
                    {
                        AddAction(new ActionModel
                        {
                            Type = "MouseMove",
                            X = relX,
                            Y = relY,
                            Relative = true
                        });
                        _lastActionTime = DateTime.Now;
                        return User32.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
                    }

                    // 处理鼠标点击
                    if (timeSinceLastAction < DEBOUNCE_MS)
                        return User32.CallNextHookEx(_mouseHook, nCode, wParam, lParam);

                    if (wParam == (IntPtr)User32.WM_LBUTTONDOWN)
                    {
                        AddAction(new ActionModel
                        {
                            Type = "MouseClick",
                            X = relX,
                            Y = relY,
                            Relative = true,
                            Button = "Left"
                        });
                        _lastActionTime = DateTime.Now;
                    }
                    else if (wParam == (IntPtr)User32.WM_RBUTTONDOWN)
                    {
                        AddAction(new ActionModel
                        {
                            Type = "MouseClick",
                            X = relX,
                            Y = relY,
                            Relative = true,
                            Button = "Right"
                        });
                        _lastActionTime = DateTime.Now;
                    }
                    // 处理鼠标滚轮
                    else if (wParam == (IntPtr)User32.WM_MOUSEWHEEL)
                    {
                        // 在低级鼠标钩子中，lParam指向MSLLHOOKSTRUCT
                        // 结构：POINT pt(8字节) + DWORD mouseData(4字节) + DWORD flags(4字节) + DWORD time(4字节) + ULONG_PTR dwExtraInfo(8字节)
                        // mouseData的高16位是wheel delta
                        int mouseData = Marshal.ReadInt32(lParam, 8); // 偏移8字节（跳过POINT）获取mouseData
                        int delta = (short)((mouseData >> 16) & 0xFFFF); // 高16位是wheel delta（有符号）
                        
                        AddAction(new ActionModel
                        {
                            Type = "MouseWheel",
                            X = relX,
                            Y = relY,
                            Relative = true,
                            Param = delta
                        });
                        _lastActionTime = DateTime.Now;
                    }
                }
            }

            return User32.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        }

        private IntPtr KeyboardHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && _isRecording)
            {
                // 只记录按键按下
                if (wParam == (IntPtr)User32.WM_KEYDOWN || wParam == (IntPtr)User32.WM_SYSKEYDOWN)
                {
                    int vkCode = Marshal.ReadInt32(lParam);
                    
                    // 检查修饰键状态
                    bool ctrl = (User32.GetAsyncKeyState(User32.VK_CONTROL) & 0x8000) != 0;
                    bool shift = (User32.GetAsyncKeyState(User32.VK_SHIFT) & 0x8000) != 0;
                    bool alt = (User32.GetAsyncKeyState(User32.VK_ALT) & 0x8000) != 0;
                    
                    // 记录快捷键组合（Ctrl+C, Ctrl+V等）
                    if (ctrl || shift || alt)
                    {
                        AddAction(new ActionModel
                        {
                            Type = "KeyPress",
                            Param = vkCode,
                            Text = $"{(ctrl ? "Ctrl+" : "")}{(shift ? "Shift+" : "")}{(alt ? "Alt+" : "")}"
                        });
                        _lastActionTime = DateTime.Now;
                        return User32.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
                    }
                    
                    // 忽略功能键（F1-F12等），但可以记录其他特殊键
                    if (vkCode >= 0x70 && vkCode <= 0x7B)
                        return User32.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);

                    // 记录特殊键（Enter, Tab, Escape, Win键等）
                    if (vkCode == User32.VK_ENTER || vkCode == User32.VK_TAB || 
                        vkCode == User32.VK_ESCAPE || vkCode == User32.VK_BACK || 
                        vkCode == User32.VK_DELETE || vkCode == User32.VK_LWIN || 
                        vkCode == User32.VK_RWIN)
                    {
                        AddAction(new ActionModel
                        {
                            Type = "KeyPress",
                            Param = vkCode
                        });
                        _lastActionTime = DateTime.Now;
                        return User32.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
                    }

                    // 记录可见字符输入（字母、数字等）
                    try
                    {
                        // 使用ToUnicode将虚拟键码转换为字符
                        byte[] keyboardState = new byte[256];
                        User32.GetKeyboardState(keyboardState);
                        StringBuilder sb = new StringBuilder(10);
                        int result = User32.ToUnicode((uint)vkCode, 0, keyboardState, sb, sb.Capacity, 0);
                        
                        if (result > 0 && sb.Length > 0)
                        {
                            char ch = sb[0];
                            // 记录可打印字符（字母、数字、标点等）
                            if (char.IsLetterOrDigit(ch) || char.IsPunctuation(ch) || char.IsSymbol(ch) || ch == ' ')
                            {
                                AddAction(new ActionModel
                                {
                                    Type = "Input",
                                    Text = ch.ToString()
                                });
                                _lastActionTime = DateTime.Now;
                                return User32.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // 如果转换失败，记录为KeyPress
                        System.Diagnostics.Debug.WriteLine($"字符转换失败: {ex.Message}");
                    }
                }
            }

            return User32.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
        }

        private void AddAction(ActionModel action)
        {
            // 如果上一个动作是Wait且时间很短，合并
            if (_actions.Count > 0)
            {
                var lastAction = _actions[_actions.Count - 1];
                var timeSinceLast = (DateTime.Now - _lastActionTime).TotalMilliseconds;

                if (lastAction.Type == "Wait" && timeSinceLast < 500)
                {
                    lastAction.Param = (int)timeSinceLast;
                }
                else if (timeSinceLast > 100)
                {
                    _actions.Add(new ActionModel
                    {
                        Type = "Wait",
                        Param = (int)timeSinceLast
                    });
                }
            }
            else if ((DateTime.Now - _lastActionTime).TotalMilliseconds > 100)
            {
                _actions.Add(new ActionModel
                {
                    Type = "Wait",
                    Param = (int)(DateTime.Now - _lastActionTime).TotalMilliseconds
                });
            }

            _actions.Add(action);
            _lastActionTime = DateTime.Now;
        }

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        public void Dispose()
        {
            StopRecording();
        }
    }
}

