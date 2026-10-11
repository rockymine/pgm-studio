using PgmStudio.Analysis.Region;
using PgmStudio.Vocabulary;

namespace PgmStudio.Analysis.Playability;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Whether the map's block rules let each goal be completed where it stands: a core's and a monument's block
/// broken (<see cref="EditZoneRules.SealedGoal"/>), and a core's lava let flow out of it
/// (<see cref="EditZoneRules.HeldLava"/>). A wool is not asked: PGM's wool module decides a block placed on a
/// monument last and lets the right team's wool in whatever the rules said.
///
/// <para><b>The goal's own material is asked, not "somebody's".</b> The zone grid reads a material filter as a
/// permission for somebody, which is right for a column and wrong for a goal: the void rule's break side lets
/// leaves and logs through over the void and refuses obsidian, so a core floating over a column with nothing at
/// y=0 reads breakable on the grid and cannot be broken in a match. Lava flowing is a block placed to PGM, so
/// the same place rule that refuses a bridge over the void refuses the lava a core lets out.</para>
/// </summary>
public static class GoalEdits
{
    /// <summary>Every goal the block rules keep from being completed, with the first column that refuses it.
    /// Empty without a y=0 layer, where void cannot be told from ground.</summary>
    public static IReadOnlyList<Finding> Check(Dict data, Editability.Result zones)
    {
        if (!zones.HasY0) return [];
        var regions = MapDoc.AsDict(data.GetValueOrDefault("regions"));
        var bounds = ((double)zones.MinX, (double)zones.MinZ, (double)zones.MaxX, (double)zones.MaxZ);
        var findings = new List<Finding>();

        List<(int X, int Z)> Cells(object? reference)
        {
            var mask = Editability.RegionMask(reference, regions, bounds, zones.MinX, zones.MinZ, zones.Width, zones.Height);
            var cells = new List<(int X, int Z)>();
            if (mask is null) return cells;
            for (var i = 0; i < mask.Length; i++)
                if (mask[i]) cells.Add((zones.MinX + i % zones.Width, zones.MinZ + i / zones.Width));
            return cells;
        }
        bool Void((int X, int Z) cell) => zones.VoidAt(cell.X, cell.Z) ?? true;
        bool Refused((int X, int Z) cell, bool place, string material) =>
            Editability.Lets(data, cell, Void(cell), place, material) == false;

        void Sealed(string what, List<(int X, int Z)> cells, string material)
        {
            var refused = cells.Where(cell => Refused(cell, false, material)).ToList();
            if (refused.Count == 0) return;
            var (x, z) = refused[0];
            findings.Add(new Finding(EditZoneRules.SealedGoal,
                $"{what}: the block rule refuses breaking {material} in "
                + $"{Wording.Count(refused.Count, "column", "columns")} of {cells.Count}, first at x {x}, z {z}",
                Severity.Complaint, Subjects: [$"{x},{z}"]));
        }

        foreach (var core in MapDoc.AsList(data.GetValueOrDefault("cores")).OfType<Dict>())
        {
            var id = core.GetValueOrDefault("id") as string ?? "a core";
            var material = core.GetValueOrDefault("material") as string is { Length: > 0 } stated ? stated : "obsidian";
            var cells = Cells(core.GetValueOrDefault("region"));
            if (cells.Count == 0) continue;
            Sealed($"core {id}", cells, material);

            var around = cells.SelectMany(c => Ring(c)).Distinct().ToList();
            if (around.All(cell => Refused(cell, true, "lava")))
            {
                var (x, z) = cells[0];
                findings.Add(new Finding(EditZoneRules.HeldLava,
                    $"core {id}: the block rule refuses lava in all {Wording.Count(around.Count, "column", "columns")} "
                    + $"under and beside it, from x {x}, z {z}",
                    Severity.Complaint, Subjects: [$"{x},{z}"]));
            }
        }

        foreach (var destroyable in MapDoc.AsList(data.GetValueOrDefault("destroyables")).OfType<Dict>())
        {
            var materials = (destroyable.GetValueOrDefault("materials") as string ?? "obsidian")
                .Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var cells = Cells(destroyable.GetValueOrDefault("region"));
            if (cells.Count > 0 && materials.Length > 0)
                Sealed($"monument {destroyable.GetValueOrDefault("id") as string ?? ""}".TrimEnd(), cells, materials[0]);
        }

        return findings;
    }

    /// <summary>A column and the eight round it: where lava let out of a core's casing lands first.</summary>
    private static IEnumerable<(int X, int Z)> Ring((int X, int Z) cell)
    {
        for (var dx = -1; dx <= 1; dx++)
            for (var dz = -1; dz <= 1; dz++)
                yield return (cell.X + dx, cell.Z + dz);
    }
}
