using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace VarNamer
{
    public enum TokenKind { Word, Literal, BooleanFlag, PluralTrigger, Unknown }
    public enum CaseStyle { Pascal, Camel, Snake, Screaming, Kebab, Dot }

    public class Token
    {
        public TokenKind Kind;
        public string Source;
        public List<string> Candidates = new List<string>();
        public int CandidateIndex;
        public bool Plural;

        public string CurrentCandidate()
        {
            if (Candidates.Count == 0) return "";
            int i = CandidateIndex;
            if (i < 0) i = 0;
            if (i >= Candidates.Count) i = 0;
            return Candidates[i];
        }

        public List<string> Words()
        {
            List<string> outList = new List<string>();
            string cand = CurrentCandidate();
            string[] parts = cand.Split(' ');
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i].Trim();
                if (p.Length > 0) outList.Add(p);
            }
            if (outList.Count == 0) return outList;
            if (Plural) outList[outList.Count - 1] = Pluralizer.Pluralize(outList[outList.Count - 1]);
            return outList;
        }
    }

    public class NameOptions
    {
        public string TypePrefix = "";
        public bool BooleanPrefix = true;
        public bool PluralizeTriggers = true;
        public int BriefLength = 4;
    }

    public class NameLine
    {
        public string Label;
        public string Value;
        public NameLine(string label, string value) { Label = label; Value = value; }
    }

    public class NameResult
    {
        public List<Token> Tokens = new List<Token>();
        public List<string> Unknowns = new List<string>();
        public List<NameLine> Lines = new List<NameLine>();
        public bool BooleanDetected;
    }

    public static class Pluralizer
    {
        private static readonly Dictionary<string, string> irregular = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "person", "people" }, { "child", "children" }, { "man", "men" }, { "woman", "women" },
            { "foot", "feet" }, { "tooth", "teeth" }, { "mouse", "mice" }, { "goose", "geese" },
            { "data", "data" }, { "info", "info" }, { "status", "statuses" }, { "index", "indices" },
            { "box", "boxes" }, { "class", "classes" }, { "match", "matches" }, { "batch", "batches" },
            { "score", "scores" }, { "student", "students" }, { "user", "users" }, { "item", "items" }
        };

        public static string Pluralize(string w)
        {
            if (string.IsNullOrEmpty(w)) return w;
            string lower = w.ToLowerInvariant();
            string hit;
            if (irregular.TryGetValue(lower, out hit))
            {
                if (w.Length > 0 && char.IsUpper(w[0]) && hit.Length > 0)
                    return char.ToUpperInvariant(hit[0]) + hit.Substring(1);
                return hit;
            }
            if (lower.EndsWith("s") || lower.EndsWith("x") || lower.EndsWith("z")
                || lower.EndsWith("ch") || lower.EndsWith("sh"))
                return w + "es";
            if (lower.Length >= 2 && lower.EndsWith("y"))
            {
                char prev = lower[lower.Length - 2];
                if ("aeiou".IndexOf(prev) < 0) return w.Substring(0, w.Length - 1) + "ies";
            }
            if (lower.EndsWith("f")) return w.Substring(0, w.Length - 1) + "ves";
            return w + "s";
        }
    }

    public static class Namer
    {
        private static readonly HashSet<string> pluralTriggers = new HashSet<string>(StringComparer.Ordinal)
        { "\u4eec", "\u6240\u6709", "\u591a\u4e2a", "\u5404\u4e2a", "\u4e00\u4e9b", "\u4e00\u6279", "\u82e5\u5e72" };

        private static readonly HashSet<string> acronyms = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "id","url","api","sql","db","uuid","guid","ip","tcp","udp","http","https","json","xml","html","css",
          "ssh","ftp","gpa","os","ui","uri","cpu","gpu","ram","io","mac","pid","jwt","sdk","orm","dto","dao" };

        private static readonly string boolTrigger = "\u662f\u5426";

        public static bool IsCjk(char c)
        {
            return (c >= '\u4e00' && c <= '\u9fff') || (c >= '\u3400' && c <= '\u4dbf') || (c >= '\uf900' && c <= '\ufaff');
        }

        private static bool IsWordChar(char c)
        {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_' || c == '$';
        }

        public static string Normalize(string input)
        {
            if (input == null) return "";
            StringBuilder sb = new StringBuilder(input.Length);
            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                if (c == '\u3000') { sb.Append(' '); continue; }
                if (c >= '\uff01' && c <= '\uff5e') { sb.Append((char)(c - 0xfee0)); continue; }
                if (c == '\uffe5') { sb.Append(' '); continue; }
                sb.Append(c);
            }
            return sb.ToString();
        }

        public static NameResult Create(string input, Lexicon lex, NameOptions opt)
        {
            if (opt == null) opt = new NameOptions();
            NameResult res = new NameResult();
            string text = Normalize(input);
            int i = 0;
            while (i < text.Length)
            {
                char c = text[i];
                if (IsCjk(c))
                {
                    int runEnd = i;
                    while (runEnd < text.Length && IsCjk(text[runEnd])) runEnd++;
                    int runLen = runEnd - i;

                    string seg = null;
                    LexEntry entry = null;
                    int maxL = Math.Min(lex.MaxKeyLength, runLen);
                    for (int L = maxL; L >= 1; L--)
                    {
                        string cand = text.Substring(i, L);
                        LexEntry e;
                        if (lex.TryGet(cand, out e)) { seg = cand; entry = e; break; }
                    }
                    if (entry == null)
                    {
                        Token un = new Token();
                        un.Kind = TokenKind.Unknown;
                        un.Source = text.Substring(i, 1);
                        un.Candidates.Add("");
                        res.Tokens.Add(un);
                        res.Unknowns.Add(text.Substring(i, 1));
                        i += 1;
                        continue;
                    }
                    int take = seg.Length;
                    if (seg == boolTrigger)
                    {
                        Token bt = new Token();
                        bt.Kind = TokenKind.BooleanFlag;
                        bt.Source = seg;
                        bt.Candidates.Add("is");
                        res.Tokens.Add(bt);
                        res.BooleanDetected = true;
                    }
                    else if (pluralTriggers.Contains(seg))
                    {
                        Token pt = new Token();
                        pt.Kind = TokenKind.PluralTrigger;
                        pt.Source = seg;
                        res.Tokens.Add(pt);
                    }
                    else if (!entry.IsStopWord)
                    {
                        Token wt = new Token();
                        wt.Kind = TokenKind.Word;
                        wt.Source = seg;
                        wt.Candidates.AddRange(entry.Translations);
                        res.Tokens.Add(wt);
                    }
                    i += take;
                    continue;
                }
                if (IsWordChar(c))
                {
                    int j = i;
                    while (j < text.Length && IsWordChar(text[j])) j++;
                    string lit = text.Substring(i, j - i);
                    Token lt = new Token();
                    lt.Kind = TokenKind.Literal;
                    lt.Source = lit;
                    lt.Candidates.Add(lit);
                    res.Tokens.Add(lt);
                    i = j;
                    continue;
                }
                i++;
            }

            if (opt.PluralizeTriggers)
            {
                List<Token> kept = new List<Token>();
                bool pending = false;
                for (int k = 0; k < res.Tokens.Count; k++)
                {
                    Token t = res.Tokens[k];
                    if (t.Kind == TokenKind.PluralTrigger) { pending = true; continue; }
                    if (pending && (t.Kind == TokenKind.Word || t.Kind == TokenKind.Literal))
                    {
                        t.Plural = true;
                        pending = false;
                    }
                    kept.Add(t);
                }
                res.Tokens = kept;
            }

            res.Lines = Render(res.Tokens, opt);
            return res;
        }

        public static List<string> AllWords(List<Token> tokens)
        {
            List<string> words = new List<string>();
            for (int i = 0; i < tokens.Count; i++)
            {
                if (tokens[i].Kind != TokenKind.Word && tokens[i].Kind != TokenKind.Literal) continue;
                words.AddRange(tokens[i].Words());
            }
            // 相邻重复词去重（只做“完全相同”的安全去重，避免 员工+工号 这类组合出现 employeeEmployee…）
            List<string> unique = new List<string>();
            for (int i = 0; i < words.Count; i++)
            {
                if (unique.Count > 0 && string.Equals(unique[unique.Count - 1], words[i], StringComparison.OrdinalIgnoreCase))
                    continue;
                unique.Add(words[i]);
            }
            return unique;
        }

        public static List<NameLine> Render(List<Token> tokens, NameOptions opt)
        {
            List<NameLine> lines = new List<NameLine>();
            List<string> words = AllWords(tokens);
            if (words.Count == 0) return lines;

            string prefix = PrefixOf(tokens, opt);
            List<string> withPrefix = new List<string>();
            if (prefix.Length > 0) withPrefix.Add(prefix);
            withPrefix.AddRange(words);

            lines.Add(new NameLine("\u5168\u79f0 PascalCase", JoinSeq(withPrefix, CaseStyle.Pascal)));
            lines.Add(new NameLine("\u5168\u79f0 camelCase", JoinSeq(withPrefix, CaseStyle.Camel)));
            lines.Add(new NameLine("\u5168\u79f0 snake_case", JoinSeq(withPrefix, CaseStyle.Snake)));
            lines.Add(new NameLine("\u5168\u79f0 SCREAMING_SNAKE_CASE", JoinSeq(withPrefix, CaseStyle.Screaming)));
            lines.Add(new NameLine("\u5168\u79f0 kebab-case", JoinSeq(withPrefix, CaseStyle.Kebab)));
            lines.Add(new NameLine("\u5168\u79f0 dot.case", JoinSeq(withPrefix, CaseStyle.Dot)));

            List<string> brief = BriefWords(withPrefix, opt.BriefLength);
            lines.Add(new NameLine("\u7b80\u77ed PascalCase(\u6bcf\u8bcd\u2264" + opt.BriefLength + ")", JoinSeq(brief, CaseStyle.Pascal)));
            lines.Add(new NameLine("\u7b80\u77ed camelCase(\u6bcf\u8bcd\u2264" + opt.BriefLength + ")", JoinSeq(brief, CaseStyle.Camel)));
            lines.Add(new NameLine("\u7b80\u77ed snake_case(\u6bcf\u8bcd\u2264" + opt.BriefLength + ")", JoinSeq(brief, CaseStyle.Snake)));

            List<string> abbr3 = AbbrevSegments(tokens, 3, prefix);
            lines.Add(new NameLine("\u7f29\u5199 camelCase(\u6bcf\u6bb5\u22643)", JoinSeq(abbr3, CaseStyle.Camel)));
            lines.Add(new NameLine("\u7f29\u5199 snake_case(\u6bcf\u6bb5\u22643)", JoinSeq(abbr3, CaseStyle.Snake)));

            List<string> abbr1 = AbbrevSegments(tokens, 1, prefix);
            lines.Add(new NameLine("\u7f29\u5199 \u9996\u5b57\u6bcd(\u5c0f\u5199)", JoinFlat(abbr1, false)));
            lines.Add(new NameLine("\u7f29\u5199 \u9996\u5b57\u6bcd(\u5927\u5199)", JoinFlat(abbr1, true)));

            return lines;
        }

        private static string PrefixOf(List<Token> tokens, NameOptions opt)
        {
            bool hasBool = false;
            for (int i = 0; i < tokens.Count; i++) if (tokens[i].Kind == TokenKind.BooleanFlag) hasBool = true;
            if (hasBool && opt.BooleanPrefix) return "is";
            if (opt.TypePrefix != null && opt.TypePrefix.Length > 0) return opt.TypePrefix;
            return "";
        }

        public static List<string> BriefWords(List<string> words, int maxLen)
        {
            List<string> outList = new List<string>();
            if (maxLen < 1) maxLen = 1;
            for (int i = 0; i < words.Count; i++)
            {
                string w = words[i];
                if (w.Length > maxLen) w = w.Substring(0, maxLen);
                outList.Add(w);
            }
            return outList;
        }

        public static List<string> AbbrevSegments(List<Token> tokens, int perWordMax, string prefix)
        {
            List<string> segs = new List<string>();
            if (prefix != null && prefix.Length > 0)
                segs.Add(prefix.Length > perWordMax ? prefix.Substring(0, perWordMax) : prefix);
            for (int i = 0; i < tokens.Count; i++)
            {
                Token t = tokens[i];
                if (t.Kind != TokenKind.Word && t.Kind != TokenKind.Literal) continue;
                List<string> w = t.Words();
                if (w.Count == 0) continue;
                if (w.Count == 1)
                {
                    string one = w[0];
                    if (one.Length > perWordMax) one = one.Substring(0, perWordMax);
                    segs.Add(one.ToLowerInvariant());
                }
                else
                {
                    StringBuilder sb = new StringBuilder();
                    for (int k = 0; k < w.Count; k++)
                    {
                        string one = w[k];
                        int take = Math.Min(perWordMax, one.Length);
                        if (take > 0) sb.Append(one.Substring(0, take));
                    }
                    segs.Add(sb.ToString().ToLowerInvariant());
                }
            }
            return segs;
        }

        private static bool IsPreserve(string w)
        {
            for (int i = 1; i < w.Length; i++) if (char.IsUpper(w[i])) return true;
            return false;
        }

        private static bool IsAcronym(string w)
        {
            if (w.Length == 0) return false;
            for (int i = 0; i < w.Length; i++) if (!char.IsLetter(w[i])) return false;
            return acronyms.Contains(w);
        }

        private static string Cap(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return char.ToUpperInvariant(s[0]) + s.Substring(1);
        }

        private static string StyleWord(string w, bool first, CaseStyle style)
        {
            string lower = w.ToLowerInvariant();
            switch (style)
            {
                case CaseStyle.Snake:
                case CaseStyle.Kebab:
                case CaseStyle.Dot:
                    return lower;
                case CaseStyle.Screaming:
                    return lower.ToUpperInvariant();
                case CaseStyle.Pascal:
                    if (IsPreserve(w)) return Cap(w);
                    if (IsAcronym(w)) return w.ToUpperInvariant();
                    return Cap(lower);
                case CaseStyle.Camel:
                    if (IsPreserve(w)) return first ? w : Cap(w);
                    if (IsAcronym(w)) return first ? lower : lower.ToUpperInvariant();
                    return first ? lower : Cap(lower);
            }
            return lower;
        }

        public static string JoinFlat(List<string> segs, bool upper)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < segs.Count; i++)
            {
                string s = segs[i];
                sb.Append(upper ? s.ToUpperInvariant() : s.ToLowerInvariant());
            }
            return sb.ToString();
        }
        public static string JoinSeq(List<string> words, CaseStyle style)
        {
            if (words == null || words.Count == 0) return "";
            string sep = "_";
            if (style == CaseStyle.Kebab) sep = "-";
            else if (style == CaseStyle.Dot) sep = ".";
            else if (style == CaseStyle.Pascal || style == CaseStyle.Camel) sep = "";
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < words.Count; i++)
            {
                if (i > 0) sb.Append(sep);
                sb.Append(StyleWord(words[i], i == 0, style));
            }
            return sb.ToString();
        }
    }
}