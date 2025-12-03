using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AutoDaily.UI.Forms
{
    public partial class RunningOverlayForm : Form
    {
        // 字号规范常量（参考 Apple Human Interface Guidelines）
        // 原则：清晰易读、层次分明、最小字号不小于 11pt
        private const float FONT_SIZE_TITLE = 13f;      // 标题 - 突出显示
        private const float FONT_SIZE_STATUS = 11f;     // 状态文字 - 重要信息
        private const float FONT_SIZE_WARNING = 11f;    // 警告提示 - 需要清晰可见
        private const float FONT_SIZE_HINT = 10f;       // 小提示 - 最小字号

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
            
            // 缩小窗口，避免遮挡（基础尺寸280x120）
            Size = new Size(280, 120);
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
                Size = new Size(Width - 10, Height - 30),
                Location = new Point(5, 25),
                BackColor = Color.White
            };

            // 圆角
            var path = new GraphicsPath();
            int radius = 10;
            path.AddArc(0, 0, radius * 2, radius * 2, 180, 90);
            path.AddArc(panel.Width - radius * 2, 0, radius * 2, radius * 2, 270, 90);
            path.AddArc(panel.Width - radius * 2, panel.Height - radius * 2, radius * 2, radius * 2, 0, 90);
            path.AddArc(0, panel.Height - radius * 2, radius * 2, radius * 2, 90, 90);
            path.CloseFigure();
            panel.Region = new Region(path);

            _titleLabel = new Label
            {
                Text = "🤖 正在运行",
                Font = new Font("Microsoft YaHei", FONT_SIZE_TITLE, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 122, 204), // 蓝色 RGB: 0,122,204
                Location = new Point(10, 8),
                Size = new Size(panel.Width - 20, 20),
                TextAlign = ContentAlignment.MiddleCenter
            };

            _statusLabel = new Label
            {
                Text = "准备中...",
                Font = new Font("Microsoft YaHei", FONT_SIZE_STATUS, FontStyle.Regular),
                ForeColor = Color.FromArgb(60, 60, 60), // 灰色 RGB: 60,60,60
                Location = new Point(10, 30),
                Size = new Size(panel.Width - 20, 18),
                TextAlign = ContentAlignment.MiddleCenter
            };

            _progressBar = new ProgressBar
            {
                Location = new Point(10, 50),
                Size = new Size(panel.Width - 20, 15), // 15px高，符合文档要求
                Style = ProgressBarStyle.Continuous
            };
            // 进度条颜色将在Paint事件中设置为绿色 RGB: 76,175,80
            _progressBar.Paint += ProgressBar_Paint;

            _warningLabel = new Label
            {
                Text = "按 F10 停止",
                Font = new Font("Microsoft YaHei", FONT_SIZE_WARNING, FontStyle.Regular),
                ForeColor = Color.FromArgb(255, 152, 0),
                Location = new Point(10, 68),
                Size = new Size(panel.Width - 20, 15),
                TextAlign = ContentAlignment.MiddleCenter
            };

            _stopHintLabel = new Label
            {
                Text = "或点击 × 关闭",
                Font = new Font("Microsoft YaHei", FONT_SIZE_HINT, FontStyle.Regular),
                ForeColor = Color.FromArgb(150, 150, 150),
                Location = new Point(10, 85),
                Size = new Size(panel.Width - 20, 12),
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
            
            // 根据文档要求：执行步骤 X/Y: 动作类型
            string statusText = $"执行步骤 {current}/{total}: {status}";
            
            if (InvokeRequired)
            {
                Invoke(new System.Action(() =>
                {
                    _statusLabel.Text = statusText;
                    _progressBar.Maximum = total;
                    _progressBar.Value = current;
                }));
            }
            else
            {
                _statusLabel.Text = statusText;
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

        private void ProgressBar_Paint(object sender, PaintEventArgs e)
        {
            // 自定义绘制进度条为绿色 RGB: 76,175,80
            var progressBar = sender as ProgressBar;
            if (progressBar == null) return;

            var rect = progressBar.ClientRectangle;
            var progress = progressBar.Maximum > 0 
                ? (int)(rect.Width * (double)progressBar.Value / progressBar.Maximum) 
                : 0;

            // 绘制背景
            e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(240, 240, 240)), rect);

            // 绘制进度（绿色 RGB: 76,175,80）
            if (progress > 0)
            {
                var progressRect = new Rectangle(rect.X, rect.Y, progress, rect.Height);
                e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(76, 175, 80)), progressRect);
            }
        }
    }
}

