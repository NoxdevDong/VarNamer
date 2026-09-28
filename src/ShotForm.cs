using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace VarNamer
{
    // 截图浮层：全屏选区（可实时预览 / 可调整 / 可重选）+ 标注工具栏
    public class ShotForm : Form
    {
        [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(POINT p);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr h, out RECT r);
        [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr h, uint flags);
        [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }

        private AppState state;
        private Bitmap screen, pix;
        private Rectangle vs;
        private Rectangle sel, selAtStart;
        private bool hasSel, draggingSel, draggingMove, drawing;
        private int resizeHandle = ShotGeom.HandleNone;
        private Point handleStart, dragDownPoint;   // 手柄起始位置 / 按下点（用于位移增量）
        private Point startPt, lastPt, moveFrom;
        private ShotTool tool = ShotTool.None;          // 默认“选择/调整”态，不画东西
        private Color color = ShotDraw.Palette[0];
        private int penWidth = 3;
        private List<ShotShape> shapes = new List<ShotShape>();
        private ShotShape current;
        private int selectedShape = -1;       // 选中的标注（可拖动 / 删除）
        private bool draggingShape;
        private Point dragShapeFrom;
        private ShotToolbar bar;
        private TextBox textInput;
        private string hint = "";

        public ShotForm(AppState st)
        {
            state = st;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            KeyPreview = true;
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Cursor = Cursors.Cross;
        }

        public bool Start()
        {
            vs = SystemInformation.VirtualScreen;
            string err;
            screen = ShotCapture.Grab(out err);
            if (screen == null)
            {
                MessageBox.Show("截屏失败：" + err + "\r\n\r\n（可能被安全策略拦截，或当前会话不支持屏幕捕获）",
                    "VarNamer 截图", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            Bounds = vs;
            Show();
            Activate();
            return true;
        }

        // ---------------- 绘制 ----------------
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            if (screen == null) return;
            g.DrawImageUnscaled(screen, 0, 0);

            if (!hasSel && !draggingSel)
            {
                // 还没开始选：整体压暗 + 十字线 + 提示
                using (SolidBrush dim = new SolidBrush(Color.FromArgb(120, 0, 0, 0)))
                    g.FillRectangle(dim, ClientRectangle);
                DrawCrosshair(g);
                DrawHint(g, string.IsNullOrEmpty(hint) ? "拖动选择区域　·　单击选窗口　·　Esc 取消" : hint);
                return;
            }

            // 有选区或正在拖选：选区外压暗（实时跟随）
            Rectangle r = sel;
            using (SolidBrush dim = new SolidBrush(Color.FromArgb(120, 0, 0, 0)))
            {
                g.FillRectangle(dim, new Rectangle(0, 0, ClientSize.Width, Math.Max(0, r.Top)));
                g.FillRectangle(dim, new Rectangle(0, r.Bottom, ClientSize.Width, Math.Max(0, ClientSize.Height - r.Bottom)));
                g.FillRectangle(dim, new Rectangle(0, Math.Max(0, r.Top), Math.Max(0, r.Left), Math.Max(0, r.Height)));
                g.FillRectangle(dim, new Rectangle(r.Right, Math.Max(0, r.Top), Math.Max(0, ClientSize.Width - r.Right), Math.Max(0, r.Height)));
            }

            // 标注
            if (pix == null) pix = ShotDraw.Pixellate(screen, 12);
            List<ShotShape> all = new List<ShotShape>(shapes);
            if (current != null && current.Points.Count > 0) all.Add(current);
            if (all.Count > 0) ShotDraw.DrawShapes(g, all, pix, Point.Empty);

            // 边框 + 手柄 + 尺寸
            using (Pen p = new Pen(Theme.Accent, Math.Max(1, Theme.S(2))))
                g.DrawRectangle(p, r.X, r.Y, Math.Max(1, r.Width - 1), Math.Max(1, r.Height - 1));
            if (hasSel) DrawHandles(g);
            if (selectedShape >= 0 && selectedShape < shapes.Count)
            {
                Rectangle sb2 = ShotDraw.ShapeBounds(shapes[selectedShape]);
                using (Pen sp = new Pen(Theme.Accent, Math.Max(1, Theme.S(1))))
                {
                    sp.DashStyle = DashStyle.Dash;
                    g.DrawRectangle(sp, sb2);
                }
            }
            DrawSizeTag(g);
        }

        private void DrawHandles(Graphics g)
        {
            Point[] h = ShotGeom.HandlePoints(sel);
            int s = Theme.S(8);
            using (SolidBrush fill = new SolidBrush(Color.White))
            using (Pen edge = new Pen(Theme.Accent, Math.Max(1, Theme.S(2))))
            {
                for (int i = 0; i < h.Length; i++)
                {
                    Rectangle box = new Rectangle(h[i].X - s / 2, h[i].Y - s / 2, s, s);
                    g.FillRectangle(fill, box);
                    g.DrawRectangle(edge, box);
                }
            }
        }

        private void DrawCrosshair(Graphics g)
        {
            Point m = PointToClient(Cursor.Position);
            using (Pen p = new Pen(Color.FromArgb(140, 255, 255, 255)))
            {
                p.DashStyle = DashStyle.Dash;
                g.DrawLine(p, 0, m.Y, ClientSize.Width, m.Y);
                g.DrawLine(p, m.X, 0, m.X, ClientSize.Height);
            }
            using (SolidBrush b = new SolidBrush(Color.FromArgb(200, 20, 20, 20)))
                g.FillRectangle(b, new Rectangle(m.X + Theme.S(14), m.Y + Theme.S(14), Theme.S(90), Theme.S(22)));
            TextRenderer.DrawText(g, "x=" + (m.X + vs.Left) + " y=" + (m.Y + vs.Top), Theme.FontSmall,
                new Rectangle(m.X + Theme.S(18), m.Y + Theme.S(15), Theme.S(86), Theme.S(20)), Color.White);
        }

        private void DrawHint(Graphics g, string text)
        {
            Size sz = TextRenderer.MeasureText(text, Theme.FontBody);
            Rectangle r = new Rectangle((ClientSize.Width - sz.Width) / 2 - Theme.S(14), Theme.S(28),
                sz.Width + Theme.S(28), sz.Height + Theme.S(16));
            using (GraphicsPath p = Theme.RoundedRect(r, Theme.S(8)))
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(220, 20, 20, 20))) g.FillPath(b, p);
                using (Pen pen = new Pen(Color.FromArgb(90, 255, 255, 255))) g.DrawPath(pen, p);
            }
            TextRenderer.DrawText(g, text, Theme.FontBody, r, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }

        private void DrawSizeTag(Graphics g)
        {
            string t = sel.Width + " x " + sel.Height + (tool == ShotTool.None && hasSel ? "　·　拖动边角改大小 / 右键重选" : "");
            Size sz = TextRenderer.MeasureText(t, Theme.FontSmall);
            int x = sel.Left, y = sel.Top - sz.Height - Theme.S(10);
            if (y < 0) y = sel.Bottom + Theme.S(6);
            Rectangle r = new Rectangle(x, y, sz.Width + Theme.S(16), sz.Height + Theme.S(8));
            using (GraphicsPath p = Theme.RoundedRect(r, Theme.S(6)))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(220, 20, 20, 20))) g.FillPath(b, p);
            TextRenderer.DrawText(g, t, Theme.FontSmall, r, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        // ---------------- 鼠标 ----------------
        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                if (hasSel) { ResetSelection(); }        // 右键：取消当前选区，回到重新选择
                else Close();
                return;
            }
            if (e.Button != MouseButtons.Left) return;

            if (!hasSel)
            {
                draggingSel = true;
                startPt = e.Location;
                sel = new Rectangle(e.Location, Size.Empty);
                Invalidate();
                return;
            }

            int h = ShotGeom.HitHandle(sel, e.Location, Theme.S(7));
            if (h != ShotGeom.HandleNone)
            {
                resizeHandle = h;
                selAtStart = sel;
                handleStart = ShotGeom.HandlePoints(selAtStart)[h];   // 该手柄的起始位置
                dragDownPoint = e.Location;
                return;
            }
            if (sel.Contains(e.Location))
            {
                if (tool == ShotTool.None)
                {
                    // 先看有没有点中已有标注：点中就拖动标注，否则移动整个选区
                    int hitIdx = ShotDraw.HitTest(shapes, e.Location);
                    if (hitIdx >= 0)
                    {
                        selectedShape = hitIdx;
                        draggingShape = true;
                        dragShapeFrom = e.Location;
                        Cursor = Cursors.SizeAll;
                        Invalidate();
                        return;
                    }
                    selectedShape = -1;
                    draggingMove = true;
                    moveFrom = e.Location;
                    selAtStart = sel;
                    Cursor = Cursors.SizeAll;
                    return;
                }
                if (tool == ShotTool.Text) { ShowTextInput(e.Location); return; }
                drawing = true;
                startPt = lastPt = e.Location;
                current = new ShotShape();
                current.Tool = tool;
                current.Color = color;
                current.Width = penWidth;
                current.Points.Add(e.Location);
                Invalidate();
                return;
            }
            // 点在选区外：当作重新开始选择（微信行为）
            ResetSelection();
            draggingSel = true;
            startPt = e.Location;
            sel = new Rectangle(e.Location, Size.Empty);
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (draggingSel)
            {
                sel = Normalize(startPt, e.Location);
                sel.Intersect(new Rectangle(0, 0, ClientSize.Width, ClientSize.Height));
                Invalidate();                                   // 实时预览选区
                return;
            }
            if (resizeHandle != ShotGeom.HandleNone)
            {
                // 用位移增量：起手不跳（否则手柄会瞬间吸到鼠标位置）
                Point target = new Point(handleStart.X + (e.Location.X - dragDownPoint.X),
                                         handleStart.Y + (e.Location.Y - dragDownPoint.Y));
                Rectangle rr = ShotGeom.ResizeFromHandle(selAtStart, resizeHandle, target);
                sel = ShotGeom.ClampResize(rr, new Rectangle(0, 0, ClientSize.Width, ClientSize.Height), Theme.S(8));
                UpdateToolbarPos();
                Invalidate();
                return;
            }
            if (draggingShape && selectedShape >= 0 && selectedShape < shapes.Count)
            {
                ShotDraw.Translate(shapes[selectedShape], e.Location.X - dragShapeFrom.X, e.Location.Y - dragShapeFrom.Y);
                dragShapeFrom = e.Location;
                Invalidate();
                return;
            }
            if (draggingMove)
            {
                sel = ShotGeom.ClampInside(ShotGeom.MoveTo(selAtStart, moveFrom, e.Location),
                    new Rectangle(0, 0, ClientSize.Width, ClientSize.Height));
                UpdateToolbarPos();
                Invalidate();
                return;
            }
            if (drawing && current != null)
            {
                if (tool == ShotTool.Pen || tool == ShotTool.Highlight) current.Points.Add(e.Location);
                else
                {
                    current.Points.Clear();
                    current.Points.Add(startPt);
                    current.Points.Add(e.Location);
                }
                lastPt = e.Location;
                Invalidate();
                return;
            }
            UpdateCursor(e.Location);
            if (!hasSel) Invalidate();
        }

        private void UpdateCursor(Point p)
        {
            if (!hasSel) { Cursor = Cursors.Cross; return; }
            int h = ShotGeom.HitHandle(sel, p, Theme.S(7));
            if (h != ShotGeom.HandleNone) { Cursor = ShotGeom.CursorForHandle(h); return; }
            if (sel.Contains(p)) Cursor = (tool == ShotTool.None) ? Cursors.SizeAll : Cursors.Cross;
            else Cursor = Cursors.Cross;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (draggingSel)
            {
                draggingSel = false;
                if (sel.Width < Theme.S(6) && sel.Height < Theme.S(6))
                {
                    Rectangle w = WindowRectAt(Cursor.Position);
                    if (w.Width > 4 && w.Height > 4) sel = ToClientRect(w);
                }
                if (sel.Width >= Theme.S(6) && sel.Height >= Theme.S(6))
                {
                    hasSel = true;
                    tool = ShotTool.None;               // 选完先进入“可调整”状态
                    ShowToolbar();
                    if (bar != null) bar.SyncState();
                }
                Invalidate();
                return;
            }
            if (draggingShape) { draggingShape = false; return; }
            if (resizeHandle != ShotGeom.HandleNone) { resizeHandle = ShotGeom.HandleNone; return; }
            if (draggingMove) { draggingMove = false; Cursor = Cursors.Cross; return; }
            if (drawing)
            {
                drawing = false;
                if (current != null && current.Points.Count >= 2) shapes.Add(current);
                current = null;
                Invalidate();
            }
        }

        // 清空选区，回到“重新选择”状态
        internal void ResetSelection()
        {
            hasSel = false;
            sel = Rectangle.Empty;
            shapes.Clear();
            current = null;
            selectedShape = -1;
            if (textInput != null) textInput.Visible = false;
            if (bar != null) bar.Visible = false;
            tool = ShotTool.None;
            Cursor = Cursors.Cross;
            Invalidate();
        }

        private static Rectangle Normalize(Point a, Point b)
        {
            return new Rectangle(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
        }

        private Rectangle ToClientRect(Rectangle scr)
        {
            return new Rectangle(scr.X - vs.Left, scr.Y - vs.Top, scr.Width, scr.Height);
        }

        private Rectangle WindowRectAt(Point screenPt)
        {
            POINT p; p.X = screenPt.X; p.Y = screenPt.Y;
            IntPtr h = WindowFromPoint(p);
            h = GetAncestor(h, 2);
            RECT r;
            if (h == IntPtr.Zero || !GetWindowRect(h, out r)) return Rectangle.Empty;
            return new Rectangle(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
        }

        // ---------------- 工具栏 ----------------
        private void ShowToolbar()
        {
            if (bar == null)
            {
                bar = new ShotToolbar(this);
                Controls.Add(bar);
            }
            bar.Visible = true;
            UpdateToolbarPos();
            bar.BringToFront();
        }

        internal void UpdateToolbarPos()
        {
            if (bar == null || !bar.Visible) return;
            int w = bar.Width, h = bar.Height;
            int x = sel.Left;
            int y = sel.Bottom + Theme.S(8);
            if (y + h > ClientSize.Height) y = sel.Top - h - Theme.S(8);
            if (y < 0) y = Math.Min(Math.Max(Theme.S(4), sel.Bottom - h - Theme.S(4)), ClientSize.Height - h);
            if (x + w > ClientSize.Width) x = ClientSize.Width - w - Theme.S(4);
            if (x < 0) x = Theme.S(4);
            if (y < 0) y = Theme.S(4);
            bar.Location = new Point(x, y);
        }

        internal void SetTool(ShotTool t) { tool = t; Cursor = t == ShotTool.None ? Cursors.SizeAll : Cursors.Cross; Invalidate(); }
        internal void SetColor(Color c) { color = c; Invalidate(); }
        internal void SetWidth(int w) { penWidth = w; Invalidate(); }
        internal ShotTool CurrentTool() { return tool; }
        internal Color CurrentColor() { return color; }
        internal int CurrentWidth() { return penWidth; }
        internal AppState State() { return state; }
        internal bool HasSelection { get { return hasSel; } }

        internal void Undo()
        {
            if (shapes.Count > 0) { shapes.RemoveAt(shapes.Count - 1); Invalidate(); }
        }

        internal Bitmap Result()
        {
            if (!hasSel) return null;
            Rectangle region = sel;
            region.Offset(vs.Left, vs.Top);
            List<ShotShape> abs = new List<ShotShape>();
            for (int i = 0; i < shapes.Count; i++)
            {
                ShotShape s = new ShotShape();
                s.Tool = shapes[i].Tool; s.Color = shapes[i].Color; s.Width = shapes[i].Width;
                s.Text = shapes[i].Text; s.FontSize = shapes[i].FontSize;
                for (int k = 0; k < shapes[i].Points.Count; k++)
                    s.Points.Add(new Point(shapes[i].Points[k].X + vs.Left, shapes[i].Points[k].Y + vs.Top));
                abs.Add(s);
            }
            return ShotDraw.Compose(screen, region, abs);
        }

        internal void DoCopy()
        {
            Bitmap b = Result();
            if (b == null) return;
            try { Clipboard.SetImage(b); } catch (Exception) { }
            b.Dispose();
            Close();
        }

        internal void DoSave()
        {
            Bitmap b = Result();
            if (b == null) return;
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "PNG 图片 (*.png)|*.png|JPEG 图片 (*.jpg)|*.jpg";
            string dir = state.Cfg.ShotDir;
            try { if (!string.IsNullOrEmpty(dir) && System.IO.Directory.Exists(dir)) sfd.InitialDirectory = dir; }
            catch (Exception) { }
            sfd.FileName = "截图_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png";
            if (sfd.ShowDialog(this) == DialogResult.OK)
            {
                ImageFormat fmt = sfd.FileName.ToLower().EndsWith(".jpg") ? ImageFormat.Jpeg : ImageFormat.Png;
                try
                {
                    b.Save(sfd.FileName, fmt);
                    state.Cfg.ShotDir = System.IO.Path.GetDirectoryName(sfd.FileName);
                    state.Cfg.Save();
                    Clipboard.SetText(sfd.FileName);
                }
                catch (Exception ex) { MessageBox.Show(ex.Message, "保存失败", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            }
            b.Dispose();
            Close();
        }

        internal void DoPin()
        {
            Bitmap b = Result();
            if (b == null) return;
            PinForm pin = new PinForm(b);
            pin.Show();
            Close();
        }

        // 细/中/粗 → 文字字号（差异明显，避免"看不出效果"）
        internal static int TextFontSize(int penWidth)
        {
            if (penWidth <= 2) return 18;      // 细
            if (penWidth <= 4) return 26;      // 中
            return 36;                         // 粗
        }

        internal void ClearAnnotations() { shapes.Clear(); current = null; Invalidate(); }

        // ---------- 自测 seam（不参与正常交互）----------
        internal bool FormHandlesKeys { get { return !(textInput != null && textInput.Visible); } }
        internal int ShapeCount { get { return shapes.Count; } }
        internal void SetSelectionForTest(Rectangle r) { sel = r; hasSel = true; Invalidate(); }
        internal void BeginTextForTest(Point at) { ShowTextInput(at); }
        internal void TypeTextForTest(string s) { if (textInput != null) textInput.Text = s; }
        internal int CommitTextForTest() { CommitText(); return shapes.Count; }
        internal void SyncToolbar() { if (bar != null) { bar.Visible = hasSel; bar.SyncState(); UpdateToolbarPos(); } }

        // ---------------- 文字标注 ----------------
        private void ShowTextInput(Point at)
        {
            if (textInput == null)
            {
                textInput = new TextBox();
                textInput.Font = new Font("Microsoft YaHei UI", 12f);
                textInput.BorderStyle = BorderStyle.FixedSingle;
                textInput.KeyDown += delegate(object s, KeyEventArgs e)
                {
                    if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; CommitText(); }
                    else if (e.KeyCode == Keys.Escape) { e.SuppressKeyPress = true; CancelText(); }
                };
                textInput.LostFocus += delegate(object s, EventArgs e) { CommitText(); };   // 点到别处也落字
                textInput.TextChanged += delegate(object s, EventArgs e) { textInput.Invalidate(); };
                Controls.Add(textInput);
            }
            textInput.Text = "";
            textInput.Width = Theme.S(220);
            textInput.Height = Theme.S(30);
            textInput.Location = at;
            textInput.Visible = true;
            textInput.BringToFront();
            textInput.Focus();
        }

        private void CancelText()
        {
            if (textInput != null) { textInput.Text = ""; textInput.Visible = false; }
            Invalidate();
        }

        private bool committing;
        private void CommitText()
        {
            if (committing || textInput == null || !textInput.Visible) return;
            committing = true;
            string t = textInput.Text.Trim();
            if (t.Length > 0)
            {
                ShotShape s = new ShotShape();
                s.Tool = ShotTool.Text;
                s.Color = color;
                s.FontSize = TextFontSize(penWidth);
                s.Text = t;
                s.Points.Add(new Point(textInput.Left, textInput.Top));
                shapes.Add(s);
            }
            textInput.Visible = false;
            committing = false;
            if (t.Length > 0)
            {
                selectedShape = shapes.Count - 1;   // 落字后自动选中，并切回“选择”态便于拖动
                tool = ShotTool.None;
                if (bar != null) bar.SyncState();
            }
            Invalidate();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            // 正在输入文字时：键盘交给输入框；但若回车/取消落到窗体（输入框没拿到焦点），
            // 窗体也必须提交/取消——否则会出现“必须点一下别的按钮文字才落下来”
            if (!FormHandlesKeys)
            {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; CommitText(); }
                else if (e.KeyCode == Keys.Escape) { e.SuppressKeyPress = true; CancelText(); }
                return;
            }
            if (e.KeyCode == Keys.Escape)
            {
                if (selectedShape >= 0) { selectedShape = -1; Invalidate(); return; }   // 先取消选中
                if (hasSel) { ResetSelection(); return; }                                // 再取消选区
                Close();                                                                 // 最后退出
                return;
            }
            if (e.KeyCode == Keys.Delete && selectedShape >= 0 && selectedShape < shapes.Count)
            {
                shapes.RemoveAt(selectedShape);
                selectedShape = -1;
                Invalidate();
                return;
            }
            if (e.KeyCode == Keys.Enter && hasSel) { DoCopy(); return; }
            if (e.Control && e.KeyCode == Keys.Z) { Undo(); return; }
            if (e.Control && e.KeyCode == Keys.A)
            {
                sel = new Rectangle(0, 0, ClientSize.Width, ClientSize.Height);
                hasSel = true;
                tool = ShotTool.None;
                ShowToolbar();
                if (bar != null) bar.SyncState();
                Invalidate();
                return;
            }
            if (e.Control && e.KeyCode == Keys.S) { DoSave(); return; }
            if (e.Control && e.KeyCode == Keys.C) { DoCopy(); return; }
            if (e.Control && e.KeyCode == Keys.D) { DoPin(); return; }
            base.OnKeyDown(e);
        }
    }
}