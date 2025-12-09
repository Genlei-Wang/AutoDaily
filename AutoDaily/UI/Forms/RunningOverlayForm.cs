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

        private Label _statusLabel;
        private Label _warningLabel;
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
            
            // 精简窗口，非常小，避免遮挡（基础尺寸180x60）
            Size = new Size(180, 60);
            TopMost = true;
            ShowInTaskbar = false;
            BackColor = Color.Transparent; // 透明背景
            StartPosition = FormStartPosition.Manual;
            // 放在屏幕右下角（业界常见位置，不遮挡操作）
            Location = new Point(
                Screen.PrimaryScreen.WorkingArea.Right - Width - 10,
                Screen.PrimaryScreen.WorkingArea.Bottom - Height - 10);
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.DoubleBuffer, true);
            
            // HUD面板（精简小窗口，半透明）
            var panel = new Panel
            {
                Size = new Size(Width, Height),
                Location = new Point(0, 0),
                BackColor = Color.FromArgb(240, 240, 240, 240) // 半透明白色
            };

            // 圆角
            var path = new GraphicsPath();
            int radius = 8;
            path.AddArc(0, 0, radius * 2, radius * 2, 180, 90);
            path.AddArc(panel.Width - radius * 2, 0, radius * 2, radius * 2, 270, 90);
            path.AddArc(panel.Width - radius * 2, panel.Height - radius * 2, radius * 2, radius * 2, 0, 90);
            path.AddArc(0, panel.Height - radius * 2, radius * 2, radius * 2, 90, 90);
            path.CloseFigure();
            panel.Region = new Region(path);

            // 精简：只显示进度和停止快捷键
            _statusLabel = new Label
            {
                Text = "5/12",
                Font = new Font("Microsoft YaHei", 12, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 122, 255),
                Location = new Point(8, 8),
                Size = new Size(60, 20),
                TextAlign = ContentAlignment.MiddleLeft
            };

            _warningLabel = new Label
            {
                Text = "F10停止",
                Font = new Font("Microsoft YaHei", 9, FontStyle.Regular),
                ForeColor = Color.FromArgb(142, 142, 147),
                Location = new Point(70, 10),
                Size = new Size(100, 16),
                TextAlign = ContentAlignment.MiddleLeft
            };

            _progressBar = new ProgressBar
            {
                Location = new Point(8, 32),
                Size = new Size(panel.Width - 16, 8), // 精简高度
                Style = ProgressBarStyle.Continuous
            };
            _progressBar.Paint += ProgressBar_Paint;

            panel.Controls.Add(_statusLabel);
            panel.Controls.Add(_warningLabel);
            panel.Controls.Add(_progressBar);

            Controls.Add(panel);
        }

        public void UpdateProgress(int current, int total, string status)
        {
            _currentStep = current;
            _totalSteps = total;
            
            // 精简显示：只显示进度 5/12
            string statusText = $"{current}/{total}";
            
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

