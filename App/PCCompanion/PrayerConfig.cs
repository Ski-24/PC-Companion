namespace PCCompanion;

class PrayerConfig
{
    public string Country    { get; set; } = "Qatar";
    public string City       { get; set; } = "Doha";
    public double Latitude   { get; set; } = 25.2854;
    public double Longitude  { get; set; } = 51.5310;
    public string Timezone   { get; set; } = "Arab Standard Time";
    public string CalcMethod { get; set; } = "Qatar";
    public bool   AsrHanafi  { get; set; } = false;
    public bool   Use24Hour  { get; set; } = false;   // false = 12-hour (6:15 PM), true = 24-hour (18:15)

    // Play a short alarm sound when each prayer's iqama time arrives. Opt-in (off by
    // default): the sound firing unattended startled people, so it's disabled unless the
    // user turns it on in Settings → Prayer. Existing users are migrated off once too
    // (see AppSettings.Migrate / SettingsVersion 1).
    public bool   PlayIqamaSound { get; set; } = false;

    // Iqama alarm volume, 0.0 (silent) .. 1.0 (full).
    public double IqamaVolume    { get; set; } = 0.8;

    // When true AND Country == "Qatar", fetch the official adhan times from the Qatar MOI
    // (Awqaf) endpoint instead of calculating them. Falls back to the offline calculation
    // when offline or for any non-Qatar country. Iqama is still adhan + the offsets below.
    public bool   UseOnlineTimes { get; set; } = true;

    // Iqama offset (minutes after adhan) per prayer. Defaults match Qatar's official
    // published offsets (prayers.qa); other countries can differ per mosque.
    public int FajrOffset    { get; set; } = 25;
    public int DhuhrOffset   { get; set; } = 20;
    public int AsrOffset     { get; set; } = 25;
    public int MaghribOffset { get; set; } = 10;
    public int IshaOffset    { get; set; } = 20;
}
