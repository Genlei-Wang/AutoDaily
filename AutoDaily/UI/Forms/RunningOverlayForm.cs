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
            
            // DPI缩放支持
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);
            
            // 基础尺寸350x180（在96 DPI下），WinForms会自动根据DPI缩放
            Size = new Size(350, 180);
            TopMost = true;
            ShowInTaskbar = false;
            BackColor = Color.FromArgb(240, 240, 240);
            StartPosition = FormStartPosition.Manual;
            Location = new Point(
                Screen.PrimaryScreen.WorkingArea.Right - Width - 20,
                Screen.PrimaryScreen.WorkingArea.Top + 20);
            SetStyle(ControlStyles.SupportsTransparentBackColor, false);
            
            // 添加关闭按钮
            var closeButton = new Button
            {
                Text = "×",
                Size = new Size(30, 30),
                Location = new Point(Width - 35, 5),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.Black,
                BackColor = Color.Transparent,
                Font = new Font("Arial", 16, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            closeButton.FlatAppearance.BorderSize = 0;
            closeButton.Click += (s, e) => Close();
            Controls.Add(closeButton);

            // HUD面板（小窗口）
            var panel = new Panel
            {
                Size = new Size(Width - 10, Height - 40),
                Location = new Point(5, 35),
                BackColor = Color.White
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
                Text = "🤖 正在运行",
                Font = new Font("Microsoft YaHei", 12, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 122, 204),
                Location = new Point(10, 10),
                Size = new Size(panel.Width - 20, 25),
                TextAlign = ContentAlignment.MiddleCenter
            };

            _statusLabel = new Label
            {
                Text = "准备中...",
                Font = new Font("Microsoft YaHei", 9),
                ForeColor = Color.FromArgb(60, 60, 60),
                Location = new Point(10, 40),
                Size = new Size(panel.Width - 20, 20),
                TextAlign = ContentAlignment.MiddleCenter
            };

            _progressBar = new ProgressBar
            {
                Location = new Point(10, 65),
                Size = new Size(panel.Width - 20, 20),
                Style = ProgressBarStyle.Continuous
            };

            _warningLabel = new Label
            {
                Text = "按 F10 停止",
                Font = new Font("Microsoft YaHei", 8),
                ForeColor = Color.FromArgb(255, 152, 0),
                Location = new Point(10, 90),
                Size = new Size(panel.Width - 20, 20),
                TextAlign = ContentAlignment.MiddleCenter
            };

            _stopHintLabel = new Label
            {
                Text = "或点击右上角 × 关闭",
                Font = new Font("Microsoft YaHei", 7),
                ForeColor = Color.FromArgb(150, 150, 150),
                Location = new Point(10, 110),
                Size = new Size(panel.Width - 20, 15),
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

