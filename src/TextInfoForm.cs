using System;
using System.Drawing;
using System.Windows.Forms;

namespace VarNamer
{
    // 通用文本说明窗（可滚动、可复制全文）
    public class TextInfoForm : ModernForm
    {
        private TextBox box;
        private string fullText;

        public TextInfoForm(string title, string subTitle, string text)
            : this(title, subTitle, text, null, null)
        {
        }

        // linkUrl 非空时，底部会多一个可点击的跳转入口（用于作者 / 项目主页）
        public TextInfoForm(string title, string subTitle, string text, string linkUrl, string linkText)
        {
            fullText = text == null ? "" : text;
            Text = "VarNamer  " + title;
            Size = new Size(Theme.S(720), Theme.S(620));
            MinimumSize = new Size(Theme.S(560), Theme.S(420));
            LblSub.Text = "v" + AppVersion.Value + (string.IsNullOrEmpty(subTitle) ? "" : (" · " + subTitle));
            StartPosition = FormStartPosition.CenterParent;

            TableLayoutPanel rootGrid = new TableLayoutPanel();
            rootGrid.Dock = DockStyle.Fill;
            rootGrid.BackColor = Theme.Bg;
            rootGrid.ColumnCount = 1;
            rootGrid.RowCount = 2;
            rootGrid.Padding = new Padding(Theme.S(14), Theme.S(4), Theme.S(14), Theme.S(12));
            rootGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            rootGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.S(42)));
            Content.Controls.Add(rootGrid);

            CardPanel card = new CardPanel();
            card.Title = title;
            card.Hint = "可滚动 · 可全选复制";
            card.Dock = DockStyle.Fill;
            card.Margin = new Padding(0);
            card.Padding = new Padding(Theme.S(12), Theme.S(32), Theme.S(12), Theme.S(10));

            box = new TextBox();
            box.Dock = DockStyle.Fill;
            box.Multiline = true;
            box.ReadOnly = true;
            box.ScrollBars = ScrollBars.Both;
            box.WordWrap = false;            // 说明里有对齐表格，不换行更好读
            box.BorderStyle = BorderStyle.None;
            box.BackColor = Theme.Surface;
            box.ForeColor = Theme.TextPrimary;
            box.Font = new Font("Microsoft YaHei UI", 9.5f);
            box.Text = fullText;
            box.Select(0, 0);
            card.Controls.Add(box);
            rootGrid.Controls.Add(card, 0, 0);

            FlowLayoutPanel btns = new FlowLayoutPanel();
            btns.Dock = DockStyle.Fill;
            btns.WrapContents = false;
            btns.BackColor = Theme.Bg;
            // 跳转入口（放最左，先出现）
            if (!string.IsNullOrEmpty(linkUrl))
            {
                string finalUrl = linkUrl;
                FlatButton btnLink = new FlatButton();
                btnLink.Text = string.IsNullOrEmpty(linkText) ? linkUrl : linkText;
                btnLink.Tonal = true;
                btnLink.Font = Theme.FontCaption;
                btnLink.ForeColor = Theme.Accent;
                btnLink.Size = new Size(Theme.S(24) + TextRenderer.MeasureText(btnLink.Text, Theme.FontCaption).Width, Theme.S(30));
                btnLink.Margin = new Padding(0, Theme.S(4), Theme.S(12), 0);
                ToolTip lt = new ToolTip();
                lt.SetToolTip(btnLink, "在浏览器中打开：" + finalUrl);
                btnLink.Click += delegate(object s, EventArgs e)
                {
                    try { System.Diagnostics.Process.Start(finalUrl); }
                    catch (Exception ex) { MessageBox.Show("打开链接失败：" + ex.Message, "VarNamer", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
                };
                btns.Controls.Add(btnLink);
            }

            FlatButton btnCopy = new FlatButton();
            btnCopy.Text = "复制全文";
            btnCopy.Size = new Size(Theme.S(104), Theme.S(30));
            btnCopy.Margin = new Padding(0, Theme.S(4), Theme.S(8), 0);
            btnCopy.Click += delegate(object s, EventArgs e)
            {
                if (Clip.SetText(fullText)) LblSub.Text = "已复制全文";
            };
            btns.Controls.Add(btnCopy);

            FlatButton btnClose = new FlatButton();
            btnClose.Text = "关闭";
            btnClose.Primary = true;
            btnClose.Size = new Size(Theme.S(88), Theme.S(30));
            btnClose.Margin = new Padding(0, Theme.S(4), 0, 0);
            btnClose.Click += delegate(object s, EventArgs e) { Close(); };
            btns.Controls.Add(btnClose);
            rootGrid.Controls.Add(btns, 0, 1);
        }
    }
}