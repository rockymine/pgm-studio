using PgmStudio.Domain;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Palette;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Sketch;
using PgmStudio.Vocabulary;

namespace PgmStudio.Export.Tests;

/// <summary>
/// <b>How high a player may build, and how far above that a goal marker hangs</b> — the author's rule, and
/// the one measurement that makes it right.
///
/// <para>The cap is twenty blocks over the highest thing the map builds that a player meets: the terrain,
/// and the buildings standing on it. Every assertion here is about which of those words is in and which is
/// out. Measuring the <em>built</em> ground rather than the plan's nominal surface is what stops a board
/// coming out with a ceiling under its own terrain; clearing the roofs is what stops one coming out with a
/// ceiling a player cannot build over the town in; and leaving out the made things and the objectives is
/// what stops a balloon or a floating monument deciding it.</para>
/// </summary>
public sealed class BuildCeilingTests
{
    // One flat plate at y=1. Everything stamped on this map — cages, spawn cubes, the observer platform —
    // therefore stands *above* the terrain, which is what makes it a fixture for the distinction.
    private const string Flat =
        """
        {"setup":{"mirror_mode":"rot_180","center":{"cx":0,"cz":0}},"layers": [{ "id": "ground", "base_y": 0, "layout":{"shapes":[{"id":"a","type":"rectangle","operation":"add","min_x":-40,"min_z":-40,"max_x":40,"max_z":40,"base_height":1}],"groups":[]} }]}
        """;

    // The same plate with a second one thirty blocks up, in a corner nothing is stamped on — so the terrain
    // is the tallest thing on the board and the cap has only it to follow. The corner is chosen for its
    // `rot_180` IMAGE as much as for itself: a layout stating no group is fanned shape by shape, so a plate
    // dropped opposite the spawn puts the spawn cube on it and the cap then reads the roof it raised.
    private const string Stepped =
        """
        {"setup":{"mirror_mode":"rot_180","center":{"cx":0,"cz":0}},"layers": [{ "id": "ground", "base_y": 0, "layout":{"shapes":[{"id":"a","type":"rectangle","operation":"add","min_x":-40,"min_z":-40,"max_x":40,"max_z":40,"base_height":1},{"id":"b","type":"rectangle","operation":"add","min_x":28,"min_z":-40,"max_x":40,"max_z":-28,"base_height":31}],"groups":[]} }]}
        """;

    // The flat plate with a made thing flying eighty blocks over the red wool room — a prop layer, which is
    // what a balloon or a ship is drawn as. It stands on nothing and a player meets it nowhere.
    private const string Flown =
        """
        {"setup":{"mirror_mode":"rot_180","center":{"cx":0,"cz":0}},"layers": [{ "id": "ground", "base_y": 0, "layout":{"shapes":[{"id":"a","type":"rectangle","operation":"add","min_x":-40,"min_z":-40,"max_x":40,"max_z":40,"base_height":1}],"groups":[]} }, { "id": "sky", "base_y": 80, "kind": "made", "prop": "balloon", "layout":{"shapes":[{"id":"s","type":"rectangle","operation":"add","min_x":-15,"min_z":5,"max_x":-5,"max_z":15,"base_height":4}],"groups":[]} }]}
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

    /// <summary>The tallest thing standing over the red wool room's own footprint, read off the finished
    /// world below the cap — the cage, not the sky marker hanging five over it. A cage is hollow, so this
    /// finds its walls rather than the column through its middle.</summary>
    private static int CageTop(BuiltWorld built) =>
        Enumerable.Range(0, built.ResolvedIntent.Build!.MaxHeight!.Value).Reverse().First(y =>
            Enumerable.Range(-15, 11).Any(x =>
                Enumerable.Range(5, 11).Any(z => built.World.GetBlock(x, y, z).Id != Blocks.Air)));

    /// <summary>
    /// <b>The measurement, and the assertion this file exists for.</b> The map stamps wool cages, a spawn
    /// cube and an observer platform on ground at y=1, and the cap clears the tallest of them. A cap read
    /// off the terrain alone would sit under the roofs, which is a map a player cannot build over the town
    /// in.
    /// </summary>
    [Test]
    public async Task The_cap_clears_the_buildings_standing_on_the_ground()
    {
        var built = WorldBuilder.Build(Flat, Intent());
        var cap = built.ResolvedIntent.Build!.MaxHeight!.Value;

        // The fixture is only meaningful if something really does stand above the ground.
        await Assert.That(CageTop(built)).IsGreaterThan(1);
        await Assert.That(cap).IsEqualTo(BuildCeiling.Of(CageTop(built)));
        await Assert.That(cap).IsGreaterThan(BuildCeiling.Of(1));
    }

    /// <summary>And where the terrain is the tallest thing, the cap follows the ground the relief actually
    /// built rather than a number the plan asserted. The second plate sits thirty blocks up in a corner
    /// nothing stands on — this is the case the old <c>surface + headroom</c> got wrong, where the ceiling
    /// stayed at the nominal ground the relief solve had already left behind.</summary>
    [Test]
    public async Task The_cap_follows_the_terrain_the_relief_actually_built()
    {
        var built = WorldBuilder.Build(Stepped, Intent());

        await Assert.That(built.ResolvedIntent.Build!.MaxHeight).IsEqualTo(BuildCeiling.Of(31));
    }

    /// <summary><b>A made thing does not decide it.</b> A balloon, a ship, a sculpture drawn out of layers is
    /// scenery the author hung in the air, and a cap that tracked it would follow whatever altitude was felt
    /// like. The prop here flies eighty blocks over the red room, so the cage's columns and its own are the
    /// same ones — which is the case that has to be got right, because one column carries both and the read
    /// wants the roof under the balloon rather than the balloon.
    ///
    /// <para>Both rooms name the layer they stand on, which is what puts the cage under the prop instead of
    /// on it: a room naming none takes the board's top surface, and on this fixture that is the prop.</para>
    /// </summary>
    [Test]
    public async Task A_made_thing_flying_over_a_building_does_not_raise_the_ceiling()
    {
        var onGround = Intent() with
        {
            Wools = [.. Intent().Wools!.Select(wool => wool with { Layer = SketchLayer.GroundId })],
        };
        var flown = WorldBuilder.Build(Flown, onGround);
        var grounded = WorldBuilder.Build(Flat, onGround);

        await Assert.That(flown.World.GetBlock(-10, 80, 10).Id).IsNotEqualTo(Blocks.Air);   // it is really there
        await Assert.That(CageTop(flown)).IsLessThan(80);                                   // and the cage is under it
        await Assert.That(flown.ResolvedIntent.Build!.MaxHeight)
            .IsEqualTo(grounded.ResolvedIntent.Build!.MaxHeight);
    }

    /// <summary><b>Nor does an objective.</b> A goal floats over the ground by design — a core on the ground
    /// cannot leak — so a cap derived from one could never be beneath it, and
    /// <see cref="A_goal_over_the_cap_is_a_complaint_naming_its_top_and_the_ceiling"/> could never
    /// fire.</summary>
    [Test]
    public async Task A_floating_objective_does_not_raise_the_ceiling()
    {
        var bare = WorldBuilder.Build(Flat, Intent());
        var floated = WorldBuilder.Build(Flat, Intent() with
        {
            Cores =
            [
                new CoreIntent
                {
                    Owner = "red", Name = "Red Core", Anchor = new Pt(0, 0, 0),
                    Size = 5, Height = 20, Shell = 1,
                    Float = ObjectiveDefaults.MaxFloat, Leak = ObjectiveDefaults.CoreLeak,
                },
            ],
        });

        await Assert.That(floated.ResolvedIntent.Cores![0].Box!.Value.MaxY)
            .IsGreaterThan(bare.ResolvedIntent.Build!.MaxHeight!.Value);
        await Assert.That(floated.ResolvedIntent.Build!.MaxHeight)
            .IsEqualTo(bare.ResolvedIntent.Build!.MaxHeight);
    }

    /// <summary>A goal marker hangs five over the cap, which is one rule for every goal kind — the
    /// simplification the author asked for, in place of a destroyable and a core each reasoning about the
    /// height of their own structure.</summary>
    [Test]
    public async Task A_goal_marker_hangs_five_blocks_over_the_cap()
    {
        var built = WorldBuilder.Build(Flat, Intent());
        var floor = built.ResolvedIntent.Build!.MaxHeight!.Value + BuildCeiling.MarkerOver;

        // The wool-room markers are cubes of the room's own wool, floor..floor+2 over the room's centre.
        await Assert.That(built.World.GetBlock(-10, floor + 1, 10).Id).IsEqualTo(Blocks.Wool);
        await Assert.That(built.World.GetBlock(-10, floor - 1, 10).Id).IsEqualTo(Blocks.Air);
    }

    /// <summary>The derived half of the cap (OB23): a goal's own structure standing over the line players may
    /// build to. The blocks above it can still be broken, so nothing is unwinnable — what is wrong is a goal
    /// contested from ground nobody may build up to reach — which is why it is a complaint on a built world
    /// rather than a refusal, and why it is asked here rather than at the plan gate: the ceiling is twenty
    /// over what the map <em>actually built</em>, which a plan has not solved yet.</summary>
    [Test]
    public async Task A_goal_over_the_cap_is_a_complaint_naming_its_top_and_the_ceiling()
    {
        // It takes both knobs to reach the line: at the most a goal may float, only a casing taller than the
        // clearance leaves can top out over the ceiling — a core, since a destroyable's own styles stop at
        // four courses.
        var floated = Intent() with
        {
            Cores =
            [
                new CoreIntent
                {
                    Owner = "red", Name = "Red Core", Anchor = new Pt(0, 0, 0),
                    Size = 5, Height = 20, Shell = 1,
                    Float = ObjectiveDefaults.MaxFloat, Leak = ObjectiveDefaults.CoreLeak,
                },
            ],
        };
        var built = WorldBuilder.Build(Flat, floated);
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
        var built = WorldBuilder.Build(Flat, seated);
        await Assert.That(built.Declines.Any(f => f.Rule == ObjectiveRules.OverBuildCeiling)).IsFalse();
    }
}
