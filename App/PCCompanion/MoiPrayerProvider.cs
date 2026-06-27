using System.Net.Http;
using System.Text.RegularExpressions;

namespace PCCompanion;

// Fetches the official Qatar prayer (adhan) times published by the Ministry of Interior /
// Awqaf. Despite the "rest" path the endpoint returns a small HTML table (the same data
// shown on prayers.qa), so we parse the six time cells in order:
//   Fajr, Sunrise, Dhuhr, Asr, Maghrib, Isha
// Times come without AM/PM but increase monotonically through the day, so we convert to
// 24-hour by adding 12h whenever a value would otherwise land before the previous one.
// Iqama is computed from the adhan plus the user's per-prayer offsets (same as the offline
// provider). Returns null on any failure (offline, layout change, stale date) so the caller
// can fall back to the calculated times.
class MoiPrayerProvider
{
    private const string Url = "https://portal.moi.gov.qa/MoiPortalRestServices/rest/prayertimings/today/en";

    private static readonly HttpClient _http = CreateClient();

    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("PCCompanion/1.0 (+https://github.com/Ski-24/PC-Companion)");
        return c;
    }

    private readonly PrayerConfig _cfg;
    public MoiPrayerProvider(PrayerConfig cfg) => _cfg = cfg;

    // Returns today's official Qatar times, or null if they can't be fetched/parsed or the
    // page's date doesn't match the requested day (the endpoint only ever serves "today").
    public async Task<PrayerDailyData?> TryGetTodayAsync(DateOnly today)
    {
        try
        {
            string html = await _http.GetStringAsync(Url).ConfigureAwait(false);

            // Date sanity check: the page prints dd/MM/yyyy. If it doesn't match the local
            // "today" we asked for, treat it as stale and fall back to the calculation.
            var dateMatch = Regex.Match(html, @"\b(\d{2})/(\d{2})/(\d{4})\b");
            if (dateMatch.Success)
            {
                int dd = int.Parse(dateMatch.Groups[1].Value);
                int mm = int.Parse(dateMatch.Groups[2].Value);
                int yy = int.Parse(dateMatch.Groups[3].Value);
                if (dd != today.Day || mm != today.Month || yy != today.Year)
                {
                    Logger.Log($"MoiPrayerProvider: page date {dd:D2}/{mm:D2}/{yy} != today {today} — using calc");
                    return null;
                }
            }

            // The data row lives in the table body; pull every <td> time cell from it.
            var body = Regex.Match(html, @"<tbody>(.*?)</tbody>", RegexOptions.Singleline);
            string scope = body.Success ? body.Groups[1].Value : html;
            var cells = Regex.Matches(scope, @"<td[^>]*>\s*(\d{1,2}:\d{2})\s*</td>", RegexOptions.Singleline);
            if (cells.Count < 6)
            {
                Logger.Log($"MoiPrayerProvider: expected 6 time cells, found {cells.Count} — using calc");
                return null;
            }

            // Cells in published order: Fajr, Sunrise, Dhuhr, Asr, Maghrib, Isha.
            var raw = new List<int>();   // minutes-of-day, AM/PM resolved monotonically
            int prev = -1;
            for (int i = 0; i < 6; i++)
            {
                var parts = cells[i].Groups[1].Value.Split(':');
                int h = int.Parse(parts[0]) % 12;          // 12 → 0; resolved below
                int m = int.Parse(parts[1]);
                int mins = h * 60 + m;
                while (mins <= prev) mins += 12 * 60;       // ensure strictly increasing through the day
                raw.Add(mins);
                prev = mins;
            }

            var names    = new[] { "Fajr", "Sunrise", "Dhuhr", "Asr", "Maghrib", "Isha" };
            var offsets  = new Dictionary<string, int>
            {
                ["Fajr"]    = _cfg.FajrOffset,
                ["Dhuhr"]   = _cfg.DhuhrOffset,
                ["Asr"]     = _cfg.AsrOffset,
                ["Maghrib"] = _cfg.MaghribOffset,
                ["Isha"]    = _cfg.IshaOffset,
            };

            var data = new PrayerDailyData
            {
                Date      = today.ToString("yyyy-MM-dd"),
                Source    = "MOI Qatar",
                FetchedAt = DateTimeOffset.UtcNow.ToString("O"),
            };

            for (int i = 0; i < 6; i++)
            {
                if (names[i] == "Sunrise") continue;
                if (!offsets.TryGetValue(names[i], out int off)) continue;
                int adhanMin = raw[i] % (24 * 60);
                int iqamaMin = (adhanMin + off) % (24 * 60);
                data.Prayers.Add(new PrayerEntry
                {
                    Name   = names[i],
                    Adhan  = $"{adhanMin / 60:D2}:{adhanMin % 60:D2}",
                    Offset = off,
                    Iqama  = $"{iqamaMin / 60:D2}:{iqamaMin % 60:D2}",
                });
            }

            Logger.Log($"MoiPrayerProvider: fetched official Qatar times for {today}");
            return data.Prayers.Count == 5 ? data : null;
        }
        catch (Exception ex)
        {
            Logger.Log($"MoiPrayerProvider.TryGetTodayAsync: {ex.Message}");
            return null;
        }
    }
}
