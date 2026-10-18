using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Tests.Authoring;

/// <summary>
/// Adding a play piece from the sketch's Layout step, and deleting one. <b>A piece arrives with its whole
/// orbit</b>, every image stored under one stamp, so the orbit moves and deletes together; and <b>it stands where
/// it was placed</b>, at the rectangle the pieces read then draws it at.
/// </summary>
public sealed class PlayPieceAddTests
{
    private static readonly SymmetryIntent MirrorX = new() { Mode = "mirror_x", CenterX = 0, CenterZ = 0 };

    private static MapIntent Board() => new()
    {
        Teams = [new TeamDef { Id = "red", Name = "Red", Color = "red" }, new TeamDef { Id = "blue", Name = "Blue", Color = "blue" }],
    };

    [Test]
    public async Task A_spawn_arrives_with_its_mirror_and_faces_the_board()
    {
        var (added, id, refused) = PlayPieceAdd.Apply(Board(), PlayPieceKinds.Spawn, new Rect(-40, -6, -30, 6), board: MirrorX);

        await Assert.That(refused.Count).IsEqualTo(0);
        await Assert.That(added!.Spawns.Select(spawn => spawn.Team)).IsEquivalentTo(new[] { "red", "blue" });
        var red = added.Spawns.Single(spawn => spawn.Id == id);
        await Assert.That(red.Doors.Select(door => door.Edge)).IsEquivalentTo(new[] { "+x" }).Because("the middle of the board is in +x of it");
        await Assert.That(added.Spawns[1].Protection.Single()).IsEqualTo(new Rect(30, -6, 40, 6));
        await Assert.That(added.Spawns[1].Stamp.Identity).IsEqualTo(red.Stamp.Identity);
    }

    [Test]
    public async Task A_spawn_goes_to_a_team_with_none_and_is_refused_once_every_team_has_one()
    {
        var oneSided = Board() with { Spawns = [new SpawnIntent { Id = "spawn-red", Team = "red", Protection = [new Rect(-40, -6, -30, 6)] }] };
        var (added, id, _) = PlayPieceAdd.Apply(oneSided, PlayPieceKinds.Spawn, new Rect(30, -6, 40, 6));
        await Assert.That(added!.Spawns.Single(spawn => spawn.Id == id).Team).IsEqualTo("blue");

        var (full, _, why) = PlayPieceAdd.Apply(added, PlayPieceKinds.Spawn, new Rect(0, 30, 10, 40));
        await Assert.That(full).IsNull();
        await Assert.That(why.Single().Field).IsEqualTo("kind");
    }

    [Test]
    public async Task A_teams_second_wool_takes_a_dye_no_wool_has()
    {
        var (once, _, _) = PlayPieceAdd.Apply(Board(), PlayPieceKinds.WoolRoom, new Rect(-20, 10, -10, 20), board: MirrorX);
        var (twice, _, _) = PlayPieceAdd.Apply(once!, PlayPieceKinds.WoolRoom, new Rect(-20, -20, -10, -10), board: MirrorX);

        var colours = twice!.Wools!.Select(wool => wool.Color).ToList();
        await Assert.That(colours.Take(2)).IsEquivalentTo(new[] { "", "" }).Because("a team's first wool is its own colour");
        await Assert.That(colours.Skip(2).Distinct().Count()).IsEqualTo(2);
        await Assert.That(colours.Skip(2).All(color => PieceDefaults.Dyes.Contains(color))).IsTrue();
    }

    [Test]
    public async Task An_added_piece_stands_where_it_was_placed()
    {
        var placed = new Rect(-12, 4, -11, 5);
        foreach (var kind in new[] { PlayPieceKinds.Destroyable, PlayPieceKinds.Core, PlayPieceKinds.IronCube })
        {
            var (added, id, _) = PlayPieceAdd.Apply(Board(), kind, placed, board: MirrorX);
            var drawn = PlayPieces.Of(added!).Single(piece => piece.Id == id).Rect;

            await Assert.That(drawn.MinX <= placed.MinX && drawn.MaxX >= placed.MaxX
                              && drawn.MinZ <= placed.MinZ && drawn.MaxZ >= placed.MaxZ).IsTrue()
                .Because($"{kind} is drawn over the block it was placed on, at {drawn}");
        }

        var (areas, area, _) = PlayPieceAdd.Apply(Board(), PlayPieceKinds.BuildArea, new Rect(-30, -4, -20, 4), board: MirrorX);
        await Assert.That(PlayPieces.Of(areas!).Single(piece => piece.Id == area).Rect).IsEqualTo(new Rect(-30, -4, -20, 4));
        await Assert.That(areas!.Build!.Areas.Count).IsEqualTo(2);
    }

    [Test]
    public async Task An_area_across_the_axis_is_its_own_image()
    {
        var (added, _, _) = PlayPieceAdd.Apply(Board(), PlayPieceKinds.BuildArea, new Rect(-5, -4, 5, 4), board: MirrorX);

        await Assert.That(added!.Build!.Areas.Count).IsEqualTo(1);
    }

    [Test]
    public async Task A_wall_is_a_line_between_its_ends_and_its_mirror_is_the_mirrored_line()
    {
        var (added, id, _) = PlayPieceAdd.Wall(Board(), -9, 0, -9, 8, board: MirrorX);

        await Assert.That(added!.Structures!.Walls.Select(wall => (wall.X1, wall.Z1, wall.X2, wall.Z2)))
            .IsEquivalentTo(new[] { (-9, 0, -9, 8), (9, 0, 9, 8) });
        await Assert.That(added.Structures.Walls[0].Id).IsEqualTo(id);
        await Assert.That(PlayPieceAdd.Wall(Board(), 3, 3, 3, 3).Intent).IsNull();
    }

    [Test]
    public async Task A_wall_is_not_placed_as_a_rectangle()
    {
        var (added, _, why) = PlayPieceAdd.Apply(Board(), PlayPieceKinds.Wall, new Rect(-10, 0, -8, 8), board: MirrorX);

        await Assert.That(added).IsNull();
        await Assert.That(why.Single().Field).IsEqualTo("kind");
    }

    [Test]
    public async Task A_chest_beside_a_wall_fronts_away_from_it_and_its_mirror_the_other_way()
    {
        var (walled, _, _) = PlayPieceAdd.Wall(Board(), -9, 0, -9, 8, board: MirrorX);
        var (added, _, _) = PlayPieceAdd.Apply(walled!, PlayPieceKinds.Chest, new Rect(-11, 4, -10, 5), board: MirrorX);

        await Assert.That(added!.Structures!.Chests.Select(chest => (chest.X, chest.Z, chest.Facing)))
            .IsEquivalentTo(new[] { (-11, 4, "-x"), (10, 4, "+x") });
    }

    [Test]
    public async Task Deleting_a_piece_deletes_its_orbit_unless_alone()
    {
        var (added, id, _) = PlayPieceAdd.Apply(Board(), PlayPieceKinds.Spawn, new Rect(-40, -6, -30, 6), board: MirrorX);

        var (gone, _) = PlayPieceAdd.Delete(added!, id, board: MirrorX);
        var (one, _) = PlayPieceAdd.Delete(added!, id, alone: true, board: MirrorX);

        await Assert.That(gone!.Spawns).IsEmpty();
        await Assert.That(one!.Spawns.Single().Team).IsEqualTo("blue");
    }

    [Test]
    public async Task A_second_observer_and_an_unknown_team_are_refused()
    {
        var withObserver = Board() with { Observer = new ObserverIntent { Point = new Pt(0.5, 30, 0.5) } };
        var (observer, _, why) = PlayPieceAdd.Apply(withObserver, PlayPieceKinds.Observer, new Rect(4, 4, 5, 5));
        var (team, _, which) = PlayPieceAdd.Apply(Board(), PlayPieceKinds.Spawn, new Rect(0, 0, 8, 8), team: "green");

        await Assert.That(observer).IsNull();
        await Assert.That(why.Single().Field).IsEqualTo("kind");
        await Assert.That(team).IsNull();
        await Assert.That(which.Single().Field).IsEqualTo("team");
    }
}
