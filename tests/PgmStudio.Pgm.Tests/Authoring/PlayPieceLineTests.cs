using PgmStudio.Domain;
using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Pgm.Tests.Authoring;

/// <summary>
/// A wall's line set whole by its id. <b>The named image takes the corners as posted</b>, and <b>every other image
/// of its unit takes them carried through the symmetry</b>, so a corner dragged, added or taken out on one wall is
/// the same change on its mirror.
/// </summary>
public sealed class PlayPieceLineTests
{
    private static readonly SymmetryIntent MirrorX = new() { Mode = "mirror_x", CenterX = 50, CenterZ = 50 };

    private static MapIntent Fanned() => new()
    {
        Structures = new StructureIntent
        {
            IronCubes = [new IronCube(5, 5, true, new StampId("ironcube", "0", 0), "iron-1")],
            Walls = [new WallStructure([[41, 10], [41, 18]], 20, Stamp: new StampId("wall", "0", 0), Id: "wall-1"),
                     new WallStructure([[59, 10], [59, 18]], 20, Stamp: new StampId("wall", "0", 1), Id: "wall-2")],
        },
    };

    [Test]
    public async Task A_bend_added_to_a_wall_is_mirrored_on_its_image()
    {
        var (lined, refused) = PlayPieceLine.Apply(Fanned(), "wall-1", [[41, 10], [41, 18], [44, 22]], board: MirrorX);

        await Assert.That(refused.Count).IsEqualTo(0);
        await Assert.That(lined!.Structures!.Walls[0].Corners()).IsEquivalentTo(new[] { (41, 10), (41, 18), (44, 22) });
        await Assert.That(lined.Structures.Walls[1].Corners()).IsEquivalentTo(new[] { (59, 10), (59, 18), (56, 22) })
            .Because("the image is the line mirrored about x = 50");
    }

    [Test]
    public async Task Alone_leaves_the_image_where_it_stands()
    {
        var (lined, _) = PlayPieceLine.Apply(Fanned(), "wall-1", [[41, 10], [44, 22]], alone: true, board: MirrorX);

        await Assert.That(lined!.Structures!.Walls[1].Corners()).IsEquivalentTo(new[] { (59, 10), (59, 18) });
    }

    [Test]
    public async Task A_line_of_one_corner_or_two_corners_on_one_point_is_refused()
    {
        var (single, why) = PlayPieceLine.Apply(Fanned(), "wall-1", [[41, 10]]);
        var (folded, _) = PlayPieceLine.Apply(Fanned(), "wall-1", [[41, 10], [41, 18], [41, 18]]);

        await Assert.That(single).IsNull();
        await Assert.That(why.Single().Field).IsEqualTo("points");
        await Assert.That(folded).IsNull();
    }

    [Test]
    public async Task Only_a_wall_runs_along_a_line()
    {
        var (cube, why) = PlayPieceLine.Apply(Fanned(), "iron-1", [[1, 1], [4, 4]]);
        var (missing, absent) = PlayPieceLine.Apply(Fanned(), "wall-9", [[1, 1], [4, 4]]);

        await Assert.That(cube).IsNull();
        await Assert.That(why.Single().Rule).IsEqualTo(RequestRules.Unreadable);
        await Assert.That(missing).IsNull();
        await Assert.That(absent.Single().Rule).IsEqualTo(RequestRules.NoSuchSubject);
    }
}
