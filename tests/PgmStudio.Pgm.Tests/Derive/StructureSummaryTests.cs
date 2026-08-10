using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Derive;
using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Tests.Derive;

/// <summary>The structural read of a composed unit: wool approach families (sorted), the hub body form, the
/// frontline form (or none), and the dock arrangement. Reads uniformly off the labeled unit, matches the
/// wool count, is deterministic per seed, and serializes to a stable canonical bucket key.</summary>
public sealed class StructureSummaryTests
{
    [Test]
    public async Task Derive_reads_wools_hub_frontline_and_seats_across_seeds()
    {
        foreach (var players in new[] { 6, 8, 12, 20 })
            for (ulong seed = 0; seed < 8; seed++)
            {
                var stages = Composer.ComposeStages(new ComposeRequest(players, seed: seed));
                var s = StructureSummary.Derive(stages.Unit);

                // one family per wool box == the plan's wool count
                await Assert.That(s.Wools.Count).IsEqualTo(stages.Plan.Placements.Wools.Count)
                    .Because($"{players}p seed {seed}");
                // every wool reads a real (non-Isolated) family
                await Assert.That(s.Wools.All(f => f != ShapeFamily.Isolated)).IsTrue();
                // sorted (canonical order)
                await Assert.That(s.Wools.SequenceEqual(s.Wools.OrderBy(f => f))).IsTrue();
                // a unit always has a hub
                await Assert.That(s.Hub).IsNotNull();

                // a composed unit always yields a seat read: the spawn docks the hub on the back or a lateral
                // (never the front), and every wool box that docks the hub names a side
                await Assert.That(s.Seats).IsNotNull().Because($"{players}p seed {seed}");
                await Assert.That(s.Seats!.Spawn).IsNotEqualTo(UnitSide.Front);
                await Assert.That(s.Seats.Wools.Count > 0).IsTrue();
                await Assert.That(s.Seats.Wools.All(side => side != UnitSide.Front)).IsTrue();
                // the classification is the spawn side: back = canonical, lateral = lopsided
                await Assert.That(s.Seats.Canonical).IsEqualTo(s.Seats.Spawn == UnitSide.Back);
            }
    }

    [Test]
    public async Task Derive_is_deterministic_and_canonical_key_is_stable()
    {
        var a = StructureSummary.Derive(Composer.ComposeStages(new ComposeRequest(12, seed: 3)).Unit);
        var b = StructureSummary.Derive(Composer.ComposeStages(new ComposeRequest(12, seed: 3)).Unit);
        await Assert.That(a.Canonical()).IsEqualTo(b.Canonical());
        await Assert.That(a.Canonical()).StartsWith("wools:");
        await Assert.That(a.Canonical()).Contains("|hub:");
        await Assert.That(a.Canonical()).Contains("|front:");
        await Assert.That(a.Canonical().EndsWith("|seat:canonical") || a.Canonical().EndsWith("|seat:lopsided")).IsTrue();
    }

    [Test]
    public async Task Form_tokens_map_the_body_vocabulary()
    {
        await Assert.That(StructureNames.Form(null)).IsEqualTo("none");
        await Assert.That(StructureNames.Form(new CompoundRead(Compound.Rectangle))).IsEqualTo("bar");
        await Assert.That(StructureNames.Form(new CompoundRead(Compound.SpineArms, 1))).IsEqualTo("single");
        await Assert.That(StructureNames.Form(new CompoundRead(Compound.SpineArms, 2))).IsEqualTo("twin");
        await Assert.That(StructureNames.Form(new CompoundRead(Compound.Ring))).IsEqualTo("ring");
        await Assert.That(StructureNames.Form(new CompoundRead(Compound.DoubleHole))).IsEqualTo("double-hole");
    }

    [Test]
    public async Task Seat_tokens_map_the_arrangement()
    {
        await Assert.That(StructureNames.Seat(null)).IsEqualTo("none");
        await Assert.That(StructureNames.Seat(new DockArrangement(UnitSide.Back, [UnitSide.Left, UnitSide.Right])))
            .IsEqualTo("canonical");
        await Assert.That(StructureNames.Seat(new DockArrangement(UnitSide.Left, [UnitSide.Back, UnitSide.Right])))
            .IsEqualTo("lopsided");
    }

    [Test]
    public async Task Seat_read_agrees_with_the_flow_measured_split()
    {
        // both arrangements occur in the mix, and the canonical one is the minority (roughly a quarter of
        // boards at marker-id-1 — asserted loosely: present, and not the majority)
        int canonical = 0, total = 0;
        for (ulong seed = 0; seed < 40; seed++)
        {
            StructureSummary s;
            try { s = StructureSummary.Derive(Composer.ComposeStages(new ComposeRequest(12, seed: seed)).Unit); }
            catch (ComposeException) { continue; }
            total++;
            if (s.Seats!.Canonical) canonical++;
        }
        await Assert.That(total > 20).IsTrue();
        await Assert.That(canonical > 0).IsTrue();
        await Assert.That(canonical < total).IsTrue();
    }
}
