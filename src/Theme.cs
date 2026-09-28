using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace VarNamer
{
    // 统一视觉语言：深色 + 单一强调色 + 大圆角，避免 WinForms 默认控件观感
    public static class Theme
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern uint GetDpiForSystem();

        public static readonly float DpiScale = ResolveScale();

        private static float ResolveScale()
        {
            try
            {
                uint dpi = GetDpiForSystem();
                if (dpi >= 72 && dpi <= 480) return dpi / 96f;
            }
            catch (Exception) { }
            return 1f;
        }

        // 设计稿按 96 DPI 写，运行时按实际系统 DPI 换算
        public static int S(int px)
        {
            return (int)Math.Round(px * DpiScale);
        }

        // ---- 调色板（可由设置切换 深色/浅色）----
        public static bool IsDark = true;
        public static Color Bg;
        public static Color Surface;
        public static Color Elevated;
        public static Color Border;
        public static Color BorderSoft;
        public static Color TextPrimary;
        public static Color TextSecondary;
        public static Color TextMuted;
        public static Color Accent;
        public static Color AccentHover;
        public static Color AccentDown;
        public static Color AccentSoft;
        public static Color BtnBg;
        public static Color BtnHover;
        public static Color BtnDown;
        public static Color Danger;
        public static Color Warn;
        public static Color RowHover;      // 列表行悬停底色
        public static Color RowStripe;     // 列表隔行底色（极淡）
        public static Color CardLine;      // 卡片内细分隔线
        public static Color Ok;

        static Theme() { ApplyPalette(true); }

        public static void ApplyPalette(bool dark)
        {
            IsDark = dark;
            if (dark)
            {
                Bg            = Color.FromArgb(13, 15, 19);
                Surface       = Color.FromArgb(22, 25, 32);
                Elevated      = Color.FromArgb(31, 35, 45);
                Border        = Color.FromArgb(44, 50, 62);
                BorderSoft    = Color.FromArgb(33, 38, 48);
                TextPrimary   = Color.FromArgb(232, 236, 243);
                TextSecondary = Color.FromArgb(154, 164, 178);
                TextMuted     = Color.FromArgb(106, 116, 131);
                Accent        = Color.FromArgb(88, 140, 255);
                AccentHover   = Color.FromArgb(110, 156, 255);
                AccentDown    = Color.FromArgb(70, 116, 220);
                AccentSoft    = Color.FromArgb(32, 44, 68);
                BtnBg         = Color.FromArgb(34, 39, 49);
                BtnHover      = Color.FromArgb(44, 51, 64);
                BtnDown       = Color.FromArgb(28, 33, 42);
                Danger        = Color.FromArgb(240, 96, 96);
                Warn          = Color.FromArgb(240, 170, 70);
                Ok            = Color.FromArgb(80, 200, 140);
                RowHover      = Color.Empty;    // 统一在 if/else 之后按状态层派生
                RowStripe     = Color.Empty;
                CardLine      = Color.Empty;
            }
            else
            {
                Bg            = Color.FromArgb(243, 245, 248);
                Surface       = Color.FromArgb(255, 255, 255);
                Elevated      = Color.FromArgb(246, 248, 251);
                Border        = Color.FromArgb(214, 219, 226);
                BorderSoft    = Color.FromArgb(228, 232, 238);
                TextPrimary   = Color.FromArgb(24, 28, 34);
                TextSecondary = Color.FromArgb(88, 97, 110);
                TextMuted     = Color.FromArgb(138, 146, 158);
                Accent        = Color.FromArgb(47, 107, 255);
                AccentHover   = Color.FromArgb(74, 128, 255);
                AccentDown    = Color.FromArgb(37, 87, 214);
                AccentSoft    = Color.FromArgb(232, 239, 255);
                BtnBg         = Color.FromArgb(255, 255, 255);
                BtnHover      = Color.FromArgb(240, 243, 247);
                BtnDown       = Color.FromArgb(230, 234, 240);
                Danger        = Color.FromArgb(229, 72, 77);
                Warn          = Color.FromArgb(183, 121, 31);
                Ok            = Color.FromArgb(26, 156, 98);
                RowHover      = Color.Empty;    // 统一在 if/else 之后按状态层派生
                RowStripe     = Color.Empty;
                CardLine      = Color.Empty;
            }
            // 统一按“Material 3 状态层”推导，保证深浅两套主题观感一致
            RowHover  = Anim.Lerp(Surface, Accent, StateHover);
            RowStripe = Anim.Lerp(Surface, Accent, 0.03);
            CardLine  = Anim.Lerp(Surface, Border, 0.55);
        }

        public static readonly Font FontTitle = new Font("Microsoft YaHei UI", 11.5f, FontStyle.Bold);
        public static readonly Font FontCardTitle = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold);
        public static readonly Font FontBodyBold = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold);
        public static readonly Font FontSmallBold = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Bold);
        // Fluent 字号阶梯：200 = 12px、300 = 14px、400 = 16px semibold（fonts.ts 官方值）
        public static readonly Font FontCaption = new Font("Microsoft YaHei UI", 9f);
        public static readonly Font FontBodyLg = new Font("Microsoft YaHei UI", 10.5f);
        public static readonly Font FontSubtitle = new Font("Microsoft YaHei UI", 12f, FontStyle.Bold);
        public static readonly Font FontBody  = new Font("Microsoft YaHei UI", 9.5f);
        public static readonly Font FontSmall = new Font("Microsoft YaHei UI", 8.5f);
        public static readonly Font FontInput = new Font("Microsoft YaHei UI", 13f);
        public static readonly Font FontMono  = new Font("Consolas", 10.5f);
        public static readonly Font FontMonoSm= new Font("Consolas", 9.5f);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hWnd, string pszSubAppName, string pszSubIdList);

        // 暗色主题下让系统滚动条也走暗色（Win10 1809+ 支持），避免深色界面里出现一条亮灰滚动条
        public static bool ApplyScrollbarTheme(Control c)
        {
            try
            {
                if (!c.IsHandleCreated) return false;
                return SetWindowTheme(c.Handle, IsDark ? "DarkMode_Explorer" : "Explorer", null) == 0;   // S_OK
            }
            catch (Exception) { return false; }
        }

        public static void StyleGrid(DataGridView g, bool mono)
        {
            g.BackgroundColor = Surface;
            g.BorderStyle = BorderStyle.None;
            g.EnableHeadersVisualStyles = false;
            g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            g.ColumnHeadersDefaultCellStyle.BackColor = Surface;
            g.ColumnHeadersDefaultCellStyle.ForeColor = TextMuted;
            g.ColumnHeadersDefaultCellStyle.SelectionBackColor = Surface;
            g.ColumnHeadersDefaultCellStyle.SelectionForeColor = TextMuted;
            g.ColumnHeadersDefaultCellStyle.Font = FontSmall;
            g.ColumnHeadersDefaultCellStyle.Padding = new Padding(S(6), 0, 0, 0);
            g.ColumnHeadersHeight = S(28);
            g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            g.DefaultCellStyle.BackColor = Surface;
            g.DefaultCellStyle.ForeColor = TextPrimary;
            g.DefaultCellStyle.SelectionBackColor = RowHover;
            g.DefaultCellStyle.SelectionForeColor = TextPrimary;
            g.DefaultCellStyle.Font = mono ? FontMono : FontBody;
            g.DefaultCellStyle.Padding = new Padding(S(6), 0, 0, 0);
            g.AlternatingRowsDefaultCellStyle.BackColor = RowStripe;
            g.GridColor = BorderSoft;
            g.CellBorderStyle = DataGridViewCellBorderStyle.None;   // 只在行悬停/选中时给底色，减少“网格线”的廉价感
            g.RowHeadersVisible = false;
            g.AllowUserToAddRows = false;
            g.AllowUserToDeleteRows = false;
            g.AllowUserToResizeRows = false;
            g.AllowUserToResizeColumns = false;
            g.ReadOnly = true;
            g.MultiSelect = false;
            g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            g.RowTemplate.Height = S(34);
            g.ScrollBars = ScrollBars.Vertical;
            g.TabStop = false;
            if (g.IsHandleCreated) ApplyScrollbarTheme(g);
            g.HandleCreated += delegate(object s, EventArgs e) { ApplyScrollbarTheme((Control)s); };
        }

        public static DataGridViewTextBoxColumn TextColumn(string header, bool mono, Color fg)
        {
            DataGridViewTextBoxColumn col = new DataGridViewTextBoxColumn();
            col.HeaderText = header;
            col.Width = 200;
            col.SortMode = DataGridViewColumnSortMode.NotSortable;
            col.DefaultCellStyle.BackColor = Surface;
            col.DefaultCellStyle.ForeColor = fg;
            col.DefaultCellStyle.Font = mono ? FontMono : FontBody;
            col.DefaultCellStyle.SelectionBackColor = RowHover;
            col.DefaultCellStyle.SelectionForeColor = fg;
            return col;
        }

        // 自绘控件重绘前必须整块擦除，否则 hover 换色时旧像素残留造成“字叠字/重影”
        public static Color SurfaceBehind(Control c)
        {
            if (c == null) return Theme.Surface;
            Control p = c.Parent;
            if (p is CardPanel) return Theme.Surface;   // 卡片内部的有效底色是 Surface
            if (p != null)
            {
                Color bc = p.BackColor;
                if (bc.A == 255) return bc;             // 不透明则直接用
            }
            return Theme.Surface;
        }

        public static void EraseBackground(Control c, Graphics g)
        {
            using (SolidBrush b = new SolidBrush(SurfaceBehind(c)))
                g.FillRectangle(b, c.ClientRectangle);
        }

        // ---- 设计令牌：间距 / 圆角 / 动效时长（统一节奏，改一处全局生效）----
        // 间距阶梯：Fluent 2 官方 spacings.ts（4 / 8 / 12 / 16 / 20 / 24）
        public static readonly int GapXs = S(4);
        public static readonly int GapSm = S(8);
        public static readonly int GapMd = S(12);
        public static readonly int GapLg = S(16);
        public static readonly int GapXl = S(20);
        public static readonly int GapXxl = S(24);
        // 圆角：Fluent 2 borderRadius.ts（8 = XLarge / 12 = 2XLarge / 16 = 3XLarge）
        public static readonly int RadSm = S(8);
        public static readonly int RadMd = S(12);
        public static readonly int RadLg = S(16);
        // 时长：Fluent 2 durations.ts（100 = faster / 200 = normal / 250 = gentle）
        public const int DurFast = 100;    // 悬停、聚焦等状态变化
        public const int DurMed = 200;     // 入场：淡入、展开
        public const int DurSlow = 250;    // 高亮闪动
        // 状态层不透明度：Material 3 官方 _md-sys-state（hover 8% / focus 12% / pressed 12%）
        public const double StateHover = 0.08;
        public const double StateFocus = 0.12;
        public const double StatePressed = 0.12;

        public static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            GraphicsPath p = new GraphicsPath();
            if (radius <= 0 || r.Width <= 0 || r.Height <= 0) { p.AddRectangle(r); return p; }
            int d = radius * 2;
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }

    // 圆角自绘按钮（普通 / 主按钮 / 词块）
    public class FlatButton : Button
    {
        public bool Primary;
        public bool Pill;
        public bool Tonal;      // 柔和强调底色（次要操作，如「清空」）
        public int Radius = Theme.S(8);
        public Color HoverOverride = Color.Empty;
        public Color PressOverride = Color.Empty;
        private bool hover, down;
        private double hoverAmt;
        private Tween hoverTw;

        public FlatButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Font = Theme.FontBody;
            TabStop = false;
            Cursor = Cursors.Hand;
        }

        protected override void OnParentChanged(EventArgs e)
        {
            base.OnParentChanged(e);
            Color b = Theme.SurfaceBehind(this);
            if (b.A == 255) BackColor = b;   // 明确不透明，杜绝透明背景重绘残留
        }


        protected override void OnMouseEnter(EventArgs e) { hover = true; AnimateHover(1); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; AnimateHover(0); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }

        // 悬停渐入渐出（比瞬间变色更柔和）
        private void AnimateHover(double target)
        {
            if (hoverTw != null) hoverTw.Stop();
            double from = hoverAmt;
            if (Math.Abs(from - target) < 0.01) { hoverAmt = target; Invalidate(); return; }
            hoverTw = Tween.Run(Theme.DurFast, delegate(double k)
            {
                if (IsDisposed) return;
                hoverAmt = from + (target - from) * Curves.EasyEase(k);
                Invalidate();
            }, null);
        }

        protected override void OnPaint(PaintEventArgs pe)
        {
            Graphics g = pe.Graphics;
            Theme.EraseBackground(this, g);          // 先擦干净再画
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color idle = Primary ? Theme.Accent : (Tonal ? Theme.AccentSoft : (Pill ? Theme.Elevated : Theme.BtnBg));
            Color baseBg = idle;
            // 悬停 = 状态层叠加（Material 3）：主按钮提亮 8%，其余叠 8% 强调色
            Color hotBg = Primary
                ? Anim.Lerp(Theme.Accent, Color.White, Theme.StateHover)
                : Anim.Lerp(idle, Theme.Accent, Tonal ? 0.12 : Theme.StateHover);
            if (down) baseBg = Anim.Lerp(idle, Theme.Accent, Tonal ? 0.20 : Theme.StatePressed);
            if (Primary && down) baseBg = Anim.Lerp(Theme.AccentDown, Color.Black, Theme.StatePressed * 0.25);
            Color bg = Anim.Lerp(baseBg, hotBg, hoverAmt);
            if (down && PressOverride != Color.Empty) bg = PressOverride;
            else if (hoverAmt > 0.5 && HoverOverride != Color.Empty) bg = HoverOverride;

            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath p = Theme.RoundedRect(r, Pill ? Height / 2 : Radius))
            {
                using (SolidBrush b = new SolidBrush(bg)) g.FillPath(b, p);
                if (!Primary)
                {
                    Color bc = Tonal ? Anim.Lerp(Theme.AccentSoft, Theme.Accent, 0.40)
                              : (Pill ? (hover ? Theme.Accent : Theme.Border) : Theme.Border);
                    using (Pen pen = new Pen(bc)) g.DrawPath(pen, p);
                }
            }
            Color fg = Primary ? Color.White : (Pill ? (hover ? Theme.AccentHover : Theme.TextPrimary) : Theme.TextPrimary);
            if (!Enabled) fg = Theme.TextMuted;
            TextRenderer.DrawText(g, Text, Font, new Rectangle(0, 0, Width, Height), fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }

    // 状态徽章：圆角胶囊 + 语义色（已启用/未启用这类状态展示）
    public class Badge : Control
    {
        private string label = "";
        public Color AccentColor = Theme.TextMuted;

        public Badge()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Surface;
            Font = Theme.FontCaption;
            Height = Theme.S(24);
        }

        public new string Text { get { return label; } set { label = value == null ? "" : value; Invalidate(); } }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.EraseBackground(this, e.Graphics);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, Theme.S(2), Width - 1, Height - Theme.S(4));
            using (GraphicsPath p = Theme.RoundedRect(r, r.Height / 2))
            {
                using (SolidBrush b = new SolidBrush(Anim.Lerp(Theme.Surface, AccentColor, 0.16))) g.FillPath(b, p);
                using (Pen pen = new Pen(Anim.Lerp(Theme.Surface, AccentColor, 0.45))) g.DrawPath(pen, p);
            }
            TextRenderer.DrawText(g, label, Font, ClientRectangle, AccentColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
    }

    // 卡片容器：圆角表面 + 细边
    public class CardPanel : Panel
    {
        public string Title;
        public string Hint;
        public int Radius = Theme.S(12);
        public int TitleHeight = Theme.S(30);

        public CardPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
            Padding = new Padding(Theme.GapLg, Theme.S(34), Theme.GapLg, Theme.GapMd);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme.EraseBackground(this, g);          // 先擦干净再画
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath p = Theme.RoundedRect(r, Radius))
            {
                using (SolidBrush b = new SolidBrush(Theme.Surface)) g.FillPath(b, p);
                using (Pen pen = new Pen(Theme.BorderSoft)) g.DrawPath(pen, p);
            }
            if (!string.IsNullOrEmpty(Title))
                TextRenderer.DrawText(g, Title, Theme.FontCardTitle, new Rectangle(Theme.S(14), Theme.S(8), Width - Theme.S(120), Theme.S(20)), Theme.TextPrimary,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            if (!string.IsNullOrEmpty(Hint))
                TextRenderer.DrawText(g, Hint, Theme.FontSmall, new Rectangle(Theme.S(14), Theme.S(8), Width - Theme.S(28), Theme.S(20)), Theme.TextMuted,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            base.OnPaint(e);
        }
    }

    // 自绘复选框
    public class ToggleCheck : CheckBox
    {
        private bool hover;
        public ToggleCheck()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Font = Theme.FontBody;
            ForeColor = Theme.TextSecondary;
            Cursor = Cursors.Hand;
            AutoSize = false;
        }

        public override Size GetPreferredSize(Size proposed)
        {
            Size s = TextRenderer.MeasureText(Text, Font);
            return new Size(s.Width + Theme.S(28), Math.Max(Theme.S(22), s.Height + Theme.S(4)));
        }

        protected override void OnParentChanged(EventArgs e)
        {
            base.OnParentChanged(e);
            Color b = Theme.SurfaceBehind(this);
            if (b.A == 255) BackColor = b;   // 关键：CheckBox 默认背景可透明，显式改不透明
        }


        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs pe)
        {
            Graphics g = pe.Graphics;
            Theme.EraseBackground(this, g);          // 关键修复：hover 换文字色前先擦掉旧文字
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int box = Theme.S(16);
            Rectangle r = new Rectangle(0, (Height - box) / 2, box, box);
            using (GraphicsPath p = Theme.RoundedRect(r, 4))
            {
                using (SolidBrush b = new SolidBrush(Checked ? Theme.Accent : (hover ? Theme.BtnHover : Theme.BtnBg)))
                    g.FillPath(b, p);
                if (!Checked) using (Pen pen = new Pen(hover ? Theme.TextMuted : Theme.Border)) g.DrawPath(pen, p);
            }
            if (Checked)
            {
                using (Pen pen = new Pen(Color.White, 2f))
                {
                    pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round;
                    g.DrawLines(pen, new Point[] {
                        new Point(r.X + Theme.S(4), r.Y + Theme.S(8)),
                        new Point(r.X + Theme.S(7), r.Y + Theme.S(11)),
                        new Point(r.X + Theme.S(12), r.Y + Theme.S(4)) });
                }
            }
            TextRenderer.DrawText(g, Text, Font, new Rectangle(box + Theme.S(8), 0, Width - box - Theme.S(8), Height),
                Enabled ? (hover ? Theme.TextPrimary : Theme.TextSecondary) : Theme.TextMuted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
    }

    // 深色输入框外壳（自绘边框，聚焦高亮）
    public class DarkTextBox : Panel
    {
        public readonly TextBox Box = new TextBox();
        private double focusAmt;
        private Tween focusTw;
        private int radius = Theme.S(10);

        public DarkTextBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;          // 建时先给个不透明底；挂到父容器后会跟着父底色走（见 OnParentChanged）
            Box.BorderStyle = BorderStyle.None;
            Box.BackColor = Theme.Elevated;
            Box.ForeColor = Theme.TextPrimary;
            Box.Font = Theme.FontInput;
            Box.GotFocus += delegate(object s, EventArgs e) { AnimateFocus(1); };
            Box.LostFocus += delegate(object s, EventArgs e) { AnimateFocus(0); };
            Controls.Add(Box);
            Padding = new Padding(Theme.S(12), 0, Theme.S(12), 0);
        }

        private void AnimateFocus(double target)
        {
            if (focusTw != null) focusTw.Stop();
            double from = focusAmt;
            focusTw = Tween.Run(Theme.DurFast, delegate(double k)
            {
                if (IsDisposed) return;
                focusAmt = from + (target - from) * Curves.EasyEase(k);
                Invalidate();
            }, null);
        }

        // 关键修复：输入框外框是圆角，但控件矩形本身会被填成 BackColor。
        // 之前固定 BackColor = Bg（近黑），放在浅一档的卡片里就露出四个“黑色方角”。
        // 现在跟着父容器底色走，圆角外的区域与卡片同色，圆角才真正显出来。
        protected override void OnParentChanged(EventArgs e)
        {
            base.OnParentChanged(e);
            Color behind = Theme.SurfaceBehind(this);
            if (behind.A == 255) BackColor = behind;
            Invalidate();
        }

        public override string Text { get { return Box.Text; } set { Box.Text = value; } }
        public TextBox InnerBox { get { return Box; } }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Box.Location = new Point(Padding.Left, (Height - Box.Height) / 2);
            Box.Width = Width - Padding.Left - Padding.Right;
            UpdateRoundRegion();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            UpdateRoundRegion();
        }

        // 关键：用 Region 把矩形四角裁掉，圆角外的区域直接由父容器透出。
        // 之前只画圆角填充、控件矩形仍被 BackColor 填满，放在浅色卡片里就是四个黑方角。
        private void UpdateRoundRegion()
        {
            if (Width <= 2 || Height <= 2) return;
            try
            {
                using (GraphicsPath p = Theme.RoundedRect(new Rectangle(0, 0, Width, Height), radius))
                    Region = new Region(p);
            }
            catch (Exception) { }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme.EraseBackground(this, g);        // 先擦底，避免圆角外露出旧像素
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath p = Theme.RoundedRect(r, radius))
            {
                using (SolidBrush b = new SolidBrush(Theme.Elevated)) g.FillPath(b, p);
                Color bc = Anim.Lerp(Theme.Border, Theme.Accent, focusAmt);
                using (Pen pen = new Pen(bc, 1f + 0.6f * (float)focusAmt)) g.DrawPath(pen, p);
            }
        }
    }
}
