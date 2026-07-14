using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Plan;

/// <summary>
/// The wool-lane string read — the corridor a wool room caps, read on ONE team unit's terrain (the k=0 image).
/// A thin adapter over <see cref="ShapeClassifier.ClassifyOpen(PlanModel, string)"/>: the canonical open read is
/// the <see cref="LaneRead"/> enum; this maps it back to the legacy strings (<c>I</c>/<c>L</c>/<c>Z</c>/
/// <c>complex</c>/<c>plaza</c>/<c>none</c>) the deriver gallery and lane-audit consume until they migrate.
/// </summary>
public static class WoolLaneShape
{
    /// <summary>Classify the lane of the wool room piece <paramref name="woolPieceId"/> in <paramref name="plan"/>
    /// (k=0 unit terrain).</summary>
    public static (string Shape, int Width) Classify(PlanModel plan, string woolPieceId)
    {
        var (read, w) = ShapeClassifier.ClassifyOpen(plan, woolPieceId);
        return (Name(read), w);
    }

    /// <summary>Classify the lane a <paramref name="room"/> caps within a single unit's <paramref name="filled"/>
    /// terrain (both in cell coordinates).</summary>
    public static (string Shape, int Width) Classify(IReadOnlySet<(int, int)> filled, IReadOnlySet<(int, int)> room)
    {
        var (read, w) = ShapeClassifier.ClassifyOpen(filled, room);
        return (Name(read), w);
    }

    private static string Name(LaneRead read) => read switch
    {
        LaneRead.I => "I",
        LaneRead.L => "L",
        LaneRead.Z => "Z",
        LaneRead.Complex => "complex",
        LaneRead.Plaza => "plaza",
        _ => "none",
    };
}
