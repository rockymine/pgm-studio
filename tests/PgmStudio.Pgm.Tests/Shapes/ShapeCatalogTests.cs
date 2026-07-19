using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Tests.Shapes;

/// <summary>
/// The base wool-approach catalog (docs/contracts/map-generation.md §5): each fixture is the doc's t/v/w grid
/// (t = terrain, v = void, w = wool), built as a real plan and classified — the classification the contract
/// asserts, verified against the catalog family. The t/v/w notation is doc-only; it lives here as inline
/// literals that mirror the doc.
/// </summary>
public sealed class ShapeCatalogTests
{
    public static IEnumerable<Func<(string Name, ShapeFamily Family, string[] Rows)>> Catalog() =>
    [
        () => ("isolated",      ShapeFamily.Isolated, ["vv", "wv", "vv"]),
        () => ("i-straight",    ShapeFamily.I,        ["tttw", "vvvv"]),
        () => ("i-sidetuck",    ShapeFamily.I,        ["tttt", "vvvw"]),
        () => ("l-corner-1",    ShapeFamily.L,        ["vw", "vt", "tt"]),
        () => ("l-corner-2",    ShapeFamily.L,        ["tttv", "vvtw"]),
        () => ("clamp",         ShapeFamily.Clamp,    ["tt", "vw", "tt"]),
        () => ("clamp-corner",  ShapeFamily.Clamp,    ["ttw", "vvt", "ttt"]),
        () => ("scythe-1",      ShapeFamily.Scythe,   ["tttv", "tvtw"]),
        () => ("scythe-2",      ShapeFamily.Scythe,   ["tttvv", "tvttw"]),
        () => ("scythe-3-wide", ShapeFamily.Scythe,   ["ttttv", "ttvtw"]),
        () => ("u-flush-1",     ShapeFamily.U,        ["ttv", "vtw", "ttv"]),
        () => ("u-flush-2",     ShapeFamily.U,        ["ttv", "vtv", "ttw"]),
        () => ("h-stub-1",      ShapeFamily.H,        ["ttvv", "vtvv", "tttw"]),
        () => ("h-stub-2",      ShapeFamily.H,        ["ttvw", "vtvt", "tttt"]),
        () => ("h-stub-3",      ShapeFamily.H,        ["vwv", "vtv", "ttt", "tvt"]),
        () => ("donut-1",       ShapeFamily.Donut,    ["ttttv", "vtvtv", "vtttw"]),
        () => ("donut-2",       ShapeFamily.Donut,    ["ttttv", "ttvtv", "vtttw"]),
        () => ("donut-3",       ShapeFamily.Donut,    ["ttttvv", "ttvtvv", "vttttw"]),
    ];

    [Test]
    [MethodDataSource(nameof(Catalog))]
    public async Task Catalog_fixture_classifies_to_its_family((string Name, ShapeFamily Family, string[] Rows) fixture)
    {
        var (_, family, rows) = fixture;
        var plan = BuildPlan(rows);
        var (derived, _) = ShapeClassifier.Classify(plan, "wool");
        await Assert.That(derived).IsEqualTo(family);
    }

    // greedy horizontal-run merge per row for terrain; the wool is a 1×1 wool-room piece
    private static PlanModel BuildPlan(string[] rows)
    {
        var plan = new PlanModel
        {
            Meta = new PlanMeta { Name = "catalog" },
            Globals = new PlanGlobals { Cell = 5, Symmetry = "none", MaxPlayers = 12, Surface = 9, Headroom = 11 },
        };
        (int, int)? wool = null;
        int pid = 0;
        for (int z = 0; z < rows.Length; z++)
        {
            int x = 0;
            while (x < rows[z].Length)
            {
                var ch = rows[z][x];
                if (ch == 'w') wool = (x, z);
                if (ch != 't') { x++; continue; }
                int c0 = x; while (x < rows[z].Length && rows[z][x] == 't') x++;
                plan.Pieces.Add(new PlanPiece { Id = $"t{++pid}", Role = PlanRoles.Piece, Rect = [c0, z, x - c0, 1] });
            }
        }
        var w = wool!.Value;
        plan.Pieces.Add(new PlanPiece { Id = "wool", Role = PlanRoles.WoolRoom, Rect = [w.Item1, w.Item2, 1, 1] });
        plan.Placements.Wools.Add(new WoolPlacement { Piece = "wool", At = [0, 0] });
        return plan;
    }
}
