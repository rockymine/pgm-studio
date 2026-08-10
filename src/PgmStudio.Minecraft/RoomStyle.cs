using System.Text.Json.Serialization;
using PgmStudio.Domain;

namespace PgmStudio.Minecraft;

/// <summary>One course of a room part: the material its blocks resolve through, and how many courses deep it
/// runs before the next takes over.</summary>
public readonly record struct RoomCourse(TerrainMaterial Material, int Height = 1);

/// <summary>
/// A part of a room shell — its floor, its walls or its roof — as a stack of courses plus how far the part
/// runs. The stack is read from the part's own base outward: a floor downward from the surface players stand
/// on, a wall and a roof upward.
///
/// <para>The <b>last course repeats</b> past the end of the stack, the rule
/// <see cref="LayeredMaterial"/> already holds, so <see cref="Extent"/> moves without the stack being
/// re-authored: a taller wall grows in whatever its top course is, and a band written at the fourth course
/// stays at the fourth course rather than sliding with the height.</para>
/// </summary>
public sealed record RoomPart(IReadOnlyList<RoomCourse> Courses, int Extent)
{
    /// <summary>The material <paramref name="step"/> courses along the part, and how deep into that course
    /// the step is — what a nested <see cref="LayeredMaterial"/> reads, so a course may itself be a stack.</summary>
    public (TerrainMaterial Material, int DepthInCourse) At(int step)
    {
        var remaining = Math.Max(0, step);
        foreach (var (material, height) in Courses)
        {
            if (remaining < height) return (material, remaining);
            remaining -= height;
        }
        return Courses.Count > 0 ? (Courses[^1].Material, remaining) : (Air, 0);
    }

    /// <summary>A part of one material, <paramref name="extent"/> courses deep — the common case.</summary>
    public static RoomPart Of(TerrainMaterial material, int extent = 1) => new([new RoomCourse(material)], extent);

    /// <summary>Two parts are the same part when their stacks match course for course. The generated equality
    /// would compare the course lists by <em>reference</em>, so a style read back from its snapshot could never
    /// equal the style it was written from — which is the comparison a round trip is made of.</summary>
    public bool Equals(RoomPart? other)
        => other is not null && Extent == other.Extent && Courses.SequenceEqual(other.Courses);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Extent);
        foreach (var course in Courses) hash.Add(course);
        return hash.ToHashCode();
    }

    private static readonly TerrainMaterial Air = new SolidMaterial(Blocks.Air);
}

/// <summary>Where a roof ends. A shell is its piece inset by one block on every side (WX1), so an
/// <see cref="Overlap"/> roof reaches exactly the piece edge and never leaves it — even under WX8, where an
/// iron cube pulls a shell edge back and the eave lands strictly inside the piece. Nothing to negotiate,
/// nothing to validate: the ring the eave covers is the room's own.</summary>
public enum RoofEdge
{
    /// <summary>Ends with the walls, as the shipped shell does.</summary>
    Flush,
    /// <summary>Reaches one block past the walls, giving the roof a depth the inset shell otherwise hides.</summary>
    Overlap,
}

/// <summary>
/// How a room shell is finished: a material stack per part, plus the geometry knobs each part carries. The
/// footprint is never here — it comes from the piece (WX1), which is what makes a style adaptive
/// (docs/world-export/structures.md §6.4).
///
/// <para>What a style may <b>not</b> decide is as deliberate as what it may. The pad is the exported
/// wool/spawn point (WX5) and the doorway is the entry contract (WX6/WX7), so both are stamped over the
/// styled parts rather than by them. The platform under the room and the entrance redstone line belong to
/// the plan-derived structures (ST1) and are not part of a shell at all.</para>
///
/// <para>The room's colour rides <see cref="BucketContext.TeamData"/> — the tint channel, a 0–15 wool damage
/// nibble — so a <see cref="TeamTintedMaterial"/> course paints the wool's colour in a cage and the team's in
/// a spawn with no room-specific material.</para>
/// </summary>
public sealed record RoomStyle
{
    /// <summary>Downward from the course players stand on, which stays where it is: a thicker floor digs into
    /// the platform below rather than lifting the spawn point off it.</summary>
    public RoomPart Floor { get; init; } = RoomPart.Of(new SolidMaterial(Blocks.Bedrock));

    /// <summary>The perimeter ring, upward from the floor.</summary>
    public RoomPart Wall { get; init; } = RoomPart.Of(new SolidMaterial(Blocks.Bedrock), 7);

    /// <summary>The plane over the walls, upward.</summary>
    public RoomPart Roof { get; init; } = RoomPart.Of(new SolidMaterial(Blocks.Bedrock));

    public RoofEdge Eave { get; init; } = RoofEdge.Flush;

    /// <summary>Whether the roof carries its centred hole. Off is a sealed, unlit room — a real style, and
    /// worth choosing rather than getting by accident.</summary>
    public bool RoofHole { get; init; } = true;

    public DoorMaterial Door { get; init; } = DoorMaterial.StainedGlassPane;

    public int DoorHeight { get; init; } = 3;

    /// <summary>The highest course the shell writes, as a layer above the floor — what a caller reserves
    /// headroom for and what a preview draws to. Derived, so a snapshot does not carry it.</summary>
    [JsonIgnore]
    public int TopLayer => Wall.Extent + Roof.Extent;

    /// <summary>The shipped wool structure: bedrock, a wool band at the fourth course, a light slit at the
    /// sixth, and a stained-glass-pane door.</summary>
    public static RoomStyle Wool { get; } = new() { Wall = Banded(Blocks.Wool) };

    /// <summary>The shipped spawn structure: the same shell with a stained-clay band, and an open doorway a
    /// player can walk straight out of.</summary>
    public static RoomStyle Spawn { get; } = new()
    {
        Wall = Banded(Blocks.StainedClay),
        Door = DoorMaterial.Air,
        DoorHeight = 4,
    };

    /// <summary>The tallest shell the shipped styles build — what a caller clamps a floor against so a stamp
    /// cannot run past the world ceiling. Becomes a per-room read once a map binds its own styles.</summary>
    public static int MaxTopLayer { get; } = Math.Max(Wool.TopLayer, Spawn.TopLayer);

    /// <summary>The shipped wall: three courses of bedrock, a coloured band, bedrock, an open course, bedrock.
    /// The band and the slit are courses like any other — that they used to be a hard-coded layer index and a
    /// skipped one is exactly what this stack replaces.</summary>
    private static RoomPart Banded(int bandBlock) => new(
    [
        new RoomCourse(new SolidMaterial(Blocks.Bedrock), 3),
        new RoomCourse(new TeamTintedMaterial(bandBlock, new SolidMaterial(Blocks.Bedrock))),
        new RoomCourse(new SolidMaterial(Blocks.Bedrock)),
        new RoomCourse(new SolidMaterial(Blocks.Air)),
        new RoomCourse(new SolidMaterial(Blocks.Bedrock)),
    ], Extent: 7);
}
