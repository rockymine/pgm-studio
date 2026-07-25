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

            foreach (var read in Producibility.Read(plan))
            {
                // the mid box is carved rather than emitted from a form menu, so it has no candidates to match
                if (read.Kind == PlanBoxKinds.Mid) continue;
                checkedBoxes++;
                await Assert.That(read.IsProducible).IsTrue()
                    .Because($"seed {seed} {read.BoxId} ({read.Kind}, reads {read.Identity}) came out of the " +
                             $"composer, so a tuple must reproduce it — nearest was " +
                             $"{read.Nearest?.Label ?? "none"} at {read.Nearest?.DifferingCells ?? -1} cells");
            }
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
    }
}
