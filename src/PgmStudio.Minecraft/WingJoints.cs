using PgmStudio.Domain;

namespace PgmStudio.Minecraft;

/// <summary>Why two wings of one plan do not make a building, or <see cref="None"/> where they do.</summary>
public enum JointFault
{
    /// <summary>They meet, and the meeting is a junction.</summary>
    None,

    /// <summary>Neither a shared cell nor a shared edge — two buildings that happen to be near each other, or
    /// two that touch at a corner alone, which is a corner and not an edge.</summary>
    Apart,

    /// <summary>Blocks belong to both. A plan states ground once; two rectangles claiming the same cell have no
    /// account of which of them the walls, the storeys or the roof over it belong to.</summary>
    Overlapping,

    /// <summary>They touch along part of an edge only. Neither joint can happen there: some of the wing's end
    /// meets its neighbour and the rest hangs over open ground.</summary>
    PartialEdge,

    /// <summary>Both ridges run parallel to the edge they share — two ranges side by side, meeting in a gutter
    /// rather than a valley.</summary>
    SideBySide,

    /// <summary>Both ridges run into the edge they share — two ranges end to end, which is one longer range and
    /// wants drawing as one rectangle.</summary>
    EndToEnd,

    /// <summary>The wing's ridge stands taller than the hall's, so its roof cannot run into the hall's and
    /// comes out the far side instead.</summary>
    WingOvertops,
}

/// <summary>The wing-joint rule ids a refusal cites, catalogued in <c>docs/world-export/structures.md</c>.
/// Stable names for what a refusal is about, the way <c>HS*</c> names a house-style rule and <c>WX*</c> a
/// room-frame one, and kept apart from any task-tracking id.</summary>
public static class WingJointRules
{
    /// <summary>Two rectangles share blocks. A plan states its ground once.</summary>
    /// <remarks>Move one wing until they touch along an edge instead of sharing blocks. A plan states its ground once, and two wings claiming the same cells have no single answer for what stands there.</remarks>
    public const string Overlapping = "HJ1";

    /// <summary>They touch over part of an edge only, so neither joint can happen along it.</summary>
    /// <remarks>Align the wings so they meet over the whole of the shorter edge, or move them apart. A part-length meeting leaves half the wing end against its neighbour and half over open ground, which is neither joint.</remarks>
    public const string PartialEdge = "HJ2";

    /// <summary>Both ridges run along the shared edge — two ranges side by side, meeting in a gutter.</summary>
    /// <remarks>Turn one wing a quarter so its ridge runs into the shared edge rather than along it, or draw the two as one rectangle. Two ridges side by side meet in a gutter no roof form covers.</remarks>
    public const string SideBySide = "HJ3";

    /// <summary>Both ridges run into it — one longer range, which wants drawing as one rectangle.</summary>
    /// <remarks>Draw the two as one rectangle. Two ridges running into the shared edge are one longer range, and stating them separately asks for a seam through the middle of a roof.</remarks>
    public const string EndToEnd = "HJ4";

    /// <summary>The wing reaches further along the shared edge than the hall reaches across it, so its roof
    /// stands taller than the one it is meant to run into.</summary>
    /// <remarks>Shorten the wing along the shared edge, or widen the hall across it. A roof's height comes from its span, so a wing reaching further than its hall is wide stands taller than the roof it runs into.</remarks>
    public const string WingOvertops = "HJ5";
}

/// <summary>
/// How two rectangles of one plan meet: which is the hall, which is the wing, along what edge, and whether the
/// pair is a building at all.
///
/// <para><b>Nothing here is named by an author.</b> Which rectangle is the hall follows from the ridges and the
/// edge they share — the hall's ridge runs <em>along</em> that edge and the wing's runs <em>into</em> it, which
/// is what makes one of them a range and the other a cross wing. Both along it is two ranges side by side and
/// both into it is one longer range, and neither is a junction.</para>
/// </summary>
public readonly record struct WingJoint(
    int A, int B, JointFault Fault, int Hall, int Wing,
    bool EdgeAlongX, int Lo, int Hi, int Toward = 0)
{
    /// <summary>Whether the pair makes a building.</summary>
    public bool Joins => Fault == JointFault.None;

    /// <summary>How many blocks of edge the two share.</summary>
    public int Shared => Hi - Lo + 1;
}

/// <summary>
/// The joints of a plan, derived from its rectangles alone.
///
/// <para><b>Wings touch and never overlap.</b> A plan states ground once, so two rectangles claiming the same
/// cell have no account of whose walls stand there or whose roof covers it — and the joint a building is made
/// at is the edge they share, which an overlap does not have. Where they touch they share it <b>whole</b>: the
/// shorter edge lies within the longer, so the wing's end meets the hall over all of itself rather than
/// partly meeting it and partly hanging over open ground.</para>
/// </summary>
public static class WingJoints
{
    /// <summary>Why a pair is no building, as a <see cref="Finding"/> — or null where it is one. The rule id
    /// is what an author or an agent acts on; the sentence is in the terms the rectangles were drawn in, and
    /// the subjects are the two wings' own indices, so an editor highlights the pair it is about.</summary>
    public static Finding? Refusal(WingJoint joint)
    {
        var (rule, said) = joint.Fault switch
        {
            JointFault.Overlapping => (WingJointRules.Overlapping,
                "the two rectangles share blocks; wings touch and never overlap, because a joint is the edge "
                + "between them and an overlap has none"),
            JointFault.PartialEdge => (WingJointRules.PartialEdge,
                $"they touch over {joint.Shared} blocks of edge but not over all of the shorter one, so part of "
                + "the wing's end meets its neighbour and the rest hangs over open ground — neither joint can "
                + "happen"),
            JointFault.SideBySide => (WingJointRules.SideBySide,
                "both ridges run along the edge they share, which is two ranges side by side meeting in a "
                + "gutter rather than a valley; turn one of them across the other"),
            JointFault.EndToEnd => (WingJointRules.EndToEnd,
                "both ridges run into the edge they share, which is one longer range — draw it as one "
                + "rectangle"),
            JointFault.WingOvertops => (WingJointRules.WingOvertops,
                "the wing stands taller than the hall it meets, so its roof runs over rather than into it; a "
                + "wing reaches no further along the shared edge than the hall reaches across it"),
            _ => ("", ""),
        };
        return rule.Length == 0 ? null : new Finding(rule, said, Field: "wings",
            Subjects: [joint.A.ToString(), joint.B.ToString()]);
    }

    /// <summary>Every way a plan's wings fail to make a building, in the order the wings were given. Empty
    /// where the plan is one — which is what a caller gates on.</summary>
    public static Findings Check(BuildingPlan plan)
    {
        var refusals = new List<Finding>();
        foreach (var joint in Of(plan))
            if (Refusal(joint) is { } refusal) refusals.Add(refusal);
        return refusals;
    }

    /// <summary>Every pair of a plan's wings that touches or overlaps, in the order the wings were given. Pairs
    /// standing clear of each other are left out — they are not a joint to judge.</summary>
    public static IReadOnlyList<WingJoint> Of(BuildingPlan plan)
    {
        var joints = new List<WingJoint>();
        for (var a = 0; a < plan.Wings.Count; a++)
            for (var b = a + 1; b < plan.Wings.Count; b++)
            {
                var joint = Between(plan.Wings[a], plan.Wings[b], a, b);
                if (joint.Fault != JointFault.Apart) joints.Add(joint);
            }
        return joints;
    }

    /// <summary>How one pair meets.</summary>
    public static WingJoint Between(Wing a, Wing b, int indexA = 0, int indexB = 1)
    {
        WingJoint Fault(JointFault fault, bool alongX = true, int lo = 0, int hi = -1) =>
            new(indexA, indexB, fault, -1, -1, alongX, lo, hi);

        if (a.MaxX >= b.MinX && b.MaxX >= a.MinX && a.MaxZ >= b.MinZ && b.MaxZ >= a.MinZ)
            return Fault(JointFault.Overlapping);

        // The edge runs along x where one rectangle's z ends exactly where the other's begins, and along z for
        // the same in x. Meeting at neither is standing clear; meeting at a corner alone leaves an empty span
        // below and is standing clear too, since a corner is not an edge to build a joint on.
        var alongX = a.MaxZ + 1 == b.MinZ || b.MaxZ + 1 == a.MinZ;
        var alongZ = a.MaxX + 1 == b.MinX || b.MaxX + 1 == a.MinX;
        if (alongX == alongZ) return Fault(JointFault.Apart);

        var (aLo, aHi) = alongX ? (a.MinX, a.MaxX) : (a.MinZ, a.MaxZ);
        var (bLo, bHi) = alongX ? (b.MinX, b.MaxX) : (b.MinZ, b.MaxZ);
        var lo = Math.Max(aLo, bLo);
        var hi = Math.Min(aHi, bHi);
        if (hi < lo) return Fault(JointFault.Apart);

        // Whole edge: the shorter of the two lies within the longer. Anything else is a partial touch.
        if ((lo, hi) != (aLo, aHi) && (lo, hi) != (bLo, bHi))
            return Fault(JointFault.PartialEdge, alongX, lo, hi);

        // The hall's ridge runs along the shared edge and the wing's runs into it.
        var aParallel = a.RidgeAlongX == alongX;
        var bParallel = b.RidgeAlongX == alongX;
        if (aParallel && bParallel) return Fault(JointFault.SideBySide, alongX, lo, hi);
        if (!aParallel && !bParallel) return Fault(JointFault.EndToEnd, alongX, lo, hi);

        var hall = aParallel ? indexA : indexB;
        var wing = aParallel ? indexB : indexA;
        var hallWing = aParallel ? a : b;
        var wingWing = aParallel ? b : a;

        // A wing marches into a hall only where its ridge stands no taller, and ridge height follows the span
        // the slope crosses: for the wing that is its reach along the shared edge, for the hall its reach
        // across it. Equal is legal and meets exactly at the hall's own ridge.
        var wingAcross = alongX ? wingWing.Width : wingWing.Depth;
        var hallAcross = alongX ? hallWing.Depth : hallWing.Width;
        if (wingAcross > hallAcross) return new(indexA, indexB, JointFault.WingOvertops, hall, wing, alongX, lo, hi);

        // Which way the hall lies from the wing, along the wing's own ridge — the wing's ridge runs into the
        // shared edge, so this is the axis the edge is not on, and the two abut, so one of them starts exactly
        // where the other stops.
        var toward = alongX
            ? hallWing.MinZ > wingWing.MaxZ ? 1 : -1
            : hallWing.MinX > wingWing.MaxX ? 1 : -1;
        return new(indexA, indexB, JointFault.None, hall, wing, alongX, lo, hi, toward);
    }

    /// <summary>The joint at one of a wing's gable ends: the hall its roof runs into that way, or null where
    /// nothing lies past it. An end with open ground beyond it is the building's own gable — it neither marches
    /// nor projects, because there is nothing there to do either into.</summary>
    /// <param name="joints">A plan's joints, as <see cref="Of"/> derived them.</param>
    /// <param name="wing">The index of the wing whose end is being asked about.</param>
    /// <param name="step">−1 for its low gable end, +1 for its high one.</param>
    public static WingJoint? Past(IReadOnlyList<WingJoint> joints, int wing, int step)
    {
        foreach (var joint in joints)
            if (joint.Joins && joint.Wing == wing && joint.Toward == step) return joint;
        return null;
    }
}
