using System.Reflection;
using System.Text;

namespace PCCompanion;

/// <summary>
/// Gathers read-only diagnostics/about info. Every value is collected behind a try/catch
/// so a single failing probe shows "Unavailable" instead of crashing the window.
/// </summary>
static class DiagnosticsService
{
    public record Row(string Label, string? Value, bool IsHeader = false);

    public static List<Row> Collect()
    {
        var rows = new List<Row>();
        void Header(string s) => rows.Add(new Row(s, null, IsHeader: true));
        void Item(string label, Func<string> get)
        {
            string v;
            try { v = get(); if (string.IsNullOrWhiteSpace(v)) v = "Unavailable"; }
            catch { v = "Unavailable"; }
            rows.Add(new Row(label, v));
        }

        Header("App");
        Item("Name",       () => "PC Companion");
        Item("Version",    () => Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unavailable");
        Item("Build date", () => File.GetLastWriteTime(Environment.ProcessPath ?? AppContext.BaseDirectory).ToString("yyyy-MM-dd HH:mm"));
        Item("Build type", () => BuildType);
        Item("Theme",      () => AppSettings.Current.Theme);
        Item("App folder", () => AppContext.BaseDirectory);

        Header("Paths");
        Item("Config folder",   () => AppPaths.Config);
        Item("Logs folder",     () => AppPaths.Logs);
        Item("Startup at login",() => StartupShortcutExists() ? "Yes" : "No");

        Header("Display");
        var hdr = SafeHdr();
        Item("HDR supported",          () => hdr.Supported);
        Item("HDR enabled",            () => hdr.Enabled);
        Item("Auto HDR",               () => AutoHdrText());
        Item("SDR brightness control", () => HdrSliderService.IsSupported ? "Available" : "Not available");
        Item("Monitor brightness",     () => BrightnessText());

        Header("Audio");
        Item("Device 1",       () => DeviceLabel(AppSettings.Current.Device1Id, AppSettings.Current.Device1Label));
        Item("Device 2",       () => DeviceLabel(AppSettings.Current.Device2Id, AppSettings.Current.Device2Label));
        Item("Current default",() => CurrentAudio());

        Header("Gopher360");
        Item("Executable", () => File.Exists(AppPaths.GopherExe) ? "Extracted" : "Bundled (extracts on first use)");
        Item("Running",    () => GopherManager.IsRunning ? "Yes" : "No");

        Header("Couch Mode");
        var couch = AppSettings.Current.Couch;
        Item("Status",  () => couch.Active ? "ON" : "Off");
        Item("Actions", () =>
            string.Join(", ", new[]
            {
                couch.ToggleGopher  ? "Gopher" : null,
                couch.ToggleHdr     ? "HDR off" : null,
                couch.SetBrightness ? "Dim" : null,
                couch.SwitchAudio   ? "Audio" : null,
            }.Where(x => x is not null)) is { Length: > 0 } s ? s : "None enabled");
        Item("Target brightness", () => $"{couch.TargetBrightness}%");
        Item("Target audio",      () => string.IsNullOrEmpty(couch.TargetAudioLabel) ? "Not set" : couch.TargetAudioLabel);
        Item("Save state first",  () => couch.SaveStateBeforeApply ? "Yes" : "No");

        Header("Morning Mode");
        var morning = AppSettings.Current.Morning;
        Item("Status",  () => morning.Active ? "ON" : "Off");
        Item("Actions", () =>
            string.Join(", ", new[]
            {
                morning.ManageGopher  ? "Gopher off" : null,
                morning.ManageHdr     ? "HDR on" : null,
                morning.ManageAutoHdr ? "Auto HDR on" : null,
                morning.SwitchAudio   ? "Audio" : null,
            }.Where(x => x is not null)) is { Length: > 0 } s ? s : "None enabled");
        Item("Target audio",      () => string.IsNullOrEmpty(morning.TargetAudioLabel) ? "Not set" : morning.TargetAudioLabel);
        Item("Save state first",  () => morning.SaveStateBeforeApply ? "Yes" : "No");

        Header("Prayer");
        var p = AppSettings.Current.Prayer;
        Item("Mode",    () => $"Offline calculation ({p.CalcMethod})");
        Item("Country", () => p.Country);
        Item("City",    () => p.City);
        Item("Iqama offsets (min)", () =>
            $"Fajr +{p.FajrOffset}, Dhuhr +{p.DhuhrOffset}, Asr +{p.AsrOffset}, " +
            $"Maghrib +{p.MaghribOffset}, Isha +{p.IshaOffset}");

        return rows;
    }

    /// <summary>Plain-text dump for "Copy Diagnostics to Clipboard".</summary>
    public static string ToText(List<Row> rows)
    {
        var sb = new StringBuilder();
        foreach (var r in rows)
        {
            if (r.IsHeader) { sb.AppendLine(); sb.AppendLine($"== {r.Label} =="); }
            else            sb.AppendLine($"{r.Label}: {r.Value}");
        }
        return sb.ToString().Trim();
    }

    // ── Safe probes ──────────────────────────────────────────────────────────

    private static string BuildType =>
#if DEBUG
        "Debug";
#else
        "Release";
#endif

    private static bool StartupShortcutExists()
    {
        var lnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                               "PCCompanion.exe.lnk");
        return File.Exists(lnk);
    }

    private static (string Supported, string Enabled) SafeHdr()
    {
        try
        {
            var s = HdrDetector.GetAdvancedColorState();
            return (s.Supported ? "Yes" : "No", s.Enabled ? "Yes" : "No");
        }
        catch { return ("Unavailable", "Unavailable"); }
    }

    private static string AutoHdrText() => AutoHdrService.GetState() switch
    {
        AutoHdrService.State.On  => "On",
        AutoHdrService.State.Off => "Off",
        _                        => "Unavailable",
    };

    private static string BrightnessText()
    {
        int v = BrightnessService.Get();
        return v >= 0 ? $"{v}% (DDC/CI or WMI)" : "Not supported";
    }

    private static string DeviceLabel(string id, string label) =>
        string.IsNullOrEmpty(id) ? "Not configured" : label;

    private static string CurrentAudio()
    {
        var id = AudioManager.GetCurrentDefaultId();
        if (string.IsNullOrEmpty(id)) return "Unavailable";
        var name = AudioManager.GetRenderDevices().FirstOrDefault(d => d.Id == id).Name;
        return string.IsNullOrEmpty(name) ? "Unknown device" : name;
    }
}
