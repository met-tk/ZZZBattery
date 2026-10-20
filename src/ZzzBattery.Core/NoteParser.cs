using System.Text.Json;

namespace ZzzBattery.Core;

// Field names and enums are verified against the official daily-note page and
// the WV_V2 Android widget model. Missing values must never become completed tasks.
public static class NoteParser
{
    public const string ScratchCardName = "饼铺盲盒/刮刮卡/占卜";
    public static IReadOnlyList<NoteLine> Parse(JsonElement data, DateTimeOffset receivedAt, string source = "实时便笺")
    {
        var notes = new Dictionary<string, NoteLine>(StringComparer.Ordinal);
        void Add(string id, string name, string value, NoteState state, string group = "日常",
            bool daily = false, int? current = null, int? target = null, DateTimeOffset? reset = null, string detail = "") =>
            notes[id] = new(name, value, state == NoteState.Pending, id, state, group, current, target,
                reset, detail, source, daily);
        void Enum(string id, string name, string raw, (string Raw, string Value, NoteState State)[] states,
            string group = "日常", bool daily = true)
        {
            if (string.IsNullOrWhiteSpace(raw)) return;
            var found = states.FirstOrDefault(s => s.Raw == raw);
            Add(id, name, found.Value ?? "状态未知", found.Value is null ? NoteState.Unknown : found.State,
                group, daily, detail: found.Value is null ? "服务端返回了尚未识别的状态，等待适配。" : "");
        }
        void Progress(JsonElement item, string id, string name, string curKey, string maxKey, string group,
            string resetKey = "refresh_time", bool daily = false)
        {
            if (item.ValueKind != JsonValueKind.Object || !item.EnumerateObject().Any()) return;
            var reset = Deadline(item, resetKey, receivedAt);
            if (Bool(item, "unlock") == false) { Add(id, name, "未解锁", NoteState.Locked, group); return; }
            var cur = JsonValue.Int(item, curKey);
            var max = JsonValue.Int(item, maxKey);
            if (cur is null or < 0 || max is null or <= 0)
                Add(id, name, "数据暂缺", NoteState.Unknown, group, daily, reset: reset);
            else Add(id, name, $"{cur} / {max}", cur >= max ? NoteState.Complete : NoteState.Pending,
                group, daily, cur, max, reset);
        }

        Progress(JsonValue.Object(data, "vitality"), "vitality", "今日活跃", "current", "max", "日常", daily: true);
        var cafe = JsonValue.String(data, "cafe_state");
        if (cafe.Length == 0) cafe = JsonValue.String(JsonValue.Object(data, "coffee"), "coffee_state");
        Enum("cafe", "咖啡", cafe,
            [("CafeStateDone", "已享用", NoteState.Complete), ("CafeStateNo", "未享用", NoteState.Pending),
             ("CoffeeStateDone", "已享用", NoteState.Complete), ("CoffeeStateNo", "未享用", NoteState.Pending),
             ("CoffeeStateWait", "未享用", NoteState.Pending)]);
        var card = JsonValue.String(data, "card_sign");
        if (card.Length == 0 && Bool(JsonValue.Object(data, "card_sign"), "has_signed") is { } signed)
            card = signed ? "CardSignDone" : "CardSignNo";
        Enum("card", ScratchCardName, card,
            [("CardSignDone", "已完成", NoteState.Complete), ("CardSignNo", "未完成", NoteState.Pending),
             ("CardSignNotDone", "未完成", NoteState.Pending)]);
        Enum("video", "录像店经营", JsonValue.String(JsonValue.Object(data, "vhs_sale"), "sale_state"),
            [("SaleStateDoing", "正在营业", NoteState.InProgress), ("SaleStateDone", "收益待领取", NoteState.Pending),
             ("SaleStateNo", "等待营业", NoteState.Pending)]);
        Progress(JsonValue.Object(data, "weekly_task"), "weekly", "丽都周纪积分", "cur_point", "max_point", "每周");
        var bounty = JsonValue.Object(data, "s2_bounty_commission");
        if (!bounty.EnumerateObject().Any()) bounty = JsonValue.Object(data, "bounty_commission");
        Progress(bounty, "bounty", "悬赏委托", "num", "total", "每周");
        Progress(JsonValue.Object(data, "survey_points"), "survey", "调查点数", "num", "total", "每周");

        var member = JsonValue.Object(data, "member_card");
        if (Bool(member, "is_open") is { } open)
        {
            var raw = JsonValue.String(member, "member_card_state");
            Add("member", "绳网会员每日奖励", !open ? "未开通" : raw == "MemberCardStateACK" ? "已领取" :
                raw == "MemberCardStateNo" ? "待领取" : "状态未知",
                !open ? NoteState.Locked : raw == "MemberCardStateACK" ? NoteState.Complete :
                raw == "MemberCardStateNo" ? NoteState.Pending : NoteState.Unknown, daily: true,
                reset: Deadline(member, "exp_time", receivedAt));
        }
        var temple = JsonValue.Object(data, "temple_running");
        Progress(temple, "temple_currency", "随便观 · 周常经营收益", "current_currency", "weekly_currency_max", "经营", "currency_next_refresh_ts");
        var auto = JsonValue.Object(temple, "auto_work");
        if (Bool(auto, "is_auto_work_running") == true)
        {
            var ended = Bool(auto, "auto_work_ended");
            Add("temple_auto", "随便观 · 自动经营", ended == true ? "收益待领取" : ended == false ? "托管中" : "状态未知",
                ended == true ? NoteState.Pending : ended == false ? NoteState.InProgress : NoteState.Unknown,
                "经营", reset: ended == false ? Deadline(auto, "left_ts", receivedAt) : null);
        }
        else
        {
            Enum("temple_expedition", "随便观 · 邦布游历", JsonValue.String(temple, "expedition_state"),
                [("ExpeditionStateInCanSend", "可派遣", NoteState.Pending), ("ExpeditionStateInProgress", "游历中", NoteState.InProgress),
                 ("ExpeditionStateEnd", "奖励待领取", NoteState.Pending)], "经营", false);
            Enum("temple_bench", "随便观 · 制作", JsonValue.String(temple, "bench_state"),
                [("BenchStateCanProduce", "可制作", NoteState.Pending), ("BenchStateProducing", "制作中", NoteState.InProgress)], "经营", false);
            Enum("temple_shelve", "随便观 · 售卖", JsonValue.String(temple, "shelve_state"),
                [("ShelveStateCanSell", "可上架", NoteState.Pending), ("ShelveStateSelling", "售卖中", NoteState.InProgress),
                 ("ShelveStateSoldOut", "等待补货", NoteState.Pending)], "经营", false);
        }

        // The server-provided widget list carries version-specific activities (including
        // surprise screenings). Merge it with structured fields instead of an either/or branch.
        if (data.TryGetProperty("note_list", out var list) && list.ValueKind == JsonValueKind.Array)
            foreach (var item in list.EnumerateArray().Take(40))
            {
                var name = Clean(JsonValue.String(item, "name"), 80);
                var value = Clean(JsonValue.String(item, "value"), 160);
                if (name.Length == 0 || value.Length == 0) continue;
                var id = KnownId(name) ?? "server:" + name;
                if (notes.ContainsKey(id)) continue;
                // Text is shown verbatim, but does not grant a completed checkmark.
                Add(id, name, value, NoteState.Unknown, id.StartsWith("server:") ? "活动" : id is "weekly" or "bounty" or "survey" ? "每周" : "日常",
                    daily: id is "cafe" or "card" or "video" or "vitality",
                    detail: "米游社小组件文案；接口未提供独立完成标记。");
            }
        if (data.TryGetProperty("activity_list", out var activities) && activities.ValueKind == JsonValueKind.Array)
            foreach (var item in activities.EnumerateArray().Take(40))
            {
                var name = Clean(JsonValue.String(item, "name"), 80);
                if (name.Length == 0) continue;
                var raw = JsonValue.String(item, "state");
                var state = raw switch
                {
                    "STATE_COMPLETED" => NoteState.Complete, "STATE_IN_PROGRESS" => NoteState.Pending,
                    "STATE_NOT_START" => NoteState.Locked, _ => NoteState.Unknown
                };
                var value = raw switch
                {
                    "STATE_COMPLETED" => "已完成", "STATE_IN_PROGRESS" => "进行中", "STATE_NOT_START" => "尚未开始", _ => "状态未知"
                };
                var got = JsonValue.Int(item, "monochrome_got_cnt");
                var total = JsonValue.Int(item, "monochrome_cnt");
                var id = "activity:" + (JsonValue.String(item, "activity_id") is { Length: > 0 } aid ? aid : name);
                Add(id, name, value, state, "活动", current: got is >= 0 ? got : null, target: total is > 0 ? total : null,
                    reset: Deadline(item, raw == "STATE_NOT_START" ? "left_start_ts" : "left_end_ts", receivedAt),
                    detail: got is >= 0 && total is > 0 ? $"菲林奖励 {got} / {total} · {value}" : value);
                // An activity entry is richer than the duplicate display-only row.
                notes.Remove("server:" + name);
            }
        return notes.Values.ToArray();
    }

    public static string? KnownId(string name) => name switch
    {
        "咖啡" or "喝咖啡" => "cafe", "录像店经营" or "录像店营收" => "video", "今日活跃" or "每日活跃" => "vitality",
        "占卜 / 刮刮卡" or "刮刮卡" or "饼铺盲盒/刮刮卡" or "饼铺盲盒 / 刮刮卡" or
        "饼铺盲盒/刮刮卡/占卜" or "饼铺盲盒 / 刮刮卡 / 占卜" => "card",
        "丽都周纪积分" or "每周任务" => "weekly", "悬赏委托" => "bounty", "调查点数" or "零号空洞调查点数" => "survey", _ => null
    };
    private static string Clean(string text, int limit) => new(text.Where(c => !char.IsControl(c)).Take(limit).ToArray());
    public static bool? Bool(JsonElement parent, string key) => parent.ValueKind == JsonValueKind.Object &&
        parent.TryGetProperty(key, out var value) ? value.ValueKind switch
        { JsonValueKind.True => true, JsonValueKind.False => false, _ => null } : null;
    private static DateTimeOffset? Deadline(JsonElement data, string key, DateTimeOffset receivedAt)
    {
        var seconds = JsonValue.Int(data, key);
        return seconds is > 0 and <= 31536000 ? receivedAt.AddSeconds(seconds.Value) : null;
    }
}

public static class NoteBoard
{
    // The CN server resets at 04:00 UTC+8, independent of the Windows time zone.
    public static DateTimeOffset NextDailyReset(DateTimeOffset now)
    {
        var local = now.ToOffset(TimeSpan.FromHours(8));
        var reset = new DateTimeOffset(local.Year, local.Month, local.Day, 4, 0, 0, local.Offset);
        return reset > local ? reset : reset.AddDays(1);
    }
    public static IReadOnlyList<NoteLine> Project(EnergySnapshot snapshot, DateTimeOffset now, bool uncertain = false)
    {
        var aged = uncertain || now - snapshot.ReceivedAt > TimeSpan.FromMinutes(30) || now < snapshot.ReceivedAt.AddSeconds(-5);
        return snapshot.Notes.Select(n =>
        {
            if (snapshot.SchemaVersion == 0) n = n with { Id = NoteParser.KnownId(n.Name) ?? "legacy:" + n.Name };
            if (n.Id == "card") n = n with { Name = NoteParser.ScratchCardName };
            var stale = snapshot.SchemaVersion == 0 || aged || (n.Daily && now >= NextDailyReset(snapshot.ReceivedAt)) || (n.ResetAt.HasValue && now >= n.ResetAt);
            if (!stale) return n;
            return n with { Value = "待同步", State = NoteState.Stale, Pending = false,
                Detail = $"上次：{n.Value}（{snapshot.ReceivedAt.ToLocalTime():MM-dd HH:mm}）\n请刷新确认当前状态。" };
        }).ToArray();
    }
    public static IReadOnlyList<NoteLine> WithMissing(IReadOnlyList<NoteLine> notes)
    {
        var result = notes.ToList();
        foreach (var (id, name, group) in new[]
        {
            ("cafe", "咖啡", "日常"), ("card", NoteParser.ScratchCardName, "日常"), ("video", "录像店经营", "日常"),
            ("weekly", "丽都周纪积分", "每周"), ("bounty", "悬赏委托", "每周")
        })
            if (!result.Any(n => n.Id == id)) result.Add(new(name, "接口未提供", Id: id, Group: group,
                Detail: "此项未由本次接口返回，不能据此认定已完成或未完成。"));
        return result;
    }
    public static string Hint(NoteLine note, DateTimeOffset now)
    {
        if (note.State == NoteState.Stale) return note.Detail;
        var detail = note.Detail;
        if (note.ResetAt is { } reset)
        {
            var left = reset - now;
            var duration = left.TotalDays >= 1 ? $"{(int)left.TotalDays}天 {left.Hours}小时" :
                left.TotalHours >= 1 ? $"{(int)left.TotalHours}小时 {left.Minutes}分" : $"{Math.Max(1, (int)Math.Ceiling(left.TotalMinutes))}分钟";
            detail += (detail.Length > 0 ? " · " : "") + (left <= TimeSpan.Zero ? "已到刷新时间" : $"{duration}后刷新 / 结束");
        }
        return detail;
    }
}
