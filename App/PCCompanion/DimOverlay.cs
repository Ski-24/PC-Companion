using System.Runtime.InteropServices;

namespace PCCompanion;

/// <summary>
/// Software dimming implemented with the display gamma ramp.
///
/// A top-most transparent window cannot reliably dim Windows: other top-most windows
/// move above it, and hardware/MPO video surfaces may bypass it altogether. Applying
/// the dim at the display output stage keeps desktop, taskbar, browsers and video at
/// one stable brightness without depending on window z-order.
/// </summary>
static class DimOverlay
{
    private const double MaxDim = 0.85;
    private static readonly object Sync = new();
    private static readonly Dictionary<string, ushort[]> OriginalRamps =
        new(StringComparer.OrdinalIgnoreCase);

    private static int _level;

    /// <summary>Current dim level, 0–100. 0 = original display gamma.</summary>
    public static int Level => _level;

    /// <summary>Sets software dimming on every active monitor.</summary>
    public static void SetLevel(int percent)
    {
        lock (Sync)
        {
            _level = Math.Clamp(percent, 0, 100);
            ApplyToAllDisplays(_level);
        }
    }

    /// <summary>Restores every gamma table captured by this process.</summary>
    public static void Reset()
    {
        lock (Sync)
        {
            _level = 0;
            ApplyToAllDisplays(0);
            OriginalRamps.Clear();
        }
    }

    private static void ApplyToAllDisplays(int level)
    {
        var activeDevices = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (monitor, _, _, _) =>
        {
            var info = new MonitorInfoEx { cbSize = Marshal.SizeOf<MonitorInfoEx>() };
            if (!GetMonitorInfo(monitor, ref info) || string.IsNullOrWhiteSpace(info.szDevice))
                return true;

            activeDevices.Add(info.szDevice);
            ApplyToDisplay(info.szDevice, level);
            return true;
        }, IntPtr.Zero);

        foreach (string device in OriginalRamps.Keys.Where(d => !activeDevices.Contains(d)).ToArray())
            OriginalRamps.Remove(device);
    }

    private static void ApplyToDisplay(string device, int level)
    {
        IntPtr dc = CreateDC("DISPLAY", device, null, IntPtr.Zero);
        if (dc == IntPtr.Zero) return;

        try
        {
            if (!OriginalRamps.TryGetValue(device, out ushort[]? original))
            {
                original = new ushort[3 * 256];
                if (!GetDeviceGammaRamp(dc, original)) return;
                OriginalRamps[device] = original;
            }

            if (level <= 0)
            {
                SetDeviceGammaRamp(dc, original);
                return;
            }

            double multiplier = 1.0 - (level / 100.0 * MaxDim);
            var dimmed = new ushort[original.Length];
            for (int i = 0; i < original.Length; i++)
                dimmed[i] = (ushort)Math.Clamp(
                    (int)Math.Round(original[i] * multiplier), ushort.MinValue, ushort.MaxValue);

            SetDeviceGammaRamp(dc, dimmed);
        }
        finally
        {
            DeleteDC(dc);
        }
    }

    private delegate bool MonitorEnumProc(
        IntPtr monitor, IntPtr hdc, IntPtr monitorRect, IntPtr data);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfoEx
    {
        public int cbSize;
        public Rect rcMonitor;
        public Rect rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(
        IntPtr hdc, IntPtr clipRect, MonitorEnumProc callback, IntPtr data);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfoEx info);

    [DllImport("gdi32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr CreateDC(
        string driver, string device, string? output, IntPtr initData);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr dc);

    [DllImport("gdi32.dll")]
    private static extern bool GetDeviceGammaRamp(IntPtr dc, [Out] ushort[] ramp);

    [DllImport("gdi32.dll")]
    private static extern bool SetDeviceGammaRamp(IntPtr dc, ushort[] ramp);
}
