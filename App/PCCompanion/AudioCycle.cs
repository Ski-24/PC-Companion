namespace PCCompanion;

// Pure cycle/candidate selection, kept separate from Windows audio policy calls so
// disconnected devices and failed switches can be regression-tested without changing audio.
internal static class AudioCycle
{
    internal readonly record struct Result(int Slot, string EndpointId);

    public static List<int> SlotsAfterCurrent(IReadOnlyList<int> slots, int currentSlot)
    {
        var order = new List<int>();
        if (slots.Count == 0) return order;

        int currentIndex = -1;
        for (int i = 0; i < slots.Count; i++)
            if (slots[i] == currentSlot) { currentIndex = i; break; }

        // Preserve the existing unknown-default behavior: start with the second slot,
        // but eventually try the first as well because none of the slots is current.
        int count = currentIndex >= 0 ? slots.Count - 1 : slots.Count;
        if (currentIndex < 0) currentIndex = 0;
        for (int step = 1; step <= count; step++)
            order.Add(slots[(currentIndex + step) % slots.Count]);
        return order;
    }

    public static List<string> ActiveCandidates(string savedId, string label,
        IReadOnlyList<(string Id, string Name)> activeDevices)
    {
        var candidates = new List<string>();
        // A saved ID is not evidence that an endpoint is connected. Never send an
        // inactive endpoint to Windows; match a reconnected/renumbered device by name.
        if (!string.IsNullOrEmpty(savedId) && activeDevices.Any(d => d.Id == savedId))
            candidates.Add(savedId);
        if (!string.IsNullOrEmpty(label))
            foreach (var (id, name) in activeDevices)
                if (!candidates.Contains(id) && Normalize(name) == Normalize(label))
                    candidates.Add(id);
        return candidates;
    }

    public static Result? TrySwitchNext(IReadOnlyList<int> slots, int currentSlot,
        string currentId, Func<int, IReadOnlyList<string>> candidatesForSlot,
        Func<string, bool> trySwitch, Action<int>? onSkipped = null)
    {
        var attempted = new HashSet<string>(StringComparer.Ordinal);
        foreach (int slot in SlotsAfterCurrent(slots, currentSlot))
        {
            foreach (string id in candidatesForSlot(slot))
            {
                // Duplicate configuration must not switch back to the starting output
                // or keep retrying the same failed endpoint under another slot.
                if (string.IsNullOrEmpty(id) || id == currentId || !attempted.Add(id)) continue;
                if (trySwitch(id)) return new Result(slot, id);
            }
            // A device may still enumerate as Active while its headset/base is off.
            // Failed verification is a reason to try the next slot, not end the cycle.
            onSkipped?.Invoke(slot);
        }
        return null;
    }

    // Windows adds this USB-instance prefix after reconnects/driver updates.
    public static string Normalize(string name) =>
        System.Text.RegularExpressions.Regex.Replace(name, @"\(\d+- ", "(");
}
