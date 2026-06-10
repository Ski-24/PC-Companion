using System.Windows.Forms;
using System.Windows;

namespace PCCompanion;

sealed class TrayManager : IDisposable
{
    private readonly NotifyIcon _tray;
    private readonly PopupWindow _popup;
    private bool _suppressShow;
    private DiagnosticsWindow? _diag;
    private UpdateService.UpdateInfo? _pendingUpdate;   // set when a check finds one
    private DateTimeOffset _lastUpdateCheck = DateTimeOffset.MinValue;

    public TrayManager(PopupWindow popup)
    {
        _popup = popup;
        _popup.IsVisibleChanged += OnPopupVisibilityChanged;

        _tray = new NotifyIcon
        {
            Text    = "PC Companion",
            Visible = true,
            Icon    = LoadIcon(),
        };
        _tray.MouseClick += OnTrayClick;
        _tray.BalloonTipClicked += OnUpdateBalloonClicked;

        var menu = new ContextMenuStrip();
        menu.Items.Add("Open PC Companion",   null, (_, _) => OpenPopup());
        menu.Items.Add("Diagnostics / About", null, (_, _) => OpenDiagnostics());
        menu.Items.Add("Check for Updates…",  null, (_, _) => CheckForUpdates(interactive: true));
        menu.Items.Add("Open Logs Folder",    null, (_, _) => OpenLogsFolder());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit",                null, (_, _) => System.Windows.Application.Current.Shutdown());
        _tray.ContextMenuStrip = menu;
    }

    // Background check at startup: silent if up-to-date or offline, a tray balloon if an
    // update is available (clicking it starts the one-click install).
    public void StartupUpdateCheck()
    {
        _lastUpdateCheck = DateTimeOffset.UtcNow;
        CheckForUpdates(interactive: false);
    }

    // interactive (menu) → always report the outcome and prompt immediately.
    // non-interactive (startup) → only surface a balloon when an update exists.
    private async void CheckForUpdates(bool interactive)
    {
        UpdateService.CheckResult result;
        try { result = await UpdateService.CheckAsync(); }
        catch (Exception ex) { Logger.Log($"CheckForUpdates: {ex.Message}"); return; }

        switch (result.Status)
        {
            case UpdateService.Status.UpdateAvailable:
                if (interactive)
                {
                    await UpdateService.ConfirmDownloadInstallAsync(result.Info!, null,
                        s => _tray.Text = "PC Companion — " + s);
                }
                else
                {
                    // New version vs. one we already flagged this session? Balloon only on a
                    // change (so re-checks on every popup-open don't keep re-ballooning), but
                    // always (re)show the persistent banner — ShowUpdateAvailable is idempotent.
                    bool isNew = _pendingUpdate is null || _pendingUpdate.Version != result.Info!.Version;
                    _pendingUpdate = result.Info;
                    _popup.ShowUpdateAvailable(result.Info!);
                    if (isNew)
                    {
                        _tray.BalloonTipTitle = "PC Companion update available";
                        _tray.BalloonTipText  = $"Version {result.Info!.Tag} is ready. Click to install.";
                        _tray.ShowBalloonTip(8000);
                    }
                }
                break;

            case UpdateService.Status.UpToDate:
                if (interactive)
                    System.Windows.MessageBox.Show(
                        $"You're on the latest version (v{UpdateService.CurrentVersion}).",
                        "PC Companion", System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                break;

            default:
                if (interactive)
                    System.Windows.MessageBox.Show(
                        "Couldn't check for updates.\n\n" + (result.Error ?? "Unknown error"),
                        "PC Companion", System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                break;
        }
    }

    private async void OnUpdateBalloonClicked(object? sender, EventArgs e)
    {
        if (_pendingUpdate is null) return;
        var info = _pendingUpdate;
        await UpdateService.ConfirmDownloadInstallAsync(info, null,
            s => _tray.Text = "PC Companion — " + s);
    }

    // Right-click menu actions (run on the UI thread — NotifyIcon events fire there).
    private void OpenPopup()
    {
        if (_popup.IsVisible) _popup.Activate();
        else                  _popup.ShowPopup();
    }

    private void OpenDiagnostics()
    {
        if (_diag is not null) { _diag.Activate(); return; }
        _diag = new DiagnosticsWindow();
        _diag.Closed += (_, _) => _diag = null;
        _diag.Show();
        _diag.Activate();
    }

    private static void OpenLogsFolder()
    {
        try
        {
            System.IO.Directory.CreateDirectory(AppPaths.Logs);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = AppPaths.Logs,
                UseShellExecute = true,
            });
        }
        catch (Exception ex) { Logger.Log($"OpenLogsFolder: {ex.Message}"); }
    }

    private void OnTrayClick(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        if (_suppressShow) return;

        if (_popup.IsVisible)
            _popup.HidePopup();
        else
            _popup.ShowPopup();
    }

    private void OnPopupVisibilityChanged(object? sender, DependencyPropertyChangedEventArgs e)
    {
        if (_popup.IsVisible)
        {
            // Re-check on open (throttled) so the banner appears even when a release happened
            // after the one-shot startup check. Skipped while an update is already pending.
            if (_pendingUpdate is null &&
                DateTimeOffset.UtcNow - _lastUpdateCheck > TimeSpan.FromMinutes(30))
            {
                _lastUpdateCheck = DateTimeOffset.UtcNow;
                CheckForUpdates(interactive: false);
            }
            return;
        }
        _suppressShow = true;
        Task.Delay(300).ContinueWith(_ => _suppressShow = false);
    }

    private static System.Drawing.Icon LoadIcon()
    {
        try
        {
            using var stream = System.Reflection.Assembly.GetExecutingAssembly()
                                     .GetManifestResourceStream("pccontrol.ico");
            if (stream is not null) return new System.Drawing.Icon(stream);
        }
        catch { }
        return SystemIcons.Application;
    }

    public void Dispose() => _tray.Dispose();
}
