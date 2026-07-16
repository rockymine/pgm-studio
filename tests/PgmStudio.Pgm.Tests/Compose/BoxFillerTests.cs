using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Tests.Compose;

/// <summary>
/// The profile-driven fill spine: <see cref="FillProfiles"/> (the per-<see cref="BoxKind"/> profile as data)
/// and <see cref="BoxFiller"/> (the one profile-gated fill entry point over a positioned <see cref="Box"/>,
/// with land-vs-target accounting). This is the machinery the partitioner (G63) drives; here it is exercised
/// directly on hand-built boxes.
/// </summary>
public sealed class BoxFillerTests
{
    private const int Cw = 2;
    private static Box WoolBox(int w, int h, int landTarget = 1000) => new("wool-a", BoxKind.Wool, [0, 0, w, h], landTarget);

    // ── FillProfiles: the profile as data ────────────────────────────────────────────────────────────────

    [Test]
    public async Task Wool_profile_is_the_width_menu_spawn_profile_is_I_and_L()
    {
        await Assert.That(FillProfiles.Families(BoxKind.Wool, Cw)).IsEquivalentTo(FillMenu.FamiliesFor(Cw));
        await Assert.That(FillProfiles.Families(BoxKind.Wool, Cw)).Contains(ShapeFamily.Donut);   // admitted since G79
        await Assert.That(FillProfiles.Families(BoxKind.Spawn, Cw)).IsEquivalentTo(SpawnBoxEmitter.Families);
        await Assert.That(FillProfiles.Families(BoxKind.Spawn, Cw)).IsEquivalentTo(new[] { ShapeFamily.I, ShapeFamily.L });
    }

    [Test]
    public async Task Fits_gates_on_both_the_profile_and_the_footprint()
    {
        // I is in the wool profile and tiny, so a roomy box holds it; the donut needs a big footprint
        await Assert.That(FillProfiles.Fits(BoxKind.Wool, ShapeFamily.I, Cw, 16, 20)).IsTrue();
        await Assert.That(FillProfiles.Fits(BoxKind.Wool, ShapeFamily.Donut, Cw, 16, 20)).IsTrue();
        await Assert.That(FillProfiles.Fits(BoxKind.Wool, ShapeFamily.Donut, Cw, 6, 6)).IsFalse();   // footprint too small
        // the scythe is a valid shape but not in the wool production profile → not admitted
        await Assert.That(FillProfiles.Fits(BoxKind.Wool, ShapeFamily.Scythe, Cw, 20, 20)).IsFalse();
        // U/H are not in the spawn profile
        await Assert.That(FillProfiles.Fits(BoxKind.Spawn, ShapeFamily.U, Cw, 20, 20)).IsFalse();
        await Assert.That(FillProfiles.Fits(BoxKind.Spawn, ShapeFamily.I, Cw, 6, 8)).IsTrue();
    }

    [Test]
    public async Task FittingFamilies_is_the_profile_intersect_footprint()
    {
        var big = FillProfiles.FittingFamilies(BoxKind.Wool, Cw, 16, 22);
        await Assert.That(big).Contains(ShapeFamily.Donut);
        // a footprint only a straight lane holds
        var tiny = FillProfiles.FittingFamilies(BoxKind.Wool, Cw, 2, 4);
        await Assert.That(tiny).IsEquivalentTo(new[] { ShapeFamily.I });
    }

    // ── BoxFiller: the profile-gated fill entry point ────────────────────────────────────────────────────

    [Test]
    public async Task Fill_emits_a_legal_family_and_reports_its_land()
    {
        var box = WoolBox(16, 22);
        var res = BoxFiller.Fill(box, BoxEdge.Top, Cw, ShapeFamily.L);
        await Assert.That(res).IsTypeOf<FillResult.Ok>();
        var ok = (FillResult.Ok)res;
        // land = every terrain cell + the room, and the family reads back as requested
        var land = BoxFiller.Land(ok.Approach);
        await Assert.That(land).IsGreaterThan(0);
        await Assert.That(ShapeClassifier.Classify(WoolBoxEmitter.AsPlan(ok.Approach), ok.Approach.WoolRoom.Id).Family)
            .IsEqualTo(ShapeFamily.L);
    }

    [Test]
    public async Task Fill_rejects_a_family_outside_the_profile()
    {
        // the scythe emits fine as a shape but is not in the wool production profile
        var res = BoxFiller.Fill(WoolBox(20, 20), BoxEdge.Top, Cw, ShapeFamily.Scythe);
        await Assert.That(res).IsTypeOf<FillResult.NoFamilyFits>();
    }

    [Test]
    public async Task Fill_signals_too_small_rather_than_throwing()
    {
        var res = BoxFiller.Fill(WoolBox(6, 6), BoxEdge.Top, Cw, ShapeFamily.Donut);
        await Assert.That(res).IsTypeOf<FillResult.TooSmall>();
    }

    [Test]
    public async Task Roll_fill_picks_a_fitting_family()
    {
        var box = WoolBox(16, 22);
        var fitting = BoxFiller.FittingFamilies(box, BoxEdge.Top, Cw);
        await Assert.That(fitting).IsNotEmpty();
        for (var roll = 0; roll < fitting.Count; roll++)
        {
            var res = BoxFiller.Fill(box, BoxEdge.Top, Cw, roll);
            await Assert.That(res).IsTypeOf<FillResult.Ok>();
        }
    }

    [Test]
    public async Task Land_target_is_the_two_currency_check()
    {
        var box = WoolBox(16, 22, landTarget: 1);   // an impossibly low target
        var ok = (FillResult.Ok)BoxFiller.Fill(box, BoxEdge.Top, Cw, ShapeFamily.I);
        await Assert.That(BoxFiller.WithinLandTarget(box, ok.Approach)).IsFalse();   // over target → fragment's job (G63)
        var roomy = WoolBox(16, 22, landTarget: 10_000);
        await Assert.That(BoxFiller.WithinLandTarget(roomy, ok.Approach)).IsTrue();
    }

    [Test]
    public async Task A_spawn_box_docks_through_its_own_binding_not_this_one()
    {
        await Assert.That(() => BoxFiller.Fill(new Box("spawn-a", BoxKind.Spawn, [0, 0, 6, 12], 100), BoxEdge.Top, Cw, ShapeFamily.I))
            .Throws<ComposeException>();
    }
}
