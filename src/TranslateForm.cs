using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace VarNamer
{
    // 在线翻译设置（独立小窗；不用在线翻译的可以完全忽略）
    public class TranslateForm : ModernForm
    {
        private AppState state;
        private ToggleCheck chkOn, chkAdd;
        private DarkCombo cboProvider;
        private DarkTextBox txtAppId, txtKey, txtUrl;
        private Label lblStatus;
        private Label lblNeed;

        public TranslateForm(AppState st)
        {
            state = st;
            Text = "VarNamer  在线翻译设置";
            Size = new Size(Theme.S(620), Theme.S(400));
            MinimumSize = new Size(Theme.S(560), Theme.S(380));
            Resizable = false;
            LblSub.Text = "v" + AppVersion.Value + " · 可选功能";
            StartPosition = FormStartPosition.CenterParent;

            TableLayoutPanel rootGrid = new TableLayoutPanel();
            rootGrid.Dock = DockStyle.Fill;
            rootGrid.BackColor = Theme.Bg;
            rootGrid.ColumnCount = 1;
            rootGrid.RowCount = 5;
            rootGrid.Padding = new Padding(Theme.S(16), Theme.S(4), Theme.S(16), Theme.S(14));
            rootGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.S(56)));
            rootGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.S(40)));
            rootGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.S(30)));
            rootGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.S(40)));
            rootGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            Content.Controls.Add(rootGrid);

            Label hint = new Label();
            hint.Dock = DockStyle.Fill;
            hint.AutoSize = false;
            hint.Margin = new Padding(0);
            hint.ForeColor = Theme.TextMuted;
            hint.Font = Theme.FontSmall;
            hint.TextAlign = ContentAlignment.MiddleLeft;
            hint.Text = "内置词库没命中的词，可调用翻译接口兜底；成功结果会写回你的用户词库，之后离线可用。" + "\r\n"
                + "需要能访问外网 + 对应平台的密钥。用不了就保持关闭，不影响其它功能。";
            rootGrid.Controls.Add(hint, 0, 0);

            FlowLayoutPanel row1 = new FlowLayoutPanel();
            row1.Dock = DockStyle.Fill;
            row1.WrapContents = false;
            row1.BackColor = Theme.Bg;
            row1.MinimumSize = new Size(0, Theme.S(34));
            chkOn = new ToggleCheck();
            chkOn.Text = "启用在线翻译";
            chkOn.Checked = state.Cfg.TranslateEnabled;
            chkOn.Margin = new Padding(0, Theme.S(2), Theme.S(18), 0);
            chkOn.Size = chkOn.GetPreferredSize(Size.Empty);
            row1.Controls.Add(chkOn);
            cboProvider = new DarkCombo();
            cboProvider.AddRange(new object[] { "百度翻译", "有道智云", "DeepL", "自定义URL" });
            cboProvider.Size = new Size(Theme.S(120), Theme.S(28));
            cboProvider.Margin = new Padding(0, Theme.S(1), Theme.S(18), 0);
            cboProvider.SelectText(LexiconForm.ProviderLabel(state.Cfg.TranslateProvider));
            row1.Controls.Add(cboProvider);
            chkAdd = new ToggleCheck();
            chkAdd.Text = "译文写入我的词库";
            chkAdd.Checked = state.Cfg.TranslateAutoAdd;
            chkAdd.Margin = new Padding(0, Theme.S(2), 0, 0);
            chkAdd.Size = chkAdd.GetPreferredSize(Size.Empty);
            row1.Controls.Add(chkAdd);
            rootGrid.Controls.Add(row1, 0, 1);

            lblNeed = new Label();
            lblNeed.Dock = DockStyle.Fill;
            lblNeed.AutoSize = false;
            lblNeed.Margin = new Padding(0);
            lblNeed.ForeColor = Theme.TextSecondary;
            lblNeed.Font = Theme.FontSmall;
            lblNeed.TextAlign = ContentAlignment.MiddleLeft;
            lblNeed.Text = TranslateHelp.ProviderNeed(state.Cfg.TranslateProvider);
            cboProvider.SelectedIndexChanged += delegate(object s, EventArgs e)
            {
                lblNeed.Text = TranslateHelp.ProviderNeed(LexiconForm.ProviderKey(cboProvider.SelectedItem));
            };

            TableLayoutPanel row2 = new TableLayoutPanel();
            row2.Dock = DockStyle.Fill;
            row2.BackColor = Theme.Bg;
            row2.Margin = new Padding(0);
            row2.ColumnCount = 4;
            row2.RowCount = 1;
            row2.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            row2.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Theme.S(96)));
            row2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40f));
            row2.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Theme.S(76)));
            row2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60f));
            row2.Controls.Add(Caption("AppID/AppKey"), 0, 0);
            txtAppId = new DarkTextBox();
            txtAppId.Dock = DockStyle.Fill;
            txtAppId.Margin = new Padding(0, Theme.S(2), Theme.S(8), Theme.S(2));
            txtAppId.InnerBox.Font = Theme.FontMonoSm;
            txtAppId.Text = state.Cfg.TranslateAppId;
            row2.Controls.Add(txtAppId, 1, 0);
            row2.Controls.Add(Caption("密钥/Key"), 2, 0);
            txtKey = new DarkTextBox();
            txtKey.Dock = DockStyle.Fill;
            txtKey.Margin = new Padding(0, Theme.S(2), 0, Theme.S(2));
            txtKey.InnerBox.Font = Theme.FontMonoSm;
            txtKey.Text = state.Cfg.TranslateKey;
            row2.Controls.Add(txtKey, 3, 0);
            rootGrid.Controls.Add(lblNeed, 0, 2);
            rootGrid.Controls.Add(row2, 0, 3);

            TableLayoutPanel row3 = new TableLayoutPanel();
            row3.Dock = DockStyle.Fill;
            row3.BackColor = Theme.Bg;
            row3.Margin = new Padding(0);
            row3.ColumnCount = 1;
            row3.RowCount = 3;
            row3.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.S(38)));
            row3.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.S(40)));
            row3.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            TableLayoutPanel urlRow = new TableLayoutPanel();
            urlRow.Dock = DockStyle.Fill;
            urlRow.BackColor = Theme.Bg;
            urlRow.Margin = new Padding(0);
            urlRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            urlRow.ColumnCount = 2;
            urlRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Theme.S(96)));
            urlRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            urlRow.Controls.Add(Caption("自定义URL"), 0, 0);
            txtUrl = new DarkTextBox();
            txtUrl.Dock = DockStyle.Fill;
            txtUrl.Margin = new Padding(0, Theme.S(2), 0, Theme.S(2));
            txtUrl.InnerBox.Font = Theme.FontMonoSm;
            txtUrl.Text = state.Cfg.TranslateCustomUrl;
            urlRow.Controls.Add(txtUrl, 1, 0);
            row3.Controls.Add(urlRow, 0, 0);

            FlowLayoutPanel btns = new FlowLayoutPanel();
            btns.Dock = DockStyle.Fill;
            btns.WrapContents = false;
            btns.BackColor = Theme.Bg;
            FlatButton btnTest = new FlatButton();
            btnTest.Text = "测试翻译";
            btnTest.Size = new Size(Theme.S(104), Theme.S(30));
            btnTest.Margin = new Padding(0, Theme.S(3), Theme.S(8), 0);
            btnTest.Click += delegate(object s, EventArgs e) { TestTranslate(); };
            btns.Controls.Add(btnTest);
            FlatButton btnHelp = new FlatButton();
            btnHelp.Text = "使用说明";
            btnHelp.Size = new Size(Theme.S(104), Theme.S(30));
            btnHelp.Margin = new Padding(0, Theme.S(3), Theme.S(8), 0);
            btnHelp.Click += delegate(object s, EventArgs e)
            {
                using (TextInfoForm f = new TextInfoForm("在线翻译 使用说明", "含各平台字段对照与报错对照", TranslateHelp.Text))
                { f.ShowDialog(this); }
            };
            btns.Controls.Add(btnHelp);

            FlatButton btnSave = new FlatButton();
            btnSave.Text = "保存";
            btnSave.Primary = true;
            btnSave.Size = new Size(Theme.S(88), Theme.S(30));
            btnSave.Margin = new Padding(0, Theme.S(3), Theme.S(8), 0);
            btnSave.Click += delegate(object s, EventArgs e) { Save(); Close(); };
            btns.Controls.Add(btnSave);
            FlatButton btnOpen = new FlatButton();
            btnOpen.Text = "打开未收录词清单";
            btnOpen.Size = new Size(Theme.S(160), Theme.S(30));
            btnOpen.Margin = new Padding(0, Theme.S(3), 0, 0);
            btnOpen.Click += delegate(object s, EventArgs e) { UnknownLog.OpenLog(); };
            btns.Controls.Add(btnOpen);
            row3.Controls.Add(btns, 0, 1);

            lblStatus = new Label();
            lblStatus.Dock = DockStyle.Fill;
            lblStatus.AutoSize = false;
            lblStatus.Margin = new Padding(0);
            lblStatus.ForeColor = Theme.TextMuted;
            lblStatus.Font = Theme.FontSmall;
            lblStatus.TextAlign = ContentAlignment.TopLeft;
            lblStatus.Padding = new Padding(0, Theme.S(4), 0, 0);
            lblStatus.AutoEllipsis = true;
            lblStatus.Text = "第一次用？点上面的「使用说明」，里面有各平台要填什么、怎么申请、报错怎么查。";
            row3.Controls.Add(lblStatus, 0, 2);
            rootGrid.Controls.Add(row3, 0, 4);
        }

        private static Label Caption(string t)
        {
            Label l = new Label();
            l.Text = t;
            l.Dock = DockStyle.Fill;
            l.AutoSize = false;
            l.Margin = new Padding(0);
            l.ForeColor = Theme.TextSecondary;
            l.Font = Theme.FontSmall;
            l.TextAlign = ContentAlignment.MiddleLeft;
            return l;
        }

        private void Collect()
        {
            state.Cfg.TranslateEnabled = chkOn.Checked;
            state.Cfg.TranslateProvider = LexiconForm.ProviderKey(cboProvider.SelectedItem);
            state.Cfg.TranslateAppId = txtAppId.Text.Trim();
            state.Cfg.TranslateKey = txtKey.Text.Trim();
            state.Cfg.TranslateCustomUrl = txtUrl.Text.Trim();
            state.Cfg.TranslateAutoAdd = chkAdd.Checked;
            state.Cfg.ApplyToTranslator();
        }

        private void Save()
        {
            Collect();
            state.Cfg.Save();
            lblStatus.Text = "已保存到 " + Config.ConfigPath;
        }

        private void TestTranslate()
        {
            Collect();
            Translation.ClearCache();
            Cursor = Cursors.WaitCursor;
            string sample = "缓存命中率";
            string en = Translation.Translate(sample);
            Cursor = Cursors.Default;
            lblStatus.Text = en.Length > 0
                ? ("测试成功：" + sample + " → " + en)
                : ("测试失败：" + (Translation.LastError.Length > 0 ? Translation.LastError : "无返回（检查网络/密钥/额度）"));
        }
    }
}