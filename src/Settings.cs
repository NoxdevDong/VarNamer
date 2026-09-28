using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace VarNamer
{
    public class Config
    {
        public string TypePrefix = "";
        public int BriefLength = 4;
        public bool BooleanPrefix = true;
        public bool PluralizeTriggers = true;
        public int FloatX = 200, FloatY = 200, FloatW = 460, FloatH = 210;
        public double FloatOpacity = 1.0;        // 活动（点进来/正在输入）时的不透明度
        public double FloatOpacityIdle = 0.6;    // 默认（未激活）时的半透明程度
        public int FloatGroup = 0;               // 悬浮窗风格组：0 混合 / 1 全称 / 2 简短 / 3 缩写
        public bool FloatVisible = true;
        public string Hotkey = "Ctrl+Alt+V";
        // 在线翻译（可选）
        public bool TranslateEnabled = false;
        public string TranslateProvider = "baidu";
        public string TranslateAppId = "";
        public string TranslateKey = "";
        public string TranslateCustomUrl = "";
        public bool TranslateAutoAdd = true;
        // 新手友好选项
        public string Language = "通用";
        public bool BriefMode = false;         // 简洁模式：只显示变量名/类名/常量名
        public bool ReturnToPrevWindow = true; // 复制后自动切回上一个窗口
        public bool GuideShown = false;        // 首次引导是否已展示
        public string ThemeName = "dark";      // dark | light
        public string ShotHotkey = "Ctrl+Alt+A";   // 截图热键
        public string ShotDir = "";                // 截图默认保存目录
        public string DictSignature = "";          // 外置词库指纹（变了才自动重新导入）

        private static string dataDirCache;

        public static string DataDir
        {
            get
            {
                if (dataDirCache != null) return dataDirCache;
                dataDirCache = ResolveDataDir();
                return dataDirCache;
            }
        }

        public static string ResolveDataDir()
        {
            List<string> cands = new List<string>();
            string env = Environment.GetEnvironmentVariable("VARNAMER_DATA_DIR");
            if (!string.IsNullOrEmpty(env)) cands.Add(env);

            // 便携模式：exe 同级放了 portable.txt 就表示“数据跟着程序走”，
            // 配置/词库/记录都写在 exe 同级的 VarNamerData 里，不写 %APPDATA%。
            try
            {
                string selfDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                if (!string.IsNullOrEmpty(selfDir))
                {
                    string sub = Path.Combine(selfDir, "VarNamerData");
                    // portable.txt 在，或者同级已经有 VarNamerData 目录 → 都按便携处理（免得标记文件被删后跑去写系统盘）
                    if (File.Exists(Path.Combine(selfDir, "portable.txt")) || Directory.Exists(sub))
                        cands.Add(sub);
                }
            }
            catch (Exception) { }

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (!string.IsNullOrEmpty(appData)) cands.Add(Path.Combine(appData, "VarNamer"));
            try
            {
                string exeDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                if (!string.IsNullOrEmpty(exeDir)) cands.Add(Path.Combine(exeDir, "VarNamerData"));
            }
            catch (Exception) { }
            cands.Add(Path.Combine(Path.GetTempPath(), "VarNamer"));

            for (int i = 0; i < cands.Count; i++)
            {
                try
                {
                    string dir = cands[i];
                    Directory.CreateDirectory(dir);
                    string probe = Path.Combine(dir, ".write_probe");
                    File.WriteAllText(probe, "ok");
                    File.Delete(probe);
                    return dir;
                }
                catch (Exception) { }
            }
            return Path.GetTempPath();
        }
        public static string ConfigPath { get { return Path.Combine(DataDir, "config.ini"); } }
        public static string UserDictPath { get { return Path.Combine(DataDir, "userdict.txt"); } }

        public static Config Load()
        {
            Config c = new Config();
            try
            {
                if (!File.Exists(ConfigPath)) return c;
                string[] lines = File.ReadAllLines(ConfigPath, Encoding.UTF8);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (line.Length == 0 || line[0] == '#') continue;
                    int p = line.IndexOf('=');
                    if (p <= 0) continue;
                    string k = line.Substring(0, p).Trim();
                    string v = line.Substring(p + 1).Trim();
                    switch (k)
                    {
                        case "prefix": c.TypePrefix = v; break;
                        case "brief": c.BriefLength = ToInt(v, 4); break;
                        case "boolprefix": c.BooleanPrefix = v != "0"; break;
                        case "plural": c.PluralizeTriggers = v != "0"; break;
                        case "floatx": c.FloatX = ToInt(v, 200); break;
                        case "floaty": c.FloatY = ToInt(v, 200); break;
                        case "floatw": c.FloatW = ToInt(v, 460); break;
                        case "floath": c.FloatH = ToInt(v, 210); break;
                        case "floatopacity": c.FloatOpacity = ToDouble(v, 1.0); break;
            case "floatidle": c.FloatOpacityIdle = ToDouble(v, 0.6); break;
            case "floatgroup": c.FloatGroup = (int)ToDouble(v, 0); break;
                        case "floatshow": c.FloatVisible = v != "0"; break;
                        case "hotkey": c.Hotkey = v; break;
                        case "translate": c.TranslateEnabled = v != "0"; break;
                        case "trprovider": c.TranslateProvider = v; break;
                        case "trappid": c.TranslateAppId = v; break;
                        case "trkey": c.TranslateKey = v; break;
                        case "trurl": c.TranslateCustomUrl = v; break;
                        case "trautoadd": c.TranslateAutoAdd = v != "0"; break;
                        case "language": c.Language = (v == "通用（全部风格）") ? "通用" : v; break;
                        case "briefmode": c.BriefMode = v != "0"; break;
                        case "returnback": c.ReturnToPrevWindow = v != "0"; break;
                        case "guide": c.GuideShown = v != "0"; break;
                        case "theme": c.ThemeName = (v == "light") ? "light" : "dark"; break;
                        case "shothotkey": c.ShotHotkey = string.IsNullOrEmpty(v) ? "Ctrl+Alt+A" : v; break;
                        case "shotdir": c.ShotDir = v; break;
                        case "dictsig": c.DictSignature = v; break;
                    }
                }
            }
            catch (Exception) { }
            return c;
        }

        private static int ToInt(string s, int d)
        {
            int r;
            if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out r)) return r;
            return d;
        }

        private static double ToDouble(string s, double d)
        {
            double r;
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out r)) return r;
            return d;
        }

        public void Save()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("prefix=" + TypePrefix);
                sb.AppendLine("brief=" + BriefLength.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("boolprefix=" + (BooleanPrefix ? "1" : "0"));
                sb.AppendLine("plural=" + (PluralizeTriggers ? "1" : "0"));
                sb.AppendLine("floatx=" + FloatX.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("floaty=" + FloatY.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("floatw=" + FloatW.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("floath=" + FloatH.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("floatopacity=" + FloatOpacity.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("floatidle=" + FloatOpacityIdle.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("floatgroup=" + FloatGroup.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("floatshow=" + (FloatVisible ? "1" : "0"));
                sb.AppendLine("hotkey=" + Hotkey);
                sb.AppendLine("translate=" + (TranslateEnabled ? "1" : "0"));
                sb.AppendLine("trprovider=" + TranslateProvider);
                sb.AppendLine("trappid=" + TranslateAppId);
                sb.AppendLine("trkey=" + TranslateKey);
                sb.AppendLine("trurl=" + TranslateCustomUrl);
                sb.AppendLine("trautoadd=" + (TranslateAutoAdd ? "1" : "0"));
                sb.AppendLine("language=" + Language);
                sb.AppendLine("briefmode=" + (BriefMode ? "1" : "0"));
                sb.AppendLine("returnback=" + (ReturnToPrevWindow ? "1" : "0"));
                sb.AppendLine("guide=" + (GuideShown ? "1" : "0"));
                sb.AppendLine("theme=" + ThemeName);
                sb.AppendLine("shothotkey=" + ShotHotkey);
                sb.AppendLine("shotdir=" + ShotDir);
                sb.AppendLine("dictsig=" + DictSignature);
                File.WriteAllText(ConfigPath, sb.ToString(), new UTF8Encoding(false));
            }
            catch (Exception) { }
        }

        public void ApplyToTranslator()
        {
            Translation.Cfg.Enabled = TranslateEnabled;
            Translation.Cfg.Provider = TranslateProvider;
            Translation.Cfg.AppId = TranslateAppId;
            Translation.Cfg.Key = TranslateKey;
            Translation.Cfg.CustomUrl = TranslateCustomUrl;
            Translation.Cfg.AutoAddToDict = TranslateAutoAdd;
        }

        public NameOptions ToOptions()
        {
            NameOptions o = new NameOptions();
            o.TypePrefix = TypePrefix == null ? "" : TypePrefix;
            o.BriefLength = BriefLength < 1 ? 1 : BriefLength;
            o.BooleanPrefix = BooleanPrefix;
            o.PluralizeTriggers = PluralizeTriggers;
            return o;
        }
    }

    public class AppState
    {
        public Lexicon Lex = Lexicon.Create();
        public Config Cfg = Config.Load();
        public NameOptions Opt;
        public NameResult Current;
        public string LastInput = "";

        public AppState()
        {
            Theme.ApplyPalette(Cfg.ThemeName != "light");   // 先应用主题，再建界面
            Cfg.ApplyToTranslator();
            Opt = Cfg.ToOptions();
            Lex.LoadUserFile(Config.UserDictPath);
            Lex.Rebuild();
        }

        public void ApplyOptions()
        {
            Opt = Cfg.ToOptions();
        }

        public void AddUserEntry(string cn, string en)
        {
            if (cn == null) return;
            cn = cn.Trim();
            en = en == null ? "" : en.Trim();
            if (cn.Length == 0 || en.Length == 0) return;
            Lex.SetEntry(cn, en);      // 覆盖语义：用户加了同名条目就以用户的为准
            Lex.Rebuild();
            SaveUserEntry(cn, en);
        }

        private static void SaveUserEntry(string cn, string en)
        {
            try
            {
                File.AppendAllText(Config.UserDictPath, cn + "=" + en + Environment.NewLine, new UTF8Encoding(false));
            }
            catch (Exception) { }
        }

        public void SaveUserDict(List<string[]> entries)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("# VarNamer 用户词库  (格式: 中文=英文, 英文多词用空格, 同义词用 | 分隔)");
                for (int i = 0; i < entries.Count; i++)
                    sb.AppendLine(entries[i][0] + "=" + entries[i][1]);
                File.WriteAllText(Config.UserDictPath, sb.ToString(), new UTF8Encoding(false));
            }
            catch (Exception) { }
        }

        public List<string[]> LoadUserDictEntries()
        {
            List<string[]> list = new List<string[]>();
            try
            {
                if (!File.Exists(Config.UserDictPath)) return list;
                string[] lines = File.ReadAllLines(Config.UserDictPath, Encoding.UTF8);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (line.Length == 0 || line[0] == '#') continue;
                    int p = line.IndexOf('=');
                    if (p <= 0) continue;
                    list.Add(new string[] { line.Substring(0, p).Trim(), line.Substring(p + 1).Trim() });
                }
            }
            catch (Exception) { }
            return list;
        }
    }
}