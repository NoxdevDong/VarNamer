using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace VarNamer
{
    // 一个被扫描的目录（用于在界面上如实汇报扫描范围）
    public class ScanDir
    {
        public string Path;
        public bool Exists;
        public bool Loose;   // true = 该目录下任意 *.txt 都收（dict 子文件夹）
    }

    // 一次扫描命中的词库文件的结果
    public class DictHit
    {
        public string Path;
        public int Total;      // 文件里通过格式校验、去重后的条数
        public int Added;      // 真正写进用户词库的条数
        public int Invalid;    // 格式不对被丢掉的行数
        public List<string> InvalidSamples = new List<string>();
        public bool UpToDate;  // 文件与上次导入时一致，本次没有重新解析
        public string Error;   // 读取/写入失败

        public int Skipped { get { return Total - Added; } }   // 因为已存在而没写的条数
    }

    // 外置词库检测与导入。
    // 词库本体已内嵌在程序里（打开就能用）；这里额外支持把词库文件放到**程序自己的目录**里，
    // 程序启动时自动识别并并入，也可以在“词库管理”里点「检测本地词库」立即执行。
    //
    // 扫描范围（只扫程序自己的目录）：
    //   1) 程序所在目录 —— 固定文件名，或名字里含「词库 / dict / lexicon」的 .txt
    //   2) 程序目录\dict —— 该目录就是专门放词库的，任意 .txt 都收
    //
    // 合并规则（默认）：
    //   用户词库里已有该键   → 跳过（永不覆盖用户自己的定义）
    //   内置词库已有且值相同 → 跳过（没必要重复占位）
    //   其余（新词）         → 追加到用户词库文件，并同步到运行时词库
    // importAll = true 时：只跳过“用户词库已有”的，其余全部写进用户词库（便于在词条列表里直接看/改）。
    public static class AutoDict
    {
        // 程序目录里认这些固定文件名
        public static readonly string[] CandidateNames = new string[]
        {
            "VarNamer-Dict-CN-EN.txt", "VarNamer-Dict.txt", "userdict.txt", "词库.txt", "dict.txt"
        };

        // 程序目录里还额外认“名字里含这些词”的 txt（「我的词库.txt」「mydict.txt」等）
        private static readonly string[] LooseNameHints = new string[] { "词库", "dict", "lexicon" };

        // 这些文件永远不当词库
        private static readonly string[] NeverImport = new string[] { "使用说明.txt", "在线翻译使用说明.txt", "unknowns.txt", "history.txt" };

        public static List<ScanDir> ScanDirs()
        {
            List<ScanDir> list = new List<ScanDir>();
            string root = ExeDir();
            if (string.IsNullOrEmpty(root)) return list;
            list.Add(MakeDir(root, false));
            list.Add(MakeDir(Path.Combine(root, "dict"), true));
            return list;
        }

        // 程序目录\dict（推荐放词库的位置）
        public static string DictFolderPath
        {
            get
            {
                string root = ExeDir();
                return string.IsNullOrEmpty(root)
                    ? Path.Combine(Environment.CurrentDirectory, "dict")
                    : Path.Combine(root, "dict");
            }
        }

        private static ScanDir MakeDir(string path, bool loose)
        {
            ScanDir d = new ScanDir();
            d.Path = path;
            d.Loose = loose;
            try { d.Exists = Directory.Exists(path); } catch (Exception) { }
            return d;
        }

        private static string ExeDir()
        {
            try { return Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location); }
            catch (Exception) { return null; }
        }

        // 扫描范围内的所有词库文件
        public static List<string> FindAll()
        {
            List<string> res = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try { seen.Add(Path.GetFullPath(Config.UserDictPath)); }
            catch (Exception) { }

            List<ScanDir> dirs = ScanDirs();
            for (int d = 0; d < dirs.Count; d++)
            {
                if (!dirs[d].Exists) continue;
                if (dirs[d].Loose)
                {
                    try
                    {
                        string[] files = Directory.GetFiles(dirs[d].Path, "*.txt");
                        Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                        for (int i = 0; i < files.Length; i++) AddIfFile(res, seen, files[i]);
                    }
                    catch (Exception) { }
                    continue;
                }
                for (int i = 0; i < CandidateNames.Length; i++)
                    AddIfFile(res, seen, Path.Combine(dirs[d].Path, CandidateNames[i]));
                try
                {
                    string[] files = Directory.GetFiles(dirs[d].Path, "*.txt");
                    Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < files.Length; i++)
                        if (NameHintsDict(Path.GetFileName(files[i]))) AddIfFile(res, seen, files[i]);
                }
                catch (Exception) { }
            }
            return res;
        }

        private static bool NameHintsDict(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string low = name.ToLowerInvariant();
            for (int i = 0; i < NeverImport.Length; i++)
                if (string.Equals(name, NeverImport[i], StringComparison.OrdinalIgnoreCase)) return false;
            for (int i = 0; i < LooseNameHints.Length; i++)
                if (low.IndexOf(LooseNameHints[i]) >= 0) return true;
            return false;
        }

        private static void AddIfFile(List<string> res, HashSet<string> seen, string path)
        {
            try
            {
                if (!File.Exists(path)) return;
                string full = Path.GetFullPath(path);
                if (seen.Contains(full)) return;
                seen.Add(full);
                res.Add(path);
            }
            catch (Exception) { }
        }

        // 给界面用的“扫描范围”文本（带存在标记）
        public static string DescribeDirs()
        {
            StringBuilder sb = new StringBuilder();
            List<ScanDir> dirs = ScanDirs();
            for (int i = 0; i < dirs.Count; i++)
            {
                sb.Append("  [").Append(dirs[i].Exists ? "有" : "无").Append("] ").Append(dirs[i].Path);
                if (dirs[i].Loose) sb.Append("   （该目录下任意 .txt 都收）");
                sb.Append(Environment.NewLine);
            }
            return sb.ToString();
        }

        public static string SignatureOf(List<string> files)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < files.Count; i++)
            {
                try
                {
                    FileInfo fi = new FileInfo(files[i]);
                    if (sb.Length > 0) sb.Append(";;");
                    sb.Append(Path.GetFileName(files[i])).Append("|").Append(fi.Length).Append("|").Append(fi.LastWriteTimeUtc.Ticks);
                }
                catch (Exception) { }
            }
            return sb.ToString();
        }

        // 扫描并导入。force = true 忽略指纹强制重新解析（手动「检测本地词库」用）
        // importAll = true 时把“与内置重复”的词条也写进用户词库
        public static List<DictHit> ScanAndImport(AppState state, bool force, bool importAll)
        {
            List<DictHit> hits = new List<DictHit>();
            List<string> files = FindAll();
            if (files.Count == 0) return hits;

            string sig = SignatureOf(files);
            bool fresh = !force && !importAll && sig.Length > 0 && sig == state.Cfg.DictSignature;

            for (int i = 0; i < files.Count; i++)
            {
                DictHit h = new DictHit();
                h.Path = files[i];
                if (fresh)
                {
                    h.UpToDate = true;
                    h.Total = CountEntries(files[i]);
                }
                else
                {
                    h.Added = ImportOne(files[i], state, h, importAll);
                }
                hits.Add(h);
            }

            if (!fresh && sig.Length > 0)
            {
                try { state.Cfg.DictSignature = sig; state.Cfg.Save(); }
                catch (Exception) { }
            }
            return hits;
        }

        public static List<DictHit> ScanAndImport(AppState state, bool force)
        {
            return ScanAndImport(state, force, false);
        }

        // 启动时调用：返回新增条数（0 = 无变化，-1 = 没有外置词库）
        public static int ImportIfNeeded(AppState state, out string file)
        {
            file = null;
            try
            {
                List<DictHit> hits = ScanAndImport(state, false, false);
                if (hits.Count == 0) return -1;
                file = hits[0].Path;
                int added = 0;
                for (int i = 0; i < hits.Count; i++) added += hits[i].Added;
                return added;
            }
            catch (Exception) { return -1; }
        }

        private static int CountEntries(string path)
        {
            int n = 0;
            try
            {
                string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                for (int i = 0; i < lines.Length; i++)
                {
                    string t = lines[i].Trim();
                    if (t.Length == 0 || t[0] == '#') continue;
                    int p = t.IndexOf('=');
                    if (p <= 0) continue;
                    if (IsPlausibleEntry(t.Substring(0, p).Trim(), t.Substring(p + 1).Trim())) n++;
                }
            }
            catch (Exception) { }
            return n;
        }

        // 词条合法性：挡住被误当成词库的普通 txt（例如放在 dict\ 里的说明文件）
        public static bool IsPlausibleEntry(string cn, string en)
        {
            if (string.IsNullOrEmpty(cn)) return false;
            if (cn.Length > 24) return false;
            for (int i = 0; i < cn.Length; i++)
            {
                char c = cn[i];
                if (c == ' ' || c == '\t') return false;
                if (",;:!?，。；：、！？（）「」《》…—“”‘’".IndexOf(c) >= 0) return false;
            }
            if (en == null) en = "";
            for (int i = 0; i < en.Length; i++)
                if (en[i] >= 0x2E80) return false;
            return true;
        }

        private static int ImportOne(string path, AppState state, DictHit hit, bool importAll)
        {
            string[] lines;
            try { lines = File.ReadAllLines(path, Encoding.UTF8); }
            catch (Exception ex) { hit.Error = "读取失败：" + ex.Message; return 0; }

            // 文件内部去重：同一中文以最后出现的一条为准（用户习惯在文件末尾追加修改）
            List<string> raw = new List<string>();
            Dictionary<string, int> seen = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < lines.Length; i++)
            {
                string t = lines[i].Trim();
                if (t.Length == 0 || t[0] == '#') continue;
                int p = t.IndexOf('=');
                if (p <= 0)
                {
                    hit.Invalid++;
                    AddSample(hit, t);
                    continue;
                }
                string cn = t.Substring(0, p).Trim();
                string en = t.Substring(p + 1).Trim();
                if (cn.Length == 0 || !IsPlausibleEntry(cn, en)) { hit.Invalid++; AddSample(hit, t); continue; }
                string line = cn + "=" + en;
                if (seen.ContainsKey(cn)) raw[seen[cn]] = line;
                else { seen[cn] = raw.Count; raw.Add(line); }
            }
            hit.Total = raw.Count;

            // 现有用户词库
            List<string> uLines = new List<string>();
            HashSet<string> uKeys = new HashSet<string>(StringComparer.Ordinal);
            if (File.Exists(Config.UserDictPath))
            {
                try { uLines.AddRange(File.ReadAllLines(Config.UserDictPath, Encoding.UTF8)); }
                catch (Exception) { }
            }
            for (int i = 0; i < uLines.Count; i++)
            {
                string t = uLines[i].Trim();
                if (t.Length == 0 || t[0] == '#') continue;
                int p = t.IndexOf('=');
                if (p <= 0) continue;
                string cn = t.Substring(0, p).Trim();
                if (cn.Length > 0) uKeys.Add(cn);
            }

            List<string> addedKeys = new List<string>();
            for (int i = 0; i < raw.Count; i++)
            {
                int p = raw[i].IndexOf('=');
                string cn = raw[i].Substring(0, p);
                string en = raw[i].Substring(p + 1);
                if (uKeys.Contains(cn)) continue;                       // 用户词库已有 → 永不覆盖
                if (!importAll && state.Lex.MatchValue(cn, en)) continue; // 内置已有且等价 → 默认不重复写
                uLines.Add(raw[i]);
                uKeys.Add(cn);
                state.Lex.Add(cn, en);
                addedKeys.Add(cn);
            }
            hit.Added = addedKeys.Count;
            if (addedKeys.Count == 0) return 0;

            string werr;
            if (!WriteAtomic(Config.UserDictPath, uLines, out werr))
            {
                hit.Error = "写文件失败（目录只读或没有权限）：" + werr;
                hit.Added = 0;
                return 0;
            }

            // 写完回读校验，确保真的落盘了
            int ok = 0;
            try
            {
                string[] back = File.ReadAllLines(Config.UserDictPath, Encoding.UTF8);
                HashSet<string> backKeys = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < back.Length; i++)
                {
                    string t = back[i].Trim();
                    if (t.Length == 0 || t[0] == '#') continue;
                    int p = t.IndexOf('=');
                    if (p > 0) backKeys.Add(t.Substring(0, p).Trim());
                }
                for (int i = 0; i < addedKeys.Count; i++) if (backKeys.Contains(addedKeys[i])) ok++;
            }
            catch (Exception) { ok = -1; }

            if (ok >= 0 && ok < addedKeys.Count)
                hit.Error = "写入后校验不通过：只有 " + ok + "/" + addedKeys.Count + " 条落盘（文件可能被杀软/同步盘占用）";
            state.Lex.Rebuild();
            return hit.Added;
        }

        private static void AddSample(DictHit hit, string line)
        {
            if (hit.InvalidSamples.Count >= 3) return;
            if (line.Length > 42) line = line.Substring(0, 42) + "…";
            hit.InvalidSamples.Add(line);
        }

        private static bool WriteAtomic(string path, List<string> lines, out string error)
        {
            error = null;
            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                string tmp = path + ".tmp";
                File.WriteAllLines(tmp, lines.ToArray(), new UTF8Encoding(false));
                if (File.Exists(path))
                {
                    try { File.Replace(tmp, path, null); return true; }
                    catch (Exception) { }
                }
                File.Copy(tmp, path, true);
                try { File.Delete(tmp); } catch (Exception) { }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}
