using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace VarNamer
{
    // 自绘深色下拉框。原生 ComboBox 在开启视觉样式时会忽略 BackColor（画成系统浅灰），
    // 因此完全自绘；展开的选项列表是“窗口内覆盖层”，不再使用独立顶层窗口，
    // 避免出现浮在窗口之上、失焦不关闭、半透明叠字等问题。
    public class DarkCombo : Control
    {
        private readonly List<string> items = new List<string>();
        private int selectedIndex = -1;
        private bool hover, open;
        private DropDownPanel popup;

        public event EventHandler SelectedIndexChanged;

        public DarkCombo()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            Font = Theme.FontBody;
            BackColor = Theme.Surface;
            Cursor = Cursors.Hand;
            TabStop = true;
        }

        internal List<string> ItemsList { get { return items; } }
        internal int SelectedIndexRaw { get { return selectedIndex; } }

        public override Size GetPreferredSize(Size proposedSize)
        {
            Size s = base.GetPreferredSize(proposedSize);
            return new Size(s.Width, Theme.S(31));
        }

        public void AddRange(object[] values)
        {
            for (int i = 0; i < values.Length; i++) items.Add(values[i] == null ? "" : values[i].ToString());
            Invalidate();
        }

        public string SelectedItem
        {
            get { return (selectedIndex >= 0 && selectedIndex < items.Count) ? items[selectedIndex] : null; }
        }

        public int SelectedIndex
        {
            get { return selectedIndex; }
            set
            {
                if (value < -1 || value >= items.Count) return;
                if (selectedIndex == value) return;
                selectedIndex = value;
                Invalidate();
                if (SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty);
            }
        }

        public void SelectText(string text)
        {
            int i = items.IndexOf(text);
            if (i >= 0) SelectedIndex = i;
        }

        protected override void OnParentChanged(EventArgs e)
        {
            base.OnParentChanged(e);
            Color b = Theme.SurfaceBehind(this);
            if (b.A == 255) BackColor = b;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            Toggle();
            base.OnMouseDown(e);
        }

        public void Toggle()
        {
            if (open) CloseDrop();
            else OpenDrop();
        }

        private void OpenDrop()
        {
            if (items.Count == 0) return;
            Form host = FindForm();
            if (host == null) return;
            open = true;
            Invalidate();
            popup = new DropDownPanel(this);
            popup.Closed += delegate(object s, EventArgs e)
            {
                open = false;
                popup = null;
                Invalidate();
            };
            popup.OpenOn(host, this, selectedIndex);
        }

        public void CloseDrop()
        {
            if (popup != null) popup.ClosePanel();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme.EraseBackground(this, g);          // 先擦干净再画
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Color bg = (hover || open) ? Theme.BtnHover : Theme.Elevated;
            using (GraphicsPath p = Theme.RoundedRect(r, Theme.S(7)))
            {
                using (SolidBrush b = new SolidBrush(bg)) g.FillPath(b, p);
                using (Pen pen = new Pen(open ? Theme.Accent : Theme.Border)) g.DrawPath(pen, p);
            }
            int cx = Width - Theme.S(15), cy = Height / 2;
            using (SolidBrush b = new SolidBrush(open ? Theme.AccentHover : Theme.TextMuted))
                g.FillPolygon(b, new Point[] {
                    new Point(cx - Theme.S(5), cy - Theme.S(2)),
                    new Point(cx + Theme.S(5), cy - Theme.S(2)),
                    new Point(cx, cy + Theme.S(3)) });

            string t = SelectedItem;
            if (!string.IsNullOrEmpty(t))
                TextRenderer.DrawText(g, t, Font, new Rectangle(Theme.S(10), 0, Width - Theme.S(28), Height), Theme.TextPrimary,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    // 窗口内覆盖层：自身是窗体的子控件，天然不透明、随窗口一起被抓图、点击别处必然关闭
    internal class DropDownPanel : Control, IMessageFilter
    {
        public event EventHandler Closed;

        private DarkCombo owner;
        private List<string> items;
        private int hoverIndex = -1;
        private int selected;
        private int rowH;
        private int pad = 1;
        private int openedAt;
        private Action<int> pickCallback;
        private string header = "";
        private int minWidth = 0;
        private int scrollOffset;          // 滚动偏移（行）
        private int visibleRows = 8;       // 固定显示行数（超出则滚动）
        private bool closed;

        // 通用列表弹层（供“最近使用”等功能复用）
        public DropDownPanel(List<string> list, string headerText, int minWidthPx, Action<int> onPick)
        {
            items = list;
            header = headerText == null ? "" : headerText;
            minWidth = minWidthPx;
            pickCallback = onPick;
            rowH = Theme.S(28);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
            BackColor = Theme.Border;
            visibleRows = Math.Min(items.Count, 8);
            Size = new Size(Math.Max(minWidth, Theme.S(170)),
                visibleRows * rowH + Theme.S(2) * pad + (header.Length > 0 ? Theme.S(22) : 0) + (items.Count > visibleRows ? Theme.S(2) : 0));
        }

        public void OpenOnAnchor(Form host, Control anchor)
        {
            Point below = anchor.PointToScreen(new Point(0, anchor.Height + Theme.S(2)));
            Point pt = host.PointToClient(below);
            int h = Height, w = Width;
            Rectangle client = host.ClientRectangle;
            if (pt.Y + h > client.Bottom)
            {
                Point above = anchor.PointToScreen(new Point(0, -h - Theme.S(2)));
                int y2 = host.PointToClient(above).Y;
                pt = new Point(pt.X, y2 >= client.Top ? y2 : Math.Max(client.Top, client.Bottom - h));
            }
            if (pt.X + w > client.Right) pt.X = Math.Max(client.Left, client.Right - w);
            if (pt.X < client.Left) pt.X = client.Left;
            if (pt.Y < client.Top) pt.Y = client.Top;
            Bounds = new Rectangle(pt.X, pt.Y, w, h);
            openedAt = Environment.TickCount;
            host.Controls.Add(this);
            BringToFront();
            Application.AddMessageFilter(this);
            host.Resize += OnHostChanged;
            host.Move += OnHostChanged;
            host.Deactivate += OnHostDeactivated;
            Invalidate();
        }

        public DropDownPanel(DarkCombo ownerCombo)
        {
            owner = ownerCombo;
            items = ownerCombo.ItemsList;
            selected = ownerCombo.SelectedIndexRaw;
            hoverIndex = selected;
            rowH = Theme.S(28);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Border;
        }

        public void OpenOn(Form host, Control anchor, int selectedIndex)
        {
            visibleRows = Math.Min(items.Count, 8);      // 固定行数，超出滚动
            scrollOffset = 0;
            int h = visibleRows * rowH + Theme.S(2) * 2;
            int w = Math.Max(anchor.Width, Theme.S(120));
            Point below = anchor.PointToScreen(new Point(0, anchor.Height + Theme.S(2)));
            Point pt = host.PointToClient(below);
            Rectangle client = host.ClientRectangle;
            if (pt.Y + h > client.Bottom)                       // 放不下就翻到上方
            {
                Point above = anchor.PointToScreen(new Point(0, -h - Theme.S(2)));
                int y2 = host.PointToClient(above).Y;
                if (y2 >= client.Top) pt = new Point(pt.X, y2);
                else pt = new Point(pt.X, Math.Max(client.Top, client.Bottom - h));  // 仍放不下则贴窗底
            }
            if (pt.X + w > client.Right) pt.X = Math.Max(client.Left, client.Right - w);
            if (pt.X < client.Left) pt.X = client.Left;
            if (pt.Y < client.Top) pt.Y = client.Top;
            Bounds = new Rectangle(pt.X, pt.Y, w, h);

            host.Controls.Add(this);
            BringToFront();
            Application.AddMessageFilter(this);
            host.Resize += OnHostChanged;
            host.Move += OnHostChanged;
            host.Deactivate += OnHostDeactivated;
            Invalidate();
        }

        private void OnHostChanged(object sender, EventArgs e) { ClosePanel(); }
        private void OnHostDeactivated(object sender, EventArgs e) { ClosePanel(); }

        public void ClosePanel()
        {
            if (closed) return;
            closed = true;
            try { Application.RemoveMessageFilter(this); } catch (Exception) { }
            Form host = Parent as Form;
            if (host != null)
            {
                host.Resize -= OnHostChanged;
                host.Move -= OnHostChanged;
                host.Deactivate -= OnHostDeactivated;
            }
            if (Parent != null) Parent.Controls.Remove(this);
            if (Closed != null) Closed(this, EventArgs.Empty);
            Dispose();
        }

        public bool PreFilterMessage(ref Message m)
        {
            const int WM_LBUTTONDOWN = 0x0201, WM_RBUTTONDOWN = 0x0204, WM_MBUTTONDOWN = 0x0207,
                       WM_MOUSEWHEEL = 0x020A, WM_KEYDOWN = 0x0100;
            int msg = m.Msg;
            if (msg == WM_LBUTTONDOWN || msg == WM_RBUTTONDOWN || msg == WM_MBUTTONDOWN || msg == WM_MOUSEWHEEL)
            {
                if (m.HWnd != Handle) ClosePanel();          // 点面板以外的任何地方都关闭
                return false;
            }
            if (msg == WM_KEYDOWN)
            {
                int vk = m.WParam.ToInt32();
                if (vk == 0x1B) { ClosePanel(); return true; }                       // Esc
                if (vk == 0x0D) { Pick(hoverIndex); return true; }                   // Enter
                if (vk == 0x28) { if (hoverIndex < items.Count - 1) hoverIndex++; EnsureVisible(); Invalidate(); return true; }  // Down
                if (vk == 0x26) { if (hoverIndex > 0) hoverIndex--; EnsureVisible(); Invalidate(); return true; }               // Up
            }
            return false;
        }

        private int RowAt(int y, int topPadding)
        {
            int i = (y - topPadding - Theme.S(2) * pad) / rowH;
            if (i < 0 || i >= visibleRows) return -1;
            i += scrollOffset;
            if (i < 0 || i >= items.Count) return -1;
            return i;
        }

        private int TopPadding()
        {
            return header.Length > 0 ? Theme.S(22) : 0;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int i = RowAt(e.Y, TopPadding());
            if (i != hoverIndex) { hoverIndex = i; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hoverIndex = -1;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            int i = RowAt(e.Y, TopPadding());
            if (i >= 0) Pick(i);
            base.OnMouseDown(e);
        }

        // 滚轮滚动（固定行数 + 滚动选择）
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            ScrollBy(e.Delta > 0 ? -1 : 1);
            base.OnMouseWheel(e);
        }

        internal void ScrollBy(int rows)
        {
            if (items.Count <= visibleRows) return;
            int max = items.Count - visibleRows;
            int v = scrollOffset + rows;
            if (v < 0) v = 0;
            if (v > max) v = max;
            if (v != scrollOffset) { scrollOffset = v; Invalidate(); }
        }

        // 保证当前高亮行在可见范围内（键盘导航用）
        internal void EnsureVisible()
        {
            if (hoverIndex < 0) return;
            if (hoverIndex < scrollOffset) scrollOffset = hoverIndex;
            else if (hoverIndex >= scrollOffset + visibleRows) scrollOffset = hoverIndex - visibleRows + 1;
        }

        private void Pick(int i)
        {
            if (i >= 0 && i < items.Count)
            {
                selected = i;
                if (owner != null) owner.SelectedIndex = i;
            }
            ClosePanel();
            if (pickCallback != null && i >= 0 && i < items.Count) pickCallback(i);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.Border);                                   // 不透明底 + 1px 边框
            Rectangle inner = new Rectangle(Theme.S(2), Theme.S(2), Width - Theme.S(4), Height - Theme.S(4));
            using (SolidBrush b = new SolidBrush(Theme.Elevated)) g.FillRectangle(b, inner);
            int top = inner.Y;
            if (header.Length > 0)
            {
                Rectangle hr = new Rectangle(inner.X, inner.Y, inner.Width, Theme.S(22));
                using (SolidBrush b = new SolidBrush(Theme.Surface)) g.FillRectangle(b, hr);
                TextRenderer.DrawText(g, header, Theme.FontSmall, new Rectangle(hr.X + Theme.S(10), hr.Y, hr.Width - Theme.S(14), hr.Height),
                    Theme.TextMuted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                top += Theme.S(22);
            }
            bool needBar = items.Count > visibleRows;
            int textWidth = needBar ? inner.Width - Theme.S(8) : inner.Width;
            for (int k = 0; k < visibleRows; k++)
            {
                int i = scrollOffset + k;
                if (i >= items.Count) break;
                Rectangle row = new Rectangle(inner.X, top + k * rowH, textWidth, rowH);
                if (i == hoverIndex)
                {
                    using (SolidBrush b = new SolidBrush(Theme.AccentSoft)) g.FillRectangle(b, row);
                }
                Color fg = (i == selected) ? Theme.AccentHover : Theme.TextPrimary;
                TextRenderer.DrawText(g, items[i], Theme.FontBody,
                    new Rectangle(row.X + Theme.S(10), row.Y, row.Width - Theme.S(14), row.Height), fg,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
            if (needBar)
            {
                // 右侧滚动条：轨道 + 滑块（提示"还有更多"）
                Rectangle track = new Rectangle(inner.Right - Theme.S(6), inner.Y + Theme.S(3), Theme.S(3), inner.Height - Theme.S(6));
                using (SolidBrush b = new SolidBrush(Theme.Border)) g.FillRectangle(b, track);
                int total = items.Count;
                int thumbH = Math.Max(Theme.S(18), track.Height * visibleRows / total);
                int maxOffset = Math.Max(1, total - visibleRows);
                int thumbY = track.Y + (track.Height - thumbH) * scrollOffset / maxOffset;
                using (SolidBrush b = new SolidBrush(Theme.TextMuted))
                    g.FillRectangle(b, new Rectangle(track.X, thumbY, track.Width, thumbH));
            }
        }
    }
}
