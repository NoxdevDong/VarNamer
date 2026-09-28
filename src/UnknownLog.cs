using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace VarNamer
{
    // 未收录词清单：自动累积内置词库没命中的词，便于一次性补齐
    public static class UnknownLog
    {
        private static readonly HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);

        private static string pathCache;

        // 未收录词清单：优先放在「程序目录\dict\」（和词库文件在一起，方便顺手补齐）；
        // 若该目录不可写（例如装到 Program Files 且无权限），自动回退到用户数据目录。
        public static string LogPath
        {
            get
            {
                if (pathCache != null) return pathCache;
                try
                {
                    string dir = AutoDict.DictFolderPath;
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    string probe = Path.Combine(dir, ".vn_write_probe");
                    File.WriteAllText(probe, "ok");
                    File.Delete(probe);
                    pathCache = Path.Combine(dir, "unknowns.txt");
                    return pathCache;
                }
                catch (Exception) { }
                pathCache = Path.Combine(Config.DataDir, "unknowns.txt");
                return pathCache;
            }
        }

        public static void Record(List<string> unknowns)
        {
            if (unknowns == null || unknowns.Count == 0) return;
            List<string> add = new List<string>();
            lock (seen)
            {
                for (int i = 0; i < unknowns.Count; i++)
                {
                    string u = unknowns[i];
                    if (string.IsNullOrEmpty(u)) continue;
                    if (seen.Contains(u)) continue;
                    seen.Add(u);
                    add.Add(u);
                }
            }
            if (add.Count == 0) return;
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm")).Append("  ");
                for (int i = 0; i < add.Count; i++)
                {
                    if (i > 0) sb.Append(' ');
                    sb.Append(add[i]);
                }
                sb.AppendLine();
                File.AppendAllText(LogPath, sb.ToString(), new UTF8Encoding(false));
            }
            catch (Exception) { }
        }

        public static void OpenLog()
        {
            try
            {
                if (!File.Exists(LogPath))
                    File.WriteAllText(LogPath, "# VarNamer 未收录词清单（使用中自动累积；把本文件发回可一次性补齐词库）" + Environment.NewLine, new UTF8Encoding(false));
                Process.Start("notepad.exe", LogPath);
            }
            catch (Exception) { }
        }
    }
}