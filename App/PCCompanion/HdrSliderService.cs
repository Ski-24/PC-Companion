using System.Runtime.InteropServices;

namespace PCCompanion;

/// <summary>
/// Controls the "SDR content brightness" slider in HDR mode via undocumented dwmapi.dll ordinal 171.
/// Value range 1.0–6.0 matches the Windows Settings slider.
/// </summary>
static class HdrSliderService
{
    private delegate void SdrBoostFn(IntPtr hmonitor, double value);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    static extern IntPtr LoadLibrary(string path);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr GetProcAddress(IntPtr hModule, uint ordinal);

    private static SdrBoostFn? _fn;
    private static bool _tried;

    private static bool TryInit()
    {
        if (_tried) return _fn is not null;
        _tried = true;
        try
        {
            var lib = LoadLibrary("dwmapi.dll");
            if (lib == IntPtr.Zero) return false;
            var ptr = GetProcAddress(lib, 171);
            if (ptr == IntPtr.Zero) return false;
            _fn = Marshal.GetDelegateForFunctionPointer<SdrBoostFn>(ptr);
        }
        catch { }
        return _fn is not null;
    }

    public static bool IsSupported => TryInit();

    /// <summary>Sets SDR content brightness 1.0–6.0 for all monitors. Returns false if not supported.</summary>
    public static bool Set(double value)
    {
        if (!TryInit() || _fn is null) return false;
        try
        {
            foreach (var hMon in GetHMonitors())
                _fn(hMon, value);
            return true;
        }
        catch { return false; }
    }

    // ── Monitor enumeration ──────────────────────────────────────────────────

    [StructLayout(LayoutKind.Sequential)]
    struct Rect { public int Left, Top, Right, Bottom; }
    delegate bool MonEnumProc(IntPtr h, IntPtr hdc, ref Rect rc, IntPtr lp);
    [DllImport("user32.dll")] static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonEnumProc fn, IntPtr lp);

    [ThreadStatic] static List<IntPtr>? _hmons;
    static bool Collect(IntPtr h, IntPtr _, ref Rect __, IntPtr ___) { _hmons?.Add(h); return true; }

    static IntPtr[] GetHMonitors()
    {
        _hmons = new();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, Collect, IntPtr.Zero);
        var r = _hmons.ToArray();
        _hmons = null;
        return r;
    }
}
