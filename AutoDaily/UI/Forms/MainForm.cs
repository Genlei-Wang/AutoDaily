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

        public MainForm()
        {
            InitializeComponent();
            InitializeServices();
            LoadTaskData();
            RegisterHotKey();
        }

        private void InitializeComponent()
        {
            Text = "AutoDaily 日报助手";
            Size = new Size(420, 280);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = true;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(243, 243, 243); // #F3F3F3

            // 状态指示灯
            _statusIndicator = new Label
            {
                Text = "🟢 就绪",
                Font = new Font("Microsoft YaHei", 12, FontStyle.Bold),
                ForeColor = Color.FromArgb(76, 175, 80),
                Location = new Point(20, 20),
                AutoSize = true
            };

            // 核心操作区卡片
            _operationCard = new Panel
            {
                Location = new Point(20, 50),
                Size = new Size(380, 100),
                BackColor = Color.White
            };
            DrawRoundedPanel(_operationCard, 8);

            // 录制按钮
            _recordButton = new Button
            {
                Text = "🔴 录制",
                Size = new Size(160, 60),
                Location = new Point(20, 20),
                Font = new Font("Microsoft YaHei", 11, FontStyle.Bold),
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
                Font = new Font("Microsoft YaHei", 8),
                ForeColor = Color.FromArgb(150, 150, 150),
                Location = new Point(20, 85),
                AutoSize = true
            };

            // 运行按钮
            _runButton = new Button
            {
                Text = "▶️ 运行",
                Size = new Size(160, 60),
                Location = new Point(200, 20),
                Font = new Font("Microsoft YaHei", 11, FontStyle.Bold),
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
                Text = "运行跑一遍",
                Font = new Font("Microsoft YaHei", 8),
                ForeColor = Color.FromArgb(150, 150, 150),
                Location = new Point(200, 85),
                AutoSize = true
            };

            _operationCard.Controls.Add(_recordButton);
            _operationCard.Controls.Add(recordHint);
            _operationCard.Controls.Add(_runButton);
            _operationCard.Controls.Add(runHint);

            // 定时运行卡片
            _scheduleCard = new Panel
            {
                Location = new Point(20, 160),
                Size = new Size(380, 90),
                BackColor = Color.FromArgb(250, 250, 250)
            };
            DrawRoundedPanel(_scheduleCard, 8);

            var scheduleLabel = new Label
            {
                Text = "每天",
                Font = new Font("Microsoft YaHei", 10),
                ForeColor = Color.FromArgb(100, 100, 100),
                Location = new Point(20, 15),
                AutoSize = true
            };

            _timePicker = new DateTimePicker
            {
                Format = DateTimePickerFormat.Time,
                ShowUpDown = true,
                Size = new Size(80, 25),
                Location = new Point(60, 12),
                Font = new Font("Microsoft YaHei", 9)
            };
            _timePicker.Value = DateTime.Today.AddHours(9);
            _timePicker.ValueChanged += TimePicker_ValueChanged;

            _scheduleToggle = new ToggleSwitch
            {
                Location = new Point(160, 10),
                Checked = false
            };
            _scheduleToggle.CheckedChanged += ScheduleToggle_CheckedChanged;

            _scheduleTimeLabel = new Label
            {
                Text = "自动运行",
                Font = new Font("Microsoft YaHei", 9),
                ForeColor = Color.FromArgb(100, 100, 100),
                Location = new Point(220, 15),
                AutoSize = true
            };

            _nextRunLabel = new Label
            {
                Text = "*下次运行：明天 09:00",
                Font = new Font("Microsoft YaHei", 8),
                ForeColor = Color.FromArgb(150, 150, 150),
                Location = new Point(20, 45),
                AutoSize = true
            };

            _scheduleCard.Controls.Add(scheduleLabel);
            _scheduleCard.Controls.Add(_timePicker);
            _scheduleCard.Controls.Add(_scheduleToggle);
            _scheduleCard.Controls.Add(_scheduleTimeLabel);
            _scheduleCard.Controls.Add(_nextRunLabel);

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

        private void LoadTaskData()
        {
            var task = _taskService.GetCurrentTask();
            
            // 更新UI
            _scheduleToggle.Checked = task.Schedule.Enabled;
            _timePicker.Value = DateTime.Today.AddHours(task.Schedule.Hour).AddMinutes(task.Schedule.Minute);
            
            UpdateRunButtonState();
            UpdateNextRunTime();
        }

        private void UpdateRunButtonState()
        {
            bool hasActions = _taskService.HasRecordedActions();
            _runButton.Enabled = hasActions;
            if (!hasActions)
            {
                _runButton.Text = "▶️ 运行";
                // 在提示标签中显示
                var hintLabel = _operationCard.Controls.OfType<Label>()
                    .FirstOrDefault(l => l.Text.Contains("运行"));
                if (hintLabel != null)
                {
                    hintLabel.Text = "请先录制动作";
                    hintLabel.ForeColor = Color.FromArgb(244, 67, 54);
                }
            }
            else
            {
                _runButton.Text = "▶️ 运行";
                var hintLabel = _operationCard.Controls.OfType<Label>()
                    .FirstOrDefault(l => l.Text.Contains("请先"));
                if (hintLabel != null)
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

            _overlayForm = new OverlayForm();
            _overlayForm.PauseClicked += (s, e) => { /* 暂停功能暂不实现 */ };
            _overlayForm.StopClicked += (s, e) => StopRecording();
            _overlayForm.Show();

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

            _overlayForm?.Close();
            _overlayForm = null;

            _recorder.StopRecording();
            UpdateRunButtonState();
        }

        private void Recorder_OnRecordingComplete(List<AutoDaily.Core.Models.Action> actions, WindowInfo windowInfo)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() =>
                {
                    var task = _taskService.GetCurrentTask();
                    task.Actions = actions;
                    task.TargetWindow = windowInfo;
                    _taskService.UpdateCurrentTask(task);
                }));
            }
            else
            {
                var task = _taskService.GetCurrentTask();
                task.Actions = actions;
                task.TargetWindow = windowInfo;
                _taskService.UpdateCurrentTask(task);
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
            }
        }

        private void StopRunning()
        {
            _playerCancellationTokenSource?.Cancel();
        }

        private void Player_OnStatusUpdate(string status)
        {
            if (_runningOverlay != null && !_runningOverlay.IsDisposed)
            {
                _runningOverlay.UpdateStatus(status);
            }
        }

        private void Player_OnProgressUpdate(int current, int total)
        {
            if (_runningOverlay != null && !_runningOverlay.IsDisposed)
            {
                _runningOverlay.UpdateProgress(current, total, "执行中");
            }
        }

        private void ScheduleToggle_CheckedChanged(object sender, EventArgs e)
        {
            var task = _taskService.GetCurrentTask();
            task.Schedule.Enabled = _scheduleToggle.Checked;
            _taskService.UpdateCurrentTask(task);
            UpdateNextRunTime();

            if (_scheduleToggle.Checked)
            {
                _nextRunLabel.Text = "已激活。哪怕电脑关机，只要您上班解锁屏幕，我就能帮您跑。";
            }
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
                Invoke(new Action(() => StartRunning()));
            }
            else
            {
                StartRunning();
            }
        }

        private void RegisterHotKey()
        {
            // 注册F12热键用于紧急停止
            User32.RegisterHotKey(Handle, 1, User32.MOD_NONE, User32.VK_F12);
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
            }
            base.WndProc(ref m);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            User32.UnregisterHotKey(Handle, 1);
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

