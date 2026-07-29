using System.Runtime.InteropServices;

namespace PCCompanion;

static class HdrDetector
{
    // Reads the actual per-monitor HDR state — the same flag Win+Alt+B and
    // Settings → Display → Use HDR toggle.
    //
    // Prefers DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO_2 (Win11 24H2+).
    // The legacy ..._GET_ADVANCED_COLOR_INFO "advancedColorEnabled" bit does NOT
    // mean HDR: it means "advanced colour is active", which Automatic Color
    // Management (WCG) also sets. On a display with ACM on it is stuck at 1
    // whether HDR is on or off, so it is only a fallback for older Windows.
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
                var req2 = new DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO_2
                {
                    header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
                    {
                        type      = DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO_2,
                        size      = (uint)Marshal.SizeOf<DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO_2>(),
                        adapterId = path.targetInfo.adapterId,
                        id        = path.targetInfo.id,
                    }
                };
                int ret2 = DisplayConfigGetDeviceInfo(ref req2);
                if (ret2 == 0)
                {
                    bool hdrOn = (req2.value & HDR_USER_ENABLED) != 0
                              || req2.activeColorMode == ADVANCED_COLOR_MODE_HDR;
                    Logger.Log($"HdrDetector(v2): value=0b{Convert.ToString(req2.value,2).PadLeft(8,'0')} " +
                               $"hdrEn={((req2.value>>5)&1)} wcgEn={((req2.value>>7)&1)} mode={req2.activeColorMode} → {hdrOn}");
                    if (hdrOn) return true;
                    continue;   // this path answered authoritatively: HDR is off here
                }

                // Fallback: pre-24H2 Windows, where advancedColorEnabled == HDR.
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
                var req2 = new DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO_2
                {
                    header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
                    {
                        type      = DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO_2,
                        size      = (uint)Marshal.SizeOf<DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO_2>(),
                        adapterId = path.targetInfo.adapterId,
                        id        = path.targetInfo.id,
                    }
                };
                if (DisplayConfigGetDeviceInfo(ref req2) == 0)
                {
                    bool supported = (req2.value & HDR_SUPPORTED) != 0;
                    bool enabled   = (req2.value & HDR_USER_ENABLED) != 0
                                  || req2.activeColorMode == ADVANCED_COLOR_MODE_HDR;
                    if (supported || enabled) return (supported, enabled);
                    continue;
                }

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
    const int  DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO   = 9;
    const int  DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO_2 = 15;  // Win11 24H2+

    // DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO_2.value bits
    const uint HDR_SUPPORTED    = 1u << 4;   // highDynamicRangeSupported
    const uint HDR_USER_ENABLED = 1u << 5;   // highDynamicRangeUserEnabled
    // DISPLAYCONFIG_ADVANCED_COLOR_MODE: 0 = SDR, 1 = WCG, 2 = HDR
    const uint ADVANCED_COLOR_MODE_HDR = 2;

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

    [DllImport("user32.dll")]
    static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO_2 req);

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

    // value bits: 0=advancedColorSupported, 1=advancedColorActive, 2=reserved1,
    // 3=advancedColorLimitedByPolicy, 4=highDynamicRangeSupported,
    // 5=highDynamicRangeUserEnabled, 6=wideColorSupported, 7=wideColorUserEnabled
    [StructLayout(LayoutKind.Sequential)]
    struct DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO_2
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
        public uint value;
        public uint colorEncoding;
        public uint bitsPerColorChannel;
        public uint activeColorMode;
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
