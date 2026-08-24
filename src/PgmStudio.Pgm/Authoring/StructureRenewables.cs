using PgmStudio.Domain;
using PgmStudio.Geom;

namespace PgmStudio.Pgm.Authoring;

/// <summary>
/// Export-time renewables for the plan-stamped iron cubes marked <c>renew</c> (a cube inside a spawn-role
/// piece — docs/generator/rules.md ST2). Each cube's XZ footprint becomes a region a
/// <c>&lt;renewable&gt;</c> regrows (mined iron → air replaced back to iron by the world). This is the direct
/// region-per-cube construct rather than <see cref="ResourceRenewables"/>'s spawn-protection reuse: a sketch
/// map's spawn protection is only the auto-stamped spawn cube, which does not cover a cube placed beside it,
/// so the cube gets its own renewable region.
///
/// <para>A cube standing <b>inside</b> a spawn protection is also ore in a spawn, and the blanket
/// <c>block=never</c> over the spawns would leave it unmineable — a renewable regrowing a block nobody may
/// break is scenery. So a cube that lands there is reported as ore and the protection is stated over it by
/// <see cref="SpawnOreProtection"/>, the same rule an imported world's scanned ore goes through.</para>
/// </summary>
public static class StructureRenewables
{
    private const int AvoidPlayers = 2;

    /// <summary>Adds a renewable per stamped cube, and answers whether any of them stands in a spawn — the
    /// ore the caller states the spawns' block protection over.</summary>
    public static List<SpawnOreProtection.Ore> Apply(MapXml m, IReadOnlyList<(int MinX, int MinZ, int MaxX, int MaxZ)> cubes)
    {
        if (cubes.Count == 0) return [];

        if (!m.Filters.ContainsKey("only-air"))
            m.Filters["only-air"] = new Filter { Id = "only-air", Type = "material", Material = "air" };
        if (!m.Filters.ContainsKey("only-iron"))
            m.Filters["only-iron"] = new Filter { Id = "only-iron", Type = "material", Material = "iron block" };

        var childIds = new List<string>();
        for (var i = 0; i < cubes.Count; i++)
        {
            var c = cubes[i];
            var id = $"iron-cube-{i}";
            m.Regions[id] = new Region
            {
                Id = id, Type = "rectangle",
                MinX = c.MinX, MinZ = c.MinZ, MaxX = c.MaxX, MaxZ = c.MaxZ,
                Bounds2d = Bounds2d.Of(c.MinX, c.MinZ, c.MaxX, c.MaxZ),
            };
            childIds.Add(id);
        }

        string regionId;
        if (childIds.Count == 1)
            regionId = childIds[0];
        else
        {
            regionId = "iron-cubes";
            m.Regions[regionId] = new Region { Id = regionId, Type = "union", Children = childIds };
        }

        m.Renewables.Add(new Renewable
        {
            RegionId = regionId, RenewFilter = "only-iron", ReplaceFilter = "only-air", AvoidPlayers = AvoidPlayers,
        });

        // A cube overlapping a spawn protection is ore in a spawn, whatever put it there. Overlap rather than
        // containment: a cube half inside the protection is half unmineable, which is the same fault.
        var spawns = SpawnOreProtection.Protections(m);
        var inSpawn = cubes.Any(c => spawns.Any(s => s.Box.Overlaps(new Rect(c.MinX, c.MinZ, c.MaxX, c.MaxZ))));
        return inSpawn ? [new SpawnOreProtection.Ore("iron", "iron block")] : [];
    }
}
