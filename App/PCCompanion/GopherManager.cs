using System.Diagnostics;
using System.Reflection;

namespace PCCompanion;

static class GopherManager
{
    private const string ProcessName = "Gopher";

    public static bool IsRunning =>
        Process.GetProcessesByName(ProcessName).Length > 0;

    public static void Toggle()
    {
        if (IsRunning)
            Kill();
        else
            Launch(EnsureExtracted());
    }

    // Drives Gopher360 to a specific state (used by Couch Mode apply/restore).
    // No-op if already in the requested state — avoids needless process churn.
    public static void EnsureRunning(bool on)
    {
        if (on == IsRunning) return;
        if (on) Launch(EnsureExtracted());
        else    Kill();
    }

    private static string EnsureExtracted()
    {
        Directory.CreateDirectory(AppPaths.GopherDir);
        ExtractOnce("Gopher.exe",        AppPaths.GopherExe);
        ExtractOnce("Gopher.config.ini", AppPaths.GopherIni);
        return AppPaths.GopherExe;
    }

    private static void ExtractOnce(string resourceName, string destPath)
    {
        if (File.Exists(destPath)) return;
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");
        using var dest = File.Create(destPath);
        stream.CopyTo(dest);
        Logger.Log($"Extracted {resourceName} → {destPath}");
    }

    private static void Launch(string exePath)
    {
        Logger.Log($"GopherManager: launching {exePath}");
        Process.Start(new ProcessStartInfo
        {
            FileName         = exePath,
            WorkingDirectory = Path.GetDirectoryName(exePath),
            WindowStyle      = ProcessWindowStyle.Hidden,
            UseShellExecute  = true,
        });
    }

    private static void Kill()
    {
        Logger.Log("GopherManager: stopping Gopher");
        foreach (var p in Process.GetProcessesByName(ProcessName))
        {
            try { p.Kill(); p.WaitForExit(3000); }
            catch (Exception ex) { Logger.Log($"GopherManager.Kill: {ex.Message}"); }
        }
    }
}
