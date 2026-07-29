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
    // Optional 3rd audio output. Leave blank for a plain 2-device toggle; set it to make the
    // Audio card / switch cycle through three outputs (Device1 → Device2 → Device3 → …).
    public string Device3Id    { get; set; } = "";
    public string Device3Label { get; set; } = "";
    public string Theme        { get; set; } = "Ocean";
    public double SdrBrightness          { get; set; } = 3.0;
    public bool   BrightnessSliderEnabled { get; set; } = true;
    public bool   SdrSliderEnabled        { get; set; } = true;
    public bool   AutoHdrEnabled          { get; set; } = true;
    public PrayerConfig Prayer            { get; set; } = new();
    public CouchModeConfig Couch          { get; set; } = new();
    public MorningModeConfig Morning      { get; set; } = new();

    // Schema version for one-time settings migrations (see Migrate()). Files written before
    // this existed deserialize to 0, which triggers whatever migrations they haven't had.
    public int SettingsVersion            { get; set; }
    public List<CardConfig> Cards { get; set; } = new()
    {
        new CardConfig { Id = "Gopher",  Visible = true  },
        new CardConfig { Id = "Audio",   Visible = true  },
        new CardConfig { Id = "Display", Visible = true  },
        new CardConfig { Id = "Prayer",  Visible = true  },
    };

    private static readonly string _path = Path.Combine(AppPaths.Config, "settings.json");
    private static AppSettings? _cache;

    // Bump when adding a migration in Migrate(). Fresh installs are stamped with this so
    // they never re-run past migrations.
    private const int CurrentSettingsVersion = 1;

    public static AppSettings Current => _cache ??= Load();
    public static void Invalidate() => _cache = null;

    private static AppSettings Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path));
                if (s is not null)
                {
                    if (s.Migrate()) s.Save();   // persist any one-time migrations
                    return s;
                }
            }
        }
        catch (Exception ex) { Logger.Log($"Settings load: {ex.Message}"); }
        return new AppSettings { SettingsVersion = CurrentSettingsVersion };
    }

    // One-time, version-gated migrations for an existing settings file. Returns true if
    // anything changed so the caller can persist it. Keep each block idempotent.
    private bool Migrate()
    {
        bool changed = false;

        // v1: the iqama alarm sound shipped enabled-by-default (v1.0.13) and startled people
        // on update. Make it opt-in — silence it once for anyone upgrading. They can turn it
        // back on in Settings → Prayer.
        if (SettingsVersion < 1)
        {
            Prayer.PlayIqamaSound = false;
            changed = true;
        }

        if (SettingsVersion < CurrentSettingsVersion)
        {
            SettingsVersion = CurrentSettingsVersion;
            changed = true;
        }
        return changed;
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
