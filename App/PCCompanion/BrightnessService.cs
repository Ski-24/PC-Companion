using System.Management;
using System.Runtime.InteropServices;

namespace PCCompanion;

/// <summary>
/// Monitor brightness via DDC/CI (external monitors) or WMI (laptop/internal displays).
/// Tries high-level dxva2 API first, falls back to low-level VCP, then WMI.
/// </summary>
static class BrightnessService
{
    // ── P/Invoke ─────────────────────────────────────────────────────────────

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct PhysicalMonitor
    {
        public IntPtr hPhysicalMonitor;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szPhysicalMonitorDescription;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct Rect { public int Left, Top, Right, Bottom; }

    delegate bool MonEnumProc(IntPtr hMon, IntPtr hdc, ref Rect rc, IntPtr lp);

    [DllImport("user32.dll")]
    static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonEnumProc fn, IntPtr lp);

    [DllImport("dxva2.dll", SetLastError = true)]
    static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr hMon, out uint count);

    [DllImport("dxva2.dll", SetLastError = true)]
    static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr hMon, uint count, [Out] PhysicalMonitor[] arr);

    [DllImport("dxva2.dll", SetLastError = true)]
    static extern bool DestroyPhysicalMonitors(uint count, PhysicalMonitor[] arr);

    // High-level brightness API
    [DllImport("dxva2.dll", SetLastError = true)]
    static extern bool GetMonitorBrightness(IntPtr hMon, out uint min, out uint cur, out uint max);

    [DllImport("dxva2.dll", SetLastError = true)]
    static extern bool SetMonitorBrightness(IntPtr hMon, uint value);

    // Low-level VCP API (more compatible with some monitors)
    [DllImport("dxva2.dll", SetLastError = true)]
    static extern bool GetVCPFeatureAndVCPFeatureReply(IntPtr hMon, byte vcpCode,
        out int pvct, out uint current, out uint max);

    [DllImport("dxva2.dll", SetLastError = true)]
    static extern bool SetVCPFeature(IntPtr hMon, byte vcpCode, uint value);

    const byte VCP_BRIGHTNESS = 0x10;

    // ── Monitor enumeration ──────────────────────────────────────────────────

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

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>Returns 0–100, or -1 if not supported on this machine.</summary>
    public static int Get()
    {
        int v = GetViaDdc();
        if (v >= 0) return v;
        return GetViaWmi();
    }

    /// <summary>Sets brightness 0–100. Returns true if any method succeeded.</summary>
    public static bool Set(int percent)
    {
        return SetViaDdc(percent) || SetViaWmi(percent);
    }

    // ── DDC/CI ───────────────────────────────────────────────────────────────

    static int GetViaDdc()
    {
        try
        {
            var hmons = GetHMonitors();
            Logger.Log($"DDC Get: {hmons.Length} HMONITOR(s)");

            foreach (var hMon in hmons)
            {
                if (!GetNumberOfPhysicalMonitorsFromHMONITOR(hMon, out uint n))
                {
                    Logger.Log($"DDC Get: GetNumberOfPhysical failed, err={Marshal.GetLastWin32Error()}");
                    continue;
                }
                Logger.Log($"DDC Get: HMON={hMon:X8} physical count={n}");
                if (n == 0) continue;

                var arr = new PhysicalMonitor[n];
                if (!GetPhysicalMonitorsFromHMONITOR(hMon, n, arr))
                {
                    Logger.Log($"DDC Get: GetPhysical failed, err={Marshal.GetLastWin32Error()}");
                    continue;
                }

                try
                {
                    var hPhys = arr[0].hPhysicalMonitor;

                    // Try high-level API first
                    if (GetMonitorBrightness(hPhys, out _, out uint cur, out uint max) && max > 0)
                    {
                        int pct = (int)(cur * 100 / max);
                        Logger.Log($"DDC Get (high-level): cur={cur} max={max} → {pct}%");
                        return pct;
                    }
                    Logger.Log($"DDC Get: high-level failed err={Marshal.GetLastWin32Error()}, trying VCP");

                    // Try low-level VCP 0x10
                    if (GetVCPFeatureAndVCPFeatureReply(hPhys, VCP_BRIGHTNESS, out _, out uint vcpCur, out uint vcpMax) && vcpMax > 0)
                    {
                        int pct = (int)(vcpCur * 100 / vcpMax);
                        Logger.Log($"DDC Get (VCP 0x10): cur={vcpCur} max={vcpMax} → {pct}%");
                        return pct;
                    }
                    Logger.Log($"DDC Get: VCP failed err={Marshal.GetLastWin32Error()}");
                }
                finally { DestroyPhysicalMonitors(n, arr); }
            }
        }
        catch (Exception ex) { Logger.Log($"DDC Get exception: {ex.Message}"); }
        return -1;
    }

    static bool SetViaDdc(int percent)
    {
        try
        {
            foreach (var hMon in GetHMonitors())
            {
                if (!GetNumberOfPhysicalMonitorsFromHMONITOR(hMon, out uint n) || n == 0) continue;
                var arr = new PhysicalMonitor[n];
                if (!GetPhysicalMonitorsFromHMONITOR(hMon, n, arr)) continue;
                try
                {
                    var hPhys = arr[0].hPhysicalMonitor;
                    bool anyOk = false;

                    // High-level API
                    if (GetMonitorBrightness(hPhys, out uint min, out uint curBefore, out uint max) && max > 0)
                    {
                        uint v = (uint)(min + (long)percent * (max - min) / 100);
                        if (SetMonitorBrightness(hPhys, v))
                        {
                            // Read back after 60ms to verify the monitor actually applied it
                            Thread.Sleep(60);
                            GetMonitorBrightness(hPhys, out _, out uint curAfter, out _);
                            Logger.Log($"DDC Set (high-level): {percent}% → raw {v}, verify before={curBefore} after={curAfter}");
                            anyOk = true;
                        }
                        else
                        {
                            Logger.Log($"DDC Set: high-level failed err={Marshal.GetLastWin32Error()}");
                        }
                    }

                    // Low-level VCP 0x10 (brightness) — try regardless of high-level result;
                    // some monitors ignore the high-level wrapper but respond to direct VCP
                    if (GetVCPFeatureAndVCPFeatureReply(hPhys, VCP_BRIGHTNESS, out _, out _, out uint vcp10Max) && vcp10Max > 0)
                    {
                        uint v = (uint)((long)percent * vcp10Max / 100);
                        if (SetVCPFeature(hPhys, VCP_BRIGHTNESS, v))
                        {
                            Logger.Log($"DDC Set (VCP 0x10): {percent}% → raw {v}/{vcp10Max}");
                            anyOk = true;
                        }
                        else
                        {
                            Logger.Log($"DDC Set: VCP 0x10 failed err={Marshal.GetLastWin32Error()}");
                        }
                    }

                    // VCP 0x13 — backlight level on some monitors (separate from image brightness)
                    if (GetVCPFeatureAndVCPFeatureReply(hPhys, 0x13, out _, out _, out uint vcp13Max) && vcp13Max > 0)
                    {
                        uint v = (uint)((long)percent * vcp13Max / 100);
                        if (SetVCPFeature(hPhys, 0x13, v))
                        {
                            Logger.Log($"DDC Set (VCP 0x13 backlight): {percent}% → raw {v}/{vcp13Max}");
                            anyOk = true;
                        }
                        else
                        {
                            Logger.Log($"DDC Set: VCP 0x13 failed err={Marshal.GetLastWin32Error()}");
                        }
                    }

                    if (anyOk) return true;
                }
                finally { DestroyPhysicalMonitors(n, arr); }
            }
        }
        catch (Exception ex) { Logger.Log($"DDC Set exception: {ex.Message}"); }
        return false;
    }

    // ── WMI (laptop/internal displays) ──────────────────────────────────────

    static int GetViaWmi()
    {
        try
        {
            using var s = new ManagementObjectSearcher("root\\wmi",
                "SELECT CurrentBrightness FROM WmiMonitorBrightness WHERE Active=True");
            foreach (ManagementObject o in s.Get())
            {
                int v = Convert.ToInt32(o["CurrentBrightness"]);
                Logger.Log($"WMI Get: {v}%");
                return v;
            }
            Logger.Log("WMI Get: no active WmiMonitorBrightness found");
        }
        catch (Exception ex) { Logger.Log($"WMI Get exception: {ex.Message}"); }
        return -1;
    }

    static bool SetViaWmi(int percent)
    {
        try
        {
            using var s = new ManagementObjectSearcher("root\\wmi",
                "SELECT * FROM WmiMonitorBrightnessMethods WHERE Active=True");
            foreach (ManagementObject o in s.Get())
            {
                o.InvokeMethod("WmiSetBrightness", new object[] { 1u, (byte)percent });
                Logger.Log($"WMI Set: {percent}%");
                return true;
            }
        }
        catch (Exception ex) { Logger.Log($"WMI Set exception: {ex.Message}"); }
        return false;
    }
}
