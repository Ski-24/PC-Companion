namespace PCCompanion;

// The set of prayer-time calculation methods the user can pick from. Single source of
// truth: both the settings dropdown (display names) and CalculatedPrayerProvider (angles)
// read from this list. Angles follow the widely-used Aladhan / PrayTimes conventions.
//
//   IshaMinAfterMaghrib != null  → Isha is a fixed interval after Maghrib (IshaAngle ignored)
static class PrayerCalcMethods
{
    public record Method(
        string Key,
        string Name,
        double FajrAngle,
        double IshaAngle,
        int?   IshaMinAfterMaghrib = null);

    // Order shown in the dropdown.
    public static readonly IReadOnlyList<Method> All = new Method[]
    {
        new("MWL",          "Muslim World League",        18.0, 17.0),
        new("ISNA",         "ISNA (North America)",       15.0, 15.0),
        new("Egyptian",     "Egyptian General Authority", 19.5, 17.5),
        new("UmmAlQura",    "Umm Al-Qura (Makkah)",       18.5,  0.0, 90),
        new("Karachi",      "Karachi",                    18.0, 18.0),
        new("Tehran",       "Tehran",                     17.7, 14.0),
        new("Gulf",         "Gulf Region",                19.5,  0.0, 90),
        new("Kuwait",       "Kuwait",                     18.0, 17.5),
        new("Qatar",        "Qatar",                      18.0,  0.0, 90),
        new("Singapore",    "Singapore",                  20.0, 18.0),
        new("France",       "France",                     12.0, 12.0),
        new("Turkey",       "Turkey",                     18.0, 17.0),
        new("Russia",       "Russia",                     16.0, 15.0),
        new("Moonsighting", "Moonsighting Committee",     18.0, 18.0),
        new("Dubai",        "Dubai",                      18.2, 18.2),
        new("Malaysia",     "Malaysia (JAKIM)",           20.0, 18.0),
        new("Tunisia",      "Tunisia",                    18.0, 18.0),
        new("Algeria",      "Algeria",                    18.0, 17.0),
        new("Indonesia",    "Indonesia",                  20.0, 18.0),
        new("Morocco",      "Morocco",                    19.0, 17.0),
    };

    public static Method Get(string key) =>
        All.FirstOrDefault(m => string.Equals(m.Key, key, StringComparison.OrdinalIgnoreCase)) ?? All[0];

    public static string NameFor(string key) => Get(key).Name;

    public static int IndexOf(string key)
    {
        for (int i = 0; i < All.Count; i++)
            if (string.Equals(All[i].Key, key, StringComparison.OrdinalIgnoreCase)) return i;
        return 0;
    }
}
