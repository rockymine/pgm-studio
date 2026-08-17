using System.Text.Json;
using PgmStudio.Export;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Export.Tests;

/// <summary>
/// Where a goal's two orbit images land relative to the ground they stand on. A board whose extent is even
/// turns about a <b>cell boundary</b>, so the image of block <c>x</c> is <c>-1-x</c> and a pair of images
/// straddles that boundary. Reflecting the anchor as a position instead puts the pair one block apart from
/// its own mirror — the terrain keeps the boundary and the stamp does not, which is a goal a block off the
/// ground it is measured against and invisible to every test that only counts blocks.
/// </summary>
public sealed class MirrorCentreWorldTests
{
    private const string Destroyables = """
        {
          "plan": 1,
          "meta": { "name": "Mirror Probe" },
          "globals": { "cell": 5, "symmetry": "rot_180", "surface": 9, "headroom": 11 },
          "pieces": [
            { "id": "bar-w", "role": "piece", "rect": [1, -2, 4, 4], "surface": 12 }
          ],
          "placements": {
            "destroyables": [ { "piece": "bar-w", "at": [2, 2] } ]
          }
        }
        """;

    private const string Cores = """
        {
          "plan": 1,
          "meta": { "name": "Mirror Probe" },
          "globals": { "cell": 5, "symmetry": "rot_180", "surface": 9, "headroom": 11 },
          "pieces": [
            { "id": "bar-w", "role": "piece", "rect": [1, -2, 4, 4], "surface": 12 }
          ],
          "placements": {
            "cores": [ { "piece": "bar-w", "at": [2, 2] } ]
          }
        }
        """;

    private static SketchWorld Build(string json)
    {
        var (layout, intent) = PlanCompiler.Compile(PlanModel.Parse(json)!);
        return SketchWorldBuilder.Build(JsonSerializer.Serialize(layout, SketchLayout.Json), intent);
    }

    /// <summary>Two inclusive spans are images of each other about the boundary at 0 when each one's low end
    /// pairs with the other's high end — <c>a.min + b.max == -1</c> both ways round.</summary>
    private static bool Mirrored(int aMin, int aMax, int bMin, int bMax)
        => aMin + bMax == -1 && aMax + bMin == -1;

    [Test]
    public async Task Two_destroyables_straddle_the_boundary_the_board_turns_about()
    {
        var built = Build(Destroyables);
        var goals = built.ResolvedIntent.Destroyables!;
        await Assert.That(goals.Count).IsEqualTo(2);

        var (first, second) = (goals[0].Box!.Value, goals[1].Box!.Value);
        await Assert.That(Mirrored(first.MinX, first.MaxX, second.MinX, second.MaxX)).IsTrue();
        await Assert.That(Mirrored(first.MinZ, first.MaxZ, second.MinZ, second.MaxZ)).IsTrue();
    }

    [Test]
    public async Task Two_cores_straddle_the_boundary_the_board_turns_about()
    {
        var built = Build(Cores);
        var goals = built.ResolvedIntent.Cores!;
        await Assert.That(goals.Count).IsEqualTo(2);

        var (first, second) = (goals[0].Box!.Value, goals[1].Box!.Value);
        await Assert.That(Mirrored(first.MinX, first.MaxX, second.MinX, second.MaxX)).IsTrue();
        await Assert.That(Mirrored(first.MinZ, first.MaxZ, second.MinZ, second.MaxZ)).IsTrue();
    }

    [Test]
    public async Task The_blocks_a_goal_stamps_stand_at_the_image_of_the_other_goal_s()
    {
        // The boxes agreeing is not the same claim as the world agreeing, so this reads the built voxels:
        // every column one goal occupies must be occupied, to the same heights, at its mirror.
        var built = Build(Destroyables);
        var goals = built.ResolvedIntent.Destroyables!;
        var box = goals[0].Box!.Value;

        var columns = WorldColumns.Of(built.World)
            .ToDictionary(column => (column.X, column.Z), column => column.Runs);

        var compared = 0;
        for (var z = box.MinZ; z <= box.MaxZ; z++)
        for (var x = box.MinX; x <= box.MaxX; x++)
        {
            var here = columns.GetValueOrDefault((x, z));
            var image = columns.GetValueOrDefault((-1 - x, -1 - z));
            await Assert.That(image).IsNotNull();
            await Assert.That(image!.Select(run => (run.YTop, run.YBottom)))
                        .IsEquivalentTo(here!.Select(run => (run.YTop, run.YBottom)));
            compared++;
        }

        await Assert.That(compared).IsGreaterThan(0);
    }
}
