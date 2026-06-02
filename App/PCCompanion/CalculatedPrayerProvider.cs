namespace PCCompanion;

// Standard prayer time calculation using Jean Meeus solar formulas.
// Adhan times are computed offline; iqama = adhan + user offset.
class CalculatedPrayerProvider
{
    private readonly PrayerConfig _cfg;

    public CalculatedPrayerProvider(PrayerConfig cfg) => _cfg = cfg;

    public PrayerDailyData GetForDate(DateOnly date)
    {
        TimeZoneInfo tz;
        try { tz = TimeZoneInfo.FindSystemTimeZoneById(_cfg.Timezone); }
        catch
        {
            // Accept legacy IANA ids stored in old settings files
            TimeZoneInfo.TryConvertIanaIdToWindowsId(_cfg.Timezone, out var winId);
            tz = TimeZoneInfo.FindSystemTimeZoneById(winId ?? "UTC");
        }

        double tzOffset = tz.GetUtcOffset(date.ToDateTime(TimeOnly.MinValue)).TotalHours;
        var times = Calculate(date, _cfg.Latitude, _cfg.Longitude, tzOffset,
                              _cfg.AsrHanafi, _cfg.CalcMethod);

        var offsets = new Dictionary<string, int>
        {
            ["Fajr"]    = _cfg.FajrOffset,
            ["Dhuhr"]   = _cfg.DhuhrOffset,
            ["Asr"]     = _cfg.AsrOffset,
            ["Maghrib"] = _cfg.MaghribOffset,
            ["Isha"]    = _cfg.IshaOffset,
        };

        var data = new PrayerDailyData { Date = date.ToString("yyyy-MM-dd"), Source = "calculated" };

        foreach (var (prayer, adhan) in times)
        {
            if (prayer == "Sunrise" || adhan is null) continue;
            if (!offsets.TryGetValue(prayer, out int off)) continue;
            var iqama = adhan.Value.AddMinutes(off);
            data.Prayers.Add(new PrayerEntry
            {
                Name   = prayer,
                Adhan  = $"{adhan.Value.Hour:D2}:{adhan.Value.Minute:D2}",
                Offset = off,
                Iqama  = $"{iqama.Hour:D2}:{iqama.Minute:D2}",
            });
        }

        return data;
    }

    // ── Calculation method parameters ─────────────────────────────────────────
    // Full list lives in PrayerCalcMethods (shared with the settings dropdown).

    private static (double FajrAngle, double IshaAngle, int? IshaMinAfterMaghrib)
        MethodParams(string method)
    {
        var m = PrayerCalcMethods.Get(method);
        return (m.FajrAngle, m.IshaAngle, m.IshaMinAfterMaghrib);
    }

    // ── Solar formulas ────────────────────────────────────────────────────────

    private static Dictionary<string, TimeOnly?> Calculate(
        DateOnly date, double lat, double lng, double tzOffset,
        bool asrHanafi, string calcMethod)
    {
        double jd      = JulianDate(date.Year, date.Month, date.Day);
        double dec     = Declination(jd);
        double et      = EquationOfTime(jd);
        double transit = 12.0 - lng / 15.0 + tzOffset - et;

        var (fajrAngle, ishaAngle, ishaMinAfterMaghrib) = MethodParams(calcMethod);

        double H0   = HourAngle(lat, dec, -0.8333);   // sunrise / sunset
        double fH   = HourAngle(lat, dec, -fajrAngle);
        double asrH = AsrHourAngle(lat, dec, asrHanafi ? 2 : 1);

        double maghribHours = transit + H0;

        TimeOnly? ishaTime = ishaMinAfterMaghrib.HasValue
            ? ToTime(maghribHours + ishaMinAfterMaghrib.Value / 60.0)
            : ToTime(transit + HourAngle(lat, dec, -ishaAngle));

        return new Dictionary<string, TimeOnly?>
        {
            ["Fajr"]    = ToTime(transit - fH),
            ["Sunrise"] = ToTime(transit - H0),
            ["Dhuhr"]   = ToTime(transit + 1.0 / 60),
            ["Asr"]     = ToTime(transit + asrH),
            ["Maghrib"] = ToTime(maghribHours),
            ["Isha"]    = ishaTime,
        };
    }

    private static double D2R(double d) => d * Math.PI / 180.0;

    private static double JulianDate(int y, int mo, int d)
    {
        if (mo <= 2) { y--; mo += 12; }
        int A = y / 100, B = 2 - A + A / 4;
        return (int)(365.25 * (y + 4716)) + (int)(30.6001 * (mo + 1)) + d + B - 1524.5;
    }

    private static double Declination(double jd)
    {
        double D = jd - 2451545.0;
        double g = (357.529 + 0.98560028 * D) % 360;
        double q = (280.459 + 0.98564736 * D) % 360;
        double L = (q + 1.915 * Math.Sin(D2R(g)) + 0.020 * Math.Sin(D2R(2 * g))) % 360;
        double e = 23.439 - 0.00000036 * D;
        return Math.Asin(Math.Sin(D2R(e)) * Math.Sin(D2R(L))) * 180 / Math.PI;
    }

    private static double EquationOfTime(double jd)
    {
        double D  = jd - 2451545.0;
        double g  = (357.529 + 0.98560028 * D) % 360;
        double q  = (280.459 + 0.98564736 * D) % 360;
        double L  = (q + 1.915 * Math.Sin(D2R(g)) + 0.020 * Math.Sin(D2R(2 * g))) % 360;
        double e  = 23.439 - 0.00000036 * D;
        double RA = Math.Atan2(Math.Cos(D2R(e)) * Math.Sin(D2R(L)), Math.Cos(D2R(L))) * 180 / Math.PI / 15.0;
        return q / 15.0 - ((RA % 24 + 24) % 24);
    }

    private static double HourAngle(double lat, double dec, double angle)
    {
        double cosH = (Math.Sin(D2R(angle)) - Math.Sin(D2R(lat)) * Math.Sin(D2R(dec)))
                    / (Math.Cos(D2R(lat)) * Math.Cos(D2R(dec)));
        if (cosH < -1 || cosH > 1) return double.NaN;
        return Math.Acos(cosH) * 180 / Math.PI / 15.0;
    }

    private static double AsrHourAngle(double lat, double dec, int shadowFactor)
    {
        double angle = -Math.Atan(shadowFactor + Math.Tan(D2R(Math.Abs(lat - dec)))) * 180 / Math.PI;
        return HourAngle(lat, dec, angle);
    }

    private static TimeOnly? ToTime(double hours)
    {
        if (double.IsNaN(hours)) return null;
        hours = ((hours % 24) + 24) % 24;
        int totalMin = (int)Math.Round(hours * 60);
        return new TimeOnly(totalMin / 60 % 24, totalMin % 60);
    }
}
