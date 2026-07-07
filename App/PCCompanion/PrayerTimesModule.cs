using System.Windows.Threading;

namespace PCCompanion;

class PrayerTimesModule : IDisposable
{
    private readonly CalculatedPrayerProvider _calc;
    private readonly MoiPrayerProvider?       _online;   // non-null only for Qatar + UseOnlineTimes
    private readonly TimeZoneInfo             _tz;
    private readonly bool                     _use24;
    private readonly bool                     _iqamaSound;
    private readonly float                    _iqamaVolume;

    private DispatcherTimer? _ticker;
    private PrayerDailyData? _today;
    private PrayerDailyData? _tomorrow;

    // Location-local time of the previous tick. An iqama that falls in (_lastCheck, now]
    // has just arrived → play the alarm. Starts null so nothing historical fires on launch.
    private DateTime? _lastCheck;

    // Fires on the UI thread: (statusLine, countdownLine)
    public event Action<string, string>? Updated;

    public PrayerTimesModule(PrayerConfig cfg)
    {
        _calc       = new CalculatedPrayerProvider(cfg);
        _use24       = cfg.Use24Hour;
        _iqamaSound  = cfg.PlayIqamaSound;
        _iqamaVolume = (float)Math.Clamp(cfg.IqamaVolume, 0.0, 1.0);

        // Official online times are Qatar-only; everyone else (and Qatar with online off)
        // uses the offline calculation.
        if (cfg.UseOnlineTimes &&
            string.Equals(cfg.Country, "Qatar", StringComparison.OrdinalIgnoreCase))
            _online = new MoiPrayerProvider(cfg);

        // Resolve location timezone so times are compared in the prayer location's
        // local time, not the machine's local time.
        try { _tz = TimeZoneInfo.FindSystemTimeZoneById(cfg.Timezone); }
        catch
        {
            TimeZoneInfo.TryConvertIanaIdToWindowsId(cfg.Timezone, out var winId);
            _tz = TimeZoneInfo.FindSystemTimeZoneById(winId ?? "UTC");
        }
    }

    public async Task InitializeAsync()
    {
        try  { await LoadAsync(); }
        catch (Exception ex) { Logger.Log($"PrayerModule.Init: {ex.Message}"); }
        try  { StartTicker(); }
        catch (Exception ex) { Logger.Log($"PrayerModule.Init ticker: {ex.Message}"); }
    }

    private async Task LoadAsync()
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _tz));

        // Prefer the official online times for today (Qatar); fall back to the calculation
        // when offline / unavailable. Tomorrow is always calculated — it's only needed for
        // the after-Isha "next is tomorrow's Fajr" line and the official endpoint is today-only.
        PrayerDailyData? data = null;
        if (_online is not null)
            data = await _online.TryGetTodayAsync(today);

        _today    = data ?? _calc.GetForDate(today);
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
        _ticker.Tick += async (_, _) =>
        {
            try
            {
                var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _tz));

                // Reload when the calendar date rolls over, OR keep retrying the online fetch
                // if an earlier attempt fell back to the calculation (e.g. started offline).
                bool dateRolled = today > DateOnly.Parse(_today?.Date ?? "2000-01-01");
                bool onlineRetry = _online is not null && _today?.Source != "MOI Qatar";
                if (dateRolled || onlineRetry)
                    await LoadAsync();

                CheckIqamaAlarm();
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

    // Fires the alarm once per iqama, when its time crosses the gap since the last tick.
    private void CheckIqamaAlarm()
    {
        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _tz);
        var prev = _lastCheck;
        _lastCheck = now;

        if (!_iqamaSound || prev is null) return;              // skip startup / disabled
        if (now.Date != prev.Value.Date)  return;              // date rolled — avoid spurious fire
        if (_today?.Prayers is not { Count: > 0 }) return;

        foreach (var p in _today.Prayers)
        {
            if (!TimeOnly.TryParse(p.Iqama, out var iqama)) continue;
            var iqamaAt = now.Date + iqama.ToTimeSpan();
            if (iqamaAt > prev.Value && iqamaAt <= now)
            {
                Logger.Log($"PrayerModule: {p.Name} iqama reached — sounding alarm.");
                IqamaSound.Play(_iqamaVolume);
                break;
            }
        }
    }

    private (string status, string countdown) BuildDisplay()
    {
        if (_today?.Prayers is not { Count: > 0 })
            return ("Unavailable", "");

        var now = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _tz));

        // Walk today's prayers in order. For the first one still ahead of "now":
        //   • before its adhan  → count down to the ADHAN
        //   • adhan has passed but iqama hasn't → count down to the IQAMA (same prayer)
        // Once a prayer's iqama has also passed, move to the next prayer.
        foreach (var p in _today.Prayers)
        {
            if (TimeOnly.TryParse(p.Adhan, out var adhan) && now < adhan)
            {
                int mins = MinutesUntil(now, adhan);
                return ($"{p.Name} Adhan  {Fmt(adhan)}", FmtCountdown(mins));
            }
            if (TimeOnly.TryParse(p.Iqama, out var iqama) && now < iqama)
            {
                int mins = MinutesUntil(now, iqama);
                return ($"{p.Name} Iqama  {Fmt(iqama)}", FmtCountdown(mins));
            }
        }

        // Everything today has passed — count down to tomorrow's Fajr adhan.
        var fajr = _tomorrow?.Prayers.FirstOrDefault(p => p.Name == "Fajr");
        if (fajr is not null && TimeOnly.TryParse(fajr.Adhan, out var fajrTime))
        {
            int mins = MinutesUntil(now, fajrTime, crossesMidnight: true);
            return ($"Fajr Adhan  {Fmt(fajrTime)}", FmtCountdown(mins));
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
