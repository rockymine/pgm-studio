using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Tests.Shapes;

/// <summary>
/// Family stability under the emitter's placement manipulations: sliding an endpoint (entry or wool) off its
/// box corner, docking the wool room off the side of the terminal piece, and moving a donut attachment along
/// the ring — each must keep the family it varies, both standalone and with neighbour terrain docked at the
/// entry (an approach is classified inside a plan, so the read must not flip when a hub mass joins the
/// component). Grids follow the catalog notation (t terrain, v void, w wool) plus h = docked neighbour
/// terrain; each grid is also classified at a uniform 2× scale (width-independence).
/// </summary>
public sealed class ShapeVariantTests
{
    public static IEnumerable<Func<(string Name, ShapeFamily Family, string[] Rows)>> Variants() =>
    [
        // scythe — the fold survives endpoint shifts: the vacated corner opens an escape route past the bay
        // that a bounding-box read follows, while the fold stays in the terrain itself.
        () => ("scythe standard",             ShapeFamily.Scythe, ["ttvw", "vtvt", "vttt"]),
        () => ("scythe shifted entry",        ShapeFamily.Scythe, ["vvvw", "ttvt", "vttt"]),
        () => ("scythe shifted entry + hub",  ShapeFamily.Scythe, ["hvvvw", "httvt", "hvttt", "hvvvv"]),
        () => ("scythe shifted wool",         ShapeFamily.Scythe, ["ttvv", "vtvw", "vtvt", "vttt"]),
        () => ("scythe shifted wool + hub",   ShapeFamily.Scythe, ["httvv", "hvtvw", "hvtvt", "hvttt", "hvvvv"]),
        () => ("scythe side-docked wool",     ShapeFamily.Scythe, ["ttvv", "vtvtw", "vttt"]),
        () => ("scythe side-docked wool + hub", ShapeFamily.Scythe, ["httvvv", "hvtvtw", "hvtttv", "hvvvvv"]),
        // Z — the staircase stays a Z through the same manipulations (no fold appears)
        () => ("Z wool extends the run",      ShapeFamily.Z, ["ttvvv", "vtttw"]),
        () => ("Z side-docked wool (up)",     ShapeFamily.Z, ["ttvv", "vtvw", "vttt"]),
        () => ("Z side-docked wool (down)",   ShapeFamily.Z, ["ttvv", "vttt", "vvvw"]),
        // the hub docks the entry's mouth interval only — a mass running the shape's whole flank would
        // wrap its own concavity beside the lane, which genuinely folds the component (a scope question,
        // not a classifier one)
        () => ("Z + hub",                     ShapeFamily.Z, ["httvv", "vvttt", "vvvvw"]),
        // donut — the attachment slides from the corner onto the leg; the ring's enclosed void decides
        () => ("donut standard",              ShapeFamily.Donut, ["ttttv", "vtvtv", "vtttw"]),
        () => ("donut moved attachment",      ShapeFamily.Donut, ["vtttv", "ttvtv", "vtttw"]),
        () => ("donut moved attachment + hub", ShapeFamily.Donut, ["hvtttv", "httvtv", "hvtttw"]),
    ];

    [Test]
    [MethodDataSource(nameof(Variants))]
    public async Task Variant_keeps_its_family_at_both_scales((string Name, ShapeFamily Family, string[] Rows) v)
    {
        foreach (var scale in new[] { 1, 2 })
        {
            var (filled, room) = BuildCells(v.Rows, scale);
            var (derived, _) = ShapeClassifier.Classify(filled, room);
            await Assert.That(derived).IsEqualTo(v.Family);
        }
    }

    // grid -> cell sets; h counts as plain terrain (a docked neighbour mass), scaled uniformly
    private static (HashSet<(int, int)> Filled, HashSet<(int, int)> Room) BuildCells(string[] rows, int scale)
    {
        var filled = new HashSet<(int, int)>();
        var room = new HashSet<(int, int)>();
        for (var z = 0; z < rows.Length; z++)
            for (var x = 0; x < rows[z].Length; x++)
            {
                var ch = rows[z][x];
                if (ch is not ('t' or 'w' or 'h')) continue;
                for (var dx = 0; dx < scale; dx++)
                    for (var dz = 0; dz < scale; dz++)
                    {
                        var c = (x * scale + dx, z * scale + dz);
                        filled.Add(c);
                        if (ch == 'w') room.Add(c);
                    }
            }
        return (filled, room);
    }
}
