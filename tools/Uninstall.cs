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

namespace VarNamerUninstall
{
    // VarNamer 卸载程序：删除程序文件、快捷方式、开机启动项与"应用和功能"登记项。
    // 参数： /S 静默卸载   /PURGE 同时删除用户配置与词库（%APPDATA%\\VarNamer）
    internal static class Program
    {
        private const string AppName = "VarNamer";
        private const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\VarNamer";
        private static bool silent;
        private static bool purge;
        internal static readonly List<string> Warnings = new List<string>();

        internal static void Log(string text)
        {
            try
            {
                string p = Path.Combine(Path.GetTempPath(), "VarNamer-Uninstall.log");
                File.AppendAllText(p, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + text + Environment.NewLine, new UTF8Encoding(false));
            }
            catch (Exception) { }
        }

        private static void Safe(string what, MethodInvoker act)
        {
            try { act(); }
            catch (Exception ex) { Warnings.Add(what + "未完成：" + ex.Message); }
        }

        [STAThread]
        private static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            string dir = Path.GetDirectoryName(Application.ExecutablePath);
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i].Trim().ToLowerInvariant();
                if (a == "/s" || a == "/silent") silent = true;
                else if (a == "/purge" || a == "/rmuserdata") purge = true;
                else if (a.StartsWith("/dir=")) dir = args[i].Substring(5).Trim().Trim('"');
            }
            if (silent) return Run(dir, purge);
            using (UninstallForm f = new UninstallForm(dir))
            {
                Application.Run(f);
                if (!f.Confirmed) return 0;
                int rc = Run(f.InstallDir, f.PurgeUserData);
                if (Warnings.Count > 0)
                    MessageBox.Show("卸载已完成，但有 " + Warnings.Count + " 项未能删除（通常是被占用或权限不足）："
                        + Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine, Warnings.ToArray())
                        + Environment.NewLine + Environment.NewLine + "日志：" + Path.Combine(Path.GetTempPath(), "VarNamer-Uninstall.log"),
                        "VarNamer 卸载", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return rc;
            }
        }

        internal static int Run(string dir, bool purgeData)
        {
            try
            {
                KillRunning();
                Safe("删除快捷方式", delegate() { RemoveShortcuts(); });
                Safe("移除开机启动项", delegate() { RemoveRunEntry(); });
                Safe("注销卸载登记项", delegate() { RemoveRegistry(); });
                Safe("删除程序文件", delegate() { DeleteProgramFiles(dir); });
                if (purgeData) Safe("删除用户配置与词库", delegate() { DeleteUserData(); });
                ScheduleSelfDelete(dir, Application.ExecutablePath);
                Log(Warnings.Count == 0
                    ? "卸载完成：" + dir
                    : ("卸载完成（有 " + Warnings.Count + " 项未成功）：" + dir + "；" + string.Join("；", Warnings.ToArray())));
                return 0;
            }
            catch (Exception ex)
            {
                if (!silent) MessageBox.Show(ex.Message, "VarNamer 卸载", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }

        private static void KillRunning()
        {
            Process[] ps = Process.GetProcessesByName("VarNamer");
            for (int i = 0; i < ps.Length; i++)
            {
                try { ps[i].Kill(); } catch (Exception) { }
            }
            for (int i = 0; i < ps.Length; i++)
            {
                try { ps[i].WaitForExit(3000); } catch (Exception) { }
                try { ps[i].Dispose(); } catch (Exception) { }
            }
            if (ps.Length > 0) Thread.Sleep(400);
        }

        internal static void RemoveShortcuts()
        {
            string[] dirs = new string[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
                Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms)
            };
            for (int i = 0; i < dirs.Length; i++)
            {
                if (string.IsNullOrEmpty(dirs[i])) continue;
                TryDeleteFile(Path.Combine(dirs[i], AppName + ".lnk"));
                TryDeleteFile(Path.Combine(dirs[i], "VarNamer 中文变量取名.lnk"));
                string folder = Path.Combine(dirs[i], AppName);
                if (Directory.Exists(folder))
                {
                    TryDeleteFile(Path.Combine(folder, AppName + ".lnk"));
                    TryDeleteFile(Path.Combine(folder, "卸载 VarNamer.lnk"));
                    Directory.Delete(folder, true);
                }
            }
        }

        private static void TryDeleteFile(string p)
        {
            if (File.Exists(p)) File.Delete(p);   // 失败就抛，交给上层 Safe 记录
        }

        private static void RemoveRunEntry()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (k != null) k.DeleteValue(AppName, false);
                }
            }
            catch (Exception) { }
        }

        private static void RemoveRegistry()
        {
            try { Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false); } catch (Exception) { }
            try { Registry.LocalMachine.DeleteSubKeyTree(UninstallKey, false); } catch (Exception) { }
        }

        private static void DeleteProgramFiles(string dir)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;
            string self = Application.ExecutablePath;
            string[] files = Directory.GetFiles(dir);
            for (int i = 0; i < files.Length; i++)
            {
                if (string.Equals(files[i], self, StringComparison.OrdinalIgnoreCase)) continue;
                try { File.Delete(files[i]); } catch (Exception ex) { Warnings.Add("删除 " + Path.GetFileName(files[i]) + " 失败：" + ex.Message); }
            }
            string[] subs = Directory.GetDirectories(dir);
            for (int i = 0; i < subs.Length; i++)
            {
                try { Directory.Delete(subs[i], true); } catch (Exception ex) { Warnings.Add("删除目录 " + Path.GetFileName(subs[i]) + " 失败：" + ex.Message); }
            }
        }

        // 与主程序一致：优先用 VARNAMER_DATA_DIR，其次 %APPDATA%\VarNamer
        private static void DeleteUserData()
        {
            string d = null;
            try
            {
                string env = Environment.GetEnvironmentVariable("VARNAMER_DATA_DIR");
                if (!string.IsNullOrEmpty(env)) d = env;
            }
            catch (Exception) { }
            if (string.IsNullOrEmpty(d))
                d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppName);
            try { if (Directory.Exists(d)) Directory.Delete(d, true); } catch (Exception) { }
        }

        // 生成一个临时批处理，等本进程退出后删掉自身与空目录
        private static void ScheduleSelfDelete(string dir, string self)
        {
            try
            {
                string bat = Path.Combine(Path.GetTempPath(), "varnamer_uninstall_" + DateTime.Now.Ticks + ".bat");
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("@echo off");
                sb.AppendLine("set n=0");
                sb.AppendLine(":loop");
                sb.AppendLine("ping -n 2 127.0.0.1 >nul");
                sb.AppendLine("del /f /q \"" + self + "\" >nul 2>nul");
                sb.AppendLine("if not exist \"" + self + "\" goto done");
                sb.AppendLine("set /a n+=1");
                sb.AppendLine("if %n% geq 15 goto done");
                sb.AppendLine("goto loop");
                sb.AppendLine(":done");
                sb.AppendLine("rmdir \"" + dir + "\" >nul 2>nul");
                sb.AppendLine("del /f /q \"%~f0\" >nul 2>nul");
                sb.AppendLine("exit /b");
                File.WriteAllText(bat, sb.ToString(), Encoding.Default);
                ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", "/c \"" + bat + "\"");
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                Process.Start(psi);
            }
            catch (Exception) { }
        }
    }

    // 卸载确认窗口
    internal class UninstallForm : Form
    {
        public bool Confirmed;
        public string InstallDir;
        public bool PurgeUserData;

        private CheckBox chkPurge;
        private Button btnOk;
        private Button btnCancel;

        public UninstallForm(string dir)
        {
            InstallDir = dir;
            Text = "卸载 VarNamer";
            Font = new Font("Microsoft YaHei UI", 9f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(452, 218);
            BackColor = Color.White;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch (Exception) { }

            Label t = new Label();
            t.Text = "卸载 VarNamer 中文变量取名";
            t.Font = new Font("Microsoft YaHei UI", 12f, FontStyle.Bold);
            t.ForeColor = Color.FromArgb(31, 35, 40);
            t.SetBounds(22, 20, 400, 28);
            Controls.Add(t);

            Label d = new Label();
            d.Text = "将从下面的目录中移除程序文件、桌面与开始菜单快捷方式、" + Environment.NewLine
                   + "开机启动项，并注销“应用和功能”中的登记项。" + Environment.NewLine + Environment.NewLine
                   + "安装位置：" + dir;
            d.ForeColor = Color.FromArgb(87, 96, 106);
            d.SetBounds(22, 54, 410, 80);
            Controls.Add(d);

            chkPurge = new CheckBox();
            chkPurge.Text = "同时删除我的配置与词库（%APPDATA%\\VarNamer）";
            chkPurge.ForeColor = Color.FromArgb(31, 35, 40);
            chkPurge.SetBounds(22, 132, 400, 24);
            Controls.Add(chkPurge);

            btnOk = new Button();
            btnOk.Text = "卸载";
            btnOk.SetBounds(262, 170, 80, 30);
            btnOk.Click += delegate(object s, EventArgs e) { Confirmed = true; PurgeUserData = chkPurge.Checked; this.Close(); };
            Controls.Add(btnOk);

            btnCancel = new Button();
            btnCancel.Text = "取消";
            btnCancel.SetBounds(352, 170, 80, 30);
            btnCancel.Click += delegate(object s, EventArgs e) { Confirmed = false; this.Close(); };
            Controls.Add(btnCancel);
            CancelButton = btnCancel;
        }
    }
}
