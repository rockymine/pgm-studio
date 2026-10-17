using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Minecraft.Houses;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Vocabulary;

namespace PgmStudio.Export;

/// <summary>
/// The wings a room's house is stamped from: the room's resolved frame as the first, wearing what the house's
/// first wing states, then the house's other wings. A joined wing reaching outside the room's region, or meeting
/// the rest in a joint a building cannot be made at (<see cref="WingJoints"/>), leaves the room standing alone
/// and says why.
/// </summary>
public static class RoomHouses
{
    /// <summary>The wings of <paramref name="house"/> raised over <paramref name="frame"/>, max-inclusive, and
    /// the findings that left the joined ones out. Empty where the frame raises no building or the house has
    /// one wing, which stamps the frame alone.</summary>
    public static (IReadOnlyList<Wing> Wings, IReadOnlyList<Finding> Refused) Of(
        RoomHouse? house, RoomFrame frame, IReadOnlyList<Rect> region, string owner)
    {
        if (house is not { Wings.Count: > 1 } || frame.Wall == 0) return ([], []);
        var wings = new List<Wing> { new(frame.MinX, frame.MinZ, frame.MaxX - 1, frame.MaxZ - 1, house.Wings[0].Spec) };
        foreach (var authored in house.Wings.Skip(1))
        {
            var cells = RoomHouse.RectOf(authored);
            wings.Add(new Wing((int)cells.MinX, (int)cells.MinZ, (int)cells.MaxX - 1, (int)cells.MaxZ - 1, authored.Spec));
        }

        var outside = wings.Skip(1).Select((wing, index) => (wing, index: index + 1))
            .FirstOrDefault(entry => !Inside(entry.wing, region));
        if (outside.index > 0)
            return ([], [new Finding(RoomFrameRules.FootprintOffPiece,
                $"wing {outside.index} of the house of '{owner}' reaches outside the region of its room, and the "
                + "room stands alone", Severity.Complaint, Field: "house.wings", Subjects: [owner])]);

        var joints = WingJoints.Check(new BuildingPlan(wings));
        return joints.Refuses
            ? ([], [.. joints.Refusals.Select(finding => finding with { Severity = Severity.Complaint, Subjects = [owner] })])
            : (wings, []);
    }

    /// <summary>Whether every cell of <paramref name="wing"/> lies in one of the rectangles of
    /// <paramref name="region"/>.</summary>
    private static bool Inside(Wing wing, IReadOnlyList<Rect> region)
    {
        for (var x = wing.MinX; x <= wing.MaxX; x++)
            for (var z = wing.MinZ; z <= wing.MaxZ; z++)
                if (!region.Any(rect => x >= rect.MinX && x < rect.MaxX && z >= rect.MinZ && z < rect.MaxZ))
                    return false;
        return true;
    }
}
