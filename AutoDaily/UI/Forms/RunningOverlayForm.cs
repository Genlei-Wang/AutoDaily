using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AutoDaily.UI.Forms
{
    public partial class RunningOverlayForm : Form
    {
        private Label _titleLabel;
        private Label _statusLabel;
        private Label _warningLabel;
        private Label _stopHintLabel;
        private ProgressBar _progressBar;
        private int _currentStep = 0;
        private int _totalSteps = 0;

        public RunningOverlayForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Maximized;
            TopMost = true;
            ShowInTaskbar = false;
            BackColor = Color.FromArgb(25, 25, 25); // 深灰色，不透明
            Opacity = 0.1; // 使用 Opacity 实现透明度
            StartPosition = FormStartPosition.CenterScreen;
            SetStyle(ControlStyles.SupportsTransparentBackColor, false);

            // 中央HUD面板
            var panel = new Panel
            {
                Size = new Size(400, 250),
                Location = new Point(
                    (Screen.PrimaryScreen.WorkingArea.Width - 400) / 2,
                    (Screen.PrimaryScreen.WorkingArea.Height - 250) / 2),
                BackColor = Color.FromArgb(250, 255, 255, 255)
            };

            // 圆角
            var path = new GraphicsPath();
            int radius = 15;
            path.AddArc(0, 0, radius * 2, radius * 2, 180, 90);
            path.AddArc(panel.Width - radius * 2, 0, radius * 2, radius * 2, 270, 90);
            path.AddArc(panel.Width - radius * 2, panel.Height - radius * 2, radius * 2, radius * 2, 0, 90);
            path.AddArc(0, panel.Height - radius * 2, radius * 2, radius * 2, 90, 90);
            path.CloseFigure();
            panel.Region = new Region(path);

            _titleLabel = new Label
            {
                Text = "🤖 正在干活...",
                Font = new Font("Microsoft YaHei", 16, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 122, 204),
                Location = new Point(20, 20),
                Size = new Size(360, 30),
                TextAlign = ContentAlignment.MiddleCenter
            };

            _statusLabel = new Label
            {
                Text = "步骤: 准备中...",
                Font = new Font("Microsoft YaHei", 11),
                ForeColor = Color.FromArgb(60, 60, 60),
                Location = new Point(20, 70),
                Size = new Size(360, 25),
                TextAlign = ContentAlignment.MiddleCenter
            };

            _progressBar = new ProgressBar
            {
                Location = new Point(30, 110),
                Size = new Size(340, 25),
                Style = ProgressBarStyle.Continuous
            };

            _warningLabel = new Label
            {
                Text = "⚠️ 请勿触碰鼠标键盘",
                Font = new Font("Microsoft YaHei", 10),
                ForeColor = Color.FromArgb(255, 152, 0),
                Location = new Point(20, 150),
                Size = new Size(360, 25),
                TextAlign = ContentAlignment.MiddleCenter
            };

            _stopHintLabel = new Label
            {
                Text = "[ 按 F12 紧急停止 ]",
                Font = new Font("Microsoft YaHei", 9),
                ForeColor = Color.FromArgb(150, 150, 150),
                Location = new Point(20, 180),
                Size = new Size(360, 25),
                TextAlign = ContentAlignment.MiddleCenter
            };

            panel.Controls.Add(_titleLabel);
            panel.Controls.Add(_statusLabel);
            panel.Controls.Add(_progressBar);
            panel.Controls.Add(_warningLabel);
            panel.Controls.Add(_stopHintLabel);

            Controls.Add(panel);
        }

        public void UpdateProgress(int current, int total, string status)
        {
            _currentStep = current;
            _totalSteps = total;
            
            if (InvokeRequired)
            {
                Invoke(new System.Action(() =>
                {
                    _statusLabel.Text = $"步骤: {status} ({current}/{total})";
                    _progressBar.Maximum = total;
                    _progressBar.Value = current;
                }));
            }
            else
            {
                _statusLabel.Text = $"步骤: {status} ({current}/{total})";
                _progressBar.Maximum = total;
                _progressBar.Value = current;
            }
        }

        public void UpdateStatus(string status)
        {
            if (InvokeRequired)
            {
                Invoke(new System.Action(() => _statusLabel.Text = status));
            }
            else
            {
                _statusLabel.Text = status;
            }
        }
    }
}

