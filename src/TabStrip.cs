using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace VarNamer
{
    // 下划线式标签条（Fluent / WinUI 形态）：
    //   纯文字（选中加粗、未选弱化） + 2px 强调色指示条（切换时滑动） + 通栏分隔线
    //   比“胶囊按钮当标签”更克制，也更像正经的标签页
    public class TabStrip : Control
    {
        public event EventHandler SelectedIndexChanged;

        private string[] items = new string[0];
        private int selected = -1;
        private int hoverIndex = -1;
        private bool small;

        private double indX, indW;          // 指示条当前位置（动画中）
        private Tween indTw;

        public int TabGap = Theme.S(22);
        public int LineHeight = 2;
        public int Indent = 0;          // 左侧缩进（让标签与内容左对齐）

        public TabStrip()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            BackColor = Theme.Bg;
            Height = Theme.S(38);
            TabStop = true;
            Cursor = Cursors.Hand;
        }

        public bool Small
        {
            get { return small; }
            set { small = value; Invalidate(); }
        }

        public string[] Items
        {
            get { return items; }
            set { items = value == null ? new string[0] : value; Measure(); Invalidate(); }
        }

        public int SelectedIndex
        {
            get { return selected; }
            set
            {
                if (value < -1 || value >= items.Length || value == selected) return;
                selected = value;
                SlideIndicator();
                Invalidate();
                if (SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty);
            }
        }

        private Font FontOf(bool active)
        {
            if (small) return active ? Theme.FontSmallBold : Theme.FontSmall;
            return active ? Theme.FontBodyBold : Theme.FontBody;
        }

        private int[] tabX = new int[0];
        private int[] tabW = new int[0];

        private void Measure()
        {
            tabX = new int[items.Length];
            tabW = new int[items.Length];
            int x = Indent;
            for (int i = 0; i < items.Length; i++)
            {
                int w = TextRenderer.MeasureText(items[i], FontOf(false)).Width;
                int wb = TextRenderer.MeasureText(items[i], FontOf(true)).Width;
                if (wb > w) w = wb;                 // 加粗后不变宽，避免切换时文字抖动
                tabX[i] = x;
                tabW[i] = w;
                x += w + TabGap;
            }
            if (selected >= 0 && selected < items.Length) { indX = tabX[selected]; indW = tabW[selected]; }
        }

        private void SlideIndicator()
        {
            if (selected < 0 || selected >= items.Length) return;
            if (indTw != null) indTw.Stop();
            double fromX = indX, fromW = indW;
            double toX = tabX[selected], toW = tabW[selected];
            if (fromW <= 0) { indX = toX; indW = toW; return; }
            indTw = Tween.Run(Theme.DurMed, delegate(double k)
            {
                if (IsDisposed) return;
                double e = Curves.DecelerateMax(k);
                indX = fromX + (toX - fromX) * e;
                indW = fromW + (toW - fromW) * e;
                Invalidate();
            }, null);
        }

        private int HitTest(Point p)
        {
            for (int i = 0; i < items.Length; i++)
                if (p.X >= tabX[i] - TabGap / 2 && p.X <= tabX[i] + tabW[i] + TabGap / 2) return i;
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int i = HitTest(e.Location);
            if (i != hoverIndex) { hoverIndex = i; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hoverIndex != -1) { hoverIndex = -1; Invalidate(); }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            int i = HitTest(e.Location);
            if (i >= 0) { Focus(); SelectedIndex = i; }
        }

        protected override bool IsInputKey(Keys keyData)
        {
            if (keyData == Keys.Left || keyData == Keys.Right) return true;
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Left) { SelectedIndex = Math.Max(0, selected - 1); e.Handled = true; }
            else if (e.KeyCode == Keys.Right) { SelectedIndex = Math.Min(items.Length - 1, selected + 1); e.Handled = true; }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Measure();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.EraseBackground(this, e.Graphics);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            int baseY = Height - LineHeight;
            using (Pen pen = new Pen(Theme.Border))
                g.DrawLine(pen, 0, Height - 1, Width, Height - 1);        // 通栏分隔线

            for (int i = 0; i < items.Length; i++)
            {
                bool active = i == selected;
                Color c = active ? Theme.TextPrimary : (i == hoverIndex ? Theme.TextSecondary : Theme.TextMuted);
                TextRenderer.DrawText(g, items[i], FontOf(active),
                    new Rectangle(tabX[i], 0, tabW[i] + Theme.S(2), baseY), c,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            }

            if (selected >= 0 && indW > 0)
            {
                using (SolidBrush b = new SolidBrush(Theme.Accent))
                    g.FillRectangle(b, (float)indX, baseY, (float)indW, LineHeight);
            }
        }
    }
}
