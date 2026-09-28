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

        public static string LogPath
        {
            get { return Path.Combine(Config.DataDir, "unknowns.txt"); }
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