using System;
using System.Threading;
using System.Windows.Forms;

namespace VarNamer
{
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
