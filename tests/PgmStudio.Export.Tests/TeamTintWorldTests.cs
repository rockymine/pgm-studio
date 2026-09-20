using System.Text.Json;
using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Minecraft.Painting;
using PgmStudio.Minecraft.Palette;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Sketch;
using PgmStudio.Vocabulary;

namespace PgmStudio.Export.Tests;

/// <summary>
/// <c>PT5</c> — a team tint painted over land more than one team enters. The tint is one colour per canonical
/// island, so an island both teams' spawns stand on wears one team's colour everywhere; a board whose ground
/// is a single landmass, which is the ordinary shape of a capture board, is then the whole map in one colour.
///
/// <para>The finding is raised over the themes the painter resolves rather than over the registry, so the
/// three cases are the three combinations that matter: shared ground with a tint, shared ground without one,
/// and a tint over ground each team has to itself.</para>
/// </summary>
public sealed class TeamTintWorldTests
{
    private static TerrainTheme Tinted => TerrainTheme.Default with
    {
        Surface = new TopBand(new TeamTintedMaterial(Blocks.StainedClay, new SolidMaterial(Blocks.Grass)), 1),
    };

    private static string Board(TerrainTheme theme, params SketchShape[] shapes) => new SketchLayout
    {
        Setup = new SketchSetup { MirrorMode = "rot_180", Center = new SketchCenter { Cx = 0, Cz = 0 } },
        Layers = [SketchLayer.Ground([.. shapes], [])],
        Themes = new Dictionary<string, JsonElement>
        {
            ["map"] = JsonSerializer.Deserialize<JsonElement>(TerrainThemeJson.Serialize(theme)),
        },
        MapTheme = "map",
    }.ToJson();

    private static SketchShape Slab(string id, int minX, int maxX) => new()
    {
        Id = id, Type = "rectangle", Operation = "add",
        MinX = minX, MinZ = -30, MaxX = maxX, MaxZ = 30, BaseHeight = 4,
    };

    private static MapIntent TwoTeams => new()
    {
        Teams = [new TeamDef { Id = "red", Color = "red" }, new TeamDef { Id = "blue", Color = "blue" }],
        Spawns =
        [
            new SpawnIntent { Team = "red", Point = new Pt(-25, 4, 0), Yaw = 0 },
            new SpawnIntent { Team = "blue", Point = new Pt(25, 4, 0), Yaw = 180 },
        ],
        Wools = [],
        Observer = new ObserverIntent { Point = new Pt(0, 30, 0), Yaw = 0 },
        Meta = new MetaIntent { Name = "Tint", Authors = [] },
    };

    private static IReadOnlyList<Finding> Tint(BuiltWorld built) =>
        [.. built.Declines.Where(finding => finding.Rule == TerrainThemeRules.TintOverSharedGround)];

    /// <summary>One landmass, both teams on it, a tint on its surface: the whole board wears red because red
    /// compiled first, and the complaint says so with the island, the teams and how much ground it is.</summary>
    [Test]
    public async Task A_tint_over_one_island_both_teams_enter_is_a_complaint()
    {
        var built = WorldBuilder.Build(Board(Tinted, Slab("a", -40, 40)), TwoTeams);

        // The defect the complaint is about, in the blocks: a cell in the far corner of blue's half wears
        // red's clay, because the island's one owner is whichever spawn was read first.
        await Assert.That(built.World.GetBlock(38, 3, 28))
            .IsEqualTo((Blocks.StainedClay, BlockColors.BlockDamage("red")));

        var found = Tint(built);
        await Assert.That(found.Count).IsEqualTo(1);
        await Assert.That(found[0].Severity).IsEqualTo(Severity.Complaint);
        await Assert.That(found[0].Field).IsEqualTo("islandTeams.1");
        await Assert.That(found[0].SubjectIds).IsEquivalentTo(new[] { "red", "blue" });
        await Assert.That(found[0].Message).Contains("red's colour");
    }

    /// <summary>The same board painted by a theme that states no team says nothing: shared ground is only a
    /// fault where the paint was going to claim it.</summary>
    [Test]
    public async Task Shared_ground_under_a_theme_that_states_no_team_is_no_complaint()
    {
        var built = WorldBuilder.Build(Board(TerrainTheme.Default, Slab("a", -40, 40)), TwoTeams);

        await Assert.That(Tint(built)).IsEmpty();
    }

    /// <summary>And the arrangement the tint is for — a landmass per team, separated by void — is what it can
    /// state, so the same theme over it is silent.</summary>
    [Test]
    public async Task A_tint_over_a_landmass_per_team_is_no_complaint()
    {
        var built = WorldBuilder.Build(
            Board(Tinted, Slab("a", -40, -10), Slab("b", 10, 40)), TwoTeams);

        await Assert.That(Tint(built)).IsEmpty();
    }
}
