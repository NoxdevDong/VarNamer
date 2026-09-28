using System;
using System.Collections.Generic;

namespace VarNamer
{
    // 语言预设：决定“变量名/类名/常量名”该用哪种命名风格，以及新手简洁模式显示哪几行
    public static class LanguagePreset
    {
        public static readonly string[] Names = new string[]
        {
            "通用", "Java / C#", "Python", "JavaScript / TS", "Go", "C / C++", "SQL", "CSS"
        };

        private static string Key(string label)
        {
            if (label == null) return "all";
            if (label == "通用") return "all";
            if (label.StartsWith("CSS")) return "css";
            if (label.StartsWith("Java")) return "java";
            if (label.StartsWith("Python")) return "python";
            if (label.StartsWith("JavaScript")) return "js";
            if (label.StartsWith("Go")) return "go";
            if (label.StartsWith("C /")) return "cpp";
            if (label.StartsWith("SQL")) return "sql";
            if (label.StartsWith("Web")) return "css";
            if (label.StartsWith("前端")) return "css";
            return "all";
        }

        public static bool IsAll(string label) { return Key(label) == "all"; }

        // 变量名风格 -> 主列表行号
        public static int VariableRow(string label)
        {
            switch (Key(label))
            {
                case "python": return 2;   // snake_case
                case "cpp": return 2;      // snake_case
                case "sql": return 2;      // snake_case
                case "css": return 4;      // kebab-case
                default: return 1;         // camelCase
            }
        }

        public static int ClassRow(string label)
        {
            switch (Key(label))
            {
                case "python": return 0;   // PascalCase
                case "sql": return 2;      // snake_case（表名）
                case "css": return 4;      // kebab-case（类名）
                default: return 0;         // PascalCase
            }
        }

        public static int ConstantRow(string label)
        {
            switch (Key(label))
            {
                case "css": return 4;
                case "sql": return 2;
                default: return 3;         // SCREAMING_SNAKE_CASE
            }
        }

        // 简洁模式下要显示的 3 行（行号, 显示标签）
        public static List<KeyValuePair<int, string>> BriefRows(string label)
        {
            List<KeyValuePair<int, string>> list = new List<KeyValuePair<int, string>>();
            if (IsAll(label)) return list;    // 通用 = 显示全部 13 行
            list.Add(new KeyValuePair<int, string>(VariableRow(label), "变量名"));
            list.Add(new KeyValuePair<int, string>(ClassRow(label), "类名 / 表名"));
            list.Add(new KeyValuePair<int, string>(ConstantRow(label), "常量名"));
            return list;
        }

        // 悬浮窗 5 行的行号映射（行号含义：0=变量 1=类 2=常量 3=简短 4=缩写）
        public static int[] FloatRows(string label)
        {
            return new int[] { VariableRow(label), ClassRow(label), ConstantRow(label), 7, 12 };
        }

        public static string[] FloatLabels(string label)
        {
            if (IsAll(label)) return new string[] { "Pascal", "camel", "snake", "简短", "缩写" };
            return new string[] { "变量", "类", "常量", "简短", "缩写" };
        }
    }
}