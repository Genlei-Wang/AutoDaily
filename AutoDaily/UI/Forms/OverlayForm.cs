using System;
using System.Drawing;
using System.Windows.Forms;

namespace AutoDaily.UI.Forms
{
    public partial class OverlayForm : Form
    {
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
            BackColor = Color.FromArgb(240, 50, 50, 50);
            Size = new Size(400, 60);
            Location = new Point(
                (Screen.PrimaryScreen.WorkingArea.Width - Width) / 2,
                20);

            // 圆角窗口
            Region = System.Drawing.Region.FromHrgn(
                CreateRoundRectRgn(0, 0, Width, Height, 15, 15));

            _statusLabel = new Label
            {
                Text = "🔴 录制中",
                ForeColor = Color.White,
                Font = new Font("Microsoft YaHei", 10, FontStyle.Bold),
                Location = new Point(15, 15),
                AutoSize = true
            };

            _timeLabel = new Label
            {
                Text = "00:00",
                ForeColor = Color.White,
                Font = new Font("Microsoft YaHei", 9),
                Location = new Point(120, 18),
                AutoSize = true
            };

            _pauseButton = new Button
            {
                Text = "⏸ 暂停",
                Size = new Size(70, 30),
                Location = new Point(200, 15),
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
                Size = new Size(100, 30),
                Location = new Point(280, 15),
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

        [System.Runtime.InteropServices.DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern System.IntPtr CreateRoundRectRgn(
            int nLeftRect, int nTopRect, int nRightRect, int nBottomRect,
            int nWidthEllipse, int nHeightEllipse);
    }
}

