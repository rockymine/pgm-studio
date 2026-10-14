using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Shapes;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Tests.Compose;

/// <summary>
/// The wool arms as box fills: every wool-approach piece a filled unit carries is part- and box-labeled, the
/// wool room is emitted as a real role-bearing room, the labels survive every compose move up to
/// assembly (where the written plan drops them by design), and the family variety the fill menu admits
/// actually occurs across seeds.
/// </summary>
public sealed class WoolBoxGrowthTests
{
    private static TeamUnit? TryFill(int players, ulong seed)
    {
        var request = new ComposeRequest(players, seed: seed);
        var rng = new ComposeRng(seed);
        var env = Envelope.Derive(request, rng);
        var crossing = MidCarver.Crossing(env, splitBand: false, doubleRank: false, fine: false);
        if (TeamUnitAllocator.Allocate(env, rng, crossing) is not { } alloc) return null;
        return TeamUnitFiller.Fill(alloc, rng);
    }

    private static TeamUnit Fill(int players, ulong seedFrom)
    {
        for (var seed = seedFrom; seed < seedFrom + 20; seed++)
            if (TryFill(players, seed) is { } unit) return unit;
        throw new InvalidOperationException($"no seed in [{seedFrom}..{seedFrom + 20}) fills at {players} players");
    }

    private static (HashSet<(int, int)> Filled, HashSet<(int, int)> Room) BoxCells(
        IReadOnlyList<UnitPiece> boxPieces, string roomId)
    {
        var filled = new HashSet<(int, int)>();
        var roomCells = new HashSet<(int, int)>();
        foreach (var p in boxPieces)
            for (var x = p.Rect.X; x < p.Rect.X + p.Rect.Width; x++)
                for (var z = p.Rect.Z; z < p.Rect.Z + p.Rect.Height; z++)
                {
                    filled.Add((x, z));
                    if (p.Id == roomId) roomCells.Add((x, z));
                }
        return (filled, roomCells);
    }

    [Test]
    public async Task Every_wool_approach_piece_carries_its_part_and_box_label()
    {
        var unit = Fill(16, seedFrom: 3);
        var boxed = unit.Pieces.Where(p => p.Box.Kind == BoxKind.Wool).ToList();
        await Assert.That(boxed).IsNotEmpty();
        foreach (var p in boxed)
        {
            await Assert.That(p.Part).IsNotNull();
            // terrain ids serialize the ownership as a prefix; the room carries its role-bearing name
            if (p.Part != CompoundParts.Room)
                await Assert.That(p.Id.StartsWith(p.Box.Id)).IsTrue();
        }
        // every wool/spawn box piece carries a part + a box label
        foreach (var p in unit.Pieces.Where(p => p.Box.Kind is BoxKind.Wool or BoxKind.Spawn))
            await Assert.That(p.Part).IsNotNull();
    }

    [Test]
    public async Task The_wool_room_is_emitted_as_a_role_bearing_room()
    {
        var unit = Fill(12, seedFrom: 5);
        await Assert.That(unit.Wools).IsNotEmpty();
        foreach (var wool in unit.Wools)
        {
            var room = unit.Pieces.Single(p => p.Id == wool.Piece);
            await Assert.That(room.Role).IsEqualTo(PlanRoles.WoolRoom);
            await Assert.That(room.Part).IsEqualTo(CompoundParts.Room);
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
            foreach (var boxId in unit.Pieces.Where(p => p.Box.Kind == BoxKind.Wool).Select(p => p.Box.Id).Distinct())
            {
                var boxPieces = unit.Pieces.Where(p => p.Box.Id == boxId).ToList();
                var room = boxPieces.Single(p => p.Part == CompoundParts.Room);
                var (filled, roomCells) = BoxCells(boxPieces, room.Id);
                var (family, _) = ShapeClassifier.Classify(filled, roomCells);
                await Assert.That(family is { } read && FillMenu.For(BoxKind.Wool).Contains(read)).IsTrue();
            }
        }
    }

    [Test]
    public async Task Bent_and_branching_families_occur_across_seeds()
    {
        // the menu must actually escalate: big budgets produce more than straight lanes
        var families = new HashSet<CompoundForm>();
        for (ulong seed = 1; seed <= 30; seed++)
        {
            if (TryFill(30, seed) is not { } unit) continue;
            foreach (var boxId in unit.Pieces.Where(p => p.Box.Kind == BoxKind.Wool).Select(p => p.Box.Id).Distinct())
            {
                var boxPieces = unit.Pieces.Where(p => p.Box.Id == boxId).ToList();
                var room = boxPieces.Single(p => p.Part == CompoundParts.Room);
                var (filled, roomCells) = BoxCells(boxPieces, room.Id);
                if (ShapeClassifier.Classify(filled, roomCells).Family is { } family) families.Add(family);
            }
        }
        await Assert.That(families.Count >= 2).IsTrue();
        await Assert.That(families.Any(f => f != CompoundForm.I)).IsTrue();
    }

    [Test]
    public async Task The_spawn_box_classifies_to_its_family_and_its_slots_mirror()
    {
        // the spawn is the second box kind: its room a Spawn-role room, every piece part- and box-labeled.
        // Classify the box's own terrain and re-derive its parts — the same emit↔derive mirror the wool box
        // passes.
        var seen = new HashSet<CompoundForm>();
        for (ulong seed = 1; seed <= 20; seed++)
            foreach (var players in new[] { 12, 20, 30 })
            {
                if (TryFill(players, seed) is not { } unit) continue;
                var boxPieces = unit.Pieces.Where(p => p.Box.Kind == BoxKind.Spawn).ToList();
                if (boxPieces.Count == 0) continue;

                var room = boxPieces.Single(p => p.Part == CompoundParts.Room);
                await Assert.That(room.Role).IsEqualTo(PlanRoles.Spawn);

                var (filled, roomCells) = BoxCells(boxPieces, room.Id);
                var family = ShapeClassifier.Classify(filled, roomCells).Family ?? throw new InvalidOperationException("no ground reaches the spawn room");
                await Assert.That(FillMenu.For(BoxKind.Spawn).Contains(family)).IsTrue();

                var parts = PartAssignment.AssignParts(family, boxPieces.Select(p => (p.Id, p.Rect)).ToList(), room.Id);
                foreach (var p in boxPieces)
                    await Assert.That(parts[p.Id]).IsEqualTo(p.Part);
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
                    .Where(p => p.Id == "hub" || p.Id.StartsWith("hub-")).Min(p => p.Rect.X);
                var spawnPieces = unit.Pieces.Where(p => p.Box.Kind == BoxKind.Spawn).Select(p => p.Rect).ToList();
                if (spawnPieces.Count == 0) continue;
                seats.Add(spawnPieces.Min(r => r.X) - hubX);
            }
            await Assert.That(seats.Count >= 2).IsTrue();
        }
    }

    [Test]
    public async Task Assembled_plans_carry_no_labels()
    {
        // Assemble is the label boundary: PlanPiece has no part/box field, so the write is label-free —
        // this pins the pieces still carry their roles (the room) while nothing else leaks
        var plan = Composer.Compose(new ComposeRequest(16, teams: 2, seed: 7));
        await Assert.That(plan.Pieces.Any(p => p.Role == PlanRoles.WoolRoom)).IsTrue();
    }

    [Test]
    public async Task A_grown_donut_box_widens_the_ring_the_hole_and_the_entry()
    {
        // the donut growth knobs: a box bigger than the min is absorbed by the ring (its span derives from the
        // box), so the enclosed hole grows with it, and the sampled attachment width widens the hub entry —
        // here the caps: hole 3 (along) × 5 (deep), entry 5 wide, at laneWidth 2 / rd 2. Canonical frame: W = laneWidth
        // (attachment) + span (2·laneWidth + holeDeep) + rd = 13; H = 2·laneWidth + holeAlong = 7 (also ≥ entry + laneWidth).
        const int laneWidth = 2;
        var box = new Box("wool-a", BoxKind.Wool, new(0, 0, 13, 7), 91);
        var filled = BoxFiller.Fill(box, BoxEdge.Left, laneWidth, CompoundForm.Donut, out _, attachmentWidth: 5)!;

        var hole = filled.Vacancies.Single(v => v.Kind == "hole");
        await Assert.That(hole.Rect.Width * hole.Rect.Height).IsEqualTo(15);         // the 3×5 hole (orientation aside)
        await Assert.That(Math.Min(hole.Rect.Width, hole.Rect.Height)).IsEqualTo(3);
        var entry = filled.Pieces.Where(p => p.Part == CompoundParts.Entry).ToList();
        await Assert.That(entry.Count).IsEqualTo(1);
        await Assert.That(Math.Max(entry[0].Rect.Width, entry[0].Rect.Height)).IsEqualTo(5);   // the widened hub entry
    }
}
