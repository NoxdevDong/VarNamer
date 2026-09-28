using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace VarNamer
{
    internal static class AppVersion
    {
        public const string Value = "4.9";
    }

    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            // 命令行用法一：转换并写入剪贴板
            //   VarNamer.exe --copy 数学成绩 [--style snake] [--out 结果.txt]
            if (args != null && args.Length >= 2 && args[0] == "--copy") return CopyMode(args);

            // 命令行用法二：导出在线翻译使用说明
            if (args != null && args.Length > 0 && args[0] == "--trhelp") return HelpMode();

            // 图形界面：可选把第一个参数当作初始中文
            string initial = null;
            if (args != null && args.Length > 0 && args[0].Length > 0 && args[0][0] != '-') initial = args[0];

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            if (AlreadyRunning())
            {
                MessageBox.Show("已有一个 VarNamer 在运行：" + Environment.NewLine + Environment.NewLine + ReadRunningInfo()
                    + Environment.NewLine + Environment.NewLine
                    + "如果你看到的界面与预期不符（例如旧版本外观），请右键托盘图标 → 退出，然后重新启动本程序。",
                    "VarNamer 已在运行", MessageBoxButtons.OK);   // 信息类提示不带图标 → 不播系统提示音
                return 0;
            }
            try
            {
                Application.Run(new TrayApp(initial));
                return 0;
            }
            catch (Exception ex)
            {
                LogError(ex);
                MessageBox.Show(ex.ToString(), "VarNamer 启动失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }

        private static int CopyMode(string[] args)
        {
            try
            {
                Lexicon lex = Lexicon.Create();
                lex.LoadUserFile(Config.UserDictPath);
                lex.Rebuild();
                Config cfg = Config.Load();
                NameResult r = Namer.Create(args[1], lex, cfg.ToOptions());
                string style = "camel";
                string outFile = null;
                for (int i = 2; i < args.Length - 1; i++)
                {
                    if (args[i] == "--style") style = args[i + 1];
                    if (args[i] == "--out") outFile = args[i + 1];
                }
                string value = PickStyle(r, style);
                bool ok = Clip.SetText(value);
                if (outFile != null)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine(value);
                    for (int i = 0; i < r.Lines.Count; i++)
                        sb.AppendLine(r.Lines[i].Label + " = " + r.Lines[i].Value);
                    if (r.Unknowns.Count > 0) sb.AppendLine("未识别字: " + string.Join(" ", r.Unknowns.ToArray()));
                    File.WriteAllText(outFile, sb.ToString(), new UTF8Encoding(false));
                }
                return ok ? 0 : 2;
            }
            catch (Exception ex)
            {
                LogError(ex);
                return 1;
            }
        }

        private static string PickStyle(NameResult r, string style)
        {
            int idx = 1;
            string s = (style == null ? "" : style.ToLowerInvariant());
            if (s == "pascal") idx = 0;
            else if (s == "camel") idx = 1;
            else if (s == "snake") idx = 2;
            else if (s == "screaming" || s == "const") idx = 3;
            else if (s == "kebab") idx = 4;
            else if (s == "dot") idx = 5;
            else if (s == "brief") idx = 7;
            else if (s == "briefsnake") idx = 8;
            else if (s == "abbr") idx = 11;
            else if (s == "abbr3") idx = 9;
            else if (s == "ABBR") idx = 12;
            if (idx < r.Lines.Count) return r.Lines[idx].Value;
            return r.Lines.Count > 0 ? r.Lines[0].Value : "";
        }

        private static int HelpMode()
        {
            try
            {
                string dir = Path.GetDirectoryName(Application.ExecutablePath);
                File.WriteAllText(Path.Combine(dir, "在线翻译使用说明.txt"), TranslateHelp.Text, new UTF8Encoding(true));
                return 0;
            }
            catch (Exception ex)
            {
                LogError(ex);
                return 1;
            }
        }

        private static void LogError(Exception ex)
        {
            try
            {
                string path = Path.Combine(Config.DataDir, "error.log");
                File.AppendAllText(path,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + ex + Environment.NewLine + new string('-', 60) + Environment.NewLine,
                    new UTF8Encoding(false));
            }
            catch (Exception) { }
        }

        private static string ReadRunningInfo()
        {
            try
            {
                string path = Path.Combine(Config.DataDir, "running.txt");
                if (!File.Exists(path)) return "(未能读取运行信息，版本见其窗口标题栏)";
                return File.ReadAllText(path, new UTF8Encoding(false)).Trim();
            }
            catch (Exception) { return "(未能读取运行信息)"; }
        }

        private static bool AlreadyRunning()
        {
            try
            {
                bool created;
                System.Threading.Mutex m = new System.Threading.Mutex(true, "VarNamer.SingleInstance.v1", out created);
                if (!created) return true;
                GC.KeepAlive(m);
                return false;
            }
            catch (Exception) { return false; }
        }
    }
}
