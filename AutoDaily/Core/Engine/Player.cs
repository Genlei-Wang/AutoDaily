using System;
using System.Collections.Generic;
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
        private TaskModel _currentTask; // 保存当前任务，用于坐标计算
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
            _currentTask = task; // 保存当前任务引用
            
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

            // 2. 调整窗口大小和位置（恢复到录制时的状态）
            if (task.TargetWindow?.Rect != null)
            {
                AdjustWindow(hwnd, task.TargetWindow.Rect, task.TargetWindow);
            }

            // 3. 激活窗口
            User32.ShowWindow(hwnd, User32.SW_RESTORE);
            User32.SetForegroundWindow(hwnd);
            Thread.Sleep(500); // 等待窗口激活

            // 再次验证窗口位置和大小
            User32.GetWindowRect(hwnd, out var windowRect);
            int currentWidth = windowRect.Right - windowRect.Left;
            int currentHeight = windowRect.Bottom - windowRect.Top;
            LogService.Log($"目标窗口位置: Left={windowRect.Left}, Top={windowRect.Top}, Width={currentWidth}, Height={currentHeight}");
            
            // 如果窗口位置或大小与录制时不一致，记录警告并尝试修正
            if (task.TargetWindow != null)
            {
                bool positionMismatch = (task.TargetWindow.WindowLeft != 0 && task.TargetWindow.WindowTop != 0) &&
                    (windowRect.Left != task.TargetWindow.WindowLeft || windowRect.Top != task.TargetWindow.WindowTop);
                
                bool sizeMismatch = task.TargetWindow.Rect != null &&
                    (currentWidth != task.TargetWindow.Rect.Width || currentHeight != task.TargetWindow.Rect.Height);
                
                if (positionMismatch || sizeMismatch)
                {
                    LogService.LogWarning($"窗口状态不匹配: 录制时位置({task.TargetWindow.WindowLeft},{task.TargetWindow.WindowTop}) 大小({task.TargetWindow.Rect?.Width}x{task.TargetWindow.Rect?.Height}) vs 当前位置({windowRect.Left},{windowRect.Top}) 大小({currentWidth}x{currentHeight})");
                    
                    // 尝试恢复到录制时的位置和大小
                    if (task.TargetWindow.WindowLeft != 0 && task.TargetWindow.WindowTop != 0 && task.TargetWindow.Rect != null)
                    {
                        User32.SetWindowPos(
                            hwnd,
                            User32.HWND_TOP,
                            task.TargetWindow.WindowLeft,
                            task.TargetWindow.WindowTop,
                            task.TargetWindow.Rect.Width,
                            task.TargetWindow.Rect.Height,
                            User32.SWP_SHOWWINDOW);
                        Thread.Sleep(200);
                        LogService.Log($"已尝试恢复窗口到录制时的位置和大小");
                    }
                }
            }

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
                    // 优先使用主窗口句柄，如果不存在则尝试查找第一个可见窗口
                    var hwnd = processes[0].MainWindowHandle;
                    if (hwnd != IntPtr.Zero && User32.IsWindow(hwnd))
                    {
                        return hwnd;
                    }
                    
                    // 如果主窗口句柄无效，尝试查找第一个可见窗口
                    foreach (var process in processes)
                    {
                        hwnd = process.MainWindowHandle;
                        if (hwnd != IntPtr.Zero && User32.IsWindow(hwnd))
                        {
                            return hwnd;
                        }
                    }
                }
            }

            // 通过窗口标题查找
            if (!string.IsNullOrEmpty(windowInfo.Title))
            {
                var hwnd = User32.FindWindow(null, windowInfo.Title);
                if (hwnd != IntPtr.Zero && User32.IsWindow(hwnd))
                {
                    return hwnd;
                }
            }

            return IntPtr.Zero;
        }

        private void AdjustWindow(IntPtr hwnd, WindowRect rect, WindowInfo windowInfo)
        {
            User32.GetWindowRect(hwnd, out var currentRect);
            
            int currentWidth = currentRect.Right - currentRect.Left;
            int currentHeight = currentRect.Bottom - currentRect.Top;
            
            // 确定目标位置：优先使用录制时的位置，否则保持当前位置
            int targetLeft = (windowInfo != null && windowInfo.WindowLeft != 0) 
                ? windowInfo.WindowLeft 
                : currentRect.Left;
            int targetTop = (windowInfo != null && windowInfo.WindowTop != 0) 
                ? windowInfo.WindowTop 
                : currentRect.Top;

            // 如果窗口大小或位置不匹配，调整
            if (currentWidth != rect.Width || currentHeight != rect.Height || 
                currentRect.Left != targetLeft || currentRect.Top != targetTop)
            {
                User32.SetWindowPos(
                    hwnd,
                    User32.HWND_TOP,
                    targetLeft,
                    targetTop,
                    rect.Width,
                    rect.Height,
                    User32.SWP_SHOWWINDOW);
                
                Thread.Sleep(300); // 等待窗口调整完成
                LogService.Log($"窗口已调整: 位置({targetLeft},{targetTop}) 大小({rect.Width}x{rect.Height})");
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

                case "MouseWheel":
                    PerformMouseWheel(action, hwnd);
                    break;

                case "KeyPress":
                    PerformKeyPress(action);
                    break;
            }
        }

        /// <summary>
        /// 执行鼠标点击操作
        /// 使用平滑移动复现运动轨迹，然后点击（参考TinyTask：完整复现hover动作）
        /// </summary>
        private void PerformMouseClick(ActionModel action, IntPtr hwnd)
        {
            // 使用绝对坐标
            int screenX = action.X;
            int screenY = action.Y;
            
            // 验证坐标在屏幕范围内
            var screenBounds = System.Windows.Forms.SystemInformation.VirtualScreen;
            if (screenX < screenBounds.Left || screenX > screenBounds.Right || 
                screenY < screenBounds.Top || screenY > screenBounds.Bottom)
            {
                LogService.LogWarning($"警告: 屏幕坐标({screenX},{screenY})超出屏幕范围，已限制");
                screenX = Math.Max(screenBounds.Left, Math.Min(screenX, screenBounds.Right - 1));
                screenY = Math.Max(screenBounds.Top, Math.Min(screenY, screenBounds.Bottom - 1));
            }
            
            // 使用平滑移动复现运动轨迹（参考TinyTask：完整复现hover动作）
            SmoothMoveMouse(screenX, screenY);
            
            // 确保鼠标精确移动到目标位置（关键：点击前必须精确到位）
            User32.GetCursorPos(out var finalPos);
            if (Math.Abs(finalPos.X - screenX) > 2 || Math.Abs(finalPos.Y - screenY) > 2)
            {
                // 如果位置不精确，直接移动到目标位置
                User32.SetCursorPos(screenX, screenY);
                Thread.Sleep(5);
            }
            else
            {
                Thread.Sleep(10);  // 短暂延迟，确保鼠标移动到位
            }
            
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

        /// <summary>
        /// 执行鼠标移动操作
        /// 使用平滑移动复现运动轨迹（参考TinyTask：完整复现hover动作和运动轨迹）
        /// </summary>
        private void PerformMouseMove(ActionModel action, IntPtr hwnd)
        {
            // 使用绝对坐标
            int screenX = action.X;
            int screenY = action.Y;
            
            // 验证坐标在屏幕范围内
            var screenBounds = System.Windows.Forms.SystemInformation.VirtualScreen;
            if (screenX < screenBounds.Left || screenX > screenBounds.Right || 
                screenY < screenBounds.Top || screenY > screenBounds.Bottom)
            {
                LogService.LogWarning($"警告: 鼠标移动坐标超出屏幕范围，已限制");
                screenX = Math.Max(screenBounds.Left, Math.Min(screenX, screenBounds.Right - 1));
                screenY = Math.Max(screenBounds.Top, Math.Min(screenY, screenBounds.Bottom - 1));
            }

            // 使用平滑移动复现运动轨迹（参考TinyTask）
            SmoothMoveMouse(screenX, screenY);
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
            if (distance < 2)
            {
                User32.SetCursorPos(targetX, targetY);
                return;
            }

            // 平滑移动：分多步移动（参考TinyTask：完整复现运动轨迹）
            // 根据距离动态计算步数，确保轨迹平滑自然
            // 优化：减少步数间隔，提高移动频率，但保持平滑
            int steps = Math.Max(2, Math.Min(30, (int)(distance / 3))); // 每3像素一步，最多30步
            for (int i = 1; i <= steps; i++)
            {
                double ratio = (double)i / steps;
                // 使用缓动函数（ease-in-out）使移动更自然
                double easedRatio = ratio < 0.5 
                    ? 2 * ratio * ratio 
                    : 1 - Math.Pow(-2 * ratio + 2, 2) / 2;
                
                int x = currentX + (int)(dx * easedRatio);
                int y = currentY + (int)(dy * easedRatio);
                User32.SetCursorPos(x, y);
                Thread.Sleep(2); // 每步间隔2ms，提高移动频率，减少卡顿
            }
            
            // 确保最终位置准确（关键：必须精确到位）
            User32.SetCursorPos(targetX, targetY);
        }

        private void PerformInput(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            try
            {
                // 使用SendInput发送字符，支持所有字符（包括中文、符号等）
                foreach (char c in text)
                {
                    // 对于ASCII字符，使用虚拟键码
                    if (c <= 127)
                    {
                        // 使用VkKeyScan获取虚拟键码
                        short vkScan = User32.VkKeyScan(c);
                        if (vkScan != -1)
                        {
                            byte vkCode = (byte)(vkScan & 0xFF);
                            byte shiftState = (byte)((vkScan >> 8) & 0xFF);
                            
                            var inputs = new List<User32.INPUT>();
                            
                            // 如果需要Shift键（大写字母、符号等）
                            if ((shiftState & 1) != 0)
                            {
                                inputs.Add(CreateKeyInput(User32.VK_SHIFT, false));
                            }
                            
                            // 按下字符键
                            inputs.Add(CreateKeyInput(vkCode, false));
                            inputs.Add(CreateKeyInput(vkCode, true));
                            
                            // 释放Shift键
                            if ((shiftState & 1) != 0)
                            {
                                inputs.Add(CreateKeyInput(User32.VK_SHIFT, true));
                            }
                            
                            User32.SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf(typeof(User32.INPUT)));
                        }
                        else
                        {
                            // 如果VkKeyScan失败，尝试使用SendKeys作为后备
                            System.Windows.Forms.SendKeys.SendWait(c.ToString());
                        }
                    }
                    else
                    {
                        // 对于非ASCII字符（如中文），使用SendKeys
                        System.Windows.Forms.SendKeys.SendWait(c.ToString());
                    }
                    
                    Thread.Sleep(20);
                }
            }
            catch (Exception ex)
            {
                LogService.LogError($"输入模拟失败: {text}", ex);
                OnStatusUpdate?.Invoke($"输入出错: {ex.Message}");
            }
        }

        private void PerformMouseWheel(ActionModel action, IntPtr hwnd)
        {
            // 使用绝对坐标，直接定位
            int screenX = action.X;
            int screenY = action.Y;
            
            // 验证坐标在屏幕范围内
            var screenBounds = System.Windows.Forms.SystemInformation.VirtualScreen;
            if (screenX < screenBounds.Left || screenX > screenBounds.Right || 
                screenY < screenBounds.Top || screenY > screenBounds.Bottom)
            {
                screenX = Math.Max(screenBounds.Left, Math.Min(screenX, screenBounds.Right - 1));
                screenY = Math.Max(screenBounds.Top, Math.Min(screenY, screenBounds.Bottom - 1));
            }

            // 直接定位鼠标
            User32.SetCursorPos(screenX, screenY);
            Thread.Sleep(10);

            // 执行滚轮操作
            var inputs = new User32.INPUT[1];
            inputs[0] = new User32.INPUT
            {
                type = User32.INPUT_MOUSE,
                U = new User32.InputUnion
                {
                    mi = new User32.MOUSEINPUT
                    {
                        dx = 0,
                        dy = 0,
                        mouseData = (uint)action.Param,
                        dwFlags = User32.MOUSEEVENTF_WHEEL,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };
            User32.SendInput(1, inputs, Marshal.SizeOf(typeof(User32.INPUT)));
            Thread.Sleep(50);
        }

        private void PerformKeyPress(ActionModel action)
        {
            int vkCode = action.Param;
            
            // 处理修饰键组合（包括Win键）
            bool ctrl = action.Text?.Contains("Ctrl+") == true;
            bool shift = action.Text?.Contains("Shift+") == true;
            bool alt = action.Text?.Contains("Alt+") == true;
            bool win = action.Text?.Contains("Win+") == true || action.Text == "Win";
            
            // 如果是Win键本身（没有其他键）
            if ((vkCode == User32.VK_LWIN || vkCode == User32.VK_RWIN) && !win && string.IsNullOrEmpty(action.Text))
            {
                // Win键单独按下
                var inputs = new User32.INPUT[2];
                inputs[0] = CreateKeyInput(vkCode, false);
                inputs[1] = CreateKeyInput(vkCode, true);
                User32.SendInput(2, inputs, Marshal.SizeOf(typeof(User32.INPUT)));
                Thread.Sleep(50);
                return;
            }

            var inputsList = new List<User32.INPUT>();

            // 同时按下所有修饰键（关键：必须同时按下，不能挨个按下）
            if (ctrl)
            {
                inputsList.Add(CreateKeyInput(User32.VK_CONTROL, false));
            }
            if (shift)
            {
                inputsList.Add(CreateKeyInput(User32.VK_SHIFT, false));
            }
            if (alt)
            {
                inputsList.Add(CreateKeyInput(User32.VK_ALT, false));
            }
            if (win)
            {
                // Win键通常使用左Win键
                inputsList.Add(CreateKeyInput(User32.VK_LWIN, false));
            }

            // 按下主键（与其他修饰键同时）
            inputsList.Add(CreateKeyInput(vkCode, false));

            // 一次性发送所有按下事件（确保同时按下）
            if (inputsList.Count > 0)
            {
                User32.SendInput((uint)inputsList.Count, inputsList.ToArray(), Marshal.SizeOf(typeof(User32.INPUT)));
                Thread.Sleep(10); // 短暂延迟，确保按键被识别
            }

            // 释放主键
            var releaseInputs = new List<User32.INPUT>();
            releaseInputs.Add(CreateKeyInput(vkCode, true));

            // 同时释放所有修饰键（逆序释放）
            if (win)
            {
                releaseInputs.Add(CreateKeyInput(User32.VK_LWIN, true));
            }
            if (alt)
            {
                releaseInputs.Add(CreateKeyInput(User32.VK_ALT, true));
            }
            if (shift)
            {
                releaseInputs.Add(CreateKeyInput(User32.VK_SHIFT, true));
            }
            if (ctrl)
            {
                releaseInputs.Add(CreateKeyInput(User32.VK_CONTROL, true));
            }

            // 一次性发送所有释放事件（确保同时释放）
            if (releaseInputs.Count > 0)
            {
                User32.SendInput((uint)releaseInputs.Count, releaseInputs.ToArray(), Marshal.SizeOf(typeof(User32.INPUT)));
            }
            
            Thread.Sleep(50);
        }

        private User32.INPUT CreateKeyInput(int vkCode, bool keyUp)
        {
            return new User32.INPUT
            {
                type = User32.INPUT_KEYBOARD,
                U = new User32.InputUnion
                {
                    ki = new User32.KEYBDINPUT
                    {
                        wVk = (ushort)vkCode,
                        wScan = 0,
                        dwFlags = keyUp ? User32.KEYEVENTF_KEYUP : 0,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };
        }

        public void Stop()
        {
            _cancellationTokenSource?.Cancel();
        }
    }
}

