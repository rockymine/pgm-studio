using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// The presentation step that makes a generated layout editable: the board is recentred on the symmetry
/// origin, a square framing bbox is set, and dense lane hulls are thinned to a handful of draggable vertices.
/// </summary>
public sealed class SketchLayoutPrepTests
{
    [Test]
    public async Task ForEditor_recentres_on_the_origin_and_frames_every_shape()
    {
        // HLayout's board is 60×90 → its symmetry centre starts at (30, 45).
        var layout = SketchLayoutPrep.ForEditor(LaneSketchGenerator.HLayout().Layout);

        await Assert.That(layout.Setup!.Center!.Cx).IsEqualTo(0);
        await Assert.That(layout.Setup.Center.Cz).IsEqualTo(0);

        // square, origin-centred bbox
        var bbox = layout.Setup.Bbox!;
        await Assert.That(bbox.MaxX).IsEqualTo(-bbox.MinX);
        await Assert.That(bbox.MaxZ).IsEqualTo(-bbox.MinZ);
        await Assert.That(bbox.MaxX).IsEqualTo(bbox.MaxZ);

        // every recentred vertex sits inside the frame
        var half = bbox.MaxX;
        foreach (var shape in layout.Layout!.Shapes)
            foreach (var v in shape.Vertices!)
            {
                await Assert.That(Math.Abs(v[0]) <= half).IsTrue();
                await Assert.That(Math.Abs(v[1]) <= half).IsTrue();
            }
    }

    [Test]
    public async Task ForEditor_thins_dense_lane_polygons()
    {
        // Organic lanes are the dense case — jittered ribbon hulls of many vertices.
        var layout = LaneSketchGenerator.Build(new LaneLayoutOptions { Archetype = LaneArchetype.Organic, Seed = 3 }).Layout;
        var before = layout.Layout!.Shapes.Max(s => s.Vertices!.Length);

        SketchLayoutPrep.ForEditor(layout);
        var after = layout.Layout.Shapes.Max(s => s.Vertices!.Length);

        await Assert.That(before > 12).IsTrue();   // the generator really did emit dense hulls
        await Assert.That(after < before).IsTrue(); // simplified down
    }

    [Test]
    public async Task ForEditor_keeps_the_layout_rasterizable()
    {
        // Recentre + simplify must not break the round-trip into cells (the Finish path).
        var layout = SketchLayoutPrep.ForEditor(LaneSketchGenerator.HLayout().Layout);
        var cells = SketchRasterizer.Rasterize(layout.ToJson());
        await Assert.That(cells.Count).IsGreaterThan(0);
    }
}
