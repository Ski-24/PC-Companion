using System.Text.Json;

namespace PCCompanion;

class CardConfig
{
    public string Id      { get; set; } = "";
    public bool   Visible { get; set; } = true;
}

// Snapshot of the display/audio/gopher state taken right before a scene (Couch /
// Morning) is applied, so toggling the scene off can restore exactly what was there.
class SceneSnapshot
{
    public bool   GopherWasRunning { get; set; }
    public bool   HdrWasOn         { get; set; }
    public bool   AutoHdrWasOn     { get; set; }
    public int    PrevBrightness   { get; set; } = -1;   // -1 = unknown / not captured
    public string PrevAudioId      { get; set; } = "";
}

class CouchModeConfig
{
    // Per-action enable switches
    public bool ToggleGopher         { get; set; } = true;   // turn Gopher360 ON
    public bool ToggleHdr            { get; set; } = true;   // turn HDR OFF
    public bool SetBrightness        { get; set; } = true;   // dim to TargetBrightness
    public bool SwitchAudio          { get; set; } = true;   // switch to TargetAudio*
    public bool SaveStateBeforeApply { get; set; } = true;   // snapshot before applying (needed for restore)

    public int    TargetBrightness   { get; set; } = 20;
    public string TargetAudioId      { get; set; } = "";
    public string TargetAudioLabel   { get; set; } = "";

    public bool Configured { get; set; }                    // user has set this mode up in Settings
    public bool          Active { get; set; }               // currently in Couch Mode
    public SceneSnapshot? Saved { get; set; }               // state to restore on exit
}

// Morning Mode — the wake-up counterpart to Couch Mode. Same toggle/restore model,
// but drives the opposite targets: Gopher OFF, HDR ON, Auto HDR ON, audio → target.
// Brightness is intentionally not managed (DDC brightness can't be written while HDR
// is on, which is exactly the state Morning leaves the display in).
class MorningModeConfig
{
    public bool ManageGopher         { get; set; } = true;   // turn Gopher360 OFF
    public bool ManageHdr            { get; set; } = true;   // turn HDR ON
    public bool ManageAutoHdr        { get; set; } = true;   // turn Auto HDR ON
    public bool SwitchAudio          { get; set; } = true;   // switch to TargetAudio*
    public bool SaveStateBeforeApply { get; set; } = true;   // snapshot before applying (needed for restore)

    public string TargetAudioId      { get; set; } = "";
    public string TargetAudioLabel   { get; set; } = "";

    public bool Configured { get; set; }                    // user has set this mode up in Settings
    public bool          Active { get; set; }               // currently in Morning Mode
    public SceneSnapshot? Saved { get; set; }               // state to restore on exit
}

class AppSettings
{
    public string Device1Id    { get; set; } = "";
    public string Device1Label { get; set; } = "Device 1";
    public string Device2Id    { get; set; } = "";
    public string Device2Label { get; set; } = "Device 2";
    public string Theme        { get; set; } = "Ocean";
    public double SdrBrightness          { get; set; } = 3.0;
    public bool   BrightnessSliderEnabled { get; set; } = true;
    public bool   SdrSliderEnabled        { get; set; } = true;
    public bool   AutoHdrEnabled          { get; set; } = true;
    public string BackgroundSoundFile     { get; set; } = "";   // user-supplied loop file ("" = unconfigured)
    public double BackgroundSoundVolume   { get; set; } = 0.5;  // 0.0–1.0
    public PrayerConfig Prayer            { get; set; } = new();
    public CouchModeConfig Couch          { get; set; } = new();
    public MorningModeConfig Morning      { get; set; } = new();
    public List<CardConfig> Cards { get; set; } = new()
    {
        new CardConfig { Id = "Gopher",  Visible = true  },
        new CardConfig { Id = "Audio",   Visible = true  },
        new CardConfig { Id = "Display", Visible = true  },
        new CardConfig { Id = "Prayer",  Visible = true  },
        new CardConfig { Id = "Background", Visible = true },
    };

    private static readonly string _path = Path.Combine(AppPaths.Config, "settings.json");
    private static AppSettings? _cache;

    public static AppSettings Current => _cache ??= Load();
    public static void Invalidate() => _cache = null;

    private static AppSettings Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path));
                if (s is not null) return s;
            }
        }
        catch (Exception ex) { Logger.Log($"Settings load: {ex.Message}"); }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.Config);
            File.WriteAllText(_path, JsonSerializer.Serialize(this,
                new JsonSerializerOptions { WriteIndented = true }));
            _cache = this;
            Logger.Log($"Settings saved: {Device1Label} / {Device2Label}");
        }
        catch (Exception ex) { Logger.Log($"Settings save: {ex.Message}"); }
    }
}
