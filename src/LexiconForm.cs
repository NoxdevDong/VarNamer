using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace VarNamer
{
    public class LexiconForm : ModernForm
    {
        private AppState state;
        private DataGridView grid;
        private DarkTextBox txtCn;
        private DarkTextBox txtEn;
        private Label lblStatus;
        private List<string[]> entries;
        private DarkTextBox txtSearch;
        private Label lblCount, lblEmpty;
        private Panel pageHost;
        private TableLayoutPanel pageList, pageIO, pageTrans;
        private TabStrip tabs;
        private Badge badge;
        private Label lbEngine, lblTrKey, lblTrAuto;
        private int activeTab;
        private bool suppressReload;

        public LexiconForm(AppState st)
        {
            state = st;
            Text = "VarNamer  词库管理";
            Size = new Size(Theme.S(860), Theme.S(720));
            MinimumSize = new Size(Theme.S(760), Theme.S(600));
            LblSub.Text = "v" + AppVersion.Value + " · 用户词库 · 优先于内置词库";
            StartPosition = FormStartPosition.CenterParent;

            TableLayoutPanel rootGrid = new TableLayoutPanel();
            rootGrid.Dock = DockStyle.Fill;
            rootGrid.BackColor = Theme.Bg;
            rootGrid.ColumnCount = 1;
            rootGrid.RowCount = 4;
            rootGrid.Padding = new Padding(Theme.S(16), Theme.S(12), Theme.S(16), Theme.S(14));
            rootGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.S(40)));   // 标签条
            rootGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));           // 页面
            rootGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.S(26)));   // 状态
            rootGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.S(46)));   // 底部按钮
            Content.Controls.Add(rootGrid);

            // ---------- 标签条（下划线式，Fluent 形态）----------
            tabs = new TabStrip();
            tabs.Dock = DockStyle.Fill;
            tabs.BackColor = Theme.Bg;
            tabs.Items = new string[] { "词条", "导入导出", "在线翻译" };
            tabs.SelectedIndexChanged += delegate(object s, EventArgs e) { SwitchTab(tabs.SelectedIndex); };
            rootGrid.Controls.Add(tabs, 0, 0);

            // ---------- 三个页面：同位置叠放，靠 Visible 切换 ----------
            pageHost = new Panel();
            pageHost.Dock = DockStyle.Fill;
            pageHost.BackColor = Theme.Bg;
            pageHost.Margin = new Padding(0);

            pageList = new TableLayoutPanel();
            pageList.Dock = DockStyle.Fill;
            pageList.BackColor = Theme.Bg;
            pageList.Margin = new Padding(0);
            pageList.ColumnCount = 1;
            pageList.RowCount = 3;
            pageList.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.S(48)));   // 搜索工具栏（圆角卡）
            pageList.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));           // 词条列表
            pageList.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.S(118)));  // 新增 / 覆盖（圆角卡）

            pageIO = new TableLayoutPanel();
            pageIO.Dock = DockStyle.Fill;
            pageIO.BackColor = Theme.Bg;
            pageIO.Margin = new Padding(0);
            pageIO.ColumnCount = 1;
            pageIO.RowCount = 2;
            pageIO.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            pageIO.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.S(0)));

            pageTrans = new TableLayoutPanel();
            pageTrans.Dock = DockStyle.Fill;
            pageTrans.BackColor = Theme.Bg;
            pageTrans.Margin = new Padding(0);
            pageTrans.ColumnCount = 1;
            pageTrans.RowCount = 2;
            pageTrans.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.S(288)));
            pageTrans.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            pageHost.Controls.Add(pageList);
            pageHost.Controls.Add(pageIO);
            pageHost.Controls.Add(pageTrans);
            rootGrid.Controls.Add(pageHost, 0, 1);


            // ---------- 搜索行 ----------
            TableLayoutPanel searchRow = new TableLayoutPanel();
            searchRow.Dock = DockStyle.Fill;
            searchRow.BackColor = Theme.Surface;   // 在卡片内 → 跟卡片同色，圆角才不会露黑角
            
            searchRow.Margin = new Padding(0);
            searchRow.ColumnCount = 4;
            searchRow.RowCount = 1;
            searchRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Theme.S(52)));
            searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Theme.S(72)));
            searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Theme.S(150)));

            Label lbSearch = new Label();
            lbSearch.Text = "搜索";
            lbSearch.Dock = DockStyle.Fill;
            lbSearch.AutoSize = false;
            lbSearch.Margin = new Padding(0);
            lbSearch.ForeColor = Theme.TextSecondary;
            lbSearch.Font = Theme.FontCaption;
            lbSearch.TextAlign = ContentAlignment.MiddleLeft;
            searchRow.Controls.Add(lbSearch, 0, 0);

            txtSearch = new DarkTextBox();
            txtSearch.Dock = DockStyle.Fill;
            txtSearch.Margin = new Padding(0, Theme.S(2), Theme.S(8), Theme.S(2));
            txtSearch.InnerBox.Font = Theme.FontBody;
            txtSearch.InnerBox.TextChanged += delegate(object s, EventArgs e) { if (!suppressReload) Reload(); };
            txtSearch.InnerBox.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape) { suppressReload = true; txtSearch.Text = ""; suppressReload = false; txtSearch.InnerBox.Refresh(); Reload(); e.SuppressKeyPress = true; }
            };
            searchRow.Controls.Add(txtSearch, 1, 0);

            FlatButton btnClearSearch = new FlatButton();
            btnClearSearch.Text = "清空";
            btnClearSearch.Tonal = true;                       // 柔和强调底色，和主按钮区分开
            btnClearSearch.Radius = Theme.RadSm;
            btnClearSearch.ForeColor = Theme.Accent;
            btnClearSearch.Font = Theme.FontCaption;
            btnClearSearch.Dock = DockStyle.Fill;
            btnClearSearch.Margin = new Padding(0, Theme.S(2), Theme.S(8), Theme.S(2));
            btnClearSearch.Click += delegate(object s, EventArgs e)
            {
                suppressReload = true;                         // 避免清空时触发两次重建
                txtSearch.Text = "";
                suppressReload = false;
                txtSearch.InnerBox.Refresh();                  // 输入框立刻变空，不等表格重排
                Reload();
            };
            searchRow.Controls.Add(btnClearSearch, 2, 0);

            lblCount = new Label();
            lblCount.Dock = DockStyle.Fill;
            lblCount.AutoSize = false;
            lblCount.Margin = new Padding(0);
            lblCount.ForeColor = Theme.TextMuted;
            lblCount.Font = Theme.FontCaption;
            lblCount.TextAlign = ContentAlignment.MiddleRight;
            searchRow.Controls.Add(lblCount, 3, 0);
            CardPanel toolbarCard = new CardPanel();
            toolbarCard.Title = "";
            toolbarCard.Hint = "";
            toolbarCard.Radius = Theme.RadMd;
            toolbarCard.Dock = DockStyle.Fill;
            toolbarCard.Margin = new Padding(0, 0, 0, Theme.GapMd);
            toolbarCard.Padding = new Padding(Theme.GapMd, Theme.S(8), Theme.GapMd, Theme.S(8));
            toolbarCard.Controls.Add(searchRow);
            pageList.Controls.Add(toolbarCard, 0, 0);


            CardPanel card = new CardPanel();
            card.Title = "词条列表";
            card.Hint = "双击载入到下方输入框";
            card.Dock = DockStyle.Fill;
            card.Padding = new Padding(Theme.S(12), Theme.S(32), Theme.S(12), Theme.S(12));
            card.Margin = new Padding(0, 0, 0, Theme.S(10));
            grid = new DataGridView();
            grid.Dock = DockStyle.Fill;
            Theme.StyleGrid(grid, false);
            grid.Columns.Add(Theme.TextColumn("中文", false, Theme.TextPrimary));
            grid.Columns.Add(Theme.TextColumn("英文", true, Theme.TextSecondary));
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.Columns[0].FillWeight = 30f;
            grid.Columns[1].FillWeight = 70f;
            grid.CellDoubleClick += delegate(object s, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex < 0) return;
                txtCn.Text = grid.Rows[e.RowIndex].Cells[0].Value as string;
                txtEn.Text = grid.Rows[e.RowIndex].Cells[1].Value as string;
            };
            card.Controls.Add(grid);

            lblEmpty = new Label();
            lblEmpty.Dock = DockStyle.Fill;
            lblEmpty.AutoSize = false;
            lblEmpty.BackColor = Theme.Surface;
            lblEmpty.ForeColor = Theme.TextMuted;
            lblEmpty.Font = Theme.FontCaption;
            lblEmpty.TextAlign = ContentAlignment.MiddleCenter;
            lblEmpty.Visible = false;
            card.Controls.Add(lblEmpty);
            lblEmpty.BringToFront();
            pageList.Controls.Add(card, 0, 1);

            TableLayoutPanel addRow = new TableLayoutPanel();
            addRow.Dock = DockStyle.Fill;
            addRow.BackColor = Theme.Surface;
            addRow.Margin = new Padding(0);
            addRow.ColumnCount = 5;
            addRow.RowCount = 1;
            addRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Theme.S(44)));
            addRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34f));
            addRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Theme.S(44)));
            addRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 66f));
            addRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Theme.S(100)));
            addRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            Label lbCn = new Label();
            lbCn.Text = "中文";
            lbCn.Dock = DockStyle.Fill;
            lbCn.AutoSize = false;
            lbCn.Margin = new Padding(0);
            lbCn.ForeColor = Theme.TextSecondary;
            lbCn.TextAlign = ContentAlignment.MiddleLeft;
            addRow.Controls.Add(lbCn, 0, 0);
            txtCn = new DarkTextBox();
            txtCn.Dock = DockStyle.Fill;
            txtCn.Margin = new Padding(0, Theme.S(2), Theme.S(8), Theme.S(2));
            txtCn.InnerBox.Font = Theme.FontBody;
            txtCn.InnerBox.KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; AddEntry(); } };
            addRow.Controls.Add(txtCn, 1, 0);
            Label lbEn = new Label();
            lbEn.Text = "英文";
            lbEn.Dock = DockStyle.Fill;
            lbEn.AutoSize = false;
            lbEn.Margin = new Padding(0);
            lbEn.ForeColor = Theme.TextSecondary;
            lbEn.TextAlign = ContentAlignment.MiddleLeft;
            addRow.Controls.Add(lbEn, 2, 0);
            txtEn = new DarkTextBox();
            txtEn.Dock = DockStyle.Fill;
            txtEn.Margin = new Padding(0, Theme.S(2), Theme.S(8), Theme.S(2));
            txtEn.InnerBox.Font = Theme.FontMonoSm;
            txtEn.InnerBox.KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; AddEntry(); } };
            addRow.Controls.Add(txtEn, 3, 0);
            FlatButton btnAdd = new FlatButton();
            btnAdd.Text = "添加/覆盖";
            btnAdd.Primary = true;
            btnAdd.Dock = DockStyle.Fill;
            btnAdd.Margin = new Padding(0, Theme.S(2), 0, Theme.S(2));
            btnAdd.Click += delegate(object s, EventArgs e) { AddEntry(); };
            addRow.Controls.Add(btnAdd, 4, 0);
            CardPanel addCard = new CardPanel();
            addCard.Title = "新增 / 覆盖词条";
            addCard.Hint = "同名词条以你的为准";
            addCard.Radius = Theme.RadMd;
            addCard.Dock = DockStyle.Fill;
            addCard.Margin = new Padding(0);
            addCard.Padding = new Padding(Theme.GapLg, Theme.S(36), Theme.GapLg, Theme.GapSm);

            TableLayoutPanel addStack = new TableLayoutPanel();
            addStack.Dock = DockStyle.Fill;
            addStack.BackColor = Theme.Surface;
            addStack.Margin = new Padding(0);
            addStack.ColumnCount = 1;
            addStack.RowCount = 2;
            addStack.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.S(38)));
            addStack.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            addStack.Controls.Add(addRow, 0, 0);

            Label lbFormat = new Label();
            lbFormat.Dock = DockStyle.Fill;
            lbFormat.AutoSize = false;
            lbFormat.Margin = new Padding(0);
            lbFormat.Font = Theme.FontCaption;
            lbFormat.ForeColor = Theme.TextMuted;
            lbFormat.TextAlign = ContentAlignment.TopLeft;
            lbFormat.AutoEllipsis = true;
            lbFormat.Text = "格式：中文=英文；同义词用 | 分隔（成绩 = score|grade）；英文多词用空格；空值 = 停用词。";
            addStack.Controls.Add(lbFormat, 0, 1);

            addCard.Controls.Add(addStack);
            pageList.Controls.Add(addCard, 0, 2);

            // ---------- 「导入导出」页 ----------
            CardPanel cardIO = new CardPanel();
            cardIO.Title = "词库文件与备份";
            cardIO.Hint = "外置词库放 dict 文件夹，启动自动导入";
            cardIO.Dock = DockStyle.Fill;
            cardIO.Margin = new Padding(0);
            cardIO.Padding = new Padding(Theme.GapLg, Theme.S(34), Theme.GapLg, Theme.GapMd);

            TableLayoutPanel ioGrid = new TableLayoutPanel();
            ioGrid.Dock = DockStyle.Fill;
            ioGrid.BackColor = Theme.Surface;
            ioGrid.Margin = new Padding(0);
            ioGrid.ColumnCount = 1;
            ioGrid.RowCount = 5;
            ioGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.S(52)));
            ioGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.S(40)));
            ioGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.S(40)));
            ioGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.S(40)));
            ioGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            Label lbIODir = new Label();
            lbIODir.Dock = DockStyle.Fill;
            lbIODir.AutoSize = false;
            lbIODir.Margin = new Padding(0);
            lbIODir.Font = Theme.FontSmall;
            lbIODir.ForeColor = Theme.TextMuted;
            lbIODir.TextAlign = ContentAlignment.TopLeft;
            lbIODir.Text = "程序目录：" + AutoDict.DictFolderPath + Environment.NewLine
                + "把任意 .txt 词库文件放进 dict 文件夹，程序启动自动导入（或点「检测本地词库」立即导入）。";
            ioGrid.Controls.Add(lbIODir, 0, 0);

            FlowLayoutPanel ioRow1 = NewOpRow();
            ioRow1.Controls.Add(MakeOp("检测本地词库", delegate { DetectLocalDict(); }));
            ioRow1.Controls.Add(MakeOp("打开词库目录", delegate
            {
                try
                {
                    string dir = AutoDict.DictFolderPath;
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    System.Diagnostics.Process.Start("explorer.exe", dir);
                }
                catch (Exception ex) { MessageBox.Show(ex.Message, "打开目录失败", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            }));
            ioGrid.Controls.Add(ioRow1, 0, 1);

            FlowLayoutPanel ioRow2 = NewOpRow();
            ioRow2.Controls.Add(MakeOp("导入 txt…", delegate { Import(); }));
            ioRow2.Controls.Add(MakeOp("导出 txt…", delegate { Export(); }));
            ioGrid.Controls.Add(ioRow2, 0, 2);

            FlowLayoutPanel ioRow3 = NewOpRow();
            ioRow3.Controls.Add(MakeOp("备份数据", delegate { BackupAll(); }));
            ioRow3.Controls.Add(MakeOp("恢复数据", delegate { RestoreAll(); }));
            ioGrid.Controls.Add(ioRow3, 0, 3);

            cardIO.Controls.Add(ioGrid);
            pageIO.Controls.Add(cardIO, 0, 0);

            lblStatus = new Label();
            lblStatus.Dock = DockStyle.Fill;
            lblStatus.AutoSize = false;
            lblStatus.Margin = new Padding(0);
            lblStatus.ForeColor = Theme.TextMuted;
            lblStatus.Font = Theme.FontSmall;
            lblStatus.TextAlign = ContentAlignment.MiddleLeft;
            lblStatus.AutoEllipsis = true;
            lblStatus.Text = "共 " + state.LoadUserDictEntries().Count + " 条用户词条";
            rootGrid.Controls.Add(lblStatus, 0, 2);

            // ---------- 底部固定操作 ----------
            CardPanel footerCard = new CardPanel();
            footerCard.Title = "";
            footerCard.Hint = "";
            footerCard.Radius = Theme.RadMd;
            footerCard.Dock = DockStyle.Fill;
            footerCard.Margin = new Padding(0);
            footerCard.Padding = new Padding(Theme.GapMd, Theme.S(7), Theme.GapMd, Theme.S(7));
            FlowLayoutPanel footer = NewOpRow();
            footer.FlowDirection = FlowDirection.RightToLeft;      // 主操作靠右（对话框惯例）
            FlatButton btnSave = MakeOp("保存并关闭", delegate { SaveAndClose(); });
            btnSave.Primary = true;
            btnSave.Dock = DockStyle.None;
            btnSave.Size = new Size(Theme.S(118), Theme.S(32));
            btnSave.Margin = new Padding(0);
            footer.Controls.Add(btnSave);
            footerCard.Controls.Add(footer);
            rootGrid.Controls.Add(footerCard, 0, 3);

            // ---------- 在线翻译入口 ----------
            // ---------- 「在线翻译」页：设置面板 ----------
            CardPanel cardTr = new CardPanel();
            cardTr.Title = "在线翻译";
            cardTr.Hint = "可选 · 默认关闭";
            cardTr.Dock = DockStyle.Fill;
            cardTr.Margin = new Padding(0, 0, 0, Theme.GapMd);
            cardTr.Padding = new Padding(Theme.GapXl, Theme.S(42), Theme.GapXl, Theme.GapLg);

            TableLayoutPanel trGrid = new TableLayoutPanel();
            trGrid.Dock = DockStyle.Fill;
            trGrid.BackColor = Theme.Surface;
            trGrid.Margin = new Padding(0);
            trGrid.ColumnCount = 1;
            trGrid.RowCount = 6;
            trGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.S(32)));   // 状态行
            trGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.S(22)));   // 小标题
            trGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.S(44)));   // 说明正文
            trGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.S(13)));   // 分隔线
            trGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.S(66)));   // 键值信息
            trGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));           // 主操作

            // 状态行：徽章 + 引擎
            TableLayoutPanel stRow = new TableLayoutPanel();
            stRow.Dock = DockStyle.Fill;
            stRow.BackColor = Theme.Surface;
            stRow.Margin = new Padding(0);
            stRow.ColumnCount = 2;
            stRow.RowCount = 1;
            stRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            stRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            stRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Theme.S(150)));

            FlowLayoutPanel stLeft = new FlowLayoutPanel();
            stLeft.Dock = DockStyle.Fill;
            stLeft.BackColor = Theme.Surface;
            stLeft.Margin = new Padding(0);
            stLeft.WrapContents = false;
            badge = new Badge();
            badge.Text = state.Cfg.TranslateEnabled ? "已启用" : "未启用";
            badge.AccentColor = state.Cfg.TranslateEnabled ? Theme.Ok : Theme.TextMuted;
            badge.Size = new Size(Theme.S(76), Theme.S(24));
            badge.Margin = new Padding(0, Theme.S(4), Theme.GapMd, 0);
            stLeft.Controls.Add(badge);
            lbEngine = new Label();
            lbEngine.AutoSize = false;
            lbEngine.Font = Theme.FontCaption;
            lbEngine.ForeColor = Theme.TextSecondary;
            lbEngine.TextAlign = ContentAlignment.MiddleLeft;
            lbEngine.Margin = new Padding(0, Theme.S(4), 0, 0);
            lbEngine.Size = new Size(Theme.S(320), Theme.S(24));
            lbEngine.Text = "当前引擎：" + ProviderLabel(state.Cfg.TranslateProvider);
            stLeft.Controls.Add(lbEngine);
            stRow.Controls.Add(stLeft, 0, 0);

            // 页面上的启用开关（原来只能进设置弹窗改）
            ToggleCheck chkTrOn = new ToggleCheck();
            chkTrOn.Text = "启用在线翻译";
            chkTrOn.Checked = state.Cfg.TranslateEnabled;
            chkTrOn.Margin = new Padding(0, Theme.S(4), 0, 0);
            chkTrOn.Size = chkTrOn.GetPreferredSize(Size.Empty);
            chkTrOn.CheckedChanged += delegate(object s, EventArgs e)
            {
                state.Cfg.TranslateEnabled = chkTrOn.Checked;
                state.Cfg.Save();
                RefreshTranslatePanel();
            };
            stRow.Controls.Add(chkTrOn, 1, 0);
            trGrid.Controls.Add(stRow, 0, 0);

            Label lbTrTitle = new Label();
            lbTrTitle.Dock = DockStyle.Fill;
            lbTrTitle.AutoSize = false;
            lbTrTitle.Margin = new Padding(0);
            lbTrTitle.Font = Theme.FontCaption;
            lbTrTitle.ForeColor = Theme.TextSecondary;
            lbTrTitle.TextAlign = ContentAlignment.MiddleLeft;
            lbTrTitle.Text = "它能做什么";
            trGrid.Controls.Add(lbTrTitle, 0, 1);

            Label lbTrBody = new Label();
            lbTrBody.Dock = DockStyle.Fill;
            lbTrBody.AutoSize = false;
            lbTrBody.Margin = new Padding(0);
            lbTrBody.Font = Theme.FontCaption;
            lbTrBody.ForeColor = Theme.TextMuted;
            lbTrBody.TextAlign = ContentAlignment.TopLeft;
            lbTrBody.Text = "内置词库没命中的词，可联网翻译一次，并把结果写回用户词库——之后离线也能用。" + Environment.NewLine
                          + "不启用完全不影响任何本地功能。";
            trGrid.Controls.Add(lbTrBody, 0, 2);

            Panel trLine = new Panel();
            trLine.Dock = DockStyle.Top;
            trLine.Height = 1;
            trLine.BackColor = Theme.CardLine;
            trLine.Margin = new Padding(0, Theme.S(6), 0, Theme.S(6));
            trGrid.Controls.Add(trLine, 0, 3);

            // 键值信息：需要什么 / 密钥状态 / 自动收录
            TableLayoutPanel kv = new TableLayoutPanel();
            kv.Dock = DockStyle.Fill;
            kv.BackColor = Theme.Surface;
            kv.Margin = new Padding(0);
            kv.ColumnCount = 2;
            kv.RowCount = 3;
            kv.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Theme.S(84)));
            kv.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            for (int i = 0; i < 3; i++) kv.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.S(22)));
            Label lbNeed = new Label();
            AddKVRow(kv, 0, "需要", lbNeed);
            lbNeed.Text = "外网 + 在服务商申请密钥（百度翻译 / 有道智云 / DeepL / 自定义）";
            lblTrKey = new Label();
            AddKVRow(kv, 1, "密钥", lblTrKey);
            lblTrAuto = new Label();
            AddKVRow(kv, 2, "自动收录", lblTrAuto);
            trGrid.Controls.Add(kv, 0, 4);

            // 主操作
            FlowLayoutPanel trActions = new FlowLayoutPanel();
            trActions.Dock = DockStyle.Fill;
            trActions.BackColor = Theme.Surface;
            trActions.Margin = new Padding(0);
            trActions.WrapContents = false;
            trActions.Padding = new Padding(0, Theme.S(10), 0, 0);
            FlatButton btnTrSet = new FlatButton();
            btnTrSet.Text = "配置在线翻译…";
            btnTrSet.Primary = true;
            btnTrSet.Size = new Size(Theme.S(150), Theme.S(32));
            btnTrSet.Margin = new Padding(0);
            btnTrSet.Click += delegate(object s, EventArgs e)
            {
                using (TranslateForm tf = new TranslateForm(state)) { tf.ShowDialog(this); }
                RefreshTranslatePanel();      // 弹窗里改了启用/密钥 → 页面同步
                if (chkTrOn != null) chkTrOn.Checked = state.Cfg.TranslateEnabled;
            };
            trActions.Controls.Add(btnTrSet);
            trGrid.Controls.Add(trActions, 0, 5);
            RefreshTranslatePanel();

            cardTr.Controls.Add(trGrid);
            pageTrans.Controls.Add(cardTr, 0, 0);

            // 下方说明卡：未收录词怎么处理
            CardPanel cardUnknown = new CardPanel();
            cardUnknown.Title = "查不到的汉字";
            cardUnknown.Hint = "不联网也能处理";
            cardUnknown.Dock = DockStyle.Fill;
            cardUnknown.Margin = new Padding(0);
            cardUnknown.Padding = new Padding(Theme.GapXl, Theme.S(42), Theme.GapXl, Theme.GapLg);
            Label lbUnknown = new Label();
            lbUnknown.Dock = DockStyle.Fill;
            lbUnknown.AutoSize = false;
            lbUnknown.Margin = new Padding(0);
            lbUnknown.Font = Theme.FontCaption;
            lbUnknown.ForeColor = Theme.TextMuted;
            lbUnknown.TextAlign = ContentAlignment.TopLeft;
            lbUnknown.Text = "未命中的字会自动记到这个文件：" + Environment.NewLine
                + Path.Combine(Config.DataDir, "unknowns.txt") + Environment.NewLine + Environment.NewLine
                + "到「词条」页补一条即可永久生效，不需要联网。";
            cardUnknown.Controls.Add(lbUnknown);
            pageTrans.Controls.Add(cardUnknown, 0, 1);

            SwitchTab(0);          // 默认打开「词条」页
            entries = state.LoadUserDictEntries();
            Reload();
        }

        // 一行的按钮容器（统一间距）
        private static FlowLayoutPanel NewOpRow()
        {
            FlowLayoutPanel p = new FlowLayoutPanel();
            p.Dock = DockStyle.Fill;
            p.BackColor = Theme.Surface;
            p.Margin = new Padding(0);
            p.WrapContents = false;
            return p;
        }

        // 切页：三个页面同位置叠放，只显示当前页
        private void SwitchTab(int index)
        {
            if (index < 0 || index > 2) return;
            activeTab = index;
            pageList.Visible = index == 0;
            pageIO.Visible = index == 1;
            pageTrans.Visible = index == 2;
            if (tabs != null && tabs.SelectedIndex != index) tabs.SelectedIndex = index;
            if (index == 0 && txtSearch != null) txtSearch.InnerBox.Focus();
        }

        // 一行“标签 + 值控件”（值用字段持有，便于运行时刷新）
        private static void AddKVRow(TableLayoutPanel grid, int row, string key, Label value)
        {
            Label k = new Label();
            k.Dock = DockStyle.Fill;
            k.AutoSize = false;
            k.Margin = new Padding(0);
            k.Font = Theme.FontCaption;
            k.ForeColor = Theme.TextSecondary;
            k.TextAlign = ContentAlignment.MiddleLeft;
            k.Text = key;
            grid.Controls.Add(k, 0, row);

            value.Dock = DockStyle.Fill;
            value.AutoSize = false;
            value.Margin = new Padding(0);
            value.Font = Theme.FontCaption;
            value.ForeColor = Theme.TextMuted;
            value.TextAlign = ContentAlignment.MiddleLeft;
            value.AutoEllipsis = true;
            grid.Controls.Add(value, 1, row);
        }

        // 刷新「在线翻译」页：状态徽章 / 引擎 / 密钥是否配置 / 自动收录
        private void RefreshTranslatePanel()
        {
            if (badge == null || badge.IsDisposed) return;
            bool on = state.Cfg.TranslateEnabled;
            badge.Text = on ? "已启用" : "未启用";
            badge.AccentColor = on ? Theme.Ok : Theme.TextMuted;
            badge.Invalidate();
            if (lbEngine != null)
                lbEngine.Text = "当前引擎：" + ProviderLabel(state.Cfg.TranslateProvider) + (on ? "" : "（未启用）");
            bool keySet = (state.Cfg.TranslateAppId != null && state.Cfg.TranslateAppId.Length > 0)
                       || (state.Cfg.TranslateKey != null && state.Cfg.TranslateKey.Length > 0);
            if (lblTrKey != null) lblTrKey.Text = keySet ? "已配置" : "未配置";
            if (lblTrAuto != null) lblTrAuto.Text = state.Cfg.TranslateAutoAdd ? "开（翻译结果自动写进用户词库）" : "关";
        }

        // 分组分隔条（1px 竖线）：让按钮区有层次
        private static Control OpSeparator()
        {
            Panel p = new Panel();
            p.Width = 1;
            p.Height = Theme.S(18);
            p.BackColor = Theme.Border;
            p.Margin = new Padding(Theme.GapSm, Theme.S(7), Theme.GapSm, 0);
            return p;
        }

        private static FlatButton MakeOp(string text, EventHandler onClick)
        {
            FlatButton b = new FlatButton();
            b.Text = text;
            b.Size = new Size(Theme.S(92), Theme.S(30));
            b.Margin = new Padding(0, Theme.S(2), Theme.S(8), Theme.S(2));
            b.Click += onClick;
            return b;
        }

        private static Label MakeCaption(string text)
        {
            Label l = new Label();
            l.Text = text;
            l.Dock = DockStyle.Fill;
            l.AutoSize = false;
            l.Margin = new Padding(0);
            l.ForeColor = Theme.TextSecondary;
            l.Font = Theme.FontSmall;
            l.TextAlign = ContentAlignment.MiddleLeft;
            return l;
        }

        internal static string ProviderLabel(string p)
        {
            if (p == "baidu") return "百度翻译";
            if (p == "youdao") return "有道智云";
            if (p == "deepl") return "DeepL";
            if (p == "custom") return "自定义URL";
            return "百度翻译";
        }

        internal static string ProviderKey(string label)
        {
            if (label == "有道智云") return "youdao";
            if (label == "DeepL") return "deepl";
            if (label == "自定义URL") return "custom";
            return "baidu";
        }

        private void Reload()
        {
            string kw = (txtSearch == null) ? "" : txtSearch.Text.Trim();
            // 先筛出命中的下标，再一次性建行（逐行 Rows.Add(值) 在几千条时会卡几百毫秒）
            List<int> hits = new List<int>();
            for (int i = 0; i < entries.Count; i++)
            {
                if (kw.Length > 0)
                {
                    bool hit = entries[i][0].IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0
                        || entries[i][1].IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!hit) continue;
                }
                hits.Add(i);
            }
            int shown = hits.Count;
            grid.SuspendLayout();
            grid.Rows.Clear();
            if (shown > 0)
            {
                grid.Rows.Add(shown);                       // 批量建行
                for (int k = 0; k < shown; k++)
                {
                    DataGridViewRow row = grid.Rows[k];
                    row.Cells[0].Value = entries[hits[k]][0];
                    row.Cells[1].Value = entries[hits[k]][1];
                }
            }
            grid.ResumeLayout();
            if (lblEmpty != null)
            {
                lblEmpty.Visible = shown == 0;
                if (shown == 0)
                {
                    lblEmpty.Text = entries.Count == 0
                        ? ("还没有你自己的词条" + Environment.NewLine + Environment.NewLine
                           + "在下面「新增 / 覆盖词条」加一条，或到「导入导出」页导入 txt。")
                        : ("没有匹配「" + kw + "」的词条");
                    lblEmpty.BringToFront();
                }
            }
            if (lblCount != null)
                lblCount.Text = kw.Length > 0
                    ? ("匹配 " + shown + " / " + entries.Count + " 条")
                    : ("共 " + entries.Count + " 条");
            lblStatus.Text = kw.Length > 0
                ? ("已按「" + kw + "」过滤，显示 " + shown + " 条；双击条目可载入下方输入框")
                : "共 " + entries.Count + " 条用户词条（未保存的改动点「保存并关闭」生效）";
        }

        private void AddEntry()
        {
            string cn = txtCn.Text.Trim();
            string en = txtEn.Text.Trim();
            if (cn.Length == 0 || en.Length == 0) { lblStatus.Text = "中文与英文都要填"; return; }
            bool found = false;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i][0] == cn) { entries[i][1] = en; found = true; break; }
            }
            if (!found) entries.Add(new string[] { cn, en });
            txtCn.Text = "";
            txtEn.Text = "";
            Reload();
        }

        private void DeleteSelected()
        {
            if (grid.SelectedRows.Count == 0) return;
            List<string> drop = new List<string>();
            for (int i = 0; i < grid.SelectedRows.Count; i++)
                drop.Add(grid.SelectedRows[i].Cells[0].Value as string);
            for (int d = 0; d < drop.Count; d++)
            {
                for (int i = entries.Count - 1; i >= 0; i--)
                    if (entries[i][0] == drop[d]) entries.RemoveAt(i);
            }
            Reload();
        }

        // 检测本地词库并导入。只扫程序自己的目录（程序目录 + 程序目录\dict）。
        // 手动触发时忽略指纹、强制重新解析一次，并如实报告：每个文件多少条、写入多少、为什么没写入。
        private void DetectLocalDict()
        {
            List<DictHit> hits = AutoDict.ScanAndImport(state, true, false);
            if (hits.Count == 0)
            {
                DialogResult r = MessageBox.Show(this,
                    "没检测到本地词库文件。" + Environment.NewLine + Environment.NewLine
                    + "只扫描程序自己的目录：" + Environment.NewLine
                    + AutoDict.DescribeDirs() + Environment.NewLine
                    + "把词库文件（.txt）放进上面标 [有] 的目录就会自动识别，其中：" + Environment.NewLine
                    + "· dict 文件夹：任意 .txt 文件名都可以（推荐）" + Environment.NewLine
                    + "· 程序目录本身：文件名要含「词库」或「dict」，或用固定名" + Environment.NewLine
                    + "  （VarNamer-Dict-CN-EN.txt / VarNamer-Dict.txt / userdict.txt / 词库.txt / dict.txt）" + Environment.NewLine
                    + "· 格式：中文=英文（# 开头是注释；英文多词用空格；同义词用 | 分隔）" + Environment.NewLine + Environment.NewLine
                    + "提示：如果程序是直接在压缩包里双击运行的，请先解压到文件夹再运行，"
                    + "否则程序会去临时目录找词库。" + Environment.NewLine + Environment.NewLine
                    + "是否现在手动选择一个词库文件导入？",
                    "未检测到本地词库", MessageBoxButtons.YesNo);
                if (r == DialogResult.Yes) Import();
                return;
            }

            int totalAdded = 0;
            int totalValid = 0;
            int problems = 0;
            for (int i = 0; i < hits.Count; i++)
            {
                totalAdded += hits[i].Added;
                totalValid += hits[i].Total;
                if (hits[i].Error != null) problems++;
            }

            if (totalAdded > 0)
            {
                entries = state.LoadUserDictEntries();   // 先刷新列表，报告里的条数才是最新的
                Reload();
                lblStatus.Text = "已导入 " + totalAdded + " 条（已保存并立即生效）";
                ShowDetectReport(hits, false);
                return;
            }

            ShowDetectReport(hits, false);

            if (problems > 0 || totalValid == 0)
            {
                lblStatus.Text = problems > 0 ? "检测到问题，详见提示" : "没有可导入的词条（格式不对？）";
                return;
            }

            // 检测到了、但都已在词库里 → 问清楚是否要“全量写进用户词库”
            DialogResult r2 = MessageBox.Show(this,
                "检测到 " + totalValid + " 条词条，但它们都已经在词库里了（内置词库或你的用户词库），"
                + "所以按“只做加法、不重复导入”的规则没有写文件。" + Environment.NewLine + Environment.NewLine
                + "这些词现在就能正常使用，不需要导入。" + Environment.NewLine
                + "如果你希望在「词条列表」里也能看到并编辑它们，可以把它们全部写进用户词库"
                + "（会和内置词库内容重复）。" + Environment.NewLine + Environment.NewLine
                + "要现在全部写入用户词库吗？",
                "词条都已在词库中", MessageBoxButtons.YesNo);
            if (r2 != DialogResult.Yes)
            {
                lblStatus.Text = "词条都已收录，无需导入";
                return;
            }
            List<DictHit> all = AutoDict.ScanAndImport(state, true, true);
            int added2 = 0;
            for (int i = 0; i < all.Count; i++) added2 += all[i].Added;
            entries = state.LoadUserDictEntries();
            Reload();
            lblStatus.Text = added2 > 0 ? ("已全量导入 " + added2 + " 条") : "没有可导入的词条";
            ShowDetectReport(all, true);
        }

        // 检测报告窗口
        private void ShowDetectReport(List<DictHit> hits, bool importAll)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("扫描范围（只扫程序自己的目录）");
            sb.Append(AutoDict.DescribeDirs());
            sb.AppendLine();

            int added = 0;
            int valid = 0;
            for (int i = 0; i < hits.Count; i++) { added += hits[i].Added; valid += hits[i].Total; }
            sb.AppendLine("找到 " + hits.Count + " 个词库文件");
            for (int i = 0; i < hits.Count; i++)
            {
                DictHit h = hits[i];
                sb.AppendLine("  " + (i + 1) + ") " + h.Path);
                if (h.UpToDate)
                {
                    sb.AppendLine("     文件里 " + h.Total + " 条 · 与上次导入时一致，本次未重新解析");
                }
                else if (h.Total == 0 && h.Invalid == 0)
                {
                    sb.AppendLine("     这里没有可识别的词条（空文件或全是注释）");
                }
                else if (h.Total == 0)
                {
                    sb.AppendLine("     没有可用词条 · 格式不对 " + h.Invalid + " 行");
                    for (int k = 0; k < h.InvalidSamples.Count; k++)
                        sb.AppendLine("       示例：「" + h.InvalidSamples[k] + "」");
                    sb.AppendLine("     正确格式：中文=英文");
                }
                else
                {
                    sb.AppendLine("     文件里 " + h.Total + " 条 · 写入用户词库 " + h.Added + " 条 · 跳过 " + h.Skipped + " 条");
                    if (h.Skipped > 0 && h.Added == 0)
                        sb.AppendLine("       （跳过的都已在词库中：内置词库里已有，或你的用户词库里已有。这些词现在就能用。）");
                    if (h.Invalid > 0)
                        sb.AppendLine("       （另有 " + h.Invalid + " 行格式不对已忽略）");
                }
                if (h.Error != null) sb.AppendLine("     ⚠ " + h.Error);
            }
            sb.AppendLine();
            sb.AppendLine("用户词库文件：" + Config.UserDictPath);
            sb.AppendLine("规则：只做加法。用户词库里已有同名的条目永远不会被覆盖；"
                + (importAll ? "本次按你的选择把与内置重复的词条也写进了用户词库。" : "与内置词库重复的条目不重复写入。"));
            sb.AppendLine();
            if (added > 0)
                sb.AppendLine("结果：写入 " + added + " 条，用户词条共 " + entries.Count + " 条，已立即生效。");
            else if (valid > 0)
                sb.AppendLine("结果：本次没有写入（" + valid + " 条都已收录）。");
            else
                sb.AppendLine("结果：没有可用词条。");
            new TextInfoForm("本地词库检测", "自动检测与导入", sb.ToString()).ShowDialog(this);
        }

        private void Import()
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "词库文件 (*.txt)|*.txt|所有文件 (*.*)|*.*";
            if (ofd.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                string[] lines = File.ReadAllLines(ofd.FileName, Encoding.UTF8);
                int n = 0;
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (line.Length == 0 || line[0] == '#') continue;
                    int p = line.IndexOf('=');
                    if (p <= 0) continue;
                    string cn = line.Substring(0, p).Trim();
                    string en = line.Substring(p + 1).Trim();
                    if (cn.Length == 0 || en.Length == 0) continue;
                    bool found = false;
                    for (int k = 0; k < entries.Count; k++)
                        if (entries[k][0] == cn) { entries[k][1] = en; found = true; break; }
                    if (!found) entries.Add(new string[] { cn, en });
                    n++;
                }
                Reload();
                lblStatus.Text = "导入 " + n + " 条，保存后生效";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "导入失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void Export()
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "词库文件 (*.txt)|*.txt";
            sfd.FileName = "userdict.txt";
            if (sfd.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("# VarNamer 用户词库");
                for (int i = 0; i < entries.Count; i++) sb.AppendLine(entries[i][0] + "=" + entries[i][1]);
                File.WriteAllText(sfd.FileName, sb.ToString(), new UTF8Encoding(false));
                lblStatus.Text = "已导出 " + entries.Count + " 条";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "导出失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // 一键备份：把配置 + 用户词库打成单个文件，方便换机器/重装迁移
        private void BackupAll()
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "VarNamer 备份 (*.vnbak)|*.vnbak";
            sfd.FileName = "VarNamer-备份-" + DateTime.Now.ToString("yyyyMMdd") + ".vnbak";
            if (sfd.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                SaveUserDictToFile();
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("# VarNamer 备份文件 v1");
                sb.AppendLine("# 包含：配置选项 + 用户词库（导入即恢复）");
                sb.AppendLine("[config]");
                sb.Append(File.Exists(Config.ConfigPath) ? File.ReadAllText(Config.ConfigPath, Encoding.UTF8) : "");
                sb.AppendLine("[userdict]");
                sb.Append(File.Exists(Config.UserDictPath) ? File.ReadAllText(Config.UserDictPath, Encoding.UTF8) : "");
                File.WriteAllText(sfd.FileName, sb.ToString(), new UTF8Encoding(false));
                lblStatus.Text = "已备份到 " + Path.GetFileName(sfd.FileName) + "（含配置与 " + entries.Count + " 条词条）";
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "备份失败", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }

        private void RestoreAll()
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "VarNamer 备份 (*.vnbak)|*.vnbak|所有文件 (*.*)|*.*";
            if (ofd.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                string text = File.ReadAllText(ofd.FileName, Encoding.UTF8);
                int ci = text.IndexOf("[config]");
                int ui = text.IndexOf("[userdict]");
                if (ui < 0) { MessageBox.Show("不是有效的备份文件。", "恢复失败", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                string cfgText = (ci >= 0 && ui > ci) ? text.Substring(ci + 8, ui - ci - 8) : "";
                string dictText = text.Substring(ui + 10);
                if (dictText.Trim().Length > 0) File.WriteAllText(Config.UserDictPath, dictText, new UTF8Encoding(false));
                if (cfgText.Trim().Length > 0)
                {
                    File.WriteAllText(Config.ConfigPath, cfgText, new UTF8Encoding(false));
                    state.Cfg = Config.Load();
                    state.Cfg.ApplyToTranslator();
                    state.Opt = state.Cfg.ToOptions();
                }
                state.Lex = Lexicon.Create();
                state.Lex.LoadUserFile(Config.UserDictPath);
                state.Lex.Rebuild();
                entries = state.LoadUserDictEntries();
                Reload();
                MessageBox.Show("已恢复：词条 " + entries.Count + " 条" + (cfgText.Trim().Length > 0 ? "，配置也已覆盖（部分选项需重启生效）" : "")
                    + "。", "恢复完成", MessageBoxButtons.OK);   // 信息类弹窗不带图标 → 不播提示音
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "恢复失败", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }

        // 把界面上的词条写回用户词库文件（备份/恢复用，不关闭窗口）
        private void SaveUserDictToFile()
        {
            try { state.SaveUserDict(entries); } catch (Exception) { }
        }

        private void SaveAndClose()
        {
            try
            {
                state.SaveUserDict(entries);
                state.Lex = Lexicon.Create();
                state.Lex.LoadUserFile(Config.UserDictPath);
                state.Lex.Rebuild();
            }
            catch (Exception) { }
            Close();
        }
    }
}
