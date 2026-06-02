namespace PCCompanion;

class PrayerDailyData
{
    public string            Date        { get; set; } = "";  // "yyyy-MM-dd" local to provider timezone
    public string            Source      { get; set; } = "";
    public string            FetchedAt   { get; set; } = "";
    public bool              IsEstimated { get; set; }
    public List<PrayerEntry> Prayers     { get; set; } = new();
}

class PrayerEntry
{
    public string Name   { get; set; } = "";
    public string Adhan  { get; set; } = "";  // "HH:mm" 24-hour
    public int    Offset { get; set; }         // iqama offset minutes after adhan
    public string Iqama  { get; set; } = "";  // "HH:mm" 24-hour, pre-computed
}
