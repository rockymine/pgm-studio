using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Tests.Compose;

/// <summary>
/// The wool arms as box fills: every wool-approach piece a filled unit carries is slot- and box-labeled, the
/// wool room is emitted as a real role-bearing terminal, the labels survive every compose move up to
/// assembly (where the written plan drops them by design), and the family variety the fill menu admits
/// actually occurs across seeds.
/// </summary>
public sealed class WoolBoxGrowthTests
{
    private static GrownUnit? TryFill(int players, ulong seed)
    {
        var request = new ComposeRequest(players, seed: seed);
        var rng = new ComposeRng(seed);
        var env = Envelope.Derive(request, rng);
        var crossing = MidCarver.BandOnly(env);
        if (TeamUnitAllocator.Allocate(env, rng, crossing) is not { } alloc) return null;
        return TeamUnitFiller.Fill(alloc.Partition, alloc.SpawnFacing, rng)?.Unit;
    }

    private static GrownUnit Fill(int players, ulong seedFrom)
    {
        for (var seed = seedFrom; seed < seedFrom + 20; seed++)
            if (TryFill(players, seed) is { } unit) return unit;
        throw new InvalidOperationException($"no seed in [{seedFrom}..{seedFrom + 20}) fills at {players} players");
    }

    private static (HashSet<(int, int)> Filled, HashSet<(int, int)> Room) BoxCells(
        IReadOnlyList<GrownPiece> boxPieces, string roomId)
    {
        var filled = new HashSet<(int, int)>();
        var roomCells = new HashSet<(int, int)>();
        foreach (var p in boxPieces)
            for (var x = p.Rect[0]; x < p.Rect[0] + p.Rect[2]; x++)
                for (var z = p.Rect[1]; z < p.Rect[1] + p.Rect[3]; z++)
                {
                    filled.Add((x, z));
                    if (p.Id == roomId) roomCells.Add((x, z));
                }
        return (filled, roomCells);
    }

    [Test]
    public async Task Every_wool_approach_piece_carries_its_slot_and_box_label()
    {
        var unit = Fill(16, seedFrom: 3);
        var boxed = unit.Pieces.Where(p => p.Box?.Kind == BoxKind.Wool).ToList();
        await Assert.That(boxed).IsNotEmpty();
        foreach (var p in boxed)
        {
            await Assert.That(p.Slot).IsNotNull();
            // terrain ids serialize the ownership as a prefix; the room carries its role-bearing name
            if (p.Slot != ApproachSlots.Room)
                await Assert.That(p.Id.StartsWith(p.Box!.Id)).IsTrue();
        }
        // every wool/spawn box piece carries a slot + a box label
        foreach (var p in unit.Pieces.Where(p => p.Box?.Kind is BoxKind.Wool or BoxKind.Spawn))
            await Assert.That(p.Slot).IsNotNull();
    }

    [Test]
    public async Task The_wool_room_is_emitted_as_a_role_bearing_terminal()
    {
        var unit = Fill(12, seedFrom: 5);
        await Assert.That(unit.Wools).IsNotEmpty();
        foreach (var wool in unit.Wools)
        {
            var room = unit.Pieces.Single(p => p.Id == wool.Piece);
            await Assert.That(room.Role).IsEqualTo(PlanRoles.WoolRoom);
            await Assert.That(room.Slot).IsEqualTo(ApproachSlots.Room);
        }
    }

    [Test]
    public async Task A_boxed_wool_approach_classifies_to_its_emitted_family()
    {
        // the mirror inside the unit: the box's own pieces classify to the family the fill emitted — the
        // scope is the box (its piece set), not the welded unit
        for (ulong seed = 1; seed <= 12; seed++)
        {
            if (TryFill(20, seed) is not { } unit) continue;
            foreach (var boxId in unit.Pieces.Where(p => p.Box?.Kind == BoxKind.Wool).Select(p => p.Box!.Id).Distinct())
            {
                var boxPieces = unit.Pieces.Where(p => p.Box?.Id == boxId).ToList();
                var room = boxPieces.Single(p => p.Slot == ApproachSlots.Room);
                var (filled, roomCells) = BoxCells(boxPieces, room.Id);
                var (family, _) = ShapeClassifier.Classify(filled, roomCells);
                await Assert.That(FillMenu.ProductionFamilies.Contains(family)).IsTrue();
            }
        }
    }

    [Test]
    public async Task Bent_and_branching_families_occur_across_seeds()
    {
        // the menu must actually escalate: big budgets produce more than straight lanes
        var families = new HashSet<ShapeFamily>();
        for (ulong seed = 1; seed <= 30; seed++)
        {
            if (TryFill(30, seed) is not { } unit) continue;
            foreach (var boxId in unit.Pieces.Where(p => p.Box?.Kind == BoxKind.Wool).Select(p => p.Box!.Id).Distinct())
            {
                var boxPieces = unit.Pieces.Where(p => p.Box?.Id == boxId).ToList();
                var room = boxPieces.Single(p => p.Slot == ApproachSlots.Room);
                var (filled, roomCells) = BoxCells(boxPieces, room.Id);
                families.Add(ShapeClassifier.Classify(filled, roomCells).Family);
            }
        }
        await Assert.That(families.Count >= 2).IsTrue();
        await Assert.That(families.Any(f => f != ShapeFamily.I)).IsTrue();
    }

    [Test]
    public async Task The_spawn_box_classifies_to_its_family_and_its_slots_mirror()
    {
        // the spawn is the second box kind: its room a Spawn-role terminal, every piece slot- and box-labeled.
        // Classify the box's own terrain and re-derive its slots — the same emit↔derive mirror the wool box
        // passes.
        var seen = new HashSet<ShapeFamily>();
        for (ulong seed = 1; seed <= 20; seed++)
            foreach (var players in new[] { 12, 20, 30 })
            {
                if (TryFill(players, seed) is not { } unit) continue;
                var boxPieces = unit.Pieces.Where(p => p.Box?.Kind == BoxKind.Spawn).ToList();
                if (boxPieces.Count == 0) continue;

                var room = boxPieces.Single(p => p.Slot == ApproachSlots.Room);
                await Assert.That(room.Role).IsEqualTo(PlanRoles.Spawn);

                var (filled, roomCells) = BoxCells(boxPieces, room.Id);
                var family = ShapeClassifier.Classify(filled, roomCells).Family;
                await Assert.That(SpawnBoxEmitter.Families.Contains(family)).IsTrue();

                var slots = SlotAssignment.AssignSlots(family, boxPieces.Select(p => (p.Id, p.Rect)).ToList(), room.Id);
                foreach (var p in boxPieces)
                    await Assert.That(slots[p.Id]).IsEqualTo(p.Slot);
                seen.Add(family);
            }
        await Assert.That(seen).IsNotEmpty();
    }

    [Test]
    public async Task The_spawn_seats_at_varied_points_along_the_hub_back_edge()
    {
        // SP2: the spawn is not pinned to one back-edge corner — its seat is sampled, so across seeds it lands at
        // more than one offset from the hub. (Default symmetry is a z-frame, so the seat varies along x; measure
        // the spawn box's x-min relative to the hub's.) A degenerate always-same-corner spawn fails this.
        foreach (var players in new[] { 16, 24 })
        {
            var seats = new HashSet<int>();
            for (ulong seed = 1; seed <= 40; seed++)
            {
                if (TryFill(players, seed) is not { } unit) continue;
                var hubX = unit.Pieces
                    .Where(p => p.Id == "hub" || p.Id.StartsWith("hub-")).Min(p => p.Rect[0]);
                var spawnPieces = unit.Pieces.Where(p => p.Box?.Kind == BoxKind.Spawn).Select(p => p.Rect).ToList();
                if (spawnPieces.Count == 0) continue;
                seats.Add(spawnPieces.Min(r => r[0]) - hubX);
            }
            await Assert.That(seats.Count >= 2).IsTrue();
        }
    }

    [Test]
    public async Task Assembled_plans_carry_no_labels()
    {
        // Assemble is the label boundary: PlanPiece has no slot/box field, so the write is label-free —
        // this pins the pieces still carry their roles (the room) while nothing else leaks
        var plan = Composer.Compose(new ComposeRequest(16, teams: 2, seed: 7));
        await Assert.That(plan.Pieces.Any(p => p.Role == PlanRoles.WoolRoom)).IsTrue();
    }

    [Test]
    public async Task A_grown_donut_box_widens_the_ring_the_hole_and_the_entry()
    {
        // the donut growth knobs: a box bigger than the min is absorbed by the ring (its span derives from the
        // box), so the enclosed hole grows with it, and the sampled attachment width widens the hub entry —
        // here the caps: hole 3 (along) × 5 (deep), entry 5 wide, at cw 2 / rd 2. Canonical frame: W = cw
        // (attachment) + span (2·cw + holeDeep) + rd = 13; H = 2·cw + holeAlong = 7 (also ≥ entry + cw).
        const int cw = 2;
        var box = new Box("wool-a", BoxKind.Wool, [0, 0, 13, 7], 91);
        var ok = (FillResult.Ok)BoxFiller.Fill(
            box, BoxEdge.Left, cw, ShapeFamily.Donut, roomId: "wool-a-room", attachmentWidth: 5);

        var hole = ok.Vacancies.Single(v => v.Kind == "hole");
        await Assert.That(hole.Rect[2] * hole.Rect[3]).IsEqualTo(15);         // the 3×5 hole (orientation aside)
        await Assert.That(Math.Min(hole.Rect[2], hole.Rect[3])).IsEqualTo(3);
        var entry = ok.Approach.Terrain.Where(p => p.Slot == ApproachSlots.Entry).ToList();
        await Assert.That(entry.Count).IsEqualTo(1);
        await Assert.That(Math.Max(entry[0].Rect[2], entry[0].Rect[3])).IsEqualTo(5);   // the widened hub entry
    }
}
