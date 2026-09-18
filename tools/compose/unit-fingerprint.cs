#:project ../../src/PgmStudio.Pgm/PgmStudio.Pgm.csproj
// team-unit fingerprint: a total hash of allocate->fill over every symmetry x preset x seed. Serializes
// EVERYTHING the allocator decides (box rects, hub form, flip, per-wool fill, every joint + its offer, the
// spawn facing) and everything the filler emits (piece id, rect, role, slot, owning box), then hashes it —
// per preset and overall. The check a refactor runs against: a pure structure pass must leave the total
// bit-identical, and a behavioural change should move exactly the presets it claims to.
// Run: dotnet run tools/compose/unit-fingerprint.cs
// NB `dotnet run <script>.cs` caches its build keyed on the SCRIPT — after changing src/, first
//    rm -rf ~/.local/share/dotnet/runfile/unit-fingerprint-*   or it silently re-reports the old hash.
using System.Security.Cryptography;
using System.Text;
using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Plan;

// one preset per size band, plus a second count inside the wide ones — the envelope is derived the way a
// compose derives it, so the land and the corridor are the band's rather than a number picked here
var presets = new (string Label, int Players)[]
{
    ("nano", 8), ("nano+", 12), ("micro", 16), ("micro+", 20),
    ("milli", 24), ("centi", 32), ("centi+", 40),
};
var symmetries = new[] { "mirror_z", "mirror_x", "rot_180" };
const int seeds = 400;

var all = new StringBuilder();
foreach (var sym in symmetries)
foreach (var (label, players) in presets)
{
    var env = Envelope.Derive(new ComposeRequest(players, 2, sym), new ComposeRng(0));
    var sb = new StringBuilder();
    int alloc = 0, filled = 0;

    for (var seed = 0; seed < seeds; seed++)
    {
        sb.Append($"[{sym}/{label}/{seed}]");
        if (TeamUnitAllocator.Allocate(env, new ComposeRng((ulong)seed)) is not { } a) { sb.Append("NOALLOC\n"); continue; }
        alloc++;
        sb.Append($"facing={a.SpawnFacing};");
        foreach (var b in a.Partition.Boxes)
            sb.Append($"B({b.Id},{b.Kind},[{string.Join(" ", b.Rect)}],land={b.LandTargetCells}," +
                $"form={(b.Form is null ? "-" : $"{b.Form.Form}:{b.Form.Arms}")},flip={b.FlipV}," +
                $"wool={(b.Wool is null ? "-" : $"{b.Wool.Family}:{b.Wool.Placement}:{b.Wool.Flip}:{b.Wool.WoolAtEnd}")});");
        foreach (var j in a.Partition.Joints)
            sb.Append($"J({j.BoxA}->{j.BoxB},{j.Abutment.Edge}@{j.Abutment.Start}+{j.Abutment.WidthCells}," +
                $"grant={(j.Grant is null ? "-" : $"{j.Grant.Edge}@{j.Grant.Interval.Start}+{j.Grant.Interval.LengthCells}" +
                    $":w{j.Grant.WidthClass}:{j.Grant.Grouping}:{j.Grant.GroupId}")});");

        if (TeamUnitFiller.Fill(a.Partition, a.SpawnFacing, new ComposeRng((ulong)seed)) is not { } f)
        { sb.Append("NOFILL\n"); continue; }
        filled++;
        foreach (var p in f.Unit.Pieces)
            sb.Append($"P({p.Id},[{string.Join(" ", p.Rect)}],{p.Role},{p.Slot},{p.Box?.Id}/{p.Box?.Kind});");
        sb.Append($"spawn({f.Unit.Spawn.Piece},[{string.Join(" ", f.Unit.Spawn.At)}],{f.Unit.Spawn.Facing});");
        foreach (var wl in f.Unit.Wools) sb.Append($"wool({wl.Piece},[{string.Join(" ", wl.At)}]);");
        foreach (var o in f.FrontlineFace)
            sb.Append($"face({o.Edge}@{o.Interval.Start}+{o.Interval.LengthCells}:w{o.WidthClass}:{o.Grouping});");
        sb.Append('\n');
    }

    var text = sb.ToString();
    all.Append(text);
    Console.WriteLine($"{sym,-9} {label,-6} alloc {alloc,3}/{seeds}  filled {filled,3}  {Hash(text)}");
}

Console.WriteLine($"\nTOTAL {Hash(all.ToString())}   ({all.Length} chars)");

static string Hash(string s) =>
    Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)))[..16];
