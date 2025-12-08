using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using AutoDaily.Core.Models;
using AutoDaily.Core.Native;
using AutoDaily.Core.Services;
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

        /// <summary>
        /// 捕获当前活动窗口信息（录制开始时调用）
        /// 关键：必须准确保存窗口位置，用于后续计算相对坐标
        /// </summary>
        private void CaptureCurrentWindow()
        {
            var hwnd = User32.GetForegroundWindow();
            if (hwnd == IntPtr.Zero || !User32.IsWindow(hwnd))
            {
                LogService.LogWarning("录制开始时无法获取活动窗口");
                return;
            }
            
            if (_targetWindow == null)
            {
                _targetWindow = new WindowInfo();
            }
            
            // 获取窗口位置和大小（关键：必须准确获取，用于后续相对坐标计算）
            if (!User32.GetWindowRect(hwnd, out var rect))
            {
                LogService.LogWarning("录制开始时无法获取窗口位置");
                return;
            }
            
            if (_targetWindow.Rect == null)
            {
                _targetWindow.Rect = new WindowRect();
            }
            
            // 保存窗口的完整位置和大小信息（这是坐标计算的基础）
            _targetWindow.Rect.Width = rect.Right - rect.Left;
            _targetWindow.Rect.Height = rect.Bottom - rect.Top;
            _targetWindow.WindowLeft = rect.Left;  // 关键：窗口左上角的屏幕X坐标
            _targetWindow.WindowTop = rect.Top;   // 关键：窗口左上角的屏幕Y坐标

            // 获取窗口标题（用于窗口识别）
            int length = User32.GetWindowTextLength(hwnd);
            if (length > 0)
            {
                StringBuilder sb = new StringBuilder(length + 1);
                User32.GetWindowText(hwnd, sb, sb.Capacity);
                _targetWindow.Title = sb.ToString();
            }

            // 获取进程名（用于窗口识别，优先级最高）
            try
            {
                GetWindowThreadProcessId(hwnd, out uint processId);
                var process = Process.GetProcessById((int)processId);
                _targetWindow.ProcessName = process.ProcessName;
                LogService.Log($"录制目标窗口: {_targetWindow.ProcessName}, 位置({_targetWindow.WindowLeft},{_targetWindow.WindowTop}), 大小({_targetWindow.Rect.Width}x{_targetWindow.Rect.Height})");
            }
            catch (Exception ex)
            {
                LogService.LogWarning($"获取进程名失败: {ex.Message}");
            }
        }

        private IntPtr MouseHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && _isRecording)
            {
                var timeSinceLastAction = (DateTime.Now - _lastActionTime).TotalMilliseconds;

                // 获取鼠标位置（屏幕绝对坐标）
                User32.GetCursorPos(out var point);
                
                // 关键修复：录制时应该始终使用录制开始时捕获的目标窗口
                // 而不是每次重新获取窗口（可能获取到错误的窗口，导致坐标计算错误）
                IntPtr targetHwnd = IntPtr.Zero;
                int windowLeft = 0;
                int windowTop = 0;
                
                // 优先使用录制开始时保存的目标窗口
                if (_targetWindow != null && !string.IsNullOrEmpty(_targetWindow.ProcessName))
                {
                    try
                    {
                        var processes = System.Diagnostics.Process.GetProcessesByName(
                            _targetWindow.ProcessName.Replace(".exe", ""));
                        if (processes.Length > 0)
                        {
                            targetHwnd = processes[0].MainWindowHandle;
                            if (targetHwnd != IntPtr.Zero && User32.IsWindow(targetHwnd))
                            {
                                // 使用录制时保存的窗口位置（关键！）
                                windowLeft = _targetWindow.WindowLeft;
                                windowTop = _targetWindow.WindowTop;
                            }
                        }
                    }
                    catch { }
                }
                
                // 如果目标窗口无效，尝试获取当前鼠标下的窗口（后备方案）
                if (targetHwnd == IntPtr.Zero || windowLeft == 0 || windowTop == 0)
                {
                    targetHwnd = User32.WindowFromPoint(point);
                    if (targetHwnd == IntPtr.Zero)
                    {
                        targetHwnd = User32.GetForegroundWindow();
                    }
                    
                    if (targetHwnd != IntPtr.Zero && User32.GetWindowRect(targetHwnd, out var rect))
                    {
                        windowLeft = rect.Left;
                        windowTop = rect.Top;
                    }
                    else
                    {
                        // 无法获取窗口，跳过此次操作
                        return User32.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
                    }
                }
                
                // 计算相对坐标：使用录制时保存的窗口位置（关键修复！）
                int relX = point.X - windowLeft;
                int relY = point.Y - windowTop;
                
                // 验证相对坐标是否合理（防止计算错误）
                if (relX < -1000 || relX > 10000 || relY < -1000 || relY > 10000)
                {
                    // 坐标异常，跳过
                    LogService.LogWarning($"警告: 相对坐标异常 ({relX}, {relY})，窗口位置({windowLeft},{windowTop})，鼠标位置({point.X},{point.Y})，跳过");
                    return User32.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
                }

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

                    // 记录可见字符输入（字母、数字、符号等）
                    // 使用ToUnicode将虚拟键码转换为字符，支持所有可打印字符
                    try
                    {
                        byte[] keyboardState = new byte[256];
                        if (!User32.GetKeyboardState(keyboardState))
                        {
                            return User32.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
                        }
                        
                        StringBuilder sb = new StringBuilder(10);
                        int result = User32.ToUnicode((uint)vkCode, 0, keyboardState, sb, sb.Capacity, 0);
                        
                        if (result > 0 && sb.Length > 0)
                        {
                            char ch = sb[0];
                            // 记录所有可打印字符（包括字母、数字、标点、符号、空格等）
                            // 排除控制字符（如回车、换行等，这些已作为特殊键处理）
                            if (!char.IsControl(ch) && (char.IsLetterOrDigit(ch) || char.IsPunctuation(ch) || 
                                char.IsSymbol(ch) || char.IsWhiteSpace(ch) || ch > 127))
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
                        else if (result < 0)
                        {
                            // 死键（dead key），需要等待下一个按键
                            // 暂时跳过，等待下一个按键组合
                        }
                    }
                    catch (Exception ex)
                    {
                        // 如果转换失败，静默处理（某些特殊键无法转换为字符是正常的）
                        System.Diagnostics.Debug.WriteLine($"字符转换失败 (VK={vkCode}): {ex.Message}");
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

