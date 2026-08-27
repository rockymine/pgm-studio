using PgmStudio.Geom;
using PgmStudio.Domain;
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

    /// <summary>A span is half-open — <c>[YFloor, YTop)</c> — so the yard's five courses are 0..4 and the
    /// deck's five are 20..24.</summary>
    private static List<ColumnSegment> Spans() =>
    [
        new(0, 0, 0, 5, "yard"),
        new(0, 0, 20, 25, "deck"),
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
        var yard = WorldStorey.Of(Board(), Spans(), "yard", new WorldProvenance())!.World;
        await Assert.That(Solid(yard)).IsEquivalentTo(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 });
    }

    /// <summary>The topmost storey runs to the world ceiling, so the mast on the deck is in it.</summary>
    [Test]
    public async Task The_topmost_storey_runs_to_the_ceiling()
    {
        var deck = WorldStorey.Of(Board(), Spans(), "deck", new WorldProvenance())!.World;
        await Assert.That(Solid(deck)).IsEquivalentTo(new[] { 20, 21, 22, 23, 24, 25 });
    }

    /// <summary>Naming no layer is the whole world, which is what every read answered before and still does.
    /// </summary>
    [Test]
    public async Task Naming_no_layer_is_the_whole_world()
    {
        var world = Board();
        await Assert.That(WorldStorey.Of(world, Spans(), null, new WorldProvenance())!.World).IsSameReferenceAs(world);
        await Assert.That(WorldStorey.Of(world, Spans(), "", new WorldProvenance())!.World).IsSameReferenceAs(world);
    }

    /// <summary>A layer the board does not have answers null, so the route refuses by naming the ones it does
    /// rather than drawing an empty picture.</summary>
    [Test]
    public async Task A_layer_the_board_lacks_answers_nothing_and_the_names_say_what_it_has()
    {
        await Assert.That(WorldStorey.Of(Board(), Spans(), "cellar", new WorldProvenance())).IsNull();
        await Assert.That(WorldStorey.Of(Board(), null, "yard", new WorldProvenance())).IsNull();
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
        spans.Add(new ColumnSegment(9, 9, 20, 25, "deck"));

        var yard = WorldStorey.Of(world, spans, "yard", new WorldProvenance())!.World;
        await Assert.That(yard.GetBlock(9, 22, 9).Id).IsEqualTo(0);
        await Assert.That(yard.GetBlock(0, 2, 0).Id).IsEqualTo(1);
    }

    /// <summary>The record is narrowed with the world. A claim carries no course, so it describes the
    /// column's topmost block: the hut's claim belongs to the yard storey, which shows y8, and must not be
    /// read onto the deck storey — nor may the deck's own claim be read down onto the yard.</summary>
    [Test]
    public async Task A_storey_reads_the_record_for_the_courses_it_shows()
    {
        var record = new WorldProvenance();
        record.Claim(0, 0, ProvenancePass.Structure, new StampId("mast", "m1", 0));   // the world's top, y25

        var deck = WorldStorey.Of(Board(), Spans(), "deck", record)!;
        var yard = WorldStorey.Of(Board(), Spans(), "yard", record)!;

        // The deck storey shows the column's own top, so the claim is what it was recorded as.
        await Assert.That(deck.Provenance.PassAt(0, 0)).IsEqualTo(ProvenancePass.Structure);
        await Assert.That(deck.Provenance.OwnerAt(0, 0)).IsEqualTo(new StampId("mast", "m1", 0));
        // The yard storey tops out at the hut on y8, which the mast's claim never described. The hut stands
        // over the layer's drawn span, so nothing is attributed to it rather than the mast being.
        await Assert.That(yard.Provenance.PassAt(0, 0)).IsNull();
    }

    /// <summary>Where the course a storey shows is inside the layer's own drawn span, it is the rasterizer's
    /// terrain whatever the record's last claim was — which is what keeps a built-looking floor from reading
    /// as a building under every storey above it.</summary>
    [Test]
    public async Task A_course_inside_the_layer_s_own_span_reads_as_ground()
    {
        var world = Board();
        for (var y = 5; y <= 8; y++) world.SetBlock(0, y, 0, 0, 0);   // no hut: the yard's own top is y4
        var record = new WorldProvenance();
        record.Claim(0, 0, ProvenancePass.Structure, new StampId("mast", "m1", 0));

        var yard = WorldStorey.Of(world, Spans(), "yard", record)!;

        await Assert.That(yard.Provenance.PassAt(0, 0)).IsEqualTo(ProvenancePass.Ground);
        await Assert.That(yard.Provenance.OwnerAt(0, 0)).IsNull();
    }

    /// <summary>A storey whose layer meets the one over it with no gap. A span is half-open, so the layer
    /// above starts <b>at</b> this one's <c>YTop</c> rather than past it: reading the two ends as inclusive
    /// finds nothing above and hands the storey the rest of the world, which is a picture of the top storey
    /// wearing the lower one's name.</summary>
    [Test]
    public async Task A_storey_that_abuts_the_layer_over_it_stops_at_its_own_top()
    {
        var world = new VoxelWorld();
        for (var y = 1; y <= 17; y++) world.SetBlock(0, y, 0, 1, 0);      // the rock
        for (var y = 18; y <= 27; y++) world.SetBlock(0, y, 0, 24, 0);    // the landmass on it, no gap
        world.SetBlock(0, 28, 0, 17, 0);                                  // a tree on the landmass
        List<ColumnSegment> spans = [new(0, 0, 1, 18, "under"), new(0, 0, 18, 28, "ground")];

        var under = WorldStorey.Of(world, spans, "under", new WorldProvenance())!;
        var ground = WorldStorey.Of(world, spans, "ground", new WorldProvenance())!;

        await Assert.That(Solid(under.World)).IsEquivalentTo(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17 });
        await Assert.That(Solid(ground.World)).IsEquivalentTo(new[] { 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28 });
    }
}
