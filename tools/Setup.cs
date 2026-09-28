using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace VarNamerSetup
{
    // VarNamer 安装程序（单文件，安装包内置主程序与词库，无需联网）
    //   /S              静默安装（默认：不建桌面图标、不建开始菜单、不开机启动、不启动程序）
    //   /DIR="路径"      指定安装目录
    //   /DESKTOP /STARTMENU /AUTORUN /RUN   静默安装时对应选项
    internal static class Program
    {
        internal const string AppName = "VarNamer";
        internal const string DisplayName = "VarNamer 中文变量取名";
        internal const string DisplayVersion = "5.7.0";
        internal const string Publisher = "VarNamer";
        internal const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\VarNamer";
        internal const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        internal static string DefaultDir
        {
            get
            {
                string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(Path.Combine(local, "Programs"), AppName);
            }
        }

        [STAThread]
        private static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool silent = false;
            string dir = DefaultDir;
            bool desk = false, menu = false, autorun = false, run = false;
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i].Trim();
                string low = a.ToLowerInvariant();
                if (low == "/s" || low == "/silent") silent = true;
                else if (low.StartsWith("/dir=")) dir = a.Substring(5).Trim().Trim('"');
                else if (low == "/desktop") desk = true;
                else if (low == "/startmenu") menu = true;
                else if (low == "/autorun") autorun = true;
                else if (low == "/run") run = true;
            }

            if (silent)
            {
                string err;
                if (!Installer.Install(dir, desk, menu, autorun, null, out err))
                {
                    Log("静默安装失败：" + err);
                    return 1;
                }
                Log("静默安装完成：" + dir + (Installer.Warnings.Count > 0 ? ("；警告 " + string.Join("；", Installer.Warnings.ToArray())) : ""));
                if (run) TryRun(dir);
                return 0;
            }

            using (SetupForm f = new SetupForm(dir))
            {
                Application.Run(f);
                return 0;
            }
        }

        internal static void Log(string text)
        {
            try
            {
                string p = Path.Combine(Path.GetTempPath(), "VarNamer-Setup.log");
                File.AppendAllText(p, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + text + Environment.NewLine, new UTF8Encoding(false));
            }
            catch (Exception) { }
        }

        internal static void TryRun(string dir)
        {
            try { Process.Start(Path.Combine(dir, AppName + ".exe")); } catch (Exception) { }
        }
    }

    // ---------------- 安装逻辑 ----------------
    internal static class Installer
    {
        internal static readonly string[] Payloads = new string[] { "p_main", "p_dict", "p_readme", "p_trhelp", "p_uninst" };

        internal static string TargetOf(string res, string dir)
        {
            if (res == "p_main") return Path.Combine(dir, Program.AppName + ".exe");
            if (res == "p_dict") return Path.Combine(Path.Combine(dir, "dict"), "VarNamer-Dict-CN-EN.txt");
            if (res == "p_readme") return Path.Combine(dir, "使用说明.txt");
            if (res == "p_trhelp") return Path.Combine(dir, "在线翻译使用说明.txt");
            return Path.Combine(dir, "uninstall.exe");
        }

        internal static readonly List<string> Warnings = new List<string>();

        private static void Safe(string what, MethodInvoker act)
        {
            try { act(); }
            catch (Exception ex) { Warnings.Add(what + "未完成：" + ex.Message); }
        }

        internal static bool Install(string dir, bool desktop, bool startmenu, bool autorun, ProgressCallback cb, out string error)
        {
            error = null;
            Warnings.Clear();
            try
            {
                Directory.CreateDirectory(dir);
                string probe = Path.Combine(dir, ".vn_write_probe");
                File.WriteAllText(probe, "ok");
                File.Delete(probe);

                // 第一步：写程序文件。这一步失败就是真失败，直接中止。
                Assembly asm = Assembly.GetExecutingAssembly();
                long total = 0;
                for (int i = 0; i < Payloads.Length; i++)
                {
                    string target = TargetOf(Payloads[i], dir);
                    string sub = Path.GetDirectoryName(target);
                    if (!string.IsNullOrEmpty(sub)) Directory.CreateDirectory(sub);
                    using (Stream s = asm.GetManifestResourceStream(Payloads[i]))
                    {
                        if (s == null) throw new InvalidOperationException("安装包损坏：缺少 " + Payloads[i]);
                        byte[] buf = new byte[s.Length];
                        int off = 0;
                        while (off < buf.Length)
                        {
                            int n = s.Read(buf, off, buf.Length - off);
                            if (n <= 0) break;
                            off += n;
                        }
                        total += off;
                        File.WriteAllBytes(target, buf);
                    }
                    if (cb != null) cb((i + 1) * 80 / Payloads.Length, "正在写入 " + Path.GetFileName(target));
                }

                // 第二步：快捷方式 / 开机启动 / 卸载登记。受系统策略或权限限制时只记警告，不影响安装结果。
                if (cb != null) cb(85, "正在创建快捷方式 …");
                string exePath = Path.Combine(dir, Program.AppName + ".exe");
                string dl = DesktopLnk();
                if (desktop) Safe("创建桌面快捷方式", delegate() { CreateShortcut(dl, exePath, dir, "", Program.DisplayName); });
                else Safe("移除旧桌面快捷方式", delegate() { TryDelete(dl); });

                string menuFolder = StartMenuFolder();
                if (startmenu)
                {
                    Safe("创建开始菜单项", delegate()
                    {
                        Directory.CreateDirectory(menuFolder);
                        CreateShortcut(Path.Combine(menuFolder, Program.AppName + ".lnk"), exePath, dir, "", Program.DisplayName);
                        CreateShortcut(Path.Combine(menuFolder, "卸载 VarNamer.lnk"), Path.Combine(dir, "uninstall.exe"), dir, "", "卸载 " + Program.DisplayName);
                    });
                }
                else if (Directory.Exists(menuFolder))
                {
                    Safe("清理旧开始菜单项", delegate() { Directory.Delete(menuFolder, true); });
                }

                if (cb != null) cb(92, "正在登记卸载信息 …");
                Safe("写入开机启动项", delegate()
                {
                    using (RegistryKey k = Registry.CurrentUser.CreateSubKey(Program.RunKey))
                    {
                        if (k == null) return;
                        if (autorun) k.SetValue(Program.AppName, "\"" + exePath + "\"");
                        else k.DeleteValue(Program.AppName, false);
                    }
                });
                Safe("写入卸载登记项", delegate()
                {
                    using (RegistryKey k = Registry.CurrentUser.CreateSubKey(Program.UninstallKey))
                    {
                        if (k == null) return;
                        k.SetValue("DisplayName", Program.DisplayName);
                        k.SetValue("DisplayVersion", Program.DisplayVersion);
                        k.SetValue("Publisher", Program.Publisher);
                        k.SetValue("InstallLocation", dir);
                        k.SetValue("UninstallString", "\"" + Path.Combine(dir, "uninstall.exe") + "\"");
                        k.SetValue("QuietUninstallString", "\"" + Path.Combine(dir, "uninstall.exe") + "\" /S");
                        k.SetValue("DisplayIcon", exePath);
                        k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                        k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                        k.SetValue("EstimatedSize", (int)(total / 1024), RegistryValueKind.DWord);
                    }
                });
                if (cb != null) cb(100, "安装完成");
                return true;
            }
            catch (Exception ex)
            {
                error = ex.ToString();
                return false;
            }
        }

        internal static bool NeedsElevation(string dir, out string msg)
        {
            msg = null;
            try
            {
                Directory.CreateDirectory(dir);
                string probe = Path.Combine(dir, ".vn_write_probe");
                File.WriteAllText(probe, "ok");
                File.Delete(probe);
                return false;
            }
            catch (Exception ex)
            {
                msg = ex.Message;
                return true;
            }
        }

        internal static string DesktopLnk()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Program.AppName + ".lnk");
        }

        internal static string StartMenuFolder()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), Program.AppName);
        }

        internal static void TryDelete(string p)
        {
            try { if (File.Exists(p)) File.Delete(p); } catch (Exception) { }
        }

        internal static void CreateShortcut(string lnk, string target, string workDir, string args, string desc)
        {
            Type t = Type.GetTypeFromProgID("WScript.Shell");
            if (t == null) return;
            object sh = Activator.CreateInstance(t);
            object sc = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, sh, new object[] { lnk });
            Type st = sc.GetType();
            st.InvokeMember("TargetPath", BindingFlags.SetProperty, null, sc, new object[] { target });
            st.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, sc, new object[] { workDir });
            st.InvokeMember("Arguments", BindingFlags.SetProperty, null, sc, new object[] { args });
            st.InvokeMember("Description", BindingFlags.SetProperty, null, sc, new object[] { desc });
            st.InvokeMember("IconLocation", BindingFlags.SetProperty, null, sc, new object[] { target + ",0" });
            st.InvokeMember("Save", BindingFlags.InvokeMethod, null, sc, null);
        }

        internal static bool IsInstalled(out string dir)
        {
            dir = null;
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(Program.UninstallKey))
                {
                    if (k == null) return false;
                    dir = k.GetValue("InstallLocation") as string;
                    return !string.IsNullOrEmpty(dir);
                }
            }
            catch (Exception) { return false; }
        }
    }

    internal delegate void ProgressCallback(int percent, string text);

    // ---------------- 界面 ----------------
    internal class SetupForm : Form
    {
        private static readonly Color Accent = Color.FromArgb(47, 111, 237);
        private static readonly Color AccentHover = Color.FromArgb(38, 96, 212);
        private static readonly Color Ink = Color.FromArgb(31, 35, 40);
        private static readonly Color Sub = Color.FromArgb(110, 119, 129);
        private static readonly Color Line = Color.FromArgb(225, 228, 232);
        private static readonly Color Panel = Color.FromArgb(247, 248, 250);

        private TextBox txtDir;
        private Button btnBrowse;
        private Button btnMain;
        private Button btnCancel;
        private CheckBox chkDesktop, chkMenu, chkAutorun, chkRun;
        private Label lblState, lblHint, lblTitle, lblSub, lblDirCap, lblOption;
        private Panel header, page1, page2;
        private ProgressBar bar;
        private bool busy;

        public SetupForm(string dir)
        {
            Font = new Font("Microsoft YaHei UI", 9f);
            Text = "安装 VarNamer";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(566, 452);
            BackColor = Color.White;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch (Exception) { }

            header = new Panel();
            header.SetBounds(0, 0, 566, 92);
            header.BackColor = Panel;
            header.Paint += delegate(object s, PaintEventArgs e)
            {
                e.Graphics.DrawLine(new Pen(Line), 0, 91, 566, 91);
            };
            Controls.Add(header);

            PictureBox ico = new PictureBox();
            ico.SetBounds(24, 22, 48, 48);
            ico.SizeMode = PictureBoxSizeMode.Zoom;
            try { ico.Image = Icon.ExtractAssociatedIcon(Application.ExecutablePath).ToBitmap(); } catch (Exception) { }
            header.Controls.Add(ico);

            lblTitle = new Label();
            lblTitle.Text = Program.DisplayName;
            lblTitle.Font = new Font("Microsoft YaHei UI", 14f, FontStyle.Bold);
            lblTitle.ForeColor = Ink;
            lblTitle.SetBounds(86, 22, 440, 30);
            header.Controls.Add(lblTitle);

            lblSub = new Label();
            lblSub.Text = "版本 " + Program.DisplayVersion + "　·　安装向导";
            lblSub.ForeColor = Sub;
            lblSub.SetBounds(88, 54, 440, 22);
            header.Controls.Add(lblSub);

            page1 = new Panel();
            page1.SetBounds(0, 92, 566, 316);
            page1.BackColor = Color.White;
            Controls.Add(page1);

            lblDirCap = new Label();
            lblDirCap.Text = "安装位置";
            lblDirCap.ForeColor = Ink;
            lblDirCap.SetBounds(24, 18, 200, 22);
            page1.Controls.Add(lblDirCap);

            txtDir = new TextBox();
            txtDir.Text = dir;
            txtDir.SetBounds(24, 44, 420, 26);
            txtDir.BorderStyle = BorderStyle.FixedSingle;
            page1.Controls.Add(txtDir);

            btnBrowse = new Button();
            btnBrowse.Text = "浏览…";
            btnBrowse.SetBounds(454, 43, 88, 27);
            btnBrowse.FlatStyle = FlatStyle.System;
            btnBrowse.Click += delegate(object s, EventArgs e) { Browse(); };
            page1.Controls.Add(btnBrowse);

            lblState = new Label();
            lblState.ForeColor = Sub;
            lblState.SetBounds(24, 76, 518, 22);
            page1.Controls.Add(lblState);

            lblOption = new Label();
            lblOption.Text = "选项";
            lblOption.ForeColor = Ink;
            lblOption.SetBounds(24, 112, 200, 22);
            page1.Controls.Add(lblOption);

            chkDesktop = MakeCheck("创建桌面快捷方式", 24, 140);
            chkMenu = MakeCheck("创建开始菜单快捷方式", 24, 172);
            chkAutorun = MakeCheck("开机自动启动 VarNamer", 24, 204);
            chkRun = MakeCheck("安装完成后立即运行", 24, 236);
            chkDesktop.Checked = true;
            chkMenu.Checked = true;
            chkRun.Checked = true;

            lblHint = new Label();
            lblHint.ForeColor = Sub;
            lblHint.SetBounds(24, 272, 518, 34);
            lblHint.Text = "默认安装到当前用户目录，不需要管理员权限；把路径改成 Program Files 会请求管理员权限。"
                         + Environment.NewLine + "全量词库已内置在主程序里，装好即可用，无需任何导入操作。";
            page1.Controls.Add(lblHint);

            page2 = new Panel();
            page2.SetBounds(0, 92, 566, 316);
            page2.BackColor = Color.White;
            page2.Visible = false;
            Controls.Add(page2);

            Label done = new Label();
            done.Text = "安装完成";
            done.Font = new Font("Microsoft YaHei UI", 13f, FontStyle.Bold);
            done.ForeColor = Ink;
            done.SetBounds(24, 20, 300, 30);
            page2.Controls.Add(done);

            Label doneSub = new Label();
            doneSub.ForeColor = Sub;
            doneSub.SetBounds(24, 56, 518, 140);
            doneSub.Text = "VarNamer 已安装到：" + Environment.NewLine;
            page2.Controls.Add(doneSub);
            lblDoneSub = doneSub;

            Label tip = new Label();
            tip.ForeColor = Sub;
            tip.SetBounds(24, 200, 518, 100);
            tip.Text = "使用提示：" + Environment.NewLine
                     + "· 默认热键 Ctrl+Alt+V 唤起悬浮窗，输入中文回车即得变量名；" + Environment.NewLine
                     + "· 悬浮窗与主界面左键双击结果即可复制；" + Environment.NewLine
                     + "· 托盘图标右键可显示/隐藏悬浮窗、打开设置或退出。";
            page2.Controls.Add(tip);

            bar = new ProgressBar();
            bar.SetBounds(24, 384, 400, 6);
            bar.Style = ProgressBarStyle.Continuous;
            bar.Visible = false;
            Controls.Add(bar);
            bar.BringToFront();          // 否则会被上面那层面板盖住

            btnMain = new Button();
            btnMain.Text = "开始安装";
            btnMain.SetBounds(346, 408, 96, 32);
            btnMain.BackColor = Accent;
            btnMain.ForeColor = Color.White;
            btnMain.FlatStyle = FlatStyle.Flat;
            btnMain.FlatAppearance.BorderSize = 0;
            btnMain.Click += delegate(object s, EventArgs e) { OnMain(); };
            Controls.Add(btnMain);

            btnCancel = new Button();
            btnCancel.Text = "取消";
            btnCancel.SetBounds(452, 408, 90, 32);
            btnCancel.FlatStyle = FlatStyle.Flat;
            btnCancel.FlatAppearance.BorderColor = Line;
            btnCancel.Click += delegate(object s, EventArgs e) { if (!busy) this.Close(); };
            Controls.Add(btnCancel);
            CancelButton = btnCancel;

            RefreshState();
            txtDir.TextChanged += delegate(object s, EventArgs e) { RefreshState(); };
        }

        private Label lblDoneSub;

        private CheckBox MakeCheck(string text, int x, int y)
        {
            CheckBox c = new CheckBox();
            c.Text = text;
            c.ForeColor = Ink;
            c.SetBounds(x, y, 300, 24);
            page1.Controls.Add(c);
            return c;
        }

        private void RefreshState()
        {
            string d = txtDir.Text.Trim().Trim('"');
            string dir;
            if (Installer.IsInstalled(out dir))
                lblState.Text = "检测到已安装版本（" + dir + "），继续安装将覆盖更新。";
            else
            {
                long free = 0;
                try { free = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(d))).AvailableFreeSpace; } catch (Exception) { }
                lblState.Text = free > 0
                    ? "预计占用约 0.4 MB 磁盘空间，目标盘可用 " + (free / 1024 / 1024) + " MB。"
                    : "预计占用约 0.4 MB 磁盘空间。";
            }
        }

        private void Browse()
        {
            using (FolderBrowserDialog f = new FolderBrowserDialog())
            {
                f.Description = "选择 VarNamer 的安装目录";
                f.SelectedPath = txtDir.Text.Trim();
                if (f.ShowDialog(this) == DialogResult.OK)
                    txtDir.Text = Path.Combine(f.SelectedPath, Program.AppName);
            }
        }

        private bool done;

        private void OnMain()
        {
            if (done) { this.Close(); return; }
            string dir = txtDir.Text.Trim().Trim('"');
            if (dir.Length == 0) { MessageBox.Show(this, "请填写安装位置。", "VarNamer", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }

            string msg;
            if (Installer.NeedsElevation(dir, out msg))
            {
                DialogResult r = MessageBox.Show(this,
                    "当前用户没有写入权限：" + Environment.NewLine + dir + Environment.NewLine + Environment.NewLine
                    + "是否以管理员身份重新运行安装程序？",
                    "需要管理员权限", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (r == DialogResult.Yes)
                {
                    try
                    {
                        StringBuilder a = new StringBuilder();
                        a.Append("/DIR=\"").Append(dir).Append("\"");
                        if (chkDesktop.Checked) a.Append(" /DESKTOP");
                        if (chkMenu.Checked) a.Append(" /STARTMENU");
                        if (chkAutorun.Checked) a.Append(" /AUTORUN");
                        if (chkRun.Checked) a.Append(" /RUN");
                        ProcessStartInfo psi = new ProcessStartInfo(Application.ExecutablePath, a.ToString());
                        psi.Verb = "runas";
                        Process.Start(psi);
                        this.Close();
                    }
                    catch (Exception) { }
                }
                return;
            }

            SetBusy(true);
            string err;
            bool ok = Installer.Install(dir, chkDesktop.Checked, chkMenu.Checked, chkAutorun.Checked, OnProgress, out err);
            SetBusy(false);
            if (!ok)
            {
                MessageBox.Show(this, "安装失败：" + Environment.NewLine + err, "VarNamer", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            done = true;
            page1.Visible = false;
            page2.Visible = true;
            if (Installer.Warnings.Count > 0)
                MessageBox.Show(this, "安装已完成，但有 " + Installer.Warnings.Count + " 项未成功（不影响使用）："
                    + Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine, Installer.Warnings.ToArray()),
                    "VarNamer", MessageBoxButtons.OK, MessageBoxIcon.Information);
            lblDoneSub.Text = "VarNamer 已安装到：" + Environment.NewLine + dir + Environment.NewLine + Environment.NewLine
                + "卸载方式：开始菜单 → VarNamer → 卸载，或" + Environment.NewLine + "「设置 → 应用 → 已安装的应用」中卸载。";
            btnMain.Text = "完成";
            btnMain.BackColor = Accent;
            if (chkRun.Checked)
            {
                Program.TryRun(dir);
                this.Close();
            }
        }

        private void OnProgress(int percent, string text)
        {
            bar.Value = Math.Max(0, Math.Min(100, percent));
            lblState.Text = text;
            Application.DoEvents();
        }

        private void SetBusy(bool b)
        {
            busy = b;
            bar.Visible = b;
            txtDir.Enabled = !b;
            btnBrowse.Enabled = !b;
            chkDesktop.Enabled = !b;
            chkMenu.Enabled = !b;
            chkAutorun.Enabled = !b;
            chkRun.Enabled = !b;
            btnCancel.Enabled = !b;
            btnMain.Enabled = !b;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (txtDir.Text.Trim().Length == 0) txtDir.Text = Program.DefaultDir;
        }
    }
}
