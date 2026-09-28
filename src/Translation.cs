using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace VarNamer
{
    public class TranslateConfig
    {
        public bool Enabled = false;
        public string Provider = "baidu";     // baidu | youdao | deepl | custom
        public string AppId = "";             // 百度 appid / 有道 appKey / DeepL 留空
        public string Key = "";               // 百度密钥 / 有道 appSecret / DeepL auth_key
        public string CustomUrl = "";         // 自定义：GET 模板，用 {q} 占位
        public bool AutoAddToDict = true;     // 翻译结果写入用户词库
        public int TimeoutMs = 6000;
    }

    // 在线翻译（可选）：百度 / 有道 / DeepL / 自定义 GET 模板。
    // 设计原则：离线优先——只用它兜底“内置词库没有的词”，成功结果写回用户词库，之后完全离线可用。
    public static class Translation
    {
        public static TranslateConfig Cfg = new TranslateConfig();
        public static string LastError = "";

        private static readonly Dictionary<string, string> cache = new Dictionary<string, string>();
        private static readonly Dictionary<string, string> failed = new Dictionary<string, string>();

        public static string Translate(string zh)
        {
            LastError = "";
            if (string.IsNullOrEmpty(zh)) return "";
            if (cache.ContainsKey(zh)) return cache[zh];
            if (failed.ContainsKey(zh)) return "";
            if (!Cfg.Enabled) { LastError = "未启用在线翻译"; return ""; }

            try
            {
                string body = Request(zh);
                string en = ParseResponse(Cfg.Provider, body);
                en = NormalizeEnglish(en);
                if (en == "")
                {
                    LastError = "响应未解析出译文: " + Short(body);
                    failed[zh] = "1";
                    return "";
                }
                cache[zh] = en;
                return en;
            }
            catch (Exception ex)
            {
                LastError = ex.GetType().Name + ": " + ex.Message;
                failed[zh] = "1";
                return "";
            }
        }

        private static string Request(string zh)
        {
            string url;
            string post = null;
            if (Cfg.Provider == "baidu")
            {
                string salt = ((long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds).ToString(CultureInfo.InvariantCulture);
                string sign = Md5(Cfg.AppId + zh + salt + Cfg.Key);
                url = "https://fanyi-api.baidu.com/api/trans/vip/translate?q=" + Enc(zh) + "&from=zh&to=en&appid="
                    + Enc(Cfg.AppId) + "&salt=" + salt + "&sign=" + sign;
            }
            else if (Cfg.Provider == "youdao")
            {
                string salt = Guid.NewGuid().ToString("N");
                string curtime = ((long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds).ToString(CultureInfo.InvariantCulture);
                string input = zh.Length <= 20 ? zh : (zh.Substring(0, 10) + zh.Length.ToString(CultureInfo.InvariantCulture) + zh.Substring(zh.Length - 10));
                string sign = Sha256(Cfg.AppId + input + salt + curtime + Cfg.Key);
                url = "https://openapi.youdao.com/api?q=" + Enc(zh) + "&from=zh-CHS&to=en&appKey=" + Enc(Cfg.AppId)
                    + "&salt=" + salt + "&sign=" + sign + "&signType=v3&curtime=" + curtime;
            }
            else if (Cfg.Provider == "deepl")
            {
                url = "https://api-free.deepl.com/v2/translate";
                post = "auth_key=" + Enc(Cfg.Key) + "&text=" + Enc(zh) + "&target_lang=EN";
            }
            else
            {
                if (string.IsNullOrEmpty(Cfg.CustomUrl)) throw new InvalidOperationException("自定义 URL 未填写");
                url = Cfg.CustomUrl.Replace("{q}", Enc(zh));
            }
            return HttpGet(url, post, Cfg.TimeoutMs);
        }

        private static string HttpGet(string url, string postBody, int timeoutMs)
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = postBody == null ? "GET" : "POST";
            req.Timeout = timeoutMs;
            req.ReadWriteTimeout = timeoutMs;
            req.UserAgent = "VarNamer/1.8";
            if (postBody != null)
            {
                req.ContentType = "application/x-www-form-urlencoded";
                byte[] data = Encoding.UTF8.GetBytes(postBody);
                req.ContentLength = data.Length;
                using (Stream s = req.GetRequestStream()) s.Write(data, 0, data.Length);
            }
            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
            using (Stream s = resp.GetResponseStream())
            using (StreamReader r = new StreamReader(s, Encoding.UTF8))
                return r.ReadToEnd();
        }

        // 解析各家响应（手写轻量 JSON 取值，避免引入第三方库）
        public static string ParseResponse(string provider, string body)
        {
            if (string.IsNullOrEmpty(body)) return "";
            if (provider == "baidu")
            {
                if (body.IndexOf("\"error_code\"") >= 0) { LastError = "百度返回错误: " + JsonStr(body, "error_msg"); return ""; }
                return JsonStr(body, "dst");
            }
            if (provider == "youdao")
            {
                string code = JsonStr(body, "errorCode");
                if (!string.IsNullOrEmpty(code) && code != "0") return "";
                return JsonStr(body, "translation");
            }
            if (provider == "deepl") return JsonStr(body, "text");
            // custom：优先取 translation/text，否则整段当作译文
            string v = JsonStr(body, "translation");
            if (v == "") v = JsonStr(body, "text");
            if (v == "") v = body;
            return v;
        }

        // 取 JSON 中第一个 "key":"value" 的 value（支持转义与 \uXXXX）
        public static string JsonStr(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return "";
            string pat = "\"" + key + "\"";
            int i = json.IndexOf(pat, StringComparison.Ordinal);
            if (i < 0) return "";
            i = json.IndexOf(':', i + pat.Length);
            if (i < 0) return "";
            i++;
            while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
            if (i >= json.Length) return "";
            if (json[i] == '[')   // 数组：取第一个字符串元素
            {
                i++;
                while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
                if (i < json.Length && json[i] == '"') return ReadString(json, i);
                if (i < json.Length && json[i] == '{')
                {
                    int end = json.IndexOf('}', i);
                    if (end < 0) return "";
                    string inner = json.Substring(i, end - i);
                    string v2 = JsonStr(inner, "dst");
                    if (v2 == "") v2 = JsonStr(inner, "text");
                    return v2;
                }
                return "";
            }
            if (json[i] != '"') return "";
            return ReadString(json, i);
        }

        private static string ReadString(string json, int quoteStart)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = quoteStart + 1; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '\\' && i + 1 < json.Length)
                {
                    i++;
                    char e = json[i];
                    if (e == 'n') sb.Append(' ');
                    else if (e == 't') sb.Append(' ');
                    else if (e == 'u' && i + 4 < json.Length)
                    {
                        try
                        {
                            int cp = int.Parse(json.Substring(i + 1, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                            sb.Append((char)cp);
                            i += 4;
                        }
                        catch (Exception) { }
                    }
                    else sb.Append(e);
                    continue;
                }
                if (c == '"') break;
                sb.Append(c);
            }
            return sb.ToString();
        }

        // 译文清洗：只保留字母/数字与空格，转小写，压空格（贴合词库 value 格式）
        public static string NormalizeEnglish(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')) sb.Append(c);
                else if (c == ' ' || c == '-' || c == '_' || c == ',' || c == ';' || c == '/') sb.Append(' ');
                else if (c == '.') sb.Append(' ');
            }
            string[] parts = sb.ToString().Split(' ');
            StringBuilder outSb = new StringBuilder();
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i].Trim();
                if (p.Length == 0) continue;
                if (outSb.Length > 0) outSb.Append(' ');
                outSb.Append(p.ToLowerInvariant());
            }
            return outSb.ToString();
        }

        private static string Short(string s)
        {
            if (s == null) return "";
            return s.Length <= 120 ? s : s.Substring(0, 120) + "…";
        }

        public static string Enc(string s) { return Uri.EscapeDataString(s == null ? "" : s); }

        public static string Md5(string s)
        {
            using (MD5 md5 = MD5.Create())
            {
                byte[] h = md5.ComputeHash(Encoding.UTF8.GetBytes(s));
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < h.Length; i++) sb.Append(h[i].ToString("x2", CultureInfo.InvariantCulture));
                return sb.ToString();
            }
        }

        public static string Sha256(string s)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] h = sha.ComputeHash(Encoding.UTF8.GetBytes(s));
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < h.Length; i++) sb.Append(h[i].ToString("x2", CultureInfo.InvariantCulture));
                return sb.ToString();
            }
        }

        public static void ClearCache() { cache.Clear(); failed.Clear(); }
    }
}
