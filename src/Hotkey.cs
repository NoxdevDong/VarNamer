using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows.Forms;

namespace VarNamer
{
    // 全局热键的解析与格式化（配置里以字符串保存，如 Ctrl+Alt+V）
    public static class Hotkey
    {
        public const int MOD_ALT = 0x0001;
        public const int MOD_CONTROL = 0x0002;
        public const int MOD_SHIFT = 0x0004;
        public const int MOD_WIN = 0x0008;

        public static string Format(int mods, int vk)
        {
            StringBuilder sb = new StringBuilder();
            if ((mods & MOD_CONTROL) != 0) sb.Append("Ctrl+");
            if ((mods & MOD_ALT) != 0) sb.Append("Alt+");
            if ((mods & MOD_SHIFT) != 0) sb.Append("Shift+");
            if ((mods & MOD_WIN) != 0) sb.Append("Win+");
            sb.Append(VkName(vk));
            return sb.ToString();
        }

        public static string VkName(int vk)
        {
            if (vk >= 0x41 && vk <= 0x5A) return ((char)vk).ToString();          // A-Z
            if (vk >= 0x30 && vk <= 0x39) return ((char)vk).ToString();          // 0-9
            if (vk >= 0x70 && vk <= 0x87) return "F" + (vk - 0x6F);              // F1-F24
            Keys k = (Keys)vk;
            if (k == Keys.Space) return "Space";
            if (k == Keys.Oemtilde) return "`";
            if (k == Keys.OemMinus) return "-";
            if (k == Keys.Oemplus) return "=";
            if (k == Keys.OemOpenBrackets) return "[";
            if (k == Keys.OemCloseBrackets) return "]";
            if (k == Keys.OemPipe) return "\\";
            if (k == Keys.OemSemicolon) return ";";
            if (k == Keys.OemQuotes) return "'";
            if (k == Keys.Oemcomma) return ",";
            if (k == Keys.OemPeriod) return ".";
            if (k == Keys.OemQuestion) return "/";
            if (k == Keys.Up) return "Up";
            if (k == Keys.Down) return "Down";
            if (k == Keys.Left) return "Left";
            if (k == Keys.Right) return "Right";
            return k.ToString();
        }

        public static int VkFromName(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            s = s.Trim().ToUpperInvariant();
            if (s.Length == 1)
            {
                char c = s[0];
                if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')) return (int)c;
                if (c == '`') return (int)Keys.Oemtilde;
                if (c == '-') return (int)Keys.OemMinus;
                if (c == '=') return (int)Keys.Oemplus;
                if (c == '[') return (int)Keys.OemOpenBrackets;
                if (c == ']') return (int)Keys.OemCloseBrackets;
                if (c == '\\') return (int)Keys.OemPipe;
                if (c == ';') return (int)Keys.OemSemicolon;
                if (c == '\'') return (int)Keys.OemQuotes;
                if (c == ',') return (int)Keys.Oemcomma;
                if (c == '.') return (int)Keys.OemPeriod;
                if (c == '/') return (int)Keys.OemQuestion;
            }
            if (s == "SPACE") return (int)Keys.Space;
            if (s == "UP") return (int)Keys.Up;
            if (s == "DOWN") return (int)Keys.Down;
            if (s == "LEFT") return (int)Keys.Left;
            if (s == "RIGHT") return (int)Keys.Right;
            if (s.StartsWith("F") && s.Length <= 3)
            {
                int n;
                if (int.TryParse(s.Substring(1), out n) && n >= 1 && n <= 24) return 0x6F + n;
            }
            return 0;
        }

        public static bool TryParse(string text, out int mods, out int vk)
        {
            mods = 0; vk = 0;
            if (string.IsNullOrEmpty(text)) return false;
            string[] parts = text.Split('+');
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i].Trim();
                if (p.Length == 0) continue;
                string u = p.ToUpperInvariant();
                if (u == "CTRL" || u == "CONTROL") mods |= MOD_CONTROL;
                else if (u == "ALT") mods |= MOD_ALT;
                else if (u == "SHIFT") mods |= MOD_SHIFT;
                else if (u == "WIN") mods |= MOD_WIN;
                else vk = VkFromName(p);
            }
            return mods != 0 && vk != 0;
        }

        public static string FromKeys(Keys keyData)
        {
            int mods = 0;
            if ((keyData & Keys.Control) == Keys.Control) mods |= MOD_CONTROL;
            if ((keyData & Keys.Alt) == Keys.Alt) mods |= MOD_ALT;
            if ((keyData & Keys.Shift) == Keys.Shift) mods |= MOD_SHIFT;
            Keys k = keyData & Keys.KeyCode;
            if (k == Keys.ControlKey || k == Keys.ShiftKey || k == Keys.Menu || k == Keys.LWin || k == Keys.RWin)
                return Format(mods, 0).TrimEnd('+');
            return Format(mods, (int)k);
        }
    }
}
