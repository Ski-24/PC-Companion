using System.Runtime.InteropServices;

namespace PCCompanion;

static class HdrDetector
{
    // Uses QueryDisplayConfig + DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO
    // to read the actual per-monitor HDR (Advanced Color) enabled state —
    // the same flag Win+Alt+B and Settings → Display → Use HDR toggle.
    public static bool IsEnabled()
    {
        try
        {
            // Step 1: get required buffer sizes
            uint pathCount = 0, modeCount = 0;
            if (GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, ref pathCount, ref modeCount) != 0)
            {
                Logger.Log("HdrDetector: GetDisplayConfigBufferSizes failed");
                return false;
            }

            // Step 2: query with allocated buffers
            var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
            var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];
            if (QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, ref pathCount, paths,
                                   ref modeCount, modes, IntPtr.Zero) != 0)
            {
                Logger.Log("HdrDetector: QueryDisplayConfig failed");
                return false;
            }

            foreach (var path in paths)
            {
                var req = new DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO
                {
                    header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
                    {
                        type      = DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO,
                        size      = (uint)Marshal.SizeOf<DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO>(),
                        adapterId = path.targetInfo.adapterId,
                        id        = path.targetInfo.id,
                    }
                };
                int ret = DisplayConfigGetDeviceInfo(ref req);
                Logger.Log($"HdrDetector: ret={ret} value=0b{Convert.ToString(req.value,2).PadLeft(8,'0')} en={(req.value>>1)&1}");
                if (ret == 0 && (req.value & 2u) != 0)
                    return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            Logger.Log($"HdrDetector: {ex.Message}");
            return false;
        }
    }

    // Read-only: returns (supported, enabled) advanced-color (HDR) state of the first
    // active path that reports either. Used by diagnostics; never throws.
    public static (bool Supported, bool Enabled) GetAdvancedColorState()
    {
        try
        {
            uint pathCount = 0, modeCount = 0;
            if (GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, ref pathCount, ref modeCount) != 0)
                return (false, false);

            var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
            var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];
            if (QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, ref pathCount, paths,
                                   ref modeCount, modes, IntPtr.Zero) != 0)
                return (false, false);

            foreach (var path in paths)
            {
                var req = new DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO
                {
                    header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
                    {
                        type      = DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO,
                        size      = (uint)Marshal.SizeOf<DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO>(),
                        adapterId = path.targetInfo.adapterId,
                        id        = path.targetInfo.id,
                    }
                };
                if (DisplayConfigGetDeviceInfo(ref req) == 0)
                {
                    bool supported = (req.value & 1u) != 0;  // bit 0
                    bool enabled   = (req.value & 2u) != 0;  // bit 1
                    if (supported || enabled) return (supported, enabled);
                }
            }
            return (false, false);
        }
        catch { return (false, false); }
    }

    // ── P/Invoke ──────────────────────────────────────────────────────────────

    // ── P/Invoke ──────────────────────────────────────────────────────────────

    const uint QDC_ONLY_ACTIVE_PATHS = 2;
    const int  DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO = 9;

    [DllImport("user32.dll")]
    static extern int GetDisplayConfigBufferSizes(uint flags, ref uint numPathArrayElements, ref uint numModeInfoArrayElements);

    [DllImport("user32.dll")]
    static extern int QueryDisplayConfig(
        uint flags,
        ref uint numPathArrayElements,   [Out] DISPLAYCONFIG_PATH_INFO[] pathArray,
        ref uint numModeInfoArrayElements, [Out] DISPLAYCONFIG_MODE_INFO[] modeInfoArray,
        IntPtr currentTopologyId);

    [DllImport("user32.dll")]
    static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO req);

    [StructLayout(LayoutKind.Sequential)]
    struct LUID { public uint LowPart; public int HighPart; }

    [StructLayout(LayoutKind.Sequential)]
    struct DISPLAYCONFIG_DEVICE_INFO_HEADER
    {
        public int  type;
        public uint size;
        public LUID adapterId;
        public uint id;
    }

    // value bits: 0=supported, 1=enabled, 2=wideColorEnforced, 3=forceDisabled
    [StructLayout(LayoutKind.Sequential)]
    struct DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
        public uint value;
        public uint colorEncoding;
        public uint bitsPerColorChannel;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct DISPLAYCONFIG_RATIONAL { public uint Numerator; public uint Denominator; }

    [StructLayout(LayoutKind.Sequential)]
    struct DISPLAYCONFIG_PATH_SOURCE_INFO
    {
        public LUID adapterId;
        public uint id;
        public uint modeInfoIdx;
        public uint statusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct DISPLAYCONFIG_PATH_TARGET_INFO
    {
        public LUID                  adapterId;
        public uint                  id;
        public uint                  modeInfoIdx;
        public uint                  outputTechnology;
        public uint                  rotation;
        public uint                  scaling;
        public DISPLAYCONFIG_RATIONAL refreshRate;
        public uint                  scanLineOrdering;
        public int                   targetAvailable; // BOOL = 4 bytes
        public uint                  statusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct DISPLAYCONFIG_PATH_INFO
    {
        public DISPLAYCONFIG_PATH_SOURCE_INFO sourceInfo;
        public DISPLAYCONFIG_PATH_TARGET_INFO targetInfo;
        public uint flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct DISPLAYCONFIG_MODE_INFO
    {
        public uint infoType;
        public uint id;
        public LUID adapterId;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 48)]
        public byte[] modeUnion; // largest union member (DISPLAYCONFIG_TARGET_MODE = 48 bytes)
    }
}
