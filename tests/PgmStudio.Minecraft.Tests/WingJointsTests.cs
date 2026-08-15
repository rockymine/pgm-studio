using PgmStudio.Minecraft;
using PgmStudio.Minecraft.Houses;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// The joint model: which rectangle is the hall, which is the wing, and which pairs are not a building at all.
/// Every answer here is derived from the two rectangles and their ridges — nothing is named by an author.
/// </summary>
public class WingJointsTests
{
    private static readonly Wing Hall = new(0, 5, 9, 9);          // 10 x 5, ridge along x

    /// <summary>
    /// <b>The hall's ridge runs along the edge they share and the wing's runs into it.</b> That is what makes
    /// one of them a range and the other a cross wing, and it needs no size comparison: a 5-wide wing off the
    /// middle of a 10 x 5 hall is a T, and the hall is the hall because its ridge lies along the seam.
    /// </summary>
    [Test]
    public async Task The_hall_is_the_wing_whose_ridge_runs_along_the_shared_edge()
    {
        var wing = new Wing(2, 0, 6, 4, new WingSpec(Ridge: RidgeAxis.AlongZ));
        var joint = WingJoints.Between(Hall, wing);

        await Assert.That(joint.Fault).IsEqualTo(JointFault.None);
        await Assert.That(joint.Hall).IsEqualTo(0);
        await Assert.That(joint.Wing).IsEqualTo(1);
        await Assert.That(joint.EdgeAlongX).IsTrue();
        await Assert.That((joint.Lo, joint.Hi)).IsEqualTo((2, 6));

        // Given the other way round, the same pair answers the same way — the hall is not the first argument.
        var swapped = WingJoints.Between(wing, Hall);
        await Assert.That(swapped.Fault).IsEqualTo(JointFault.None);
        await Assert.That(swapped.Hall).IsEqualTo(1);
        await Assert.That(swapped.Wing).IsEqualTo(0);
    }

    /// <summary>
    /// <b>Equal rectangles join, so long as their ridges cross.</b> Two 5 x 5s sharing their whole edge make a
    /// T whose plan is a single 5 x 10 box — what makes it a building is that the roof levels differ, one ridge
    /// running north to south and meeting the other running east to west.
    /// </summary>
    [Test]
    public async Task Equal_squares_join_where_their_ridges_cross()
    {
        var hall = new Wing(0, 5, 4, 9);                          // 5 x 5, ties toward x
        var wing = new Wing(0, 0, 4, 4, new WingSpec(Ridge: RidgeAxis.AlongZ));

        var joint = WingJoints.Between(hall, wing);
        await Assert.That(joint.Fault).IsEqualTo(JointFault.None);
        await Assert.That(joint.Hall).IsEqualTo(0);
        await Assert.That(joint.Shared).IsEqualTo(5);
    }

    /// <summary>
    /// <b>A wing reaches no further along the shared edge than the hall reaches across it.</b> Ridge height
    /// follows the span a slope crosses, so a wing wider than the hall is deep stands taller than the roof it
    /// is meant to run into, and its march comes out the far side instead of forming a valley. Equal is legal
    /// and meets exactly at the hall's own ridge.
    /// </summary>
    [Test]
    [Arguments(3, JointFault.None)]
    [Arguments(5, JointFault.None)]
    [Arguments(7, JointFault.WingOvertops)]
    [Arguments(9, JointFault.WingOvertops)]
    public async Task A_wing_may_not_stand_taller_than_the_hall_it_meets(int width, JointFault expected)
    {
        // A hall 5 deep, so a wing wider than 5 tops it. Drawn from x0 so the shorter edge always lies within.
        var hall = new Wing(0, 5, 19, 9);
        var wing = new Wing(0, -1, width - 1, 4, new WingSpec(Ridge: RidgeAxis.AlongZ));

        await Assert.That((width, WingJoints.Between(hall, wing).Fault)).IsEqualTo((width, expected));
    }

    /// <summary>
    /// <b>Wings touch and never overlap.</b> A plan states its ground once, so two rectangles claiming the same
    /// cell have no account of whose walls stand there or whose roof covers it — and the joint a building is
    /// made at is the edge between them, which an overlap does not have.
    /// </summary>
    [Test]
    public async Task Rectangles_that_share_a_cell_are_refused()
    {
        var wing = new Wing(2, 0, 6, 5, new WingSpec(Ridge: RidgeAxis.AlongZ));   // z 0..5 against a hall at z 5..9
        var joint = WingJoints.Between(Hall, wing);

        await Assert.That(joint.Fault).IsEqualTo(JointFault.Overlapping);
        await Assert.That(WingJoints.Refusal(joint)!.Rule).IsEqualTo(WingJointRules.Overlapping);
    }

    /// <summary>
    /// <b>A touching edge is shared whole.</b> Where the shorter edge does not lie within the longer, part of
    /// the wing's end meets its neighbour and the rest hangs over open ground, and neither joint can happen —
    /// which is not caught by asking whether the neighbour is there, because a partial touch answers yes.
    /// </summary>
    [Test]
    public async Task A_partial_touch_is_refused()
    {
        var hall = new Wing(0, 5, 4, 9);
        var wing = new Wing(2, 0, 6, 4, new WingSpec(Ridge: RidgeAxis.AlongZ));   // shares x 2..4 of its own x 2..6

        var joint = WingJoints.Between(hall, wing);
        await Assert.That(joint.Fault).IsEqualTo(JointFault.PartialEdge);
        await Assert.That(joint.Shared).IsEqualTo(3);
        await Assert.That(WingJoints.Refusal(joint)!.Rule).IsEqualTo(WingJointRules.PartialEdge);
    }

    /// <summary>
    /// <b>Both ridges along the seam is two ranges side by side; both into it is one longer range.</b> Neither
    /// is a junction, and the first is exactly the shape both `Ell()` fixtures had — a gutter where the two
    /// roofs meet, which is the thing a march exists to prevent.
    /// </summary>
    [Test]
    public async Task Parallel_ridges_are_a_gutter_and_opposed_ones_are_a_single_range()
    {
        var sideBySide = WingJoints.Between(
            new Wing(0, 5, 9, 9, new WingSpec(Ridge: RidgeAxis.AlongX)),
            new Wing(0, 0, 9, 4, new WingSpec(Ridge: RidgeAxis.AlongX)));
        await Assert.That(sideBySide.Fault).IsEqualTo(JointFault.SideBySide);
        await Assert.That(WingJoints.Refusal(sideBySide)!.Rule).IsEqualTo(WingJointRules.SideBySide);

        var endToEnd = WingJoints.Between(
            new Wing(0, 5, 4, 9, new WingSpec(Ridge: RidgeAxis.AlongZ)),
            new Wing(0, 0, 4, 4, new WingSpec(Ridge: RidgeAxis.AlongZ)));
        await Assert.That(endToEnd.Fault).IsEqualTo(JointFault.EndToEnd);
        await Assert.That(WingJoints.Refusal(endToEnd)!.Rule).IsEqualTo(WingJointRules.EndToEnd);
    }

    /// <summary>
    /// <b>Standing clear is not a joint to judge.</b> Two rectangles with ground between them are two
    /// buildings, and so are two meeting at a corner alone — a corner is not an edge to build a joint on. Both
    /// are left out of a plan's joints rather than reported as faults.
    /// </summary>
    [Test]
    public async Task Rectangles_standing_clear_or_meeting_at_a_corner_are_not_a_joint()
    {
        var apart = WingJoints.Between(Hall, new Wing(0, 0, 4, 3, new WingSpec(Ridge: RidgeAxis.AlongZ)));
        await Assert.That(apart.Fault).IsEqualTo(JointFault.Apart);

        // Diagonally placed: its z ends where the hall's begins, but their x runs never meet.
        var corner = WingJoints.Between(Hall, new Wing(12, 0, 16, 4, new WingSpec(Ridge: RidgeAxis.AlongZ)));
        await Assert.That(corner.Fault).IsEqualTo(JointFault.Apart);

        var plan = new BuildingPlan([Hall, new Wing(0, 0, 4, 3, new WingSpec(Ridge: RidgeAxis.AlongZ))]);
        await Assert.That(WingJoints.Of(plan).Count).IsEqualTo(0);
    }

    /// <summary>Every fault a plan can be refused for is a finding with a rule, a sentence and the two wings it
    /// is about; a joint that stands is no finding at all. The gate reads <see cref="WingJoints.Check"/>
    /// rather than the enum, so a fault added without a rule would refuse a plan and say nothing.</summary>
    [Test]
    public async Task Every_fault_is_a_finding_and_a_standing_joint_is_none()
    {
        foreach (var fault in Enum.GetValues<JointFault>())
        {
            var refusal = WingJoints.Refusal(new WingJoint(0, 1, fault, -1, -1, true, 0, 4));
            var stands = fault is JointFault.None or JointFault.Apart;
            await Assert.That((fault, refusal is null)).IsEqualTo((fault, stands));
            if (stands) continue;
            await Assert.That((fault, refusal!.Rule.Length > 0, refusal.Message.Length > 0))
                .IsEqualTo((fault, true, true));
            await Assert.That(refusal.SubjectIds).IsEquivalentTo(new[] { "0", "1" });
            await Assert.That(refusal.Refuses).IsTrue();
        }
    }
}
