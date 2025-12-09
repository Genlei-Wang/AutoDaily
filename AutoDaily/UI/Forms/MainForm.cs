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
        private Panel _mainContainer; // 主容器：包含所有内容，在主窗口中居中
        private Label _statusIndicator;
        private Button _recordButton;
        private Button _runButton;
        private Panel _operationCard;
        private Panel _scheduleCard;
        private ToggleSwitch _scheduleToggle;
        private Label _scheduleTimeLabel;
        private Label _nextRunLabel;
        private DateTimePicker _timePicker;
        private Button _saveScheduleButton; // 保存按钮

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
            
            // 初始化时更新主容器大小并居中
            UpdateMainContainerSize();
            CenterContainerControls();
            CenterMainContainer();
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

            // 创建主容器：包含所有内容，在主窗口中上下左右居中
            int containerWidth = 340; // 容器宽度
            int containerHeight = 400; // 容器高度（初始值，会根据内容动态调整）
            _mainContainer = new Panel
            {
                Size = new Size(containerWidth, containerHeight),
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.None // 不使用Anchor，使用居中定位
            };
            // 居中计算将在Resize事件中处理
            CenterMainContainer();

            // 状态指示灯（在主容器内，顶部靠左）
            _statusIndicator = new Label
            {
                Text = "🟢 就绪",
                Font = new Font("Microsoft YaHei", FONT_SIZE_TITLE, FontStyle.Bold),
                ForeColor = Color.FromArgb(76, 175, 80), // Apple绿色
                Location = new Point(20, 20), // 靠左对齐
                AutoSize = true
            };
            _mainContainer.Controls.Add(_statusIndicator);

            // 核心操作区卡片（在主容器内，靠上，水平居中）
            int cardWidth = 300; // 卡片宽度
            _operationCard = new Panel
            {
                Size = new Size(cardWidth, 120),
                BackColor = Color.White,
                Location = new Point((containerWidth - cardWidth) / 2, 20) // 水平居中，靠上（与状态同一层级）
            };
            DrawRoundedPanel(_operationCard);
            _mainContainer.Controls.Add(_operationCard);

            // 录制按钮（参考Apple设计：按钮间距和颜色，确保不超出卡片）
            _recordButton = new Button
            {
                Text = "🔴 录制",
                Size = new Size(130, 60), // 减小按钮宽度
                Location = new Point(15, 25), // 增加内边距
                Font = new Font("Microsoft YaHei", FONT_SIZE_BUTTON, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.FromArgb(255, 59, 48), // Apple红色
                BackColor = Color.White,
                Cursor = Cursors.Hand,
                AutoSize = false  // 确保按钮大小固定
            };
            _recordButton.FlatAppearance.BorderColor = Color.FromArgb(255, 59, 48);
            _recordButton.FlatAppearance.BorderSize = 2;
            _recordButton.Click += RecordButton_Click;
            DrawRoundedButton(_recordButton, 8);

            var recordHint = new Label
            {
                Text = "录制新动作",
                Font = new Font("Microsoft YaHei", FONT_SIZE_HINT, FontStyle.Regular),
                ForeColor = Color.FromArgb(142, 142, 147), // Apple次要文字颜色
                Location = new Point(15, 90),
                AutoSize = true
            };

            // 运行按钮（参考Apple设计：按钮间距和颜色，确保不超出卡片）
            _runButton = new Button
            {
                Text = "▶️ 运行",
                Size = new Size(130, 60), // 减小按钮宽度
                Location = new Point(155, 25), // 调整位置，确保不超出
                Font = new Font("Microsoft YaHei", FONT_SIZE_BUTTON, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(0, 122, 255), // Apple蓝色
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
                ForeColor = Color.FromArgb(142, 142, 147), // Apple次要文字颜色
                Location = new Point(155, 90),
                AutoSize = true
            };

            _operationCard.Controls.Add(_recordButton);
            _operationCard.Controls.Add(recordHint);
            _operationCard.Controls.Add(_runButton);
            _operationCard.Controls.Add(runHint);

            // 定时运行卡片（在主容器内，操作卡片下方，水平居中，靠上）
            _scheduleCard = new Panel
            {
                Size = new Size(cardWidth, 60), // 默认关闭状态60px，开启后动态调整
                BackColor = Color.FromArgb(248, 248, 248), // Apple浅灰背景
                Location = new Point((containerWidth - cardWidth) / 2, 150) // 水平居中，操作卡片下方，靠上
            };
            DrawRoundedPanel(_scheduleCard);
            _mainContainer.Controls.Add(_scheduleCard);
            
            // 监听窗口大小变化，重新居中主容器
            this.Resize += MainForm_Resize;

            // 开关和标签（始终显示，参考Apple设计：增加行间距）
            _scheduleToggle = new ToggleSwitch
            {
                Location = new Point(20, 18), // 从15增加到20，增加左边距
                Checked = false
            };
            _scheduleToggle.CheckedChanged += ScheduleToggle_CheckedChanged_UI; // 只更新UI，不生效

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
            _timePicker.ValueChanged += TimePicker_ValueChanged_UI; // 只更新UI，不生效

            // 保存按钮（开启定时运行后显示）
            _saveScheduleButton = new Button
            {
                Name = "ScheduleTimeConfig",
                Text = "💾 保存",
                Size = new Size(80, 32),
                Location = new Point(160, 57), // 时间选择器右侧
                Font = new Font("Microsoft YaHei", FONT_SIZE_BUTTON, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(0, 122, 255), // Apple蓝色
                Cursor = Cursors.Hand,
                Visible = false
            };
            _saveScheduleButton.FlatAppearance.BorderSize = 0;
            _saveScheduleButton.Click += SaveScheduleButton_Click;
            DrawRoundedButton(_saveScheduleButton, 6);

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

            // 定时运行提示信息（开启后显示，参考Apple设计：增加行间距，支持自动换行）
            var scheduleHintLabel = new Label
            {
                Name = "ScheduleTimeConfig",
                Text = "⚠️ 请保持软件运行，\n不要关闭或让电脑睡眠",  // 手动换行，确保显示完整
                Font = new Font("Microsoft YaHei", FONT_SIZE_WARNING, FontStyle.Regular),
                ForeColor = Color.FromArgb(255, 149, 0), // Apple橙色
                Location = new Point(20, 120), // 从105增加到120，增加行间距
                Size = new Size(cardWidth - 40, 50), // 增加高度以支持两行文字
                AutoSize = false,  // 固定大小
                AutoEllipsis = false,  // 不使用省略号
                TextAlign = ContentAlignment.TopLeft,  // 顶部对齐
                Visible = false
            };

            _scheduleCard.Controls.Add(_scheduleToggle);
            _scheduleCard.Controls.Add(_scheduleTimeLabel);
            _scheduleCard.Controls.Add(scheduleLabel);
            _scheduleCard.Controls.Add(_timePicker);
            _scheduleCard.Controls.Add(_saveScheduleButton);
            _scheduleCard.Controls.Add(_nextRunLabel);
            _scheduleCard.Controls.Add(scheduleHintLabel);

            // 将主容器添加到窗口
            Controls.Add(_mainContainer);
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
            
            // 更新UI（先取消事件，避免触发）
            _scheduleToggle.CheckedChanged -= ScheduleToggle_CheckedChanged_UI;
            _scheduleToggle.Checked = task.Schedule.Enabled;
            _scheduleToggle.CheckedChanged += ScheduleToggle_CheckedChanged_UI;
            
            _timePicker.ValueChanged -= TimePicker_ValueChanged_UI;
            _timePicker.Value = DateTime.Today.AddHours(task.Schedule.Hour).AddMinutes(task.Schedule.Minute);
            _timePicker.ValueChanged += TimePicker_ValueChanged_UI;
            
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
                // 自适应高度：根据提示文字的实际高度计算
                var hintLabel = _scheduleCard.Controls.OfType<Label>()
                    .FirstOrDefault(l => l.Name == "ScheduleTimeConfig" && l.Text.Contains("⚠️"));
                int hintHeight = hintLabel != null ? hintLabel.Height : 40;
                // 计算总高度：开关行(60) + 时间配置行(40) + 下次运行行(25) + 提示行(动态) + 边距(20)
                int totalHeight = 60 + 40 + 25 + hintHeight + 20;
                _scheduleCard.Size = new Size(cardWidth, totalHeight);
            }
            else
            {
                _scheduleCard.Size = new Size(cardWidth, 60);
            }
            
            // 重新绘制圆角区域，确保内容不被裁剪
            DrawRoundedPanel(_scheduleCard);
            
            // 更新主容器高度并重新居中
            UpdateMainContainerSize();
            CenterContainerControls();
            CenterMainContainer();
            
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
            _recordButton.Text = "⏹ 停止";
            _recordButton.BackColor = Color.FromArgb(244, 67, 54);
            _recordButton.ForeColor = Color.White;
            _recordButton.Size = new Size(130, 60);  // 确保按钮大小一致，文字显示完整

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
            _recordButton.Size = new Size(130, 60);  // 确保按钮大小一致

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

        /// <summary>
        /// 定时开关变化（只更新UI，不生效）
        /// </summary>
        private void ScheduleToggle_CheckedChanged_UI(object sender, EventArgs e)
        {
            // 根据开关状态显示/隐藏配置项
            bool isEnabled = _scheduleToggle.Checked;
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
                // 自适应高度：根据提示文字的实际高度计算
                var hintLabel = _scheduleCard.Controls.OfType<Label>()
                    .FirstOrDefault(l => l.Name == "ScheduleTimeConfig" && l.Text.Contains("⚠️"));
                int hintHeight = hintLabel != null ? hintLabel.Height : 40;
                // 计算总高度：开关行(60) + 时间配置行(40) + 下次运行行(25) + 提示行(动态) + 边距(20)
                int totalHeight = 60 + 40 + 25 + hintHeight + 20;
                _scheduleCard.Size = new Size(cardWidth, totalHeight);
            }
            else
            {
                _scheduleCard.Size = new Size(cardWidth, 60);
            }
            
            // 重新绘制圆角区域，确保内容不被裁剪
            DrawRoundedPanel(_scheduleCard);
            
            // 更新主容器高度并重新居中
            UpdateMainContainerSize();
            CenterContainerControls();
            CenterMainContainer();
        }

        /// <summary>
        /// 时间选择器变化（只更新UI，不生效）
        /// </summary>
        private void TimePicker_ValueChanged_UI(object sender, EventArgs e)
        {
            // 只更新UI显示，不保存到任务
        }

        /// <summary>
        /// 保存按钮点击：保存定时配置并生效
        /// </summary>
        private void SaveScheduleButton_Click(object sender, EventArgs e)
        {
            var task = _taskService.GetCurrentTask();
            task.Schedule.Enabled = _scheduleToggle.Checked;
            task.Schedule.Hour = _timePicker.Value.Hour;
            task.Schedule.Minute = _timePicker.Value.Minute;
            _taskService.UpdateCurrentTask(task);
            
            // 设置开机自启
            _scheduleService.SetStartup(_scheduleToggle.Checked);
            
            // 更新下次运行时间显示
            UpdateNextRunTime();
            
            // 显示保存成功提示
            _statusIndicator.Text = "✅ 已保存";
            _statusIndicator.ForeColor = Color.FromArgb(76, 175, 80);
            
            // 2秒后恢复状态
            System.Threading.Tasks.Task.Delay(2000).ContinueWith(t =>
            {
                if (InvokeRequired)
                {
                    Invoke(new System.Action(() =>
                    {
                        _statusIndicator.Text = "🟢 就绪";
                        _statusIndicator.ForeColor = Color.FromArgb(76, 175, 80);
                    }));
                }
            });
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
            // 如果是用户点击关闭按钮（UserClosing），且启用了定时运行
            if (e.CloseReason == CloseReason.UserClosing)
            {
                var task = _taskService.GetCurrentTask();
                if (task.Schedule.Enabled)
                {
                    e.Cancel = true; // 取消关闭
                    this.Hide();     // 隐藏窗口
                    
                    // 显示托盘图标和提示
                    _notifyIcon.Visible = true;
                    _notifyIcon.ShowBalloonTip(3000, "AutoDaily 已隐藏", "软件正在后台运行，双击托盘图标可重新打开。", ToolTipIcon.Info);
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

        /// <summary>
        /// 更新主容器大小：根据内容动态调整高度
        /// </summary>
        private void UpdateMainContainerSize()
        {
            if (_mainContainer == null || _scheduleCard == null || _operationCard == null) return;
            
            // 计算所需高度：状态指示(60) + 操作卡片(120) + 间距(10) + 定时卡片(动态) + 底部边距(20)
            // 优化：靠上布局，减少间距
            int scheduleCardHeight = _scheduleCard.Height;
            int containerHeight = 60 + 120 + 10 + scheduleCardHeight + 20;
            
            _mainContainer.Size = new Size(_mainContainer.Width, containerHeight);
        }

        /// <summary>
        /// 居中主容器：在主窗口中水平居中，垂直靠上
        /// </summary>
        private void CenterMainContainer()
        {
            if (_mainContainer == null) return;
            
            int windowWidth = this.ClientSize.Width;
            int windowHeight = this.ClientSize.Height;
            int containerWidth = _mainContainer.Width;
            int containerHeight = _mainContainer.Height;
            
            // 计算位置：水平居中，垂直靠上（距离顶部60px）
            int x = (windowWidth - containerWidth) / 2;
            int y = 60; // 靠上，距离顶部60px
            
            _mainContainer.Location = new Point(x, y);
        }

        private void MainForm_Resize(object sender, EventArgs e)
        {
            // 窗口大小改变时，重新居中主容器和内部组件
            if (_mainContainer != null)
            {
                // 确保主容器内的组件水平居中
                CenterContainerControls();
                // 重新居中主容器
                CenterMainContainer();
            }
        }

        /// <summary>
        /// 居中主容器内的所有组件（状态靠左，卡片水平居中）
        /// </summary>
        private void CenterContainerControls()
        {
            if (_mainContainer == null) return;
            
            int containerWidth = _mainContainer.Width;
            int cardWidth = 300;
            
            // 状态指示器靠左（不居中）
            if (_statusIndicator != null)
            {
                _statusIndicator.Location = new Point(20, _statusIndicator.Location.Y);
            }
            
            // 居中操作卡片
            if (_operationCard != null)
            {
                _operationCard.Location = new Point((containerWidth - cardWidth) / 2, _operationCard.Location.Y);
            }
            
            // 居中定时卡片
            if (_scheduleCard != null)
            {
                _scheduleCard.Location = new Point((containerWidth - cardWidth) / 2, _scheduleCard.Location.Y);
            }
        }

        private void DrawRoundedPanel(Panel panel, int radius = 8)
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

