using PgmStudio.Geom;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Dressing;
using PgmStudio.Minecraft.Stamping;
using PgmStudio.Vocabulary;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// Which storey a prop rests on. Everything the export puts down resolved its Y from one whole-board surface
/// grid, so on a stacked board a prop stated for a gallery floor landed on the deck over it — not declined,
/// not warned, somewhere else. A prop names its layer now, and naming none keeps the top surface.
/// </summary>
public sealed class PropLayerTests
{
    /// <summary>A yard under a deck: every cell carries ground at y6 on `yard` and at y26 on `deck`, which is
    /// the shape a prop has to be able to choose between.</summary>
    private static BuiltTerrain Stacked() => TerrainBuilder.Build(
    [
        .. Cells().Select(cell => new ColumnSegment(cell.X, cell.Z, 0, 6, "yard")),
        .. Cells().Select(cell => new ColumnSegment(cell.X, cell.Z, 20, 26, "deck")),
    ]);

    private static IEnumerable<(int X, int Z)> Cells()
    {
        for (var x = 0; x < 24; x++)
        for (var z = 0; z < 24; z++)
            yield return (x, z);
    }

    private static DressingContext Context(BuiltTerrain terrain, params PlacedProp[] props) =>
        new(terrain.SurfaceTop, props, (_, _) => null, DressingSymmetry.None, null, null,
            terrain.SurfaceByLayer);

    /// <summary><b>A made thing is not the ground under it, and <c>SurfaceFor</c> is where that is answered.</b>
    /// Everything that RESTS on a board asks for the surface at a cell and names no layer — a room's floor, a
    /// goal's box and the buried plate beneath it, a wall, a build-region marker, the world spawn — and the
    /// fallback was the whole-board top. A cloud drawn at y78 over a car park is the top of every column
    /// under it, so the goal read the cloud and was stamped eighty-three blocks up, over a build ceiling of
    /// 68 that it had not itself raised. <c>SurfaceTop</c> still answers what it always answered; it is just
    /// nobody's question any more.</summary>
    [Test]
    public async Task A_placement_naming_no_layer_stands_on_the_ground_and_never_on_a_made_thing()
    {
        var terrain = TerrainBuilder.Build(
        [
            .. Cells().Select(cell => new ColumnSegment(cell.X, cell.Z, 0, 6, "yard")),
            .. Cells().Select(cell => new ColumnSegment(cell.X, cell.Z, 78, 86, "cloud")),
        ], new HashSet<string> { "cloud" });

        await Assert.That(terrain.SurfaceTop[(8, 8)]).IsEqualTo(86);       // the highest thing standing
        await Assert.That(terrain.Ground[(8, 8)]).IsEqualTo(6);           // and the ground under it
        await Assert.That(terrain.SurfaceFor(null)[(8, 8)]).IsEqualTo(6);
        // Naming the layer still reaches it: a monument authored onto a deck sits on the deck.
        await Assert.That(terrain.SurfaceFor("cloud")[(8, 8)]).IsEqualTo(86);
        // A board that names no made thing reads exactly as it did.
        await Assert.That(Stacked().SurfaceFor(null)[(8, 8)]).IsEqualTo(26);
    }

    [Test]
    public async Task A_prop_naming_no_layer_rests_on_the_top_surface()
    {
        var terrain = Stacked();
        var context = Context(terrain, new BoulderProp { Id = "b", X = 8, Z = 8, Seed = 3 });

        await Assert.That(context.GroundFor(context.Props[0])[(8, 8)]).IsEqualTo(26);
    }

    /// <summary>The whole point: a prop stated for the lower storey reads the lower storey's ground, twenty
    /// blocks under the deck that would otherwise have taken it.</summary>
    [Test]
    public async Task A_prop_naming_a_layer_rests_on_that_layer()
    {
        var terrain = Stacked();
        var context = Context(terrain, new BoulderProp { Id = "b", X = 8, Z = 8, Seed = 3, Layer = "yard" });

        await Assert.That(context.GroundFor(context.Props[0])[(8, 8)]).IsEqualTo(6);
    }

    /// <summary>Naming a storey the board does not have is not the same as naming none. Falling back to the
    /// top surface would seat the prop on exactly the storey the author said they did not mean, so it is
    /// declined instead — the map builds and that one prop is not in it.</summary>
    [Test]
    public async Task A_prop_naming_a_layer_the_board_lacks_is_declined()
    {
        var terrain = Stacked();
        var world = terrain.World;
        var placed = Decorator.Decorate(world,
            Context(terrain, new BoulderProp { Id = "b", X = 8, Z = 8, Seed = 3, Layer = "cellar" }));

        var decline = placed.Declines.Single(finding => finding.Rule == DressingRules.NoSuchLayer);
        await Assert.That(decline.Severity).IsEqualTo(Severity.Decline);
        await Assert.That(decline.SubjectIds).IsEquivalentTo(new[] { "b" });
        await Assert.That(decline.Message).Contains("cellar");
    }

    /// <summary>A board with one layer answers the same ground either way, so every pass can ask without a
    /// stacked board being a special case.</summary>
    [Test]
    public async Task A_flat_board_answers_the_same_ground_either_way()
    {
        var terrain = TerrainBuilder.Build(
            [.. Cells().Select(cell => new ColumnSegment(cell.X, cell.Z, 0, 6, "ground"))]);
        var context = Context(terrain,
            new BoulderProp { Id = "a", X = 4, Z = 4, Seed = 1 },
            new BoulderProp { Id = "b", X = 4, Z = 4, Seed = 1, Layer = "ground" });

        await Assert.That(context.GroundFor(context.Props[0])[(4, 4)]).IsEqualTo(6);
        await Assert.That(context.GroundFor(context.Props[1])[(4, 4)]).IsEqualTo(6);
    }

    /// <summary>The terrain's own resolver answers the same question for a stamped thing — a spawn, a room,
    /// an objective — so the two readers of a placement's storey cannot disagree about where it is.</summary>
    [Test]
    public async Task The_terrain_resolves_a_named_layer_for_a_stamp_too()
    {
        var terrain = Stacked();

        await Assert.That(terrain.SurfaceFor(null)[(8, 8)]).IsEqualTo(26);
        await Assert.That(terrain.SurfaceFor("yard")[(8, 8)]).IsEqualTo(6);
        await Assert.That(terrain.SurfaceFor("deck")[(8, 8)]).IsEqualTo(26);
        await Assert.That(terrain.Knows("cellar")).IsFalse();
        await Assert.That(terrain.Knows(null)).IsTrue();
    }
}
