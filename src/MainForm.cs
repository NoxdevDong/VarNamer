using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace VarNamer
{
    // 作者与项目地址（改这里即可全局生效：说明窗、关于入口、链接按钮）
    public static class Author
    {
        public const string Name = "NoxdevDong";
        public const string ProfileUrl = "https://github.com/NoxdevDong";
        public const string RepoUrl = "https://github.com/NoxdevDong/VarNamer";
        public const string RepoLabel = "github.com/NoxdevDong/VarNamer";
    }

    public class MainForm : ModernForm
    {
        private AppState state;
        private TrayApp app;
        private DarkTextBox input;
        private DataGridView grid;
        private FlatButton btnFloat;
        private Label lblLexStats;
        private FlowLayoutPanel chips;
        private Label lblUnknown;
        private DarkCombo cboPrefix;
        private DarkCombo cboLang;
        private ToggleCheck chkBrief;
        private DarkCombo cboBrief;
        private ToggleCheck chkBool;
        private ToggleCheck chkPlural;
        private Label lblStatus;
        private DarkTextBox addCn;
        private DarkTextBox addEn;
        private int hoverRow = -1;

        private static readonly string[] PrefixItems = new string[]
        { "(无)", "str", "int", "flt", "dbl", "b", "arr", "lst", "dict", "obj", "ptr", "u", "i", "s" };

        [DllImport("imm32.dll")]
        private static extern IntPtr ImmGetContext(IntPtr hWnd);
        [DllImport("imm32.dll")]
        private static extern bool ImmReleaseContext(IntPtr hWnd, IntPtr hIMC);
        [DllImport("imm32.dll", CharSet = CharSet.Unicode)]
        private static extern int ImmGetCompositionString(IntPtr hIMC, int dwIndex, IntPtr lpBuf, int dwBufLen);
        private const int GCS_COMPSTR = 0x0008;

        public static bool IsImeComposing(Control c)
        {
            try
            {
                IntPtr h = ImmGetContext(c.Handle);
                if (h == IntPtr.Zero) return false;
                int len = ImmGetCompositionString(h, GCS_COMPSTR, IntPtr.Zero, 0);
                ImmReleaseContext(c.Handle, h);
                return len > 0;
            }
            catch (Exception) { return false; }
        }

        public MainForm(AppState st, TrayApp owner, string initialText)
        {
            state = st;
            app = owner;
            Text = "VarNamer  中文变量取名";
            Size = new Size(Theme.S(1000), Theme.S(690));
            MinimumSize = new Size(Theme.S(880), Theme.S(580));
            LblSub.Text = "v" + AppVersion.Value + " · 中文变量取名 · 13 种命名风格";

            StartPosition = FormStartPosition.CenterScreen;

            TableLayoutPanel rootGrid = new TableLayoutPanel();
            rootGrid.Dock = DockStyle.Fill;
            rootGrid.BackColor = Theme.Bg;
            rootGrid.ColumnCount = 2;
            rootGrid.RowCount = 1;
            rootGrid.Padding = new Padding(Theme.S(16), Theme.S(4), Theme.S(16), Theme.S(16));
            rootGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62f));
            rootGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38f));
            Content.Controls.Add(rootGrid);

            // ---------- 左：输入 + 结果 ----------
            TableLayoutPanel left = new TableLayoutPanel();
            left.Dock = DockStyle.Fill;
            left.BackColor = Theme.Bg;
            left.ColumnCount = 1;
            left.RowCount = 2;
            left.Margin = new Padding(0, 0, Theme.S(8), 0);
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.S(120)));
            left.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            CardPanel cardInput = new CardPanel();
            cardInput.Title = "中文输入";
            cardInput.Hint = "实时生成 13 种命名风格";
            cardInput.Dock = DockStyle.Fill;
            cardInput.Margin = new Padding(0, 0, 0, Theme.S(10));

            TableLayoutPanel inputStack = new TableLayoutPanel();
            inputStack.Dock = DockStyle.Fill;
            inputStack.BackColor = Theme.Surface;
            inputStack.Margin = new Padding(0);
            inputStack.ColumnCount = 1;
            inputStack.RowCount = 2;
            inputStack.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.S(48)));
            inputStack.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            TableLayoutPanel inputRow = new TableLayoutPanel();
            inputRow.Dock = DockStyle.Fill;
            inputRow.BackColor = Theme.Surface;
            inputRow.Margin = new Padding(0);
            inputRow.ColumnCount = 2;
            inputRow.RowCount = 1;
            inputRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            inputRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            inputRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Theme.S(78)));

            input = new DarkTextBox();
            input.Dock = DockStyle.Fill;
            input.Margin = new Padding(0, 0, Theme.S(8), 0);
            input.InnerBox.TextChanged += delegate(object s, EventArgs e) { Refresh_(); };
            input.InnerBox.KeyDown += OnInputKeyDown;
            inputRow.Controls.Add(input, 0, 0);

            FlatButton btnClear = new FlatButton();
            btnClear.Text = "清空";
            btnClear.Dock = DockStyle.Fill;
            btnClear.Margin = new Padding(0, Theme.S(1), 0, Theme.S(1));
            ToolTip ct = new ToolTip();
            ct.SetToolTip(btnClear, "清空输入框");
            btnClear.Click += delegate(object s, EventArgs e)
            {
                input.Text = "";
                input.InnerBox.Focus();
                SetStatus("已清空输入", Theme.TextMuted);
            };
            inputRow.Controls.Add(btnClear, 1, 0);
            inputStack.Controls.Add(inputRow, 0, 0);

            Label lblInputHint = new Label();
            lblInputHint.Dock = DockStyle.Fill;
            lblInputHint.AutoSize = false;
            lblInputHint.Margin = new Padding(0);
            lblInputHint.Font = Theme.FontSmall;
            lblInputHint.ForeColor = Theme.TextMuted;
            lblInputHint.TextAlign = ContentAlignment.MiddleLeft;
            lblInputHint.Text = "回车复制第一行 · 双击结果行复制";
            inputStack.Controls.Add(lblInputHint, 0, 1);
            cardInput.Controls.Add(inputStack);
            left.Controls.Add(cardInput, 0, 0);

            CardPanel cardResult = new CardPanel();
            cardResult.Title = "命名结果";
            cardResult.Hint = "双击复制";
            cardResult.Dock = DockStyle.Fill;
            cardResult.Margin = new Padding(0);
            cardResult.Padding = new Padding(Theme.S(12), Theme.S(32), Theme.S(12), Theme.S(12));
            grid = new DataGridView();
            grid.Dock = DockStyle.Fill;
            Theme.StyleGrid(grid, true);
            grid.Columns.Add(Theme.TextColumn("命名风格", false, Theme.TextSecondary));
            grid.Columns.Add(Theme.TextColumn("结果", true, Theme.TextPrimary));
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.Columns[0].FillWeight = 34f;
            grid.Columns[1].FillWeight = 66f;
            grid.CellDoubleClick += delegate(object s, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex >= 0 && grid.Rows[e.RowIndex].Tag is int) { grid.Rows[e.RowIndex].Selected = true; CopySelected(); }
            };
            grid.CellMouseEnter += delegate(object s, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex < 0 || e.RowIndex == hoverRow) return;
                if (hoverRow >= 0 && hoverRow < grid.Rows.Count) grid.Rows[hoverRow].DefaultCellStyle.BackColor = RowBase(hoverRow);
                hoverRow = e.RowIndex;
                if (grid.Rows[hoverRow].Tag is int) grid.Rows[hoverRow].DefaultCellStyle.BackColor = Theme.RowHover;   // 标题行不响应悬停
            };
            grid.CellMouseLeave += delegate(object s, DataGridViewCellEventArgs e)
            {
                if (hoverRow >= 0 && hoverRow < grid.Rows.Count) grid.Rows[hoverRow].DefaultCellStyle.BackColor = RowBase(hoverRow);
                hoverRow = -1;
            };
            grid.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; CopySelected(); }
            };
            cardResult.Controls.Add(grid);
            left.Controls.Add(cardResult, 0, 1);

            // ---------- 右：分词 + 选项 ----------
            TableLayoutPanel right = new TableLayoutPanel();
            right.Dock = DockStyle.Fill;
            right.BackColor = Theme.Bg;
            right.ColumnCount = 1;
            right.RowCount = 2;
            right.Margin = new Padding(Theme.S(8), 0, 0, 0);
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 40f));
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 60f));

            CardPanel cardTokens = new CardPanel();
            cardTokens.Title = "分词与释义";
            cardTokens.Hint = "点词块换同义词";
            cardTokens.Dock = DockStyle.Fill;
            cardTokens.Margin = new Padding(0, 0, 0, Theme.S(10));
            chips = new FlowLayoutPanel();
            chips.Dock = DockStyle.Fill;
            chips.AutoScroll = true;
            chips.BackColor = Theme.Surface;
            chips.FlowDirection = FlowDirection.LeftToRight;
            chips.WrapContents = true;
            chips.Margin = new Padding(0);
            cardTokens.Controls.Add(chips);
            lblUnknown = new Label();
            lblUnknown.Dock = DockStyle.Bottom;
            lblUnknown.Height = Theme.S(22);
            lblUnknown.ForeColor = Theme.Warn;
            lblUnknown.Font = Theme.FontSmall;
            lblUnknown.BackColor = Theme.Surface;
            lblUnknown.TextAlign = ContentAlignment.MiddleLeft;
            lblUnknown.AutoEllipsis = true;
            lblUnknown.Text = "未识别字：无";
            lblUnknown.Cursor = Cursors.Default;
            lblUnknown.Click += delegate(object s, EventArgs e)
            {
                string u = lblUnknown.Tag as string;
                if (u != null) { addCn.Text = u; addEn.InnerBox.Focus(); }
            };
            cardTokens.Controls.Add(lblUnknown);
            right.Controls.Add(cardTokens, 0, 0);

            TableLayoutPanel tokenBottom = new TableLayoutPanel();
            tokenBottom.Dock = DockStyle.Bottom;
            tokenBottom.Height = Theme.S(36);
            tokenBottom.BackColor = Theme.Surface;
            tokenBottom.ColumnCount = 4;
            tokenBottom.RowCount = 1;
            tokenBottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Theme.S(56)));
            tokenBottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40f));
            tokenBottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60f));
            tokenBottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Theme.S(64)));
            Label lbAdd = new Label();
            lbAdd.Text = "加词库";
            lbAdd.AutoSize = false;
            lbAdd.Margin = new Padding(0);
            lbAdd.ForeColor = Theme.TextMuted;
            lbAdd.Font = Theme.FontSmall;
            lbAdd.TextAlign = ContentAlignment.MiddleLeft;
            lbAdd.Dock = DockStyle.Fill;
            tokenBottom.Controls.Add(lbAdd, 0, 0);
            addCn = new DarkTextBox();
            addCn.Dock = DockStyle.Fill;
            addCn.Height = Theme.S(30);
            addCn.Margin = new Padding(0, Theme.S(3), Theme.S(6), Theme.S(3));
            addCn.InnerBox.Font = Theme.FontBody;
            addCn.InnerBox.KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; AddWord(); } };
            tokenBottom.Controls.Add(addCn, 1, 0);
            addEn = new DarkTextBox();
            addEn.Dock = DockStyle.Fill;
            addEn.Height = Theme.S(30);
            addEn.Margin = new Padding(0, Theme.S(3), Theme.S(6), Theme.S(3));
            addEn.InnerBox.Font = Theme.FontMonoSm;
            addEn.InnerBox.KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; AddWord(); } };
            tokenBottom.Controls.Add(addEn, 2, 0);
            FlatButton btnAdd = new FlatButton();
            btnAdd.Text = "加入";
            btnAdd.Dock = DockStyle.Fill;
            btnAdd.Margin = new Padding(0, Theme.S(3), 0, Theme.S(3));
            btnAdd.Click += delegate(object s, EventArgs e) { AddWord(); };
            tokenBottom.Controls.Add(btnAdd, 3, 0);
            cardTokens.Controls.Add(tokenBottom);

            CardPanel cardOpt = new CardPanel();
            cardOpt.Title = "选项";
            cardOpt.Hint = "改动即时生效";
            cardOpt.Dock = DockStyle.Fill;
            cardOpt.Margin = new Padding(0);

            TableLayoutPanel opt = new TableLayoutPanel();
            opt.Dock = DockStyle.Fill;
            opt.BackColor = Theme.Surface;
            opt.Margin = new Padding(0);
            opt.ColumnCount = 1;
            opt.RowCount = 10;
            // 行高自适应：以控件/字体的实际高度排版，任何字体或 DPI 下都不会压行
            opt.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.S(26)));   // 分组：命名规则
            opt.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.S(36)));   // 类型前缀 / 简短长度
            opt.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.S(36)));   // 目标语言 / 简洁模式
            opt.RowStyles.Add(new RowStyle(SizeType.AutoSize));                // 布尔前缀 / 复数
            opt.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.S(26)));   // 分组：操作
            opt.RowStyles.Add(new RowStyle(SizeType.AutoSize));                // 复制选中 / 复制全部 / 设置
            opt.RowStyles.Add(new RowStyle(SizeType.AutoSize));                // 悬浮窗 / 词库管理 / 说明
            opt.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.S(26)));   // 分组：词库
            opt.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.S(34)));   // 词库统计
            opt.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));           // 状态/提示

            TableLayoutPanel row1 = NewRow(4, 46f, 54f, 46f, 54f);
            row1.MinimumSize = new Size(0, Theme.S(36));
            row1.Controls.Add(NewCaption("类型前缀"), 0, 0);
            cboPrefix = NewCombo(PrefixItems);
            cboPrefix.SelectedIndex = 0;
            cboPrefix.SelectedIndexChanged += delegate(object s, EventArgs e)
            {
                string v = cboPrefix.SelectedItem as string;
                if (v == null || v == "(无)") v = "";
                state.Cfg.TypePrefix = v;
                state.ApplyOptions();
                Refresh_();
            };
            row1.Controls.Add(cboPrefix, 1, 0);
            row1.Controls.Add(NewCaption("简短长度"), 2, 0);
            string[] briefItems = new string[] { "3", "4", "5", "6" };
            cboBrief = NewCombo(briefItems);
            cboBrief.SelectText(state.Cfg.BriefLength.ToString(CultureInfo.InvariantCulture));
            if (cboBrief.SelectedIndex < 0) cboBrief.SelectedIndex = 1;
            cboBrief.SelectedIndexChanged += delegate(object s, EventArgs e)
            {
                int v;
                if (int.TryParse(cboBrief.SelectedItem as string, out v))
                {
                    state.Cfg.BriefLength = v;
                    state.ApplyOptions();
                    Refresh_();
                }
            };
            row1.Controls.Add(cboBrief, 3, 0);
            opt.Controls.Add(NewGroupCaption("命名规则"), 0, 0);
            opt.Controls.Add(row1, 0, 1);

            TableLayoutPanel rowLang = new TableLayoutPanel();
            rowLang.Dock = DockStyle.Fill;
            rowLang.BackColor = Theme.Surface;
            rowLang.Margin = new Padding(0);
            rowLang.ColumnCount = 3;
            rowLang.RowCount = 1;
            rowLang.MinimumSize = new Size(0, Theme.S(34));
            rowLang.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            rowLang.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Theme.S(74)));
            rowLang.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52f));
            rowLang.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48f));
            rowLang.Controls.Add(NewCaption("目标语言"), 0, 0);
            cboLang = new DarkCombo();
            cboLang.Dock = DockStyle.Fill;
            cboLang.Margin = new Padding(0, Theme.S(5), Theme.S(12), Theme.S(5));
            cboLang.AddRange(LanguagePreset.Names);
            cboLang.SelectText(state.Cfg.Language);
            // 迁移配置里可能出现的旧值
            if (cboLang.SelectedIndex < 0) cboLang.SelectedIndex = 0;
            cboLang.SelectedIndexChanged += delegate(object s, EventArgs e)
            {
                state.Cfg.Language = cboLang.SelectedItem;
                state.Cfg.Save();
                Refresh_();
            };
            rowLang.Controls.Add(cboLang, 1, 0);

            chkBrief = new ToggleCheck();
            chkBrief.Text = "简洁模式";
            chkBrief.Checked = state.Cfg.BriefMode;
            chkBrief.Margin = new Padding(0, Theme.S(4), Theme.S(14), 0);
            chkBrief.Size = chkBrief.GetPreferredSize(Size.Empty);
            chkBrief.CheckedChanged += delegate(object s, EventArgs e)
            {
                state.Cfg.BriefMode = chkBrief.Checked;
                state.Cfg.Save();
                Refresh_();
            };
            rowLang.Controls.Add(chkBrief, 2, 0);

            opt.Controls.Add(rowLang, 0, 2);

            FlowLayoutPanel row2 = new FlowLayoutPanel();
            row2.Dock = DockStyle.Fill;
            row2.BackColor = Theme.Surface;
            row2.MinimumSize = new Size(0, Theme.S(34));
            row2.AutoSize = true;
            row2.WrapContents = false;
            chkBool = new ToggleCheck();
            chkBool.Text = "布尔前缀 is";
            chkBool.Checked = state.Cfg.BooleanPrefix;
            chkBool.Margin = new Padding(0, Theme.S(4), Theme.S(22), 0);
            chkBool.Size = chkBool.GetPreferredSize(Size.Empty);
            chkBool.CheckedChanged += delegate(object s, EventArgs e)
            {
                state.Cfg.BooleanPrefix = chkBool.Checked;
                state.ApplyOptions();
                Refresh_();
            };
            row2.Controls.Add(chkBool);
            chkPlural = new ToggleCheck();
            chkPlural.Text = "集合语义复数";
            chkPlural.Checked = state.Cfg.PluralizeTriggers;
            chkPlural.Margin = new Padding(0, Theme.S(4), 0, 0);
            chkPlural.Size = chkPlural.GetPreferredSize(Size.Empty);
            chkPlural.CheckedChanged += delegate(object s, EventArgs e)
            {
                state.Cfg.PluralizeTriggers = chkPlural.Checked;
                state.ApplyOptions();
                Refresh_();
            };
            row2.Controls.Add(chkPlural);
            opt.Controls.Add(row2, 0, 3);

            FlowLayoutPanel row3 = new FlowLayoutPanel();
            row3.Dock = DockStyle.Fill;
            row3.BackColor = Theme.Surface;
            row3.MinimumSize = new Size(0, Theme.S(40));
            row3.AutoSize = true;
            row3.WrapContents = false;
            FlatButton btnCopySel = NewAction("复制选中", true, 0);
            btnCopySel.Click += delegate(object s, EventArgs e) { CopySelected(); };
            row3.Controls.Add(btnCopySel);
            FlatButton btnCopyAll = NewAction("复制全部", false, 8);
            btnCopyAll.Click += delegate(object s, EventArgs e) { CopyAll(); };
            row3.Controls.Add(btnCopyAll);
            FlatButton btnSettings = NewAction("设置", false, 8);
            btnSettings.Click += delegate(object s, EventArgs e) { app.ShowSettings(); };
            row3.Controls.Add(btnSettings);
            opt.Controls.Add(NewGroupCaption("操作"), 0, 4);
            opt.Controls.Add(row3, 0, 5);

            FlowLayoutPanel row4 = new FlowLayoutPanel();
            row4.Dock = DockStyle.Fill;
            row4.BackColor = Theme.Surface;
            row4.MinimumSize = new Size(0, Theme.S(40));
            row4.AutoSize = true;
            row4.WrapContents = false;
            btnFloat = NewAction("隐藏悬浮窗", false, 0);   // 文字随状态变化（默认启动时悬浮窗是显示的）
            btnFloat.Click += delegate(object s, EventArgs e)
            {
                app.ToggleFloater();
                UpdateFloatButton();
            };
            row4.Controls.Add(btnFloat);
            FlatButton btnLex = NewAction("词库管理", false, 8);
            btnLex.Click += delegate(object s, EventArgs e)
            {
                using (LexiconForm f = new LexiconForm(state)) { f.ShowDialog(this); }
                UpdateLexStats();
                Refresh_();
            };
            row4.Controls.Add(btnLex);
            FlatButton btnHelp = NewAction("说明", false, 8);
            btnHelp.Click += delegate(object s, EventArgs e)
            {
                // 用应用内说明窗（自绘）：避免系统 MessageBox 的提示音，观感也统一
                using (TextInfoForm f = new TextInfoForm("使用说明", "中文变量取名 · 13 种风格", HelpText(), Author.RepoUrl, "GitHub · " + Author.Name))
                { f.ShowDialog(this); }
            };
            row4.Controls.Add(btnHelp);
            opt.Controls.Add(row4, 0, 6);
            if (app != null) app.FloatVisibilityChanged += OnFloatVisibilityChanged;
            UpdateFloatButton();
            UpdateLexStats();      // 入场就同步一次：悬浮窗默认是显示的 → 按钮应为“隐藏悬浮窗”且蓝色高亮

            lblStatus = new Label();
            lblStatus.Dock = DockStyle.Fill;
            lblStatus.ForeColor = Theme.TextMuted;
            lblStatus.Font = Theme.FontSmall;
            lblStatus.TextAlign = ContentAlignment.TopLeft;
            lblStatus.Padding = new Padding(0, Theme.S(8), 0, 0);
            lblStatus.AutoEllipsis = true;
            lblStatus.Text = "";   // 空着：只在有操作反馈时出现（复制成功/失败、加词库等）
            opt.Controls.Add(NewGroupCaption("词库"), 0, 7);
            opt.Controls.Add(BuildLexStatsRow(), 0, 8);
            opt.Controls.Add(lblStatus, 0, 9);
            cardOpt.Controls.Add(opt);
            right.Controls.Add(cardOpt, 0, 1);

            rootGrid.Controls.Add(left, 0, 0);
            rootGrid.Controls.Add(right, 1, 0);
            UpdateLexStats();         // 统计行此时已建好，放在最后同步

            KeyPreview = true;
            KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.Control && e.KeyCode == Keys.C) CopySelected();
            };
            FormClosing += OnClosing;

            if (initialText != null && initialText.Length > 0) input.Text = initialText;
            Refresh_();
        }

        private static TableLayoutPanel NewRow(int cols, params float[] percents)
        {
            TableLayoutPanel t = new TableLayoutPanel();
            t.Dock = DockStyle.Fill;
            t.BackColor = Theme.Surface;
            t.Margin = new Padding(0);
            t.ColumnCount = cols;
            t.RowCount = 1;
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            for (int i = 0; i < cols; i++)
                t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, i < percents.Length ? percents[i] : 100f / cols));
            return t;
        }

        private static Label NewCaption(string text)
        {
            Label l = new Label();
            l.Text = text;
            l.Dock = DockStyle.Fill;
            l.AutoSize = false;
            l.Margin = new Padding(0);
            l.ForeColor = Theme.TextSecondary;
            l.Font = Theme.FontBody;
            l.TextAlign = ContentAlignment.MiddleLeft;
            return l;
        }

        // 分组小标题：弱化色 + 小字，用来给选项分块
        private static Label NewGroupCaption(string text)
        {
            Label l = new Label();
            l.Text = text;
            l.Dock = DockStyle.Fill;
            l.AutoSize = false;
            l.Margin = new Padding(0);
            l.Font = Theme.FontSmall;
            l.ForeColor = Theme.TextMuted;
            l.TextAlign = ContentAlignment.BottomLeft;
            l.Padding = new Padding(0, 0, 0, Theme.S(2));
            return l;
        }

        // 词库统计行：内置 / 我的词条 + 打开词库目录
        private Control BuildLexStatsRow()
        {
            TableLayoutPanel row = new TableLayoutPanel();
            row.Dock = DockStyle.Fill;
            row.BackColor = Theme.Surface;
            row.Margin = new Padding(0);
            row.ColumnCount = 2;
            row.RowCount = 1;
            row.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Theme.S(112)));

            lblLexStats = new Label();
            lblLexStats.Dock = DockStyle.Fill;
            lblLexStats.AutoSize = false;
            lblLexStats.Margin = new Padding(0);
            lblLexStats.Font = Theme.FontSmall;
            lblLexStats.ForeColor = Theme.TextSecondary;
            lblLexStats.TextAlign = ContentAlignment.MiddleLeft;
            lblLexStats.Text = "统计中…";
            row.Controls.Add(lblLexStats, 0, 0);

            FlatButton btnOpenDict = new FlatButton();
            btnOpenDict.Text = "打开词库目录";
            btnOpenDict.Font = Theme.FontSmall;
            btnOpenDict.Dock = DockStyle.Fill;
            btnOpenDict.Margin = new Padding(0, Theme.S(3), 0, Theme.S(3));
            btnOpenDict.Click += delegate(object s, EventArgs e)
            {
                try
                {
                    string dir = AutoDict.DictFolderPath;
                    if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
                    System.Diagnostics.Process.Start("explorer.exe", dir);
                }
                catch (Exception ex) { SetStatus("打开目录失败：" + ex.Message, Theme.Warn); }
            };
            row.Controls.Add(btnOpenDict, 1, 0);
            return row;
        }

        // 词库条数：只在需要时刷新（避免每次按键都读文件）
        private static int builtinWords = -1;

        private void UpdateLexStats()
        {
            if (lblLexStats == null || lblLexStats.IsDisposed) return;
            try
            {
                if (builtinWords < 0) builtinWords = Lexicon.Create().WordCount;
                int mine = state.LoadUserDictEntries().Count;
                lblLexStats.Text = "内置 " + builtinWords + " 条 · 我的词条 " + mine + " 条";
            }
            catch (Exception) { lblLexStats.Text = ""; }
        }

        private static FlatButton NewAction(string text, bool primary, int leftMargin)
        {
            FlatButton b = new FlatButton();
            b.Text = text;
            b.Primary = primary;
            b.Size = new Size(Theme.S(primary ? 96 : 84), Theme.S(32));
            b.Margin = new Padding(Theme.S(leftMargin), Theme.S(2), 0, 0);
            return b;
        }

        private static DarkCombo NewCombo(string[] items)
        {
            DarkCombo c = new DarkCombo();
            c.Dock = DockStyle.Fill;
            c.Margin = new Padding(0, Theme.S(5), Theme.S(12), Theme.S(5));
            c.AddRange(items);
            return c;
        }

        private void OnClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                return;
            }
            state.Cfg.Save();
        }

        // 隔行淡底：还原行底色时要用它，否则会把斑马纹洗掉
        // 悬浮窗按钮：显示中 = 高亮，文字提示“点一下会隐藏”
        private void UpdateFloatButton()
        {
            if (btnFloat == null || btnFloat.IsDisposed) return;
            bool vis = app != null && app.FloaterVisible;
            string t = vis ? "隐藏悬浮窗" : "显示悬浮窗";
            if (btnFloat.Text != t) btnFloat.Text = t;
            if (btnFloat.Primary != vis) { btnFloat.Primary = vis; btnFloat.Invalidate(); }
        }

        private void OnFloatVisibilityChanged(object sender, EventArgs e) { UpdateFloatButton(); }

        protected override void Dispose(bool disposing)
        {
            if (disposing && app != null) app.FloatVisibilityChanged -= OnFloatVisibilityChanged;
            base.Dispose(disposing);
        }

        private static Color RowBase(int rowIndex)
        {
            return (rowIndex % 2 == 0) ? Theme.Surface : Theme.RowStripe;
        }

        private void OnInputKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;
            if (IsImeComposing(input.InnerBox)) return;   // 输入法选字回车不触发复制
            e.SuppressKeyPress = true;
            CopySelected();
        }

        private void AddWord()
        {
            string cn = addCn.Text.Trim();
            string en = addEn.Text.Trim();
            if (cn.Length == 0 || en.Length == 0) { SetStatus("请同时填写中文与英文", Theme.Warn); return; }
            state.AddUserEntry(cn, en);
            addCn.Text = "";
            addEn.Text = "";
            SetStatus("已加入词库：" + cn + " = " + en, Theme.Ok);
            UpdateLexStats();
            Refresh_();
        }

        private Tween statusTw;

        // 状态提示：立即显示，再在 1 秒内淡回灰色（比一直红/绿更耐看）
        private void SetStatus(string s, Color c)
        {
            lblStatus.Text = s;
            lblStatus.ForeColor = c;
            if (statusTw != null) statusTw.Stop();
            if (c == Theme.TextMuted) return;
            Color from = c;
            statusTw = Tween.RunDelayed(500, 700, delegate(double k)
            {
                if (lblStatus.IsDisposed) return;
                lblStatus.ForeColor = Anim.Lerp(from, Theme.TextMuted, Curves.EasyEase(k));
            }, null);
        }

        private void Refresh_()
        {
            state.LastInput = input.Text;
            NameResult r = Namer.Create(input.Text, state.Lex, state.Opt);
            state.Current = r;

            grid.SuspendLayout();
            grid.Rows.Clear();
            string lang = state.Cfg.Language;
            if (state.Cfg.BriefMode && !LanguagePreset.IsAll(lang))
            {
                // 简洁模式：新手只关心 变量名 / 类名 / 常量名
                List<KeyValuePair<int, string>> rows = LanguagePreset.BriefRows(lang);
                for (int i = 0; i < rows.Count; i++)
                {
                    int idx = rows[i].Key;
                    if (idx < 0 || idx >= r.Lines.Count) continue;
                    grid.Rows.Add(rows[i].Value + "（" + r.Lines[idx].Label.Replace("全称 ", "") + "）", r.Lines[idx].Value);
                    grid.Rows[grid.Rows.Count - 1].Tag = idx;
                }
            }
            else
            {
                // 13 种风格按「全称 / 简短 / 缩写」分组，插入不可复制的分组标题行
                string lastGroup = null;
                for (int i = 0; i < r.Lines.Count; i++)
                {
                    string grp = GroupOf(r.Lines[i].Label);
                    if (grp != null && grp != lastGroup) { AddSectionRow(grp); lastGroup = grp; }
                    grid.Rows.Add(r.Lines[i].Label, r.Lines[i].Value);
                    grid.Rows[grid.Rows.Count - 1].Tag = i;
                }
            }
            SelectFirstCopyableRow();
            hoverRow = -1;
            grid.ResumeLayout();

            chips.SuspendLayout();
            chips.Controls.Clear();
            hoverRow = -1;
            for (int i = 0; i < r.Tokens.Count; i++)
            {
                Token t = r.Tokens[i];
                if (t.Kind != TokenKind.Word && t.Kind != TokenKind.Literal) continue;
                FlatButton b = new FlatButton();
                b.Pill = true;
                b.Font = Theme.FontBody;
                if (t.Kind == TokenKind.Literal) b.Text = "[保留] " + t.Source;
                else
                {
                    b.Text = t.Source + " = " + t.CurrentCandidate();
                    if (t.Candidates.Count > 1) b.Text += "  ▾";
                }
                Size sz = TextRenderer.MeasureText(b.Text, b.Font);
                b.Size = new Size(sz.Width + Theme.S(26), Theme.S(30));
                b.Margin = new Padding(0, Theme.S(2), Theme.S(8), Theme.S(2));
                b.Tag = t;
                if (t.Candidates.Count > 1)
                {
                    b.Click += delegate(object s, EventArgs e)
                    {
                        Button bb = (Button)s;
                        Token tk = (Token)bb.Tag;
                        tk.CandidateIndex = (tk.CandidateIndex + 1) % tk.Candidates.Count;
                        Refresh_();
                    };
                }
                chips.Controls.Add(b);
            }
            chips.ResumeLayout();

            if (r.Unknowns.Count > 0)
            {
                UnknownLog.Record(r.Unknowns);                     // 自动累积未收录词，便于一次性补齐
                if (Translation.Cfg.Enabled) StartOnlineTranslate(r.Unknowns);
            }

            if (r.Unknowns.Count > 0)
            {
                lblUnknown.Text = "未识别：" + string.Join("  ", r.Unknowns.ToArray())
                    + (Translation.Cfg.Enabled ? "　（联网翻译中…）" : "　（点此填入下方加词库）");
                lblUnknown.Tag = r.Unknowns[0];
                lblUnknown.Cursor = Cursors.Hand;
            }
            else
            {
                string note = (r.BooleanDetected && !state.Opt.BooleanPrefix)
                    ? "　（『是否』已按设置忽略，布尔前缀关闭）" : "";
                lblUnknown.Text = "未识别：无" + note;
                lblUnknown.Tag = null;
                lblUnknown.Cursor = Cursors.Default;
            }
        }

        private bool translating;
        private void StartOnlineTranslate(List<string> unknowns)
        {
            if (translating) return;
            translating = true;
            List<string> list = new List<string>(unknowns);
            System.Threading.ThreadPool.QueueUserWorkItem(delegate(object o)
            {
                List<string[]> found = new List<string[]>();
                for (int i = 0; i < list.Count; i++)
                {
                    string en = Translation.Translate(list[i]);
                    if (!string.IsNullOrEmpty(en)) found.Add(new string[] { list[i], en });
                }
                try
                {
                    BeginInvoke((MethodInvoker)delegate()
                    {
                        translating = false;
                        for (int i = 0; i < found.Count; i++)
                        {
                            if (Translation.Cfg.AutoAddToDict) state.AddUserEntry(found[i][0], found[i][1]);
                            else state.Lex.Add(found[i][0], found[i][1]);
                        }
                        if (found.Count > 0)
                        {
                            state.Lex.Rebuild();
                            SetStatus("在线翻译并写入词库 " + found.Count + " 个词", Theme.Ok);
                            Refresh_();
                        }
                        else
                        {
                            SetStatus("在线翻译未取到结果" + (Translation.LastError.Length > 0 ? "（" + Translation.LastError + "）" : ""), Theme.Warn);
                        }
                    });
                }
                catch (Exception) { translating = false; }
            });
        }

        private void CopySelected()
        {
            string val = null;
            if (grid.SelectedRows.Count > 0)
            {
                object tag = grid.SelectedRows[0].Tag;
                if (!(tag is int)) { SetStatus("这是分组标题行，请选具体风格", Theme.TextMuted); return; }
                val = GetLineValue((int)tag);
            }
            if (val == null || val.Length == 0) SetStatus("没有可复制的结果", Theme.Warn);
            else if (Clip.SetText(val))
            {
                SetStatus("已复制：" + val, Theme.Ok);
                int ri = grid.SelectedRows.Count > 0 ? grid.SelectedRows[0].Index : -1;
                FlashRow(ri);
            }
            else SetStatus("复制失败（剪贴板被占用，请重试）", Theme.Warn);
        }

        // 复制成功时让那一行“亮一下”，给出明确反馈
        private void FlashRow(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= grid.Rows.Count) return;
            DataGridViewRow row = grid.Rows[rowIndex];
            Tween.Run(Theme.DurSlow, delegate(double k)
            {
                if (grid.IsDisposed || rowIndex >= grid.Rows.Count) return;
                Color fill = Anim.Lerp(RowBase(rowIndex), Theme.AccentSoft, 1.0 - Curves.DecelerateMax(k));
                row.DefaultCellStyle.BackColor = fill;
            },
            delegate()
            {
                if (grid.IsDisposed || rowIndex >= grid.Rows.Count) return;
                row.DefaultCellStyle.BackColor = RowBase(rowIndex);
            });
        }

        // 风格分组名（标题行显示用）
        private static string GroupOf(string label)
        {
            if (string.IsNullOrEmpty(label)) return null;
            if (label.StartsWith("全称")) return "全称 · 完整单词";
            if (label.StartsWith("简短")) return "简短 · 每词截断";
            if (label.StartsWith("缩写")) return "缩写 · 压缩写法";
            return null;
        }

        // 分组标题行：不参与复制（Tag 留空），不响应悬停
        private void AddSectionRow(string title)
        {
            int ri = grid.Rows.Add("", "");
            DataGridViewRow row = grid.Rows[ri];
            row.Tag = null;
            row.Height = Theme.S(24);
            row.Cells[0].Value = title;
            row.Cells[0].Style.ForeColor = Theme.TextMuted;
            row.Cells[0].Style.Font = Theme.FontSmall;
            for (int c = 0; c < row.Cells.Count; c++)
            {
                row.Cells[c].Style.BackColor = Theme.Surface;
                row.Cells[c].Style.SelectionBackColor = Theme.Surface;
                row.Cells[c].Style.SelectionForeColor = Theme.TextMuted;
            }
        }

        private void SelectFirstCopyableRow()
        {
            for (int i = 0; i < grid.Rows.Count; i++)
                if (grid.Rows[i].Tag is int) { grid.Rows[i].Selected = true; return; }
        }

        private string GetLineValue(int idx)
        {
            if (state.Current == null || idx < 0 || idx >= state.Current.Lines.Count) return null;
            return state.Current.Lines[idx].Value;
        }

        private void CopyAll()
        {
            if (state.Current == null || state.Current.Lines.Count == 0) { SetStatus("没有可复制的结果", Theme.Warn); return; }
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < state.Current.Lines.Count; i++)
            {
                sb.Append(state.Current.Lines[i].Label);
                sb.Append(" = ");
                sb.AppendLine(state.Current.Lines[i].Value);
            }
            if (Clip.SetText(sb.ToString())) SetStatus("已复制全部 " + state.Current.Lines.Count + " 条", Theme.Ok);
            else SetStatus("复制失败（剪贴板被占用，请重试）", Theme.Warn);
        }

        public static string HelpText()
        {
            return "VarNamer —— 输入中文，得到规范变量名" + "\r\n\r\n" +
                "1) 输入框敲中文，13 种命名风格实时生成。" + "\r\n" +
                "2) 双击结果行 或 回车 = 复制该行；点结果列表后回车同样复制。" + "\r\n" +
                "3) 中文输入法正在选字时按回车不会误触发复制。" + "\r\n" +
                "4) 分词区点词块可切换同义词（如 成绩 = score → grade）。" + "\r\n" +
                "5) 未识别的字会列出，用「加词库」补映射后立即生效。" + "\r\n" +
                "6) 全局热键 Ctrl+Alt+V：呼出悬浮窗并自动转换剪贴板内容。" + "\r\n" +
                "7) 关闭主窗口只是最小化到托盘，退出走托盘菜单。" + "\r\n" +
                "8) 词库不够用时：在「词库管理」里搜索/新增词条，或导入 txt 词库文件。" + "\r\n" +
                "9) 「目标语言」决定简洁模式下变量名/类名/常量名的风格：" + "\r\n" +
                "   Python/C/C++/SQL → snake_case；Java/C#/JS/Go → camelCase；CSS → kebab-case。" + "\r\n" +
                "10) 托盘图标右键菜单 → 「设置…」：热键自定义 / 深浅主题 / 开机自启 / 截图热键 / 悬浮窗透明度。" + "\r\n" +
                "11) 截图：Ctrl+Alt+A → 选区 → 标注（矩形/箭头/画笔/高亮/马赛克/文字）→ 复制 / 保存 / 钉在桌面。" + "\r\n" +
                "----------------------------------------------------------------" + "\r\n" +
                "作者：" + Author.Name + "　（点下方「GitHub」按钮打开项目主页）" + "\r\n" +
                "项目地址：" + Author.RepoUrl + "\r\n" +
                "反馈问题 / 想要新功能：到仓库提 issue 即可。";
        }
    }

    public static class StateExtensions
    {
        public static int CurrentLineCount(this AppState s)
        {
            if (s == null || s.Current == null) return 0;
            return s.Current.Lines.Count;
        }
    }
}
