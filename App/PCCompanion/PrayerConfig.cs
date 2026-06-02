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

    // Iqama offset (minutes after adhan) per prayer. Defaults match Qatar's official
    // published offsets (prayers.qa); other countries can differ per mosque.
    public int FajrOffset    { get; set; } = 25;
    public int DhuhrOffset   { get; set; } = 20;
    public int AsrOffset     { get; set; } = 25;
    public int MaghribOffset { get; set; } = 10;
    public int IshaOffset    { get; set; } = 20;
}
