using PgmStudio.Geom;
using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Tests.Shapes;

/// <summary>
/// An emitted compound ends in its room: the room is the last piece, its part is <see cref="CompoundParts.Room"/>, the
/// marker stands inside it, and <see cref="Compound.WithoutRoom"/> is every other piece with the vacancies unchanged.
/// </summary>
public sealed class CompoundTests
{
    private static async Task AssertEndsInItsRoom(Compound emit)
    {
        var room = emit.Room;
        await Assert.That(room).IsNotNull();
        await Assert.That(emit.Pieces[^1]).IsEqualTo(room);
        await Assert.That(emit.Pieces.Count(p => p.Part == CompoundParts.Room)).IsEqualTo(1);

        var (x, z) = emit.Marker!.Value;
        var r = room!.Rect;
        await Assert.That(x >= r.X && x <= r.MaxX && z >= r.Z && z <= r.MaxZ).IsTrue();

        var ground = emit.WithoutRoom();
        await Assert.That(ground.Pieces.SequenceEqual(emit.Pieces.Take(emit.Pieces.Count - 1))).IsTrue();
        await Assert.That(ground.Room).IsNull();
        await Assert.That(ground.Marker).IsNull();
        await Assert.That(ground.Vacancies.SequenceEqual(emit.Vacancies)).IsTrue();
    }

    [Test]
    [Arguments(ShapeFamily.I)]
    [Arguments(ShapeFamily.L)]
    [Arguments(ShapeFamily.Z)]
    [Arguments(ShapeFamily.Scythe)]
    [Arguments(ShapeFamily.Clamp)]
    [Arguments(ShapeFamily.U)]
    [Arguments(ShapeFamily.H)]
    [Arguments(ShapeFamily.Donut)]
    public async Task Every_family_ends_in_its_room(ShapeFamily family)
    {
        foreach (var laneWidth in new[] { 2, 3 })
            foreach (var (W, H) in new[] { (16, 22), (18, 26) })
                foreach (var flip in new[] { false, true })
                {
                    Compound emit;
                    try { emit = ShapeEmitter.Emit(family, W, H, laneWidth, flip); }
                    catch (ArgumentException) { continue; }          // too small for these dims — skip
                    await AssertEndsInItsRoom(emit);
                }
    }

    // the knobs (side-tuck, donut attachments/extend/at-end, scythe shift) keep the room last too
    [Test]
    public async Task The_placement_knobs_keep_the_room_last()
    {
        const int laneWidth = 2;
        Compound[] emits =
        [
            ShapeEmitter.Emit(ShapeFamily.I, 16, 22, laneWidth, roomPlacement: RoomPlacement.SideTuck),
            ShapeEmitter.Emit(ShapeFamily.Donut, 18, 16, laneWidth, attachments: 2),
            ShapeEmitter.Emit(ShapeFamily.Donut, 18, 14, laneWidth, woolExtend: true),
            ShapeEmitter.Emit(ShapeFamily.Donut, 18, 14, laneWidth, woolAtEnd: true),
            ShapeEmitter.Emit(ShapeFamily.Scythe, 18, 16, laneWidth, entryShift: 3),
        ];
        foreach (var emit in emits) await AssertEndsInItsRoom(emit);
    }

    // a turn moves the marker with the room it stands in
    [Test]
    public async Task A_turn_keeps_the_marker_inside_the_room()
    {
        var emit = ShapeEmitter.Emit(ShapeFamily.L, 15, 18, 3);
        foreach (var turn in Enum.GetValues<BoxTurn>())
        {
            var (width, height) = turn.Extent(15, 18);
            var turned = emit.Turned(turn, 15, 18);
            var (x, z) = turned.Marker!.Value;
            var r = turned.Room!.Rect;
            await Assert.That(x > r.X && x < r.MaxX && z > r.Z && z < r.MaxZ).IsTrue().Because(turn.ToString());
            await Assert.That(r.MaxX <= width && r.MaxZ <= height).IsTrue().Because(turn.ToString());
        }
    }
}
