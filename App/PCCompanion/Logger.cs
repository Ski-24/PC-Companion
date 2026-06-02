namespace PCCompanion;

static class Logger
{
    private static readonly string _file =
        Path.Combine(AppPaths.Logs, $"app-{DateTime.Now:yyyy-MM-dd}.log");
    private static readonly object _lock = new();

    public static void Log(string message)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.Logs);
            lock (_lock)
                File.AppendAllText(_file, $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch { /* never crash on a logging failure */ }
    }
}
