using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Windows;

namespace PCCompanion;

// Checks GitHub Releases for a newer version, downloads the installer asset, and runs it
// silently. The Inno Setup installer is per-user (no UAC), kills the running app, updates
// in place, and relaunches it — so "update now" is a single click with no admin prompt.
//
// Backend = the project's public GitHub releases:
//   https://api.github.com/repos/Ski-24/PC-Companion/releases/latest
static class UpdateService
{
    private const string Owner = "Ski-24";
    private const string Repo  = "PC-Companion";
    private const string LatestReleaseApi =
        "https://api.github.com/repos/" + Owner + "/" + Repo + "/releases/latest";

    // Human-facing releases page for a given tag (used by the in-app "What's new" popup).
    public static string ReleasePageUrl(string tag) =>
        $"https://github.com/{Owner}/{Repo}/releases/tag/{tag}";

    public enum Status { UpToDate, UpdateAvailable, Failed }

    public sealed record UpdateInfo(Version Version, string Tag, string DownloadUrl, long Size, string AssetName, string Notes);

    public sealed record CheckResult(Status Status, UpdateInfo? Info, string? Error);

    // GitHub requires a User-Agent. One shared client, configured once.
    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(100) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("PCCompanion-Updater");
        c.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return c;
    }

    // The running app's version, normalized to Major.Minor.Build (the GitHub tags use
    // three parts, e.g. v1.0.1). Falls back to 0.0.0 if somehow unreadable.
    public static Version CurrentVersion
    {
        get
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version;
            return v is null ? new Version(0, 0, 0) : Normalize(v);
        }
    }

    private static Version Normalize(Version v) =>
        new(v.Major, v.Minor, v.Build < 0 ? 0 : v.Build);

    // Queries the latest release and decides whether it's newer than what's running.
    public static async Task<CheckResult> CheckAsync()
    {
        try
        {
            using var resp = await Http.GetAsync(LatestReleaseApi);
            if (!resp.IsSuccessStatusCode)
                return new CheckResult(Status.Failed, null, $"GitHub returned {(int)resp.StatusCode}");

            await using var stream = await resp.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);
            var root = doc.RootElement;

            string tag = root.TryGetProperty("tag_name", out var t) ? (t.GetString() ?? "") : "";
            if (!TryParseTag(tag, out var remote))
                return new CheckResult(Status.Failed, null, $"Could not parse release tag '{tag}'");

            string notes = root.TryGetProperty("body", out var b) ? (b.GetString() ?? "") : "";

            // Find the .exe installer asset.
            string url = "", assetName = "";
            long size = 0;
            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var a in assets.EnumerateArray())
                {
                    string name = a.TryGetProperty("name", out var n) ? (n.GetString() ?? "") : "";
                    if (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;
                    url       = a.TryGetProperty("browser_download_url", out var u) ? (u.GetString() ?? "") : "";
                    size      = a.TryGetProperty("size", out var s) ? s.GetInt64() : 0;
                    assetName = name;
                    break;
                }
            }

            if (remote <= CurrentVersion)
                return new CheckResult(Status.UpToDate, null, null);

            if (string.IsNullOrEmpty(url))
                return new CheckResult(Status.Failed, null, "Latest release has no installer (.exe) asset");

            Logger.Log($"UpdateService: update available {CurrentVersion} -> {remote} ({assetName}, {size} bytes)");
            return new CheckResult(Status.UpdateAvailable, new UpdateInfo(remote, tag, url, size, assetName, notes), null);
        }
        catch (Exception ex)
        {
            Logger.Log($"UpdateService.CheckAsync: {ex.Message}");
            return new CheckResult(Status.Failed, null, ex.Message);
        }
    }

    // "v1.0.2" / "1.0.2" -> Version(1,0,2). Tolerates a leading 'v' and trailing junk.
    private static bool TryParseTag(string tag, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(tag)) return false;
        tag = tag.Trim().TrimStart('v', 'V');
        // Keep only the leading dotted-number portion.
        int i = 0;
        while (i < tag.Length && (char.IsDigit(tag[i]) || tag[i] == '.')) i++;
        tag = tag[..i];
        if (!Version.TryParse(tag, out var v)) return false;
        version = Normalize(v);
        return true;
    }

    // Downloads the installer to the temp folder, reporting 0..1 progress. Returns the path.
    public static async Task<string> DownloadAsync(UpdateInfo info, IProgress<double>? progress, CancellationToken ct = default)
    {
        string dest = Path.Combine(Path.GetTempPath(), info.AssetName);

        using var resp = await Http.GetAsync(info.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();

        long total = resp.Content.Headers.ContentLength ?? info.Size;
        await using var src = await resp.Content.ReadAsStreamAsync(ct);
        await using var dst = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None);

        var buffer = new byte[81920];
        long read = 0;
        int n;
        while ((n = await src.ReadAsync(buffer, ct)) > 0)
        {
            await dst.WriteAsync(buffer.AsMemory(0, n), ct);
            read += n;
            if (total > 0) progress?.Report((double)read / total);
        }

        Logger.Log($"UpdateService: downloaded update to {dest} ({read} bytes)");
        return dest;
    }

    // Runs the downloaded installer silently. The installer closes the running app, updates
    // the files in place, and relaunches it (--startup) — see the WizardSilent [Run] entry
    // in PCCompanion.iss. We exit right after launching so our EXE isn't locked.
    public static void LaunchInstallerAndExit(string installerPath)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName        = installerPath,
            Arguments       = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART",
            UseShellExecute = true,
        });
        Logger.Log("UpdateService: launched silent installer; exiting for update.");
        System.Windows.Application.Current.Shutdown();
    }

    // One-stop interactive flow shared by the tray balloon and the About window: confirm,
    // download (reporting status text), then launch the installer. `setStatus` is optional
    // UI feedback (button/tray text). Returns false if the user declined or it failed.
    public static async Task<bool> ConfirmDownloadInstallAsync(UpdateInfo info, Window? owner, Action<string>? setStatus)
    {
        string msg =
            $"An update is available.\n\n" +
            $"Installed:  v{CurrentVersion}\n" +
            $"Latest:      {info.Tag}\n\n" +
            "Download and install it now? PC Companion will close briefly and reopen automatically.";

        var ans = owner is null
            ? MessageBox.Show(msg, "PC Companion Update", MessageBoxButton.YesNo, MessageBoxImage.Information)
            : MessageBox.Show(owner, msg, "PC Companion Update", MessageBoxButton.YesNo, MessageBoxImage.Information);
        if (ans != MessageBoxResult.Yes) return false;

        return await DownloadInstallAsync(info, owner, setStatus);
    }

    // Download + install without the confirm prompt — used when the caller already got the
    // user's go-ahead (e.g. the in-app "What's new" popup's Update button). On success the
    // installer relaunches the app and this process exits; on failure it shows an error and
    // returns false. `setStatus` receives progress/status text for the calling UI.
    public static async Task<bool> DownloadInstallAsync(UpdateInfo info, Window? owner, Action<string>? setStatus)
    {
        try
        {
            setStatus?.Invoke("Downloading… 0%");
            var progress = new Progress<double>(p => setStatus?.Invoke($"Downloading… {p:P0}"));
            string path = await DownloadAsync(info, progress);
            setStatus?.Invoke("Installing…");
            LaunchInstallerAndExit(path);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Log($"UpdateService.DownloadInstall: {ex.Message}");
            setStatus?.Invoke("Update failed");
            string err = "Could not download or start the update:\n\n" + ex.Message +
                         "\n\nYou can download it manually from the GitHub releases page.";
            if (owner is null) MessageBox.Show(err, "PC Companion Update", MessageBoxButton.OK, MessageBoxImage.Error);
            else               MessageBox.Show(owner, err, "PC Companion Update", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }
}
