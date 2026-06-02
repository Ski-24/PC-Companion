using System.Windows.Threading;

namespace PCCompanion;

class PrayerTimesModule : IDisposable
{
    private readonly CalculatedPrayerProvider _calc;
    private readonly TimeZoneInfo             _tz;
    private readonly bool                     _use24;

    private DispatcherTimer? _ticker;
    private PrayerDailyData? _today;
    private PrayerDailyData? _tomorrow;

    // Fires on the UI thread: (statusLine, countdownLine)
    public event Action<string, string>? Updated;

    public PrayerTimesModule(PrayerConfig cfg)
    {
        _calc   = new CalculatedPrayerProvider(cfg);
        _use24  = cfg.Use24Hour;

        // Resolve location timezone so times are compared in the prayer location's
        // local time, not the machine's local time.
        try { _tz = TimeZoneInfo.FindSystemTimeZoneById(cfg.Timezone); }
        catch
        {
            TimeZoneInfo.TryConvertIanaIdToWindowsId(cfg.Timezone, out var winId);
            _tz = TimeZoneInfo.FindSystemTimeZoneById(winId ?? "UTC");
        }
    }

    public Task InitializeAsync()
    {
        try  { LoadCalculated(); }
        catch (Exception ex) { Logger.Log($"PrayerModule.Init: {ex.Message}"); }
        try  { StartTicker(); }
        catch (Exception ex) { Logger.Log($"PrayerModule.Init ticker: {ex.Message}"); }
        return Task.CompletedTask;
    }

    private void LoadCalculated()
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _tz));
        _today    = _calc.GetForDate(today);
        _tomorrow = _calc.GetForDate(today.AddDays(1));
    }

    private void StartTicker()
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null) return;

        _ticker = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
        {
            Interval = TimeSpan.FromSeconds(30),
        };
        _ticker.Tick += (_, _) =>
        {
            try
            {
                // Reload when the calendar date rolls over in the prayer location's timezone
                var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _tz));
                if (today > DateOnly.Parse(_today?.Date ?? "2000-01-01"))
                    LoadCalculated();

                FireUpdate();
            }
            catch (Exception ex) { Logger.Log($"PrayerModule.Tick: {ex.Message}"); }
        };
        _ticker.Start();
        FireUpdate();
    }

    private void FireUpdate()
    {
        var d = Application.Current?.Dispatcher;
        if (d is not null && !d.CheckAccess())
        {
            d.BeginInvoke((Action)FireUpdate);
            return;
        }
        try
        {
            var (status, countdown) = BuildDisplay();
            Updated?.Invoke(status, countdown);
        }
        catch (Exception ex) { Logger.Log($"PrayerModule.FireUpdate: {ex.Message}"); }
    }

    private (string status, string countdown) BuildDisplay()
    {
        if (_today?.Prayers is not { Count: > 0 })
            return ("Unavailable", "");

        var now = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _tz));

        PrayerEntry? next = _today.Prayers
            .Where(p => TimeOnly.TryParse(p.Iqama, out var t) && t > now)
            .FirstOrDefault();

        if (next is not null)
        {
            TimeOnly.TryParse(next.Iqama, out var iqamaTime);
            int mins = MinutesUntil(now, iqamaTime);
            return ($"{next.Name} Iqama  {Fmt(iqamaTime)}", FmtCountdown(mins));
        }

        // All today's prayers have passed — show tomorrow's Fajr
        var fajr = _tomorrow?.Prayers.FirstOrDefault(p => p.Name == "Fajr");
        if (fajr is not null && TimeOnly.TryParse(fajr.Iqama, out var fajrTime))
        {
            int mins = MinutesUntil(now, fajrTime, crossesMidnight: true);
            return ($"Fajr Iqama  {Fmt(fajrTime)}", FmtCountdown(mins));
        }

        return ("Isha passed — updating", "");
    }

    private static int MinutesUntil(TimeOnly from, TimeOnly to, bool crossesMidnight = false)
    {
        int mins = (int)(to.ToTimeSpan().TotalMinutes - from.ToTimeSpan().TotalMinutes);
        if (crossesMidnight && mins <= 0) mins += 1440;
        return Math.Max(0, mins);
    }

    private string Fmt(TimeOnly t)
    {
        if (_use24) return $"{t.Hour:D2}:{t.Minute:D2}";
        int h = t.Hour;
        string s = h >= 12 ? "PM" : "AM";
        if (h > 12) h -= 12;
        if (h == 0) h  = 12;
        return $"{h}:{t.Minute:D2} {s}";
    }

    private static string FmtCountdown(int mins)
    {
        if (mins <= 0) return "now";
        if (mins < 60) return $"in {mins} min";
        int h = mins / 60, m = mins % 60;
        return m == 0 ? $"in {h}h" : $"in {h}h {m}m";
    }

    public void Dispose()
    {
        _ticker?.Stop();
    }
}
