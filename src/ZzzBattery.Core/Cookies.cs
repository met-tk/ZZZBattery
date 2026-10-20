using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ZzzBattery.Core;

public static class Cookies
{
    public static readonly HashSet<string> Allowed = new(StringComparer.Ordinal)
    {
        "account_id", "account_id_v2", "account_mid_v2",
        "cookie_token", "cookie_token_v2", "ltuid", "ltuid_v2", "ltmid_v2", "ltoken", "ltoken_v2",
        "stuid", "stuid_v2", "stoken", "stoken_v2", "mid"
    };

    public static Dictionary<string, string> Parse(string input)
    {
        if (input.Length > 32768) throw new ArgumentException("凭据内容过长。");
        input = input.Trim();
        if (input.StartsWith('{'))
        {
            try
            {
                using var document = JsonDocument.Parse(input);
                if (document.RootElement.TryGetProperty("raw_cookie", out var raw) && raw.ValueKind == JsonValueKind.String)
                    input = raw.GetString() ?? "";
                else
                    input = string.Join("; ", document.RootElement.EnumerateObject()
                        .Where(p => Allowed.Contains(p.Name) && p.Value.ValueKind == JsonValueKind.String)
                        .Select(p => p.Name + "=" + p.Value.GetString()));
            }
            catch (JsonException) { throw new ArgumentException("无法解析凭据 JSON，请粘贴 Cookie 或原有 credentials.json 的内容。"); }
        }
        if (input.StartsWith("Cookie:", StringComparison.OrdinalIgnoreCase)) input = input[7..].Trim();
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var part in input.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var equals = part.IndexOf('=');
            if (equals < 1) continue;
            var name = part[..equals].Trim();
            var value = part[(equals + 1)..].Trim();
            if (!Allowed.Contains(name)) continue;
            if (value.Length > 8192 || value.Any(c => c < 33 || c > 126 || c == '"' || c == ','))
                throw new ArgumentException("Cookie 包含不允许的字符，请重新复制完整内容。");
            if (value.Length > 0) result[name] = value;
        }
        return result;
    }

    public static Credential Create(string input)
    {
        var values = Parse(input);
        var ids = new[] { "account_id_v2", "ltuid_v2", "account_id", "ltuid", "stuid", "stuid_v2" };
        var id = ids.Select(k => values.GetValueOrDefault(k)).FirstOrDefault(v => !string.IsNullOrEmpty(v));
        if (id is null || !values.Keys.Any(k => k.Contains("token", StringComparison.Ordinal)))
            throw new ArgumentException("未找到完整登录凭据，请在官方网页完成登录后再读取。");
        if (!id.All(char.IsAsciiDigit) || ids.Select(k => values.GetValueOrDefault(k)).Where(v => !string.IsNullOrEmpty(v)).Distinct().Count() > 1)
            throw new ArgumentException("凭据中混入了不同账号，请重新登录后读取单个账号。");
        if (values.ContainsKey("cookie_token_v2") && !values.ContainsKey("account_id_v2")) values["account_id_v2"] = id;
        if (values.ContainsKey("cookie_token") && !values.ContainsKey("account_id")) values["account_id"] = id;
        if (values.ContainsKey("stoken") || values.ContainsKey("stoken_v2"))
        {
            values["stuid"] = id;
            if (!values.ContainsKey("stoken")) values["stoken"] = values["stoken_v2"];
            if (!values.ContainsKey("mid"))
            {
                var mid = values.GetValueOrDefault("account_mid_v2") ?? values.GetValueOrDefault("ltmid_v2");
                if (!string.IsNullOrEmpty(mid)) values["mid"] = mid;
            }
        }
        return new Credential
        {
            Cookie = string.Join("; ", values.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + "=" + p.Value)),
            AccountKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id)))[..24]
        };
    }

    public static bool IsOfficialHost(string host) =>
        host.Equals("mihoyo.com", StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith(".mihoyo.com", StringComparison.OrdinalIgnoreCase) ||
        host.Equals("miyoushe.com", StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith(".miyoushe.com", StringComparison.OrdinalIgnoreCase);
}
