namespace PCCompanion;

static class StateManager
{
    public static string Read(string path, string defaultValue)
    {
        try
        {
            if (!File.Exists(path)) return defaultValue;
            var v = File.ReadAllText(path, System.Text.Encoding.UTF8).Trim();
            return string.IsNullOrEmpty(v) ? defaultValue : v;
        }
        catch { return defaultValue; }
    }

    public static void Write(string path, string value)
    {
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (dir is not null) Directory.CreateDirectory(dir);
            File.WriteAllText(path, value, System.Text.Encoding.UTF8);
        }
        catch (Exception ex) { Logger.Log($"StateManager.Write failed: {ex.Message}"); }
    }

    public static void EnsureDefault(string path, string defaultValue)
    {
        if (!File.Exists(path)) Write(path, defaultValue);
    }
}
