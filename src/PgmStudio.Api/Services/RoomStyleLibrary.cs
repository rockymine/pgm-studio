using PgmStudio.Contracts;
using PgmStudio.Data.Schema;
using PgmStudio.Data.Theme;
using PgmStudio.Domain;
using PgmStudio.Minecraft;

namespace PgmStudio.Api.Services;

/// <summary>
/// The composition-root half of the room-style library (G34b): it bridges the row store
/// (<see cref="RoomStyleStore"/>) and the stamper's own model (<see cref="HouseStyle"/>), which live in
/// different layers. <see cref="ThemeLibrary"/>'s sibling, and the same discipline — a draft composes exactly
/// as a saved row does, so what an editor previews is what the save would build.
///
/// <para>A part with no courses keeps the built-in finish rather than resolving to nothing: a room style
/// overrides the parts it names and leaves the rest, which is what makes the library worth having for a style
/// that only changes its roof.</para>
/// </summary>
public sealed class RoomStyleLibrary(RoomStyleStore rooms, ThemeStore styles)
{
    /// <summary>A library room style assembled into the one the stamper consumes, or null when unknown.</summary>
    public async Task<HouseStyle?> ComposeAsync(long id, CancellationToken ct = default)
    {
        var row = await rooms.GetAsync(id, ct);
        if (row is null) return null;
        var courses = await rooms.GetCoursesAsync(id, ct);
        return Compose(row, courses, await StylesOf(courses, ct));
    }

    /// <summary>Every library room style with the shell it composes to, newest first — the whole library in
    /// two reads, so listing rooms with a picture of each does not cost a query per row.</summary>
    public async Task<List<(RoomStyleRow Row, HouseStyle Style)>> ComposeAllAsync(CancellationToken ct = default)
    {
        var rows = await rooms.ListAsync(ct);
        if (rows.Count == 0) return [];
        var byRoom = (await rooms.GetAllCourseStylesAsync(ct)).ToLookup(entry => entry.Course.RoomStyleId);
        return rows.Select(row =>
        {
            var entries = byRoom[row.Id].ToList();
            var bound = entries.GroupBy(entry => entry.Course.StyleId)
                .ToDictionary(group => group.Key, group => group.First().Style);
            return (row, Compose(row, entries.Select(entry => entry.Course).ToList(), bound));
        }).ToList();
    }

    /// <summary>The shell a draft composes to without any of it being saved — what the editor re-renders as
    /// courses are bound and knobs are turned. A course naming a style the library no longer holds is dropped,
    /// the way an unresolvable theme binding is.</summary>
    public async Task<HouseStyle> ComposeDraftAsync(RoomStyleSaveRequest draft, CancellationToken ct = default)
    {
        var courses = CourseRowsOf(draft).ToList();
        return Compose(RowOf(draft), courses, await StylesOf(courses, ct));
    }

    /// <summary>The row a save request describes — shared by create, update and the draft preview, so the three
    /// cannot disagree about what a request means.</summary>
    public static RoomStyleRow RowOf(RoomStyleSaveRequest req) => new()
    {
        Name = req.Name,
        FloorDepth = Math.Max(1, req.FloorDepth),
        WallHeight = Math.Max(1, req.WallHeight),
        RoofThickness = Math.Max(1, req.RoofThickness),
        RoofForm = RoofForms.Canonical(req.RoofForm),
        Pitch = Math.Clamp(req.Pitch, 1, 4),
        Overhang = Math.Clamp(req.Overhang, 0, 4),
        RoofHole = req.RoofHole,
        Door = DoorMaterials.IsKnown(req.Door) ? req.Door : DoorMaterials.Slug(DoorMaterial.StainedGlassPane),
        DoorHeight = Math.Max(1, req.DoorHeight),
    };

    public static IEnumerable<RoomStyleCourseRow> CourseRowsOf(RoomStyleSaveRequest req)
        => req.Courses
            .Where(course => RoomParts.All.Contains(course.Part))
            .Select(course => new RoomStyleCourseRow
            {
                Part = course.Part, Ordinal = course.Ordinal, StyleId = course.StyleId,
                Height = Math.Max(1, course.Height),
            });

    private async Task<Dictionary<long, StyleRow>> StylesOf(
        IReadOnlyList<RoomStyleCourseRow> courses, CancellationToken ct)
        => (await styles.GetStylesAsync(courses.Select(course => course.StyleId), ct))
            .ToDictionary(style => style.Id);

    /// <summary>Rows to the stamper's model: each part's courses in stack order, with its extent and knobs.</summary>
    private static HouseStyle Compose(
        RoomStyleRow row, IReadOnlyList<RoomStyleCourseRow> courses, IReadOnlyDictionary<long, StyleRow> bound)
    {
        // Built from the shipped shell rather than from a bare style, so a row inherits what a shell is —
        // flat-roofed, unframed, no sill — and names only what it changes. A fresh HouseStyle would default
        // to a gable, which a stored row has no way to ask for and no way to describe.
        var builtIn = HouseStyle.Wool;
        return builtIn with
        {
            Floor = Part(RoomParts.Floor, row.FloorDepth, builtIn.Floor),
            Wall = Part(RoomParts.Wall, row.WallHeight, builtIn.Wall),
            // A roof is one course, so its stack contributes only its first material and the stored thickness
            // is ignored. The eave was a two-valued overhang all along: flush is none, overlap is one block.
            Roof = Material(RoomParts.Roof, builtIn.Roof),
            // A house's three: unbound they stay what a shell is — corners that are wall like the rest of it,
            // no footing, and a rim in the roof's own material.
            Post = Bound(RoomParts.Post),
            Sill = Material(RoomParts.Sill, builtIn.Sill),
            Verge = Material(RoomParts.Verge, Material(RoomParts.Roof, builtIn.Roof)),
            Form = RoofForms.Canonical(row.RoofForm) == RoofForms.Gable ? RoofForm.Gable : RoofForm.Flat,
            Pitch = Math.Max(1, row.Pitch),
            Overhang = Math.Max(0, row.Overhang),
            RoofHole = row.RoofHole,
            Door = DoorMaterials.TryParse(row.Door, out var door) ? door : DoorMaterial.StainedGlassPane,
            DoorHeight = Math.Max(1, row.DoorHeight),
        };

        // A part that takes one material rather than a stack: its first bound course, or the fallback.
        TerrainMaterial Material(string part, TerrainMaterial fallback) => Bound(part) ?? fallback;

        TerrainMaterial? Bound(string part) => courses
            .Where(course => course.Part == part)
            .OrderBy(course => course.Ordinal)
            .Select(course => MaterialOf(course, bound))
            .FirstOrDefault(material => material is not null);

        RoomPart Part(string part, int extent, RoomPart fallback)
        {
            var stack = courses
                .Where(course => course.Part == part)
                .OrderBy(course => course.Ordinal)
                .Select(course => MaterialOf(course, bound) is { } material
                    ? new RoomCourse(material, Math.Max(1, course.Height))
                    : (RoomCourse?)null)
                .OfType<RoomCourse>()
                .ToList();
            return stack.Count == 0
                ? fallback with { Extent = Math.Max(1, extent) }
                : new RoomPart(stack, Math.Max(1, extent));
        }
    }

    /// <summary>The material a course resolves through, or null when it names a style the library no longer
    /// holds or one whose params this build cannot read. Deliberately forgiving for the reason a style's card
    /// picture is: <c>params_json</c> is a hand-editable leaf, and a room that draws without one bad course is
    /// more use than a library that refuses to list.</summary>
    private static TerrainMaterial? MaterialOf(RoomStyleCourseRow course, IReadOnlyDictionary<long, StyleRow> bound)
    {
        if (!bound.TryGetValue(course.StyleId, out var style)) return null;
        try { return TerrainThemeJson.DeserializeMaterial(style.Params); }
        catch { return null; }
    }
}
