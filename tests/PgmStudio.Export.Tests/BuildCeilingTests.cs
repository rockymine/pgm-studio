using PgmStudio.Domain;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Palette;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Export.Tests;

/// <summary>
/// <b>How high a player may build, and how far above that a goal marker hangs</b> — the author's rule, and
/// the one measurement that makes it right.
///
/// <para>The cap is twenty blocks over the <em>terrain</em> the map builds, and every assertion here is
/// really about that word. The old cap was the plan's flat nominal <c>surface</c> plus a slack, so it was
/// computed from a ground level the relief solve abandons and boards came out with a ceiling under their own
/// terrain. Measuring the built ground fixes that; measuring <em>only</em> the ground is what stops a wool
/// cage, a house or a tree from raising the ceiling above itself, which would make the cap a consequence of
/// the dressing.</para>
/// </summary>
public sealed class BuildCeilingTests
{
    // One flat plate at y=1. Everything stamped on this map — cages, spawn cubes, the observer platform —
    // therefore stands *above* the terrain, which is what makes it a fixture for the distinction.
    private const string Flat =
        """
        {"setup":{"mirror_mode":"rot_180","center":{"cx":0,"cz":0}},"layout":{"shapes":[{"id":"a","type":"rectangle","operation":"add","min_x":-40,"min_z":-40,"max_x":40,"max_z":40,"base_height":1}],"islands":[]}}
        """;

    private const string Stepped =
        """
        {"setup":{"mirror_mode":"rot_180","center":{"cx":0,"cz":0}},"layout":{"shapes":[{"id":"a","type":"rectangle","operation":"add","min_x":-40,"min_z":-40,"max_x":40,"max_z":40,"base_height":1},{"id":"b","type":"rectangle","operation":"add","min_x":-10,"min_z":-10,"max_x":10,"max_z":10,"base_height":31}],"islands":[]}}
        """;

    private static MapIntent Intent() => new()
    {
        Teams = [new TeamDef { Id = "red", Color = "red" }, new TeamDef { Id = "blue", Color = "blue" }],
        Spawns = [new SpawnIntent { Team = "red", Point = new Pt(-20, 2, -20), Yaw = 0 }],
        Wools =
        [
            new WoolIntent { Owner = "red", Color = "red", Spawn = new Pt(-10, 2, 10) },
            new WoolIntent { Owner = "blue", Color = "blue", Spawn = new Pt(10, 2, 10) },
        ],
    };

    /// <summary>The rule, on flat ground: ground at y=1, so the cap is 21 and nothing else decides it.</summary>
    [Test]
    public async Task The_cap_is_twenty_over_the_highest_ground()
    {
        var built = SketchWorldBuilder.Build(Flat, Intent());

        await Assert.That(built.ResolvedIntent.Build!.MaxHeight).IsEqualTo(BuildCeiling.Of(1));
        await Assert.That(built.ResolvedIntent.Build!.MaxHeight).IsEqualTo(21);
    }

    /// <summary>And it rises with the terrain rather than with a number the plan asserted. The second plate
    /// sits thirty blocks up, so the cap has to follow it — this is the case the old
    /// <c>surface + headroom</c> got wrong, where the ceiling stayed at the nominal ground the relief solve
    /// had already left behind.</summary>
    [Test]
    public async Task The_cap_follows_the_terrain_the_relief_actually_built()
    {
        var built = SketchWorldBuilder.Build(Stepped, Intent());

        await Assert.That(built.ResolvedIntent.Build!.MaxHeight).IsEqualTo(BuildCeiling.Of(31));
    }

    /// <summary>
    /// <b>The measurement, and the assertion this file exists for.</b> The map stamps wool cages, a spawn cube
    /// and an observer platform, all of which stand well above the terrain — and none of them moves the
    /// ceiling. A cap read off the finished world instead of the ground would climb to whatever was placed
    /// last, which would make it un-authorable: build a taller shell and the ceiling rises to allow a taller
    /// shell.
    /// </summary>
    [Test]
    public async Task Nothing_stamped_on_the_ground_raises_the_ceiling_over_itself()
    {
        var built = SketchWorldBuilder.Build(Flat, Intent());
        var cap = built.ResolvedIntent.Build!.MaxHeight!.Value;

        // The tallest thing standing on the ground under the cap, over the red wool room's own footprint. A
        // cage is hollow, so this reads its walls rather than the column through its middle.
        var cageTop = Enumerable.Range(0, cap).Reverse().First(y =>
            Enumerable.Range(-15, 11).Any(x =>
                Enumerable.Range(5, 11).Any(z => built.World.GetBlock(x, y, z).Id != Blocks.Air)));

        // The fixture is only meaningful if something really does stand above the ground.
        await Assert.That(cageTop).IsGreaterThan(1);
        await Assert.That(cap).IsEqualTo(BuildCeiling.Of(1));
        await Assert.That(cap).IsLessThan(BuildCeiling.Of(cageTop));
    }

    /// <summary>A goal marker hangs five over the cap, which is one rule for every goal kind — the
    /// simplification the author asked for, in place of a destroyable and a core each reasoning about the
    /// height of their own structure.</summary>
    [Test]
    public async Task A_goal_marker_hangs_five_blocks_over_the_cap()
    {
        var built = SketchWorldBuilder.Build(Flat, Intent());
        var floor = built.ResolvedIntent.Build!.MaxHeight!.Value + BuildCeiling.MarkerOver;

        // The wool-room markers are cubes of the room's own wool, floor..floor+2 over the room's centre.
        await Assert.That(built.World.GetBlock(-10, floor + 1, 10).Id).IsEqualTo(Blocks.Wool);
        await Assert.That(built.World.GetBlock(-10, floor - 1, 10).Id).IsEqualTo(Blocks.Air);
    }

    /// <summary>The derived half of the cap (OB23): a goal's own structure standing over the line players may
    /// build to. The blocks above it can still be broken, so nothing is unwinnable — what is wrong is a goal
    /// contested from ground nobody may build up to reach — which is why it is a complaint on a built world
    /// rather than a refusal, and why it is asked here rather than at the plan gate: the ceiling is twenty
    /// over the terrain the map <em>actually built</em>, which a plan has not solved yet.</summary>
    [Test]
    public async Task A_goal_over_the_cap_is_a_complaint_naming_its_top_and_the_ceiling()
    {
        // It takes both knobs to reach the line, which is why the float cap alone does not cover this: at the
        // most a goal may float, only a structure taller than the clearance leaves can top out over the
        // ceiling — a tall core casing, since a destroyable's own styles stop at four courses.
        var floated = Intent() with
        {
            Cores =
            [
                new CoreIntent
                {
                    Owner = "red", Name = "Red Core", Anchor = new Pt(0, 0, 0),
                    Size = 5, Height = 10, Shell = 1,
                    Float = ObjectiveDefaults.MaxFloat, Leak = ObjectiveDefaults.CoreLeak,
                },
            ],
        };
        var built = SketchWorldBuilder.Build(Flat, floated);
        var cap = built.ResolvedIntent.Build!.MaxHeight!.Value;
        var top = built.ResolvedIntent.Cores![0].Box!.Value.MaxY;

        // The fixture only says anything if the goal really does stand over the line.
        await Assert.That(top).IsGreaterThan(cap);

        var complaint = built.Declines.Single(f => f.Rule == ObjectiveRules.OverBuildCeiling);
        await Assert.That(complaint.Severity).IsEqualTo(Severity.Complaint);
        await Assert.That(complaint.Message).Contains($"y{top}");
        await Assert.That(complaint.Message).Contains($"y{cap}");
    }

    [Test]
    public async Task A_goal_under_the_cap_says_nothing()
    {
        var seated = Intent() with
        {
            Destroyables =
            [
                new DestroyableIntent
                {
                    Owner = "red", Name = "Red Monument", Style = "pillar-3",
                    Anchor = new Pt(0, 0, 0), Float = ObjectiveDefaults.DestroyableFloat,
                },
            ],
        };
        var built = SketchWorldBuilder.Build(Flat, seated);
        await Assert.That(built.Declines.Any(f => f.Rule == ObjectiveRules.OverBuildCeiling)).IsFalse();
    }
}
