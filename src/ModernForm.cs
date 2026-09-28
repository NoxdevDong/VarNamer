using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace VarNamer
{
    // 无边框 + 圆角 + 自定义标题栏，拖动/缩放由窗口消息处理，排版稳定
    public class ModernForm : Form
    {
        public Panel Content;
        protected Panel TitleBar;
        protected Label LblTitle;
        protected Label LblSub;
        protected FlatButton BtnMin;
        protected FlatButton BtnClose;
        protected FlowLayoutPanel TitleButtons;
        protected Label Mark;

        public bool Resizable = true;
        public int CornerRadius = Theme.S(12);
        private bool dragging;
        private Point dragOriginScreen;   // 按下时的鼠标屏幕坐标
        private Point dragOriginForm;     // 按下时的窗口位置


        private const int WM_NCHITTEST = 0x0084;
        private const int HTCLIENT = 1, HTLEFT = 10, HTRIGHT = 11, HTTOP = 12, HTTOPLEFT = 13,
                          HTTOPRIGHT = 14, HTBOTTOM = 15, HTBOTTOMLEFT = 16, HTBOTTOMRIGHT = 17;
        private int resizeBorder = Theme.S(6);

        public ModernForm()
        {
            // 显式设置窗口图标：否则任务栏按钮 / 任务栏预览缩略图会用系统默认图标，
            // 与托盘图标（我们显式加载的 app.ico）不一致
            try { Icon = TrayApp.LoadAppIcon(32); } catch (Exception) { }
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            BackColor = Theme.Bg;
            Font = Theme.FontBody;
            AutoScaleMode = AutoScaleMode.None;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;

            // 先加 Fill 内容区, 再加 Edge 栏, 保证停靠顺序确定
            Content = new Panel();
            Content.Dock = DockStyle.Fill;
            Content.BackColor = Theme.Bg;
            Controls.Add(Content);

            TitleBar = new Panel();
            TitleBar.Dock = DockStyle.Top;
            TitleBar.Height = Theme.S(46);
            TitleBar.BackColor = Theme.Bg;
            TitleBar.MouseCaptureChanged += OnTitleCaptureChanged;
            TitleBar.MouseDown += OnTitleDown;
            TitleBar.MouseMove += OnTitleMove;
            TitleBar.MouseUp += OnTitleUp;
            Controls.Add(TitleBar);

            Mark = new Label();
            Mark.AutoSize = false;
            Mark.Size = new Size(Theme.S(26), Theme.S(26));
            Mark.Location = new Point(Theme.S(16), Theme.S(10));
            Mark.Paint += OnMarkPaint;
            Mark.MouseDown += OnTitleDown;
            Mark.MouseMove += OnTitleMove;
            Mark.MouseUp += OnTitleUp;
            TitleBar.Controls.Add(Mark);

            LblTitle = new Label();
            LblTitle.Text = "VarNamer";
            LblTitle.Font = Theme.FontTitle;
            LblTitle.ForeColor = Theme.TextPrimary;
            LblTitle.AutoSize = true;
            LblTitle.Location = new Point(Theme.S(52), Theme.S(11));
            LblTitle.MouseDown += OnTitleDown;
            LblTitle.MouseMove += OnTitleMove;
            LblTitle.MouseUp += OnTitleUp;
            TitleBar.Controls.Add(LblTitle);

            LblSub = new Label();
            LblSub.Text = "v" + AppVersion.Value;
            LblSub.Font = Theme.FontSmall;
            LblSub.ForeColor = Theme.TextMuted;
            LblSub.AutoSize = true;
            LblSub.Location = new Point(Theme.S(54), Theme.S(16));
            LblSub.MouseDown += OnTitleDown;
            LblSub.MouseMove += OnTitleMove;
            LblSub.MouseUp += OnTitleUp;
            TitleBar.Controls.Add(LblSub);

            // 标题与副标题并排（先量宽再定位，避免竖向重叠）
            Size ts = TextRenderer.MeasureText(LblTitle.Text, LblTitle.Font);
            LblSub.Location = new Point(Theme.S(52) + ts.Width + Theme.S(10), Theme.S(16));

            TitleButtons = new FlowLayoutPanel();
            TitleButtons.Dock = DockStyle.Right;
            TitleButtons.Width = Theme.S(88);
            TitleButtons.FlowDirection = FlowDirection.RightToLeft;
            TitleButtons.WrapContents = false;
            TitleButtons.BackColor = Theme.Bg;
            TitleButtons.Padding = new Padding(0, Theme.S(9), Theme.S(12), 0);
            TitleBar.Controls.Add(TitleButtons);

            BtnClose = MakeChrome("✕", Theme.Danger, Color.FromArgb(120, 40, 40));
            BtnClose.Click += delegate(object s, EventArgs e) { OnChromeClose(); };
            TitleButtons.Controls.Add(BtnClose);

            BtnMin = MakeChrome("─", Theme.BtnHover, Theme.BtnDown);
            BtnMin.Click += delegate(object s, EventArgs e) { OnChromeMinimize(); };
            TitleButtons.Controls.Add(BtnMin);
        }

        protected virtual void OnChromeClose() { Close(); }
        protected virtual void OnChromeMinimize() { WindowState = FormWindowState.Minimized; }

        private FlatButton MakeChrome(string glyph, Color hover, Color press)
        {
            FlatButton b = new FlatButton();
            b.Text = glyph;
            b.Size = new Size(Theme.S(34), Theme.S(28));
            b.Font = new Font("Segoe UI", 9f);
            b.Radius = Theme.S(7);
            b.ForeColor = Theme.TextSecondary;
            b.Margin = new Padding(Theme.S(4), 0, 0, 0);
            b.HoverOverride = hover;
            b.PressOverride = press;
            return b;
        }

        private void OnMarkPaint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Theme.S(26), Theme.S(26));
            using (GraphicsPath p = Theme.RoundedRect(r, Theme.S(8)))
            {
                using (LinearGradientBrush br = new LinearGradientBrush(r,
                    Color.FromArgb(88, 140, 255), Color.FromArgb(46, 86, 184), 90f))
                    g.FillPath(br, p);
            }
            TextRenderer.DrawText(g, "V", new Font("Segoe UI", 12f, FontStyle.Bold), r, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        internal void OnTitleDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            dragging = true;
            // 关键修复：全程用屏幕坐标，且与具体是哪个子控件无关（标题栏、标题文字、副标题、Logo 都一样）
            dragOriginScreen = Control.MousePosition;
            dragOriginForm = Location;
            // 把鼠标捕获统一交给标题栏本体：后续 Move/Up 只发给它，避免“光标从一个 Label 移到另一个 Label”时坐标系混用导致跳动
            try { TitleBar.Capture = true; } catch (Exception) { }
        }

        internal void OnTitleMove(object sender, MouseEventArgs e)
        {
            if (!dragging || WindowState == FormWindowState.Maximized) return;
            Point cur = Control.MousePosition;                       // 当前鼠标屏幕坐标（不依赖 sender/e.X/e.Y）
            Point target = new Point(dragOriginForm.X + (cur.X - dragOriginScreen.X),
                                     dragOriginForm.Y + (cur.Y - dragOriginScreen.Y));
            if (target != Location) Location = target;               // 位置未变则不重设，避免无谓重绘抖动
        }

        internal void OnTitleUp(object sender, MouseEventArgs e)
        {
            if (!dragging) return;
            dragging = false;
            try { TitleBar.Capture = false; } catch (Exception) { }
        }

        // 捕获被系统抢走（例如弹出其它窗口）时结束拖动，避免“粘鼠标”
        private void OnTitleCaptureChanged(object sender, EventArgs e)
        {
            if (!TitleBar.Capture) dragging = false;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateRegion();
        }

        // 子类可关掉淡入（例如悬浮窗自己管理透明度）
        protected virtual bool FadeOnShow { get { return true; } }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (FadeOnShow)
            {
                Opacity = 0.0;
                Tween.FadeIn(this, 1.0, Theme.DurMed);
            }
            UpdateRegion();
        }

        private void UpdateRegion()
        {
            if (Width <= 0 || Height <= 0) return;
            using (GraphicsPath p = Theme.RoundedRect(new Rectangle(0, 0, Width, Height), CornerRadius))
            {
                Region old = Region;
                Region = new Region(p);
                if (old != null) old.Dispose();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath p = Theme.RoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), CornerRadius))
            using (Pen pen = new Pen(Theme.Border))
                e.Graphics.DrawPath(pen, p);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= 0x00020000; // CS_DROPSHADOW
                return cp;
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_NCHITTEST && Resizable && WindowState == FormWindowState.Normal)
            {
                base.WndProc(ref m);
                if (m.Result.ToInt32() == HTCLIENT)
                {
                    int x = unchecked((short)(long)m.LParam);
                    int y = unchecked((short)((long)m.LParam >> 16));
                    Point p = PointToClient(new Point(x, y));
                    bool left = p.X <= resizeBorder, right = p.X >= ClientSize.Width - resizeBorder;
                    bool top = p.Y <= resizeBorder, bottom = p.Y >= ClientSize.Height - resizeBorder;
                    if (top && left) m.Result = (IntPtr)HTTOPLEFT;
                    else if (top && right) m.Result = (IntPtr)HTTOPRIGHT;
                    else if (bottom && left) m.Result = (IntPtr)HTBOTTOMLEFT;
                    else if (bottom && right) m.Result = (IntPtr)HTBOTTOMRIGHT;
                    else if (left) m.Result = (IntPtr)HTLEFT;
                    else if (right) m.Result = (IntPtr)HTRIGHT;
                    else if (top) m.Result = (IntPtr)HTTOP;
                    else if (bottom) m.Result = (IntPtr)HTBOTTOM;
                }
                return;
            }
            base.WndProc(ref m);
        }
    }
}
