using System.Runtime.InteropServices;

namespace PCCompanion;

/// <summary>
/// Software dimming implemented as a full-desktop compositor color effect.
///
/// A top-most transparent window is unreliable because other top-most surfaces and
/// hardware video can move above it. Display gamma ramps avoid z-order problems but
/// many Windows display drivers clamp them at about 50% brightness. The Magnification
/// API applies a color matrix to the composed desktop, giving the complete dim range
/// to browsers, video, the taskbar and every monitor without an overlay window.
/// </summary>
static class DimOverlay
{
    // Matches the original overlay: level 100 leaves 15% visible output.
    private const float MaxDim = 0.85f;
    private static readonly object Sync = new();

    private static int _level;
    private static bool _initialized;
    private static bool _failureLogged;
    private static MagColorEffect _originalEffect;

    /// <summary>Current dim level, 0–100. 0 restores the previous desktop effect.</summary>
    public static int Level => _level;

    /// <summary>Sets software dimming across the entire composed desktop.</summary>
    public static void SetLevel(int percent)
    {
        lock (Sync)
        {
            _level = Math.Clamp(percent, 0, 100);

            // Do not start the magnification runtime just to apply an identity effect.
            if (_level == 0 && !_initialized) return;
            if (!EnsureInitialized()) return;

            MagColorEffect effect = _level == 0
                ? CloneEffect(_originalEffect)
                : ScaleOutput(_originalEffect, 1.0f - (_level / 100.0f * MaxDim));

            if (!MagSetFullscreenColorEffect(ref effect) && !_failureLogged)
            {
                _failureLogged = true;
                Logger.Log($"Dim compositor effect failed (Win32 {Marshal.GetLastWin32Error()}).");
            }
        }
    }

    /// <summary>Restores the desktop color effect that existed before dimming.</summary>
    public static void Reset()
    {
        lock (Sync)
        {
            _level = 0;
            if (!_initialized) return;

            var original = CloneEffect(_originalEffect);
            MagSetFullscreenColorEffect(ref original);
            MagUninitialize();
            _initialized = false;
            _failureLogged = false;
        }
    }

    private static bool EnsureInitialized()
    {
        if (_initialized) return true;

        if (!MagInitialize())
        {
            if (!_failureLogged)
            {
                _failureLogged = true;
                Logger.Log($"Dim compositor initialization failed (Win32 {Marshal.GetLastWin32Error()}).");
            }
            return false;
        }

        _originalEffect = IdentityEffect();
        if (!MagGetFullscreenColorEffect(ref _originalEffect))
            _originalEffect = IdentityEffect();

        _initialized = true;
        return true;
    }

    // Color effects are 5x5 row-major matrices. Scaling the first three output
    // columns preserves any existing color filter while reducing its RGB output.
    private static MagColorEffect ScaleOutput(MagColorEffect source, float multiplier)
    {
        var result = CloneEffect(source);
        for (int row = 0; row < 5; row++)
            for (int column = 0; column < 3; column++)
                result.Transform[row * 5 + column] *= multiplier;
        return result;
    }

    private static MagColorEffect CloneEffect(MagColorEffect source) =>
        new() { Transform = (float[])source.Transform.Clone() };

    private static MagColorEffect IdentityEffect()
    {
        var values = new float[25];
        values[0] = values[6] = values[12] = values[18] = values[24] = 1.0f;
        return new MagColorEffect { Transform = values };
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MagColorEffect
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 25)]
        public float[] Transform;
    }

    [DllImport("Magnification.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MagInitialize();

    [DllImport("Magnification.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MagUninitialize();

    [DllImport("Magnification.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MagGetFullscreenColorEffect(ref MagColorEffect effect);

    [DllImport("Magnification.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MagSetFullscreenColorEffect(ref MagColorEffect effect);
}
