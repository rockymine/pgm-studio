using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Plan;

namespace PgmStudio.Pgm.Tests.Compose;

/// <summary>
/// The producibility read: "could the composer have produced this box?".
///
/// <para>The load-bearing gate is <see cref="Every_box_the_composer_produces_reads_producible"/> — if the search
/// cannot reproduce what the composer itself just emitted, the search is wrong, and no amount of correct-looking
/// output on hand-authored plans would show it. The rest assert that a refusal is <b>directed</b>: an authored
/// plan that the validator scores clean still reports why the machine could not build it.</para>
/// </summary>
public sealed class ProducibilityTests
{
    /// <summary>The reproduction gate. Every box of every composed board must be reachable by enumerating the
    /// declared parameter menus and calling the real emitters — the property that makes an "unproducible" verdict
    /// mean anything.</summary>
    [Test]
    public async Task Every_box_the_composer_produces_reads_producible()
    {
        var checkedBoxes = 0;
        for (ulong seed = 1; seed <= 10; seed++)
        {
            ComposedStages stages;
            try { stages = Composer.ComposeStages(new ComposeRequest(12, 2, "rot_180", seed)); }
            catch (ComposeException) { continue; }

            var plan = stages.Plan;
            PlanBoxAnnotation.Apply(plan, stages.Unit);
            await Assert.That(plan.Boxes).IsNotEmpty();

            var read = Producibility.ReadPlan(plan);
            foreach (var b in read.Boxes)
            {
                // the mid box is carved rather than emitted from a form menu, so it has no candidates to match
                if (b.Kind == PlanBoxKinds.Mid) continue;
                checkedBoxes++;
                await Assert.That(b.IsProducible).IsTrue()
                    .Because($"seed {seed} {b.BoxId} ({b.Kind}, reads {b.Identity}) came out of the " +
                             $"composer, so a tuple must reproduce it — nearest was " +
                             $"{b.Nearest?.Label ?? "none"} at {b.Nearest?.DifferingCells ?? -1} cells");
            }
            // the other half of the gate: the composer's own arrangement must clear the unit-level rules too. A
            // unit finding here would mean the check disagrees with the allocator that produced the board.
            await Assert.That(read.Unit).IsEmpty()
                .Because($"seed {seed}: {string.Join("; ", read.Unit.Select(f => f.Code))}");
        }
        await Assert.That(checkedBoxes).IsGreaterThan(20).Because("the gate must actually have covered boxes");
    }

    [Test]
    public async Task A_producible_box_names_the_tuple_that_makes_it()
    {
        var stages = Composer.ComposeStages(new ComposeRequest(12, 2, "rot_180", 3));
        PlanBoxAnnotation.Apply(stages.Plan, stages.Unit);
        var hub = Producibility.Read(stages.Plan).First(r => r.Kind == PlanBoxKinds.Hub);
        await Assert.That(hub.Producible).IsNotNull();
        await Assert.That(hub.Producible!.Label).IsNotEmpty();
        await Assert.That(hub.Producible.Cw).IsGreaterThan(0);
        await Assert.That(hub.Nearest).IsNull().Because("a reproduced box needs no nearest miss");
    }

    /// <summary>A sub-minimum corridor is reported as the measurement it is, against the constant the emitters
    /// read — the half-scale trace case. The g-hub exemplar's boxes are drawn one cell wide where the composer
    /// builds at two.</summary>
    [Test]
    public async Task A_sub_minimum_corridor_is_measured_and_cited()
    {
        var plan = PlanModel.Parse(PlanTestSupport.ReadSeed("shifted-u-frontline-attach-g-hub.plan.json"))!;
        var reads = Producibility.Read(plan);
        await Assert.That(reads).IsNotEmpty();
        await Assert.That(reads.All(r => !r.IsProducible)).IsTrue();

        var hub = reads.First(r => r.Kind == PlanBoxKinds.Hub);
        var narrow = hub.Findings.FirstOrDefault(f => f.Code == "corridor-below-minimum");
        await Assert.That(narrow).IsNotNull();
        await Assert.That(narrow!.Cites).IsEqualTo("G2");
        await Assert.That(narrow.Detail).Contains("1 cell");
    }

    /// <summary>The unequal-wall ring: the hole-hub exemplar's hub has one wall 2 cells and the other 3, while
    /// <c>BodyEmitter.Ring</c> takes a single wall width. The nearest miss localises it to the two cells that
    /// leave the parameter space — which is what makes the report actionable rather than just negative.</summary>
    [Test]
    public async Task An_unequal_walled_ring_reports_the_nearest_ring_and_the_cells_that_differ()
    {
        var plan = PlanModel.Parse(PlanTestSupport.ReadSeed("shifted-u-frontline-attach-hole-hub.plan.json"))!;
        var hub = Producibility.Read(plan).First(r => r.Kind == PlanBoxKinds.Hub);

        await Assert.That(hub.Identity).Contains("Ring").Because("topologically it is a ring");
        await Assert.That(hub.IsProducible).IsFalse().Because("no ring has two different wall widths");
        await Assert.That(hub.Nearest).IsNotNull();
        await Assert.That(hub.Nearest!.Label).Contains("Ring");
        // the whole discrepancy is the one over-wide wall: a small, localised diff, not a shape mismatch
        await Assert.That(hub.Nearest.DifferingCells).IsLessThanOrEqualTo(4);
        await Assert.That(hub.Findings.Any(f => f.Code == "no-parameters-reproduce")).IsTrue();
    }

    /// <summary>Identity is a hint, not a verdict: the classifiers read topology, so a shape whose walls are too
    /// thin to emit still reads as the compound it is. The two must be free to disagree.</summary>
    [Test]
    public async Task Identity_and_producibility_are_separate_answers()
    {
        var plan = PlanModel.Parse(PlanTestSupport.ReadSeed("shifted-u-frontline-attach-g-hub.plan.json"))!;
        var hub = Producibility.Read(plan).First(r => r.Kind == PlanBoxKinds.Hub);
        await Assert.That(hub.Identity).Contains("G").Because("an enclosed hole plus a bay reads as a G");
        await Assert.That(hub.IsProducible).IsFalse().Because("no G is emittable at a 1-cell wall width");
    }

    /// <summary>A box too small for anything on the menu collapses to one finding naming the smallest footprint
    /// that would fit, rather than one line per mouth orientation.</summary>
    [Test]
    public async Task A_box_too_small_for_every_form_reports_the_smallest_that_fits_once()
    {
        var plan = PlanModel.Parse(PlanTestSupport.ReadSeed("shifted-u-frontline-attach-g-hub.plan.json"))!;
        var spawn = Producibility.Read(plan).First(r => r.Kind == PlanBoxKinds.Spawn);
        var tooSmall = spawn.Findings.Where(f => f.Code == "box-too-small").ToList();
        await Assert.That(tooSmall.Count).IsEqualTo(1);
        await Assert.That(tooSmall[0].Detail).Contains("2x2");
        await Assert.That(spawn.Nearest).IsNull().Because("nothing emitted, so there is nothing to diff");
    }

    [Test]
    public async Task An_empty_box_says_so_rather_than_claiming_unproducible()
    {
        var plan = new PlanModel();
        plan.Boxes.Add(new PlanBox { Id = "b", Kind = PlanBoxKinds.Hub, Rect = [0, 0, 4, 4] });
        var read = Producibility.Read(plan).Single();
        await Assert.That(read.Findings.Any(f => f.Code == "box-empty")).IsTrue();
        await Assert.That(read.IsProducible).IsFalse();
    }

    [Test]
    public async Task Reading_a_plan_without_boxes_is_empty_not_an_error()
    {
        var plan = PlanModel.Parse(PlanTestSupport.ReadSeed("base-2wool.plan.json"))!;
        await Assert.That(Producibility.Read(plan)).IsEmpty();
        await Assert.That(Producibility.ReadPlan(plan).Unit).IsEmpty();
    }

    /// <summary>The shifted frontline the exemplars were authored to exercise: the allocator pins the frontline's
    /// face to the hub's full width and the parallel-fronts gate demands per-face mirror symmetry, so both fire
    /// and both cite G123 — the task that would relax them.</summary>
    [Test]
    public async Task The_shifted_frontline_reports_both_of_its_G123_blockers()
    {
        var plan = PlanModel.Parse(PlanTestSupport.ReadSeed("shifted-u-frontline-attach-hole-hub.plan.json"))!;
        var unit = Producibility.ReadPlan(plan).Unit;

        var pin = unit.FirstOrDefault(f => f.Code == "frontline-face-not-full-hub-width");
        await Assert.That(pin).IsNotNull();
        await Assert.That(pin!.Cites).IsEqualTo("G123");

        var faces = unit.FirstOrDefault(f => f.Code == "front-faces-not-mirror-symmetric");
        await Assert.That(faces).IsNotNull();
        await Assert.That(faces!.Cites).IsEqualTo("G123");
    }

    /// <summary>A box unbuildable on its own geometry AND a unit unbuildable in its arrangement are reported
    /// together — the author needs all of it, not the first failure.</summary>
    [Test]
    public async Task Box_level_and_unit_level_failures_are_both_reported()
    {
        var plan = PlanModel.Parse(PlanTestSupport.ReadSeed("shifted-u-frontline-attach-hole-hub.plan.json"))!;
        var read = Producibility.ReadPlan(plan);
        await Assert.That(read.IsProducible).IsFalse();
        await Assert.That(read.Boxes.Any(b => !b.IsProducible)).IsTrue().Because("the ring and the donut fail");
        await Assert.That(read.Unit).IsNotEmpty().Because("the shifted front and the seat gap fail");
    }

    /// <summary>A shape the emitters know whose proportions they cannot reach cites the task that owns per-part
    /// width — more use than a bare "nothing reproduces this". A nearest miss of a different form cites nothing,
    /// because no single task owns an unreachable shape.</summary>
    [Test]
    public async Task A_reachable_shape_with_unreachable_proportions_cites_the_width_gap()
    {
        var plan = PlanModel.Parse(PlanTestSupport.ReadSeed("shifted-u-frontline-attach-hole-hub.plan.json"))!;
        var reads = Producibility.Read(plan);

        var hub = reads.First(r => r.Kind == PlanBoxKinds.Hub);
        var hubGap = hub.Findings.FirstOrDefault(f => f.Code == "proportions-outside-the-parameter-space");
        await Assert.That(hubGap).IsNotNull().Because("a Ring whose nearest miss is a Ring is a width gap");
        await Assert.That(hubGap!.Cites).IsEqualTo("G105");
        await Assert.That(hubGap.Detail).Contains("G129");

        var wool = reads.First(r => r.BoxId == "wool-a");
        var woolGap = wool.Findings.FirstOrDefault(f => f.Code == "proportions-outside-the-parameter-space");
        await Assert.That(woolGap).IsNotNull();
        await Assert.That(woolGap!.Cites).IsEqualTo("G82").Because("an approach's width gap is the entry knob");

        // the g-hub hub reads G but its nearest is a solid Rectangle — a different shape, so no width claim
        var other = PlanModel.Parse(PlanTestSupport.ReadSeed("shifted-u-frontline-attach-g-hub.plan.json"))!;
        var gHub = Producibility.Read(other).First(r => r.Kind == PlanBoxKinds.Hub);
        await Assert.That(gHub.Nearest!.Label).DoesNotContain("G(");
        await Assert.That(gHub.Findings.Any(f => f.Code == "proportions-outside-the-parameter-space")).IsFalse();
    }

    /// <summary>
    /// Which side of the axis a unit is drawn on must not change what the front rules measure. The composer
    /// always grows on the +u side, so the frame's sign is fixed per symmetry; an authored plan may be drawn on
    /// either side, and reading u the wrong way round makes the min-u pieces the unit's <b>back</b> — every front
    /// rule then measures the spawn instead of the frontline. Mirroring a plan across the axis must leave the
    /// unit findings identical.
    /// </summary>
    [Test]
    public async Task Unit_findings_do_not_depend_on_which_side_of_the_axis_the_unit_is_drawn()
    {
        var plan = PlanModel.Parse(PlanTestSupport.ReadSeed("shifted-u-frontline-attach-hole-hub.plan.json"))!;
        var mirrored = PlanModel.Parse(plan.ToJson())!;
        foreach (var p in mirrored.Pieces) p.Rect = [p.Rect[0], -(p.Rect[1] + p.Rect[3]), p.Rect[2], p.Rect[3]];
        foreach (var b in mirrored.Boxes) b.Rect = [b.Rect[0], -(b.Rect[1] + b.Rect[3]), b.Rect[2], b.Rect[3]];

        var codes = Producibility.ReadPlan(plan).Unit.Select(f => f.Code).OrderBy(c => c).ToList();
        var mirroredCodes = Producibility.ReadPlan(mirrored).Unit.Select(f => f.Code).OrderBy(c => c).ToList();
        await Assert.That(mirroredCodes).IsEquivalentTo(codes);
    }

    /// <summary>The front the rules read is the edge <b>facing the axis</b>, whichever side the unit sits on.
    /// Moving a piece at the unit's far edge must not change a front finding — under a fixed-sign frame it would,
    /// because the far edge is what that frame calls the front.</summary>
    [Test]
    public async Task A_far_edge_piece_does_not_drive_the_front_findings()
    {
        var plan = PlanModel.Parse(PlanTestSupport.ReadSeed("shifted-u-frontline-attach-hole-hub.plan.json"))!;
        // the unit is drawn wholly on the -z side, so a fixed +u frame would pick the spawn (the far edge) as
        // "the front" and measure its lateral interval instead of the frontline's
        await Assert.That(plan.Pieces.All(p => p.Rect[1] + p.Rect[3] <= 0)).IsTrue();
        var before = Producibility.ReadPlan(plan).Unit.Select(f => f.Code).OrderBy(c => c).ToList();
        await Assert.That(before).Contains("front-faces-not-mirror-symmetric");

        // slide the spawn — the far-edge piece — laterally. It is not the front, so no front finding may move.
        var moved = PlanModel.Parse(plan.ToJson())!;
        var spawn = moved.Pieces.First(p => p.Id == "spawn");
        spawn.Rect = [spawn.Rect[0] + 3, spawn.Rect[1], spawn.Rect[2], spawn.Rect[3]];
        var after = Producibility.ReadPlan(moved).Unit.Select(f => f.Code).OrderBy(c => c).ToList();
        await Assert.That(after).Contains("front-faces-not-mirror-symmetric");
    }

    /// <summary>The seat-separation report says which measurand it used, because the answer depends on it — the
    /// envelope test can indict a placement whose emitted terrain keeps the gap (the question G124 parks).</summary>
    [Test]
    public async Task Seat_separation_names_its_measurand_and_the_open_question()
    {
        var plan = PlanModel.Parse(PlanTestSupport.ReadSeed("shifted-u-frontline-attach-hole-hub.plan.json"))!;
        var gap = Producibility.ReadPlan(plan).Unit.FirstOrDefault(f => f.Code == "seats-within-separation-gap");
        await Assert.That(gap).IsNotNull();
        await Assert.That(gap!.Cites).IsEqualTo("WL7");
        await Assert.That(gap.Detail).Contains("envelope");
        await Assert.That(gap.Detail).Contains("G124");
    }
}
