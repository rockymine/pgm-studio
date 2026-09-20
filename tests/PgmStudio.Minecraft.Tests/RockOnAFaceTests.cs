using PgmStudio.Geom.Algorithms;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Dressing;
using PgmStudio.Minecraft.Palette;
using PgmStudio.Vocabulary;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// DR-STEEP: a boulder standing on a face rather than on ground. The slope is read off the surface map with
/// the same gradient the paint is banded by, and the angle it is judged against is the one the theme painting
/// that cell states — handed to the pass by whoever resolved the paint, so a fixture that resolves none is not
/// asked at all.
/// </summary>
public sealed class RockOnAFaceTests
{
    /// <summary>A hillside falling one block per cell across <paramref name="fall"/> — about 45° where it
    /// falls, level on the shelf either side of it.</summary>
    private static (VoxelWorld World, Dictionary<(int X, int Z), int> Top) Hillside(int fall)
    {
        var world = new VoxelWorld();
        var top = new Dictionary<(int X, int Z), int>();
        for (var z = 0; z < 60; z++)
        for (var x = 0; x < 60; x++)
        {
            var height = 8 + Math.Clamp(fall - Math.Max(0, x - 20), 0, fall);
            for (var y = 0; y < height; y++) world.SetBlock(x, y, z, Blocks.Stone);
            top[(x, z)] = height;
        }
        return (world, top);
    }

    private static DressingContext Context(
        Dictionary<(int X, int Z), int> top, IReadOnlyList<PlacedProp> props, int? cliff)
        => new(top, props, (_, _) => null, DressingSymmetry.None,
               CliffAngleAt: cliff is { } angle ? (_, _, _) => angle : null);

    private static Finding? Steep(DressingPlacement placed) =>
        placed.Declined?.FirstOrDefault(finding => finding.Rule == DressingRules.RockOnAFace);

    [Test]
    public async Task A_rock_on_the_face_is_named_with_the_angle_measured_and_the_angle_the_theme_calls_a_face()
    {
        var (world, top) = Hillside(fall: 30);
        // Mid-fall, where the ground drops a block a cell.
        var placed = Decorator.Decorate(world, Context(top,
            [new BoulderProp { Id = "crag", X = 30, Z = 30, Seed = 3 }], cliff: 30));

        var finding = Steep(placed);
        await Assert.That(finding).IsNotNull();
        await Assert.That(finding!.Severity).IsEqualTo(Severity.Complaint);
        await Assert.That(finding.Message).Contains("boulder 'crag' stands at (30, 30)");
        await Assert.That(finding.Message).Contains("calls the ground a face from 30°");
        await Assert.That(finding.SubjectIds).IsEquivalentTo(new[] { "crag" });
    }

    /// <summary>A complaint, not a decline: a rock on a hillside reads badly and builds fine, so the world
    /// keeps it and the author is told where it is.</summary>
    [Test]
    public async Task The_rock_is_still_in_the_world()
    {
        var (world, top) = Hillside(fall: 30);
        var placed = Decorator.Decorate(world, Context(top,
            [new BoulderProp { Id = "crag", X = 30, Z = 30, Seed = 3 }], cliff: 30));

        await Assert.That(Steep(placed)).IsNotNull();
        await Assert.That(placed.Boulders).IsEqualTo(1);
    }

    /// <summary>The shelf at the top of the fall is level ground and holds a rock.</summary>
    [Test]
    public async Task A_rock_on_the_flat_says_nothing()
    {
        var (world, top) = Hillside(fall: 30);
        var placed = Decorator.Decorate(world, Context(top,
            [new BoulderProp { Id = "erratic", X = 8, Z = 30, Seed = 3 }], cliff: 30));

        await Assert.That(Steep(placed)).IsNull();
        await Assert.That(placed.Boulders).IsEqualTo(1);
    }

    /// <summary>The board states the angle, so the same ground is a face or is not depending on where the
    /// theme painting it cut its bands. A hillside a theme still calls meadow holds a rock.</summary>
    [Test]
    public async Task The_same_hillside_is_judged_by_the_angle_its_own_theme_states()
    {
        var (world, top) = Hillside(fall: 30);
        BoulderProp rock() => new() { Id = "crag", X = 30, Z = 30, Seed = 3 };

        await Assert.That(Steep(Decorator.Decorate(world, Context(top, [rock()], cliff: 30)))).IsNotNull();

        var (second, secondTop) = Hillside(fall: 30);
        await Assert.That(Steep(Decorator.Decorate(second, Context(secondTop, [rock()], cliff: 80)))).IsNull();
    }

    /// <summary>A caller that resolved no paint — a preview, a fixture — states no angle, and ground with no
    /// cliff stated has none to be on.</summary>
    [Test]
    public async Task A_board_whose_paint_nobody_resolved_is_not_asked()
    {
        var (world, top) = Hillside(fall: 30);
        var placed = Decorator.Decorate(world, Context(top,
            [new BoulderProp { Id = "crag", X = 30, Z = 30, Seed = 3 }], cliff: null));

        await Assert.That(Steep(placed)).IsNull();
    }

    /// <summary>The reading is the gradient the paint itself is banded by, so a rule and a band cannot
    /// disagree about how steep one cell is.</summary>
    [Test]
    public async Task The_angle_is_the_one_the_paint_is_banded_by()
    {
        var (_, top) = Hillside(fall: 30);

        await Assert.That(SurfaceGradient.Degrees(top, 30, 30)).IsGreaterThanOrEqualTo(30);
        await Assert.That(SurfaceGradient.Degrees(top, 8, 30)).IsLessThan(SurfaceGradient.Level);
    }
}
