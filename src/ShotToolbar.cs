using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace VarNamer
{
    // 颜色色块按钮
    public class SwatchButton : Control
    {
        public Color ColorValue;
        public bool SelectedFlag;
        public SwatchButton(Color c)
        {
            ColorValue = c;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Size = new Size(Theme.S(24), Theme.S(24));
            Cursor = Cursors.Hand;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme.EraseBackground(this, g);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(Theme.S(2), Theme.S(2), Width - Theme.S(5), Height - Theme.S(5));
            using (SolidBrush b = new SolidBrush(ColorValue)) g.FillEllipse(b, r);
            using (Pen p = new Pen(SelectedFlag ? Theme.Accent : Theme.Border, SelectedFlag ? Theme.S(2) : 1))
                g.DrawEllipse(p, r);
        }
    }

    // 截图工具栏：工具 / 线宽 / 颜色 / 动作
    public class ShotToolbar : Panel
    {
        private ShotForm owner;
        private List<FlatButton> toolButtons = new List<FlatButton>();
        private List<FlatButton> widthButtons = new List<FlatButton>();
        private List<SwatchButton> swatches = new List<SwatchButton>();
        private FlatButton btnUndo, btnCopy, btnSave, btnPin, btnCancel;

        public ShotToolbar(ShotForm form)
        {
            owner = form;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.FromArgb(28, 30, 36);
            Padding = new Padding(Theme.S(8));

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.BackColor = Color.Transparent;
            root.Margin = new Padding(0);
            root.ColumnCount = 1;
            root.RowCount = 2;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.S(36)));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.S(36)));
            Controls.Add(root);

            // 行1：工具 + 线宽
            FlowLayoutPanel r1 = new FlowLayoutPanel();
            r1.Dock = DockStyle.Fill;
            r1.WrapContents = false;
            r1.BackColor = Color.Transparent;
            r1.Margin = new Padding(0);
            AddTool(r1, ShotTool.None, "↖", "选择 / 调整选区（拖动内部移动，拖边角改大小）");
            AddTool(r1, ShotTool.Rect, "▭", "矩形");
            AddTool(r1, ShotTool.Ellipse, "◯", "椭圆");
            AddTool(r1, ShotTool.Arrow, "↗", "箭头");
            AddTool(r1, ShotTool.Pen, "✎", "画笔");
            AddTool(r1, ShotTool.Highlight, "▤", "高亮");
            AddTool(r1, ShotTool.Mosaic, "▦", "马赛克");
            AddTool(r1, ShotTool.Text, "A", "文字");
            Label sp = new Label();
            sp.Width = Theme.S(10);
            sp.Text = "";
            r1.Controls.Add(sp);
            AddWidth(r1, 2, "细");   // 线宽2 / 文字18px
            AddWidth(r1, 4, "中");   // 线宽4 / 文字26px
            AddWidth(r1, 7, "粗");   // 线宽7 / 文字36px
            root.Controls.Add(r1, 0, 0);

            // 行2：颜色 + 动作
            FlowLayoutPanel r2 = new FlowLayoutPanel();
            r2.Dock = DockStyle.Fill;
            r2.WrapContents = false;
            r2.BackColor = Color.Transparent;
            r2.Margin = new Padding(0);
            for (int i = 0; i < ShotDraw.Palette.Length; i++) AddSwatch(r2, ShotDraw.Palette[i]);
            Label sp2 = new Label();
            sp2.Width = Theme.S(12);
            sp2.Text = "";
            r2.Controls.Add(sp2);
            btnUndo = AddAction(r2, "撤销", S(58), delegate { owner.Undo(); });
            btnCopy = AddAction(r2, "复制", S(58), delegate { owner.DoCopy(); });
            btnSave = AddAction(r2, "保存", S(58), delegate { owner.DoSave(); });
            btnPin = AddAction(r2, "钉在桌面", S(84), delegate { owner.DoPin(); });
            FlatButton btnReselect = AddAction(r2, "重选", S(58), delegate { owner.ResetSelection(); owner.SyncToolbar(); });
            btnCancel = AddAction(r2, "取消", S(58), delegate { owner.Close(); });
            root.Controls.Add(r2, 0, 1);

            // 自适应尺寸
            Size = new Size(Math.Max(r1.PreferredSize.Width, r2.PreferredSize.Width) + Padding.Horizontal + Theme.S(4),
                            Theme.S(36) * 2 + Padding.Vertical + Theme.S(4));
            SyncState();
        }

        private static int S(int px) { return Theme.S(px); }

        private void AddTool(FlowLayoutPanel parent, ShotTool t, string glyph, string tip)
        {
            FlatButton b = new FlatButton();
            b.Text = glyph;
            b.Size = new Size(Theme.S(34), Theme.S(30));
            b.Radius = Theme.S(6);
            b.Margin = new Padding(0, 0, Theme.S(4), 0);
            b.Font = new Font("Segoe UI", 10f);
            b.Tag = t;
            b.Click += delegate(object s, EventArgs e)
            {
                owner.SetTool(t);
                SyncState();
            };
            ToolTip tipObj = new ToolTip();
            tipObj.SetToolTip(b, tip);
            toolButtons.Add(b);
            parent.Controls.Add(b);
        }

        private void AddWidth(FlowLayoutPanel parent, int w, string tip)
        {
            FlatButton b = new FlatButton();
            b.Text = tip;
            b.Size = new Size(Theme.S(40), Theme.S(30));
            b.Radius = Theme.S(6);
            b.Margin = new Padding(0, 0, Theme.S(4), 0);
            b.Font = Theme.FontSmall;
            b.Tag = w;
            b.Click += delegate(object s, EventArgs e)
            {
                owner.SetWidth(w);
                SyncState();
            };
            ToolTip wtip = new ToolTip();
            wtip.SetToolTip(b, tip + "（线宽 " + w + "，文字字号 " + ShotForm.TextFontSize(w) + "px）");
            widthButtons.Add(b);
            parent.Controls.Add(b);
        }

        private void AddSwatch(FlowLayoutPanel parent, Color c)
        {
            SwatchButton b = new SwatchButton(c);
            b.Margin = new Padding(0, Theme.S(3), Theme.S(4), 0);
            b.Click += delegate(object s, EventArgs e)
            {
                owner.SetColor(c);
                SyncState();
            };
            swatches.Add(b);
            parent.Controls.Add(b);
        }

        private FlatButton AddAction(FlowLayoutPanel parent, string text, int w, EventHandler onClick)
        {
            FlatButton b = new FlatButton();
            b.Text = text;
            b.Size = new Size(w, Theme.S(30));
            b.Radius = Theme.S(6);
            b.Margin = new Padding(0, 0, Theme.S(4), 0);
            b.Click += onClick;
            parent.Controls.Add(b);
            return b;
        }

        public void SyncState()
        {
            ShotTool cur = owner.CurrentTool();
            Color col = owner.CurrentColor();
            int wid = owner.CurrentWidth();
            for (int i = 0; i < toolButtons.Count; i++)
                toolButtons[i].Primary = ((ShotTool)toolButtons[i].Tag) == cur;
            for (int i = 0; i < widthButtons.Count; i++)
                widthButtons[i].Primary = ((int)widthButtons[i].Tag) == wid;
            for (int i = 0; i < swatches.Count; i++)
            {
                Color sc = swatches[i].ColorValue;      // 先取到局部变量（避免 CS1690：Control 是 MarshalByRefObject）
                swatches[i].SelectedFlag = sc.ToArgb() == col.ToArgb();
                swatches[i].Invalidate();
            }
            Invalidate(true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath p = Theme.RoundedRect(r, Theme.S(10)))
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(240, 28, 30, 36))) g.FillPath(b, p);
                using (Pen pen = new Pen(Color.FromArgb(120, 255, 255, 255))) g.DrawPath(pen, p);
            }
        }
    }

    // 钉在桌面的截图浮窗：可拖动、滚轮缩放、Ctrl+滚轮透明度、右键菜单、Esc/双击关闭
    public class PinForm : Form
    {
        private Bitmap img;
        private float zoom = 1f;
        private bool dragging, resizing, showHandles;
        private Point mouseDownScreen;
        private int resizeHandle = ShotGeom.HandleNone;
        private Rectangle rectAtStart;          // 屏幕坐标下的起始窗口矩形
        private double aspect = 1;
        private bool smoothMode;                // 拖动中用快速插值，松手后再出高清
        private bool applyingBounds;            // 防止"改尺寸 → 触发鼠标消息 → 再改尺寸"的回环
        private Point offset;
        private Size baseSize;

        public PinForm(Bitmap b)
        {
            img = b;
            baseSize = b.Size;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            DoubleBuffered = true;
            // ResizeRedraw 是关键：否则窗口放大/缩小时 WinForms 只让“新暴露的条带”失效，
            // 旧条带里留着上一尺寸的渲染 → 看起来就是“切片重叠/像被裁过”，过一会儿全量重绘才恢复
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
                | ControlStyles.Opaque, true);
            BackColor = Color.FromArgb(28, 30, 36);
            Padding = new Padding(1);
            Size = new Size(b.Width + 2, b.Height + 2);
            Location = Cursor.Position;
            if (Location.X + Width > Screen.PrimaryScreen.WorkingArea.Right)
                Location = new Point(Screen.PrimaryScreen.WorkingArea.Right - Width, Location.Y);
            if (Location.Y + Height > Screen.PrimaryScreen.WorkingArea.Bottom)
                Location = new Point(Location.X, Screen.PrimaryScreen.WorkingArea.Bottom - Height);

            ContextMenuStrip menu = new ContextMenuStrip();
            ToolStripMenuItem zoomInfo = new ToolStripMenuItem("当前大小 " + (int)Math.Round(zoom * 100) + "%");
            zoomInfo.Enabled = false;
            menu.Items.Add(zoomInfo);
            menu.Items.Add("原始大小 (100%)", null, delegate(object s, EventArgs e)
            {
                zoom = 1f;
                Size = new Size(baseSize.Width + 2, baseSize.Height + 2);
                Rectangle nr = ShotGeom.ClampToScreen(Bounds, Screen.FromPoint(Location).WorkingArea);
                Location = nr.Location;
                zoomInfo.Text = "当前大小 100%";
            });
            menu.Items.Add("适应屏幕", null, delegate(object s, EventArgs e)
            {
                Rectangle wa = Screen.FromPoint(Location).WorkingArea;
                double z = Math.Min((double)wa.Width / baseSize.Width, (double)wa.Height / baseSize.Height);
                z = Math.Min(1.0, Math.Max(0.2, z));
                zoom = (float)z;
                Size = new Size((int)(baseSize.Width * zoom) + 2, (int)(baseSize.Height * zoom) + 2);
                Location = new Point(wa.Left + Theme.S(20), wa.Top + Theme.S(20));
                zoomInfo.Text = "当前大小 " + (int)Math.Round(zoom * 100) + "%";
            });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("复制图片", null, delegate(object s, EventArgs e)
            {
                try { Clipboard.SetImage(img); } catch (Exception) { }
            });
            menu.Items.Add("另存为…", null, delegate(object s, EventArgs e) { SaveAs(); });
            ToolStripMenuItem top = new ToolStripMenuItem("始终置顶");
            top.CheckOnClick = true;
            top.Checked = true;
            top.CheckedChanged += delegate(object s, EventArgs e) { TopMost = top.Checked; };
            menu.Items.Add(top);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("关闭", null, delegate(object s, EventArgs e) { Close(); });
            ContextMenuStrip = menu;

            KeyPreview = true;
            KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Escape) Close(); };
            MouseDoubleClick += delegate(object s, MouseEventArgs e) { Close(); };
        }


        // WS_EX_COMPOSITED：让整窗自下而上一次性合成，缩放过程中不会露出上一帧的条带
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x02000000;
                return cp;
            }
        }

        private void SaveAs()
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "PNG 图片 (*.png)|*.png";
            sfd.FileName = "截图_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png";
            if (sfd.ShowDialog(this) != DialogResult.OK) return;
            try { img.Save(sfd.FileName, System.Drawing.Imaging.ImageFormat.Png); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "保存失败"); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            // 先按边框色擦满（局部失效时也不会留下上一尺寸的画面）
            using (SolidBrush bg = new SolidBrush(Color.FromArgb(28, 30, 36)))
                g.FillRectangle(bg, ClientRectangle);
            // 拖动中用快速插值（大图每帧高清重采样会卡顿），松手后再出高清
            g.InterpolationMode = smoothMode ? InterpolationMode.Bilinear : InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = smoothMode ? PixelOffsetMode.HighSpeed : PixelOffsetMode.HighQuality;
            Rectangle r = new Rectangle(1, 1, Math.Max(1, ClientSize.Width - 2), Math.Max(1, ClientSize.Height - 2));
            g.DrawImage(img, r);
            using (Pen p = new Pen(Color.FromArgb(170, Theme.Accent)))
                g.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
            // 8 个缩放手柄（鼠标靠近时才显示，避免日常注视时干扰）
            if (showHandles)
            {
                Point[] hs = ShotGeom.HandlePoints(new Rectangle(0, 0, Width, Height));
                int s = Theme.S(9);
                using (SolidBrush fill = new SolidBrush(Color.White))
                using (Pen edge = new Pen(Theme.Accent, Math.Max(1, Theme.S(2))))
                {
                    for (int i = 0; i < hs.Length; i++)
                    {
                        Rectangle box = new Rectangle(
                            Math.Min(Width - s - 1, Math.Max(1, hs[i].X - s / 2)),
                            Math.Min(Height - s - 1, Math.Max(1, hs[i].Y - s / 2)), s, s);
                        g.FillRectangle(fill, box);
                        g.DrawRectangle(edge, box);
                    }
                }
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                int h = ShotGeom.HitHandle(new Rectangle(0, 0, Width, Height), e.Location, Theme.S(10));
                if (h != ShotGeom.HandleNone)
                {
                    resizing = true;
                    resizeHandle = h;
                    rectAtStart = new Rectangle(Left, Top, Width, Height);   // 屏幕坐标
                    aspect = ShotGeom.AspectOf(rectAtStart);
                    mouseDownScreen = PointToScreen(e.Location);             // 按下时的鼠标屏幕坐标（增量基准）
                }
                else { dragging = true; offset = e.Location; }
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (dragging)
            {
                Point sp = PointToScreen(e.Location);
                Location = new Point(sp.X - offset.X, sp.Y - offset.Y);
            }
            else if (resizing)
            {
                if (applyingBounds) return;              // 重入直接忽略
                // 等比缩放：锚定"按下瞬间的矩形 + 鼠标按下时的屏幕坐标"，全程只用鼠标位移增量，
                // 不读回窗口当前位置 —— 从根上避免反馈环
                Point screenPt = PointToScreen(e.Location);
                // 手柄的起始屏幕位置 + 鼠标位移增量：起手不跳、全程只依赖按下瞬间的状态
                Point hs = ShotGeom.HandlePoints(rectAtStart)[resizeHandle];
                int mx = hs.X + (screenPt.X - mouseDownScreen.X);
                int my = hs.Y + (screenPt.Y - mouseDownScreen.Y);
                Rectangle r = ShotGeom.ResizeProportional(rectAtStart, resizeHandle, new Point(mx, my), Theme.S(60), aspect);
                r = ShotGeom.ClampToScreen(r, Screen.FromPoint(mouseDownScreen).WorkingArea);
                if (r != Bounds)                          // 相等就不设：避免 move→paint→move 的抖动回路
                {
                    applyingBounds = true;
                    smoothMode = true;
                    Bounds = r;
                    applyingBounds = false;
                    Invalidate();
                }
                zoom = (float)r.Width / Math.Max(1, baseSize.Width);
            }
            else
            {
                int h = ShotGeom.HitHandle(new Rectangle(0, 0, Width, Height), e.Location, Theme.S(10));
                Cursor = h == ShotGeom.HandleNone ? Cursors.SizeAll : ShotGeom.CursorForHandle(h);
                bool wantHandles = h != ShotGeom.HandleNone || (e.X > Width - Theme.S(24) && e.Y > Height - Theme.S(24));
                if (wantHandles != showHandles) { showHandles = wantHandles; Invalidate(); }
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            dragging = false;
            resizing = false;
            resizeHandle = ShotGeom.HandleNone;
            if (smoothMode) { smoothMode = false; Invalidate(); }   // 松手后重绘高清
            base.OnMouseUp(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            if (showHandles) { showHandles = false; Invalidate(); }
            base.OnMouseLeave(e);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Invalidate();          // 尺寸一变就整块重绘
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if ((ModifierKeys & Keys.Control) == Keys.Control)
            {
                double o = Opacity + (e.Delta > 0 ? 0.05 : -0.05);
                Opacity = Math.Max(0.2, Math.Min(1.0, o));
                return;
            }
            float z = zoom * (e.Delta > 0 ? 1.1f : 0.9f);
            z = Math.Max(0.2f, Math.Min(4f, z));
            zoom = z;
            Size = new Size((int)(baseSize.Width * zoom) + 2, (int)(baseSize.Height * zoom) + 2);
            Rectangle nr = ShotGeom.ClampToScreen(Bounds, Screen.FromPoint(Location).WorkingArea);
            if (nr != Bounds) Location = nr.Location;
            base.OnMouseWheel(e);
        }
    }
}