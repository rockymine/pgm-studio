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
        RidgeCap = req.RidgeCap,
        Storeys = Math.Clamp(req.Storeys, 1, 8),
        StoreyClear = Math.Clamp(req.StoreyClear, 0, 16),
        Door = DoorMaterials.IsKnown(req.Door) ? req.Door : DoorMaterials.Slug(DoorMaterial.StainedGlassPane),
        DoorHeight = Math.Max(1, req.DoorHeight),
        BorderWidth = Math.Clamp(req.BorderWidth, 1, 4),
        InlayInset = Math.Clamp(req.InlayInset, 1, 8),
        WindowForm = WindowForms.Canonical(req.Windows?.Form),
        WindowBlock = Math.Max(0, req.Windows?.Block ?? Blocks.GlassPane),
        WindowData = Math.Clamp(req.Windows?.Data ?? 0, 0, 15),
        WindowSill = Math.Clamp(req.Windows?.Sill ?? 2, 1, 16),
        WindowWidth = Math.Clamp(req.Windows?.Width ?? 2, 1, 8),
        WindowHeight = Math.Clamp(req.Windows?.Height ?? 2, 1, 8),
        WindowSpacing = Math.Clamp(req.Windows?.Spacing ?? 3, 0, 16),
        // Depth 0 is what an absent porch stores, so the wire's null and a depth of nothing are one row.
        PorchDepth = Math.Clamp(req.Porch?.Depth ?? 0, 0, 8),
        PorchInset = Math.Clamp(req.Porch?.Inset ?? 0, 0, 8),
        PorchEdge = PorchEdges.Canonical(req.Porch?.Edge),
        PorchRoof = RoofForms.Canonical(req.Porch?.Roof ?? RoofForms.Shed),
        PorchRailBlock = Math.Max(0, req.Porch?.RailBlock ?? Blocks.OakFence),
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
            // Unbound the gable is the wall's top course carried up, which is what every stored style was
            // before the face had a name of its own.
            Gable = Bound(RoomParts.Gable),
            Sill = Material(RoomParts.Sill, builtIn.Sill),
            Verge = Material(RoomParts.Verge, Material(RoomParts.Roof, builtIn.Roof)),
            // The floor's top course in plan. Each zone is unbound until a course names it, and an unbound
            // zone is not a zone: the floor part shows through, which is what every stored style was.
            Surface = new FloorSurface
            {
                Field = Bound(RoomParts.Field),
                Border = Bound(RoomParts.Border),
                BorderWidth = Math.Max(1, row.BorderWidth),
                Inlay = Bound(RoomParts.Inlay),
                InlayInset = Math.Max(1, row.InlayInset),
            },
            Windows = WindowOf(row),
            Storeys = StoreysOf(row),
            Porch = PorchOf(row),
            Form = FormOf(row.RoofForm),
            Pitch = Math.Max(1, row.Pitch),
            Overhang = Math.Max(0, row.Overhang),
            RoofHole = row.RoofHole,
            RidgeCap = row.RidgeCap,
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

    /// <summary>The stored word for a roof as the stamper's own form. Unknown words are the flat lid, the same
    /// fold <see cref="RoofForms.Canonical"/> makes, so a hand-edited row still stamps.</summary>
    private static RoofForm FormOf(string? form) => RoofForms.Canonical(form) switch
    {
        RoofForms.Gable => RoofForm.Gable,
        RoofForms.Hip => RoofForm.Hip,
        RoofForms.Gambrel => RoofForm.Gambrel,
        RoofForms.Shed => RoofForm.Shed,
        RoofForms.Saltbox => RoofForm.Saltbox,
        _ => RoofForm.Flat,
    };

    /// <summary>The storeys a row stacks, or none for the single storey every style was before there were any.
    /// Each storey names only its height: the wall, the windows and the floor zones it leaves unset fall back
    /// to the style's own, so a stack of identical storeys is a count rather than a description repeated.</summary>
    private static IReadOnlyList<Storey> StoreysOf(RoomStyleRow row)
    {
        var count = Math.Clamp(row.Storeys, 1, 8);
        if (count <= 1) return [];
        var clear = row.StoreyClear > 0 ? row.StoreyClear : row.WallHeight;
        return [.. Enumerable.Repeat(new Storey { Clear = clear }, count)];
    }

    private static WindowStyle WindowOf(RoomStyleRow row) => new()
    {
        Form = WindowForms.Canonical(row.WindowForm) switch
        {
            WindowForms.StairLattice => WindowForm.StairLattice,
            WindowForms.SlabBanded => WindowForm.SlabBanded,
            WindowForms.Pane => WindowForm.Pane,
            _ => WindowForm.None,
        },
        Block = row.WindowBlock,
        Data = row.WindowData,
        Sill = Math.Max(1, row.WindowSill),
        Width = Math.Max(1, row.WindowWidth),
        Height = Math.Max(1, row.WindowHeight),
        Spacing = Math.Max(0, row.WindowSpacing),
    };

    /// <summary>The porch a row asks for, or null for a depth of nothing — which is what a building whose walls
    /// stand on the whole footprint stores, and what every row saved before there were porches is.</summary>
    private static PorchStyle? PorchOf(RoomStyleRow row) => row.PorchDepth <= 0 ? null : new PorchStyle
    {
        Depth = row.PorchDepth,
        Inset = Math.Max(0, row.PorchInset),
        Edge = PorchEdges.Canonical(row.PorchEdge) switch
        {
            PorchEdges.NegZ => RoomEdge.NegZ,
            PorchEdges.PosZ => RoomEdge.PosZ,
            PorchEdges.NegX => RoomEdge.NegX,
            PorchEdges.PosX => RoomEdge.PosX,
            _ => null,
        },
        Roof = FormOf(row.PorchRoof),
        RailBlock = Math.Max(0, row.PorchRailBlock),
    };

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
