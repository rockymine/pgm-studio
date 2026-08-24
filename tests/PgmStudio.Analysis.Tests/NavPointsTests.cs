using PgmStudio.Analysis.Playability;
using PgmStudio.Geom;

namespace PgmStudio.Analysis.Tests;

using Dict = Dictionary<string, object?>;

/// <summary>
/// The places a match is played between. The invariant every playability read rests on is that a wool, a
/// destroyable and a core are always among them — a goal missing here is a journey nobody measures, and the
/// read answers over the spawns alone while calling the rest of the board dead.
/// </summary>
public sealed class NavPointsTests
{
    private static readonly (double, double, double, double) Bounds = (-64, -64, 64, 64);

    private static Dict Xz(double x, double z) => new() { ["x"] = x, ["z"] = z };

    private static Dict Rect(double minX, double minZ, double maxX, double maxZ) => new()
    {
        ["type"] = "rectangle",
        ["min"] = Xz(minX, minZ),
        ["max"] = Xz(maxX, maxZ),
        ["bounds_2d"] = new Dict { ["min"] = Xz(minX, minZ), ["max"] = Xz(maxX, maxZ) },
    };

    private static Dict Cuboid(double minX, double minY, double minZ, double maxX, double maxY, double maxZ)
        => new()
        {
            ["type"] = "cuboid",
            ["min"] = new Dict { ["x"] = minX, ["y"] = minY, ["z"] = minZ },
            ["max"] = new Dict { ["x"] = maxX, ["y"] = maxY, ["z"] = maxZ },
            ["bounds_2d"] = new Dict { ["min"] = Xz(minX, minZ), ["max"] = Xz(maxX, maxZ) },
        };

    /// <summary>Which storey a point is on. A spawn box on a deck states its floor, and a point that carries
    /// it is the difference between walking the deck and walking the gallery twenty blocks under it.</summary>
    [Test]
    public async Task A_point_carries_the_storey_its_region_states()
    {
        var data = new Dict
        {
            ["regions"] = new Dict
            {
                ["deck-spawn"] = Cuboid(-10, 26, -10, 0, 30, 0),
                ["yard-spawn"] = Rect(4, 4, 8, 8),
                ["cairn"] = Cuboid(20, 12, 20, 24, 16, 24),
            },
            ["spawns"] = new List<object?>
            {
                new Dict { ["team"] = "red-team", ["region"] = "deck-spawn" },
                new Dict { ["team"] = "blue-team", ["region"] = "yard-spawn" },
            },
            ["wools"] = new List<object?>
            {
                new Dict { ["color"] = "green", ["team"] = "blue-team",
                           ["location"] = new Dict { ["x"] = 8.0, ["y"] = 41.0, ["z"] = 9.0 } },
                new Dict { ["color"] = "pink", ["team"] = "blue-team", ["location"] = Xz(2, 3) },
            },
            ["destroyables"] = new List<object?> { new Dict { ["owner"] = "red-team", ["region"] = "cairn" } },
        };

        var points = NavPoints.Of(data, Bounds);
        int? Storey(string kind, string name)
            => points.Single(point => point.Kind == kind && point.Name == name).Y;

        await Assert.That(Storey("spawn", "red-team")).IsEqualTo(26);
        await Assert.That(Storey("wool", "green")).IsEqualTo(41);
        await Assert.That(Storey("destroyable", "red-team")).IsEqualTo(12);

        // A region that states no height names no storey, and its cell's lowest place is what it means.
        await Assert.That(Storey("spawn", "blue-team")).IsNull();
        await Assert.That(Storey("wool", "pink")).IsNull();
    }

    /// <summary>The point resolves itself against a board rather than every caller re-deciding: a stated
    /// height picks the storey nearest it, and no stated height takes the one a player walks in at.</summary>
    [Test]
    public async Task A_point_seats_itself_on_the_storey_it_names()
    {
        var ground = new WalkGround(
            new HashSet<WalkPlace> { new(4, 4, 6), new(4, 4, 26) },
            new HashSet<WalkPlace>(), new CellRect(0, 0, 9, 9));

        await Assert.That(new NavPoint("spawn", "red-team", "red-team", 4, 4, 24).Seat(ground))
            .IsEqualTo(new WalkPlace(4, 4, 26));
        await Assert.That(new NavPoint("spawn", "blue-team", "blue-team", 4, 4).Seat(ground))
            .IsEqualTo(new WalkPlace(4, 4, 6));
        await Assert.That(new NavPoint("spawn", "grey", "grey", 8, 8, 9).Seat(ground)).IsNull();
    }

    [Test]
    public async Task Every_kind_the_document_states_is_a_point_with_its_owner()
    {
        var data = new Dict
        {
            ["regions"] = new Dict
            {
                ["red-spawn"] = Rect(-10, -10, 0, 0),
                ["cairn"] = Rect(20, 20, 24, 24),
                ["keep"] = Rect(-30, 30, -26, 34),
            },
            ["spawns"] = new List<object?> { new Dict { ["team"] = "red-team", ["region"] = "red-spawn" } },
            ["wools"] = new List<object?>
            {
                new Dict { ["color"] = "green", ["team"] = "blue-team", ["location"] = Xz(8, 9) },
            },
            ["destroyables"] = new List<object?>
            {
                new Dict { ["name"] = "Cairn", ["owner"] = "blue-team", ["region"] = "cairn" },
            },
            ["cores"] = new List<object?> { new Dict { ["owner"] = "blue-team", ["region"] = "keep" } },
        };

        var points = NavPoints.Of(data, Bounds);

        await Assert.That(points.Select(point => point.Kind))
            .IsEquivalentTo(new[] { "spawn", "wool", "destroyable", "core" });
        await Assert.That(points.Where(point => point.Kind != "spawn").All(point => point.Owner == "blue-team"))
            .IsTrue();
    }

    [Test]
    public async Task A_goal_the_document_cannot_carry_is_still_a_point()
    {
        // A destroyable's region is the box the stamper built its blocks from, so one whose box is not cast
        // yet is left out of the document. The intent states where it stands from the moment it is authored.
        var data = new Dict
        {
            ["regions"] = new Dict { ["red-spawn"] = Rect(-10, -10, 0, 0) },
            ["spawns"] = new List<object?> { new Dict { ["team"] = "red-team", ["region"] = "red-spawn" } },
        };
        List<NavPoint> declared =
        [
            new("destroyable", "Cairn", "blue-team", 19, -91),
            new("core", "blue-team", "blue-team", 30, -40),
        ];

        var points = NavPoints.Of(data, Bounds, declared);

        await Assert.That(points.Count).IsEqualTo(3);
        await Assert.That(points.Any(point => point.Kind == "destroyable" && point.X == 19 && point.Z == -91))
            .IsTrue();
        await Assert.That(points.Any(point => point.Kind == "core" && point.X == 30 && point.Z == -40)).IsTrue();
    }

    [Test]
    public async Task A_placed_goal_wins_over_the_one_declared_beside_it()
    {
        // The document's copy has been cast onto real terrain; the declared one is where the author asked for
        // it. Both name the same goal, so only the placed one is a point.
        var data = new Dict
        {
            ["regions"] = new Dict { ["cairn"] = Rect(20, 20, 24, 24) },
            ["destroyables"] = new List<object?>
            {
                new Dict { ["name"] = "Cairn", ["owner"] = "blue-team", ["region"] = "cairn" },
            },
        };
        List<NavPoint> declared = [new("destroyable", "Cairn", "blue-team", -50, -50)];

        var points = NavPoints.Of(data, Bounds, declared);

        await Assert.That(points.Count).IsEqualTo(1);
        await Assert.That(points[0].X).IsEqualTo(22);
    }

    [Test]
    public async Task Two_goals_of_one_name_are_told_apart_by_their_owner()
    {
        // A mirrored board names both cairns the same; they are different goals and both must be points.
        var data = new Dict { ["regions"] = new Dict() };
        List<NavPoint> declared =
        [
            new("destroyable", "Endstone Cairn", "red-team", -20, 90),
            new("destroyable", "Endstone Cairn", "blue-team", 19, -91),
        ];

        await Assert.That(NavPoints.Of(data, Bounds, declared).Count).IsEqualTo(2);
    }
}
