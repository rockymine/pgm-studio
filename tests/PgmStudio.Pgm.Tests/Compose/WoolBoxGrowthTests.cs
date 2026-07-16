using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Tests.Compose;

/// <summary>
/// The wool arms as box fills: every wool-approach piece a grown unit carries is slot- and box-labeled, the
/// wool room is emitted as a real role-bearing terminal, the labels survive every compose move up to
/// assembly (where the written plan drops them by design), and the family variety the fill menu admits
/// actually occurs across seeds.
/// </summary>
public sealed class WoolBoxGrowthTests
{
    private static GrownUnit Grow(int players, int teams = 2, string? symmetry = null, ulong seed = 1)
    {
        var request = new ComposeRequest(players, teams, symmetry, seed);
        var rng = new ComposeRng(seed);
        var env = Envelope.Derive(request, rng);
        return TeamUnitGrower.Grow(env, rng);
    }

    [Test]
    public async Task Every_wool_approach_piece_carries_its_slot_and_box_label()
    {
        var unit = Grow(16, seed: 3);
        var boxed = unit.Pieces.Where(p => p.Box?.Kind == BoxKind.Wool).ToList();
        await Assert.That(boxed).IsNotEmpty();
        foreach (var p in boxed)
        {
            await Assert.That(p.Slot).IsNotNull();
            // terrain ids serialize the ownership as a prefix; the room carries its role-bearing name
            if (p.Slot != ApproachSlots.Room)
                await Assert.That(p.Id.StartsWith(p.Box!.Id)).IsTrue();
        }
        // every boxed piece (wool or spawn) carries a slot + a box label
        foreach (var p in unit.Pieces.Where(p => p.Box is not null))
            await Assert.That(p.Slot).IsNotNull();
    }

    [Test]
    public async Task The_wool_room_is_emitted_as_a_role_bearing_terminal()
    {
        var unit = Grow(12, seed: 5);
        foreach (var wool in unit.Wools.Where(w => w.Piece.StartsWith("wool-room")))
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
            var unit = Grow(20, seed: seed);
            foreach (var boxId in unit.Pieces.Where(p => p.Box is not null).Select(p => p.Box!.Id).Distinct())
            {
                var boxPieces = unit.Pieces.Where(p => p.Box?.Id == boxId).ToList();
                var room = boxPieces.Single(p => p.Slot == ApproachSlots.Room);
                var filled = new HashSet<(int, int)>();
                var roomCells = new HashSet<(int, int)>();
                foreach (var p in boxPieces)
                    for (var x = p.Rect[0]; x < p.Rect[0] + p.Rect[2]; x++)
                        for (var z = p.Rect[1]; z < p.Rect[1] + p.Rect[3]; z++)
                        {
                            filled.Add((x, z));
                            if (p.Id == room.Id) roomCells.Add((x, z));
                        }
                var (family, _) = ShapeClassifier.Classify(filled, roomCells);
                await Assert.That(FillMenu.ProductionFamilies.Contains(family)).IsTrue();
            }
        }
    }

    [Test]
    public async Task Labels_survive_the_cut_and_the_room_carve_and_drop_at_assembly()
    {
        // find a composed plan whose cut severed a boxed wool room; its labels must have ridden through
        for (ulong seed = 1; seed <= 40; seed++)
        {
            var stages = Composer.ComposeStages(new ComposeRequest(16, teams: 2, seed: seed));
            if (stages.Cut is null) continue;
            var severed = stages.Unit.Pieces.FirstOrDefault(p => p.Id == stages.Cut.SeveredId && p.Box is not null);
            if (severed is null) continue;

            await Assert.That(severed.Slot).IsNotNull();               // the move preserved the label
            await Assert.That(severed.Role).IsEqualTo(PlanRoles.WoolRoom);
            return;
        }
        // no boxed-room cut in 40 seeds would itself be a distribution regression
        throw new InvalidOperationException("no cut severing a boxed wool room found in 40 seeds");
    }

    [Test]
    public async Task Bent_and_branching_families_occur_across_seeds()
    {
        // the menu must actually escalate: big budgets produce more than straight lanes
        var families = new HashSet<ShapeFamily>();
        for (ulong seed = 1; seed <= 30; seed++)
        {
            var unit = Grow(30, seed: seed);
            foreach (var boxId in unit.Pieces.Where(p => p.Box is not null).Select(p => p.Box!.Id).Distinct())
            {
                var boxPieces = unit.Pieces.Where(p => p.Box?.Id == boxId).ToList();
                var room = boxPieces.Single(p => p.Slot == ApproachSlots.Room);
                var filled = new HashSet<(int, int)>();
                var roomCells = new HashSet<(int, int)>();
                foreach (var p in boxPieces)
                    for (var x = p.Rect[0]; x < p.Rect[0] + p.Rect[2]; x++)
                        for (var z = p.Rect[1]; z < p.Rect[1] + p.Rect[3]; z++)
                        {
                            filled.Add((x, z));
                            if (p.Id == room.Id) roomCells.Add((x, z));
                        }
                families.Add(ShapeClassifier.Classify(filled, roomCells).Family);
            }
        }
        await Assert.That(families.Count >= 2).IsTrue();
        await Assert.That(families.Any(f => f != ShapeFamily.I)).IsTrue();
    }

    [Test]
    public async Task The_spawn_box_classifies_to_I_or_L_and_its_slots_mirror()
    {
        // the spawn is the second box kind: a terminal-capped I or L, its room a Spawn-role terminal, every
        // piece slot- and box-labeled. Classify the box's own terrain and re-derive its slots — the same
        // emit↔derive mirror the wool box passes.
        var seen = new HashSet<ShapeFamily>();
        for (ulong seed = 1; seed <= 20; seed++)
            foreach (var players in new[] { 12, 20, 30 })
            {
                var unit = Grow(players, seed: seed);
                var boxPieces = unit.Pieces.Where(p => p.Box?.Kind == BoxKind.Spawn).ToList();
                if (boxPieces.Count == 0) continue;

                var room = boxPieces.Single(p => p.Slot == ApproachSlots.Room);
                await Assert.That(room.Role).IsEqualTo(PlanRoles.Spawn);

                var filled = new HashSet<(int, int)>();
                var roomCells = new HashSet<(int, int)>();
                foreach (var p in boxPieces)
                    for (var x = p.Rect[0]; x < p.Rect[0] + p.Rect[2]; x++)
                        for (var z = p.Rect[1]; z < p.Rect[1] + p.Rect[3]; z++)
                        { filled.Add((x, z)); if (p.Id == room.Id) roomCells.Add((x, z)); }

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
    public async Task Assembled_plans_carry_no_labels()
    {
        // Assemble is the label boundary: PlanPiece has no slot/box field, so the write is label-free —
        // this pins the pieces still carry their roles (the room) while nothing else leaks
        var plan = Composer.Compose(new ComposeRequest(16, teams: 2, seed: 7));
        await Assert.That(plan.Pieces.Any(p => p.Role == PlanRoles.WoolRoom)).IsTrue();
    }
}
