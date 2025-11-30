using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using AutoDaily.Core.Models;
using AutoDaily.Core.Native;
using AutoDaily.Core.Services;
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

        public async System.Threading.Tasks.Task RunAsync(TaskModel task, CancellationToken cancellationToken)
        {
            if (IsRunning)
                return;

            IsRunning = true;
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            try
            {
                await System.Threading.Tasks.Task.Run(() => ExecuteTask(task, _cancellationTokenSource.Token));
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
                LogService.LogError("找不到目标窗口", null);
                return;
            }

            // 验证窗口句柄有效
            if (!User32.IsWindow(hwnd))
            {
                OnStatusUpdate?.Invoke("错误：目标窗口无效");
                LogService.LogError("目标窗口无效", null);
                return;
            }

            // 2. 调整窗口大小和位置
            if (task.TargetWindow?.Rect != null)
            {
                AdjustWindow(hwnd, task.TargetWindow.Rect);
            }

            // 3. 激活窗口
            User32.ShowWindow(hwnd, User32.SW_RESTORE);
            User32.SetForegroundWindow(hwnd);
            Thread.Sleep(500); // 等待窗口激活

            // 再次验证窗口位置
            User32.GetWindowRect(hwnd, out var windowRect);
            LogService.Log($"目标窗口位置: Left={windowRect.Left}, Top={windowRect.Top}, Width={windowRect.Right - windowRect.Left}, Height={windowRect.Bottom - windowRect.Top}");

            // 4. 执行动作序列
            if (task.Actions == null || task.Actions.Count == 0)
            {
                OnStatusUpdate?.Invoke("错误：没有录制的动作");
                return;
            }

            int totalActions = task.Actions.Count;
            for (int i = 0; i < task.Actions.Count; i++)
            {
                if (token.IsCancellationRequested)
                {
                    OnStatusUpdate?.Invoke("已停止");
                    return;
                }

                var action = task.Actions[i];
                if (action == null)
                    continue;

                // 严格按步骤更新进度
                int currentStep = i + 1;
                OnProgressUpdate?.Invoke(currentStep, totalActions);
                OnStatusUpdate?.Invoke($"执行步骤 {currentStep}/{totalActions}: {action.Type}");
                
                // 记录屏幕操作
                LogService.LogScreenAction($"{action.Type} (X:{action.X}, Y:{action.Y}, Relative:{action.Relative})", currentStep);

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
            // 重新获取窗口位置（窗口可能移动了）
            if (!User32.GetWindowRect(hwnd, out var rect))
            {
                LogService.LogError($"无法获取窗口位置，hwnd={hwnd}", null);
                return;
            }
            
            int screenX, screenY;
            if (action.Relative)
            {
                screenX = rect.Left + action.X;
                screenY = rect.Top + action.Y;
                LogService.Log($"相对坐标转换: 窗口({rect.Left},{rect.Top}) + 相对({action.X},{action.Y}) = 屏幕({screenX},{screenY})");
            }
            else
            {
                screenX = action.X;
                screenY = action.Y;
                LogService.Log($"绝对坐标: 屏幕({screenX},{screenY})");
            }

            // 验证坐标在屏幕范围内
            var screenBounds = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
            int originalX = screenX;
            int originalY = screenY;
            screenX = Math.Max(0, Math.Min(screenX, screenBounds.Width - 1));
            screenY = Math.Max(0, Math.Min(screenY, screenBounds.Height - 1));
            
            if (originalX != screenX || originalY != screenY)
            {
                LogService.LogWarning($"坐标被限制: ({originalX},{originalY}) -> ({screenX},{screenY})");
            }

            // 平滑移动鼠标（参考TinyTask的实现）
            SmoothMoveMouse(screenX, screenY);
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
            // 重新获取窗口位置
            if (!User32.GetWindowRect(hwnd, out var rect))
            {
                return;
            }
            
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

            // 验证坐标在屏幕范围内
            var screenBounds = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
            screenX = Math.Max(0, Math.Min(screenX, screenBounds.Width - 1));
            screenY = Math.Max(0, Math.Min(screenY, screenBounds.Height - 1));

            // 平滑移动鼠标
            SmoothMoveMouse(screenX, screenY);
            Thread.Sleep(50);
        }

        private void SmoothMoveMouse(int targetX, int targetY)
        {
            // 获取当前鼠标位置
            User32.GetCursorPos(out var currentPoint);
            int currentX = currentPoint.X;
            int currentY = currentPoint.Y;

            // 计算距离
            int dx = targetX - currentX;
            int dy = targetY - currentY;
            double distance = Math.Sqrt(dx * dx + dy * dy);

            // 如果距离很小，直接移动
            if (distance < 5)
            {
                User32.SetCursorPos(targetX, targetY);
                return;
            }

            // 平滑移动：分多步移动（参考TinyTask）
            int steps = Math.Max(5, (int)(distance / 10)); // 每10像素一步
            for (int i = 1; i <= steps; i++)
            {
                double ratio = (double)i / steps;
                int x = currentX + (int)(dx * ratio);
                int y = currentY + (int)(dy * ratio);
                User32.SetCursorPos(x, y);
                Thread.Sleep(5); // 每步间隔5ms，实现平滑效果
            }
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

