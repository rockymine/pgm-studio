using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Plan;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Tests.Authoring;

/// <summary>
/// The play-piece rules asked of the intent: a board mirrored across <c>x = 50</c> whose red spawn, wool and
/// goals are clean, and each rule made to fire by one change — once, on the authored unit, never again on the
/// image the symmetry carried.
/// </summary>
public sealed class IntentCheckTests
{
    private static readonly SymmetryIntent MirrorX = new() { Mode = "mirror_x", CenterX = 50, CenterZ = 30 };

    private static StampId Stamp(string kind, int image) => new(kind, "0", image);

    private static SpawnIntent Spawn(int image, double minX, double pointX) => new()
    {
        Id = image == 0 ? "spawn-red" : "spawn-blue", Team = image == 0 ? "red" : "blue", Stamp = Stamp("spawn", image),
        Protection = [new Rect(minX, 25, minX + 24, 35)], Point = new Pt(pointX, 12, 30),
        House = RoomHouse.Over(new Rect(minX + 2, 26, minX + 12, 34)),
    };

    private static MapIntent Clean() => new()
    {
        Symmetry = MirrorX,
        Spawns = [Spawn(0, 0, 4), Spawn(1, 76, 96)],
        Wools = [new WoolIntent { Id = "wool-lime", Color = "lime", Stamp = Stamp("wool", 0), Protection = [new Rect(20, 0, 30, 10)] }],
        Destroyables =
        [
            new DestroyableIntent { Id = "monument-1", Stamp = Stamp("destroyable", 0) },
            new DestroyableIntent { Id = "monument-2", Stamp = Stamp("destroyable", 1) },
        ],
        Structures = new StructureIntent { IronCubes = [new IronCube(4, 26, true, Stamp("ironcube", 0), "iron-1")] },
    };

    private static IEnumerable<string> Rules(Findings findings) => findings.Select(finding => finding.Rule);

    [Test]
    public async Task A_clean_board_breaks_nothing()
    {
        await Assert.That(IntentCheck.Check(Clean())).IsEmpty();
    }

    [Test]
    public async Task An_empty_map_has_no_spawn_and_no_objective()
    {
        var found = IntentCheck.Check(new MapIntent());

        await Assert.That(Rules(found)).IsEquivalentTo(new[] { PlanRules.NoSpawn, PlanRules.NoObjective });
        await Assert.That(found.Single(finding => finding.Rule == PlanRules.NoObjective).Severity).IsEqualTo(Severity.Complaint);
    }

    [Test]
    public async Task A_goal_the_map_cannot_build_is_named_once_on_its_authored_image()
    {
        var bad = new DestroyableIntent { Style = "spire", Float = ObjectiveDefaults.MaxFloat + 1 };
        var core = new CoreIntent { Lava = ObjectiveDefaults.MaxCoreLava + 1, LavaHeight = ObjectiveDefaults.MinCoreLavaHeight - 1 };
        var intent = Clean() with
        {
            Destroyables = [bad with { Id = "monument-1", Stamp = Stamp("destroyable", 0) }, bad with { Id = "monument-2", Stamp = Stamp("destroyable", 1) }],
            Cores = [core with { Id = "core-1", Stamp = Stamp("core", 0) }, core with { Id = "core-2", Stamp = Stamp("core", 1) }],
            Wools = [Clean().Wools![0] with { Color = "teal" }],
        };

        var found = IntentCheck.Check(intent);

        await Assert.That(Rules(found)).IsEquivalentTo(new[]
        {
            PlanRules.UnknownColor, PlanRules.UnknownStyle, ObjectiveRules.FloatCap, ObjectiveRules.Casing, ObjectiveRules.Casing,
        });
        await Assert.That(found.SelectMany(finding => finding.Subjects ?? [])).DoesNotContain("monument-2");
    }

    [Test]
    public async Task Monuments_and_cores_take_two_teams()
    {
        var four = Clean() with { Symmetry = new SymmetryIntent { Mode = "rot_90", CenterX = 50, CenterZ = 30 } };

        await Assert.That(Rules(IntentCheck.Check(four))).Contains(ObjectiveRules.TwoTeamOnly);
        await Assert.That(Rules(IntentCheck.Check(Clean()))).DoesNotContain(ObjectiveRules.TwoTeamOnly);
        await Assert.That(Rules(IntentCheck.Check(four with { Teams = [new TeamDef { Id = "red" }, new TeamDef { Id = "blue" }] })))
            .DoesNotContain(ObjectiveRules.TwoTeamOnly).Because("the teams the map states are what PGM counts");
    }

    [Test]
    public async Task Iron_whose_cube_leaves_every_spawn_region_is_complained_of()
    {
        var stray = Clean() with { Structures = new StructureIntent { IronCubes = [new IronCube(22, 26, false, Stamp("ironcube", 0), "iron-1")] } };

        var found = IntentCheck.Check(stray).Single();

        await Assert.That(found.Rule).IsEqualTo(LayoutRules.IronInSpawn);
        await Assert.That(found.Severity).IsEqualTo(Severity.Complaint);
    }

    [Test]
    public async Task A_spawn_in_the_half_of_its_region_nearer_the_centre_is_complained_of()
    {
        var forward = Clean() with { Spawns = [Spawn(0, 0, 20), Spawn(1, 76, 80)] };

        await Assert.That(Rules(IntentCheck.Check(forward))).IsEquivalentTo(new[] { LayoutRules.SpawnAtBack });
        await Assert.That(Rules(IntentCheck.Check(forward with { Symmetry = null }))).DoesNotContain(LayoutRules.SpawnAtBack)
            .Because("a board with no symmetry has no centre for a spawn to stand behind");
        await Assert.That(Rules(IntentCheck.Check(forward with { Symmetry = null }, MirrorX))).Contains(LayoutRules.SpawnAtBack)
            .Because("the sketch's symmetry stands in for one the intent does not state");
    }

    [Test]
    public async Task A_room_over_its_caps_is_complained_of()
    {
        var red = Spawn(0, 0, 4);
        var wide = red with
        {
            Protection = [new Rect(0, 20, 31, 40)],
            House = new RoomHouse { Wings = [.. red.House!.Wings, .. RoomHouse.Over(new Rect(12, 26, 23, 30)).Wings] },
        };

        var found = IntentCheck.Check(Clean() with { Spawns = [wide, Spawn(1, 76, 96)] });

        await Assert.That(Rules(found)).IsEquivalentTo(new[] { LayoutRules.BuildingFootprint, LayoutRules.RoomRegionSize });
    }
}
