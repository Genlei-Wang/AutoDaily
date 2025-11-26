using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using AutoDaily.Core.Models;
using AutoDaily.Core.Native;

namespace AutoDaily.Core.Engine
{
    public class Recorder : IDisposable
    {
        private IntPtr _mouseHook = IntPtr.Zero;
        private IntPtr _keyboardHook = IntPtr.Zero;
        private User32.LowLevelProc _mouseProc;
        private User32.LowLevelProc _keyboardProc;
        private List<Action> _actions = new List<Action>();
        private DateTime _lastActionTime = DateTime.Now;
        private const int DEBOUNCE_MS = 50;
        private bool _isRecording = false;
        private WindowInfo _targetWindow;

        public event Action<List<Action>, WindowInfo> OnRecordingComplete;
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

            UnhookWindowsHookEx(_mouseHook);
            UnhookWindowsHookEx(_keyboardHook);

            _mouseHook = IntPtr.Zero;
            _keyboardHook = IntPtr.Zero;

            OnStatusUpdate?.Invoke("录制完成");
            OnRecordingComplete?.Invoke(_actions, _targetWindow);
        }

        private void CaptureCurrentWindow()
        {
            var hwnd = User32.GetForegroundWindow();
            if (hwnd != IntPtr.Zero)
            {
                User32.GetWindowRect(hwnd, out var rect);
                _targetWindow = new WindowInfo
                {
                    Rect = new WindowRect
                    {
                        Width = rect.Right - rect.Left,
                        Height = rect.Bottom - rect.Top
                    }
                };

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

                var hwnd = User32.GetForegroundWindow();
                if (hwnd != IntPtr.Zero)
                {
                    User32.GetWindowRect(hwnd, out var rect);
                    User32.GetCursorPos(out var point);

                    // 转换为相对坐标
                    int relX = point.X - rect.Left;
                    int relY = point.Y - rect.Top;

                    if (wParam == (IntPtr)User32.WM_LBUTTONDOWN)
                    {
                        AddAction(new Action
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
                        AddAction(new Action
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

        private void AddAction(Action action)
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
                    _actions.Add(new Action
                    {
                        Type = "Wait",
                        Param = (int)timeSinceLast
                    });
                }
            }
            else if ((DateTime.Now - _lastActionTime).TotalMilliseconds > 100)
            {
                _actions.Add(new Action
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

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        public void Dispose()
        {
            StopRecording();
        }
    }
}

