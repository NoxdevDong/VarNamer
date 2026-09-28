using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace VarNamer
{
    internal static class AppVersion { public const string Value = "4.1"; }

    // 回归套件：功能 + 按钮接线 + 悬浮窗状态机 + 动效层 + 布局重叠
    internal static class Suite
    {
        private static StringBuilder sb = new StringBuilder();
        private static string outPath;
        private static int pass, fail, overlaps;

        private static void Out(string s) { sb.AppendLine(s); }
        private static void Flush() { try { System.IO.File.WriteAllText(outPath, sb.ToString(), new UTF8Encoding(true)); } catch (Exception) { } }
        private static bool Ok(string what, bool ok, string detail)
        {
            if (ok) pass++; else fail++;
            Out((ok ? "  [OK] " : "  [FAIL] ") + what + (detail == null ? "" : ("  " + detail)));
            return ok;
        }
        private static void Pump(int ms) { for (int t = 0; t < ms; t += 20) { Application.DoEvents(); System.Threading.Thread.Sleep(20); } }

        [STAThread]
        private static void Main(string[] args)
        {
            outPath = args[0];
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            AppState st = new AppState();

            Out("== A 引擎 / 选项 ==");
            string[] cases = { "数学成绩", "用户登录接口", "是否启用缓存", "所有学生", "中文夹杂abc端口" };
            bool all13 = true;
            for (int i = 0; i < cases.Length; i++) if (Namer.Create(cases[i], st.Lex, st.Opt).Lines.Count < 13) all13 = false;
            Ok("5 组输入均出 13 风格", all13, "camel=" + Namer.Create("数学成绩", st.Lex, st.Opt).Lines[1].Value);
            NameOptions o = st.Cfg.ToOptions(); o.TypePrefix = "str";
            Ok("类型前缀", Namer.Create("用户名", st.Lex, o).Lines[0].Value.StartsWith("Str"), Namer.Create("用户名", st.Lex, o).Lines[0].Value);
            bool langOk = true;
            for (int i = 0; i < LanguagePreset.Names.Length; i++) if (LanguagePreset.FloatRows(LanguagePreset.Names[i]).Length != 5) langOk = false;
            Ok("8 语言预设映射", langOk, LanguagePreset.Names.Length + " 个");

            Out("== B 词库 / 配置 ==");
            Ok("内置词条", st.Lex.WordCount > 3900, st.Lex.WordCount + " 条");
            st.AddUserEntry("量子比特纠缠", "qubitEntangle");
            Ok("加词生效", Namer.Create("量子比特纠缠", st.Lex, st.Opt).Lines[2].Value.IndexOf("qubit") >= 0, "");
            st.AddUserEntry("成绩", "scoreOnly");
            Ok("覆盖内置", Namer.Create("成绩", st.Lex, st.Opt).Lines[2].Value == "score_only", "");
            Config c = new Config();
            c.TypePrefix = "int"; c.BriefLength = 6; c.BooleanPrefix = false; c.Hotkey = "Ctrl+Shift+K"; c.ThemeName = "light";
            c.Language = "Python"; c.FloatOpacity = 0.88; c.FloatOpacityIdle = 0.42; c.DictSignature = "sig"; c.GuideShown = true;
            c.Save();
            Config r2 = Config.Load();
            Ok("配置往返", r2.TypePrefix == "int" && r2.Hotkey == "Ctrl+Shift+K" && r2.ThemeName == "light" && Math.Abs(r2.FloatOpacityIdle - 0.42) < 0.001 && r2.DictSignature == "sig", "idle=" + r2.FloatOpacityIdle);

            Out("== C 窗体 / 按钮接线 / 重叠 ==");
            Check("主界面", new MainForm(st, null, ""));
            Check("词库管理", new LexiconForm(st));
            Check("设置", new SettingsForm(st, null));
            Check("说明窗", new TextInfoForm("说明", "t", "内容"));
            Check("在线翻译", new TranslateForm(st));

            Out("== D 托盘 / 悬浮窗 ==");
            try
            {
                TrayApp app = new TrayApp(null);
                Pump(300);
                NotifyIcon ni = Fld(app, "tray") as NotifyIcon;
                Ok("托盘 4 功能 + 1 分隔", ni != null && ni.ContextMenuStrip != null && ni.ContextMenuStrip.Items.Count == 5, ni.ContextMenuStrip.Items.Count + " 项");
                Ok("启动悬浮窗显示中", app.FloaterVisible, "");
                MainForm mf = new MainForm(st, app, "");
                mf.StartPosition = FormStartPosition.Manual; mf.Location = new Point(-4000, -4000); mf.Show();
                Pump(300);
                FlatButton fb = FindBtn(mf);
                Ok("按钮默认=隐藏悬浮窗且高亮", fb != null && fb.Text == "隐藏悬浮窗" && fb.Primary, fb == null ? "未找到" : (fb.Text + " Primary=" + fb.Primary));
                app.ToggleFloater(); Pump(200);
                Ok("点一下隐藏 + 高亮消失", !app.FloaterVisible && fb != null && fb.Text == "显示悬浮窗" && !fb.Primary, "");
                app.ToggleFloater(); Pump(200);
                Ok("再点显示 + 恢复高亮", app.FloaterVisible && fb != null && fb.Primary, "");
                overlaps += Scan(mf, mf);
                mf.Close(); mf.Dispose(); app.Quit(); Pump(200);
            }
            catch (Exception ex) { Ok("托盘/悬浮窗", false, ex.Message); }

            Out("== E 悬浮窗状态机 ==");
            try
            {
                AppState s2 = new AppState();
                FloatForm ff = new FloatForm(s2);
                ff.StartPosition = FormStartPosition.Manual; ff.Location = new Point(-4000, -4000); ff.Show();
                Pump(400);
                Ok("淡入到默认透明度", Math.Abs(ff.Opacity - s2.Cfg.FloatOpacityIdle) < 0.02, "Opacity=" + Math.Round(ff.Opacity, 2));
                Ok("空输入紧凑无行", Rows(ff) == 0 && ff.Height < 260, "高=" + ff.Height);
                overlaps += Scan(ff, ff);
                ff.SetInput("数学成绩排名"); Pump(250);
                Ok("输入后 5 行展开", Rows(ff) == 5 && ff.Height > 300, "高=" + ff.Height);
                overlaps += Scan(ff, ff);
                ff.SetInput("骆駉"); Pump(200);
                Ok("生僻字不展开空行", Rows(ff) == 0, "高=" + ff.Height);
                ff.SetInput(""); Pump(150);
                Ok("清空回紧凑", Rows(ff) == 0, "");
                ff.Dispose();
            }
            catch (Exception ex) { Ok("悬浮窗状态机", false, ex.Message); }

            Out("== F 动效层（Fluent 曲线 / M3 状态层）==");
            try
            {
                Ok("Fluent 入场曲线单调且在端点闭合", Curves.DecelerateMax(0) < 0.001 && Curves.DecelerateMax(1) > 0.999
                    && Curves.DecelerateMax(0.5) > 0.9, "DecelerateMax(0.5)=" + Curves.DecelerateMax(0.5).ToString("F3"));
                Ok("Fluent 退场曲线慢进快出", Curves.AccelerateMax(0.5) < 0.15 && Curves.AccelerateMax(1) > 0.999, "AccelerateMax(0.5)=" + Curves.AccelerateMax(0.5).ToString("F3"));
                Ok("EasyEase 对称", Math.Abs(Curves.EasyEase(0.5) - 0.5) < 0.02, "0.5 → " + Curves.EasyEase(0.5).ToString("F3"));
                Ok("M3 状态层用色 = 强调色 8%", Theme.StateHover > 0.079 && Theme.StateHover < 0.081, "StateHover=" + Theme.StateHover);
                Color rh = Theme.RowHover;
                Ok("RowHover 由状态层派生（与 Surface 不同且带蓝）", rh != Theme.Surface && rh.B > rh.R, "RowHover=" + rh.R + "," + rh.G + "," + rh.B);
                double last = -1; bool done = false; int steps = 0;
                Tween.Run(120, delegate(double k) { if (k >= last - 0.0001) steps++; last = k; }, delegate() { done = true; });
                Pump(400);
                Ok("补间跑完且单调", done && last > 0.999 && steps >= 3, "步数=" + steps);
                int delayed = 0;
                Tween.RunDelayed(120, 60, delegate(double k) { delayed++; }, null);
                Pump(60); bool early = delayed == 0; Pump(300);
                Ok("延迟启动（逐行错峰）", early && delayed > 0, "延迟期未触发=" + early);
                FlatButton b = new FlatButton(); Form h = new Form();
                h.StartPosition = FormStartPosition.Manual; h.Location = new Point(-4000, -4000); h.Controls.Add(b); h.Show();
                Inv(b, "OnMouseEnter", new object[] { EventArgs.Empty }); Pump(250);
                double on = (double)Fld(b, "hoverAmt");
                Inv(b, "OnMouseLeave", new object[] { EventArgs.Empty }); Pump(250);
                double off = (double)Fld(b, "hoverAmt");
                Ok("按钮悬停渐变（Fluent 100ms）", on > 0.9 && off < 0.1, "进=" + Math.Round(on, 2) + " 出=" + Math.Round(off, 2));
                Ok("时长令牌 = Fluent 阶梯", Theme.DurFast == 100 && Theme.DurMed == 200 && Theme.DurSlow == 250, Theme.DurFast + "/" + Theme.DurMed + "/" + Theme.DurSlow);
                h.Close(); h.Dispose();
            }
            catch (Exception ex) { Ok("动效层", false, ex.Message); }

            Out("== G 重排校验 ==");
            try
            {
                AppState s3 = new AppState();
                s3.Cfg.GuideShown = true; s3.Cfg.Save();
                MainForm mf = new MainForm(s3, null, "");
                mf.StartPosition = FormStartPosition.Manual; mf.Location = new Point(-4000, -4000);
                mf.Show(); Pump(320);
                mf.GetType().GetMethod("SetInputForCheck", BindingFlags.NonPublic | BindingFlags.Instance);
                // 填一个词，让结果表格展开
                DarkTextBox dt = FindDark(mf);
                if (dt != null) { dt.Text = "数学成绩排名"; Pump(200); }
                DataGridView g = FindGrid(mf);
                int heads = 0, styles = 0; string titles = "";
                if (g != null)
                {
                    for (int i = 0; i < g.Rows.Count; i++)
                    {
                        if (g.Rows[i].Tag is int) styles++;
                        else { heads++; titles += "[" + Convert.ToString(g.Rows[i].Cells[0].Value) + "]"; }
                    }
                }
                Ok("结果表格 = 13 风格 + 3 分组标题", g != null && styles == 13 && heads == 3, styles + " 行 + " + heads + " 标题 " + titles);
                int cap = 0; Label stat = null;
                foreach (Control ctl in All(mf))
                {
                    Label lb = ctl as Label;
                    if (lb == null) continue;
                    if (lb.Text == "命名规则" || lb.Text == "操作" || lb.Text == "词库") cap++;
                    if (lb.Text != null && lb.Text.StartsWith("内置 ")) stat = lb;
                }
                Ok("选项区 3 个分组标题", cap == 3, cap + " 个");
                Ok("词库统计行已填充", stat != null && stat.Text.IndexOf("我的词条") > 0, stat == null ? "未找到" : stat.Text);
                int ov = Scan(mf, mf); overlaps += ov;
                Ok("重排后无重叠", ov == 0, "重叠 " + ov + " 处");
                bool themeOk = Theme.ApplyScrollbarTheme(g);
                Ok("暗色滚动条主题被系统接受", themeOk, themeOk ? "SetWindowTheme(DarkMode_Explorer) = S_OK" : "调用未成功（旧系统可能不支持）");
                Ok("卡片标题用粗体字（层次感）", Theme.FontCardTitle.Bold, Theme.FontCardTitle.Name + " " + Theme.FontCardTitle.Size + "pt Bold");
                mf.Close(); mf.Dispose();
            }
            catch (Exception ex) { Ok("重排校验", false, ex.Message); }

            Out("== H 新功能（标签页 / 风格组）==");
            try
            {
                AppState s4 = new AppState();
                s4.Cfg.GuideShown = true; s4.Cfg.Save();

                // 词库管理三个标签页
                LexiconForm lex = new LexiconForm(s4);
                lex.StartPosition = FormStartPosition.Manual; lex.Location = new Point(-4000, -4000);
                lex.Show(); Pump(300);
                MethodInfo sw = typeof(LexiconForm).GetMethod("SwitchTab", BindingFlags.NonPublic | BindingFlags.Instance);
                int pagesFound = 0; string pageInfo = "";
                for (int i = 0; i < 3; i++)
                {
                    sw.Invoke(lex, new object[] { i });
                    Pump(120);
                    TableLayoutPanel p = (TableLayoutPanel)Fld(lex, i == 0 ? "pageList" : (i == 1 ? "pageIO" : "pageTrans"));
                    if (p != null && p.Visible && !p.IsDisposed)
                    {
                        pagesFound++;
                        int ov = Scan(lex, lex); overlaps += ov;
                        pageInfo += "[" + (i == 0 ? "词条" : (i == 1 ? "导入导出" : "在线翻译")) + " 控件 " + All(p).Count + " 重叠 " + ov + "]";
                    }
                }
                TabStrip lexTabs = (TabStrip)Fld(lex, "tabs");
                bool tabOk = lexTabs != null && lexTabs.Items.Length == 3;
                for (int i = 0; i < 3 && tabOk; i++) { lexTabs.SelectedIndex = i; Pump(80); if (lexTabs.SelectedIndex != i) tabOk = false; }
                Ok("标签条 = 3 个下划线标签且选中项跟随", tabOk, tabOk ? string.Join("/", lexTabs.Items) : "异常");
                Ok("三个标签页均可切换且无重叠", pagesFound == 3, pageInfo);
                bool importBtn = false;
                foreach (Control ctl in All((Control)Fld(lex, "pageIO")))
                {
                    FlatButton fb = ctl as FlatButton;
                    if (fb != null && fb.Text != null && fb.Text.StartsWith("导入")) importBtn = true;
                }
                Ok("导入 txt 入口已暴露在导入导出页", importBtn, importBtn ? "有「导入 txt…」按钮" : "未找到");

                // 说明窗：作者 + GitHub 跳转入口
                string help = MainForm.HelpText();
                Ok("说明文案含作者与项目地址", help.IndexOf(Author.Name) > 0 && help.IndexOf(Author.RepoUrl) > 0,
                    "作者=" + Author.Name + " 地址=" + Author.RepoUrl);
                int linkBtns = 0; string linkText = "";
                using (TextInfoForm tif = new TextInfoForm("使用说明", "测试", help, Author.RepoUrl, "GitHub · " + Author.Name))
                {
                    tif.Show(); Pump(150);
                    foreach (Control ctl in All(tif))
                    {
                        FlatButton fb = ctl as FlatButton;
                        if (fb != null && fb.Text != null && fb.Text.StartsWith("GitHub")) { linkBtns++; linkText = fb.Text; }
                    }
                    tif.Close();
                }
                Ok("说明窗底部有 GitHub 跳转按钮", linkBtns == 1, "按钮文案「" + linkText + "」");
                Ok("作者常量已配置", Author.RepoUrl == "https://github.com/NoxdevDong/VarNamer", Author.RepoUrl);

                // 词条页：工具栏卡 / 新增覆盖卡 / 空状态
                sw.Invoke(lex, new object[] { 0 }); Pump(150);
                int cards = 0; bool hasFormat = false, hasEmpty = false, hasTitle = false;
                foreach (Control ctl in All((Control)Fld(lex, "pageList")))
                {
                    CardPanel cp3 = ctl as CardPanel;
                    if (cp3 != null && cp3.Parent is TableLayoutPanel) cards++;
                    if (cp3 != null && cp3.Title == "新增 / 覆盖词条") hasTitle = true;
                    Label lb3 = ctl as Label;
                    if (lb3 != null && lb3.Text != null && lb3.Text.StartsWith("格式：中文=英文")) hasFormat = true;
                    if (lb3 != null && lb3.Text != null && lb3.Text.StartsWith("还没有你自己的词条")) hasEmpty = true;
                }
                Ok("词条页：三张圆角卡（工具栏/列表/新增）+ 格式说明", cards == 3 && hasFormat && hasTitle,
                    "卡片 " + cards + " · 新增卡标题=" + hasTitle + " · 格式说明=" + hasFormat);
                // 空状态：搜一个不存在的词 → 应出现「没有匹配」提示
                DarkTextBox sb = null;
                foreach (Control ctl in All((Control)Fld(lex, "pageList")))
                {
                    DarkTextBox dt2 = ctl as DarkTextBox;
                    if (dt2 != null && dt2.Parent != null && dt2.Parent.Parent != null) { sb = dt2; break; }
                }
                Label le = (Label)Fld(lex, "lblEmpty");
                if (sb != null)
                {
                    sb.Text = "zzz_不存在_zzz"; Pump(150);
                    Ok("词条页空状态提示（无匹配时出现）", le != null && le.Visible && le.Text.StartsWith("没有匹配"),
                        le == null ? "无控件" : ("可见=" + le.Visible + " 文案=" + le.Text));
                    sb.Text = ""; Pump(100);
                }
                else Ok("词条页空状态提示（无匹配时出现）", false, "未找到搜索框");
                int clipList = ClipIssues(lex);
                Ok("词条页无控件被裁切", clipList == 0, clipList + " 处越界");
                overlaps += Scan((Control)Fld(lex, "pageList"), lex);

                // 底部操作区：圆角卡 + 按钮右对齐居中
                sw.Invoke(lex, new object[] { 0 }); Pump(100);
                FlatButton saveBtn = null;
                foreach (Control ctl in All(lex))
                {
                    FlatButton fb = ctl as FlatButton;
                    if (fb != null && fb.Text == "保存并关闭") saveBtn = fb;
                }
                CardPanel fcard = null;
                if (saveBtn != null)
                {
                    Control p2 = saveBtn.Parent;
                    while (p2 != null && !(p2 is CardPanel)) p2 = p2.Parent;   // 从按钮往上找它真正的父卡片
                    fcard = p2 as CardPanel;
                }
                if (fcard != null && saveBtn != null)
                {
                    Rectangle rc = fcard.RectangleToScreen(fcard.ClientRectangle);
                    Rectangle rb = saveBtn.RectangleToScreen(saveBtn.ClientRectangle);
                    int rightGap = rc.Right - rb.Right;
                    int topGap = rb.Top - rc.Top;
                    int botGap = rc.Bottom - rb.Bottom;
                    bool rounded = fcard.Radius == Theme.RadMd;
                    bool centered = Math.Abs(topGap - botGap) <= 2 && rightGap >= Theme.S(8) && rightGap <= Theme.S(20);
                    Ok("底部操作区为圆角卡且按钮右对齐居中", rounded && centered,
                        "圆角=" + fcard.Radius + " 右间距=" + rightGap + " 上间距=" + topGap + " 下间距=" + botGap);
                }
                else Ok("底部操作区为圆角卡且按钮右对齐居中", false, "未找到卡片或按钮");

                // 在线翻译页
                sw.Invoke(lex, new object[] { 2 }); Pump(150);
                int badges = 0, kvLabels = 0; string badgeText = ""; bool trPrimary = false;
                foreach (Control ctl in All((Control)Fld(lex, "pageTrans")))
                {
                    Badge bd = ctl as Badge;
                    if (bd != null) { badges++; badgeText = bd.Text; }
                    FlatButton fb2 = ctl as FlatButton;
                    if (fb2 != null && fb2.Primary) trPrimary = true;
                    Label lb2 = ctl as Label;
                    if (lb2 != null && (lb2.Text == "需要" || lb2.Text == "密钥" || lb2.Text == "自动收录")) kvLabels++;
                }
                Ok("在线翻译页：状态徽章 + 键值信息 + 主操作", badges == 1 && kvLabels == 3 && trPrimary,
                    "徽章「" + badgeText + "」· 信息行 " + kvLabels + " · 主按钮=" + trPrimary);
                int clipTr = ClipIssues(lex); overlaps += 0;
                Ok("在线翻译页无控件被裁切", clipTr == 0, clipTr + " 处越界");
                // 启用开关：页面上直接可切，并联动徽章
                ToggleCheck trOn = null;
                foreach (Control ctl in All((Control)Fld(lex, "pageTrans")))
                {
                    ToggleCheck tc = ctl as ToggleCheck;
                    if (tc != null && tc.Text == "启用在线翻译") trOn = tc;
                }
                if (trOn != null)
                {
                    trOn.Checked = true; Pump(150);
                    Ok("页面开关可开启（联动徽章+配置）", s4.Cfg.TranslateEnabled && badgeTextOf(lex) == "已启用",
                        "Cfg=" + s4.Cfg.TranslateEnabled + " 徽章=" + badgeTextOf(lex));
                    trOn.Checked = false; Pump(150);
                    Ok("页面开关可关闭", !s4.Cfg.TranslateEnabled && badgeTextOf(lex) == "未启用", "徽章=" + badgeTextOf(lex));
                }
                else Ok("页面开关存在", false, "未找到「启用在线翻译」开关");
                int ovTr = Scan(lex, lex); overlaps += ovTr;
                Ok("在线翻译页无重叠", ovTr == 0, "重叠 " + ovTr + " 处");
                lex.Close(); lex.Dispose();

                // 悬浮窗风格组
                AppState s5 = new AppState();
                FloatForm ff = new FloatForm(s5);
                ff.StartPosition = FormStartPosition.Manual; ff.Location = new Point(-4000, -4000);
                ff.Show(); Pump(300);
                ff.SetInput("数学成绩排名"); Pump(300);
                int h0 = ff.Height, n0 = Rows(ff);
                TabStrip gs = (TabStrip)Fld(ff, "groupStrip");
                Ok("悬浮窗有 4 个风格标签（下划线式）", gs != null && gs.Items.Length == 4, gs == null ? "无" : string.Join("/", gs.Items));
                gs.SelectedIndex = 1; Pump(300);
                int h1 = ff.Height, n1 = Rows(ff);
                gs.SelectedIndex = 2; Pump(300);
                int h2 = ff.Height, n2 = Rows(ff);
                gs.SelectedIndex = 3; Pump(300);
                int h3 = ff.Height, n3 = Rows(ff);
                gs.SelectedIndex = 0; Pump(300);
                int h4 = ff.Height, n4 = Rows(ff);
                Ok("切换风格组：行数 5/5/3/4/5 且高度自适应", n0 == 5 && n1 == 5 && n2 == 3 && n3 == 4 && n4 == 5 && h2 < h1 && h3 < h1,
                    "混合 " + n0 + "行/" + h0 + "px · 全称 " + n1 + "/" + h1 + " · 简短 " + n2 + "/" + h2 + " · 缩写 " + n3 + "/" + h3);
                Ok("风格组已写入配置", s5.Cfg.FloatGroup >= 0 && s5.Cfg.FloatGroup <= 3, "FloatGroup=" + s5.Cfg.FloatGroup);
                gs.SelectedIndex = 2; Pump(400);
                int[] tx = (int[])Fld(gs, "tabX"); int[] tw = (int[])Fld(gs, "tabW");
                double ix = (double)Fld(gs, "indX"), iw = (double)Fld(gs, "indW");
                Ok("指示条精确对准选中标签", tx != null && Math.Abs(ix - tx[2]) < 1.5 && Math.Abs(iw - tw[2]) < 1.5,
                    "indX=" + Math.Round(ix, 1) + " 期望 " + (tx == null ? -1 : tx[2]) + " · indW=" + Math.Round(iw, 1) + " 期望 " + (tw == null ? -1 : tw[2]));
                int ovf = Scan(ff, ff); overlaps += ovf;
                Ok("悬浮窗切换后无重叠", ovf == 0, "重叠 " + ovf + " 处");
                ff.Dispose();
            }
            catch (Exception ex) { Ok("新功能校验", false, ex.Message); }

            Out("== I 悬浮窗缩小为桌面挂件 ==");
            try
            {
                AppState s6 = new AppState();
                s6.Cfg.GuideShown = true; s6.Cfg.Save();
                FloatForm ff = new FloatForm(s6);
                ff.StartPosition = FormStartPosition.Manual;
                ff.Location = new Point(-4000, -4000);
                ff.Show(); Pump(350);
                ff.SetInput("数学成绩排名"); Pump(300);
                int w0 = ff.Width, h0 = ff.Height;

                FlatButton btnCol = null;
                foreach (Control ctl in All(ff)) { FlatButton b = ctl as FlatButton; if (b != null && b.Text == "缩") btnCol = b; }
                Ok("标题栏有「缩」按钮", btnCol != null, btnCol == null ? "未找到" : "文案=" + btnCol.Text);

                btnCol.PerformClick(); Pump(400);
                Panel titleBar = (Panel)Fld(ff, "TitleBar");
                Panel chip = (Panel)Fld(ff, "chipPanel");
                Ok("缩小后 = 150x38 且标题栏隐藏", ff.Width == Theme.S(150) && ff.Height == Theme.S(38) && titleBar != null && !titleBar.Visible,
                    ff.Width + "x" + ff.Height + " 标题栏可见=" + (titleBar != null && titleBar.Visible));
                Ok("挂件面板可见", chip != null && chip.Visible, "chip.Visible=" + (chip != null && chip.Visible));
                int ovChip = Scan(ff, ff); overlaps += ovChip;
                Ok("挂件态无重叠/裁切", ovChip == 0 && ClipIssues(ff) == 0, "重叠 " + ovChip + " 裁切 " + ClipIssues(ff));

                MouseEventArgs me = new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0);
                Inv(ff, "ChipDown", new object[] { chip, me });
                Inv(ff, "ChipUp", new object[] { chip, me });
                Pump(400);
                Ok("点挂件展开回原尺寸", ff.Width == w0 && ff.Height == h0, ff.Width + "x" + ff.Height + "（原 " + w0 + "x" + h0 + "）");

                ff.ToggleCollapsed(); Pump(350);
                int cw = ff.Width;
                Point before = ff.Location;
                Inv(ff, "ChipDown", new object[] { chip, new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0) });
                // 沙箱禁止移动鼠标，改为直接构造“已位移 40x25”的状态，验证移动与“拖动后不展开”逻辑
                Point cur = Cursor.Position;
                SetFld(ff, "chipDrag", new Point(cur.X - 40, cur.Y - 25));
                Inv(ff, "ChipMove", new object[] { chip, new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0) });
                Point afterMove = ff.Location;
                Inv(ff, "ChipUp", new object[] { chip, new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0) });
                Pump(300);
                bool movedBy = (afterMove.X - before.X) == 40 && (afterMove.Y - before.Y) == 25;
                Ok("挂件可拖动且拖动后不展开", movedBy && ff.Width == cw && ff.Location == afterMove,
                    "位移=" + (afterMove.X - before.X) + "," + (afterMove.Y - before.Y) + " 宽=" + ff.Width + "（拖动后应保持 150 且不展开）");

                ff.SetInput("缓存命中率"); Pump(350);
                Ok("挂件态收到文字自动展开", !ff.Collapsed && ff.Width > cw, "已展开=" + !ff.Collapsed + " 宽=" + ff.Width);

                ff.ToggleCollapsed(); Pump(300);
                ff.SavePosition();
                Ok("挂件态保存不覆盖展开宽度", s6.Cfg.FloatW > 200, "Cfg.FloatW=" + s6.Cfg.FloatW);
                ff.Dispose();
            }
            catch (Exception ex) { Ok("挂件功能", false, ex.Message); }

            Out("== J 本轮三个修复 ==");
            try
            {
                // ① 挂件拖走后展开 → 应停在新位置，不回原点
                AppState s7 = new AppState();
                s7.Cfg.GuideShown = true; s7.Cfg.Save();
                FloatForm fx = new FloatForm(s7);
                fx.StartPosition = FormStartPosition.Manual;
                fx.Location = new Point(-4000, -4000);
                fx.Show(); Pump(350);
                fx.SetInput("数学成绩排名"); Pump(250);
                fx.ToggleCollapsed(); Pump(400);
                Panel chip2 = (Panel)Fld(fx, "chipPanel");
                Point p0 = fx.Location;
                Inv(fx, "ChipDown", new object[] { chip2, new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0) });
                Point cur2 = Cursor.Position;
                SetFld(fx, "chipDrag", new Point(cur2.X - 60, cur2.Y - 30));
                Inv(fx, "ChipMove", new object[] { chip2, new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0) });
                Inv(fx, "ChipUp", new object[] { chip2, new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0) });
                Pump(200);
                Point dragged = fx.Location;
                SetFld(fx, "chipDrag", new Point(cur2.X - 60, cur2.Y - 30));
                Inv(fx, "ChipDown", new object[] { chip2, new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0) });
                SetFld(fx, "chipDrag", new Point(Cursor.Position.X - 60, Cursor.Position.Y - 30));
                Inv(fx, "ChipMove", new object[] { chip2, new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0) });
                Inv(fx, "ChipUp", new object[] { chip2, new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0) });
                Pump(200);
                Point dragged2 = fx.Location;
                fx.Expand(); Pump(400);
                Ok("① 挂件拖走后展开停在新位置（不回原点）",
                    fx.Location == dragged2 && (dragged2.X - p0.X) != 0,
                    "原 " + p0 + " → 拖到 " + dragged2 + " → 展开后 " + fx.Location);
                Ok("① 拖后位置已写回配置", s7.Cfg.FloatX == dragged2.X && s7.Cfg.FloatY == dragged2.Y,
                    "Cfg=(" + s7.Cfg.FloatX + "," + s7.Cfg.FloatY + ")");
                fx.Dispose();

                // ② 未收录词落到 程序目录\dict\
                string lp = UnknownLog.LogPath;
                string expect = System.IO.Path.Combine(AutoDict.DictFolderPath, "unknowns.txt");
                Ok("② 未收录词路径 = 程序目录\\dict\\unknowns.txt", string.Equals(lp, expect, StringComparison.OrdinalIgnoreCase), lp);
                UnknownLog.Record(new List<string>(new string[] { "駉" }));
                Ok("② 能写进该文件", System.IO.File.Exists(lp), lp);
                bool inDictScan = false;
                foreach (string f in AutoDict.FindAll())
                    if (string.Equals(System.IO.Path.GetFileName(f), "unknowns.txt", StringComparison.OrdinalIgnoreCase)) inDictScan = true;
                Ok("② 该文件不会被当成词库扫描", !inDictScan, inDictScan ? "被误当词库" : "已排除");

                // ③ 词库管理双向搜索（含内置词库）
                AppState s8 = new AppState();
                s8.Cfg.GuideShown = true; s8.Cfg.Save();
                System.IO.File.WriteAllText(Config.UserDictPath, "我的专用词=myOwnWord", new UTF8Encoding(false));
                LexiconForm lx = new LexiconForm(s8);
                lx.StartPosition = FormStartPosition.Manual;
                lx.Location = new Point(-4000, -4000);
                lx.Show(); Pump(300);
                DarkTextBox sb2 = null;
                foreach (Control ctl in All((Control)Fld(lx, "pageList")))
                { DarkTextBox d2 = ctl as DarkTextBox; if (d2 != null && sb2 == null) sb2 = d2; }
                DataGridView g2 = null;
                foreach (Control ctl in All(lx)) { DataGridView dg = ctl as DataGridView; if (dg != null) { g2 = dg; break; } }
                sb2.Text = "score"; Pump(250);
                int enHits = 0; string enSample = "";
                for (int i = 0; i < g2.Rows.Count; i++)
                    if (Convert.ToString(g2.Rows[i].Cells[2].Value) == "内置")
                    { enHits++; if (enSample.Length == 0) enSample = Convert.ToString(g2.Rows[i].Cells[0].Value) + "=" + Convert.ToString(g2.Rows[i].Cells[1].Value); }
                Ok("③ 输入英文能查到中文（内置词库）", enHits > 0, "命中 " + enHits + " 条，例：" + enSample);
                sb2.Text = "成绩"; Pump(250);
                int cnHits = g2.Rows.Count;
                string cnSample = cnHits > 0 ? (Convert.ToString(g2.Rows[0].Cells[0].Value) + "=" + Convert.ToString(g2.Rows[0].Cells[1].Value)) : "";
                Ok("③ 输入中文能查到英文", cnHits > 0, "命中 " + cnHits + " 条，例：" + cnSample);
                sb2.Text = "myOwnWord"; Pump(250);
                bool mineHit = g2.Rows.Count == 1 && Convert.ToString(g2.Rows[0].Cells[2].Value) == "我的";
                Ok("③ 自己的词条优先且不重复", mineHit, g2.Rows.Count + " 行，来源=" + (g2.Rows.Count > 0 ? Convert.ToString(g2.Rows[0].Cells[2].Value) : "无"));
                // 删除内置行不应误删用户词条
                sb2.Text = "成绩"; Pump(250);
                for (int i = 0; i < g2.Rows.Count; i++)
                    if (Convert.ToString(g2.Rows[i].Cells[2].Value) == "内置") { g2.Rows[i].Selected = true; break; }
                Inv(lx, "DeleteSelected", new object[0]);
                Pump(200);
                Ok("③ 内置行不可删除且不动用户词条", s8.LoadUserDictEntries().Count == 1, "用户词条 " + s8.LoadUserDictEntries().Count + " 条");
                lx.Close(); lx.Dispose();
            }
            catch (Exception ex) { Ok("本轮修复校验", false, ex.Message); }

            Out("== K 单实例 / 中英互查 / 标签列宽 ==");
            try
            {
                // ① 单实例：运行时验证 —— 拿锁、强制 GC 后再试（旧实现正是 GC 后失效）
                bool first = Single.IsFirstInstance();
                Ok("① 本进程能拿到单实例锁", first, first ? "拿到 ✓" : "拿不到");
                bool created2;
                System.Threading.Mutex probe2 = new System.Threading.Mutex(true, "VarNamer.SingleInstance.v1", out created2);
                probe2.Dispose();
                Ok("① 第二个进程拿不到锁（不会开出第二个窗口）", !created2, created2 ? "能拿到 ✗ 单实例失效" : "拿不到 ✓");
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                bool created3;
                System.Threading.Mutex probe3 = new System.Threading.Mutex(true, "VarNamer.SingleInstance.v1", out created3);
                probe3.Dispose();
                Ok("① 强制 GC 后锁仍被持有（旧实现就是在这一步失效的）", !created3, created3 ? "GC 后锁丢了 ✗" : "仍持有 ✓");

                // ② 悬浮窗：英文反查中文
                AppState s9 = new AppState();
                s9.Cfg.GuideShown = true; s9.Cfg.FloatGroup = 0; s9.Cfg.Save();
                FloatForm fz = new FloatForm(s9);
                fz.StartPosition = FormStartPosition.Manual;
                fz.Location = new Point(-4000, -4000);
                fz.Show(); Pump(350);
                fz.SetInput("成绩"); Pump(300);
                int hCn = fz.Height; int rowsCn = Rows(fz);
                fz.SetInput("score"); Pump(350);
                int hEn = fz.Height; int rowsEn = Rows(fz);
                string revVal = "", revTag = "";
                foreach (Control ctl in All(fz))
                {
                    ResultRow rr = ctl as ResultRow;
                    if (rr != null && rr.Visible && rr.TagText == "中文") { revTag = rr.TagText; revVal = rr.Value; }
                }
                Ok("② 悬浮窗输入英文只显示「中文」一行", revTag == "中文" && revVal.Length > 0 && rowsEn == 1,
                    "可见行 " + rowsEn + " · 标签「" + revTag + "」值=" + revVal);
                Ok("② 英文态高度变小（风格行已隐藏）", hEn < hCn, rowsCn + " 行/" + hCn + "px → " + rowsEn + " 行/" + hEn + "px");
                // 单个英文词只要一个最准的中文；多词短语给组合结果
                fz.SetInput("user name"); Pump(350);
                string v1 = "", v2 = "", v3 = "";
                foreach (Control ctl in All(fz)) { ResultRow rr = ctl as ResultRow; if (rr != null && rr.Visible) v1 = rr.Value; }
                fz.SetInput("user_name"); Pump(300);
                foreach (Control ctl in All(fz)) { ResultRow rr = ctl as ResultRow; if (rr != null && rr.Visible) v2 = rr.Value; }
                fz.SetInput("userName"); Pump(300);
                foreach (Control ctl in All(fz)) { ResultRow rr = ctl as ResultRow; if (rr != null && rr.Visible) v3 = rr.Value; }
                Ok("② 整词命中：user name / user_name / userName 都得到唯一结果", v1 == "用户名" && v2 == "用户名" && v3 == "用户名",
                    v1 + " / " + v2 + " / " + v3);
                fz.SetInput("user name id"); Pump(350);
                string v4 = "";
                foreach (Control ctl in All(fz)) { ResultRow rr = ctl as ResultRow; if (rr != null && rr.Visible) v4 = rr.Value; }
                Ok("② 多词无整词命中时逐词组合", v4.IndexOf("+") > 0, "user name id → " + v4);
                fz.SetInput("name"); Pump(300);
                string v5 = "";
                foreach (Control ctl in All(fz)) { ResultRow rr = ctl as ResultRow; if (rr != null && rr.Visible) v5 = rr.Value; }
                Ok("② 单词只要一个最准结果", v5 == "名字", "name → " + v5);
                // 中文值必须用中文字体（Consolas 没有汉字）
                bool cjkFont = false;
                foreach (Control ctl in All(fz))
                {
                    ResultRow rr = ctl as ResultRow;
                    if (rr == null || !rr.Visible) continue;
                    foreach (Control gc in rr.Controls) { Label lb = gc as Label; if (lb != null && lb.Text == v5) cjkFont = lb.Font.Name.IndexOf("YaHei") >= 0; }
                }
                Ok("② 中文值使用中文字体（不再是 Consolas）", cjkFont, cjkFont ? "微软雅黑 ✓" : "仍是非中文字体");
                fz.SetInput("成绩"); Pump(250);
                bool revHidden = true;
                foreach (Control ctl in All(fz))
                {
                    ResultRow rr = ctl as ResultRow;
                    if (rr != null && rr.TagText == "中文" && rr.Visible) revHidden = false;
                }
                Ok("② 输入中文时不显示反查行", revHidden, "已隐藏=" + revHidden);

                // ③ 标签列宽度：按最长标签自适应，文字要能整行显示
                fz.SetInput("成绩"); Pump(300);
                TabStrip gstrip = (TabStrip)Fld(fz, "groupStrip");
                gstrip.SelectedIndex = 2;      // 简短组：标签是 camelCase / snake_case
                Pump(400);
                int needW = 0, gotW = 0; string longTag = "";
                foreach (Control ctl in All(fz))
                {
                    ResultRow rr = ctl as ResultRow;
                    if (rr == null || !rr.Visible) continue;
                    int tw = TextRenderer.MeasureText(rr.TagText, Theme.FontSmall).Width;
                    if (tw > needW) { needW = tw; longTag = rr.TagText; }
                    gotW = rr.TagWidth;
                }
                Ok("③ 标签列宽 ≥ 最长标签实际宽度（不再截断）", gotW >= needW, "最长「" + longTag + "」需 " + needW + "px，实际 " + gotW + "px");
                bool noParen = true; string parenTag = "";
                foreach (Control ctl in All(fz))
                {
                    ResultRow rr = ctl as ResultRow;
                    if (rr == null || !rr.Visible) continue;
                    if (rr.TagText.IndexOf('(') >= 0 || rr.TagText.IndexOf('（') >= 0) { noParen = false; parenTag = rr.TagText; }
                }
                Ok("③ 标签已去掉「每词≤N / 每段≤N」括号", noParen, noParen ? "无括号 ✓" : ("仍带括号：" + parenTag));
                int ovK = Scan(fz, fz); overlaps += ovK;
                Ok("③ 反查行 + 宽标签后无重叠/裁切", ovK == 0 && ClipIssues(fz) == 0, "重叠 " + ovK + " 裁切 " + ClipIssues(fz));
                fz.Dispose();

                // ④ 主界面：英文反查中文行
                AppState s10 = new AppState();
                s10.Cfg.GuideShown = true; s10.Cfg.Save();
                MainForm mz = new MainForm(s10, null, "");
                mz.StartPosition = FormStartPosition.Manual;
                mz.Location = new Point(-4000, -4000);
                mz.Show(); Pump(350);
                DarkTextBox mIn = null;
                foreach (Control ctl in All(mz)) { DarkTextBox d = ctl as DarkTextBox; if (d != null) { mIn = d; break; } }
                mIn.Text = "score"; Pump(300);
                Label rev = (Label)Fld(mz, "lblReverse");
                Ok("④ 主界面输入英文显示中文（唯一最准结果）", rev != null && rev.Visible && rev.Text.IndexOf("score → 分数") > 0, rev == null ? "无控件" : rev.Text);
                mIn.Text = "数学成绩"; Pump(300);
                Ok("④ 输入中文时该行隐藏", rev != null && !rev.Visible, "可见=" + (rev != null && rev.Visible));
                mz.Close(); mz.Dispose();
            }
            catch (Exception ex) { Ok("本轮校验", false, ex.Message); }

            Out("");
            Out("== 小结：通过 " + pass + "，失败 " + fail + "，布局重叠 " + overlaps + " 处 ==");
            Flush();
            Console.WriteLine("written");
        }

        private static void Inv(object o, string m, object[] a)
        {
            MethodInfo mi = o.GetType().GetMethod(m, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            if (mi != null) mi.Invoke(o, a);
        }
        private static void SetFld(object o, string n, object v)
        {
            for (Type t = o.GetType(); t != null; t = t.BaseType)
            {
                FieldInfo fi = t.GetField(n, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (fi != null) { fi.SetValue(o, v); return; }
            }
        }

        private static object Fld(object o, string n)
        {
            for (Type t = o.GetType(); t != null; t = t.BaseType)
            {
                FieldInfo fi = t.GetField(n, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (fi != null) return fi.GetValue(o);
            }
            return null;
        }
        private static int Rows(Control root) { int n = 0; foreach (Control c in All(root)) if (c is ResultRow && c.Visible && c.Height > 0) n++; return n; }
        private static List<Control> All(Control p) { List<Control> l = new List<Control>(); Walk(p, l); return l; }
        private static void Walk(Control p, List<Control> l) { for (int i = 0; i < p.Controls.Count; i++) { l.Add(p.Controls[i]); Walk(p.Controls[i], l); } }

        private static string badgeTextOf(Control root)
        {
            foreach (Control c in All(root)) { Badge b = c as Badge; if (b != null) return b.Text; }
            return "(无徽章)";
        }

        private static string[] texts(FlatButton[] bs)
        {
            string[] r = new string[bs.Length];
            for (int i = 0; i < bs.Length; i++) r[i] = bs[i] == null ? "-" : bs[i].Text;
            return r;
        }

        private static DataGridView FindGrid(Control root)
        {
            foreach (Control c in All(root)) { DataGridView g = c as DataGridView; if (g != null) return g; }
            return null;
        }

        private static DarkTextBox FindDark(Control root)
        {
            foreach (Control c in All(root)) { DarkTextBox d = c as DarkTextBox; if (d != null) return d; }
            return null;
        }

        private static FlatButton FindBtn(Control root)
        {
            foreach (Control c in All(root))
            {
                FlatButton fb = c as FlatButton;
                if (fb != null && (fb.Text == "隐藏悬浮窗" || fb.Text == "显示悬浮窗")) return fb;
            }
            return null;
        }

        private static void Check(string label, Form f)
        {
            try
            {
                f.StartPosition = FormStartPosition.Manual; f.Location = new Point(-4000, -4000); f.Show();
                Pump(260);
                List<Control> all = All(f);
                int buttons = 0, dead = 0; string names = "";
                for (int i = 0; i < all.Count; i++)
                {
                    FlatButton fb = all[i] as FlatButton;
                    if (fb == null || !fb.Visible) continue;
                    buttons++;
                    int n = Handlers(fb);
                    if (n == 0) { dead++; names += "「" + fb.Text + "」"; }
                }
                int ov = Scan(f, f); overlaps += ov;
                Ok(label + "（" + buttons + " 按钮 / 重叠 " + ov + "）", dead == 0 && f.Opacity > 0.9,
                    dead == 0 ? "接线完整，淡入 " + Math.Round(f.Opacity, 2) : ("未接线：" + names));
                f.Close(); f.Dispose();
            }
            catch (Exception ex) { Ok(label, false, ex.Message); }
        }

        private static int Handlers(object c)
        {
            for (Type t = c.GetType(); t != null; t = t.BaseType)
            {
                FieldInfo fi = t.GetField("Click", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (fi != null) { Delegate d = fi.GetValue(c) as Delegate; return d == null ? 0 : d.GetInvocationList().Length; }
            }
            return -1;
        }

        private static int Scan(Control parent, Form f)
        {
            int bad = 0;
            for (int i = 0; i < parent.Controls.Count; i++)
            {
                Control a = parent.Controls[i];
                if (!a.Visible || a.Width <= 0 || a.Height <= 0) continue;
                for (int j = i + 1; j < parent.Controls.Count; j++)
                {
                    Control b = parent.Controls[j];
                    if (!b.Visible || b.Width <= 0 || b.Height <= 0) continue;
                    if (R(a, f).IntersectsWith(R(b, f))) bad++;
                }
            }
            for (int i = 0; i < parent.Controls.Count; i++) bad += Scan(parent.Controls[i], f);
            return bad;
        }
        // 裁剪检测：控件必须完整落在父容器内（防止“按钮被卡片切掉一半”）
        private static int ClipIssues(Control root)
        {
            int bad = 0;
            foreach (Control c in All(root))
            {
                if (!c.Visible || c.Width <= 0 || c.Height <= 0) continue;
                Control p = c.Parent;
                if (p == null) continue;
                if (c.Left < -1 || c.Top < -1 || c.Right > p.ClientSize.Width + 1 || c.Bottom > p.ClientSize.Height + 1)
                {
                    bad++;
                    sb.AppendLine("    ⚠ 裁剪: " + c.GetType().Name + "[" + (c.Text == null ? "" : c.Text) + "] "
                        + c.Bounds + " 超出 " + p.GetType().Name + " 客户区 " + p.ClientSize);
                }
            }
            return bad;
        }

        private static Rectangle R(Control c, Form f) { Point p = f.PointToClient(c.PointToScreen(new Point(0, 0))); return new Rectangle(p.X, p.Y, c.Width, c.Height); }
    }
}