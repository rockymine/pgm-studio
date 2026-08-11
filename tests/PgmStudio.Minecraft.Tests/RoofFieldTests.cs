using PgmStudio.Domain;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// The roof as a height field. Every form is one formula over the same two distances, so what is worth asserting
/// is not a block count but the <b>shape</b> each formula makes: where its ridge lands, which walls its slopes
/// come down to, and that a column always writes enough courses to close the step below it — the property that
/// keeps a slope from being seen straight through.
/// </summary>
public sealed class RoofFieldTests
{
    private const int BaseY = 10;

    private static RoofField Field(
        RoofForm form, int width = 11, int depth = 9, int overhang = 1, int pitch = 1,
        RoomEdge front = RoomEdge.NegZ)
        => new(form, 0, 0, width - 1, depth - 1, overhang, BaseY, pitch, front);

    [Test]
    public async Task A_flat_lid_is_one_course_everywhere()
    {
        var field = Field(RoofForm.Flat);
        await Assert.That(field.Peak).IsEqualTo(BaseY);
        await Assert.That(field.Trough).IsEqualTo(BaseY);
        await Assert.That(field.Riser(5, 4)).IsEqualTo(1);
        // Every cell is the top, so none of them is a ridge — a ridge that is the whole roof is not a ridge.
        await Assert.That(field.OnRidge(5, 4)).IsFalse();
    }

    [Test]
    public async Task A_gable_runs_its_ridge_along_the_longer_side()
    {
        // 11 wide by 9 deep: the ridge runs along x at the middle z, and the slope is taken across z.
        var field = Field(RoofForm.Gable);
        await Assert.That(field.Peak).IsEqualTo(BaseY + 4);
        await Assert.That(field.OnRidge(0, 4)).IsTrue();
        await Assert.That(field.OnRidge(10, 4)).IsTrue();
        await Assert.That(field.OnRidge(5, 2)).IsFalse();
        // The two ends are gable walls, not slopes: the crown over them is the same as over the middle.
        await Assert.That(field.Crown(0, 4)).IsEqualTo(field.Crown(5, 4));
    }

    [Test]
    public async Task A_hip_comes_down_to_all_four_walls_and_a_gable_does_not()
    {
        var hip = Field(RoofForm.Hip);
        var gable = Field(RoofForm.Gable);

        // Over the middle of an end wall the hip has already fallen to the eave; the gable is still at its ridge.
        await Assert.That(hip.Crown(0, 4)).IsEqualTo(BaseY);
        await Assert.That(gable.Crown(0, 4)).IsEqualTo(BaseY + 4);
        // Along the ridge the hip keeps a run: 11 by 9 leaves the ridge two blocks of length.
        await Assert.That(hip.Peak).IsEqualTo(BaseY + 4);
    }

    [Test]
    public async Task A_hip_over_a_square_is_a_pyramid()
    {
        var field = Field(RoofForm.Hip, width: 9, depth: 9);
        var peaks = 0;
        for (var x = field.MinX; x <= field.MaxX; x++)
            for (var z = field.MinZ; z <= field.MaxZ; z++)
                if (field.Crown(x, z) == field.Peak) peaks++;
        await Assert.That(peaks).IsEqualTo(1);       // one cell at the top — a point, not a line
    }

    [Test]
    public async Task A_shed_is_low_at_the_front_and_high_at_the_back()
    {
        var field = Field(RoofForm.Shed, front: RoomEdge.NegZ);
        await Assert.That(field.Crown(5, 0)).IsEqualTo(BaseY);
        await Assert.That(field.Crown(5, 8)).IsEqualTo(BaseY + 8);
        // One plane: the same climb whichever column of it is walked.
        for (var z = 1; z <= 8; z++)
            await Assert.That(field.Crown(5, z) - field.Crown(5, z - 1)).IsEqualTo(1);
    }

    [Test]
    public async Task A_saltbox_puts_its_ridge_off_centre_toward_the_front()
    {
        var field = Field(RoofForm.Saltbox, front: RoomEdge.NegZ);
        var ridge = -1;
        for (var z = 0; z <= 8; z++) if (field.Crown(5, z) == field.Peak) { ridge = z; break; }

        await Assert.That(ridge).IsGreaterThan(0);
        await Assert.That(ridge).IsLessThan(4);      // nearer the front wall than the middle
        // The front slope is the steep one, which is what puts the ridge there.
        await Assert.That(field.Crown(5, 1) - field.Crown(5, 0)).IsEqualTo(2);
        await Assert.That(field.Crown(5, 8) - field.Crown(5, 7)).IsEqualTo(-1);
    }

    [Test]
    public async Task A_gambrel_is_steep_off_the_eave_and_shallow_at_the_ridge()
    {
        // Wider than deep, so the ridge runs along x and the two slopes are taken across z.
        var field = Field(RoofForm.Gambrel, width: 15, depth: 11);
        var climb = new List<int>();
        for (var z = 1; z <= 5; z++) climb.Add(field.Crown(7, z) - field.Crown(7, z - 1));

        await Assert.That(climb[0]).IsEqualTo(2);            // steep off the eave
        await Assert.That(climb[^1]).IsEqualTo(1);           // shallow into the ridge
        // And it only breaks once — a gambrel is two pitches, not a curve.
        await Assert.That(climb.Distinct().Count()).IsEqualTo(2);
    }

    [Test]
    [Arguments(RoofForm.Gable)]
    [Arguments(RoofForm.Hip)]
    [Arguments(RoofForm.Shed)]
    [Arguments(RoofForm.Gambrel)]
    [Arguments(RoofForm.Saltbox)]
    public async Task A_column_always_writes_enough_courses_to_close_the_step_below_it(RoofForm form)
    {
        // The property the whole riser rule exists for: whatever a neighbour's crown is, this column's blocks
        // reach down to meet it, or the two slopes have daylight between them.
        var field = Field(form, pitch: 2);
        for (var x = field.MinX; x <= field.MaxX; x++)
            for (var z = field.MinZ; z <= field.MaxZ; z++)
                foreach (var (nextX, nextZ) in new[] { (x - 1, z), (x + 1, z), (x, z - 1), (x, z + 1) })
                {
                    if (!field.Covers(nextX, nextZ)) continue;
                    if (field.Crown(nextX, nextZ) >= field.Crown(x, z)) continue;
                    await Assert.That(field.Underside(x, z)).IsLessThanOrEqualTo(field.Crown(nextX, nextZ) + 1);
                }
    }

    [Test]
    [Arguments(RoofForm.Flat)]
    [Arguments(RoofForm.Gable)]
    [Arguments(RoofForm.Hip)]
    [Arguments(RoofForm.Shed)]
    [Arguments(RoofForm.Gambrel)]
    [Arguments(RoofForm.Saltbox)]
    public async Task The_overhang_keeps_falling_at_the_same_rate_as_the_slope(RoofForm form)
    {
        // Measured from the wall line and allowed to go past it, so the eave hangs below its neighbour instead
        // of running the last blocks of the roof flat exactly where it is most visible.
        var flush = Field(form, overhang: 0);
        var eaved = Field(form, overhang: 2);
        for (var x = 0; x < 11; x++)
            for (var z = 0; z < 9; z++)
                await Assert.That(eaved.Crown(x, z)).IsEqualTo(flush.Crown(x, z));

        if (form == RoofForm.Flat) return;
        await Assert.That(eaved.Trough).IsLessThan(flush.Trough);
    }

    // ── a roof that climbs half a block at a time ───────────────────────────────────────────────────

    [Test]
    public async Task A_slab_roof_climbs_one_half_per_block_and_a_cube_roof_is_untouched()
    {
        // Half courses are the whole of it: the same gable, one stepping two halves per block and the other
        // stepping one. The cube roof must answer exactly what it always did, which is what lets the field be
        // measured in halves at all.
        var cubes = new RoofField(RoofForm.Gable, 0, 0, 10, 8, 0, 20, 1, RoomEdge.NegZ);
        var slabs = new RoofField(RoofForm.Gable, 0, 0, 10, 8, 0, 20, 1, RoomEdge.NegZ, inHalves: true);

        // Across the short side, distance 0..4 from the wall line. The cube roof lifts a course a step; the
        // slab roof lifts half of one, so its cells go cube, slab, cube, slab — the crown rising on the odd
        // step because that is where the slab sits on top of the cube below it.
        int[] crowns = [20, 21, 21, 22, 22];
        for (var step = 0; step <= 4; step++)
        {
            await Assert.That(cubes.Crown(5, step)).IsEqualTo(20 + step);
            await Assert.That(cubes.Half(5, step)).IsFalse();

            await Assert.That(slabs.Crown(5, step)).IsEqualTo(crowns[step]);
            await Assert.That(slabs.Half(5, step)).IsEqualTo(step % 2 == 1);
        }
    }

    [Test]
    public async Task A_slab_roof_stands_half_as_tall_as_the_cube_roof_of_the_same_pitch()
    {
        var cubes = new RoofField(RoofForm.Hip, 0, 0, 6, 6, 1, 20, 1, RoomEdge.NegZ);
        var slabs = new RoofField(RoofForm.Hip, 0, 0, 6, 6, 1, 20, 1, RoomEdge.NegZ, inHalves: true);
        // Half the rise, rounded up: the topmost half-step still needs a cell to sit in.
        await Assert.That(slabs.Peak - 20).IsEqualTo((cubes.Peak - 20 + 1) / 2);
    }

    [Test]
    public async Task The_eave_of_a_slab_roof_falls_below_its_base_rather_than_rounding_back_up_to_it()
    {
        // The overhang is part of the slope and its rise goes negative, so halving it has to round down. A
        // truncating divide lifts those cells back to the base plane and the eave floats clear of the roof.
        var slabs = new RoofField(RoofForm.Gable, 0, 0, 10, 8, 3, 20, 1, RoomEdge.NegZ, inHalves: true);
        await Assert.That(slabs.Crown(5, -1)).IsEqualTo(20);       // half a block down: a slab in the base cell
        await Assert.That(slabs.Half(5, -1)).IsTrue();
        await Assert.That(slabs.Crown(5, -2)).IsEqualTo(19);       // a full block down
        await Assert.That(slabs.Half(5, -2)).IsFalse();
        await Assert.That(slabs.Crown(5, -3)).IsEqualTo(19);
        await Assert.That(slabs.Half(5, -3)).IsTrue();
    }
}
