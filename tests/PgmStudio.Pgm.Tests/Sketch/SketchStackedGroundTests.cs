using PgmStudio.Pgm.Sketch;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Tests.Sketch;

/// <summary>
/// What a stack of layers <b>builds</b>, as against what its document says: two layers claiming one column's
/// blocks (<c>SK10</c>), and a mass of standable ground nothing joins to the board (<c>SK11</c>).
///
/// <para>Neither is visible in the document. A layer states a `base_y` and a height and the pair reads
/// perfectly; only the rasterized spans say whether the air the storeys were drawn to have is there, and only
/// a walk over them says whether a player can get onto the upper one. Both are complaints — the world builds
/// either way, and a detached island may be what the board is.</para>
/// </summary>
public sealed class SketchStackedGroundTests
{
    private const string Setup = @"""setup"":{""mirror_mode"":""none"",""center"":{""cx"":0,""cz"":0}}";

    /// <summary>A rectangle of the given extent and thickness, as one add.</summary>
    private static string Slab(string id, int minX, int minZ, int maxX, int maxZ, int height) =>
        $@"{{""shapes"":[{{""id"":""{id}"",""type"":""rectangle"",""operation"":""add"","
        + $@"""min_x"":{minX},""min_z"":{minZ},""max_x"":{maxX},""max_z"":{maxZ},""base_height"":{height}}}],""islands"":[]}}";

    private static string Layer(string id, int baseY, string shapes) =>
        $@"{{""id"":""{id}"",""base_y"":{baseY},""layout"":{shapes}}}";

    private static string Board(params string[] layers) =>
        "{" + Setup + @",""layers"":[" + string.Join(",", layers) + "]}";

    private static SketchLayout? Read(string json) => SketchLayout.Parse(json);

    /// <summary>A yard eight thick at y0 with a deck at base_y 20: twelve blocks of air between them, and
    /// nothing to say.</summary>
    [Test]
    public async Task Two_layers_clear_of_each_other_say_nothing()
    {
        var board = Read(Board(Layer("yard", 0, Slab("a", 0, 0, 20, 20, 8)),
                               Layer("deck", 20, Slab("b", 0, 0, 20, 20, 4))));
        await Assert.That(SketchRasterizer.OverlappingLayerSpans(board)).IsEmpty();
    }

    /// <summary>The deck dropped to base_y 4 runs through the yard that reaches y8: four courses claimed
    /// twice, so the two storeys build as one solid mass and the gap is not in the world.</summary>
    [Test]
    public async Task Two_layers_claiming_one_columns_blocks_are_reported_once_with_the_count()
    {
        var board = Read(Board(Layer("yard", 0, Slab("a", 0, 0, 20, 20, 8)),
                               Layer("deck", 4, Slab("b", 0, 0, 20, 20, 4))));

        var overlap = SketchRasterizer.OverlappingLayerSpans(board).Single();
        await Assert.That(overlap.Lower).IsEqualTo("yard");
        await Assert.That(overlap.Upper).IsEqualTo("deck");
        await Assert.That(overlap.Courses).IsEqualTo(5);       // yard [0,8] and deck [4,8] share y4..y8
        await Assert.That(overlap.Cells).IsEqualTo(20 * 20);
    }

    /// <summary>The seam. A layer spans <c>[base_y, base_y + height]</c> inclusive, so an upper layer placed
    /// at the lower one's top shares exactly that course — "the deck starts where the walls end", which is
    /// how `opus5-mineshaft` is authored. One shared course says nothing; two is a slab driven through
    /// another.</summary>
    [Test]
    public async Task One_shared_course_is_the_seam_and_two_is_an_overlap()
    {
        // `yard` occupies [0, 8]; `deck` at base_y 8 shares y8 and nothing else.
        var seam = Read(Board(Layer("yard", 0, Slab("a", 0, 0, 20, 20, 8)),
                              Layer("deck", 8, Slab("b", 0, 0, 20, 20, 4))));
        await Assert.That(SketchRasterizer.OverlappingLayerSpans(seam)).IsEmpty();

        // Clear of each other entirely: also silent.
        var clear = Read(Board(Layer("yard", 0, Slab("a", 0, 0, 20, 20, 8)),
                               Layer("deck", 9, Slab("b", 0, 0, 20, 20, 4))));
        await Assert.That(SketchRasterizer.OverlappingLayerSpans(clear)).IsEmpty();

        // One block lower and they share y7 and y8, which is the overlap this reports.
        var driven = Read(Board(Layer("yard", 0, Slab("a", 0, 0, 20, 20, 8)),
                                Layer("deck", 7, Slab("b", 0, 0, 20, 20, 4))));
        var overlap = SketchRasterizer.OverlappingLayerSpans(driven).Single();
        await Assert.That(overlap.Courses).IsEqualTo(2);
        await Assert.That(overlap.Cells).IsEqualTo(20 * 20);
    }

    /// <summary>Two slabs side by side on the ground: one mass, nothing detached.</summary>
    [Test]
    public async Task One_connected_board_reports_no_detached_mass()
    {
        var board = Read(Board(Layer("ground", 0, Slab("a", 0, 0, 20, 20, 4))));
        await Assert.That(SketchRasterizer.DetachedMasses(board)).IsEmpty();
    }

    /// <summary>A deck twenty blocks over a yard with no way up: standable, open to the sky, and no route
    /// onto it. The coordinates say where to fly to look at it.</summary>
    [Test]
    public async Task A_deck_with_no_way_up_is_reported_with_its_seat()
    {
        var board = Read(Board(Layer("yard", 0, Slab("a", 0, 0, 20, 20, 4)),
                               Layer("deck", 30, Slab("b", 4, 4, 12, 12, 4))));

        var mass = SketchRasterizer.DetachedMasses(board).Single();
        await Assert.That(mass.Places).IsEqualTo(8 * 8);
        await Assert.That(mass.Y).IsEqualTo(35);          // the deck spans [30, 34]; a player stands on 35
        await Assert.That(mass.X).IsEqualTo(4);
        await Assert.That(mass.Z).IsEqualTo(4);
    }

    /// <summary>A deck a stepped course climbs to is joined. The thinnest slab the rasterizer builds is two
    /// blocks, so a layer seamed onto the one under it raises the standing surface by exactly two — which is
    /// what the bound is set to, and why a board built in courses reads as one mass.</summary>
    [Test]
    public async Task A_deck_a_stepped_course_climbs_to_is_joined()
    {
        // yard [0,4] stands at 5; step [5,6] at 7; deck [7,8] at 9 — two blocks a course, all the way up.
        var board = Read(Board(Layer("yard", 0, Slab("a", 0, 0, 20, 20, 4)),
                               Layer("step", 5, Slab("b", 8, 0, 20, 20, 1)),
                               Layer("deck", 7, Slab("c", 12, 0, 20, 20, 1))));
        await Assert.That(SketchRasterizer.DetachedMasses(board)).IsEmpty();
    }

    /// <summary>Two landmasses across a void are how a board is normally drawn — the build zone bridges them
    /// at the intent tier, which a sketch does not state — so a mass standing <em>beside</em> another says
    /// nothing. Only a mass standing <em>over</em> one does. Without this the finding is noise: `thunderstorm`,
    /// a one-layer board of ordinary islands, reported eight.</summary>
    [Test]
    public async Task A_mass_beside_another_is_an_island_and_a_mass_over_one_is_not()
    {
        // Two slabs at the same height sharing no column: two islands, and nothing to say.
        var islands = Read(Board(Layer("west", 0, Slab("a", 0, 0, 20, 20, 4)),
                                 Layer("east", 0, Slab("b", 40, 0, 60, 20, 4))));
        await Assert.That(SketchRasterizer.DetachedMasses(islands)).IsEmpty();

        // The same second slab lifted over the first: now it stands on nothing anyone drew a way to.
        var over = Read(Board(Layer("west", 0, Slab("a", 0, 0, 20, 20, 4)),
                              Layer("deck", 30, Slab("b", 0, 0, 20, 20, 4))));
        await Assert.That(SketchRasterizer.DetachedMasses(over).Single().Places).IsEqualTo(20 * 20);
    }

    /// <summary>A mass smaller than the floor is a ledge or a rasterizer sliver, not a place.</summary>
    [Test]
    public async Task A_mass_under_the_floor_is_not_named()
    {
        var board = Read(Board(Layer("yard", 0, Slab("a", 0, 0, 20, 20, 4)),
                               Layer("perch", 30, Slab("b", 4, 4, 5, 5, 4))));   // 2×2 = 4 places
        await Assert.That(SketchRasterizer.DetachedMasses(board)).IsEmpty();
    }

    /// <summary>Both findings ride out of the gate as complaints: the board built, and what they describe may
    /// be what the author drew.</summary>
    [Test]
    public async Task Both_findings_are_complaints_and_name_their_layers()
    {
        var json = Board(Layer("yard", 0, Slab("a", 0, 0, 20, 20, 8)),
                         Layer("deck", 4, Slab("b", 0, 0, 20, 20, 4)),
                         Layer("perch", 60, Slab("c", 4, 4, 12, 12, 4)));
        var findings = SketchLayoutCheck.Check(json);

        var overlap = findings.Single(finding => finding.Rule == SketchRules.LayersOverlap);
        await Assert.That(overlap.Severity).IsEqualTo(Severity.Complaint);
        await Assert.That(overlap.SubjectIds).IsEquivalentTo(new[] { "yard", "deck" });

        var detached = findings.Single(finding => finding.Rule == SketchRules.MassUnreached);
        await Assert.That(detached.Severity).IsEqualTo(Severity.Complaint);
        await Assert.That(detached.Message).Contains("@65");
    }
}
