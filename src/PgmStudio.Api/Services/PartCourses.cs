using PgmStudio.Contracts;
using PgmStudio.Data.Schema;
using PgmStudio.Minecraft;

namespace PgmStudio.Api.Services;

/// <summary>One course of some part's stack, whichever table it was read out of: which part it belongs to,
/// where it sits in that part's stack (0 = nearest the part's own base), the style it resolves through, and
/// how many courses it runs.</summary>
public readonly record struct PartCourse(string Part, int Ordinal, long StyleId, int Height);

/// <summary>
/// Resolves a set of <see cref="PartCourse"/> against the styles they bind — the one piece of logic a house,
/// a roof and a storey all need, written once because they are the same act. Rooms, roofs and storeys keep
/// separate course <em>tables</em> so each has a real foreign key to its owner, but a course means the same
/// thing in all three and a second copy of "resolve the stack" is how they would come to disagree.
/// </summary>
public sealed class PartCourses(IReadOnlyList<PartCourse> courses, IReadOnlyDictionary<long, StyleRow> bound)
{
    public static PartCourses Of(IEnumerable<RoomStyleCourseRow> rows, IReadOnlyDictionary<long, StyleRow> bound)
        => new([.. rows.Select(row => new PartCourse(row.Part, row.Ordinal, row.StyleId, row.Height))], bound);

    public static PartCourses Of(IEnumerable<RoofStyleCourseRow> rows, IReadOnlyDictionary<long, StyleRow> bound)
        => new([.. rows.Select(row => new PartCourse(row.Part, row.Ordinal, row.StyleId, row.Height))], bound);

    public static PartCourses Of(IEnumerable<StoreyStyleCourseRow> rows, IReadOnlyDictionary<long, StyleRow> bound)
        => new([.. rows.Select(row => new PartCourse(row.Part, row.Ordinal, row.StyleId, row.Height))], bound);

    /// <summary>The material a part that takes one rather than a stack resolves to — its first bound course —
    /// or null where nothing names it.</summary>
    public TerrainMaterial? Bound(string part) => courses
        .Where(course => course.Part == part)
        .OrderBy(course => course.Ordinal)
        .Select(Material)
        .FirstOrDefault(material => material is not null);

    /// <summary>The material a part takes, falling back where nothing names it.</summary>
    public TerrainMaterial Material(string part, TerrainMaterial fallback) => Bound(part) ?? fallback;

    /// <summary>A part's whole stack at the given extent, or <paramref name="fallback"/> re-extended where the
    /// part is unbound. Unbound keeps the built-in finish rather than resolving to nothing: a style overrides
    /// the parts it names and leaves the rest, which is what makes the level worth having for a part that only
    /// changes one thing.</summary>
    public RoomPart Stack(string part, int extent, RoomPart fallback)
    {
        var stack = courses
            .Where(course => course.Part == part)
            .OrderBy(course => course.Ordinal)
            .Select(course => Material(course) is { } material
                ? new Band(material, Math.Max(1, course.Height))
                : (Band?)null)
            .OfType<Band>()
            .ToList();
        return stack.Count == 0
            ? fallback with { Extent = Math.Max(1, extent) }
            : new RoomPart(new BandStack(stack), Math.Max(1, extent));
    }

    /// <summary>The material one course resolves through, or null when it names a style the library no longer
    /// holds or one whose params this build cannot read. Deliberately forgiving for the reason a style's card
    /// picture is: <c>params_json</c> is a hand-editable leaf, and a building that draws without one bad course
    /// is more use than a library that refuses to list.</summary>
    private TerrainMaterial? Material(PartCourse course)
    {
        if (!bound.TryGetValue(course.StyleId, out var style)) return null;
        try { return TerrainThemeJson.DeserializeMaterial(style.Params); }
        catch { return null; }
    }

    /// <summary>The courses of a save request that name a part this build knows, as rows of whatever table
    /// owns them. A part it does not know is dropped rather than stored: the part vocabulary is the contract,
    /// and a row naming a part nothing stamps is a row nothing can ever draw.</summary>
    public static IEnumerable<PartCourse> Accepted(IEnumerable<RoomCourseDto> courses)
        => courses
            .Where(course => RoomParts.All.Contains(course.Part))
            .Select(course => new PartCourse(
                course.Part, course.Ordinal, course.StyleId, Math.Max(1, course.Height)));
}
