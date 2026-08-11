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
