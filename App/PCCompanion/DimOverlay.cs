using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace PCCompanion;

/// <summary>
/// Software dimming: a single click-through, top-most, transparent black window that
/// spans the whole virtual desktop (all monitors). Compositing darkness over everything
/// lets brightness drop below the monitor's hardware backlight floor — the same trick
/// Desktop Dimmer / ScreenDimmer use. The window lives for the app's lifetime (one
/// shared instance) so the dim persists while the popup is closed; it is created lazily
/// the first time the level goes above 0 and merely hidden (not destroyed) at 0.
/// </summary>
static class DimOverlay
{
    // Opacity at slider 100%. Capped well below 1.0 so the screen is never fully black
    // (you can always still find the popup to raise brightness back up).
    private const double MaxOpacity = 0.85;

    private static Window? _win;
    private static int _level;   // 0–100

    /// <summary>Current dim level, 0–100. 0 = overlay hidden.</summary>
    public static int Level => _level;

    /// <summary>Sets the software dim 0–100. 0 hides the overlay.</summary>
    public static void SetLevel(int percent)
    {
        _level = Math.Clamp(percent, 0, 100);
        if (_level <= 0)
        {
            if (_win is { IsVisible: true }) _win.Hide();
            return;
        }

        EnsureWindow();
        PositionToVirtualScreen();
        _win!.Opacity = _level / 100.0 * MaxOpacity;
        if (!_win.IsVisible) _win.Show();   // ShowActivated=false → never steals focus
        _win.Topmost = true;
    }

    private static void EnsureWindow()
    {
        if (_win is not null) return;
        _win = new Window
        {
            WindowStyle        = WindowStyle.None,
            AllowsTransparency = true,
            Background         = System.Windows.Media.Brushes.Black,
            ShowInTaskbar      = false,
            ShowActivated      = false,
            Topmost            = true,
            ResizeMode         = ResizeMode.NoResize,
            IsHitTestVisible   = false,
            Focusable          = false,
            Title              = "PCCompanionDim",
            Opacity            = 0,
        };
        // Apply click-through / no-activate / off-Alt-Tab styles once the HWND exists.
        _win.SourceInitialized += (_, _) => ApplyOverlayStyles(_win!);
    }

    // Covers every monitor: VirtualScreen* is already in WPF logical units, so it maps
    // directly to Window.Left/Top/Width/Height for a system-DPI-aware app.
    private static void PositionToVirtualScreen()
    {
        _win!.Left   = SystemParameters.VirtualScreenLeft;
        _win.Top     = SystemParameters.VirtualScreenTop;
        _win.Width   = SystemParameters.VirtualScreenWidth;
        _win.Height  = SystemParameters.VirtualScreenHeight;
    }

    // ── Extended window styles ────────────────────────────────────────────────
    const int GWL_EXSTYLE        = -20;
    const int WS_EX_TRANSPARENT  = 0x00000020;   // click-through
    const int WS_EX_LAYERED      = 0x00080000;
    const int WS_EX_TOOLWINDOW   = 0x00000080;   // hide from Alt-Tab
    const int WS_EX_NOACTIVATE   = 0x08000000;   // never take focus

    [DllImport("user32.dll", SetLastError = true)]
    static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll", SetLastError = true)]
    static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

    private static void ApplyOverlayStyles(Window w)
    {
        var hwnd = new WindowInteropHelper(w).Handle;
        int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
        ex |= WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
        SetWindowLong(hwnd, GWL_EXSTYLE, ex);
    }
}
