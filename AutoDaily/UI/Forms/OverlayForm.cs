using System;
using System.Drawing;
using System.Windows.Forms;

namespace AutoDaily.UI.Forms
{
    public partial class OverlayForm : Form
    {
        // 字号规范常量（参考 Apple Human Interface Guidelines）
        // 原则：清晰易读、层次分明、最小字号不小于 11pt
        private const float FONT_SIZE_STATUS = 12f;     // 状态文字 - 重要信息
        private const float FONT_SIZE_TIME = 11f;       // 时间文字 - 次要信息
        private const float FONT_SIZE_BUTTON = 11f;     // 按钮文字 - 操作按钮

        private Label _statusLabel;
        private Label _timeLabel;
        private Button _pauseButton;
        private Button _stopButton;
        private DateTime _startTime;
        private Timer _timer;
        private bool _isPaused = false;

        public event EventHandler PauseClicked;
        public event EventHandler StopClicked;

        public OverlayForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            ShowInTaskbar = false;
            BackColor = Color.FromArgb(50, 50, 50); // 深灰色背景
            Opacity = 0.85; // 透明度85%
            
            // DPI缩放支持
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);
            
            // 基础尺寸350x45（在96 DPI下），优化为更小尺寸减少遮挡
            Size = new Size(350, 45);
            Location = new Point(
                (Screen.PrimaryScreen.WorkingArea.Width - Width) / 2,
                10); // 距离顶部10px

            // 圆角窗口
            Region = System.Drawing.Region.FromHrgn(
                CreateRoundRectRgn(0, 0, Width, Height, 15, 15));
            
            // 设置不透明背景，避免透明背景色错误
            SetStyle(ControlStyles.SupportsTransparentBackColor, false);

            _statusLabel = new Label
            {
                Text = "🔴 录制中",
                ForeColor = Color.White,
                Font = new Font("Microsoft YaHei", FONT_SIZE_STATUS, FontStyle.Bold),
                Location = new Point(15, 12),
                AutoSize = true
            };

            _timeLabel = new Label
            {
                Text = "00:00",
                ForeColor = Color.White,
                Font = new Font("Microsoft YaHei", FONT_SIZE_TIME, FontStyle.Regular),
                Location = new Point(100, 15),
                AutoSize = true
            };

            _pauseButton = new Button
            {
                Text = "⏸ 暂停",
                Size = new Size(70, 30), // 符合文档要求
                Location = new Point(160, 10),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            _pauseButton.FlatAppearance.BorderSize = 0;
            _pauseButton.Click += (s, e) => PauseClicked?.Invoke(this, e);

            _stopButton = new Button
            {
                Text = "⏹ 完成并保存",
                Size = new Size(100, 30), // 符合文档要求
                Location = new Point(240, 10),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            _stopButton.FlatAppearance.BorderSize = 0;
            _stopButton.Click += (s, e) => StopClicked?.Invoke(this, e);

            Controls.Add(_statusLabel);
            Controls.Add(_timeLabel);
            Controls.Add(_pauseButton);
            Controls.Add(_stopButton);

            _startTime = DateTime.Now;
            _timer = new Timer { Interval = 100 };
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (!_isPaused)
            {
                var elapsed = DateTime.Now - _startTime;
                _timeLabel.Text = $"{(int)elapsed.TotalMinutes:D2}:{elapsed.Seconds:D2}";
            }
        }

        public void SetPaused(bool paused)
        {
            _isPaused = paused;
            _statusLabel.Text = paused ? "⏸ 已暂停" : "🔴 录制中";
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _timer?.Stop();
            _timer?.Dispose();
            base.OnFormClosed(e);
        }

        [System.Runtime.InteropServices.DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern System.IntPtr CreateRoundRectRgn(
            int nLeftRect, int nTopRect, int nRightRect, int nBottomRect,
            int nWidthEllipse, int nHeightEllipse);
    }
}

