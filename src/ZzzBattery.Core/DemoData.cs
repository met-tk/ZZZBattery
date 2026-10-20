using System.Text.Json;

namespace ZzzBattery.Core;

// Explicit preview/test data only. Never used as a fallback for real account requests.
public static class DemoData
{
    public static EnergySnapshot Create(DateTimeOffset now)
    {
        using var json = JsonDocument.Parse("""
        {
          "energy":{"progress":{"current":90,"max":240},"restore":54000},
          "vitality":{"current":400,"max":400},"cafe_state":"CafeStateDone","card_sign":"CardSignNo",
          "vhs_sale":{"sale_state":"SaleStateDoing"},
          "weekly_task":{"cur_point":1200,"max_point":2100,"refresh_time":345600,"unlock":true},
          "s2_bounty_commission":{"num":2,"total":4,"refresh_time":345600,"unlock":true},
          "survey_points":{"num":6000,"total":8000},
          "temple_running":{"current_currency":3200,"weekly_currency_max":6000,"currency_next_refresh_ts":345600,
            "bench_state":"BenchStateProducing","expedition_state":"ExpeditionStateEnd","shelve_state":"ShelveStateSoldOut"},
          "activity_list":[
            {"activity_id":1,"name":"惊喜放映企划","state":"STATE_IN_PROGRESS","monochrome_got_cnt":120,"monochrome_cnt":300,"left_end_ts":172800},
            {"activity_id":2,"name":"丽都周纪","state":"STATE_IN_PROGRESS","monochrome_got_cnt":100,"monochrome_cnt":200,"left_end_ts":345600}
          ]
        }
        """);
        return MiyousheClient.ParseNote(json.RootElement, "demo", "demo", now);
    }
}
