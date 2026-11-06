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

        // Configured slots in cycle order (1, 2, and optionally 3). At least two are needed
        // to switch between; the 3rd is optional, so this stays a 2-way toggle until it's set.
        var slots = ConfiguredSlots(s);
        if (slots.Count < 2) { Logger.Log("AudioManager: fewer than 2 devices configured — skipping"); return; }

        // Heal saved IDs for each slot: exact name match first, then normalized name (strips
        // USB "N- " prefix). Also refresh the saved label when the actual device name differs.
        foreach (int n in slots)
        {
            if (devices.Any(d => d.Id == SlotId(s, n))) continue;
            var m = FindByLabel(devices, SlotLabel(s, n));
            if (m.Id != null)
            {
                Logger.Log($"AudioManager: healed D{n} '{SlotLabel(s, n)}' → '{m.Name}' [{Trunc(m.Id)}]");
                SetSlotId(s, n, m.Id); SetSlotLabel(s, n, Norm(m.Name)); updated = true;
            }
        }
        if (updated) { s.Save(); AppSettings.Invalidate(); }

        // Which configured slot is currently active (by ID, then normalized name)?
        int curSlot = CurrentSlot(s, currentId, devices.FirstOrDefault(d => d.Id == currentId).Name ?? "");
        // Current default is none of the configured devices (e.g. a scene's audio target like
        // "A50 X Voice"): treat it as the first slot so the next switch lands on the 2nd —
        // matching what the Audio card displays (it presents the unknown case the same way).
        bool attemptedSwitch = false;
        var result = AudioCycle.TrySwitchNext(slots, curSlot, currentId,
            n =>
            {
                var candidates = AudioCycle.ActiveCandidates(SlotId(s, n), SlotLabel(s, n), devices);
                Logger.Log($"AudioManager: → '{SlotLabel(s, n)}' ({candidates.Count} active candidate(s))");
                return candidates;
            },
            id =>
            {
                attemptedSwitch = true;
                return TrySetDefault(id);
            },
            n => Logger.Log($"AudioManager: skipping unavailable/failed '{SlotLabel(s, n)}'"));

        if (result is null)
        {
            // A failed policy call may have changed only some audio roles. Restore the
            // starting endpoint when no alternative worked, rather than showing an error
            // or leaving Console/Multimedia/Communications pointing at different outputs.
            if (attemptedSwitch && !string.IsNullOrEmpty(currentId) && devices.Any(d => d.Id == currentId))
            {
                if (!TrySetDefault(currentId)) Logger.Log("AudioManager: could not restore the starting endpoint");
            }
            Logger.Log("AudioManager: no available alternative; keeping the current output");
            return;
        }

        int targetSlot = result.Value.Slot;
        string targetLabel = SlotLabel(s, targetSlot);
        string workedId = result.Value.EndpointId;

        // Snap saved IDs to the endpoints that actually worked: the slot we came from (if we
        // matched one) gets the old default's ID, and the target slot gets the working ID.
        bool snap = false;
        if (curSlot > 0 && currentId != SlotId(s, curSlot)) { SetSlotId(s, curSlot, currentId); snap = true; }
        if (workedId != SlotId(s, targetSlot))              { SetSlotId(s, targetSlot, workedId); snap = true; }
        if (snap) { s.Save(); AppSettings.Invalidate(); }

        Logger.Log($"AudioManager: switched to {targetLabel}");
    }

    // ── Audio slot helpers (Device 1/2/3) ────────────────────────────────────────
    // The Audio card cycles through whichever slots are configured. Slot 3 is optional.

    public static string SlotId(AppSettings s, int n) => n switch
    {
        1 => s.Device1Id, 2 => s.Device2Id, 3 => s.Device3Id, _ => "",
    };
    public static string SlotLabel(AppSettings s, int n) => n switch
    {
        1 => s.Device1Label, 2 => s.Device2Label, 3 => s.Device3Label, _ => "",
    };
    private static void SetSlotId(AppSettings s, int n, string id)
    {
        switch (n) { case 1: s.Device1Id = id; break; case 2: s.Device2Id = id; break; case 3: s.Device3Id = id; break; }
    }
    private static void SetSlotLabel(AppSettings s, int n, string label)
    {
        switch (n) { case 1: s.Device1Label = label; break; case 2: s.Device2Label = label; break; case 3: s.Device3Label = label; break; }
    }

    // Slots that have an ID configured, in cycle order. A slot counts as configured only
    // when its endpoint ID is set (the label alone is a placeholder).
    public static List<int> ConfiguredSlots(AppSettings s)
    {
        var list = new List<int>();
        if (!string.IsNullOrEmpty(s.Device1Id)) list.Add(1);
        if (!string.IsNullOrEmpty(s.Device2Id)) list.Add(2);
        if (!string.IsNullOrEmpty(s.Device3Id)) list.Add(3);
        return list;
    }

    // Which configured slot the given default endpoint maps to (1/2/3), or 0 if none.
    // Matches by ID first, then by normalized friendly name (resilient to stale IDs).
    public static int CurrentSlot(AppSettings s, string currentId, string currentName)
    {
        foreach (int n in ConfiguredSlots(s))
        {
            if (currentId == SlotId(s, n)) return n;
        }
        if (!string.IsNullOrEmpty(currentName))
            foreach (int n in ConfiguredSlots(s))
                if (AudioCycle.RankCompatibleName(SlotLabel(s, n), currentName) >= 0) return n;
        return 0;
    }

    // Exact/renumbered name first, then a compatible main-output driver rename.
    private static (string? Id, string Name) FindByLabel(List<(string Id, string Name)> devices, string label)
    {
        int index = AudioCycle.BestCompatibleIndex("", label, devices);
        return index >= 0 ? devices[index] : (null, "");
    }

    // Strip the "N- " USB-instance prefix Windows adds inside device name parentheses.
    // "Headphones (2- A50 X Game)" → "Headphones (A50 X Game)"
    private static string Norm(string name) =>
        AudioCycle.Normalize(name);

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

    // Resolves a saved endpoint after a reconnect, driver rename, or USB-instance change.
    public static int FindConfiguredDeviceIndex(IReadOnlyList<(string Id, string Name)> devices,
        string savedId, string label) => AudioCycle.BestCompatibleIndex(savedId, label, devices);

    public static bool LabelsReferToSameOutput(string first, string second) =>
        AudioCycle.RankCompatibleName(first, second) >= 0;

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

    // Switches the default render endpoint to a target identified by a saved ID and/or a
    // friendly label, healing a stale saved ID by matching the active device list by
    // normalized name — the same resilience SwitchToNext has. Scenes (Couch/Morning) store
    // a TargetAudioId that can go stale across reconnects/driver updates/USB-instance
    // renumbering, which made the plain-ID TrySetDefault throw and the scene silently fail.
    // Returns the endpoint ID that actually became default, or null if none worked.
    public static string? TrySetDefaultByIdOrName(string savedId, string label)
    {
        var devices = GetRenderDevices();

        // Candidate order: live saved ID, exact/renumbered name, then a compatible
        // main-stereo endpoint exposed under a new driver role name.
        var candidates = AudioCycle.ActiveCandidates(savedId, label, devices);

        if (candidates.Count == 0)
        {
            Logger.Log($"TrySetDefaultByIdOrName: no candidate for '{label}' [{Trunc(savedId)}]");
            return null;
        }

        Logger.Log($"TrySetDefaultByIdOrName: → '{label}' ({candidates.Count} candidate(s))");
        foreach (var id in candidates)
        {
            try { SetDefault(id); } catch (Exception ex) { Logger.Log($"  [{Trunc(id)}] threw: {ex.Message}"); continue; }
            Thread.Sleep(120);
            if (GetCurrentDefaultId() == id) { Logger.Log($"TrySetDefaultByIdOrName: switched to {label}"); return id; }
        }
        Logger.Log($"TrySetDefaultByIdOrName: no working endpoint for '{label}'");
        return null;
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
