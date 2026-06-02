namespace PCCompanion;

static class PrayerCountryPresets
{
    public record LocationPreset(
        string Country,
        string Region,      // "" for small countries; "State / Province" for large ones
        string City,
        double Lat,
        double Lng,
        string WinTz,
        string CalcMethod = "MWL");

    // CityLabel shown in the second dropdown
    public static string CityLabel(LocationPreset p) =>
        string.IsNullOrEmpty(p.Region) ? p.City : $"{p.Region} / {p.City}";

    public static IReadOnlyList<string> Countries =>
        All.Select(p => p.Country).Distinct().ToList();

    public static IReadOnlyList<LocationPreset> ForCountry(string country) =>
        All.Where(p => p.Country == country).ToList();

    public static LocationPreset? Find(string country, string cityLabel) =>
        ForCountry(country).FirstOrDefault(p => CityLabel(p) == cityLabel);

    public static LocationPreset? FindByCity(string country, string city) =>
        All.FirstOrDefault(p => p.Country == country && p.City == city);

    public static void Apply(LocationPreset preset, PrayerConfig cfg)
    {
        cfg.Country    = preset.Country;
        cfg.City       = preset.City;
        cfg.Latitude   = preset.Lat;
        cfg.Longitude  = preset.Lng;
        cfg.Timezone   = preset.WinTz;
        cfg.CalcMethod = preset.CalcMethod;
    }

    // ── Preset table ──────────────────────────────────────────────────────────
    //
    // CalcMethod values:
    //   MWL       — Muslim World League (Fajr 18°, Isha 17°) — default for most regions
    //   ISNA      — North America (Fajr 15°, Isha 15°)
    //   Egyptian  — Egyptian (Fajr 19.5°, Isha 17.5°)
    //   Karachi   — Karachi / South Asia (Fajr 18°, Isha 18°)
    //   Gulf      — Gulf region (Fajr 19.5°, Isha = Maghrib + 90 min)
    //   UmmAlQura — Saudi Arabia (Fajr 18.5°, Isha = Maghrib + 90 min)

    public static readonly IReadOnlyList<LocationPreset> All = new LocationPreset[]
    {
        // ── Gulf / Arabian Peninsula ──────────────────────────────────────────
        new("Qatar",        "", "Doha",         25.2854,  51.5310, "Arab Standard Time",        "Qatar"),

        new("Saudi Arabia", "", "Riyadh",        24.6877,  46.7219, "Arab Standard Time",        "UmmAlQura"),
        new("Saudi Arabia", "", "Jeddah",        21.4858,  39.1925, "Arab Standard Time",        "UmmAlQura"),
        new("Saudi Arabia", "", "Mecca",         21.3891,  39.8579, "Arab Standard Time",        "UmmAlQura"),
        new("Saudi Arabia", "", "Medina",        24.5247,  39.5692, "Arab Standard Time",        "UmmAlQura"),

        new("UAE",          "", "Dubai",         25.2048,  55.2708, "Arabian Standard Time",     "Gulf"),
        new("UAE",          "", "Abu Dhabi",     24.4539,  54.3773, "Arabian Standard Time",     "Gulf"),
        new("UAE",          "", "Sharjah",       25.3462,  55.4211, "Arabian Standard Time",     "Gulf"),

        new("Kuwait",       "", "Kuwait City",   29.3759,  47.9774, "Arab Standard Time",        "Gulf"),
        new("Bahrain",      "", "Manama",        26.2235,  50.5876, "Arab Standard Time",        "Gulf"),
        new("Oman",         "", "Muscat",        23.5880,  58.3829, "Arabian Standard Time",     "Gulf"),

        // ── Levant / Middle East ──────────────────────────────────────────────
        new("Jordan",       "", "Amman",         31.9454,  35.9284, "Jordan Standard Time",      "Egyptian"),
        new("Iraq",         "", "Baghdad",       33.3152,  44.3661, "Arab Standard Time",        "MWL"),
        new("Lebanon",      "", "Beirut",        33.8938,  35.5018, "Middle East Standard Time", "MWL"),
        new("Turkey",       "", "Istanbul",      41.0082,  28.9784, "Turkey Standard Time",      "MWL"),
        new("Turkey",       "", "Ankara",        39.9334,  32.8597, "Turkey Standard Time",      "MWL"),

        // ── North Africa ──────────────────────────────────────────────────────
        new("Egypt",        "", "Cairo",         30.0444,  31.2357, "Egypt Standard Time",       "Egyptian"),
        new("Egypt",        "", "Alexandria",    31.2001,  29.9187, "Egypt Standard Time",       "Egyptian"),
        new("Morocco",      "", "Casablanca",    33.5731,  -7.5898, "Morocco Standard Time",     "MWL"),
        new("Morocco",      "", "Rabat",         34.0209,  -6.8416, "Morocco Standard Time",     "MWL"),
        new("Morocco",      "", "Marrakech",     31.6295,  -7.9811, "Morocco Standard Time",     "MWL"),
        new("Tunisia",      "", "Tunis",         36.8188,  10.1658, "W. Central Africa Standard Time", "Egyptian"),
        new("Algeria",      "", "Algiers",       36.7372,   3.0865, "W. Central Africa Standard Time", "Egyptian"),
        new("Libya",        "", "Tripoli",       32.8872,  13.1913, "Libya Standard Time",       "Egyptian"),

        // ── South Asia ───────────────────────────────────────────────────────
        new("Pakistan",     "", "Islamabad",     33.7294,  73.0931, "Pakistan Standard Time",    "Karachi"),
        new("Pakistan",     "", "Karachi",       24.8607,  67.0011, "Pakistan Standard Time",    "Karachi"),
        new("Pakistan",     "", "Lahore",        31.5204,  74.3587, "Pakistan Standard Time",    "Karachi"),
        new("Pakistan",     "", "Peshawar",      34.0151,  71.5249, "Pakistan Standard Time",    "Karachi"),
        new("Bangladesh",   "", "Dhaka",         23.8103,  90.4125, "Bangladesh Standard Time",  "Karachi"),
        new("India",        "", "New Delhi",     28.6139,  77.2090, "India Standard Time",       "Karachi"),
        new("India",        "", "Mumbai",        19.0760,  72.8777, "India Standard Time",       "Karachi"),
        new("India",        "", "Hyderabad",     17.3850,  78.4867, "India Standard Time",       "Karachi"),
        new("India",        "", "Bangalore",     12.9716,  77.5946, "India Standard Time",       "Karachi"),

        // ── Southeast Asia ────────────────────────────────────────────────────
        new("Malaysia",     "", "Kuala Lumpur",   3.1390, 101.6869, "Singapore Standard Time",   "MWL"),
        new("Indonesia",    "", "Jakarta",        -6.2088, 106.8456, "SE Asia Standard Time",    "MWL"),
        new("Indonesia",    "", "Surabaya",       -7.2575, 112.7521, "SE Asia Standard Time",    "MWL"),

        // ── Central Asia ──────────────────────────────────────────────────────
        new("Kazakhstan",   "", "Almaty",        43.2220,  76.8512, "Central Asia Standard Time","MWL"),
        new("Kazakhstan",   "", "Nur-Sultan",    51.1801,  71.4460, "Central Asia Standard Time","MWL"),
        new("Uzbekistan",   "", "Tashkent",      41.2995,  69.2401, "West Asia Standard Time",   "MWL"),

        // ── Europe ────────────────────────────────────────────────────────────
        new("UK",           "", "London",        51.5074,  -0.1278, "GMT Standard Time",         "MWL"),
        new("UK",           "", "Birmingham",    52.4862,  -1.8904, "GMT Standard Time",         "MWL"),
        new("UK",           "", "Manchester",    53.4808,  -2.2426, "GMT Standard Time",         "MWL"),
        new("UK",           "", "Bradford",      53.7960,  -1.7594, "GMT Standard Time",         "MWL"),
        new("UK",           "", "Leicester",     52.6369,  -1.1398, "GMT Standard Time",         "MWL"),

        new("France",       "", "Paris",         48.8566,   2.3522, "Romance Standard Time",     "MWL"),
        new("France",       "", "Lyon",          45.7640,   4.8357, "Romance Standard Time",     "MWL"),
        new("France",       "", "Marseille",     43.2965,   5.3698, "Romance Standard Time",     "MWL"),
        new("France",       "", "Lille",         50.6292,   3.0573, "Romance Standard Time",     "MWL"),

        new("Germany",      "", "Berlin",        52.5200,  13.4050, "W. Europe Standard Time",   "MWL"),
        new("Germany",      "", "Hamburg",       53.5511,   9.9937, "W. Europe Standard Time",   "MWL"),
        new("Germany",      "", "Munich",        48.1351,  11.5820, "W. Europe Standard Time",   "MWL"),
        new("Germany",      "", "Frankfurt",     50.1109,   8.6821, "W. Europe Standard Time",   "MWL"),
        new("Germany",      "", "Cologne",       50.9333,   6.9500, "W. Europe Standard Time",   "MWL"),

        new("Netherlands",  "", "Amsterdam",     52.3676,   4.9041, "W. Europe Standard Time",   "MWL"),
        new("Netherlands",  "", "Rotterdam",     51.9225,   4.4792, "W. Europe Standard Time",   "MWL"),
        new("Belgium",      "", "Brussels",      50.8503,   4.3517, "Romance Standard Time",     "MWL"),
        new("Sweden",       "", "Stockholm",     59.3293,  18.0686, "W. Europe Standard Time",   "MWL"),
        new("Sweden",       "", "Malmö",         55.6050,  13.0038, "W. Europe Standard Time",   "MWL"),
        new("Sweden",       "", "Gothenburg",    57.7089,  11.9746, "W. Europe Standard Time",   "MWL"),
        new("Norway",       "", "Oslo",          59.9139,  10.7522, "W. Europe Standard Time",   "MWL"),
        new("Denmark",      "", "Copenhagen",    55.6761,  12.5683, "Romance Standard Time",     "MWL"),
        new("Spain",        "", "Madrid",        40.4168,  -3.7038, "Romance Standard Time",     "MWL"),
        new("Spain",        "", "Barcelona",     41.3851,   2.1734, "Romance Standard Time",     "MWL"),
        new("Italy",        "", "Rome",          41.9028,  12.4964, "W. Europe Standard Time",   "MWL"),
        new("Italy",        "", "Milan",         45.4642,   9.1900, "W. Europe Standard Time",   "MWL"),
        new("Switzerland",  "", "Zurich",        47.3769,   8.5417, "W. Europe Standard Time",   "MWL"),
        new("Austria",      "", "Vienna",        48.2082,  16.3738, "W. Europe Standard Time",   "MWL"),
        new("Finland",      "", "Helsinki",      60.1699,  24.9384, "FLE Standard Time",         "MWL"),

        // ── Americas (Region / City for USA & Canada) ─────────────────────────
        new("USA", "California",   "Los Angeles",    34.0522, -118.2437, "Pacific Standard Time",          "ISNA"),
        new("USA", "California",   "San Francisco",  37.7749, -122.4194, "Pacific Standard Time",          "ISNA"),
        new("USA", "Washington",   "Seattle",        47.6062, -122.3321, "Pacific Standard Time",          "ISNA"),
        new("USA", "Arizona",      "Phoenix",        33.4484, -112.0740, "US Mountain Standard Time",      "ISNA"),
        new("USA", "Texas",        "Houston",        29.7604,  -95.3698, "Central Standard Time",          "ISNA"),
        new("USA", "Texas",        "Dallas",         32.7767,  -96.7970, "Central Standard Time",          "ISNA"),
        new("USA", "Illinois",     "Chicago",        41.8781,  -87.6298, "Central Standard Time",          "ISNA"),
        new("USA", "Minnesota",    "Minneapolis",    44.9778,  -93.2650, "Central Standard Time",          "ISNA"),
        new("USA", "New York",     "New York City",  40.7128,  -74.0060, "Eastern Standard Time",          "ISNA"),
        new("USA", "Pennsylvania", "Philadelphia",   39.9526,  -75.1652, "Eastern Standard Time",          "ISNA"),
        new("USA", "Virginia",     "Washington DC",  38.9072,  -77.0369, "Eastern Standard Time",          "ISNA"),
        new("USA", "Florida",      "Miami",          25.7617,  -80.1918, "Eastern Standard Time",          "ISNA"),
        new("USA", "Georgia",      "Atlanta",        33.7490,  -84.3880, "Eastern Standard Time",          "ISNA"),
        new("USA", "Michigan",     "Detroit",        42.3314,  -83.0458, "Eastern Standard Time",          "ISNA"),
        new("USA", "Massachusetts","Boston",         42.3601,  -71.0589, "Eastern Standard Time",          "ISNA"),

        new("Canada", "Ontario",          "Toronto",   43.6532,  -79.3832, "Eastern Standard Time",  "ISNA"),
        new("Canada", "Quebec",           "Montreal",  45.5017,  -73.5673, "Eastern Standard Time",  "ISNA"),
        new("Canada", "British Columbia", "Vancouver", 49.2827, -123.1207, "Pacific Standard Time",  "ISNA"),
        new("Canada", "Alberta",          "Calgary",   51.0447, -114.0719, "Mountain Standard Time", "ISNA"),
        new("Canada", "Alberta",          "Edmonton",  53.5461, -113.4938, "Mountain Standard Time", "ISNA"),

        new("Mexico",       "", "Mexico City",   19.4326,  -99.1332, "Central America Standard Time", "ISNA"),
        new("Mexico",       "", "Monterrey",     25.6714, -100.3094, "Central America Standard Time", "ISNA"),
        new("Mexico",       "", "Guadalajara",   20.6597, -103.3496, "Central America Standard Time", "ISNA"),

        // ── Oceania ───────────────────────────────────────────────────────────
        new("Australia", "New South Wales",  "Sydney",    -33.8688,  151.2093, "AUS Eastern Standard Time",  "MWL"),
        new("Australia", "Victoria",         "Melbourne", -37.8136,  144.9631, "AUS Eastern Standard Time",  "MWL"),
        new("Australia", "Queensland",       "Brisbane",  -27.4698,  153.0251, "E. Australia Standard Time", "MWL"),
        new("Australia", "Western Australia","Perth",     -31.9505,  115.8605, "W. Australia Standard Time", "MWL"),
        new("Australia", "South Australia",  "Adelaide",  -34.9285,  138.6007, "Cen. Australia Standard Time","MWL"),
        new("New Zealand",  "", "Auckland",  -36.8485,  174.7633, "New Zealand Standard Time",  "MWL"),
        new("New Zealand",  "", "Wellington",-41.2865,  174.7762, "New Zealand Standard Time",  "MWL"),

        // ── Russia ────────────────────────────────────────────────────────────
        new("Russia",       "", "Moscow",           55.7558,  37.6173, "Russian Standard Time",       "MWL"),
        new("Russia",       "", "Saint Petersburg", 59.9311,  30.3609, "Russian Standard Time",       "MWL"),
        new("Russia",       "", "Kazan",            55.7879,  49.1233, "Russian Standard Time",       "MWL"),
        new("Russia",       "", "Yekaterinburg",    56.8389,  60.6057, "Ekaterinburg Standard Time",  "MWL"),
        new("Russia",       "", "Novosibirsk",      54.9885,  82.9207, "N. Central Asia Standard Time","MWL"),

        // ── Sub-Saharan Africa ────────────────────────────────────────────────
        new("Nigeria",      "", "Lagos",           6.5244,   3.3792, "W. Central Africa Standard Time", "MWL"),
        new("Nigeria",      "", "Abuja",           9.0579,   7.4951, "W. Central Africa Standard Time", "MWL"),
        new("Nigeria",      "", "Kano",           12.0022,   8.5920, "W. Central Africa Standard Time", "MWL"),
        new("Senegal",      "", "Dakar",          14.7167, -17.4677, "Greenwich Standard Time",   "MWL"),
        new("South Africa", "", "Johannesburg",  -26.2041,  28.0473, "South Africa Standard Time", "MWL"),
        new("South Africa", "", "Cape Town",     -33.9249,  18.4241, "South Africa Standard Time", "MWL"),
        new("Kenya",        "", "Nairobi",        -1.2921,  36.8219, "E. Africa Standard Time",   "MWL"),
    };
}
