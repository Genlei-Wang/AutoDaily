using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using AutoDaily.Core.Engine;
using AutoDaily.Core.Models;
using AutoDaily.Core.Native;
using AutoDaily.Core.Services;
using AutoDaily.UI.Controls;
using System.Runtime.InteropServices;

namespace AutoDaily.UI.Forms
{
    public partial class MainForm : Form
    {
        private TaskService _taskService;
        private ScheduleService _scheduleService;
        private Recorder _recorder;
        private Player _player;
        private OverlayForm _overlayForm;
        private RunningOverlayForm _runningOverlay;
        private CancellationTokenSource _playerCancellationTokenSource;

        // UI控件
        private Label _statusIndicator;
        private Button _recordButton;
        private Button _runButton;
        private Panel _operationCard;
        private Panel _scheduleCard;
        private ToggleSwitch _scheduleToggle;
        private Label _scheduleTimeLabel;
        private Label _nextRunLabel;
        private DateTimePicker _timePicker;

        private bool _isRecording = false;
        private bool _isRunning = false;
        private IntPtr _hotkeyHook = IntPtr.Zero;
        private User32.LowLevelProc _hotkeyHookProc;
        private NotifyIcon _notifyIcon; // 系统托盘图标

        // 字号规范常量（参考 Apple Human Interface Guidelines）
        // 原则：清晰易读、层次分明、最小字号不小于 11pt
        private const float FONT_SIZE_TITLE = 16f;      // 标题（状态指示灯）- 增大以提高可读性
        private const float FONT_SIZE_BUTTON = 14f;     // 按钮文字 - 主要操作，需要突出
        private const float FONT_SIZE_LABEL = 12f;      // 标签文字（定时运行、每天等）- 重要信息
        private const float FONT_SIZE_HINT = 11f;       // 提示文字（录制新动作、运行跑一遍）- 最小字号
        private const float FONT_SIZE_TIME = 12f;       // 时间选择器 - 重要信息
        private const float FONT_SIZE_NEXT_RUN = 11f;   // 下次运行提示 - 次要信息
        private const float FONT_SIZE_WARNING = 11f;    // 警告提示 - 需要清晰可见

        public MainForm()
        {
            InitializeComponent();
            InitializeServices();
            InitializeNotifyIcon();
            LoadTaskData();
            RegisterHotKey();
        }

        private void InitializeComponent()
        {
            Text = "AutoDaily 日报助手";
            
            // 使用DPI模式进行缩放，确保在高DPI显示器上正确显示
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F); // 基准DPI 96 (100%)
            
            // 基础尺寸400x600（在96 DPI下），支持自适应调整
            // WinForms的AutoScaleMode.Dpi会自动根据系统DPI缩放窗口和控件
            Size = new Size(400, 600);
            MinimumSize = new Size(380, 550); // 允许缩小
            MaximumSize = new Size(500, 800); // 允许放大
            FormBorderStyle = FormBorderStyle.Sizable; // 改为可调整大小
            MaximizeBox = true; // 允许最大化
            MinimizeBox = true;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(242, 242, 247); // Apple系统背景色

            // 状态指示灯（参考Apple设计：顶部留白更多）
            _statusIndicator = new Label
            {
                Text = "🟢 就绪",
                Font = new Font("Microsoft YaHei", FONT_SIZE_TITLE, FontStyle.Bold),
                ForeColor = Color.FromArgb(76, 175, 80), // Apple绿色
                Location = new Point(20, 30), // 从20增加到30，增加顶部间距
                AutoSize = true
            };

            // 核心操作区卡片（居中，参考Apple设计：卡片宽度适中，左右边距充足）
            int cardWidth = 320; // 从340减小到320，增加左右边距（各40px）
            _operationCard = new Panel
            {
                Location = new Point((400 - cardWidth) / 2, 70), // 从50增加到70，增加与状态指示灯的间距
                Size = new Size(cardWidth, 120),
                BackColor = Color.White
            };
            DrawRoundedPanel(_operationCard, 8);

            // 录制按钮
            _recordButton = new Button
            {
                Text = "🔴 录制",
                Size = new Size(150, 60),
                Location = new Point(15, 20),
                Font = new Font("Microsoft YaHei", FONT_SIZE_BUTTON, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.FromArgb(244, 67, 54),
                BackColor = Color.White,
                Cursor = Cursors.Hand
            };
            _recordButton.FlatAppearance.BorderColor = Color.FromArgb(244, 67, 54);
            _recordButton.FlatAppearance.BorderSize = 2;
            _recordButton.Click += RecordButton_Click;
            DrawRoundedButton(_recordButton, 8);

            var recordHint = new Label
            {
                Text = "录制新动作",
                Font = new Font("Microsoft YaHei", FONT_SIZE_HINT, FontStyle.Regular),
                ForeColor = Color.FromArgb(150, 150, 150),
                Location = new Point(15, 85),
                AutoSize = true
            };

            // 运行按钮
            _runButton = new Button
            {
                Text = "▶️ 运行",
                Size = new Size(150, 60),
                Location = new Point(175, 20), // 调整位置以适应新宽度
                Font = new Font("Microsoft YaHei", FONT_SIZE_BUTTON, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(0, 122, 204), // #007ACC
                Cursor = Cursors.Hand
            };
            _runButton.FlatAppearance.BorderSize = 0;
            _runButton.Click += RunButton_Click;
            DrawRoundedButton(_runButton, 8);

            var runHint = new Label
            {
                Name = "RunHintLabel",
                Text = "运行跑一遍",
                Font = new Font("Microsoft YaHei", FONT_SIZE_HINT, FontStyle.Regular),
                ForeColor = Color.FromArgb(150, 150, 150),
                Location = new Point(175, 85),
                AutoSize = true
            };

            _operationCard.Controls.Add(_recordButton);
            _operationCard.Controls.Add(recordHint);
            _operationCard.Controls.Add(_runButton);
            _operationCard.Controls.Add(runHint);

            // 定时运行卡片（居中，与录制组件同宽，参考Apple设计：行间距充足，自适应高度）
            _scheduleCard = new Panel
            {
                Size = new Size(cardWidth, 60), // 默认关闭状态60px，开启后动态调整
                BackColor = Color.FromArgb(248, 248, 248), // Apple浅灰背景
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right // 自适应宽度
            };
            // 居中计算将在Resize事件中处理
            CenterCard(_scheduleCard, 210);
            DrawRoundedPanel(_scheduleCard, 8);

            // 开关和标签（始终显示，参考Apple设计：增加行间距）
            _scheduleToggle = new ToggleSwitch
            {
                Location = new Point(20, 18), // 从15增加到20，增加左边距
                Checked = false
            };
            _scheduleToggle.CheckedChanged += ScheduleToggle_CheckedChanged;

            _scheduleTimeLabel = new Label
            {
                Text = "定时运行",
                Font = new Font("Microsoft YaHei", FONT_SIZE_LABEL, FontStyle.Regular),
                ForeColor = Color.FromArgb(60, 60, 60), // Apple深灰文字
                Location = new Point(80, 20), // 从75,18调整到80,20，增加行间距
                AutoSize = true
            };

            // 时间配置（默认隐藏，开启后显示，参考Apple设计：增加行间距）
            var scheduleLabel = new Label
            {
                Name = "ScheduleTimeConfig",
                Text = "每天",
                Font = new Font("Microsoft YaHei", FONT_SIZE_LABEL, FontStyle.Regular),
                ForeColor = Color.FromArgb(60, 60, 60), // Apple深灰文字
                Location = new Point(20, 60), // 从50增加到60，增加行间距
                AutoSize = true,
                Visible = false
            };

            _timePicker = new DateTimePicker
            {
                Name = "ScheduleTimeConfig",
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "HH:mm", // 只显示时:分，不显示秒
                ShowUpDown = true,
                Size = new Size(90, 28), // 增加宽度和高度，确保时间不超出
                Location = new Point(60, 57), // 从47调整到57，增加行间距
                Font = new Font("Microsoft YaHei", FONT_SIZE_TIME, FontStyle.Regular),
                Visible = false
            };
            _timePicker.Value = DateTime.Today.AddHours(9);
            _timePicker.ValueChanged += TimePicker_ValueChanged;

            _nextRunLabel = new Label
            {
                Name = "ScheduleTimeConfig",
                Text = "*下次运行：明天 09:00",
                Font = new Font("Microsoft YaHei", FONT_SIZE_NEXT_RUN, FontStyle.Regular),
                ForeColor = Color.FromArgb(142, 142, 147), // Apple次要文字颜色
                Location = new Point(20, 95), // 从85增加到95，增加行间距
                AutoSize = true,
                Visible = false
            };

            // 定时运行提示信息（开启后显示，参考Apple设计：增加行间距）
            var scheduleHintLabel = new Label
            {
                Name = "ScheduleTimeConfig",
                Text = "⚠️ 请保持软件运行，不要关闭或让电脑睡眠",
                Font = new Font("Microsoft YaHei", FONT_SIZE_WARNING, FontStyle.Regular),
                ForeColor = Color.FromArgb(255, 149, 0), // Apple橙色
                Location = new Point(20, 120), // 从105增加到120，增加行间距
                Size = new Size(cardWidth - 40, 20), // 适应卡片宽度，增加高度
                Visible = false
            };

            _scheduleCard.Controls.Add(_scheduleToggle);
            _scheduleCard.Controls.Add(_scheduleTimeLabel);
            _scheduleCard.Controls.Add(scheduleLabel);
            _scheduleCard.Controls.Add(_timePicker);
            _scheduleCard.Controls.Add(_nextRunLabel);
            _scheduleCard.Controls.Add(scheduleHintLabel);

            Controls.Add(_statusIndicator);
            Controls.Add(_operationCard);
            Controls.Add(_scheduleCard);
        }

        private void InitializeServices()
        {
            _taskService = new TaskService();
            _scheduleService = new ScheduleService(_taskService, OnScheduledTaskTriggered);
            _recorder = new Recorder();
            _player = new Player();

            _recorder.OnRecordingComplete += Recorder_OnRecordingComplete;
            _recorder.OnStatusUpdate += Recorder_OnStatusUpdate;
            _player.OnStatusUpdate += Player_OnStatusUpdate;
            _player.OnProgressUpdate += Player_OnProgressUpdate;
        }

        private void InitializeNotifyIcon()
        {
            // 创建系统托盘图标
            _notifyIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application, // 使用默认图标，可以后续替换为自定义图标
                Text = "AutoDaily 日报助手",
                Visible = false // 默认不显示，只在需要时显示
            };

            // 创建上下文菜单
            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("显示窗口", null, (s, e) => 
            {
                this.Show();
                this.WindowState = FormWindowState.Normal;
                this.Activate();
            });
            contextMenu.Items.Add("退出", null, (s, e) => 
            {
                _notifyIcon.Visible = false;
                Application.Exit();
            });
            _notifyIcon.ContextMenuStrip = contextMenu;

            // 双击托盘图标显示窗口
            _notifyIcon.DoubleClick += (s, e) =>
            {
                this.Show();
                this.WindowState = FormWindowState.Normal;
                this.Activate();
            };
        }

        private void LoadTaskData()
        {
            var task = _taskService.GetCurrentTask();
            
            // 更新UI
            _scheduleToggle.Checked = task.Schedule.Enabled;
            _timePicker.Value = DateTime.Today.AddHours(task.Schedule.Hour).AddMinutes(task.Schedule.Minute);
            
            // 根据开关状态显示/隐藏配置项
            bool isEnabled = task.Schedule.Enabled;
            foreach (Control ctrl in _scheduleCard.Controls)
            {
                if (ctrl.Name == "ScheduleTimeConfig")
                {
                    ctrl.Visible = isEnabled;
                }
            }
            
            // 调整卡片大小：关闭状态显示开关行，开启状态显示完整配置（参考Apple设计：自适应高度）
            int cardWidth = 300; // 与录制组件同宽
            if (isEnabled)
            {
                // 自适应高度：根据内容计算所需高度
                _scheduleCard.Size = new Size(cardWidth, 160); // 增加到160，确保所有内容可见
            }
            else
            {
                _scheduleCard.Size = new Size(cardWidth, 60);
            }
            
            // 重新居中卡片
            CenterCard(_scheduleCard, 210);
            
            // 重新绘制圆角区域，确保内容不被裁剪
            DrawRoundedPanel(_scheduleCard, 8);
            
            UpdateRunButtonState();
            UpdateNextRunTime();
        }

        private void UpdateRunButtonState()
        {
            bool hasActions = _taskService.HasRecordedActions();
            _runButton.Enabled = hasActions;
            
            // 使用 Name 属性查找提示标签，更可靠
            var hintLabel = _operationCard.Controls.OfType<Label>()
                .FirstOrDefault(l => l.Name == "RunHintLabel");
            
            if (hintLabel != null)
            {
                if (!hasActions)
                {
                    hintLabel.Text = "请先录制动作";
                    hintLabel.ForeColor = Color.FromArgb(244, 67, 54);
                }
                else
                {
                    hintLabel.Text = "运行跑一遍";
                    hintLabel.ForeColor = Color.FromArgb(150, 150, 150);
                }
            }
        }

        private void UpdateNextRunTime()
        {
            var nextRun = _scheduleService.GetNextRunTime();
            if (nextRun.HasValue)
            {
                _nextRunLabel.Text = $"*下次运行：{nextRun.Value:MM月dd日 HH:mm}";
            }
            else
            {
                _nextRunLabel.Text = "*定时运行已关闭";
            }
        }

        private void RecordButton_Click(object sender, EventArgs e)
        {
            if (_isRecording)
            {
                StopRecording();
            }
            else
            {
                LogService.LogUserAction("开始录制");
                StartRecording();
            }
        }

        private void StartRecording()
        {
            _isRecording = true;
            _statusIndicator.Text = "🟡 录制中";
            _statusIndicator.ForeColor = Color.FromArgb(255, 193, 7);
            _recordButton.Text = "⏹ 停止录制";
            _recordButton.BackColor = Color.FromArgb(244, 67, 54);
            _recordButton.ForeColor = Color.White;

            // 录制时最小化主窗口，不显示弹窗（避免遮挡用户操作）
            this.WindowState = FormWindowState.Minimized;

            _recorder.StartRecording();
        }

        private void StopRecording()
        {
            _isRecording = false;
            _statusIndicator.Text = "🟢 就绪";
            _statusIndicator.ForeColor = Color.FromArgb(76, 175, 80);
            _recordButton.Text = "🔴 录制";
            _recordButton.BackColor = Color.White;
            _recordButton.ForeColor = Color.FromArgb(244, 67, 54);

            // 恢复主窗口显示
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.Activate();

            // 清理弹窗（录制时已不创建，这里只是确保清理）
            _overlayForm?.Close();
            _overlayForm = null;

            _recorder.StopRecording();
            LogService.LogUserAction("停止录制");
            UpdateRunButtonState();
        }

        private void Recorder_OnRecordingComplete(List<AutoDaily.Core.Models.Action> actions, WindowInfo windowInfo)
        {
            if (InvokeRequired)
            {
                Invoke(new System.Action(() =>
                {
                    var task = _taskService.GetCurrentTask();
                    task.Actions = actions;
                    task.TargetWindow = windowInfo;
                    _taskService.UpdateCurrentTask(task);
                    UpdateRunButtonState();
                }));
            }
            else
            {
                var task = _taskService.GetCurrentTask();
                task.Actions = actions;
                task.TargetWindow = windowInfo;
                _taskService.UpdateCurrentTask(task);
                UpdateRunButtonState();
            }
        }

        private void Recorder_OnStatusUpdate(string status)
        {
            // 可以更新状态显示
        }

        private void RunButton_Click(object sender, EventArgs e)
        {
            if (_isRunning)
            {
                StopRunning();
            }
            else
            {
                StartRunning();
            }
        }

        private async void StartRunning()
        {
            _isRunning = true;
            _statusIndicator.Text = "🟡 运行中";
            _statusIndicator.ForeColor = Color.FromArgb(255, 193, 7);
            _runButton.Enabled = false;
            _recordButton.Enabled = false;

            LogService.LogUserAction("开始运行任务");

            // 运行时隐藏主窗口，只显示进度弹窗
            this.Hide();

            _runningOverlay = new RunningOverlayForm();
            _runningOverlay.Show();

            _playerCancellationTokenSource = new CancellationTokenSource();
            var task = _taskService.GetCurrentTask();

            try
            {
                await _player.RunAsync(task, _playerCancellationTokenSource.Token);
                
                // 更新最后运行时间
                task.LastRun = DateTime.Now;
                _taskService.UpdateCurrentTask(task);
            }
            catch (OperationCanceledException)
            {
                // 用户取消
            }
            finally
            {
                _runningOverlay?.Close();
                _runningOverlay = null;
                _isRunning = false;
                _statusIndicator.Text = "🟢 就绪";
                _statusIndicator.ForeColor = Color.FromArgb(76, 175, 80);
                _runButton.Enabled = true;
                _recordButton.Enabled = true;
                
                // 恢复主窗口显示
                this.Show();
                this.WindowState = FormWindowState.Normal;
                this.Activate();
            }
        }

        private void StopRunning()
        {
            LogService.LogUserAction("用户停止运行（F10或关闭窗口）");
            _playerCancellationTokenSource?.Cancel();
        }

        private string _currentActionType = "执行中"; // 保存当前动作类型

        private void Player_OnStatusUpdate(string status)
        {
            if (_runningOverlay != null && !_runningOverlay.IsDisposed)
            {
                _runningOverlay.UpdateStatus(status);
                
                // 从状态字符串中提取动作类型（格式：执行步骤 X/Y: 动作类型）
                if (status.Contains(":"))
                {
                    var parts = status.Split(':');
                    if (parts.Length > 1)
                    {
                        _currentActionType = parts[1].Trim();
                    }
                }
            }
        }

        private void Player_OnProgressUpdate(int current, int total)
        {
            if (_runningOverlay != null && !_runningOverlay.IsDisposed)
            {
                // 使用当前动作类型更新进度
                _runningOverlay.UpdateProgress(current, total, _currentActionType);
            }
        }

        private void ScheduleToggle_CheckedChanged(object sender, EventArgs e)
        {
            var task = _taskService.GetCurrentTask();
            task.Schedule.Enabled = _scheduleToggle.Checked;
            _taskService.UpdateCurrentTask(task);
            
            // 根据开关状态显示/隐藏配置项
            bool isEnabled = _scheduleToggle.Checked;
            foreach (Control ctrl in _scheduleCard.Controls)
            {
                if (ctrl.Name == "ScheduleTimeConfig")
                {
                    ctrl.Visible = isEnabled;
                }
            }
            
            // 调整卡片大小：关闭状态显示开关行，开启状态显示完整配置（包含提示信息）
            if (isEnabled)
            {
                _scheduleCard.Size = new Size(360, 130); // 容纳时间配置和提示信息
            }
            else
            {
                _scheduleCard.Size = new Size(360, 50); // 仅显示开关
            }
            
            // 重新绘制圆角区域，确保内容不被裁剪
            DrawRoundedPanel(_scheduleCard, 8);
            
            UpdateNextRunTime();
        }

        private void TimePicker_ValueChanged(object sender, EventArgs e)
        {
            var task = _taskService.GetCurrentTask();
            task.Schedule.Hour = _timePicker.Value.Hour;
            task.Schedule.Minute = _timePicker.Value.Minute;
            _taskService.UpdateCurrentTask(task);
            UpdateNextRunTime();
        }

        private void OnScheduledTaskTriggered(AutoDaily.Core.Models.Task task)
        {
            if (InvokeRequired)
            {
                Invoke(new System.Action(() => StartRunning()));
            }
            else
            {
                StartRunning();
            }
        }

        private void RegisterHotKey()
        {
            // 使用两种方式注册热键，确保可靠性
            // 方式1: RegisterHotKey（适用于窗口有焦点时）
            try
            {
                if (!User32.RegisterHotKey(Handle, 1, User32.MOD_NONE, User32.VK_F10))
                {
                    System.Diagnostics.Debug.WriteLine("F10热键注册失败（RegisterHotKey）");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"注册热键错误: {ex.Message}");
            }
            
            // 方式2: 低级键盘钩子（全局捕获，即使窗口失去焦点也能工作）
            try
            {
                _hotkeyHookProc = HotkeyHookProc;
                _hotkeyHook = User32.SetWindowsHookEx(
                    User32.WH_KEYBOARD_LL,
                    _hotkeyHookProc,
                    Kernel32.GetModuleHandle(null),
                    0);
                
                if (_hotkeyHook == IntPtr.Zero)
                {
                    System.Diagnostics.Debug.WriteLine("F10热键钩子注册失败");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"注册热键钩子错误: {ex.Message}");
            }
        }
        
        private IntPtr HotkeyHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            // 只在运行时响应F10
            if (nCode >= 0 && _isRunning)
            {
                if (wParam == (IntPtr)User32.WM_KEYDOWN || wParam == (IntPtr)User32.WM_SYSKEYDOWN)
                {
                    int vkCode = System.Runtime.InteropServices.Marshal.ReadInt32(lParam);
                    if (vkCode == User32.VK_F10)
                    {
                        // 在UI线程中执行停止操作
                        if (InvokeRequired)
                        {
                            Invoke(new System.Action(() => StopRunning()));
                        }
                        else
                        {
                            StopRunning();
                        }
                        // 返回非零值表示已处理，阻止传递给其他程序
                        return new IntPtr(1);
                    }
                }
            }
            
            return User32.CallNextHookEx(_hotkeyHook, nCode, wParam, lParam);
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_HOTKEY = 0x0312;
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == 1)
            {
                if (_isRunning)
                {
                    StopRunning();
                }
                return; // 处理了热键，不继续传递
            }
            base.WndProc(ref m);
        }
        
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // 也在这里处理F10，确保能响应
            if (keyData == Keys.F10 && _isRunning)
            {
                StopRunning();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // 检查是否启用了定时运行
            var task = _taskService.GetCurrentTask();
            if (task.Schedule.Enabled)
            {
                // 如果启用了定时运行，提示用户
                var result = MessageBox.Show(
                    "关闭软件后将无法执行定时运行任务。\n\n是否确定要关闭？",
                    "提示",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2);
                
                if (result == DialogResult.No)
                {
                    e.Cancel = true; // 取消关闭
                    return;
                }
            }

            // 卸载热键
            User32.UnregisterHotKey(Handle, 1);
            
            // 卸载键盘钩子
            if (_hotkeyHook != IntPtr.Zero)
            {
                User32.UnhookWindowsHookEx(_hotkeyHook);
                _hotkeyHook = IntPtr.Zero;
            }
            
            // 清理系统托盘
            _notifyIcon?.Dispose();
            
            _scheduleService?.Dispose();
            _recorder?.Dispose();
            base.OnFormClosing(e);
        }

        private void DrawRoundedPanel(Panel panel, int radius)
        {
            var path = new GraphicsPath();
            path.AddArc(0, 0, radius * 2, radius * 2, 180, 90);
            path.AddArc(panel.Width - radius * 2, 0, radius * 2, radius * 2, 270, 90);
            path.AddArc(panel.Width - radius * 2, panel.Height - radius * 2, radius * 2, radius * 2, 0, 90);
            path.AddArc(0, panel.Height - radius * 2, radius * 2, radius * 2, 90, 90);
            path.CloseFigure();
            panel.Region = new Region(path);
        }

        private void DrawRoundedButton(Button button, int radius)
        {
            button.Paint += (s, e) =>
            {
                var rect = new Rectangle(0, 0, button.Width, button.Height);
                var path = new GraphicsPath();
                path.AddArc(0, 0, radius * 2, radius * 2, 180, 90);
                path.AddArc(rect.Width - radius * 2, 0, radius * 2, radius * 2, 270, 90);
                path.AddArc(rect.Width - radius * 2, rect.Height - radius * 2, radius * 2, radius * 2, 0, 90);
                path.AddArc(0, rect.Height - radius * 2, radius * 2, radius * 2, 90, 90);
                path.CloseFigure();
                button.Region = new Region(path);
            };
            // 立即应用一次
            button.Invalidate();
        }
    }
}

