using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace EasyDownloader
{
    // 깜빡임 방지 FlowLayoutPanel
    public class DoubleBufferedFlowLayoutPanel : FlowLayoutPanel
    {
        public DoubleBufferedFlowLayoutPanel()
        {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.UpdateStyles();
        }
    }

    // 마인크래프트 시그니처 에메랄드/다이아몬드 토글 스위치
    public class ToggleSwitch : Control
    {
        private bool _checked = true;
        public event EventHandler CheckedChanged;

        public bool Checked
        {
            get { return _checked; }
            set
            {
                if (_checked != value)
                {
                    _checked = value;
                    this.Invalidate();
                    if (CheckedChanged != null)
                        CheckedChanged(this, EventArgs.Empty);
                }
            }
        }

        // 테마 색상 (마인크래프트 크리퍼/에메랄드 그린 시그니처)
        public Color OnColor { get; set; }
        public Color OffColor { get; set; }
        public Color KnobColor { get; set; }

        public ToggleSwitch()
        {
            this.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.Size = new Size(52, 26);
            this.Cursor = Cursors.Hand;
            // 마인크래프트 크리퍼 & 에메랄드 그린 시그니처
            this.OnColor = Color.FromArgb(34, 197, 94);     // Minecraft Green (#22c55e)
            this.OffColor = Color.FromArgb(45, 50, 62);    // Obsidian Dark Gray
            this.KnobColor = Color.White;
        }

        protected override void OnClick(EventArgs e)
        {
            this.Checked = !this.Checked;
            base.OnClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int arcRadius = this.Height - 1;
            Rectangle rect = new Rectangle(0, 0, this.Width - 1, this.Height - 1);

            // 1. 배경 트랙 (둥근 마크 블록 바)
            using (GraphicsPath path = GetPillPath(rect, arcRadius))
            {
                using (Brush brush = new SolidBrush(_checked ? OnColor : OffColor))
                {
                    g.FillPath(brush, path);
                }

                // 미묘한 마크 테두리 음영
                using (Pen borderPen = new Pen(_checked ? Color.FromArgb(22, 163, 74) : Color.FromArgb(30, 35, 45), 1))
                {
                    g.DrawPath(borderPen, path);
                }
            }

            // 2. 내부 노브 (스위치 버튼)
            int knobPadding = 3;
            int knobSize = this.Height - (knobPadding * 2);
            int knobX = _checked ? (this.Width - knobSize - knobPadding) : knobPadding;
            Rectangle knobRect = new Rectangle(knobX, knobPadding, knobSize, knobSize);

            using (Brush knobBrush = new SolidBrush(KnobColor))
            {
                g.FillEllipse(knobBrush, knobRect);
            }

            // ON 일 때 안쪽에 마크 그린 도트
            if (_checked)
            {
                using (Brush dotBrush = new SolidBrush(Color.FromArgb(22, 163, 74)))
                {
                    int dotSize = 6;
                    g.FillEllipse(dotBrush, knobX + (knobSize - dotSize) / 2, knobPadding + (knobSize - dotSize) / 2, dotSize, dotSize);
                }
            }
        }

        private GraphicsPath GetPillPath(Rectangle r, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int diameter = radius;
            path.AddArc(r.X, r.Y, diameter, diameter, 90, 180);
            path.AddArc(r.Right - diameter, r.Y, diameter, diameter, 270, 180);
            path.CloseFigure();
            return path;
        }
    }

    // 마인크래프트 다이아몬드/에메랄드 그라데이션 프로그레스 바
    public class ModernProgressBar : Control
    {
        private int _value = 0;
        private int _maximum = 100;

        public int Value
        {
            get { return _value; }
            set
            {
                _value = Math.Max(0, Math.Min(_maximum, value));
                this.Invalidate();
            }
        }

        public int Maximum
        {
            get { return _maximum; }
            set
            {
                _maximum = Math.Max(1, value);
                this.Invalidate();
            }
        }

        public Color StartColor { get; set; }
        public Color EndColor { get; set; }
        public Color TrackColor { get; set; }

        public ModernProgressBar()
        {
            this.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.Size = new Size(200, 8);
            // 마인크래프트 다이아몬드 블루 -> 에메랄드 그린 그라데이션
            this.StartColor = Color.FromArgb(14, 165, 233); // Diamond Sky Blue
            this.EndColor = Color.FromArgb(34, 197, 94);   // Creeper Green
            this.TrackColor = Color.FromArgb(25, 28, 38);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // 트랙 배경
            using (Brush trackBrush = new SolidBrush(TrackColor))
            {
                g.FillRectangle(trackBrush, 0, 0, this.Width, this.Height);
            }

            // 채워진 바
            if (_value > 0)
            {
                float progressWidth = (float)this.Width * ((float)_value / (float)_maximum);
                using (Brush barBrush = new LinearGradientBrush(
                    new RectangleF(0, 0, Math.Max(1, progressWidth), this.Height),
                    StartColor,
                    EndColor,
                    LinearGradientMode.Horizontal))
                {
                    g.FillRectangle(barBrush, 0, 0, progressWidth, this.Height);
                }
            }
        }
    }

    // 마인크래프트 옵시디언/스톤 질감의 둥근 패널
    public class RoundedPanel : Panel
    {
        public int BorderRadius { get; set; }
        public Color BorderColor { get; set; }
        public int BorderWidth { get; set; }

        public RoundedPanel()
        {
            this.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.BorderRadius = 8;
            this.BorderColor = Color.FromArgb(42, 48, 62);
            this.BorderWidth = 1;
            this.BackColor = Color.FromArgb(22, 26, 36);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle rect = new Rectangle(0, 0, this.Width - 1, this.Height - 1);
            using (GraphicsPath path = GetRoundedRect(rect, BorderRadius))
            {
                using (Brush brush = new SolidBrush(this.BackColor))
                {
                    g.FillPath(brush, path);
                }

                if (BorderWidth > 0 && BorderColor != Color.Transparent)
                {
                    using (Pen pen = new Pen(BorderColor, BorderWidth))
                    {
                        g.DrawPath(pen, path);
                    }
                }
            }
        }

        private GraphicsPath GetRoundedRect(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int diameter = radius * 2;
            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    // 마인크래프트 런처 스타일 3D 블록 버튼 (Minecraft Creeper Green Button)
    public class McBlockButton : Button
    {
        public Color TopColor { get; set; }
        public Color BottomColor { get; set; }
        public Color EdgeColor { get; set; }

        public McBlockButton()
        {
            this.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.FlatStyle = FlatStyle.Flat;
            this.FlatAppearance.BorderSize = 0;
            this.Cursor = Cursors.Hand;
            this.Font = new Font("Segoe UI", 10.5F, FontStyle.Bold);
            this.ForeColor = Color.White;

            // 마인크래프트 공식 런처 그린 (#2ecc71 / #27ae60)
            this.TopColor = Color.FromArgb(34, 197, 94);     // 밝은 그린
            this.BottomColor = Color.FromArgb(22, 163, 74);  // 딥 그린
            this.EdgeColor = Color.FromArgb(21, 128, 61);    // 하단 입체 테두리
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle rect = new Rectangle(0, 0, this.Width, this.Height);

            if (!this.Enabled)
            {
                using (Brush disabledBrush = new SolidBrush(Color.FromArgb(50, 56, 70)))
                {
                    g.FillRectangle(disabledBrush, rect);
                }
                TextRenderer.DrawText(g, this.Text, this.Font, rect, Color.FromArgb(120, 130, 150),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }

            // 상단-하단 입체 그라데이션
            using (LinearGradientBrush lgb = new LinearGradientBrush(rect, TopColor, BottomColor, LinearGradientMode.Vertical))
            {
                g.FillRectangle(lgb, rect);
            }

            // 마크 특유의 3D 블록 림 (상단 하이라이트, 하단 그림자 라인)
            using (Pen lightPen = new Pen(Color.FromArgb(100, 255, 255, 255), 1.5F))
            {
                g.DrawLine(lightPen, 1, 1, this.Width - 2, 1);
            }
            using (Pen darkPen = new Pen(EdgeColor, 3.5F))
            {
                g.DrawLine(darkPen, 0, this.Height - 2, this.Width, this.Height - 2);
            }

            // 텍스트 드로우 (미세한 텍스트 그림자로 마크 감성 연출, NoPrefix로 & 문자 보존)
            Rectangle shadowRect = new Rectangle(rect.X, rect.Y + 1, rect.Width, rect.Height);
            TextRenderer.DrawText(g, this.Text, this.Font, shadowRect, Color.FromArgb(15, 80, 35),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(g, this.Text, this.Font, rect, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
    }
}
