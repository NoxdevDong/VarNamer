using System;
using System.Text;

namespace VarNamer
{
    // 在线翻译使用说明（显示在设置窗的「使用说明」里）
    public static class TranslateHelp
    {
        public static string ProviderNeed(string provider)
        {
            if (provider == "baidu") return "百度翻译：需填 APP ID 与 密钥（开放平台的「开发者信息」里，别把「应用名称」当密钥）";
            if (provider == "youdao") return "有道智云：需填 应用ID 与 应用密钥（控制台创建应用时选「文本翻译」）";
            if (provider == "deepl") return "DeepL：AppID 留空，只填认证密钥（形如 xxxxxx:fx，注册 API Free）";
            if (provider == "custom") return "自定义URL：AppID/密钥 留空，只填 URL 模板，用 {q} 作查询词占位";
            return "";
        }

        public static readonly string Text =
            "在线翻译 使用说明" + NL +
            "════════════════════════════════════════════════════════════" + NL +
            "【它做什么】" + NL +
            "  内置词库没命中的词，可以联网翻译兜底；翻译成功的词会自动写进你的" + NL +
            "  用户词库，之后完全离线可用（内置词库已含 3900+ 条常用与行业词）。" + NL +
            "" + NL +
            "【前提（缺一不可）】" + NL +
            "  1) 本机能访问外网（浏览器能打开对应平台网址）" + NL +
            "  2) 已在平台申请到密钥（个人免费额度一般够用）" + NL +
            "  这两条满足不了，就保持关闭——不影响其它功能。" + NL +
            "" + NL +
            "【开启三步】" + NL +
            "  1) 勾上「启用在线翻译」" + NL +
            "  2) 选平台，按下面的对照表填 AppID/AppKey 与 密钥/Key" + NL +
            "  3) 点「测试翻译」→ 显示  测试成功：缓存命中率 → cache hit rate" + NL +
            "     再点「保存」" + NL +
            "" + NL +
            "【各平台填什么】" + NL +
            "  百度翻译     AppID/AppKey = APP ID        密钥/Key = 密钥" + NL +
            "               申请：百度翻译开放平台 → 管理控制台 → 开发者信息 → 通用文本翻译" + NL +
            "  有道智云     AppID/AppKey = 应用ID        密钥/Key = 应用密钥" + NL +
            "               申请：有道智云控制台 → 创建应用（选「文本翻译」）" + NL +
            "  DeepL        AppID/AppKey 留空            密钥/Key = 认证密钥（形如 xxxxxx:fx）" + NL +
            "               申请：deepl.com/pro-api → API Free（每月一定免费字符量）" + NL +
            "  自定义URL     两个都留空，只填 URL 模板" + NL +
            "               模板示例：https://your-host/translate?text={q}&from=zh&to=en" + NL +
            "               返回内容含 translation 或 text 字段即可；直接返回纯文本也行" + NL +
            "" + NL +
            "【运行时的行为】" + NL +
            "  · 只翻译「内置词库没命中」的词；命中的词一律走本地、永不联网" + NL +
            "  · 逐词翻译（为了拼变量名），不是整句翻译" + NL +
            "  · 成功后自动刷新命名结果，状态栏提示「在线翻译并写入词库 N 个词」" + NL +
            "  · 同一次运行内已翻过的词不重复请求；失败的词本次不再重试" + NL +
            "  · 6 秒超时；失败不阻塞界面，状态栏会给出具体错误" + NL +
            "  · 走 Windows 系统代理（公司代理已配在系统里就会自动生效）" + NL +
            "" + NL +
            "【配置文件等价写法】" + NL +
            "  %APPDATA%\\VarNamer\\config.ini" + NL +
            "    translate=1        1=启用 0=关闭" + NL +
            "    trprovider=baidu   baidu | youdao | deepl | custom" + NL +
            "    trappid=你的APPID" + NL +
            "    trkey=你的密钥" + NL +
            "    trurl=             custom 时的 URL 模板" + NL +
            "    trautoadd=1        1=译文写入我的词库" + NL +
            "" + NL +
            "【常见报错对照】" + NL +
            "  无法连接到远程服务器 / 操作已超时" + NL +
            "      → 本机出不了外网，或目标域名被拦（检查网络与代理）" + NL +
            "  百度 54001 / 有道 202" + NL +
            "      → 签名错误，多半是密钥填错（把「应用名称」当密钥填了）" + NL +
            "  百度 54003 / 54004     → 调用频率限制 / 余额不足" + NL +
            "  有道 108               → 应用ID无效" + NL +
            "  DeepL 403              → 密钥无效，或免费额度已用尽" + NL +
            "  响应未解析出译文        → 平台返回格式非预期（多见于自定义URL）" + NL +
            "" + NL +
            "【推荐用法（内网 / 无外网环境）】" + NL +
            "  1) 平时保持关闭，用内置词库（开箱 3900+ 条）" + NL +
            "  2) 没收录的词，程序会自动记到 unknowns.txt" + NL +
            "     （本窗口「打开未收录词清单」即可查看）" + NL +
            "  3) 找一台有外网的机器，开着翻译用一天，它会沉淀出你们真实的用词：" + NL +
            "     %APPDATA%\\VarNamer\\userdict.txt" + NL +
            "     把这个文件拷到内网机器上覆盖同名文件 = 内网也用上了翻译成果" + NL +
            "" + NL +
            "【隐私说明】" + NL +
            "  启用后，未命中的中文词会被发送到所选平台。若词中含敏感信息，" + NL +
            "  请不要启用，改用本地词库手动补齐。";

        private const string NL = "\r\n";
    }
}