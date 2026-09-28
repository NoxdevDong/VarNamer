using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace VarNamer
{
    public static class EngineTest
    {
        private static readonly string[] Cases = new string[] {
            "数学成绩", "用户列表", "用户名密码", "是否激活", "学生姓名", "订单总价",
            "获取用户信息", "库存数量", "客户订单编号", "上传文件路径", "学生成绩排名",
            "连接池最大连接数", "是否启用缓存", "中文夹杂abc端口", "所有学生", "骆駉数据",
            "商品单价和库存数量", "一年级学生名单", "缓存命中率", "订单创建时间"
        };

        public static int Run(string outPath)
        {
            StringBuilder sb = new StringBuilder();
            Lexicon lex = Lexicon.Create();
            sb.AppendLine("== VarNamer 引擎自检 ==");
            sb.AppendLine("词库条目: " + lex.Count + "  可用词条: " + lex.WordCount + "  最长键: " + lex.MaxKeyLength);
            sb.AppendLine();

            NameOptions opt = new NameOptions();
            int problems = 0;
            for (int i = 0; i < Cases.Length; i++)
            {
                NameResult r = Namer.Create(Cases[i], lex, opt);
                sb.AppendLine("输入  : " + Cases[i]);
                sb.Append("分词  : ");
                for (int k = 0; k < r.Tokens.Count; k++)
                {
                    Token t = r.Tokens[k];
                    if (k > 0) sb.Append(" | ");
                    if (t.Kind == TokenKind.Word) sb.Append(t.Source + "=" + t.CurrentCandidate());
                    else if (t.Kind == TokenKind.Literal) sb.Append("[" + t.Source + "]");
                    else if (t.Kind == TokenKind.BooleanFlag) sb.Append(t.Source + "=bool");
                    else sb.Append(t.Source + "=?");
                }
                sb.AppendLine();
                for (int L = 0; L < r.Lines.Count; L++)
                    sb.AppendLine("    " + Pad(r.Lines[L].Label, 34) + " " + r.Lines[L].Value);
                if (r.Unknowns.Count > 0) sb.AppendLine("    !! 未识别字: " + string.Join(" ", r.Unknowns.ToArray()));
                if (r.Lines.Count == 0) { sb.AppendLine("    !! 空结果"); problems++; }
                sb.AppendLine();
            }

            problems += Assert(lex, "数学成绩", "MathScore", "mathScore", "math_score", "MS");
            problems += Assert(lex, "用户名密码", "UserNamePassword", "userNamePassword", "user_name_password", "UNP");
            problems += Assert(lex, "是否激活", "IsActivate", "isActivate", "is_activate", "IA");
            problems += Assert(lex, "用户列表", "UserList", "userList", "user_list", "UL");
            problems += Assert(lex, "所有学生", "Students", "students", "students", "S");

            sb.AppendLine(problems == 0 ? "RESULT: PASS" : ("RESULT: FAIL(" + problems + ")"));
            string text = sb.ToString();

            if (outPath != null && outPath.Length > 0)
            {
                try { File.WriteAllText(outPath, text, new UTF8Encoding(true)); }
                catch (Exception) { }
            }
            try
            {
                Console.OutputEncoding = Encoding.UTF8;
                Console.Write(text);
            }
            catch (Exception) { }
            return problems;
        }

        private static int Assert(Lexicon lex, string input, string pascal, string camel, string snake, string abbr)
        {
            NameResult r = Namer.Create(input, lex, new NameOptions());
            int bad = 0;
            bad += Check(input, "Pascal", Get(r, 0), pascal);
            bad += Check(input, "camel", Get(r, 1), camel);
            bad += Check(input, "snake", Get(r, 2), snake);
            bad += Check(input, "abbr1", Get(r, 12), abbr);
            return bad;
        }

        private static string Get(NameResult r, int idx)
        {
            if (idx < 0 || idx >= r.Lines.Count) return "<none>";
            return r.Lines[idx].Value;
        }

        private static int Check(string input, string style, string got, string want)
        {
            if (string.Equals(got, want, StringComparison.Ordinal)) return 0;
            Console.WriteLine("ASSERT FAIL [" + input + "/" + style + "] got=" + got + " want=" + want);
            return 1;
        }

        private static string Pad(string s, int n)
        {
            if (s.Length >= n) return s;
            return s + new string(' ', n - s.Length);
        }
    }
}
