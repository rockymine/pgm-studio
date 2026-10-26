using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Tests.Authoring;

/// <summary>The pieces the sketch draws stand where the piece route reads them, so a drag of a drawn rectangle
/// is a move it accepts, and an image the intent does not store is drawn as carried.</summary>
public sealed class PlayPiecesTests
{
    private static MapIntent Board() => new()
    {
        Spawns =
        [
            new SpawnIntent
            {
                Id = "spawn-red", Team = "red", Point = new Pt(5, 12, 5), Protection = [new Rect(0, 0, 10, 10)],
                Iron = [new Pt(2.5, 12, 2.5)],
            },
        ],
        Wools =
        [
            new WoolIntent
            {
                Id = "wool-red-blue", Owner = "red", Color = "blue", Spawn = new Pt(25.5, 12, 5.5),
                Protection = [new Rect(20, 0, 30, 10)],
            },
        ],
        Destroyables = [new DestroyableIntent { Id = "destroyable-red", Owner = "red", Style = "cube-3", Anchor = new Pt(40, 12, 5) }],
        Cores = [new CoreIntent { Id = "core-red", Owner = "red", Anchor = new Pt(60, 12, 5) }],
        ControlPoints = [new ControlPointIntent { Id = "point-1", Anchor = new Pt(50, 0, 40) }],
        Build = new BuildIntent { Areas = [new PieceArea(0, 20, 10, 30, "build-1")] },
        WaterLanes = new WaterLaneIntent { Rects = [new PieceArea(20, 20, 24, 30, "water-lane-1")] },
        Structures = new StructureIntent
        {
            IronCubes = [new IronCube(70, 5, true, Id: "iron-1")],
            Walls = [new WallStructure(81, 0, 81, 8, 20, Id: "wall-1")],
            Chests = [new ChestMarker(79, 4, "-x", Id: "chest-1")],
        },
        Observer = new ObserverIntent { Point = new Pt(50.5, 30, 50.5) },
    };

    [Test]
    public async Task Every_piece_is_drawn_once_by_kind()
    {
        var kinds = PlayPieces.Of(Board()).Select(piece => piece.Kind).ToList();

        await Assert.That(kinds).IsEquivalentTo(PlayPieceKinds.All.Append(PlayPieceKinds.IronCube))
            .Because("the spawn's own iron cube is drawn beside the standalone one");
    }

    [Test]
    public async Task A_drawn_rectangle_moved_a_block_is_a_move_the_route_accepts()
    {
        foreach (var piece in PlayPieces.Of(Board()).Where(piece => !piece.Carried))
        {
            var placed = new Rect(piece.Rect.MinX + 1, piece.Rect.MinZ, piece.Rect.MaxX + 1, piece.Rect.MaxZ);
            var (moved, refused) = PlayPieceMove.Apply(Board(), piece.Id, null, placed);

            await Assert.That(moved).IsNotNull().Because($"{piece.Id}: {string.Join("; ", refused.Select(f => f.Message))}");
            var after = PlayPieces.Of(moved!).Single(drawn => drawn.Id == piece.Id);
            await Assert.That(after.Rect).IsEqualTo(placed);
        }
    }

    [Test]
    public async Task An_image_the_intent_does_not_store_is_carried()
    {
        var mirrored = Board() with
        {
            Symmetry = new SymmetryIntent { Mode = "mirror_x", CenterX = 100, CenterZ = 50 },
            Teams = [new TeamDef { Id = "red", Color = "red" }, new TeamDef { Id = "blue", Color = "blue" }],
        };

        var areas = PlayPieces.Of(mirrored).Where(piece => piece.Kind == PlayPieceKinds.BuildArea).ToList();

        await Assert.That(areas.Select(area => area.Carried)).IsEquivalentTo(new[] { false, true });
        await Assert.That(areas[1].Rect).IsEqualTo(new Rect(190, 20, 200, 30));
    }

    [Test]
    public async Task A_room_s_marker_is_its_point_and_moves_alone()
    {
        var marker = PlayPieces.Of(Board()).Single(piece => piece.Id == "spawn-red-marker");
        await Assert.That(marker.Rect).IsEqualTo(new Rect(4.5, 4.5, 5.5, 5.5));

        var (moved, _) = PlayPieceMove.Apply(Board(), marker.Id, null, new Rect(6.5, 2.5, 7.5, 3.5));

        var spawn = moved!.Spawns.Single();
        await Assert.That(spawn.Point).IsEqualTo(new Pt(7, 12, 3));
        await Assert.That(spawn.Protection).IsEquivalentTo(Board().Spawns[0].Protection)
            .Because("the region stays where it stood");
    }

    [Test]
    public async Task A_marker_is_held_inside_its_room()
    {
        var (moved, refused) = PlayPieceMove.Apply(Board(), "wool-red-blue-marker", null, new Rect(30, 5, 31, 6));

        await Assert.That(moved).IsNull();
        await Assert.That(refused.Single().Subjects!).Contains("wool-red-blue-marker");
    }

    [Test]
    public async Task A_marker_moves_by_whole_blocks_and_is_not_deleted_or_turned_alone()
    {
        var (offGrid, _) = PlayPieceMove.Apply(Board(), "spawn-red-marker", null, new Rect(5, 5, 6, 6));
        var (turned, _) = PlayPieceMove.Turn(Board(), "spawn-red-marker", null, 1);
        var (deleted, refused) = PlayPieceAdd.Delete(Board(), "spawn-red-marker");

        await Assert.That(offGrid).IsNull();
        await Assert.That(turned).IsNull();
        await Assert.That(deleted).IsNull();
        await Assert.That(refused.Single().Rule).IsEqualTo(RequestRules.Unreadable);
    }
}
