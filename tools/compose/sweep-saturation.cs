#:project ../../src/PgmStudio.Pgm/PgmStudio.Pgm.csproj
#:property JsonSerializerIsReflectionEnabledByDefault=true

using PgmStudio.Pgm.Compose;

// how many distinct leg layouts the branch samplers produce per spine length, and how many seeds it takes
// to see them all — the coverage question behind Producibility's seed sweep.
static string Key(IReadOnlyList<(int Start, int Width)> l) => string.Join("+", l.Select(a => $"{a.Start}:{a.Width}"));

const ulong Deep = 2_000_000;

Console.WriteLine("frontline 2-leg");
for (int spine = 8; spine <= 20; spine++)
{
    var seen = new Dictionary<string, ulong>();
    for (ulong s = 1; s <= Deep; s++)
        if (FrontlineBoxEmitter.SampleArms(new ComposeRng(s), spine, 2) is { } l)
            seen.TryAdd(Key(l), s);
    if (seen.Count == 0) { Console.WriteLine($"  spine {spine,2}: none"); continue; }
    var at400 = seen.Values.Count(v => v <= 400);
    Console.WriteLine($"  spine {spine,2}: {seen.Count,3} distinct, {at400,3} within 400 seeds, "
        + $"last first-seen at seed {seen.Values.Max()}");
}

Console.WriteLine("frontline 1-leg");
for (int spine = 6; spine <= 20; spine++)
{
    var seen = new Dictionary<string, ulong>();
    for (ulong s = 1; s <= Deep; s++)
        if (FrontlineBoxEmitter.SampleArms(new ComposeRng(s), spine, 1) is { } l)
            seen.TryAdd(Key(l), s);
    if (seen.Count == 0) { Console.WriteLine($"  spine {spine,2}: none"); continue; }
    Console.WriteLine($"  spine {spine,2}: {seen.Count,3} distinct, "
        + $"{seen.Values.Count(v => v <= 400),3} within 400, last at {seen.Values.Max()}");
}

Console.WriteLine("hub branch (cw 2)");
foreach (var arms in new[] { 1, 2 })
    for (int spine = 4; spine <= 20; spine++)
    {
        var seen = new Dictionary<string, ulong>();
        for (ulong s = 1; s <= Deep; s++)
            if (HubBoxEmitter.SampleArms(new ComposeRng(s), spine, arms, 2) is { } l)
                seen.TryAdd(Key(l), s);
        if (seen.Count == 0) continue;
        Console.WriteLine($"  arms {arms} spine {spine,2}: {seen.Count,3} distinct, "
            + $"{seen.Values.Count(v => v <= 400),3} within 400, last at {seen.Values.Max()}");
    }
