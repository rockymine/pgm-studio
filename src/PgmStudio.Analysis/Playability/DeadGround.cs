using PgmStudio.Analysis.Region;
using PgmStudio.Geom;
using PgmStudio.Geom.Algorithms;
using PgmStudio.Vocabulary;

namespace PgmStudio.Analysis.Playability;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Ground a player can stand on and nobody can edit — the fault the editability pass exists to find, read off
/// the zones and the walk together (<see cref="EditZoneRules.DeadGround"/>).
///
/// <para><b>What is excluded is named geometrically rather than by name.</b> A spawn and a wool room are
/// sealed deliberately, and both are recognised by the thing that makes them protected: some rule restricts
/// <c>enter</c> on the region. A place a player may not walk into is not a place they expected to build in,
/// and asking the document which regions those are beats matching on <c>"spawn"</c> — a map is free to call
/// its rooms anything.</para>
///
/// <para>Findings come one per connected patch rather than one per column, because a thousand findings over
/// one crag is a thousand ways to miss the second crag. Each names its cell count and its box, which is what
/// makes the claim checkable in-game.</para>
/// </summary>
public static class DeadGround
{
    /// <summary>The most patches reported before the rest are summarised. A map with more than this has a
    /// systematic fault rather than a list of spots, and the count says so better than the list would.</summary>
    private const int MostReported = 8;

    /// <summary>Patches of standable ground that no player may edit, largest first.</summary>
    public static IReadOnlyList<Finding> Check(Editability.Result zones, WalkGround walk, Dict data)
    {
        var protectedCells = Protected(data, zones);
        var sealedIndex = EditZone.IndexOf(EditZone.Sealed);

        var dead = new HashSet<(int X, int Z)>();
        foreach (var cell in walk.Ground.Select(place => place.Cell).Distinct())
        {
            int ix = cell.X - zones.MinX, iz = cell.Z - zones.MinZ;
            if (ix < 0 || iz < 0 || ix >= zones.Width || iz >= zones.Height) continue;
            var i = iz * zones.Width + ix;
            if (zones.Zone[i] != sealedIndex || protectedCells[i]) continue;
            dead.Add(cell);
        }
        if (dead.Count == 0) return [];

        var patches = GridComponents.Label(dead, connectivity: 8)
            .OrderByDescending(patch => patch.Count)
            .ToList();

        var findings = new List<Finding>();
        foreach (var patch in patches.Take(MostReported))
        {
            int minX = patch.Min(c => c.X), maxX = patch.Max(c => c.X);
            int minZ = patch.Min(c => c.Z), maxZ = patch.Max(c => c.Z);
            findings.Add(new Finding(EditZoneRules.DeadGround,
                $"{patch.Count} column(s) of standing ground in x {minX}..{maxX}, z {minZ}..{maxZ} cannot be "
                + "edited by anyone — no build zone reaches them and nothing is under them at y=0",
                Severity.Complaint, Subjects: [$"{minX},{minZ}"]));
        }
        if (patches.Count > MostReported)
            findings.Add(new Finding(EditZoneRules.DeadGround,
                $"and {patches.Count - MostReported} further patch(es) of ground nobody can edit, "
                + $"{patches.Skip(MostReported).Sum(patch => patch.Count)} column(s) between them",
                Severity.Complaint));
        return findings;
    }

    /// <summary>The cells a rule restricts entry to — a spawn, a wool room, anything else the author walled
    /// off. Sealing ground nobody may walk into is the point of those regions, so their columns are not the
    /// fault this looks for.</summary>
    private static bool[] Protected(Dict data, Editability.Result zones)
    {
        var regions = MapDoc.AsDict(data.GetValueOrDefault("regions"));
        var bounds = ((double)zones.MinX, (double)zones.MinZ, (double)zones.MaxX, (double)zones.MaxZ);
        var covered = new bool[zones.Width * zones.Height];

        foreach (var rule in MapDoc.AsList(data.GetValueOrDefault("apply_rules")).OfType<Dict>())
        {
            if (rule.GetValueOrDefault("enter") is not string entry || entry.Length == 0) continue;
            var mask = Editability.RegionMask(rule.GetValueOrDefault("region"), regions, bounds,
                zones.MinX, zones.MinZ, zones.Width, zones.Height);
            if (mask is null) continue;
            for (var i = 0; i < covered.Length; i++) if (mask[i]) covered[i] = true;
        }
        return covered;
    }
}
