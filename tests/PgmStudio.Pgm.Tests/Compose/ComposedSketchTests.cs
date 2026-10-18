using PgmStudio.Geom;
using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Pgm.Tests.Compose;

/// <summary>
/// A composed board is handed over as a sketch: every box of the team unit is a shape of its own, named for the
/// box, and none is named for a piece; the unit's shapes are one mirrored group; and the boxes ride beside it at
/// every orbit image, each image the box turned by the board's symmetry.
/// </summary>
public sealed class ComposedSketchTests
{
    private static IEnumerable<(int Players, ulong Seed)> Sweep() =>
        from players in new[] { 8, 16, 24, 32 } from seed in Enumerable.Range(0, 6) select (players, (ulong)seed);

    [Test]
    public async Task Every_box_is_a_shape_of_its_own_in_the_one_group()
    {
        foreach (var (players, seed) in Sweep())
        {
            var stages = Composer.ComposeStages(new ComposeRequest(players, seed: seed));
            var sketch = ComposedSketch.Of(stages);
            var ground = SketchLayout.Stack(sketch.Layout)[0];
            var added = ground.Shapes.Where(shape => shape.Operation == "add").Select(shape => shape.Id).ToList();
            var boxes = BoxLayout.Of(stages.Unit).Boxes.Select(box => box.Id).ToList();

            foreach (var box in boxes)
                await Assert.That(added.Any(id => id.StartsWith(box + "-", StringComparison.Ordinal))).IsTrue()
                    .Because($"box {box} is a shape @ {players}p seed {seed}");
            await Assert.That(stages.Unit.Pieces.Any(piece => added.Any(id => id.StartsWith(piece.Id + "-", StringComparison.Ordinal))
                                                              && !boxes.Contains(piece.Id))).IsFalse()
                .Because($"no shape is named for a piece @ {players}p seed {seed}");
            var team = ground.Groups.Single(group => group.Id == "team").ShapeIds;
            await Assert.That(added.Where(id => boxes.Any(box => id.StartsWith(box + "-", StringComparison.Ordinal))).All(team.Contains))
                .IsTrue().Because($"the unit's shapes are one group @ {players}p seed {seed}");
        }
    }

    [Test]
    public async Task The_boxes_ride_beside_at_every_image()
    {
        var stages = Composer.ComposeStages(new ComposeRequest(16, seed: 3));
        var sketch = ComposedSketch.Of(stages);
        var cell = stages.Plan.Globals.Cell;
        var mode = stages.Plan.Globals.Symmetry;

        foreach (var box in BoxLayout.Of(stages.Unit).Boxes)
        {
            var images = sketch.Boxes.Where(image => image.Id == box.Id).OrderBy(image => image.Image).ToList();
            await Assert.That(images.Count).IsEqualTo(Symmetry.Order(mode));
            await Assert.That(images[0].Rect).IsEqualTo(new BlockRect(box.Rect.X * cell, box.Rect.Z * cell,
                (box.Rect.X + box.Rect.Width) * cell, (box.Rect.Z + box.Rect.Height) * cell));
            var (minX, minZ, maxX, maxZ) = Symmetry.Rect(images[0].Rect.MinX, images[0].Rect.MinZ,
                images[0].Rect.MaxX, images[0].Rect.MaxZ, mode, 0, 0, 1);
            await Assert.That(images[1].Rect).IsEqualTo(new BlockRect((int)minX, (int)minZ, (int)maxX, (int)maxZ));
        }
    }

    [Test]
    public async Task A_label_names_the_board_for_the_players_asked()
    {
        var sketch = ComposedSketch.Of(Composer.ComposeStages(new ComposeRequest(8, seed: 4)));
        var labelled = Composer.Label(sketch.Intent, 11, 2, 4);
        await Assert.That(labelled.MaxPlayers).IsEqualTo(11);
        await Assert.That(labelled.Meta!.Name).IsEqualTo("Composed p11 t2 #4");
    }
}
