using PgmStudio.Geom;
using PgmStudio.Minecraft.Anvil;

namespace PgmStudio.Api.Tests;

/// <summary>
/// One storey of a stacked board, cut out as a world of its own. The reads that project a column to one cell
/// draw the highest thing in it, so on a stacked board they drew the topmost storey and nothing else — and
/// `ymax`, the only cut they had, separates two storeys only where the upper one happens to be flat.
/// </summary>
public sealed class WorldStoreyTests
{
    /// <summary>A yard at y0–4 with a hut standing on it at y5–8, a deck at y20–24, and a mast on the deck at
    /// y25. One column, so what each storey keeps is exactly what the window says.</summary>
    private static VoxelWorld Board()
    {
        var world = new VoxelWorld();
        for (var y = 0; y <= 4; y++) world.SetBlock(0, y, 0, 1, 0);      // yard
        for (var y = 5; y <= 8; y++) world.SetBlock(0, y, 0, 5, 0);      // a hut standing on it
        for (var y = 20; y <= 24; y++) world.SetBlock(0, y, 0, 1, 0);    // deck
        world.SetBlock(0, 25, 0, 17, 0);                                 // a mast on the deck
        return world;
    }

    private static List<ColumnSegment> Spans() =>
    [
        new(0, 0, 0, 4, "yard"),
        new(0, 0, 20, 24, "deck"),
    ];

    private static List<int> Solid(VoxelWorld world)
    {
        var ys = new List<int>();
        for (var y = 0; y < VoxelWorld.MaxHeight; y++)
            if (world.GetBlock(0, y, 0).Id != 0) ys.Add(y);
        return ys;
    }

    /// <summary>The whole point: the lower storey keeps its own ground <b>and what stands on it</b> — a
    /// window ending at the block below the deck — so the picture is a yard with a hut on it rather than a
    /// bare slab.</summary>
    [Test]
    public async Task A_storey_keeps_its_ground_and_what_stands_on_it_up_to_the_next_layer()
    {
        var yard = WorldStorey.Of(Board(), Spans(), "yard")!;
        await Assert.That(Solid(yard)).IsEquivalentTo(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 });
    }

    /// <summary>The topmost storey runs to the world ceiling, so the mast on the deck is in it.</summary>
    [Test]
    public async Task The_topmost_storey_runs_to_the_ceiling()
    {
        var deck = WorldStorey.Of(Board(), Spans(), "deck")!;
        await Assert.That(Solid(deck)).IsEquivalentTo(new[] { 20, 21, 22, 23, 24, 25 });
    }

    /// <summary>Naming no layer is the whole world, which is what every read answered before and still does.
    /// </summary>
    [Test]
    public async Task Naming_no_layer_is_the_whole_world()
    {
        var world = Board();
        await Assert.That(WorldStorey.Of(world, Spans(), null)).IsSameReferenceAs(world);
        await Assert.That(WorldStorey.Of(world, Spans(), "")).IsSameReferenceAs(world);
    }

    /// <summary>A layer the board does not have answers null, so the route refuses by naming the ones it does
    /// rather than drawing an empty picture.</summary>
    [Test]
    public async Task A_layer_the_board_lacks_answers_nothing_and_the_names_say_what_it_has()
    {
        await Assert.That(WorldStorey.Of(Board(), Spans(), "cellar")).IsNull();
        await Assert.That(WorldStorey.Of(Board(), null, "yard")).IsNull();
        await Assert.That(WorldStorey.Names(Spans())).IsEquivalentTo(new[] { "yard", "deck" });
        await Assert.That(WorldStorey.Names(null)).IsEmpty();
    }

    /// <summary>A column the layer never drew contributes nothing, which is what makes a gallery under a deck
    /// read as its own footprint rather than as the whole board.</summary>
    [Test]
    public async Task A_column_the_layer_never_drew_is_not_in_its_storey()
    {
        var world = Board();
        for (var y = 20; y <= 24; y++) world.SetBlock(9, y, 9, 1, 0);      // deck reaches further than the yard
        var spans = Spans();
        spans.Add(new ColumnSegment(9, 9, 20, 24, "deck"));

        var yard = WorldStorey.Of(world, spans, "yard")!;
        await Assert.That(yard.GetBlock(9, 22, 9).Id).IsEqualTo(0);
        await Assert.That(yard.GetBlock(0, 2, 0).Id).IsEqualTo(1);
    }
}
