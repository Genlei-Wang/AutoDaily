using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using AutoDaily.Core.Models;
using AutoDaily.Core.Native;
using TaskModel = AutoDaily.Core.Models.Task;
using ActionModel = AutoDaily.Core.Models.Action;

namespace AutoDaily.Core.Engine
{
    public class Player
    {
        private CancellationTokenSource _cancellationTokenSource;
        public event Action<string> OnStatusUpdate;
        public event Action<int, int> OnProgressUpdate; // current, total

        public bool IsRunning { get; private set; }

        public async Task RunAsync(TaskModel task, CancellationToken cancellationToken)
        {
            if (IsRunning)
                return;

            IsRunning = true;
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            try
            {
                await Task.Run(() => ExecuteTask(task, _cancellationTokenSource.Token));
            }
            finally
            {
                IsRunning = false;
            }
        }

        private void ExecuteTask(TaskModel task, CancellationToken token)
        {
            // 1. 查找目标窗口
            IntPtr hwnd = FindTargetWindow(task.TargetWindow);
            if (hwnd == IntPtr.Zero)
            {
                OnStatusUpdate?.Invoke("错误：找不到目标窗口");
                return;
            }

            // 2. 调整窗口大小和位置
            AdjustWindow(hwnd, task.TargetWindow.Rect);

            // 3. 激活窗口
            User32.ShowWindow(hwnd, User32.SW_RESTORE);
            User32.SetForegroundWindow(hwnd);
            Thread.Sleep(500); // 等待窗口激活

            // 4. 执行动作序列
            int totalActions = task.Actions.Count;
            for (int i = 0; i < task.Actions.Count; i++)
            {
                if (token.IsCancellationRequested)
                {
                    OnStatusUpdate?.Invoke("已停止");
                    return;
                }

                var action = task.Actions[i];
                OnProgressUpdate?.Invoke(i + 1, totalActions);
                OnStatusUpdate?.Invoke($"执行步骤 {i + 1}/{totalActions}: {action.Type}");

                ExecuteAction(action, hwnd, token);
            }

            OnStatusUpdate?.Invoke("执行完成");
        }

        private IntPtr FindTargetWindow(WindowInfo windowInfo)
        {
            // 优先通过进程名查找
            if (!string.IsNullOrEmpty(windowInfo.ProcessName))
            {
                var processes = Process.GetProcessesByName(windowInfo.ProcessName.Replace(".exe", ""));
                if (processes.Length > 0)
                {
                    return processes[0].MainWindowHandle;
                }
            }

            // 通过窗口标题查找
            if (!string.IsNullOrEmpty(windowInfo.Title))
            {
                return User32.FindWindow(null, windowInfo.Title);
            }

            return IntPtr.Zero;
        }

        private void AdjustWindow(IntPtr hwnd, WindowRect rect)
        {
            User32.GetWindowRect(hwnd, out var currentRect);
            
            // 如果窗口大小不匹配，调整大小
            int currentWidth = currentRect.Right - currentRect.Left;
            int currentHeight = currentRect.Bottom - currentRect.Top;

            if (currentWidth != rect.Width || currentHeight != rect.Height)
            {
                User32.SetWindowPos(
                    hwnd,
                    User32.HWND_TOP,
                    currentRect.Left,
                    currentRect.Top,
                    rect.Width,
                    rect.Height,
                    User32.SWP_SHOWWINDOW);
                
                Thread.Sleep(200); // 等待窗口调整完成
            }
        }

        private void ExecuteAction(ActionModel action, IntPtr hwnd, CancellationToken token)
        {
            switch (action.Type)
            {
                case "Wait":
                    Thread.Sleep(action.Param);
                    break;

                case "MouseClick":
                    PerformMouseClick(action, hwnd);
                    break;

                case "MouseMove":
                    PerformMouseMove(action, hwnd);
                    break;

                case "Input":
                    PerformInput(action.Text);
                    break;
            }
        }

        private void PerformMouseClick(ActionModel action, IntPtr hwnd)
        {
            User32.GetWindowRect(hwnd, out var rect);
            
            int screenX, screenY;
            if (action.Relative)
            {
                screenX = rect.Left + action.X;
                screenY = rect.Top + action.Y;
            }
            else
            {
                screenX = action.X;
                screenY = action.Y;
            }

            // 移动鼠标
            User32.SetCursorPos(screenX, screenY);
            Thread.Sleep(50);

            // 执行点击
            var inputs = new User32.INPUT[2];
            
            // 按下
            inputs[0] = new User32.INPUT
            {
                type = User32.INPUT_MOUSE,
                U = new User32.InputUnion
                {
                    mi = new User32.MOUSEINPUT
                    {
                        dx = 0,
                        dy = 0,
                        dwFlags = action.Button == "Left" 
                            ? User32.MOUSEEVENTF_LEFTDOWN 
                            : User32.MOUSEEVENTF_RIGHTDOWN,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };

            // 释放
            inputs[1] = new User32.INPUT
            {
                type = User32.INPUT_MOUSE,
                U = new User32.InputUnion
                {
                    mi = new User32.MOUSEINPUT
                    {
                        dx = 0,
                        dy = 0,
                        dwFlags = action.Button == "Left"
                            ? User32.MOUSEEVENTF_LEFTUP
                            : User32.MOUSEEVENTF_RIGHTUP,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };

            User32.SendInput(2, inputs, Marshal.SizeOf(typeof(User32.INPUT)));
            Thread.Sleep(100);
        }

        private void PerformMouseMove(ActionModel action, IntPtr hwnd)
        {
            User32.GetWindowRect(hwnd, out var rect);
            
            int screenX, screenY;
            if (action.Relative)
            {
                screenX = rect.Left + action.X;
                screenY = rect.Top + action.Y;
            }
            else
            {
                screenX = action.X;
                screenY = action.Y;
            }

            User32.SetCursorPos(screenX, screenY);
            Thread.Sleep(50);
        }

        private void PerformInput(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            foreach (char c in text)
            {
                // 简化处理：使用SendKeys（实际应该用SendInput处理所有字符）
                System.Windows.Forms.SendKeys.SendWait(c.ToString());
                Thread.Sleep(20);
            }
        }

        public void Stop()
        {
            _cancellationTokenSource?.Cancel();
        }
    }
}

