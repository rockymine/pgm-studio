using PgmStudio.Geom.Algorithms;
using PgmStudio.Vocabulary;

namespace PgmStudio.Analysis.Playability;

/// <summary>
/// Void between standing ground and a build zone that nobody may build across, where the plan put ground
/// (<see cref="EditZoneRules.BuildZoneGap"/>).
///
/// <para>Read along the four axes from every column with a block at y=0, up to <see cref="Reach"/> columns
/// out: a run of void nobody may build across that ends on a column a player may build across is a gap. A run
/// that meets ground again, or reaches no build zone within the reach, is open void between islands, which is
/// the board's shape rather than this fault.</para>
///
/// <para><b>Only void the plan filled is a gap.</b> A plan states its build zones against its own pieces, so
/// a strip of void it leaves between a piece and a zone — a frontline standing on two legs — is the composed
/// board. What this looks for is ground the plan put against a zone and the sketch pulled back, so a board
/// built without a plan has nothing to compare against and answers nothing.</para>
///
/// <para>Findings come one per connected patch, largest first, each with its box and the widest run through
/// it — the number a build zone has to grow by to close it.</para>
/// </summary>
public static class BuildZoneGap
{
    /// <summary>How far out from a coast a build zone still counts as the one that coast was meant to reach,
    /// in columns. Past it the void is a crossing the board asks for, not a strip the zone stops short of.</summary>
    public const int Reach = 10;

    /// <summary>The most patches reported before the rest are summarised.</summary>
    private const int MostReported = 8;

    private static readonly (int X, int Z)[] Axes = [(1, 0), (-1, 0), (0, 1), (0, -1)];

    /// <summary>Patches of void between ground and a build zone that nobody may build across, over ground
    /// <paramref name="planned"/> — the plan's footprint in world blocks — says was there, largest first. Empty
    /// without a Y=0 layer, where void cannot be told from ground, and without a plan.</summary>
    public static IReadOnlyList<Finding> Check(Editability.Result zones, IReadOnlySet<(int X, int Z)>? planned)
    {
        if (!zones.HasY0 || planned is null) return [];

        // Each gap column, with the widest run found through it.
        var widest = new Dictionary<(int X, int Z), int>();
        for (var iz = 0; iz < zones.Height; iz++)
        for (var ix = 0; ix < zones.Width; ix++)
        {
            if (zones.IsVoid[iz * zones.Width + ix]) continue;
            foreach (var (dx, dz) in Axes)
                for (var step = 1; step <= Reach; step++)
                {
                    int nx = ix + dx * step, nz = iz + dz * step;
                    if (nx < 0 || nz < 0 || nx >= zones.Width || nz >= zones.Height) break;
                    var i = nz * zones.Width + nx;
                    if (!zones.IsVoid[i]) break;
                    if (!zones.Bridgeable(i)) continue;
                    for (var back = 1; back < step; back++)
                    {
                        var cell = (zones.MinX + ix + dx * back, zones.MinZ + iz + dz * back);
                        if (planned.Contains(cell)) widest[cell] = Math.Max(widest.GetValueOrDefault(cell), step - 1);
                    }
                    break;
                }
        }
        if (widest.Count == 0) return [];

        var patches = GridComponents.Label(widest.Keys, connectivity: 4)
            .OrderByDescending(patch => patch.Count)
            .ToList();

        var findings = new List<Finding>();
        foreach (var patch in patches.Take(MostReported))
        {
            int minX = patch.Min(c => c.X), maxX = patch.Max(c => c.X);
            int minZ = patch.Min(c => c.Z), maxZ = patch.Max(c => c.Z);
            var width = patch.Max(cell => widest[cell]);
            findings.Add(new Finding(EditZoneRules.BuildZoneGap,
                $"the void between a coast and a build region at x between {minX} and {maxX}, "
                + $"z between {minZ} and {maxZ} is up to {width} blocks wide across "
                + $"{Wording.Count(patch.Count, "column", "columns")}, less than {Reach} blocks",
                Severity.Complaint, Subjects: [$"{minX},{minZ}"]));
        }
        if (patches.Count > MostReported)
            findings.Add(new Finding(EditZoneRules.BuildZoneGap,
                $"the layout has {Wording.Count(patches.Count - MostReported, "further void", "further voids")} "
                + "between a coast and a build region, across "
                + $"{Wording.Count(patches.Skip(MostReported).Sum(patch => patch.Count), "column", "columns")}, "
                + $"each less than {Reach} blocks wide",
                Severity.Complaint));
        return findings;
    }
}
