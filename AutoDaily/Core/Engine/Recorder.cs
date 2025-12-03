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
                
                _targetWindow.Rect.Width = rect.Right - rect.Left;
                _targetWindow.Rect.Height = rect.Bottom - rect.Top;

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

                if (timeSinceLastAction < DEBOUNCE_MS)
                    return User32.CallNextHookEx(_mouseHook, nCode, wParam, lParam);

                // 获取鼠标点击时的窗口（使用WindowFromPoint更准确）
                User32.GetCursorPos(out var point);
                var hwnd = User32.WindowFromPoint(point);
                
                // 如果WindowFromPoint返回0，则使用前台窗口
                if (hwnd == IntPtr.Zero)
                {
                    hwnd = User32.GetForegroundWindow();
                }
                
                if (hwnd != IntPtr.Zero)
                {
                    // 获取窗口的客户区矩形（更准确的坐标计算）
                    User32.GetWindowRect(hwnd, out var rect);
                    
                    // 转换为相对坐标（相对于窗口客户区）
                    int relX = point.X - rect.Left;
                    int relY = point.Y - rect.Top;
                    
                    // 记录日志以便调试
                    System.Diagnostics.Debug.WriteLine($"录制点击: 屏幕({point.X},{point.Y}) 窗口({rect.Left},{rect.Top}) 相对({relX},{relY})");

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
                }
            }

            return User32.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        }

        private IntPtr KeyboardHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && _isRecording)
            {
                // 只记录按键按下，忽略按键释放
                if (wParam == (IntPtr)User32.WM_KEYDOWN || wParam == (IntPtr)User32.WM_SYSKEYDOWN)
                {
                    int vkCode = Marshal.ReadInt32(lParam);
                    
                    // 忽略功能键（F1-F12等）
                    if (vkCode >= 0x70 && vkCode <= 0x7B)
                        return User32.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);

                    // 这里简化处理：只记录可见字符输入
                    // 实际应该记录完整的按键序列，但为了简化，我们主要依赖鼠标点击
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

