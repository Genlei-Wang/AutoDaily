using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AutoDaily.UI.Controls
{
    public class ToggleSwitch : Control
    {
        private bool _checked = false;
        private Color _checkedColor = Color.FromArgb(0, 122, 204); // #007ACC
        private Color _uncheckedColor = Color.FromArgb(200, 200, 200);
        private int _thumbSize = 20;
        private int _padding = 2;

        public bool Checked
        {
            get => _checked;
            set
            {
                if (_checked != value)
                {
                    _checked = value;
                    Invalidate();
                    OnCheckedChanged(EventArgs.Empty);
                }
            }
        }

        public event EventHandler CheckedChanged;

        public ToggleSwitch()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.DoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            
            Size = new Size(50, 26);
            Cursor = Cursors.Hand;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // 背景轨道
            var trackRect = new Rectangle(_padding, _padding, 
                Width - _padding * 2, Height - _padding * 2);
            var trackColor = _checked ? _checkedColor : _uncheckedColor;
            
            using (var brush = new SolidBrush(trackColor))
            {
                g.FillRoundedRectangle(brush, trackRect, trackRect.Height / 2);
            }

            // 滑块
            int thumbX = _checked ? Width - _thumbSize - _padding : _padding;
            var thumbRect = new Rectangle(thumbX, _padding, _thumbSize, Height - _padding * 2);
            
            using (var brush = new SolidBrush(Color.White))
            {
                g.FillEllipse(brush, thumbRect);
            }

            // 边框
            using (var pen = new Pen(Color.FromArgb(100, 0, 0, 0), 1))
            {
                g.DrawRoundedRectangle(pen, trackRect, trackRect.Height / 2);
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            Checked = !Checked;
        }

        protected virtual void OnCheckedChanged(EventArgs e)
        {
            CheckedChanged?.Invoke(this, e);
        }
    }

    // 扩展方法用于绘制圆角矩形
    public static class GraphicsExtensions
    {
        public static void FillRoundedRectangle(this Graphics g, Brush brush, Rectangle rect, int radius)
        {
            using (var path = GetRoundedRectanglePath(rect, radius))
            {
                g.FillPath(brush, path);
            }
        }

        public static void DrawRoundedRectangle(this Graphics g, Pen pen, Rectangle rect, int radius)
        {
            using (var path = GetRoundedRectanglePath(rect, radius))
            {
                g.DrawPath(pen, path);
            }
        }

        private static GraphicsPath GetRoundedRectanglePath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int diameter = radius * 2;

            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();

            return path;
        }
    }
}

