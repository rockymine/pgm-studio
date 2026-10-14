using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Tests.Compose;

/// <summary>
/// The profile-driven fill spine: <see cref="FillProfiles"/> (the per-<see cref="BoxKind"/> profile as data)
/// and <see cref="BoxFiller"/> (the one profile-gated fill entry point over a positioned <see cref="Box"/>,
/// with land-vs-target accounting). This is the machinery the partitioner drives; here it is exercised
/// directly on hand-built boxes.
/// </summary>
public sealed class BoxFillerTests
{
    private const int LaneWidth = 2;
    private static Box WoolBox(int w, int h, int landTarget = 1000) => new("wool-a", BoxKind.Wool, new(0, 0, w, h), landTarget);

    // ── FillProfiles: the profile as data ────────────────────────────────────────────────────────────────

    [Test]
    public async Task Wool_profile_is_the_width_menu_spawn_profile_is_I_and_L()
    {
        await Assert.That(FillProfiles.Families(BoxKind.Wool, LaneWidth, LaneWidth)).IsEquivalentTo(FillMenu.FamiliesFor(LaneWidth, LaneWidth));
        await Assert.That(FillProfiles.Families(BoxKind.Wool, LaneWidth, LaneWidth)).Contains(ShapeFamily.Donut);   // admitted
        await Assert.That(FillProfiles.Families(BoxKind.Spawn, LaneWidth, LaneWidth)).IsEquivalentTo(SpawnBoxEmitter.Families);
        await Assert.That(FillProfiles.Families(BoxKind.Spawn, LaneWidth, LaneWidth)).IsEquivalentTo(new[] { ShapeFamily.I, ShapeFamily.L });
    }

    [Test]
    public async Task Fits_gates_on_both_the_profile_and_the_footprint()
    {
        // I is in the wool profile and tiny, so a roomy box holds it; the donut needs a big footprint
        await Assert.That(FillProfiles.Fits(BoxKind.Wool, ShapeFamily.I, LaneWidth, LaneWidth, 16, 20)).IsTrue();
        await Assert.That(FillProfiles.Fits(BoxKind.Wool, ShapeFamily.Donut, LaneWidth, LaneWidth, 16, 20)).IsTrue();
        await Assert.That(FillProfiles.Fits(BoxKind.Wool, ShapeFamily.Donut, LaneWidth, LaneWidth, 6, 6)).IsFalse();   // footprint too small
        // the scythe is a valid shape but not in the wool production profile → not admitted
        await Assert.That(FillProfiles.Fits(BoxKind.Wool, ShapeFamily.Scythe, LaneWidth, LaneWidth, 20, 20)).IsFalse();
        // U/H are not in the spawn profile
        await Assert.That(FillProfiles.Fits(BoxKind.Spawn, ShapeFamily.U, LaneWidth, LaneWidth, 20, 20)).IsFalse();
        await Assert.That(FillProfiles.Fits(BoxKind.Spawn, ShapeFamily.I, LaneWidth, LaneWidth, 6, 8)).IsTrue();
    }

    [Test]
    public async Task FittingFamilies_is_the_profile_intersect_footprint()
    {
        var big = FillProfiles.FittingFamilies(BoxKind.Wool, LaneWidth, LaneWidth, 16, 22);
        await Assert.That(big).Contains(ShapeFamily.Donut);
        // a footprint only a straight lane holds
        var tiny = FillProfiles.FittingFamilies(BoxKind.Wool, LaneWidth, LaneWidth, 2, 4);
        await Assert.That(tiny).IsEquivalentTo(new[] { ShapeFamily.I });
    }

    // ── BoxFiller: the profile-gated fill entry point ────────────────────────────────────────────────────

    [Test]
    public async Task Fill_emits_a_legal_family_and_reports_its_land()
    {
        var filled = BoxFiller.Fill(WoolBox(16, 22), BoxEdge.Top, LaneWidth, ShapeFamily.L, out var rejection);
        await Assert.That(rejection).IsNull();
        // land = every terrain cell + the room, and the family reads back as requested
        await Assert.That(BoxFiller.Land(filled!)).IsGreaterThan(0);
        await Assert.That(ShapeClassifier.Classify(UnitPieces.AsPlan(filled!, "wool-box"), "wa-room").Family)
            .IsEqualTo(ShapeFamily.L);
    }

    [Test]
    public async Task Fill_rejects_a_family_outside_the_profile()
    {
        // the scythe emits fine as a shape but is not in the wool production profile
        await Assert.That(BoxFiller.Fill(WoolBox(20, 20), BoxEdge.Top, LaneWidth, ShapeFamily.Scythe, out var rejection)).IsNull();
        await Assert.That(rejection).IsTypeOf<FillRejection.NotOnMenu>();
    }

    [Test]
    public async Task Fill_signals_too_small_rather_than_throwing()
    {
        await Assert.That(BoxFiller.Fill(WoolBox(6, 6), BoxEdge.Top, LaneWidth, ShapeFamily.Donut, out var rejection)).IsNull();
        await Assert.That(rejection).IsTypeOf<FillRejection.TooSmall>();
    }

    [Test]
    public async Task Fill_gates_the_dock_through_the_mouth_admitting_the_production_menu()
    {
        // the docking gate is wired into the fill: every production family docks cleanly through the mouth
        // it exposes its entry on, top or bottom, so a legal fill still fills. Clamp/scythe never reach the gate
        // here — the profile stops them first (NotOnMenu).
        foreach (var family in FillMenu.ProductionFamilies)
        {
            var box = WoolBox(16, 22);
            if (BoxFiller.Fill(box, BoxEdge.Top, LaneWidth, family, out var why) is null && why is FillRejection.TooSmall) continue;
            await Assert.That(BoxFiller.Fill(box, BoxEdge.Top, LaneWidth, family, out _)).IsNotNull();
            await Assert.That(BoxFiller.Fill(box, BoxEdge.Bottom, LaneWidth, family, out _)).IsNotNull();
        }
    }

    [Test]
    public async Task Fill_docks_all_four_mouths_keeping_the_family_in_the_box()
    {
        // a box docks the host on whichever edge faces it, so the filler orients the shape onto any of the four
        // mouths (Left/Right are a quarter turn). A fill already proves the entry landed on the requested mouth
        // (the gate admitted it); we also check the family survives, the fill stays in the box, and the marker
        // sits in the room.
        var box = new Box("wool-a", BoxKind.Wool, new(3, 5, 16, 20), 10_000);
        foreach (var family in new[] { ShapeFamily.I, ShapeFamily.L, ShapeFamily.Z })
            foreach (var mouth in new[] { BoxEdge.Top, BoxEdge.Bottom, BoxEdge.Left, BoxEdge.Right })
            {
                var filled = BoxFiller.Fill(box, mouth, LaneWidth, family, out _);
                await Assert.That(filled).IsNotNull();

                await Assert.That(ShapeClassifier.Classify(UnitPieces.AsPlan(filled!, "wool-box"), "wa-room").Family)
                    .IsEqualTo(family);

                // every piece stays inside the box footprint (box-local)
                foreach (var p in filled!.Pieces)
                {
                    await Assert.That(p.Rect.X >= 0 && p.Rect.Z >= 0).IsTrue();
                    await Assert.That(p.Rect.MaxX <= box.Rect.Width && p.Rect.MaxZ <= box.Rect.Height).IsTrue();
                }

                // the wool marker lands inside its room
                var room = filled.Room!.Rect;
                var (mx, mz) = filled.Marker!.Value;
                await Assert.That(mx >= room.X && mx <= room.MaxX && mz >= room.Z && mz <= room.MaxZ).IsTrue();
            }
    }

    [Test]
    public async Task A_spawn_box_docks_through_its_own_binding_not_this_one()
    {
        await Assert.That(() => BoxFiller.Fill(new Box("spawn-a", BoxKind.Spawn, new(0, 0, 6, 12), 100), BoxEdge.Top, LaneWidth, ShapeFamily.I, out _))
            .Throws<ComposeException>();
    }
}
