using Microsoft.Win32;

namespace PCCompanion;

static class AutoHdrService
{
    private const string RegPath  = @"Software\Microsoft\DirectX\UserGpuPreferences";
    private const string RegValue = "DirectXUserGlobalSettings";

    public enum State { Unknown, Off, On }

    public static State GetState()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegPath);
            var raw = key?.GetValue(RegValue) as string;
            if (raw is null)
            {
                Logger.Log("AutoHdr.GetState: key/value not found");
                return State.Unknown;
            }
            var fields = Parse(raw);
            if (!fields.TryGetValue("AutoHDREnable", out var val))
            {
                Logger.Log($"AutoHdr.GetState: AutoHDREnable missing from '{raw}'");
                return State.Unknown;
            }
            var state = val == "1" ? State.On : State.Off;
            Logger.Log($"AutoHdr.GetState: AutoHDREnable={val} → {state}");
            return state;
        }
        catch (Exception ex)
        {
            Logger.Log($"AutoHdr.GetState: {ex.Message}");
            return State.Unknown;
        }
    }

    // Returns null on success, error description on failure.
    // Reads current state, flips AutoHDREnable, writes back, verifies, reverts on mismatch.
    public static string? Toggle(State current)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegPath, writable: true);
            if (key is null) return "Registry key not found";

            var raw = key.GetValue(RegValue) as string;
            if (raw is null) return "DirectXUserGlobalSettings not found";

            var fields = Parse(raw);
            if (!fields.ContainsKey("AutoHDREnable")) return "AutoHDREnable field missing";

            string newVal = current == State.On ? "0" : "1";
            fields["AutoHDREnable"] = newVal;
            string newRaw = Serialize(fields, raw);

            Logger.Log($"AutoHdr.Toggle: hdr={HdrDetector.IsEnabled()} old='{raw}'");
            key.SetValue(RegValue, newRaw, RegistryValueKind.String);

            // Immediate read-back to verify the write succeeded
            var readRaw = key.GetValue(RegValue) as string;
            var readFields = readRaw is not null ? Parse(readRaw) : new();
            readFields.TryGetValue("AutoHDREnable", out var readVal);
            Logger.Log($"AutoHdr.Toggle: new='{newRaw}' readback='{readRaw}' AutoHDREnable={readVal}");

            if (readVal != newVal)
            {
                Logger.Log("AutoHdr.Toggle: readback mismatch — reverting");
                key.SetValue(RegValue, raw, RegistryValueKind.String);
                return $"Write verification failed (expected {newVal}, got {readVal ?? "null"}) — reverted";
            }

            // Readback matches — write confirmed. Windows Settings page may still show the old
            // value until the user leaves Display > HDR and returns; that is a Settings UI
            // refresh issue, not a failed write.
            Logger.Log("AutoHdr.Toggle: write confirmed via readback (Settings UI may need reopening to reflect change)");
            return null;
        }
        catch (Exception ex)
        {
            Logger.Log($"AutoHdr.Toggle: {ex.Message}");
            return ex.Message;
        }
    }

    private static Dictionary<string, string> Parse(string raw)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in raw.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            int idx = part.IndexOf('=');
            if (idx > 0) d[part[..idx]] = part[(idx + 1)..];
        }
        return d;
    }

    // Reconstruct the value string preserving original field order; append new fields at end.
    private static string Serialize(Dictionary<string, string> fields, string originalRaw)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sb   = new System.Text.StringBuilder();
        foreach (var part in originalRaw.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            int idx = part.IndexOf('=');
            if (idx <= 0) continue;
            var k = part[..idx];
            if (fields.TryGetValue(k, out var v)) { sb.Append($"{k}={v};"); seen.Add(k); }
        }
        foreach (var (k, v) in fields)
            if (!seen.Contains(k)) sb.Append($"{k}={v};");
        return sb.ToString();
    }
}
