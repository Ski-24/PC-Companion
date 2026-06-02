namespace PCCompanion;

static class AppPaths
{
    private static readonly string DataRoot =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                     "PCCompanion");

    public static string Config    => Path.Combine(DataRoot, "Config");
    public static string Logs      => Path.Combine(DataRoot, "Logs");
    public static string GopherDir => Path.Combine(DataRoot, "Gopher360");

    public static string GopherState => Path.Combine(Config, "gopher-state.txt");
    public static string AudioState  => Path.Combine(Config, "audio-state.txt");
    public static string HdrState    => Path.Combine(Config, "hdr-state.txt");
    public static string StatusJson  => Path.Combine(Config, "status.json");
    public static string GopherExe   => Path.Combine(GopherDir, "Gopher.exe");
    public static string GopherIni   => Path.Combine(GopherDir, "config.ini");


}
