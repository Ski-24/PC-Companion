using PCCompanion;

int passed = 0;
void Test(string name, Action body)
{
    body();
    Console.WriteLine($"PASS {name}");
    passed++;
}
void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new Exception($"Expected {expected}, got {actual}");
}
void Sequence<T>(IEnumerable<T> expected, IEnumerable<T> actual)
{
    if (!expected.SequenceEqual(actual))
        throw new Exception($"Expected [{string.Join(", ", expected)}], got [{string.Join(", ", actual)}]");
}

int[] slots = [1, 2, 3]; // A50X, Fosi, speakers
List<(string Id, string Name)> allDevices =
    [("a50", "Headset Earphone (A50 X Voice)"), ("fosi", "Headphones (Fosi Audio K5 Pro)"), ("speakers", "Speakers (ORA by Kanto)")];
var labels = allDevices.ToDictionary(d => d.Id, d => d.Name);
string[] ids = ["a50", "fosi", "speakers"];

Test("speakers skips disconnected A50X and switches directly to Fosi", () =>
{
    var live = allDevices.Where(d => d.Id != "a50").ToList();
    var attempts = new List<string>();
    var skipped = new List<int>();
    var result = AudioCycle.TrySwitchNext(slots, 3, "speakers",
        n => AudioCycle.ActiveCandidates(ids[n - 1], labels[ids[n - 1]], live),
        id => { attempts.Add(id); return true; }, skipped.Add);
    Equal(new AudioCycle.Result(2, "fosi"), result!.Value);
    Sequence(new[] { "fosi" }, attempts);
    Sequence(new[] { 1 }, skipped);
});

Test("active-but-failing A50X falls through to Fosi in the same switch", () =>
{
    var attempts = new List<string>();
    var result = AudioCycle.TrySwitchNext(slots, 3, "speakers",
        n => AudioCycle.ActiveCandidates(ids[n - 1], labels[ids[n - 1]], allDevices),
        id => { attempts.Add(id); return id != "a50"; });
    Equal(new AudioCycle.Result(2, "fosi"), result!.Value);
    Sequence(new[] { "a50", "fosi" }, attempts);
});

Test("normal three-device cycle preserves its configured order", () =>
{
    for (int current = 1; current <= 3; current++)
    {
        var result = AudioCycle.TrySwitchNext(slots, current, ids[current - 1],
            n => new[] { ids[n - 1] }, _ => true);
        Equal(current % 3 + 1, result!.Value.Slot);
    }
});

Test("failed next slot wraps around to a working previous slot", () =>
{
    var result = AudioCycle.TrySwitchNext(slots, 2, "fosi",
        n => new[] { ids[n - 1] }, id => id == "a50");
    Equal(new AudioCycle.Result(1, "a50"), result!.Value);
});

Test("two-device toggle still works in both directions", () =>
{
    int[] two = [1, 2];
    Equal(2, AudioCycle.TrySwitchNext(two, 1, "a50", n => new[] { ids[n - 1] }, _ => true)!.Value.Slot);
    Equal(1, AudioCycle.TrySwitchNext(two, 2, "fosi", n => new[] { ids[n - 1] }, _ => true)!.Value.Slot);
});

Test("no available alternatives does not attempt the current output", () =>
{
    var live = allDevices.Where(d => d.Id == "speakers").ToList();
    int attempts = 0;
    var result = AudioCycle.TrySwitchNext(slots, 3, "speakers",
        n => AudioCycle.ActiveCandidates(ids[n - 1], labels[ids[n - 1]], live),
        _ => { attempts++; return true; });
    Equal<AudioCycle.Result?>(null, result);
    Equal(0, attempts);
});

Test("all failed alternatives are tried once, never the starting slot", () =>
{
    var attempts = new List<string>();
    var result = AudioCycle.TrySwitchNext(slots, 3, "speakers",
        n => new[] { ids[n - 1] }, id => { attempts.Add(id); return false; });
    Equal<AudioCycle.Result?>(null, result);
    Sequence(new[] { "a50", "fosi" }, attempts);
});

Test("reconnected USB device is resolved by its normalized name", () =>
{
    List<(string Id, string Name)> live = [("new-a50-id", "Headset Earphone (2- A50 X Voice)")];
    Sequence(new[] { "new-a50-id" }, AudioCycle.ActiveCandidates("old-a50-id", labels["a50"], live));
});

Test("saved endpoint is preferred only when it is active", () =>
{
    List<(string Id, string Name)> live = [("saved", "Renamed output"), ("replacement", labels["fosi"])];
    Sequence(new[] { "saved", "replacement" }, AudioCycle.ActiveCandidates("saved", labels["fosi"], live));
    Sequence(new[] { "replacement" }, AudioCycle.ActiveCandidates("offline", labels["fosi"], live));
});

Test("E1x2 Speakers driver rename resolves to main Playback 1/2", () =>
{
    List<(string Id, string Name)> live =
    [
        ("e12", "Playback 1/2 (2- E1x2 OTG)"),
        ("e34", "Playback 3/4 (2- E1x2 OTG)"),
        ("e56", "Playback 5/6 (2- E1x2 OTG)"),
        ("e78", "Playback 7/8 (2- E1x2 OTG)"),
    ];
    Sequence(new[] { "e12" },
        AudioCycle.ActiveCandidates("old-speakers", "Speakers (E1x2 OTG)", live));
    Equal(0, AudioCycle.BestCompatibleIndex(
        "old-speakers", "Speakers (E1x2 OTG)", live));
});

Test("E1x2 main-output compatibility never selects higher playback pairs", () =>
{
    Equal(-1, AudioCycle.RankCompatibleName(
        "Speakers (E1x2 OTG)", "Playback 3/4 (2- E1x2 OTG)"));
    Equal(-1, AudioCycle.RankCompatibleName(
        "Speakers (E1x2 OTG)", "Playback 7/8 (2- E1x2 OTG)"));
});

Test("unrelated interfaces are never treated as compatible", () =>
{
    Equal(-1, AudioCycle.RankCompatibleName(
        "Speakers (E1x2 OTG)", "Playback 1/2 (Fosi Audio K5 Pro)"));
});

Test("a failed endpoint does not prevent trying another candidate for that slot", () =>
{
    var result = AudioCycle.TrySwitchNext(slots, 3, "speakers",
        n => n == 1 ? new[] { "old-a50", "new-a50" } : new[] { "fosi" }, id => id == "new-a50");
    Equal(new AudioCycle.Result(1, "new-a50"), result!.Value);
});

Test("duplicate endpoint configuration never retries failures or selects the current ID", () =>
{
    var attempts = new List<string>();
    var result = AudioCycle.TrySwitchNext(slots, 3, "speakers",
        _ => new[] { "speakers", "failed", "failed" }, id => { attempts.Add(id); return false; });
    Equal<AudioCycle.Result?>(null, result);
    Sequence(new[] { "failed" }, attempts);
});

Test("unknown default starts at slot two and tries all configured outputs", () =>
{
    var attempts = new List<string>();
    var result = AudioCycle.TrySwitchNext(slots, 0, "external",
        n => new[] { ids[n - 1] }, id => { attempts.Add(id); return id == "a50"; });
    Equal(new AudioCycle.Result(1, "a50"), result!.Value);
    Sequence(new[] { "fosi", "speakers", "a50" }, attempts);
});

Test("optional third slot need not be configured", () =>
{
    Sequence(new[] { 3 }, AudioCycle.SlotsAfterCurrent(new[] { 1, 3 }, 1));
    Sequence(new[] { 1 }, AudioCycle.SlotsAfterCurrent(new[] { 1, 3 }, 3));
});

Test("zero or one configured slot terminates safely", () =>
{
    Sequence(Array.Empty<int>(), AudioCycle.SlotsAfterCurrent(Array.Empty<int>(), 0));
    Sequence(Array.Empty<int>(), AudioCycle.SlotsAfterCurrent(new[] { 1 }, 1));
});

Console.WriteLine($"{passed} audio cycle regression tests passed. No Windows audio settings were changed.");
