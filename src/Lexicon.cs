using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace VarNamer
{
    public class LexEntry
    {
        public string Chinese;
        public List<string> Translations = new List<string>();
        public bool IsStopWord { get { return Translations.Count == 0; } }
    }

    public class Lexicon
    {
        private Dictionary<string, LexEntry> map = new Dictionary<string, LexEntry>(StringComparer.Ordinal);
        private List<string> keys = new List<string>();
        private List<string> stopWords = new List<string>();
        public int MaxKeyLength = 1;

        public static Lexicon Create()
        {
            Lexicon lex = new Lexicon();
            lex.LoadBuiltin();
            lex.Rebuild();
            return lex;
        }

        public void LoadBuiltin()
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            string res = null;
            string[] names = asm.GetManifestResourceNames();
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i].EndsWith("lexicon.txt", StringComparison.OrdinalIgnoreCase)) { res = names[i]; break; }
            }
            if (res == null) throw new InvalidOperationException("builtin lexicon resource not found");
            using (Stream s = asm.GetManifestResourceStream(res))
            {
                if (s == null) throw new InvalidOperationException("builtin lexicon stream is null");
                using (StreamReader r = new StreamReader(s, new UTF8Encoding(false)))
                {
                    LoadFromText(r.ReadToEnd());
                }
            }
        }

        public void LoadFromText(string text)
        {
            if (text == null) return;
            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0) continue;
                if (line[0] == '#') continue;
                int p = line.IndexOf('=');
                if (p <= 0) continue;
                string cn = line.Substring(0, p).Trim();
                string en = line.Substring(p + 1).Trim();
                if (cn.Length == 0) continue;
                if (en.Length == 0) AddStopWord(cn);
                else Add(cn, en);
            }
        }

        public void LoadFromFile(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            if (!File.Exists(path)) return;
            LoadFromText(File.ReadAllText(path, new UTF8Encoding(false)));
        }

        // 载入“用户词库”：同名条目**覆盖**内置定义（界面文案承诺“用户词库优先于内置词库”）。
        // 注意与 LoadFromFile 的区别：LoadFromFile 是叠加（同义词合并），这里是有则替换。
        public void LoadUserFile(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            if (!File.Exists(path)) return;
            string[] lines = File.ReadAllText(path, new UTF8Encoding(false)).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0) continue;
                if (line[0] == '#') continue;
                int p = line.IndexOf('=');
                if (p <= 0) continue;
                string cn = line.Substring(0, p).Trim();
                string en = line.Substring(p + 1).Trim();
                if (cn.Length == 0) continue;
                SetEntry(cn, en);
            }
        }

        // 设置一条词条：已有同名词条先清空再写入（= 覆盖），空值则标记为停用词
        public void SetEntry(string cn, string en)
        {
            if (string.IsNullOrEmpty(cn)) return;
            LexEntry old;
            if (map.TryGetValue(cn, out old)) old.Translations.Clear();
            if (string.IsNullOrEmpty(en)) AddStopWord(cn);
            else Add(cn, en);
        }

        public void AddStopWord(string cn)
        {
            if (cn == null || cn.Length == 0) return;
            if (!map.ContainsKey(cn))
            {
                LexEntry e = new LexEntry();
                e.Chinese = cn;
                map[cn] = e;
            }
        }

        public void Add(string cn, string en)
        {
            if (string.IsNullOrEmpty(cn) || string.IsNullOrEmpty(en)) return;
            LexEntry e;
            if (!map.TryGetValue(cn, out e))
            {
                e = new LexEntry();
                e.Chinese = cn;
                map[cn] = e;
            }
            string[] cands = en.Split('|');
            for (int i = 0; i < cands.Length; i++)
            {
                string c = SplitWords(cands[i].Trim());
                if (c.Length == 0) continue;
                if (!e.Translations.Contains(c)) e.Translations.Add(c);
            }
        }

        public static string SplitWords(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            StringBuilder sb = new StringBuilder(s.Length + 8);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '_' || c == '-' || c == '.') { sb.Append(' '); continue; }
                if (i > 0 && char.IsUpper(c))
                {
                    char prev = s[i - 1];
                    bool boundary = char.IsLower(prev) || char.IsDigit(prev);
                    if (!boundary && i + 1 < s.Length && char.IsLower(s[i + 1])) boundary = true;
                    if (boundary) sb.Append(' ');
                }
                sb.Append(c);
            }
            return sb.ToString();
        }
        public void Rebuild()
        {
            keys = new List<string>(map.Keys);
            keys.Sort(delegate(string a, string b)
            {
                if (a.Length != b.Length) return b.Length.CompareTo(a.Length);
                return string.CompareOrdinal(a, b);
            });
            stopWords = new List<string>();
            MaxKeyLength = 1;
            for (int i = 0; i < keys.Count; i++)
            {
                LexEntry e = map[keys[i]];
                if (e.IsStopWord) stopWords.Add(keys[i]);
                else if (keys[i].Length > MaxKeyLength) MaxKeyLength = keys[i].Length;
            }
        }

        public bool TryGet(string cn, out LexEntry e)
        {
            return map.TryGetValue(cn, out e);
        }

        public bool ContainsKey(string cn)
        {
            return map.ContainsKey(cn);
        }

        // 判断词库里这个中文键的英文值是否与给定值等价（用于外置词库自动导入的差异判断）
        // 比较前先做归一化（下划线/驼峰/连字符 → 空格小写），避免 mathScore 与 math_score 被当成两个词。
        public bool MatchValue(string cn, string en)
        {
            LexEntry e;
            if (!map.TryGetValue(cn, out e)) return false;
            if (en == null) en = "";
            if (en.Trim().Length == 0) return e.IsStopWord;   // 空值 = 停用词
            if (e.IsStopWord) return false;
            string[] cands = en.Split('|');
            for (int i = 0; i < cands.Length; i++)
            {
                string c = Normalize(cands[i]);
                if (c.Length == 0) return false;
                bool hit = false;
                for (int k = 0; k < e.Translations.Count; k++)
                {
                    if (Normalize(e.Translations[k]) == c) { hit = true; break; }
                }
                if (!hit) return false;
            }
            return true;
        }

        // 归一化：拆词 → 小写 → 空白折叠，用于“两个英文写法是否等价”的判断
        public static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            string w = SplitWords(s);
            StringBuilder sb = new StringBuilder(w.Length);
            bool prevSpace = false;
            for (int i = 0; i < w.Length; i++)
            {
                char c = char.ToLowerInvariant(w[i]);
                if (c == ' ' || c == '\t' || c == '_' || c == '-' || c == '.')
                {
                    if (sb.Length > 0 && !prevSpace) { sb.Append(' '); prevSpace = true; }
                    continue;
                }
                sb.Append(c);
                prevSpace = false;
            }
            if (sb.Length > 0 && sb[sb.Length - 1] == ' ') sb.Length--;
            return sb.ToString();
        }

        public int Count { get { return map.Count; } }
        public int WordCount
        {
            get
            {
                int n = 0;
                foreach (LexEntry e in map.Values) if (!e.IsStopWord) n++;
                return n;
            }
        }

        // 双向搜索：中文键包含关键词，或任一英文译名包含关键词（忽略大小写）
        // 用于词库管理的“查词”功能：输入中文查英文、输入英文查中文
        public List<KeyValuePair<string, string>> Search(string kw, int limit)
        {
            List<KeyValuePair<string, string>> res = new List<KeyValuePair<string, string>>();
            if (string.IsNullOrEmpty(kw)) return res;
            string k = kw.Trim();
            if (k.Length == 0) return res;
            foreach (KeyValuePair<string, LexEntry> kv in map)
            {
                if (kv.Value.IsStopWord) continue;
                bool hit = kv.Key.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0;
                if (!hit)
                {
                    for (int i = 0; i < kv.Value.Translations.Count; i++)
                        if (kv.Value.Translations[i].IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0) { hit = true; break; }
                }
                if (!hit) continue;
                res.Add(new KeyValuePair<string, string>(kv.Key, string.Join("|", kv.Value.Translations.ToArray())));
                if (limit > 0 && res.Count >= limit) break;
            }
            res.Sort(delegate(KeyValuePair<string, string> a, KeyValuePair<string, string> b)
            {
                if (a.Key.Length != b.Key.Length) return a.Key.Length.CompareTo(b.Key.Length);
                return string.CompareOrdinal(a.Key, b.Key);
            });
            return res;
        }

        // 英文输入规范化：拆驼峰 → 下划线/连字符/点变空格 → 折叠空格 → 小写
        // 这样 "userName"、"user_name"、"user name" 都会得到 "user name"
        public static string NormalizeEnglish(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            string w = SplitWords(s);
            StringBuilder sb = new StringBuilder(w.Length);
            bool prevSpace = false;
            for (int i = 0; i < w.Length; i++)
            {
                char c = w[i];
                if (c == '_' || c == '-' || c == '.' || c == ' ' || c == '	') { prevSpace = true; continue; }
                if (prevSpace && sb.Length > 0) sb.Append(' ');
                prevSpace = false;
                sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }

        // 单个英文词/词组 → 最匹配的**一个**中文
        // 排序：精确匹配 > 前缀/整词命中 > 包含；同档优先 2 字以上、再按字数、再按字典序
        public string LookupBestChinese(string word)
        {
            if (string.IsNullOrEmpty(word)) return null;
            // 比较时把空格去掉：词库里 "用户名=username"，而输入可能是 "user name"
            string w = NormalizeEnglish(word).Replace(" ", "");
            if (w.Length == 0) return null;
            string best = null; int bestTier = 99, bestLen = 999;
            foreach (KeyValuePair<string, LexEntry> kv in map)
            {
                if (kv.Value.IsStopWord) continue;
                int tier = 99;
                for (int i = 0; i < kv.Value.Translations.Count; i++)
                {
                    string tr = kv.Value.Translations[i].ToLowerInvariant().Replace(" ", "");
                    if (tr.Length == 0) continue;
                    if (tr == w) { tier = 0; break; }
                    if (tr.StartsWith(w, StringComparison.Ordinal) || tr.EndsWith(w, StringComparison.Ordinal))
                    { if (tier > 1) tier = 1; }
                    else if (tr.IndexOf(w, StringComparison.Ordinal) >= 0) { if (tier > 2) tier = 2; }
                }
                if (tier >= 99) continue;
                int len = kv.Key.Length;
                if (len < 2) len += 100;                       // 单字词排在同档最后
                bool better = tier < bestTier
                    || (tier == bestTier && len < bestLen)
                    || (tier == bestTier && len == bestLen && best != null && string.CompareOrdinal(kv.Key, best) < 0);
                if (better) { bestTier = tier; bestLen = len; best = kv.Key; }
            }
            return best;
        }

        // 整句反查：先按整体（user name / user_name / userName 都归一成 user name）查；
        // 整体没命中就逐词查，用 " + " 组合（用户 + 名字）
        public string LookupPhrase(string typed)
        {
            if (string.IsNullOrEmpty(typed)) return null;
            string whole = NormalizeEnglish(typed);
            if (whole.Length == 0) return null;
            string one = LookupBestChinese(whole);
            if (one != null) return one;
            string[] parts = whole.Split(' ');
            if (parts.Length <= 1) return null;
            List<string> cn = new List<string>();
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length == 0) continue;
                string m = LookupBestChinese(parts[i]);
                if (m != null && !cn.Contains(m)) cn.Add(m);
            }
            if (cn.Count == 0) return null;
            return string.Join(" + ", cn.ToArray());
        }

        // 英文 → 中文反查：译名里包含关键词的中文键（忽略大小写与空格，score 能匹配 math Score）
        public List<string> ReverseLookup(string en, int limit)
        {
            List<string> res = new List<string>();
            if (string.IsNullOrEmpty(en)) return res;
            string k = en.Trim().Replace(" ", "");
            if (k.Length == 0) return res;
            foreach (KeyValuePair<string, LexEntry> kv in map)
            {
                if (kv.Value.IsStopWord) continue;
                bool hit = false;
                for (int i = 0; i < kv.Value.Translations.Count; i++)
                {
                    string tr = kv.Value.Translations[i].Replace(" ", "");
                    if (tr.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0) { hit = true; break; }
                }
                if (!hit) continue;
                res.Add(kv.Key);
                if (limit > 0 && res.Count >= limit) break;
            }
            res.Sort(delegate(string a, string b)
            {
                if (a.Length != b.Length) return a.Length.CompareTo(b.Length);
                return string.CompareOrdinal(a, b);
            });
            return res;
        }

        public void Remove(string cn)
        {
            map.Remove(cn);
        }
    }
}