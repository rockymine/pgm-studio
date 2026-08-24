using PgmStudio.Api.Services;
using PgmStudio.Geom;
using PgmStudio.Minecraft.Anvil;

namespace PgmStudio.Api.Tests;

/// <summary>
/// Which layer drew each run of the 3-D preview's payload. The preview meshed a finished world with no idea
/// what any of it came from, so a viewer could not take a deck off and look under it — the runs carry their
/// layer now, and the client filters what it already holds rather than asking for the board again.
/// </summary>
public sealed class WorldColumnLayerTests
{
    /// <summary>A world with a yard at y0–4 and a deck at y20–24 over the same column, plus a block at y30
    /// standing on neither — a tree, as far as the rasterizer's spans are concerned.</summary>
    private static VoxelWorld Stacked()
    {
        var world = new VoxelWorld();
        for (var y = 0; y <= 4; y++) world.SetBlock(0, y, 0, 1, 0);
        for (var y = 20; y <= 24; y++) world.SetBlock(0, y, 0, 1, 0);
        world.SetBlock(0, 30, 0, 17, 0);
        return world;
    }

    private static List<ColumnSegment> Spans() =>
    [
        new(0, 0, 0, 4, "yard"),
        new(0, 0, 20, 24, "deck"),
    ];

    /// <summary>The runs come back top first, so the payload reads tree, deck, yard — and only the two the
    /// rasterizer made are attributed.</summary>
    [Test]
    public async Task Each_run_names_the_layer_that_drew_it_and_a_structure_names_none()
    {
        var payload = WorldColumnPayload.Of(Stacked(), Spans());

        await Assert.That(payload.Layers).IsEquivalentTo(new[] { "yard", "deck" });
        var deck = payload.Layers!.ToList().IndexOf("deck");
        var yard = payload.Layers!.ToList().IndexOf("yard");

        // [x, z, runCount, (yTop, yBottom, colour, layer) × 3]
        var cols = payload.Cols;
        await Assert.That(cols[2]).IsEqualTo(3);
        await Assert.That((cols[3], cols[4], cols[6])).IsEqualTo((30, 30, -1));      // the tree
        await Assert.That((cols[7], cols[8], cols[10])).IsEqualTo((24, 20, deck));
        await Assert.That((cols[11], cols[12], cols[14])).IsEqualTo((4, 0, yard));
    }

    /// <summary>A world nobody rasterized in layers answers no layers and attributes nothing, which is what
    /// a plan-built board and an imported world both are.</summary>
    [Test]
    public async Task A_world_with_no_spans_attributes_nothing()
    {
        var payload = WorldColumnPayload.Of(Stacked());

        await Assert.That(payload.Layers).IsEmpty();
        await Assert.That(payload.Cols[6]).IsEqualTo(-1);
        await Assert.That(payload.Cols[10]).IsEqualTo(-1);
        await Assert.That(payload.Cols[14]).IsEqualTo(-1);
    }
}
