using System;
using System.Drawing;
using System.IO;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace VarNamer
{
    // 热键录制框：点一下，按组合键即可录入
    public class HotkeyBox : Control
    {
        private string value = "";
        private bool focused;

        public event EventHandler ValueChanged;

        public HotkeyBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            Font = Theme.FontMonoSm;
            BackColor = Theme.Surface;
            Cursor = Cursors.Hand;
            TabStop = true;
            Height = Theme.S(30);
        }

        public string Value
        {
            get { return value; }
            set
            {
                this.value = value == null ? "" : value;
                if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
                Invalidate();
            }
        }

        protected override void OnParentChanged(EventArgs e)
        {
            base.OnParentChanged(e);
            Color b = Theme.SurfaceBehind(this);
            if (b.A == 255) BackColor = b;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            Focus();
            base.OnMouseDown(e);
        }

        protected override void OnGotFocus(EventArgs e) { focused = true; Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { focused = false; Invalidate(); base.OnLostFocus(e); }

        // 优先在此捕获组合键（避免被窗体当作快捷键/助记符吞掉）
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (Focused)
            {
                Keys k = keyData & Keys.KeyCode;
                bool hasMod = ((keyData & Keys.Control) == Keys.Control) || ((keyData & Keys.Alt) == Keys.Alt)
                    || ((keyData & Keys.Shift) == Keys.Shift);
                if (hasMod && k != Keys.ControlKey && k != Keys.ShiftKey && k != Keys.Menu)
                {
                    int mods, vk;
                    string text = Hotkey.FromKeys(keyData);
                    if (Hotkey.TryParse(text, out mods, out vk))
                    {
                        Value = text;
                        return true;
                    }
                }
                else if (k != Keys.ControlKey && k != Keys.ShiftKey && k != Keys.Menu && k != Keys.Escape)
                {
                    value = "";                       // 只按了普通键：清空并提示需要修饰键
                    Invalidate();
                    return true;
                }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) { Value = ""; e.SuppressKeyPress = true; }
            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs pe)
        {
            Graphics g = pe.Graphics;
            Theme.EraseBackground(this, g);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath p = Theme.RoundedRect(r, Theme.S(7)))
            {
                using (SolidBrush b = new SolidBrush(Theme.Elevated)) g.FillPath(b, p);
                using (Pen pen = new Pen(focused ? Theme.Accent : Theme.Border, focused ? 1.6f : 1f)) g.DrawPath(pen, p);
            }
            string t = (value == null || value.Length == 0)
                ? (focused ? "请按下组合键（需含 Ctrl / Alt / Shift）" : "点这里设置")
                : value;
            Color fg = (value == null || value.Length == 0) ? Theme.TextMuted : Theme.TextPrimary;
            TextRenderer.DrawText(g, t, value == null || value.Length == 0 ? Theme.FontSmall : Theme.FontMonoSm,
                new Rectangle(Theme.S(10), 0, Width - Theme.S(20), Height), fg,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    // 设置窗：热键 / 主题 / 最近使用 / 开机自启
    public class SettingsForm : ModernForm
    {
        private AppState state;
        private TrayApp app;
        private HotkeyBox hkBox;
        private DarkCombo cboTheme;
        private ToggleCheck chkAutoStart;
        private ToggleCheck chkBack;
        private Label lblStatus;
        private HotkeyBox hkShot;
        private DarkTextBox txtShotDir;
        private DarkCombo cboOpacity;

        public SettingsForm(AppState st, TrayApp owner)
        {
            state = st;
            app = owner;
            Text = "VarNamer  设置";
            Size = new Size(Theme.S(680), Theme.S(636));
            MinimumSize = new Size(Theme.S(620), Theme.S(606));
            LblSub.Text = "v" + AppVersion.Value + " · 设置";
            StartPosition = FormStartPosition.CenterParent;

            TableLayoutPanel rootGrid = new TableLayoutPanel();
            rootGrid.Dock = DockStyle.Fill;
            rootGrid.BackColor = Theme.Bg;
            rootGrid.ColumnCount = 1;
            rootGrid.RowCount = 7;
            rootGrid.Padding = new Padding(Theme.S(16), Theme.S(4), Theme.S(16), Theme.S(14));
            rootGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.S(46)));   // 悬浮窗热键
            rootGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.S(46)));   // 主题
            rootGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.S(46)));   // 截图热键
            rootGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.S(46)));   // 截图目录
            rootGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.S(46)));   // 开关
            rootGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.S(46)));   // 悬浮窗默认透明度
            rootGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));           // 按钮 */

            Content.Controls.Add(rootGrid);

            // 热键
            TableLayoutPanel r1 = Row(4, Theme.S(150), 0, Theme.S(120), 0);
            r1.Controls.Add(Caption("呼出悬浮窗热键"), 0, 0);
            hkBox = new HotkeyBox();
            hkBox.Dock = DockStyle.Fill;
            hkBox.Margin = new Padding(0, Theme.S(2), Theme.S(8), Theme.S(2));
            hkBox.Value = state.Cfg.Hotkey;
            r1.Controls.Add(hkBox, 1, 0);
            FlatButton btnReset = new FlatButton();
            btnReset.Text = "恢复默认";
            btnReset.Dock = DockStyle.Fill;
            btnReset.Margin = new Padding(0, Theme.S(2), Theme.S(8), Theme.S(2));
            btnReset.Click += delegate(object s, EventArgs e) { hkBox.Value = "Ctrl+Alt+V"; };
            r1.Controls.Add(btnReset, 2, 0);
            Label lbHk = new Label();
            lbHk.Text = "点输入框后按下组合键；至少含 Ctrl/Alt/Shift 之一";
            lbHk.Dock = DockStyle.Fill;
            lbHk.AutoSize = false;
            lbHk.Margin = new Padding(0);
            lbHk.Font = Theme.FontSmall;
            lbHk.ForeColor = Theme.TextMuted;
            lbHk.TextAlign = ContentAlignment.MiddleLeft;
            r1.Controls.Add(lbHk, 3, 0);
            rootGrid.Controls.Add(r1, 0, 0);

            // 主题
            TableLayoutPanel r2 = Row(4, Theme.S(150), Theme.S(140), Theme.S(120), 0);
            r2.Controls.Add(Caption("界面主题"), 0, 0);
            cboTheme = new DarkCombo();
            cboTheme.Dock = DockStyle.Fill;
            cboTheme.Margin = new Padding(0, Theme.S(5), Theme.S(8), Theme.S(5));
            cboTheme.AddRange(new object[] { "深色（默认）", "浅色" });
            cboTheme.SelectText(state.Cfg.ThemeName == "light" ? "浅色" : "深色（默认）");
            r2.Controls.Add(cboTheme, 1, 0);
            Label lbTheme = new Label();
            lbTheme.Text = "切换后窗口会自动重建生效";
            lbTheme.Dock = DockStyle.Fill;
            lbTheme.AutoSize = false;
            lbTheme.Margin = new Padding(0);
            lbTheme.Font = Theme.FontSmall;
            lbTheme.ForeColor = Theme.TextMuted;
            lbTheme.TextAlign = ContentAlignment.MiddleLeft;
            r2.Controls.Add(lbTheme, 2, 0);
            rootGrid.Controls.Add(r2, 0, 1);

            // 截图热键
            TableLayoutPanel rShot = Row(4, Theme.S(150), 0, Theme.S(120), 0);
            rShot.Controls.Add(Caption("截图热键"), 0, 0);
            hkShot = new HotkeyBox();
            hkShot.Dock = DockStyle.Fill;
            hkShot.Margin = new Padding(0, Theme.S(2), Theme.S(8), Theme.S(2));
            hkShot.Value = state.Cfg.ShotHotkey;
            rShot.Controls.Add(hkShot, 1, 0);
            Label lbShot = new Label();
            lbShot.Text = "默认 Ctrl+Alt+A（微信同款）";
            lbShot.Dock = DockStyle.Fill;
            lbShot.AutoSize = false;
            lbShot.Margin = new Padding(0);
            lbShot.Font = Theme.FontSmall;
            lbShot.ForeColor = Theme.TextMuted;
            lbShot.TextAlign = ContentAlignment.MiddleLeft;
            rShot.Controls.Add(lbShot, 2, 0);
            rootGrid.Controls.Add(rShot, 0, 2);

            // 截图保存目录
            TableLayoutPanel rDir = Row(3, Theme.S(150), 0, Theme.S(120), 0);
            rDir.Controls.Add(Caption("截图保存目录"), 0, 0);
            txtShotDir = new DarkTextBox();
            txtShotDir.Dock = DockStyle.Fill;
            txtShotDir.Margin = new Padding(0, Theme.S(2), Theme.S(8), Theme.S(2));
            txtShotDir.InnerBox.Font = Theme.FontSmall;
            txtShotDir.Text = state.Cfg.ShotDir;
            rDir.Controls.Add(txtShotDir, 1, 0);
            FlatButton btnPickDir = new FlatButton();
            btnPickDir.Text = "浏览…";
            btnPickDir.Dock = DockStyle.Fill;
            btnPickDir.Margin = new Padding(0, Theme.S(2), Theme.S(8), Theme.S(2));
            btnPickDir.Click += delegate(object s, EventArgs e)
            {
                FolderBrowserDialog fbd = new FolderBrowserDialog();
                fbd.Description = "选择截图默认保存目录";
                if (Directory.Exists(txtShotDir.Text)) fbd.SelectedPath = txtShotDir.Text;
                if (fbd.ShowDialog(this) == DialogResult.OK) txtShotDir.Text = fbd.SelectedPath;
            };
            rDir.Controls.Add(btnPickDir, 2, 0);
            rootGrid.Controls.Add(rDir, 0, 3);

            // 开关
            FlowLayoutPanel r4 = new FlowLayoutPanel();
            r4.Dock = DockStyle.Fill;
            r4.WrapContents = false;
            r4.BackColor = Theme.Bg;
            r4.MinimumSize = new Size(0, Theme.S(34));
            chkAutoStart = new ToggleCheck();
            chkAutoStart.Text = "开机自启";
            chkAutoStart.Checked = TrayApp.IsAutoStartEnabled();
            chkAutoStart.Margin = new Padding(0, Theme.S(2), Theme.S(20), 0);
            chkAutoStart.Size = chkAutoStart.GetPreferredSize(Size.Empty);
            r4.Controls.Add(chkAutoStart);
            chkBack = new ToggleCheck();
            chkBack.Text = "复制后切回上个窗口";
            chkBack.Checked = state.Cfg.ReturnToPrevWindow;
            chkBack.Margin = new Padding(0, Theme.S(2), 0, 0);
            chkBack.Size = chkBack.GetPreferredSize(Size.Empty);
            r4.Controls.Add(chkBack);
            rootGrid.Controls.Add(r4, 0, 4);

            // 悬浮窗默认透明度（未激活时；点进窗口会全亮）
            TableLayoutPanel rOp = Row(4, Theme.S(150), Theme.S(140), Theme.S(120), 0);
            rOp.Controls.Add(Caption("悬浮窗默认透明度"), 0, 0);
            cboOpacity = new DarkCombo();
            cboOpacity.Dock = DockStyle.Fill;
            cboOpacity.Margin = new Padding(0, Theme.S(5), Theme.S(8), Theme.S(5));
            cboOpacity.AddRange(new object[] { "40%", "50%", "60%", "70%", "80%", "90%", "100%" });
            cboOpacity.SelectText(((int)Math.Round(state.Cfg.FloatOpacityIdle * 100 / 10.0) * 10) + "%");
            rOp.Controls.Add(cboOpacity, 1, 0);
            Label lbOp = new Label();
            lbOp.Text = "未激活时半透明；点窗口/输入时全亮";
            lbOp.Dock = DockStyle.Fill;
            lbOp.AutoSize = false;
            lbOp.Margin = new Padding(0);
            lbOp.Font = Theme.FontSmall;
            lbOp.ForeColor = Theme.TextMuted;
            lbOp.TextAlign = ContentAlignment.MiddleLeft;
            rOp.Controls.Add(lbOp, 2, 0);
            rootGrid.Controls.Add(rOp, 0, 5);

            // 按钮
            FlowLayoutPanel r5 = new FlowLayoutPanel();
            r5.Dock = DockStyle.Fill;
            r5.WrapContents = false;
            r5.BackColor = Theme.Bg;
            FlatButton btnSave = new FlatButton();
            btnSave.Text = "保存并应用";
            btnSave.Primary = true;
            btnSave.Size = new Size(Theme.S(120), Theme.S(30));
            btnSave.Margin = new Padding(0, Theme.S(4), Theme.S(8), 0);
            btnSave.Click += delegate(object s, EventArgs e) { Save(); };
            r5.Controls.Add(btnSave);
            FlatButton btnGuide = new FlatButton();
            btnGuide.Text = "新手引导";
            btnGuide.Size = new Size(Theme.S(96), Theme.S(30));
            btnGuide.Margin = new Padding(0, Theme.S(4), Theme.S(8), 0);
            btnGuide.Click += delegate(object s, EventArgs e)
            {
                using (TextInfoForm g = new TextInfoForm("新手引导 · 3 步开始用", "随时可看", Guide.Text))
                { g.ShowDialog(this); }
            };
            r5.Controls.Add(btnGuide);

            FlatButton btnClose = new FlatButton();
            btnClose.Text = "关闭";
            btnClose.Size = new Size(Theme.S(88), Theme.S(30));
            btnClose.Margin = new Padding(0, Theme.S(4), 0, 0);
            btnClose.Click += delegate(object s, EventArgs e) { Close(); };
            r5.Controls.Add(btnClose);
            lblStatus = new Label();
            lblStatus.AutoSize = false;
            lblStatus.Width = Theme.S(150);
            lblStatus.Height = Theme.S(30);
            lblStatus.Margin = new Padding(Theme.S(8), Theme.S(6), 0, 0);
            lblStatus.Font = Theme.FontSmall;
            lblStatus.ForeColor = Theme.TextMuted;
            lblStatus.TextAlign = ContentAlignment.MiddleLeft;
            lblStatus.Text = "";
            r5.Controls.Add(lblStatus);
            rootGrid.Controls.Add(r5, 0, 6);
        }

        private static TableLayoutPanel Row(int cols, int a0, int a1, int a2, int a3)
        {
            TableLayoutPanel t = new TableLayoutPanel();
            t.Dock = DockStyle.Fill;
            t.BackColor = Theme.Bg;
            t.Margin = new Padding(0);
            t.ColumnCount = cols;
            t.RowCount = 1;
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            int[] abs = new int[] { a0, a1, a2, a3 };
            for (int i = 0; i < cols; i++)
            {
                if (abs[i] > 0) t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, abs[i]));
                else t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            }
            return t;
        }

        private static Label Caption(string t)
        {
            Label l = new Label();
            l.Text = t;
            l.Dock = DockStyle.Fill;
            l.AutoSize = false;
            l.Margin = new Padding(0);
            l.ForeColor = Theme.TextSecondary;
            l.Font = Theme.FontBody;
            l.TextAlign = ContentAlignment.MiddleLeft;
            return l;
        }

        private void Save()
        {
            // 热键
            int mods, vk;
            if (!Hotkey.TryParse(hkBox.Value, out mods, out vk))
            {
                lblStatus.ForeColor = Theme.Warn;
                lblStatus.Text = "热键无效：至少包含 Ctrl/Alt/Shift 之一 + 一个按键";
                return;
            }
            string oldHotkey = state.Cfg.Hotkey;
            bool oldAuto = TrayApp.IsAutoStartEnabled();
            bool oldBack = state.Cfg.ReturnToPrevWindow;
            string oldTheme = state.Cfg.ThemeName;

            state.Cfg.Hotkey = hkBox.Value;
            state.Cfg.ThemeName = (cboTheme.SelectedItem == "浅色") ? "light" : "dark";
            state.Cfg.ReturnToPrevWindow = chkBack.Checked;
            string opTxt = cboOpacity.SelectedItem == null ? "60%" : cboOpacity.SelectedItem.ToString().Replace("%", "");
            int opPct = 60;
            int.TryParse(opTxt, out opPct);
            if (opPct < 25) opPct = 25;
            if (opPct > 100) opPct = 100;
            state.Cfg.FloatOpacityIdle = opPct / 100.0;
            if (app != null) app.ApplyFloatIdleOpacity();
            state.Cfg.ShotDir = txtShotDir.Text.Trim();
            string oldShotHk = state.Cfg.ShotHotkey;
            int sm, sv;
            if (!Hotkey.TryParse(hkShot.Value, out sm, out sv))
            {
                lblStatus.ForeColor = Theme.Warn;
                lblStatus.Text = "截图热键无效：至少含 Ctrl/Alt/Shift 之一 + 一个按键";
                return;
            }
            state.Cfg.ShotHotkey = hkShot.Value;

            // 开机自启（失败要提示）
            if (chkAutoStart.Checked != oldAuto)
            {
                bool okSet = TrayApp.TrySetAutoStart(chkAutoStart.Checked);
                if (!okSet)
                {
                    chkAutoStart.Checked = oldAuto;
                    lblStatus.ForeColor = Theme.Warn;
                    lblStatus.Text = "开机自启设置失败（系统策略不允许写注册表启动项）";
                    return;
                }
            }

            // 热键：换绑（失败则回滚）
            if (state.Cfg.Hotkey != oldHotkey)
            {
                string err;
                if (!app.ApplyHotkey(state.Cfg.Hotkey, out err))
                {
                    state.Cfg.Hotkey = oldHotkey;
                    lblStatus.ForeColor = Theme.Warn;
                    lblStatus.Text = "热键注册失败（可能已被其它程序占用）：" + err;
                    hkBox.Value = oldHotkey;
                    return;
                }
            }

            // 截图热键换绑（失败回滚）
            if (state.Cfg.ShotHotkey != oldShotHk)
            {
                string serr;
                if (!app.ApplyShotHotkey(state.Cfg.ShotHotkey, out serr))
                {
                    state.Cfg.ShotHotkey = oldShotHk;
                    lblStatus.ForeColor = Theme.Warn;
                    lblStatus.Text = "截图热键注册失败（可能被占用）：" + serr;
                    hkShot.Value = oldShotHk;
                    return;
                }
            }
            state.Cfg.Save();
            bool themeChanged = state.Cfg.ThemeName != oldTheme;
            if (themeChanged)
            {
                // 换主题会重建所有窗口（包括当前设置窗的宿主），先收起自己再重建，最后关闭
                Hide();
                try { app.ApplyTheme(state.Cfg.ThemeName == "light"); } catch (Exception) { }
                try { Close(); } catch (Exception) { }
                return;
            }
            lblStatus.ForeColor = Theme.Ok;
            lblStatus.Text = "已保存";
        }
    }
}