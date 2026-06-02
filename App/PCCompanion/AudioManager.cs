using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;

namespace PCCompanion;

static class AudioManager
{
    public static void SwitchToNext()
    {
        var s        = AppSettings.Current;
        var devices  = GetRenderDevices();
        string currentId = GetCurrentDefaultId();
        bool updated = false;

        // Heal saved IDs: exact name match first, then normalized name (strips USB "N- " prefix).
        // Also update the saved label when the actual device name differs.
        if (!devices.Any(d => d.Id == s.Device1Id))
        {
            var m = FindByLabel(devices, s.Device1Label);
            if (m.Id != null)
            {
                Logger.Log($"AudioManager: healed D1 '{s.Device1Label}' → '{m.Name}' [{Trunc(m.Id)}]");
                s.Device1Id = m.Id; s.Device1Label = Norm(m.Name); updated = true;
            }
        }
        if (!devices.Any(d => d.Id == s.Device2Id))
        {
            var m = FindByLabel(devices, s.Device2Label);
            if (m.Id != null)
            {
                Logger.Log($"AudioManager: healed D2 '{s.Device2Label}' → '{m.Name}' [{Trunc(m.Id)}]");
                s.Device2Id = m.Id; s.Device2Label = Norm(m.Name); updated = true;
            }
        }
        if (updated) { s.Save(); AppSettings.Invalidate(); }

        // Determine which configured device is currently active (ID, then normalized name)
        bool onDevice1 = currentId == s.Device1Id;
        bool onDevice2 = !onDevice1 && currentId == s.Device2Id;
        if (!onDevice1 && !onDevice2)
        {
            var curName = Norm(devices.FirstOrDefault(d => d.Id == currentId).Name ?? "");
            onDevice1 = curName != "" && curName == Norm(s.Device1Label);
            onDevice2 = !onDevice1 && curName != "" && curName == Norm(s.Device2Label);
        }
        // Current default is neither configured device (e.g. a scene's audio target like
        // "A50 X Voice"). Treat it as Device 1 so this stays a clean Device1↔Device2 toggle
        // and the next switch lands on Device 2 — matching what the Audio card displays
        // (it also presents the unknown case as Device 1).
        if (!onDevice1 && !onDevice2) onDevice1 = true;

        string targetLabel = onDevice1 ? s.Device2Label : s.Device1Label;
        string targetId    = onDevice1 ? s.Device2Id    : s.Device1Id;

        // Build candidate list: saved ID first, then any active endpoint matching by normalized name
        var candidates = new List<string>();
        if (!string.IsNullOrEmpty(targetId)) candidates.Add(targetId);
        foreach (var (id, name) in devices)
            if (!candidates.Contains(id) && Norm(name) == Norm(targetLabel))
                candidates.Add(id);

        Logger.Log($"AudioManager: → '{targetLabel}' ({candidates.Count} candidate(s))");

        string? workedId = null;
        foreach (var id in candidates)
        {
            try { SetDefault(id); } catch (Exception ex) { Logger.Log($"  [{Trunc(id)}] threw: {ex.Message}"); continue; }
            Thread.Sleep(120);
            if (GetCurrentDefaultId() == id) { workedId = id; break; }
        }

        if (workedId == null)
            throw new Exception($"No working endpoint found for '{targetLabel}'");

        // Snap saved IDs to the endpoints that actually worked
        bool snap = false;
        if (onDevice1  && currentId != s.Device1Id) { s.Device1Id = currentId; snap = true; }
        if (onDevice2  && currentId != s.Device2Id) { s.Device2Id = currentId; snap = true; }
        if (onDevice1  && workedId  != s.Device2Id) { s.Device2Id = workedId;  snap = true; }
        if (!onDevice1 && workedId  != s.Device1Id) { s.Device1Id = workedId;  snap = true; }
        if (snap) { s.Save(); AppSettings.Invalidate(); }

        Logger.Log($"AudioManager: switched to {targetLabel}");
    }

    // Exact name match first, then normalized name (strips USB "N- " prefix).
    private static (string? Id, string Name) FindByLabel(List<(string Id, string Name)> devices, string label)
    {
        var exact = devices.FirstOrDefault(d => d.Name == label);
        if (exact.Id != null) return exact;
        var norm  = devices.FirstOrDefault(d => Norm(d.Name) == Norm(label));
        return norm.Id != null ? norm : (null, "");
    }

    // Strip the "N- " USB-instance prefix Windows adds inside device name parentheses.
    // "Headphones (2- A50 X Game)" → "Headphones (A50 X Game)"
    private static string Norm(string name) =>
        System.Text.RegularExpressions.Regex.Replace(name, @"\(\d+- ", "(");

    private static string Trunc(string s) => s.Length > 36 ? s[..36] : s;

    public static string GetCurrentDefaultId()
    {
        try
        {
            using var e = new MMDeviceEnumerator();
            return e.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia).ID;
        }
        catch (Exception ex)
        {
            Logger.Log($"GetCurrentDefaultId: {ex.Message}");
            return "";
        }
    }

    // Current default render endpoint as (Id, FriendlyName). Lets the UI match by name
    // when a saved endpoint ID has gone stale (reconnect / driver update / Couch switch).
    public static (string Id, string Name) GetCurrentDefault()
    {
        try
        {
            using var e = new MMDeviceEnumerator();
            var d = e.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            return (d.ID, d.FriendlyName);
        }
        catch (Exception ex)
        {
            Logger.Log($"GetCurrentDefault: {ex.Message}");
            return ("", "");
        }
    }

    // Exposes the same name normalization used internally (strips the USB "N- " prefix).
    public static string Normalize(string name) => Norm(name);

    public static List<(string Id, string Name)> GetRenderDevices()
    {
        var list = new List<(string, string)>();
        try
        {
            using var e = new MMDeviceEnumerator();
            foreach (var d in e.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
                list.Add((d.ID, d.FriendlyName));
        }
        catch (Exception ex) { Logger.Log($"GetRenderDevices: {ex.Message}"); }
        return list;
    }

    // Public, exception-safe wrapper used by Couch Mode to set a specific endpoint
    // (and restore the previous one). Returns true if the default actually changed to it.
    public static bool TrySetDefault(string deviceId)
    {
        if (string.IsNullOrEmpty(deviceId)) return false;
        try { SetDefault(deviceId); }
        catch (Exception ex) { Logger.Log($"TrySetDefault: {ex.Message}"); return false; }
        Thread.Sleep(120);
        return GetCurrentDefaultId() == deviceId;
    }

    private static void SetDefault(string deviceId)
    {
        var client = (IPolicyConfig)new PolicyConfigCoClass();
        try
        {
            int hr;
            if ((hr = client.SetDefaultEndpoint(deviceId, ERole.eConsole)) != 0)
                throw new COMException("SetDefaultEndpoint(Console) failed", hr);
            if ((hr = client.SetDefaultEndpoint(deviceId, ERole.eMultimedia)) != 0)
                throw new COMException("SetDefaultEndpoint(Multimedia) failed", hr);
            if ((hr = client.SetDefaultEndpoint(deviceId, ERole.eCommunications)) != 0)
                throw new COMException("SetDefaultEndpoint(Communications) failed", hr);
        }
        finally { Marshal.ReleaseComObject(client); }
    }
}

// ── COM definitions (undocumented Windows audio policy API) ──────────────────

[ComImport, Guid("870af99c-171d-4f9e-af0d-e63df40c2bc9")]
[ClassInterface(ClassInterfaceType.None)]
file class PolicyConfigCoClass { }

[ComImport, Guid("f8679f50-850a-41cf-9c72-430f290290c8")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
file interface IPolicyConfig
{
    [PreserveSig] int GetMixFormat(        [MarshalAs(UnmanagedType.LPWStr)] string dev, IntPtr a);
    [PreserveSig] int GetDeviceFormat(     [MarshalAs(UnmanagedType.LPWStr)] string dev, bool b, IntPtr c);
    [PreserveSig] int ResetDeviceFormat(   [MarshalAs(UnmanagedType.LPWStr)] string dev);
    [PreserveSig] int SetDeviceFormat(     [MarshalAs(UnmanagedType.LPWStr)] string dev, IntPtr a, IntPtr b);
    [PreserveSig] int GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string dev, bool b, IntPtr c, IntPtr d);
    [PreserveSig] int SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string dev, IntPtr a);
    [PreserveSig] int GetShareMode(        [MarshalAs(UnmanagedType.LPWStr)] string dev, IntPtr a);
    [PreserveSig] int SetShareMode(        [MarshalAs(UnmanagedType.LPWStr)] string dev, IntPtr a);
    [PreserveSig] int GetPropertyValue(    [MarshalAs(UnmanagedType.LPWStr)] string dev, bool b, IntPtr c, IntPtr d);
    [PreserveSig] int SetPropertyValue(    [MarshalAs(UnmanagedType.LPWStr)] string dev, bool b, IntPtr c, IntPtr d);
    [PreserveSig] int SetDefaultEndpoint(  [MarshalAs(UnmanagedType.LPWStr)] string dev, [MarshalAs(UnmanagedType.U4)] ERole role);
    [PreserveSig] int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string dev, bool visible);
}

file enum ERole { eConsole = 0, eMultimedia = 1, eCommunications = 2 }
