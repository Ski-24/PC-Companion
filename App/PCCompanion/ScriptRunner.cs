using System.Diagnostics;

namespace PCCompanion;

static class ScriptRunner
{
    public static async Task RunAsync(string scriptPath)
    {
        Logger.Log($"RunScript: {scriptPath}");
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName        = "powershell.exe",
                Arguments       = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                CreateNoWindow  = true,
                UseShellExecute = false,
            };
            using var proc = Process.Start(psi);
            if (proc is not null)
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await proc.WaitForExitAsync(cts.Token);
            }
        }
        catch (Exception ex) { Logger.Log($"RunScript error: {ex.Message}"); }
    }
}
