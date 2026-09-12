using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// How capture points fan across the orbit — the one fan that is not keyed on a team. A point belongs to
/// nobody, so what orbits is its position, and a point standing on the centre of symmetry is its own image
/// and stays one point. That is what makes the author's rule authorable: state the middle and one side, and
/// the board comes back a middle and a matched pair.
/// </summary>
public sealed class ControlPointSymmetryTests
{
    private static MapIntent Board(string mode, params (double X, double Z)[] anchors) => new()
    {
        Symmetry = new SymmetryIntent { Mode = mode, CenterX = 0, CenterZ = 0 },
        Teams = [new TeamDef { Id = "red", Name = "Red" }, new TeamDef { Id = "blue", Name = "Blue" }],
        ControlPoints = [.. anchors.Select(a => new ControlPointIntent { Anchor = new Pt(a.X, 0, a.Z) })],
    };

    private static List<ControlPointIntent> Fan(MapIntent intent)
        => SymmetryExpander.Expand(intent).ControlPoints!;

    // ── two teams ───────────────────────────────────────────────────────────────────
    [Test]
    public async Task A_point_on_the_centre_of_symmetry_stays_one_point()
        => await Assert.That(Fan(Board("rot_180", (0, 0))).Count).IsEqualTo(1);

    [Test]
    public async Task A_point_off_the_centre_becomes_a_mirrored_pair()
    {
        var points = Fan(Board("rot_180", (0, 30)));
        await Assert.That(points.Count).IsEqualTo(2);
        await Assert.That(points.Select(p => (p.Anchor.X, p.Anchor.Z)))
            .IsEquivalentTo(new[] { (0d, 30d), (0d, -30d) });
    }

    // The author's board: state the middle and one side, get three points.
    [Test]
    public async Task A_centre_and_one_side_make_the_three_point_board()
    {
        var points = Fan(Board("rot_180", (0, 0), (0, 30)));
        await Assert.That(points.Count).IsEqualTo(3);
        await Assert.That(points.Select(p => p.Anchor.Z).Order())
            .IsEquivalentTo(new[] { -30d, 0d, 30d });
    }

    [Test]
    public async Task A_mirror_fans_the_same_way_a_rotation_does()
        => await Assert.That(Fan(Board("mirror_z", (0, 0), (0, 30))).Count).IsEqualTo(3);

    // ── four teams ──────────────────────────────────────────────────────────────────
    // rot_90 is the four-team board: a centre plus a ring of four, which is what every four-team corpus map
    // with a usable spawn frame carries.
    [Test]
    public async Task On_a_quarter_turn_board_a_centre_and_one_side_make_five_points()
    {
        var board = Board("rot_90", (0, 0), (0, 30)) with
        {
            Teams = [new TeamDef { Id = "red", Name = "Red" }, new TeamDef { Id = "blue", Name = "Blue" },
                     new TeamDef { Id = "green", Name = "Green" }, new TeamDef { Id = "yellow", Name = "Yellow" }],
        };
        var points = Fan(board);
        await Assert.That(points.Count).IsEqualTo(5);
        await Assert.That(points.Count(p => p.Anchor is { X: 0, Z: 0 })).IsEqualTo(1);
    }

    // ── what rides across ───────────────────────────────────────────────────────────
    [Test]
    public async Task An_image_carries_the_points_tuning()
    {
        var authored = new ControlPointIntent { Name = "Side", Anchor = new Pt(0, 0, 30), Size = 9, Points = 2 };
        var points = Fan(Board("rot_180", (0, 0)) with { ControlPoints = [authored] });
        foreach (var image in points)
        {
            await Assert.That(image.Size).IsEqualTo(9);
            await Assert.That(image.Points).IsEqualTo(2d);
        }
    }

    // Two rows reading "Side" name one hill twice, so an image of a named point takes an index.
    [Test]
    public async Task An_image_of_a_named_point_is_named_apart_from_it()
    {
        var authored = new ControlPointIntent { Name = "Side", Anchor = new Pt(0, 0, 30) };
        var names = Fan(Board("rot_180", (0, 0)) with { ControlPoints = [authored] }).Select(p => p.Name);
        await Assert.That(names).IsEquivalentTo(new[] { "Side", "Side 2" });
    }

    // An unnamed point stays unnamed on every image: PGM numbers those itself.
    [Test]
    public async Task An_image_of_an_unnamed_point_stays_unnamed()
        => await Assert.That(Fan(Board("rot_180", (0, 30))).Select(p => p.Name))
            .IsEquivalentTo(new[] { "", "" });

    // Both images are images of one authored point, which is what lets a reader pair a stamp with its mirror.
    [Test]
    public async Task Both_images_share_the_unit_they_are_images_of()
    {
        var points = Fan(Board("rot_180", (0, 30)));
        await Assert.That(points.Select(p => p.Stamp.Unit).Distinct().Count()).IsEqualTo(1);
        await Assert.That(points.Select(p => p.Stamp.Image).Order()).IsEquivalentTo(new[] { 0, 1 });
    }

    // An author who states both sides gets both sides, not four points stacked two to a pad.
    [Test]
    public async Task Stating_a_point_and_its_own_image_does_not_double_the_board()
        => await Assert.That(Fan(Board("rot_180", (0, 30), (0, -30))).Count).IsEqualTo(2);

    [Test]
    public async Task A_board_with_no_symmetry_passes_its_points_through()
        => await Assert.That(
            SymmetryExpander.Expand(new MapIntent
            {
                ControlPoints = [new ControlPointIntent { Anchor = new Pt(0, 0, 30) }],
            }).ControlPoints!.Count).IsEqualTo(1);
}
