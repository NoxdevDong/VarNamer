using System;
using System.Threading;
using System.Windows.Forms;

namespace VarNamer
{
    public static class TextUtil
    {
        // 是否含中日韩字符（用来判断“输入的是中文还是英文”）
        public static bool HasCjk(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            for (int i = 0; i < s.Length; i++) if (s[i] >= 0x2E80) return true;
            return false;
        }
    }

    public static class Clip
    {
        public static bool SetText(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    Clipboard.SetDataObject(text, true);
                    return true;
                }
                catch (Exception)
                {
                    Thread.Sleep(40);
                }
            }
            return false;
        }

        public static string GetText()
        {
            try
            {
                if (Clipboard.ContainsText()) return Clipboard.GetText();
            }
            catch (Exception) { }
            return "";
        }
    }
}
