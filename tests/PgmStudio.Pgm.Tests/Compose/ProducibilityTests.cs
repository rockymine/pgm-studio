using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Shapes;
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

    /// <summary>The unequal-wall ring: the hole-hub exemplar's hub has one wall 2 cells and the other 3. The
    /// search reproduces it and <b>names the wall vector</b> that did — the widened ring is inside the parameter
    /// space, not near it, and the label says which side is wide.</summary>
    [Test]
    public async Task An_unequal_walled_ring_reproduces_and_names_its_walls()
    {
        var plan = PlanModel.Parse(PlanTestSupport.ReadSeed("shifted-u-frontline-attach-hole-hub.plan.json"))!;
        var hub = Producibility.Read(plan).First(r => r.Kind == PlanBoxKinds.Hub);

        await Assert.That(hub.Identity).Contains("Ring").Because("topologically it is a ring");
        await Assert.That(hub.IsProducible).IsTrue().Because("a ring may carry one wall wider than the rest");
        await Assert.That(hub.Producible!.Label).Contains("Ring");
        await Assert.That(hub.Producible.Label).Contains("walls 2/3/2/2")
            .Because("the tuple that reproduced it names which side is wide, not just that one is");
        await Assert.That(hub.Nearest).IsNull().Because("a reproduced box needs no nearest miss");
    }

    /// <summary>The widening is bounded, and the bound is the sampler's: a ring whose wide wall is more than
    /// twice the narrow one is legal geometry the emitter will build, but not something the composer ever draws —
    /// so the search does not admit it. This is the line between "the emitter can" and "the composer would".</summary>
    [Test]
    public async Task A_ring_widened_past_the_ratio_cap_does_not_reproduce()
    {
        var lawful = HubPlan(BodyEmitter.Ring(new RingWalls(2, 4, 2, 2), 13, 7));
        var beyond = HubPlan(BodyEmitter.Ring(new RingWalls(2, 6, 2, 2), 13, 7));

        await Assert.That(Producibility.Read(lawful).First().IsProducible).IsTrue()
            .Because("4 is twice the 2-cell lane — the widest the spread law admits");
        var over = Producibility.Read(beyond).First();
        await Assert.That(over.IsProducible).IsFalse().Because("6 is three times the lane");
        await Assert.That(over.Nearest!.Label).Contains("Ring")
            .Because("the nearest miss is still a ring — the shape is right, the proportions are not");
    }

    // wrap a body as a one-box hub plan at the origin — the form the producibility read consumes
    private static PlanModel HubPlan(ShapeBody body)
    {
        int w = body.Pieces.Max(p => p.Rect.X + p.Rect.Width), h = body.Pieces.Max(p => p.Rect.Z + p.Rect.Height);
        var plan = new PlanModel
        {
            Meta = new PlanMeta { Name = "hub-probe" },
            Globals = new PlanGlobals { Cell = 5, Symmetry = "none", MaxPlayers = 12, Surface = 9, Headroom = 11 },
            Boxes = [new PlanBox { Id = "hub", Kind = PlanBoxKinds.Hub, Rect = new(0, 0, w, h) }],
        };
        for (var i = 0; i < body.Pieces.Count; i++)
            plan.Pieces.Add(new PlanPiece { Id = $"hub-t{i + 1}", Role = PlanRoles.Piece, Rect = body.Pieces[i].Rect });
        return plan;
    }

    /// <summary>The search collects a sampler's range by running it over a fixed number of seeds, which makes that
    /// count a <b>coverage</b> guarantee: a layout the composer draws but the sweep never happened to see reads as
    /// unproducible, and the report blames the box instead of the search. The frontline's two-leg layout is the
    /// widest such space — bay, end recess, offset and split are drawn independently — so it is the one that binds.
    /// Every layout the sampler yields over a range deeper than the sweep's must reproduce.</summary>
    [Test]
    [Arguments(11)]
    [Arguments(16)]
    [Arguments(20)]
    public async Task Every_leg_layout_the_frontline_sampler_draws_is_inside_the_search(int spine)
    {
        var seen = new HashSet<string>();
        var checkedLayouts = 0;
        for (ulong seed = 1; seed <= 200_000; seed++)
        {
            if (FrontlineBoxEmitter.SampleArms(new ComposeRng(seed), spine, 2) is not { } legs) continue;
            var key = string.Join("+", legs.Select(a => $"{a.Start}:{a.Width}"));
            if (!seen.Add(key)) continue;

            var box = new Box("front", BoxKind.Frontline, new(0, 0, spine, 5), spine * 5);
            var built = FrontlineBoxEmitter.Fill(box, new CompoundRead(Compound.SpineArms, 2), 2,
                OfferGrouping.Joint, BoxEdge.Top, armLayout: legs);
            if (built is null) continue;
            checkedLayouts++;

            var read = Producibility.Read(FrontPlan(box, built.Pieces)).Single();
            await Assert.That(read.IsProducible).IsTrue()
                .Because($"legs {key} on a {spine} spine came out of the sampler, so the sweep must have seen it — "
                         + $"nearest was {read.Nearest?.Label ?? "none"}");
        }
        await Assert.That(checkedLayouts).IsGreaterThan(30).Because("the sweep must have had a real tail to cover");
    }

    // wrap emitted frontline pieces as a one-box plan — the frontline twin of HubPlan
    private static PlanModel FrontPlan(Box box, IReadOnlyList<GrownPiece> pieces)
    {
        var plan = new PlanModel
        {
            Meta = new PlanMeta { Name = "front-probe" },
            Globals = new PlanGlobals { Cell = 5, Symmetry = "none", MaxPlayers = 12, Surface = 9, Headroom = 11 },
            Boxes = [new PlanBox { Id = box.Id, Kind = PlanBoxKinds.Frontline, Rect = box.Rect }],
        };
        foreach (var p in pieces)
            plan.Pieces.Add(new PlanPiece { Id = p.Id, Role = PlanRoles.Piece, Rect = p.Rect });
        return plan;
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
        plan.Boxes.Add(new PlanBox { Id = "b", Kind = PlanBoxKinds.Hub, Rect = new(0, 0, 4, 4) });
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

    /// <summary>The exemplar the shifted frontline was authored for now clears both of the blockers that used to
    /// stop it (G123): its face need not span the hub's full front edge, and its faces need not mirror onto
    /// themselves — only their hull must sit on the axis, which this one's does. What remains is the exemplar's
    /// unrelated scale anomalies, which G123 does not own.</summary>
    [Test]
    public async Task The_shifted_frontline_no_longer_reports_a_front_blocker()
    {
        var plan = PlanModel.Parse(PlanTestSupport.ReadSeed("shifted-u-frontline-attach-hole-hub.plan.json"))!;
        var unit = Producibility.ReadPlan(plan).Unit;

        await Assert.That(unit.Any(f => f.Code == "frontline-contact-patch-too-narrow")).IsFalse()
            .Because("the frontline meets the hub over plenty of cells; it just isn't the full edge");
        await Assert.That(unit.Any(f => f.Code == "front-hull-off-axis")).IsFalse()
            .Because("its front hull is symmetric about the axis — only the legs within it differ");

        // the frontline box itself was always producible; the point is the arrangement no longer blocks it
        var front = Producibility.Read(plan).First(b => b.Kind == PlanBoxKinds.Frontline);
        await Assert.That(front.IsProducible).IsTrue();
    }

    /// <summary>The spanning-dock exemplar — the funnel at the composer's own corridor width — reads producible
    /// end to end. It is the plan the half-scale g-hub trace was duplicated from, with the widths fixed, so it
    /// isolates the shifted frontline from that plan's tracing scale.</summary>
    [Test]
    public async Task The_spanning_dock_exemplar_reads_producible_end_to_end()
    {
        var plan = PlanModel.Parse(PlanTestSupport.ReadSeed("shifted-frontline-spanning-dock.plan.json"))!;
        var read = Producibility.ReadPlan(plan);

        foreach (var b in read.Boxes)
            await Assert.That(b.IsProducible).IsTrue()
                .Because($"{b.BoxId} ({b.Kind}, reads {b.Identity}) — nearest {b.Nearest?.Label ?? "none"}");
        await Assert.That(read.Unit).IsEmpty()
            .Because(string.Join("; ", read.Unit.Select(f => f.Code)));
        await Assert.That(read.IsProducible).IsTrue();

        // the point of the exemplar: a bay-fronted hub (a G) with a frontline reaching across its bay
        await Assert.That(read.Boxes.First(b => b.Kind == PlanBoxKinds.Hub).Identity).Contains("G");
    }

    /// <summary>A face reaching across a bay must hold a corridor's width on <b>every</b> shoulder. One thinned to
    /// a sliver is a cantilever over the hole, and reports as such — the spanning dock's law, read back.</summary>
    [Test]
    public async Task A_face_cantilevered_over_a_bay_reports_its_thin_shoulder()
    {
        var plan = PlanModel.Parse(PlanTestSupport.ReadSeed("shifted-frontline-spanning-dock.plan.json"))!;
        await Assert.That(Producibility.ReadPlan(plan).Unit).IsEmpty();

        // The face sits on two 2-cell shoulders with the hub's bay between them. Slide it one cell and the near
        // shoulder drops to 1 — still spanning the bay, now resting on a sliver, which is the cantilever.
        var box = plan.Boxes.First(b => b.Kind == PlanBoxKinds.Frontline);
        foreach (var p in PlanBoxes.MembersOf(plan, box))
            p.Rect = new(p.Rect.X + 1, p.Rect.Z, p.Rect.Width, p.Rect.Height);
        box.Rect = new(box.Rect.X + 1, box.Rect.Z, box.Rect.Width, box.Rect.Height);

        var thin = Producibility.ReadPlan(plan).Unit.FirstOrDefault(f => f.Code == "frontline-shoulder-too-narrow");
        await Assert.That(thin).IsNotNull();
        await Assert.That(thin!.Detail).Contains("shoulder");
        await Assert.That(thin.Detail).Contains("2 patch(es)").Because("it still spans the bay — that is the point");
    }

    /// <summary>The relaxation has a bound: a front slid far enough off the axis makes the mid band reach past
    /// the front it docks, and that slack is what BZ9 refuses.</summary>
    [Test]
    public async Task A_front_far_off_the_axis_still_reports_the_band_slack()
    {
        var plan = PlanModel.Parse(PlanTestSupport.ReadSeed("shifted-u-frontline-attach-hole-hub.plan.json"))!;
        // slide the whole frontline sideways, well past the cap
        foreach (var p in plan.Pieces.Where(p => p.Id.StartsWith("frontline")))
            p.Rect = new(p.Rect.X + 8, p.Rect.Z, p.Rect.Width, p.Rect.Height);

        var slack = Producibility.ReadPlan(plan).Unit.FirstOrDefault(f => f.Code == "front-hull-off-axis");
        await Assert.That(slack).IsNotNull();
        await Assert.That(slack!.Cites).IsEqualTo("BZ9");
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
        // a ring widened past the spread law: the shape is one the emitters build, the proportions are not one
        // the composer draws — the case that survives now that the lawful widening reproduces
        var hub = Producibility.Read(HubPlan(BodyEmitter.Ring(new RingWalls(2, 6, 2, 2), 13, 7))).First();
        var hubGap = hub.Findings.FirstOrDefault(f => f.Code == "proportions-outside-the-parameter-space");
        await Assert.That(hubGap).IsNotNull().Because("a Ring whose nearest miss is a Ring is a width gap");
        await Assert.That(hubGap!.Cites).IsEqualTo("G105");
        await Assert.That(hubGap.Detail).Contains("G129");

        var plan = PlanModel.Parse(PlanTestSupport.ReadSeed("shifted-u-frontline-attach-hole-hub.plan.json"))!;
        var wool = Producibility.Read(plan).First(r => r.BoxId == "wool-a");
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
        foreach (var p in mirrored.Pieces) p.Rect = new(p.Rect.X, -(p.Rect.Z + p.Rect.Height), p.Rect.Width, p.Rect.Height);
        foreach (var b in mirrored.Boxes) b.Rect = new(b.Rect.X, -(b.Rect.Z + b.Rect.Height), b.Rect.Width, b.Rect.Height);

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
        await Assert.That(plan.Pieces.All(p => p.Rect.Z + p.Rect.Height <= 0)).IsTrue();
        // sliding the spawn far off-axis would look like a badly off-centre "front" to a fixed-sign frame, so a
        // front finding would appear where none belongs. The spawn is the back; no front finding may react.
        var moved = PlanModel.Parse(plan.ToJson())!;
        var spawn = moved.Pieces.First(p => p.Id == "spawn");
        spawn.Rect = new(spawn.Rect.X + 12, spawn.Rect.Z, spawn.Rect.Width, spawn.Rect.Height);

        var before = Producibility.ReadPlan(plan).Unit.Count(f => f.Code == "front-hull-off-axis");
        var after = Producibility.ReadPlan(moved).Unit.Count(f => f.Code == "front-hull-off-axis");
        await Assert.That(after).IsEqualTo(before);
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
