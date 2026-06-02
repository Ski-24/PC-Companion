using System.Windows.Forms;
using System.Windows;

namespace PCCompanion;

sealed class TrayManager : IDisposable
{
    private readonly NotifyIcon _tray;
    private readonly PopupWindow _popup;
    private bool _suppressShow;
    private DiagnosticsWindow? _diag;

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

        var menu = new ContextMenuStrip();
        menu.Items.Add("Open PC Companion",   null, (_, _) => OpenPopup());
        menu.Items.Add("Diagnostics / About", null, (_, _) => OpenDiagnostics());
        menu.Items.Add("Open Logs Folder",    null, (_, _) => OpenLogsFolder());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit",                null, (_, _) => System.Windows.Application.Current.Shutdown());
        _tray.ContextMenuStrip = menu;
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
        if (_popup.IsVisible) return;
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
