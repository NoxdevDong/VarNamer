using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace VarNamer
{
    // 单行结果：风格标签 + 变量名；悬停时整行高亮、左侧亮条、右侧出现「复制」
    public class ResultRow : Panel
    {
        private Label lblTag;
        private Label lblVal;
        private Label lblCopy;
        private bool hover;
        private bool isLast;
        private double flash;      // 0..1，结果出现时的高亮闪现
        public event EventHandler CopyRequested;

        public ResultRow(string tag, bool last)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Surface;
            isLast = last;
            Height = FloatForm.RowH;
            Dock = DockStyle.Top;
            Margin = new Padding(0);
            Cursor = Cursors.Hand;

            lblTag = new Label();
            lblTag.Text = tag;
            lblTag.ForeColor = Theme.TextMuted;
            lblTag.Font = Theme.FontSmall;
            lblTag.AutoSize = false;
            lblTag.TextAlign = ContentAlignment.MiddleLeft;
            lblTag.BackColor = Color.Transparent;
            lblTag.Cursor = Cursors.Hand;
            Controls.Add(lblTag);

            lblVal = new Label();
            lblVal.ForeColor = Theme.TextPrimary;
            lblVal.Font = Theme.FontMono;
            lblVal.AutoSize = false;
            lblVal.TextAlign = ContentAlignment.MiddleLeft;
            lblVal.BackColor = Color.Transparent;
            lblVal.AutoEllipsis = true;
            lblVal.Cursor = Cursors.Hand;
            Controls.Add(lblVal);

            lblCopy = new Label();
            lblCopy.Text = "复制";
            lblCopy.ForeColor = Theme.Accent;
            lblCopy.Font = Theme.FontSmall;
            lblCopy.AutoSize = false;
            lblCopy.TextAlign = ContentAlignment.MiddleRight;
            lblCopy.BackColor = Color.Transparent;
            lblCopy.Cursor = Cursors.Hand;
            lblCopy.Visible = false;
            Controls.Add(lblCopy);

            lblTag.Click += Fire;
            lblVal.Click += Fire;
            lblCopy.Click += Fire;
            Click += Fire;
            lblTag.MouseEnter += HoverOn;
            lblVal.MouseEnter += HoverOn;
            lblCopy.MouseEnter += HoverOn;
            MouseEnter += HoverOn;
            lblTag.MouseLeave += HoverOff;
            lblVal.MouseLeave += HoverOff;
            lblCopy.MouseLeave += HoverOff;
            MouseLeave += HoverOff;
        }

        public void SetTagText(string t)
        {
            if (lblTag != null) lblTag.Text = t == null ? "" : t;
        }

        // 结果出现时的逐行高亮（delayMs 错峰，形成“依次亮起”的动效）
        public void PlayAppear(int delayMs)
        {
            Tween.RunDelayed(delayMs, Theme.DurSlow, delegate(double k)
            {
                if (IsDisposed) return;
                flash = 1.0 - Curves.DecelerateMax(k);
                Invalidate();
                if (flash <= 0.01) flash = 0;
            }, null);
        }

        public string Value
        {
            get { return lblVal.Text; }
            set
            {
                lblVal.Text = value;
                lblVal.Tag = value;
                if (tip == null && lblVal != null) tip = new ToolTip();
                if (tip != null) tip.SetToolTip(lblVal, value);
            }
        }

        private ToolTip tip;

        private int tagW = Theme.S(64);

        // 标签列宽度：由外部按“最长的那个标签”算出来，避免 "camelCase(每词≤4)" 被截断
        public int TagWidth
        {
            get { return tagW; }
            set { if (tagW == value) return; tagW = value; LayoutRow(); }
        }

        public string TagText { get { return lblTag == null ? "" : lblTag.Text; } }

        // 中文值要用中文字体：Consolas 没有汉字，回退字体的度量会让行高/基线看着不对
        public void SetValueText(string v, bool cjkFont)
        {
            if (lblVal != null) lblVal.Font = cjkFont ? Theme.FontBodyLg : Theme.FontMono;
            Value = v;
        }

        private void LayoutRow()
        {
            if (lblTag == null) return;
            int valX = Theme.S(14) + tagW + Theme.S(6);
            lblTag.SetBounds(Theme.S(14), 0, tagW, Height);
            lblCopy.SetBounds(Width - Theme.S(56), 0, Theme.S(44), Height);
            lblVal.SetBounds(valX, 0, Math.Max(Theme.S(10), Width - valX - Theme.S(58)), Height);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutRow();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.EraseBackground(this, e.Graphics);      // 自绘先擦底，避免 hover 残留重影
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (hover || flash > 0.01)
            {
                Rectangle rr = new Rectangle(0, 0, Width, Height);
                Color fill = hover ? Theme.RowHover : Theme.Surface;
                if (flash > 0.01) fill = Anim.Lerp(fill, Theme.AccentSoft, flash * 0.95);
                using (GraphicsPath p = Theme.RoundedRect(new Rectangle(rr.X, rr.Y, rr.Width - 1, rr.Height - 1), Theme.RadSm))
                using (SolidBrush b = new SolidBrush(fill))
                    g.FillPath(b, p);
                if (hover)
                    using (SolidBrush ab = new SolidBrush(Theme.Accent))
                        g.FillRectangle(ab, Theme.S(3), Theme.S(7), Theme.S(3), Height - Theme.S(14));
            }
            if (!isLast)
            {
                using (Pen pen = new Pen(Theme.CardLine))
                    g.DrawLine(pen, Theme.S(14), Height - 1, Width - Theme.S(14), Height - 1);
            }
            base.OnPaint(e);
        }

        private void Fire(object sender, EventArgs e)
        {
            if (CopyRequested != null) CopyRequested(this, EventArgs.Empty);
        }

        private void HoverOn(object sender, EventArgs e) { ApplyHover(true); }
        private void HoverOff(object sender, EventArgs e) { ApplyHover(false); }

        private void ApplyHover(bool on)
        {
            if (hover == on) return;
            hover = on;
            if (lblCopy != null) lblCopy.Visible = on && lblVal.Text.Length > 0;
            Invalidate();
        }
    }

    // 悬浮窗：空输入时只留输入框（不显示候选列表），输入内容后才展开 5 行结果
    public class FloatForm : ModernForm
    {
        private AppState state;
        private DarkTextBox input;
        private Panel inputArea;
        private CardPanel rowsHost;
        private TableLayoutPanel rowsBox;
        private Panel emptyPanel;
        private Label lblEmptyTitle;
        private Label lblEmptySub;
        private Label lblTip;
        private FlatButton btnPin;
        private ResultRow[] rows;
        private TabStrip groupStrip;
        private FlatButton btnCollapse;
        private Panel chipPanel;
        private Panel rowsWrap;
        private bool collapsed;          // 收起为桌面挂件
        private int expandedW;           // 收起前的宽度（展开时还原）
        private Point chipDrag;          // 挂件拖动起点
        private bool chipMoved;
        private int group;
        private static readonly string[] GroupNames = new string[] { "混合", "全称", "简短", "缩写" };

        private static readonly string[] RowTags = new string[] { "Pascal", "camel", "snake", "简短", "缩写", "中文" };

        public static readonly int RowH = Theme.S(34);
        private static readonly int InputH = Theme.S(58);
        private static readonly int TipH = Theme.S(26);
        private static readonly int EmptyH = Theme.S(78);
        private static readonly int GroupBarH = Theme.S(28);
        public static readonly int HeaderH = Theme.S(46);

        private bool lastHasText = false;
        private bool bright = false;          // true = 当前是全亮（活动）状态

        public Action OpenMain;
        public Action AfterCopy;
        public Action VisibilityChanged;   // 显示/隐藏状态变化时通知托盘与主界面

        public FloatForm(AppState st)
        {
            state = st;
            Text = "VarNamer 悬浮窗";
            ShowInTaskbar = false;
            TopMost = true;
            CornerRadius = 16;
            LblSub.Text = "v" + AppVersion.Value + " · 悬浮窗";
            Opacity = state.Cfg.FloatOpacityIdle;   // 初始半透明，点进来才全亮
            bright = false;
            BackColor = Theme.Bg;

            TitleButtons.Width = Theme.S(170);   // 主 / 顶 / 缩 / ✕
            BtnMin.Text = "主";
            BtnMin.Font = new Font("Microsoft YaHei UI", 8.5f);
            BtnClose.Font = new Font("Microsoft YaHei UI", 9f);

            btnPin = new FlatButton();
            btnPin.Text = "顶";
            btnPin.Font = new Font("Microsoft YaHei UI", 8.5f);
            btnPin.Size = new Size(Theme.S(34), Theme.S(28));
            btnPin.Radius = Theme.S(7);
            btnPin.Margin = new Padding(Theme.S(4), 0, 0, 0);
            btnPin.HoverOverride = Theme.AccentSoft;
            btnPin.PressOverride = Theme.AccentDown;
            btnPin.ForeColor = Theme.Accent;
            btnPin.Click += delegate(object s, EventArgs e)
            {
                TopMost = !TopMost;
                btnPin.ForeColor = TopMost ? Theme.Accent : Theme.TextMuted;
                lblTip.Text = TopMost ? "已锁定置顶" : "已取消置顶";
            };
            TitleButtons.Controls.Add(btnPin);

            btnCollapse = new FlatButton();
            btnCollapse.Text = "缩";
            btnCollapse.Font = new Font("Microsoft YaHei UI", 8.5f);
            btnCollapse.Size = new Size(Theme.S(34), Theme.S(28));
            btnCollapse.Radius = Theme.S(7);
            btnCollapse.Margin = new Padding(Theme.S(4), 0, 0, 0);
            btnCollapse.HoverOverride = Theme.AccentSoft;
            btnCollapse.PressOverride = Theme.AccentDown;
            btnCollapse.ForeColor = Theme.TextSecondary;
            ToolTip ct2 = new ToolTip();
            ct2.SetToolTip(btnCollapse, "缩小成桌面挂件（不占地方，点挂件展开）");
            btnCollapse.Click += delegate(object s, EventArgs e) { ToggleCollapsed(); };
            TitleButtons.Controls.Add(btnCollapse);

            // ---------- 输入区 ----------
            inputArea = new Panel();
            inputArea.Dock = DockStyle.Top;
            inputArea.Height = InputH;
            inputArea.BackColor = Theme.Bg;
            inputArea.Padding = new Padding(Theme.S(12), Theme.S(8), Theme.S(12), Theme.S(8));

            TableLayoutPanel inputRow = new TableLayoutPanel();
            inputRow.Dock = DockStyle.Fill;
            inputRow.BackColor = Theme.Bg;
            inputRow.Margin = new Padding(0);
            inputRow.ColumnCount = 2;
            inputRow.RowCount = 1;
            inputRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            inputRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            inputRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Theme.S(32)));

            input = new DarkTextBox();
            input.Dock = DockStyle.Fill;
            input.Margin = new Padding(0, 0, Theme.S(8), 0);
            input.InnerBox.Font = Theme.FontInput;
            input.InnerBox.TextChanged += delegate(object s, EventArgs e) { Recalc(); };
            input.InnerBox.GotFocus += delegate(object s, EventArgs e) { if (input.InnerBox.Capture || Control.MouseButtons != MouseButtons.None) Brighten(); };
            input.InnerBox.MouseDown += delegate(object s, MouseEventArgs e) { Brighten(); };
            input.InnerBox.KeyDown += delegate(object s, KeyEventArgs e)
            {
                Brighten();
                if (e.KeyCode == Keys.Down)
                {
                    e.SuppressKeyPress = true;
                    FocusRow(0);
                    return;
                }
                if (e.KeyCode != Keys.Enter) return;
                if (MainForm.IsImeComposing(input.InnerBox)) return;   // 输入法选字回车不算
                e.SuppressKeyPress = true;
                CopyFirst();
            };
            inputRow.Controls.Add(input, 0, 0);

            FlatButton btnClear = new FlatButton();
            btnClear.Text = "✕";
            btnClear.Dock = DockStyle.Fill;
            btnClear.Margin = new Padding(0, Theme.S(1), 0, Theme.S(1));
            btnClear.Radius = Theme.S(6);
            btnClear.Font = new Font("Segoe UI", 9f);
            btnClear.HoverOverride = Color.FromArgb(120, 45, 45);
            ToolTip ct = new ToolTip();
            ct.SetToolTip(btnClear, "清空输入");
            btnClear.Click += delegate(object s, EventArgs e)
            {
                input.Text = "";
                input.InnerBox.Focus();
                lblTip.Text = "已清空输入";
                ApplyState();
            };
            inputRow.Controls.Add(btnClear, 1, 0);
            inputArea.Controls.Add(inputRow);

            // ---------- 空状态（只露输入框）----------
            emptyPanel = new Panel();
            emptyPanel.Dock = DockStyle.Fill;
            emptyPanel.BackColor = Theme.Bg;
            emptyPanel.Padding = new Padding(Theme.S(12), Theme.S(6), Theme.S(12), 0);

            lblEmptyTitle = new Label();
            lblEmptyTitle.Dock = DockStyle.Top;
            lblEmptyTitle.Height = Theme.S(34);
            lblEmptyTitle.TextAlign = ContentAlignment.MiddleCenter;
            lblEmptyTitle.ForeColor = Theme.TextSecondary;
            lblEmptyTitle.Font = Theme.FontBody;
            lblEmptyTitle.BackColor = Theme.Bg;
            lblEmptyTitle.Text = "输入中文，立即生成 5 种命名风格";
            lblEmptySub = new Label();
            lblEmptySub.Dock = DockStyle.Top;
            lblEmptySub.Height = Theme.S(22);
            lblEmptySub.TextAlign = ContentAlignment.MiddleCenter;
            lblEmptySub.ForeColor = Theme.TextMuted;
            lblEmptySub.Font = Theme.FontSmall;
            lblEmptySub.BackColor = Theme.Bg;
            lblEmptySub.Text = "回车复制第一行 · 点结果行复制 · Ctrl+滚轮调透明度 · Esc 隐藏";

            // 注意：Dock=Top 的控件是“后加的排在上面”，这里倒序加：sub → title
            emptyPanel.Controls.Add(lblEmptySub);
            emptyPanel.Controls.Add(lblEmptyTitle);

            // ---------- 结果区（有输入才显示）----------
            rowsHost = new CardPanel();
            rowsHost.Dock = DockStyle.Fill;
            rowsHost.Title = "";
            rowsHost.Hint = "";
            rowsHost.Radius = Theme.S(12);
            rowsHost.Padding = new Padding(Theme.S(8), Theme.S(8), Theme.S(8), Theme.S(8));
            rowsHost.Margin = new Padding(Theme.S(12), 0, Theme.S(12), 0);

            rowsBox = new TableLayoutPanel();
            rowsBox.Dock = DockStyle.Fill;
            rowsBox.BackColor = Theme.Surface;
            rowsBox.ColumnCount = 1;
            rowsBox.RowCount = RowTags.Length;
            rows = new ResultRow[RowTags.Length];
            for (int i = 0; i < RowTags.Length; i++)
            {
                rowsBox.RowStyles.Add(new RowStyle(SizeType.Absolute, RowH));
                ResultRow rr = new ResultRow(RowTags[i], i == RowTags.Length - 1);
                rr.CopyRequested += delegate(object s, EventArgs e) { CopyRow((ResultRow)s); };
                rows[i] = rr;
                rowsBox.Controls.Add(rr, 0, i);
            }
            rowsHost.Controls.Add(rowsBox);

            groupStrip = new TabStrip();
            groupStrip.Small = true;
            groupStrip.Height = GroupBarH;
            groupStrip.TabGap = Theme.S(16);
            groupStrip.Indent = Theme.S(2);
            groupStrip.Dock = DockStyle.Top;
            groupStrip.BackColor = Theme.Bg;
            groupStrip.Items = GroupNames;
            groupStrip.SelectedIndexChanged += delegate(object s, EventArgs e)
            {
                group = groupStrip.SelectedIndex;
                state.Cfg.FloatGroup = group;
                state.Cfg.Save();
                Recalc();
            };

            rowsWrap = new Panel();
            rowsWrap.Dock = DockStyle.Fill;
            rowsWrap.BackColor = Theme.Bg;
            rowsWrap.Padding = new Padding(Theme.S(12), 0, Theme.S(12), Theme.S(10));
            rowsWrap.Controls.Add(rowsHost);
            rowsWrap.Controls.Add(groupStrip);

            // ---------- 桌面挂件（收起态）----------
            chipPanel = new Panel();
            chipPanel.Dock = DockStyle.Fill;
            chipPanel.BackColor = Theme.Bg;
            chipPanel.Visible = false;
            chipPanel.Cursor = Cursors.Hand;
            ToolTip chipTip = new ToolTip();
            chipTip.SetToolTip(chipPanel, "点击展开悬浮窗 · 按住可拖动");

            Label chipIcon = new Label();
            chipIcon.AutoSize = false;
            chipIcon.BackColor = Color.Transparent;
            chipIcon.TextAlign = ContentAlignment.MiddleCenter;
            chipIcon.Text = "V";
            chipIcon.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
            chipIcon.ForeColor = Theme.Accent;
            chipIcon.SetBounds(Theme.S(10), Theme.S(9), Theme.S(18), Theme.S(18));
            chipIcon.Cursor = Cursors.Hand;
            chipPanel.Controls.Add(chipIcon);

            Label chipText = new Label();
            chipText.AutoSize = false;
            chipText.BackColor = Color.Transparent;
            chipText.TextAlign = ContentAlignment.MiddleLeft;
            chipText.Text = "VarNamer";
            chipText.Font = Theme.FontSmallBold;
            chipText.ForeColor = Theme.TextPrimary;
            chipText.SetBounds(Theme.S(32), 0, Theme.S(88), Theme.S(38));
            chipText.Cursor = Cursors.Hand;
            chipPanel.Controls.Add(chipText);

            Label chipHint = new Label();
            chipHint.AutoSize = false;
            chipHint.BackColor = Color.Transparent;
            chipHint.TextAlign = ContentAlignment.MiddleCenter;
            chipHint.Text = "▸";
            chipHint.Font = Theme.FontSmall;
            chipHint.ForeColor = Theme.TextMuted;
            chipHint.SetBounds(Theme.S(122), 0, Theme.S(18), Theme.S(38));
            chipHint.Cursor = Cursors.Hand;
            chipPanel.Controls.Add(chipHint);

            // 点一下展开；按住拖动移动；hover 时由半透明变全亮
            chipPanel.MouseDown += ChipDown;
            chipPanel.MouseMove += ChipMove;
            chipPanel.MouseUp += ChipUp;
            chipIcon.MouseDown += ChipDown; chipIcon.MouseMove += ChipMove; chipIcon.MouseUp += ChipUp;
            chipText.MouseDown += ChipDown; chipText.MouseMove += ChipMove; chipText.MouseUp += ChipUp;
            chipHint.MouseDown += ChipDown; chipHint.MouseMove += ChipMove; chipHint.MouseUp += ChipUp;
            chipIcon.MouseEnter += delegate(object s, EventArgs e) { Brighten(); };
            chipText.MouseEnter += delegate(object s, EventArgs e) { Brighten(); };
            chipPanel.MouseEnter += delegate(object s, EventArgs e) { Brighten(); };
            chipPanel.MouseLeave += delegate(object s, EventArgs e) { if (!chipMoved) Dim(); };

            lblTip = new Label();
            lblTip.Dock = DockStyle.Bottom;
            lblTip.Height = TipH;
            lblTip.ForeColor = Theme.TextMuted;
            lblTip.Font = Theme.FontSmall;
            lblTip.TextAlign = ContentAlignment.MiddleLeft;
            lblTip.Padding = new Padding(Theme.S(14), 0, 0, 0);
            lblTip.BackColor = Theme.Bg;
            lblTip.Text = "输入中文即可生成 · 点结果行复制";

            // 加顺序：Dock 填充的控件后加的先占位，这里按 输入区 → 结果区 → 空状态 → 提示 排列
            Content.Controls.Add(chipPanel);
            Content.Controls.Add(emptyPanel);
            Content.Controls.Add(rowsWrap);
            Content.Controls.Add(inputArea);
            Content.Controls.Add(lblTip);

            int w = state.Cfg.FloatW > 0 ? state.Cfg.FloatW : Theme.S(500);
            Size = new Size(w, FullHeight());
            MinimumSize = new Size(Theme.S(420), EmptyHeight());
            Location = new Point(state.Cfg.FloatX, state.Cfg.FloatY);

            KeyPreview = true;
            KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape) HideFloat();
            };
            MouseWheel += OnWheel;
            FormClosing += delegate(object s, FormClosingEventArgs e)
            {
                if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; HideFloat(); }
            };

            AttachBrighten(this);            // 窗口内任意位置按下 → 全亮
            group = state.Cfg.FloatGroup;
            if (group < 0 || group > 3) group = 0;
            SyncGroupTabs();
            ApplyState();
        }

        private void SyncGroupTabs()
        {
            if (groupStrip != null && groupStrip.SelectedIndex != group) groupStrip.SelectedIndex = group;
        }

        private void AttachBrighten(Control c)
        {
            c.MouseDown += delegate(object s, MouseEventArgs e) { Brighten(); };
            for (int i = 0; i < c.Controls.Count; i++) AttachBrighten(c.Controls[i]);
        }

        private int VisibleRowCount()
        {
            int n = 0;
            for (int i = 0; i < rows.Length; i++) if (rows[i].Visible) n++;
            return n;
        }

        // 精确高度：标题栏 + 输入区 + 提示行 + 切换条 + 卡片内边距 + 可见行 + 卡片外边距
        // （之前少算了卡片上下内边距，最后一行会被裁掉几个像素）
        private int FullHeight()
        {
            return HeaderH + InputH + TipH + GroupBarH
                 + rowsHost.Padding.Vertical + VisibleRowCount() * RowH + rowsWrap.Padding.Vertical;
        }

        private int EmptyHeight()
        {
            return HeaderH + InputH + EmptyH + TipH;
        }

        // ---------- 收起为桌面挂件 / 展开 ----------
        public bool Collapsed { get { return collapsed; } }

        public void ToggleCollapsed() { SetCollapsed(!collapsed); }

        public void Expand() { SetCollapsed(false); }

        private void SetCollapsed(bool on)
        {
            if (collapsed == on) return;
            if (on)
            {
                expandedW = Width;        // 记住展开时的宽度
                SavePosition();           // 同时记住位置
                collapsed = true;
                state.Cfg.FloatVisible = true;
                ApplyState();
                ClampChipOnScreen();
                Tween.FadeIn(this, state.Cfg.FloatOpacityIdle, Theme.DurMed);
            }
            else
            {
                collapsed = false;
                ApplyState();
                int w = expandedW > 0 ? expandedW : state.Cfg.FloatW;
                if (w > Width) Width = w;      // 还原展开宽度（高度由 ApplyState 定）
                // 关键：按“当前坐标”钳制并写回，而不是读回收起前记住的旧坐标
                // （否则把挂件拖到别处后展开会跳回原点）
                ClampChipOnScreen();
                SavePosition();
                input.InnerBox.Focus();
            }
        }

        // 挂件要留在屏幕内（用当前坐标，不能用展开时记住的坐标）
        private void ClampChipOnScreen()
        {
            try
            {
                Rectangle wa = Screen.FromPoint(Location).WorkingArea;
                int x = Math.Max(wa.Left, Math.Min(Location.X, wa.Right - Width));
                int y = Math.Max(wa.Top, Math.Min(Location.Y, wa.Bottom - Height));
                Location = new Point(x, y);
            }
            catch (Exception) { }
        }

        // 挂件：按住拖动移动；点一下（没拖动）展开
        private void ChipDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            chipDrag = Cursor.Position;      // 屏幕坐标，避免跨控件坐标系混用
            chipMoved = false;
        }

        private void ChipMove(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            Point now = Cursor.Position;
            int dx = now.X - chipDrag.X, dy = now.Y - chipDrag.Y;
            if (!chipMoved && Math.Abs(dx) + Math.Abs(dy) < 4) return;   // 抖动阈值
            chipMoved = true;
            chipDrag = now;
            Location = new Point(Location.X + dx, Location.Y + dy);
        }

        private void ChipUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            bool moved = chipMoved;
            chipMoved = false;
            if (!moved) Expand();            // 没拖动 → 点击展开
            else
            {
                SavePosition();              // 挂件拖到哪就记到哪（挂件态不会覆盖展开宽度）
                Dim();                       // 拖完回到半透明
            }
        }

        private bool HasAnyValue()
        {
            for (int i = 0; i < rows.Length; i++) if (!string.IsNullOrEmpty(rows[i].Value)) return true;
            return false;
        }

        // 有可用结果 → 展开结果列表；空输入、或一个字都没认出来 → 收起，只留输入框
        private void ApplyState()
        {
            // 收起态：只留一个桌面挂件（宽度 150、高度 38）
            if (collapsed)
            {
                TitleBar.Visible = false;
                inputArea.Visible = false;
                emptyPanel.Visible = false;
                rowsWrap.Visible = false;
                lblTip.Visible = false;
                chipPanel.Visible = true;
                chipPanel.BringToFront();
                Size chipSize = new Size(Theme.S(150), Theme.S(38));
                MinimumSize = chipSize;
                MaximumSize = chipSize;
                if (Size != chipSize) Size = chipSize;
                return;
            }
            TitleBar.Visible = true;
            inputArea.Visible = true;
            lblTip.Visible = true;
            chipPanel.Visible = false;
            MaximumSize = new Size(0, 0);

            bool typed = input.Text != null && input.Text.Trim().Length > 0;
            bool has = typed && HasAnyValue();
            if (lblEmptyTitle != null)
                lblEmptyTitle.Text = !typed
                    ? "输入中文，立即生成 5 种命名风格"
                    : "没有识别到可用的词 · 可在主窗口「加词库」补词";
            emptyPanel.Visible = !has;
            rowsHost.Parent.Visible = has;
            int target = has ? FullHeight() : EmptyHeight();
            MinimumSize = new Size(Theme.S(420), has ? target : EmptyHeight());
            if (Height != target)
            {
                int bottom = Location.Y + Height;
                Height = target;
                try
                {
                    Rectangle wa = Screen.FromPoint(new Point(Location.X, bottom)).WorkingArea;
                    if (Location.Y + Height > wa.Bottom) Location = new Point(Location.X, Math.Max(wa.Top, wa.Bottom - Height));
                }
                catch (Exception) { }
            }
            if (has && !lastHasText)
            {
                lblTip.Text = "点结果行复制 · Ctrl+滚轮调透明度 · Esc 隐藏";
                input.InnerBox.Focus();
                for (int i = 0; i < rows.Length; i++) rows[i].PlayAppear(i * 35);   // 逐行错峰亮起
            }
            else if (typed && !has && !lastHasText)
            {
                lblTip.Text = "没有识别到可用的词（可在主窗口把新词加进词库）";
            }
            lastHasText = has;
        }

        protected override bool FadeOnShow { get { return false; } }   // 悬浮窗自己控制透明度

        protected override void OnChromeClose() { HideFloat(); }
        protected override void OnChromeMinimize() { if (OpenMain != null) OpenMain(); }

        private void OnWheel(object sender, MouseEventArgs e)
        {
            if ((ModifierKeys & Keys.Control) != Keys.Control) return;
            double step = e.Delta > 0 ? 0.04 : -0.04;
            if (bright)
            {
                double o = state.Cfg.FloatOpacity + step;
                if (o < 0.4) o = 0.4;
                if (o > 1.0) o = 1.0;
                state.Cfg.FloatOpacity = o;
                Opacity = o;
                lblTip.Text = "全亮不透明度 " + Math.Round(o * 100) + "%";
            }
            else
            {
                double o = state.Cfg.FloatOpacityIdle + step;
                if (o < 0.25) o = 0.25;
                if (o > 1.0) o = 1.0;
                state.Cfg.FloatOpacityIdle = o;
                Opacity = o;
                lblTip.Text = "默认透明度 " + Math.Round(o * 100) + "%（点进来会全亮）";
            }
        }

        // 用户碰了窗口（点击/输入）→ 取消半透明，全亮
        private void Brighten()
        {
            if (bright) return;
            bright = true;
            Opacity = state.Cfg.FloatOpacity;
        }

        // 变回默认的半透明
        private void Dim()
        {
            bright = false;
            Opacity = state.Cfg.FloatOpacityIdle;
        }

        // 设置里改了“默认透明度”后即时生效
        public void ApplyIdleOpacity()
        {
            if (!bright) Opacity = state.Cfg.FloatOpacityIdle;
        }

        private void CopyRow(ResultRow row)
        {
            string v = row.Value;
            if (string.IsNullOrEmpty(v)) return;
            if (Clip.SetText(v))
            {
                lblTip.Text = "已复制：" + v;
                if (AfterCopy != null) AfterCopy();     // 复制后切回上一个窗口（可在主窗口里关掉）
            }
            else lblTip.Text = "复制失败，请重试";
        }

        private void CopyFirst()
        {
            for (int i = 0; i < rows.Length; i++)
            {
                if (!string.IsNullOrEmpty(rows[i].Value)) { CopyRow(rows[i]); return; }
            }
        }

        private void FocusRow(int index)
        {
            if (index >= 0 && index < rows.Length) lblTip.Text = "第 " + (index + 1) + " 行：" + rows[index].Value;
        }

        public string CurrentInput() { return input.Text; }

        public void SetInput(string text)
        {
            input.Text = text;
            Brighten();                      // 通过热键带文字唤起 = 用户正在用，直接全亮
            if (collapsed && text != null && text.Trim().Length > 0) Expand();   // 有内容要读 → 自动展开
            input.InnerBox.SelectionStart = input.Text.Length;
            Recalc();
        }

        private void Recalc()
        {
            string typed = input.Text == null ? "" : input.Text.Trim();
            bool has = typed.Length > 0;

            if (!has)
            {
                // 输入为空：清空候选行，不展示占位条目
                for (int i = 0; i < rows.Length; i++) rows[i].Value = "";
                ApplyState();
                return;
            }

            NameResult r = Namer.Create(input.Text, state.Lex, state.Opt);
            state.Current = r;

            // 纯英文输入：只显示一行「中文」反查结果（风格行与切换条都隐藏）
            string typedEn = input.Text == null ? "" : input.Text.Trim();
            if (typedEn.Length > 0 && !TextUtil.HasCjk(typedEn))
            {
                for (int i = 0; i < rows.Length; i++)
                {
                    rows[i].Visible = false;
                    rowsBox.RowStyles[i].Height = 0;
                    rows[i].Value = "";
                }
                if (groupStrip != null) groupStrip.Visible = false;
                string cnOne = state.Lex.LookupPhrase(typedEn);
                int ri = rows.Length - 1;
                if (cnOne != null)
                {
                    rows[ri].SetTagText("中文");
                    rows[ri].SetValueText(cnOne, true);
                    rows[ri].Visible = true;
                    rowsBox.RowStyles[ri].Height = RowH;
                }
                int tagCn = TextRenderer.MeasureText("中文", Theme.FontSmall).Width + Theme.S(12);
                for (int i = 0; i < rows.Length; i++) rows[i].TagWidth = Math.Max(Theme.S(56), tagCn);
                ApplyState();
                lblTip.Text = cnOne != null ? "英文反查中文 · 点这一行即复制" : "词库里没查到这个词的中文";
                return;
            }
            if (groupStrip != null) groupStrip.Visible = true;

            // 风格组：0 混合（随语言预设）/ 1 全称 / 2 简短 / 3 缩写
            string lang = state.Cfg.Language;
            int[] src;
            if (group == 1) src = new int[] { LanguagePreset.VariableRow(lang), LanguagePreset.ClassRow(lang), LanguagePreset.ConstantRow(lang), 4, 5 };
            else if (group == 2) src = new int[] { 6, 7, 8 };
            else if (group == 3) src = new int[] { 9, 10, 11, 12 };
            else src = LanguagePreset.FloatRows(lang);

            for (int i = 0; i < rows.Length; i++)
            {
                bool show = i < src.Length;
                rows[i].Visible = show;
                rowsBox.RowStyles[i].Height = show ? RowH : 0;
                if (!show) { rows[i].Value = ""; continue; }
                int idx = src[i];
                rows[i].SetTagText(RowLabel(idx, r, lang));
                rows[i].Value = (idx >= 0 && idx < r.Lines.Count) ? r.Lines[idx].Value : "";
            }

            // 输入是纯英文时，反向查一遍词库：score → 成绩 / 分数 / 得分
            int revIdx = rows.Length - 1;                 // 最后一行「中文」专门给反查用
            bool revOn = false;
            string typed2 = input.Text == null ? "" : input.Text.Trim();
            if (typed2.Length > 0 && !TextUtil.HasCjk(typed2))
            {
                List<string> cn = state.Lex.ReverseLookup(typed2, 8);
                if (cn.Count > 0)
                {
                    rows[revIdx].SetTagText("中文");
                    rows[revIdx].Value = string.Join(" / ", cn.ToArray());
                    rows[revIdx].Visible = true;
                    rowsBox.RowStyles[revIdx].Height = RowH;
                    revOn = true;
                }
            }
            if (!revOn)
            {
                rows[revIdx].Visible = false;
                rowsBox.RowStyles[revIdx].Height = 0;
                rows[revIdx].Value = "";
            }

            // 标签列宽度自适应：按最长标签量一次，避免 "camelCase(每词≤4)" 之类被截断
            int maxTag = 0;
            for (int i = 0; i < rows.Length; i++)
            {
                if (!rows[i].Visible) continue;
                int tw = TextRenderer.MeasureText(rows[i].TagText, Theme.FontSmall).Width;
                if (tw > maxTag) maxTag = tw;
            }
            int tagPx = Math.Max(Theme.S(56), Math.Min(maxTag + Theme.S(12), (int)(Width * 0.44)));
            for (int i = 0; i < rows.Length; i++) rows[i].TagWidth = tagPx;

            ApplyState();

            if (r.Unknowns.Count > 0)
                lblTip.Text = "未识别：" + string.Join(" ", r.Unknowns.ToArray()) + "（主窗口可加词库）";
            else if (r.BooleanDetected && !state.Opt.BooleanPrefix)
                lblTip.Text = "『是否』已按设置忽略（布尔前缀关闭）";
            else if (!lblTip.Text.StartsWith("已复制") && !lblTip.Text.StartsWith("透明度") && !lblTip.Text.StartsWith("已锁定") && !lblTip.Text.StartsWith("已取消"))
                lblTip.Text = "点结果行复制 · Ctrl+滚轮调透明度 · Esc 隐藏";
        }

        // 行标签：变量/类/常量随语言预设，其余用该风格自身名字（去掉“全称/简短/缩写”前缀）
        private static string RowLabel(int idx, NameResult r, string lang)
        {
            if (idx == LanguagePreset.VariableRow(lang)) return "变量";
            if (idx == LanguagePreset.ClassRow(lang)) return "类";
            if (idx == LanguagePreset.ConstantRow(lang)) return "常量";
            if (idx < 0 || idx >= r.Lines.Count) return "";
            string t = r.Lines[idx].Label;
            int sp = t.IndexOf(' ');
            if (sp > 0) t = t.Substring(sp + 1);
            // 去掉「每词≤4 / 每段≤3」这类长度说明；但保留 首字母(小写)/(大写) 以免两行重名
            int br = t.IndexOfAny(new char[] { '(', '（' });
            if (br > 0)
            {
                int close = t.IndexOfAny(new char[] { ')', '）' }, br);
                string inside = close > br ? t.Substring(br + 1, close - br - 1) : "";
                if (inside.StartsWith("每")) t = (t.Substring(0, br) + (close > br ? t.Substring(close + 1) : "")).Trim();
            }
            return t;
        }

        public void ShowFloat()
        {
            ClampToScreen();                 // 位置若落在屏幕外（换显示器/分辨率变化），拉回可见区域
            Opacity = 0;                             // 淡入：0 → 默认半透明
            bright = false;
            Show();
            TopMost = true;
            state.Cfg.FloatVisible = true;
            Tween.FadeIn(this, state.Cfg.FloatOpacityIdle, Theme.DurMed);
            if (VisibilityChanged != null) VisibilityChanged();
        }

        // 把窗口位置限制在当前显示器工作区内，避免“窗口不见了”
        public void ClampToScreen()
        {
            try
            {
                Rectangle wa = Screen.FromPoint(new Point(state.Cfg.FloatX, state.Cfg.FloatY)).WorkingArea;
                int x = Math.Max(wa.Left, Math.Min(state.Cfg.FloatX, wa.Right - Width));
                int y = Math.Max(wa.Top, Math.Min(state.Cfg.FloatY, wa.Bottom - Height));
                state.Cfg.FloatX = x;
                state.Cfg.FloatY = y;
                Location = new Point(x, y);
            }
            catch (Exception) { }
        }

        public void HideFloat()
        {
            SavePosition();
            Hide();
            state.Cfg.FloatVisible = false;
            if (VisibilityChanged != null) VisibilityChanged();
        }

        public void SavePosition()
        {
            state.Cfg.FloatX = Location.X;
            state.Cfg.FloatY = Location.Y;
            if (!collapsed) state.Cfg.FloatW = Width;   // 挂件态不覆盖展开宽度
            state.Cfg.FloatH = Height;
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            SavePosition();
            Dim();                           // 点到别的地方 → 恢复半透明
        }
    }
}
