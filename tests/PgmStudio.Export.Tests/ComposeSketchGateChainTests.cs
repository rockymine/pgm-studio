using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Vocabulary;

namespace PgmStudio.Export.Tests;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Every gate a sketch map is held to is inside <see cref="MapExportComposer.ComposeSketch"/>, so which door
/// a caller came through cannot change what its map is judged by.
///
/// <para>The HTTP export reaches the chain through <c>Compose</c> and the headless driver reaches
/// <c>ComposeSketch</c> directly, so a gate asked in front of the branch is asked of one of them and not the
/// other. That is what these assert, from the driver's side: the two that used to sit in front — an unloadable
/// <c>&lt;gamemode&gt;</c> and a map that cannot be walked — refuse here.</para>
///
/// <para>The traversability judgement moved with a second consequence worth naming: it now reads the ground
/// this build produced rather than whatever the last <c>sketch/finish</c> rasterized into the store, so a
/// board cut apart after its finish is judged as it will ship rather than as it was last filed.</para>
/// </summary>
public sealed class ComposeSketchGateChainTests
{
    /// <summary>Two plates with forty blocks of void between them, and nothing bridging.</summary>
    private const string TwoIslands =
        """
        {"setup":{"mirror_mode":"rot_180","center":{"cx":0,"cz":0}},"layout":{"shapes":[
          {"id":"a","type":"rectangle","operation":"add","min_x":-60,"min_z":-20,"max_x":-20,"max_z":20,"base_height":1},
          {"id":"b","type":"rectangle","operation":"add","min_x":20,"min_z":-20,"max_x":60,"max_z":20,"base_height":1}],
         "islands":[]}}
        """;

    /// <summary>One plate covering both halves, so everything on it reaches everything else.</summary>
    private const string OneIsland =
        """
        {"setup":{"mirror_mode":"rot_180","center":{"cx":0,"cz":0}},"layout":{"shapes":[
          {"id":"a","type":"rectangle","operation":"add","min_x":-60,"min_z":-20,"max_x":60,"max_z":20,"base_height":1}],
         "islands":[]}}
        """;

    /// <summary>A spawn on the left plate and a wool on the right one — on <see cref="TwoIslands"/> the two
    /// are in different components, and on <see cref="OneIsland"/> they are not.</summary>
    private static MapIntent Intent() => new()
    {
        Teams = [new TeamDef { Id = "red", Color = "red" }, new TeamDef { Id = "blue", Color = "blue" }],
        Spawns = [new SpawnIntent { Team = "red", Point = new Pt(-40, 2, 0), Yaw = 0 }],
        Wools = [new WoolIntent { Owner = "blue", Color = "blue", Spawn = new Pt(40, 2, 0) }],
    };

    private static Dict Doc(params string[] gamemodes) => new()
    {
        ["name"] = "m", ["version"] = "1.0.0",
        ["gamemode"] = gamemodes.Cast<object?>().ToList(),
    };

    [Test]
    public async Task A_gamemode_PGM_cannot_load_refuses_on_the_driver_road_too()
    {
        var result = MapExportComposer.ComposeSketch(Doc("not-a-real-mode"), OneIsland, Intent());

        await Assert.That(result.IsError).IsTrue();
        await Assert.That(result.ErrorStatus).IsEqualTo(409);
        await Assert.That(result.Error).IsEqualTo("unknown gamemode");
        await Assert.That(result.Findings!.Single().Rule).IsEqualTo(ObjectiveRules.UnknownGamemode);
    }

    /// <summary>A board whose objective cannot be walked to from its spawn. It is a real refusal rather than
    /// a shape check: the same intent on one plate composes.</summary>
    [Test]
    public async Task A_board_cut_in_two_refuses_on_the_driver_road_too()
    {
        var result = MapExportComposer.ComposeSketch(Doc(), TwoIslands, Intent());

        await Assert.That(result.IsError).IsTrue();
        await Assert.That(result.Error).IsEqualTo("not traversable");
        await Assert.That(result.Findings!.Single().Rule).IsEqualTo("EX1");
    }

    /// <summary>And the same map on ground that joins is composed. Asserted because a traversability gate
    /// that refused every board would satisfy the test above while making the export useless.</summary>
    [Test]
    public async Task The_same_map_on_ground_that_joins_composes()
    {
        var result = MapExportComposer.ComposeSketch(Doc(), OneIsland, Intent());

        await Assert.That(result.IsError).IsFalse()
            .Because($"it answered {Finding.Summarize(result.Findings ?? [])}");
        await Assert.That(result.Xml).IsNotNull();
    }
}
