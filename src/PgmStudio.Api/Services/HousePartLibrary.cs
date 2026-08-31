using PgmStudio.Contracts;
using PgmStudio.Data.Schema;
using PgmStudio.Data.Theme;
using PgmStudio.Domain;
using PgmStudio.Minecraft;
using PgmStudio.Minecraft.Houses;
using PgmStudio.Minecraft.Painting;
using PgmStudio.Minecraft.Palette;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Services;

/// <summary>
/// The composition-root half of the house-part library (B71): it bridges <see cref="HousePartStore"/> and the
/// stamper's own model. <see cref="RoomStyleLibrary"/>'s sibling one level down, and the same discipline — a
/// draft composes exactly as a saved row does, so what an editor previews is what the save would build.
///
/// <para>A part composes to a <em>fragment</em> of a <see cref="HouseStyle"/> rather than to a whole one,
/// since a roof is not a building. The fragment is applied over a house by <see cref="RoomStyleLibrary"/>;
/// to draw a part on its own the library gives it a sample building to sit on, which is what
/// <c>OnSample</c> is for — a roof with no walls under it is a picture of nothing.</para>
/// </summary>
public sealed class HousePartLibrary(HousePartStore parts, ThemeStore styles)
{
    // ── roofs ─────────────────────────────────────────────────────────────────────────────────────────
    public async Task<HouseStyle?> ComposeRoofAsync(long id, CancellationToken ct = default)
    {
        var row = await parts.GetRoofAsync(id, ct);
        if (row is null) return null;
        return RoofOver(row, await CoursesOf(await parts.GetRoofCoursesAsync(id, ct), ct), WallFor(row));
    }

    /// <summary>Every roof with the building it composes to, newest first — the whole library in two reads.</summary>
    public async Task<List<(RoofStyleRow Row, HouseStyle Style)>> ComposeRoofsAsync(CancellationToken ct = default)
    {
        var rows = await parts.ListRoofsAsync(ct);
        if (rows.Count == 0) return [];
        var all = await parts.GetAllRoofCoursesAsync(ct);
        var bound = await StylesOf(all.Select(course => course.StyleId), ct);
        var byRoof = all.ToLookup(course => course.RoofStyleId);
        return [.. rows.Select(row => (row, RoofOver(row, PartCourses.Of(byRoof[row.Id], bound), WallFor(row))))];
    }

    public async Task<HouseStyle> ComposeRoofDraftAsync(RoofStyleSaveRequest draft, CancellationToken ct = default)
        => RoofOver(RowOf(draft), await CoursesOf(PartCourses.Accepted(draft.Courses), ct), WallFor(RowOf(draft)));

    /// <summary>What a roof style says about a building: the form and its numbers, and the three materials
    /// above the eave. Applied over <paramref name="basis"/>, so a house keeps everything a roof has no
    /// opinion about.</summary>
    public static HouseStyle RoofOver(RoofStyleRow row, PartCourses courses, HouseStyle? basis = null)
    {
        var house = basis ?? HouseStyle.Wool;
        var body = courses.Material(RoomParts.Roof, house.Roof.Body);
        return house with
        {
            Roof = house.Roof with
            {
                Body = body,
                // Unbound, the verge is the roof's own material — a roof laid in one thing, which is what a
                // plain lid is — and the gable is the wall's top course carried up, which is what a wall with
                // no gable named was before the face had a name.
                Verge = courses.Material(RoomParts.Verge, body),
                Gable = courses.Bound(RoomParts.Gable),
                Form = RoomStyleLibrary.FormOf(row.Form),
                Pitch = Math.Max(1, row.Pitch),
                Overhang = Math.Max(0, row.Overhang),
                Hole = row.RoofHole,
                RidgeCap = row.RidgeCap,
                // The half-course slab is the roof's own, so a house binding one takes the roof's answer the
                // way it takes its form and its pitch. Everything above the eave belongs to this part, and a
                // slab a house named under a bound roof would be a second opinion on the same course.
                Slab = row.RoofSlab,
                SlabData = Math.Clamp(row.RoofSlabData, 0, 15),
            },
        };
    }

    public static RoofStyleRow RowOf(RoofStyleSaveRequest req) => new()
    {
        Name = req.Name,
        Form = RoofForms.Canonical(req.Form),
        RoofSlab = req.RoofSlab,
        RoofSlabData = Math.Clamp(req.RoofSlabData, 0, 15),
        Pitch = Math.Clamp(req.Pitch, 1, 4),
        Overhang = Math.Clamp(req.Overhang, 0, 4),
        RoofHole = req.RoofHole,
        RidgeCap = req.RidgeCap,
    };

    public static IEnumerable<RoofStyleCourseRow> RoofCourseRowsOf(RoofStyleSaveRequest req)
        => PartCourses.Accepted(req.Courses).Select(course => new RoofStyleCourseRow
        {
            Part = course.Part, Ordinal = course.Ordinal, StyleId = course.StyleId, Height = course.Height,
        });

    // ── storeys ───────────────────────────────────────────────────────────────────────────────────────
    public async Task<HouseStyle?> ComposeStoreyAsync(long id, CancellationToken ct = default)
    {
        var row = await parts.GetStoreyAsync(id, ct);
        if (row is null) return null;
        return OnSample(StoreyOf(row, await CoursesOf(await parts.GetStoreyCoursesAsync(id, ct), ct)));
    }

    public async Task<List<(StoreyStyleRow Row, HouseStyle Style)>> ComposeStoreysAsync(CancellationToken ct = default)
    {
        var rows = await parts.ListStoreysAsync(ct);
        if (rows.Count == 0) return [];
        var all = await parts.GetAllStoreyCoursesAsync(ct);
        var bound = await StylesOf(all.Select(course => course.StyleId), ct);
        var byStorey = all.ToLookup(course => course.StoreyStyleId);
        return [.. rows.Select(row => (row, OnSample(StoreyOf(row, PartCourses.Of(byStorey[row.Id], bound)))))];
    }

    public async Task<HouseStyle> ComposeStoreyDraftAsync(
        StoreyStyleSaveRequest draft, CancellationToken ct = default)
        => OnSample(StoreyOf(RowOf(draft), await CoursesOf(PartCourses.Accepted(draft.Courses), ct)));

    /// <summary>One storey of a building: its clear, its wall, its corner posts, its windows and how its own
    /// floor is divided. Everything the stamper needs to lay one room and nothing about the building around
    /// it.</summary>
    public static Storey StoreyOf(StoreyStyleRow row, PartCourses courses) => new()
    {
        Clear = Math.Max(3, row.Clear),
        // The extent is the storey's clear rather than a stored height: a storey's courses follow from its
        // air, and a wall stack that disagreed with it would be a second answer to a settled question.
        Wall = courses.Stack(RoomParts.Wall, Math.Max(3, row.Clear), HouseStyle.Wool.Wall),
        Post = courses.Bound(RoomParts.Post),
        // What closes this storey where another stands on it. Unbound it is the house floor's own top
        // material, which is what every stored storey was before the slab had a name of its own.
        Deck = courses.Bound(RoomParts.Deck),
        Windows = WindowOf(row),
        Surface = new FloorSurface
        {
            Field = courses.Bound(RoomParts.Field),
            Border = courses.Bound(RoomParts.Border),
            BorderWidth = Math.Max(1, row.BorderWidth),
            Inlay = courses.Bound(RoomParts.Inlay),
            InlayInset = Math.Max(1, row.InlayInset),
        },
    };

    public static StoreyStyleRow RowOf(StoreyStyleSaveRequest req) => new()
    {
        Name = req.Name,
        Clear = Math.Clamp(req.Clear, 3, 16),
        BorderWidth = Math.Clamp(req.BorderWidth, 1, 4),
        InlayInset = Math.Clamp(req.InlayInset, 1, 8),
        WindowForm = WindowForms.Canonical(req.Windows?.Form),
        WindowBlock = Math.Max(0, req.Windows?.Block ?? Blocks.GlassPane),
        WindowData = Math.Clamp(req.Windows?.Data ?? 0, 0, 15),
        WindowSill = Math.Clamp(req.Windows?.Sill ?? 2, 1, 16),
        WindowWidth = Math.Clamp(req.Windows?.Width ?? 2, 1, 8),
        WindowHeight = Math.Clamp(req.Windows?.Height ?? 2, 1, 8),
        WindowSpacing = Math.Clamp(req.Windows?.Spacing ?? 3, 0, 16),
        WindowHostBlock = req.Windows?.HostBlock ?? -1,
        WindowHostData = Math.Clamp(req.Windows?.HostData ?? 0, 0, 15),
    };

    public static IEnumerable<StoreyStyleCourseRow> StoreyCourseRowsOf(StoreyStyleSaveRequest req)
        => PartCourses.Accepted(req.Courses).Select(course => new StoreyStyleCourseRow
        {
            Part = course.Part, Ordinal = course.Ordinal, StyleId = course.StyleId, Height = course.Height,
        });

    /// <summary>internal rather than private: the storey-style endpoints check a draft's window against
    /// <see cref="HouseStyleValidation.CheckWindow"/> before anything is stored, off the exact
    /// <see cref="WindowStyle"/> composition would build — not a second reading of the same DTO.</summary>
    internal static WindowStyle WindowOf(StoreyStyleRow row) => new()
    {
        Form = RoomStyleLibrary.WindowFormOf(row.WindowForm),
        HostBlock = row.WindowHostBlock,
        HostData = row.WindowHostData,
        Block = row.WindowBlock,
        Data = row.WindowData,
        Sill = Math.Max(1, row.WindowSill),
        Width = Math.Max(1, row.WindowWidth),
        Height = Math.Max(1, row.WindowHeight),
        Spacing = Math.Max(0, row.WindowSpacing),
    };

    // ── porches ───────────────────────────────────────────────────────────────────────────────────────
    public async Task<HouseStyle?> ComposePorchAsync(long id, CancellationToken ct = default)
        => await parts.GetPorchAsync(id, ct) is { } row ? OnSample(PorchOf(row)) : null;

    public async Task<List<(PorchStyleRow Row, HouseStyle Style)>> ComposePorchesAsync(
        CancellationToken ct = default)
        => [.. (await parts.ListPorchesAsync(ct)).Select(row => (row, OnSample(PorchOf(row))))];

    public static HouseStyle ComposePorchDraft(PorchStyleSaveRequest draft) => OnSample(PorchOf(RowOf(draft)));

    public static PorchStyle PorchOf(PorchStyleRow row) => new()
    {
        Depth = Math.Max(1, row.Depth),
        Inset = Math.Max(0, row.Inset),
        Edge = PorchEdges.Canonical(row.Edge) switch
        {
            PorchEdges.NegZ => RoomEdge.NegZ,
            PorchEdges.PosZ => RoomEdge.PosZ,
            PorchEdges.NegX => RoomEdge.NegX,
            PorchEdges.PosX => RoomEdge.PosX,
            _ => null,
        },
        Roof = RoomStyleLibrary.FormOf(row.RoofForm),
        RailBlock = Math.Max(0, row.RailBlock),
    };

    public static PorchStyleRow RowOf(PorchStyleSaveRequest req) => new()
    {
        Name = req.Name,
        Depth = Math.Clamp(req.Depth, 1, 8),
        Inset = Math.Clamp(req.Inset, 0, 8),
        Edge = PorchEdges.Canonical(req.Edge),
        RoofForm = RoofForms.Canonical(req.Roof),
        RailBlock = Math.Max(0, req.RailBlock),
    };

    // ── drawing a part on its own ─────────────────────────────────────────────────────────────────────
    /// <summary>What every part is stood on. A part is not a building, so a picture of one has to stand it on
    /// something — but that something is <b>as little building as the part needs</b>, and each kind mutes a
    /// different piece of it.
    ///
    /// <para>Standing all three on one house is what made the cards unreadable: a roof card and a storey card
    /// drew the same gabled cottage, and the thing the card was about was a detail somewhere inside it. So the
    /// roof's building is a bare plinth of wall (<see cref="RoofBasis"/>) and the storey's lid is flat
    /// (<see cref="StoreyBasis"/>) — each picture is mostly the part it is a picture of. The <b>whole</b>
    /// building is what the room-style editor draws, because that is the level a house is composed at, and
    /// seeing it there rather than three times over is the point of composing at all.</para></summary>
    private static readonly HouseStyle Plain = HouseStyle.Wool with
    {
        Wall = RoomPart.Of(new SolidMaterial(Blocks.Planks, 1), 5),
        Foundation = new Foundation
        {
            Plate = RoomPart.Of(new SolidMaterial(Blocks.Planks)),
            Footing = new SolidMaterial(Blocks.Cobblestone),
        },
        Roof = new RoofStyle
        {
            Form = RoofForm.Gable,
            Overhang = 1,
            Hole = false,
            Body = new SolidMaterial(Blocks.Planks, 1),
            Verge = new SolidMaterial(Blocks.Planks, 5),
        },
        Doorway = new Doorway { Door = DoorMaterial.Air },
    };

    /// <summary>What a roof is stood on: unbroken wall and nothing else. No windows, no posts and no porch, so
    /// the picture is the roof — its form, its pitch, how far it oversails, and what its verge and gable face
    /// are made of.</summary>
    private static readonly HouseStyle RoofBasis = Plain with
    {
        Post = null,
        Windows = new WindowStyle(),
        Porch = null,
    };

    /// <summary>That wall at the <b>least height this roof can stand on</b>. An eave is part of the slope and
    /// keeps falling past the wall line, by one course for every block it oversails at the roof's own pitch —
    /// so a steep roof with a deep overhang reaches further below its own base than a short plinth is tall, and
    /// on a fixed one it comes down through the wall and into the ground. Sizing the wall from the roof's own
    /// numbers keeps the picture the roof rather than the building, and keeps it a building at all.</summary>
    private static HouseStyle WallFor(RoofStyleRow row)
        => RoofBasis with
        {
            Wall = RoomPart.Of(
                new SolidMaterial(Blocks.Planks, 1),
                Math.Max(3, Math.Clamp(row.Overhang, 0, 4) * Math.Clamp(row.Pitch, 1, 4) + 1)),
        };

    /// <summary>What a storey is drawn under: a <b>flat lid</b>, flush with the walls. A storey is a wall, the
    /// windows through it and how its floor is divided, and every one of those is hidden or overshadowed by a
    /// pitched roof standing over it — so the roof is taken away rather than merely made plain, and what is
    /// left in the picture is one room.</summary>
    private static readonly HouseStyle StoreyBasis = Plain with
    {
        Roof = Plain.Roof with { Form = RoofForm.Flat, Overhang = 0, Hole = false },
    };

    /// <summary>The building a storey is drawn as: one of itself, or <b>two</b> where it names a ceiling.
    /// A deck is the plate a storey stands on, and the ground storey's is the building's own floor — so a
    /// storey drawn alone never shows one, and the picture would be of a building in which the knob that was
    /// just turned does not exist, which is the one thing a preview is for preventing. Drawn twice, the upper
    /// copy stands on the plate it named.</summary>
    private static HouseStyle OnSample(Storey storey)
        => StoreyBasis with { Storeys = storey.Deck is null ? [storey] : [storey, storey] };

    /// <summary>What a porch is drawn against: the plain house, roof and all. A porch is a piece of the
    /// building's own footprint given up, its deck is the house's floor and its canopy is seated to clear the
    /// door under the house's own eave — so unlike the other two it cannot be read without the house.</summary>
    private static HouseStyle OnSample(PorchStyle porch)
        => Plain with { Porch = porch };

    // ── shared plumbing ───────────────────────────────────────────────────────────────────────────────
    private async Task<PartCourses> CoursesOf(IEnumerable<RoofStyleCourseRow> rows, CancellationToken ct)
    {
        var list = rows.ToList();
        return PartCourses.Of(list, await StylesOf(list.Select(course => course.StyleId), ct));
    }

    private async Task<PartCourses> CoursesOf(IEnumerable<StoreyStyleCourseRow> rows, CancellationToken ct)
    {
        var list = rows.ToList();
        return PartCourses.Of(list, await StylesOf(list.Select(course => course.StyleId), ct));
    }

    private async Task<PartCourses> CoursesOf(IEnumerable<PartCourse> courses, CancellationToken ct)
    {
        var list = courses.ToList();
        return new PartCourses(list, await StylesOf(list.Select(course => course.StyleId), ct));
    }

    private async Task<Dictionary<long, StyleRow>> StylesOf(IEnumerable<long> ids, CancellationToken ct)
        => (await styles.GetStylesAsync(ids, ct)).ToDictionary(style => style.Id);
}
