using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Tests.Authoring;

/// <summary>
/// A control point placed, moved, resized and set as a Layout piece. <b>It stands where the build lays it</b>: the
/// rectangle the pieces read draws it at is the pad <c>PlayPieces.ControlPoint</c> centres on its anchor, and
/// <b>its orbit goes with it</b>, every image stored under one stamp.
/// </summary>
public sealed class ControlPointPieceTests
{
    private static readonly SymmetryIntent Rotated = new() { Mode = "rot_180", CenterX = 0, CenterZ = 0 };

    private static MapIntent Board() => new()
    {
        Teams = [new TeamDef { Id = "red", Name = "Red", Color = "red" }, new TeamDef { Id = "blue", Name = "Blue", Color = "blue" }],
    };

    private static (MapIntent Intent, string Id) Placed(int x, int z)
    {
        var (added, id, refused) = PlayPieceAdd.Apply(Board(), PlayPieceKinds.ControlPoint, new Rect(x, z, x + 1, z + 1), board: Rotated);
        if (refused.Count > 0) throw new InvalidOperationException(refused[0].Message);
        return (added!, id);
    }

    [Test]
    public async Task A_point_clicked_off_the_centre_arrives_with_its_image_and_stands_on_the_block_clicked()
    {
        var (added, id) = Placed(20, -30);

        await Assert.That(added.ControlPoints!.Count).IsEqualTo(2);
        var authored = added.ControlPoints.Single(point => point.Id == id);
        var piece = PlayPieces.Of(added).Single(piece => piece.Id == id);
        await Assert.That(piece.Kind).IsEqualTo(PlayPieceKinds.ControlPoint);
        await Assert.That(piece.Rect).IsEqualTo(new Rect(17, -33, 24, -26)).Because("a 7-block pad centred on block (20, -30)");
        await Assert.That(added.ControlPoints[1].Stamp.Identity).IsEqualTo(authored.Stamp.Identity);
        await Assert.That(piece.Shape).IsEqualTo(ControlPointShapes.Square);
    }

    [Test]
    public async Task A_point_on_the_centre_is_its_own_image()
    {
        var (added, _) = Placed(0, 0);

        await Assert.That(added.ControlPoints!.Count).IsEqualTo(1);
    }

    [Test]
    public async Task A_move_carries_the_image_the_rotated_way()
    {
        var (added, id) = Placed(20, -30);
        var (moved, refused) = PlayPieceMove.Apply(added, id, null, new Rect(19, -31, 26, -24), board: Rotated);

        await Assert.That(refused.Count).IsEqualTo(0);
        var pieces = PlayPieces.Of(moved!).Where(piece => piece.Kind == PlayPieceKinds.ControlPoint).ToList();
        await Assert.That(pieces[0].Rect).IsEqualTo(new Rect(19, -31, 26, -24));
        await Assert.That(pieces[1].Rect).IsEqualTo(new Rect(-26, 24, -19, 31)).Because("the image, a turn of (17, -33)..(24, -26) about the grid line, moves two blocks the other way");
    }

    [Test]
    public async Task A_resize_takes_the_longer_side_on_every_image_and_refuses_past_fifteen()
    {
        var (added, id) = Placed(20, -30);
        var (grown, refused) = PlayPieceMove.Apply(added, id, null, new Rect(17, -33, 26, -26), board: Rotated);

        await Assert.That(refused.Count).IsEqualTo(0);
        await Assert.That(grown!.ControlPoints!.Select(point => point.Size)).IsEquivalentTo(new[] { 9, 9 });
        var piece = PlayPieces.Of(grown).Single(piece => piece.Id == id);
        await Assert.That(piece.Rect.MaxX - piece.Rect.MinX).IsEqualTo(9);
        await Assert.That(piece.Rect.MinX).IsEqualTo(17).Because("the pad stands in the middle of the dragged rectangle");

        var (tooBig, why) = PlayPieceMove.Apply(added, id, null, new Rect(17, -33, 34, -26), board: Rotated);
        await Assert.That(tooBig).IsNull();
        await Assert.That(why.Single().Field).IsEqualTo("placed");
    }

    [Test]
    public async Task A_shape_is_the_points_on_every_image_and_a_name_the_named_ones()
    {
        var (added, id) = Placed(20, -30);
        var (disc, _) = PlayPieceSettings.Apply(added, id, PieceSettings.Shape, new PieceValue(Text: ControlPointShapes.Disc), board: Rotated);
        var (named, _) = PlayPieceSettings.Apply(disc!, id, PieceSettings.Name, new PieceValue(Text: "Mill"), board: Rotated);

        await Assert.That(named!.ControlPoints!.Select(point => point.Shape)).IsEquivalentTo(new[] { "disc", "disc" });
        await Assert.That(named.ControlPoints!.Select(point => point.Name)).IsEquivalentTo(new[] { "Mill", "" });
    }

    [Test]
    public async Task A_capture_time_is_whole_seconds_written_as_a_duration_and_a_mode_is_a_word()
    {
        var (added, id) = Placed(20, -30);
        var (timed, _) = PlayPieceSettings.Apply(added, id, PieceSettings.CaptureTime, new PieceValue(Number: 12));
        var (touched, _) = PlayPieceSettings.Apply(timed!, id, PieceSettings.CaptureMode, new PieceValue(Text: CaptureModes.Touch));
        var (refused, why) = PlayPieceSettings.Apply(added, id, PieceSettings.CaptureMode, new PieceValue(Text: "sit"));

        await Assert.That(touched!.ControlPoints!.All(point => point.CaptureTime == "12s" && point.CaptureMode == "touch")).IsTrue();
        await Assert.That(refused).IsNull();
        await Assert.That(why.Single().Field).IsEqualTo("value");
    }

    [Test]
    public async Task A_point_deletes_with_its_image_and_does_not_turn()
    {
        var (added, id) = Placed(20, -30);
        var (turned, refused) = PlayPieceMove.Turn(added, id, null, 1, board: Rotated);
        var (deleted, _) = PlayPieceAdd.Delete(added, id, board: Rotated);

        await Assert.That(turned).IsNull();
        await Assert.That(refused.Single().Field).IsEqualTo("turn");
        await Assert.That(deleted!.ControlPoints).IsEmpty();
    }
}
