using Microsoft.AspNetCore.Components;
using PgmStudio.Client.Components;
using PgmStudio.Contracts;
using PgmStudio.Vocabulary;

namespace PgmStudio.Client.Features.Library;

/// <summary>
/// Authors one house part. One editor for a roof, a storey and a porch because they are the same act — pick a
/// form, bind styles to that kind's pieces, turn that kind's knobs — and what differs between them is
/// <see cref="PartKindInfo"/>, which is data.
///
/// <para>Exactly one of the three drafts is non-null, and which one is the kind. They are kept as the request
/// types rather than as a shared shape because a roof and a porch state different things, and a type that held
/// both would be two records wearing one name.</para>
/// </summary>
public partial class HousePartEditor
{
    [Parameter, EditorRequired] public PartKindInfo Part { get; set; } = default!;
    [Parameter, EditorRequired] public string Entry { get; set; } = "";
    [Parameter] public EventCallback<string?> OnSaved { get; set; }
    [Parameter] public EventCallback<string> OnName { get; set; }

    /// <summary>The outline row the kind's own knobs sit on, and the one a storey's windows sit on.</summary>
    private const string KnobsPart = "knobs";
    private const string WindowsPart = "windows";

    // The few block ids the editor names. Literals because the client references Contracts only — Blocks lives
    // in Minecraft, which is the export's layer and not the browser's.
    private const int OakStairs = 53, GlassPane = 102, WoodenSlab = 126, OakFence = 85;

    private static readonly RoomWindowDto NoWindows =
        new(WindowForms.None, Block: GlassPane, Data: 0, Sill: 2, Width: 2, Height: 2, Spacing: 3);

    private IReadOnlyList<StyleDto> styles = [];
    private IReadOnlyList<PaintBlockDto> blocks = [];
    private RoofStyleSaveRequest? roof;
    private StoreyStyleSaveRequest? storey;
    private PorchStyleSaveRequest? porch;
    private long? editingId;
    private string draftName = "";
    private string selected = KnobsPart;
    private string? note;
    private RoomStylePreviewDto? preview;

    private IEnumerable<IGrouping<string, StyleDto>> StylesByKind => styles.GroupBy(style => style.Kind);

    private StyleDto? StyleOf(long id) => styles.FirstOrDefault(style => style.Id == id);

    /// <summary>The stacked piece the outline has picked, or null when it has picked something else.</summary>
    private RoomPartInfo? Stacked => Part.Stacked.FirstOrDefault(piece => piece.Id == selected);

    /// <summary>The single-material piece the outline has picked, or null when it has picked something else.</summary>
    private RoomPartInfo? SingleBound => Part.Single.FirstOrDefault(piece => piece.Id == selected);

    private IReadOnlyList<EditorPart> Outline
    {
        get
        {
            List<EditorPart> rows = [new(KnobsPart, Part.KnobsTitle, Part.Kind.Icon, Badge: KnobsBadge)];
            if (storey is not null)
            {
                rows.Add(new EditorPart(WindowsPart, "Windows", "grid",
                    Badge: WindowForms.Canonical(storey.Windows.Form)));
            }
            rows.AddRange(Part.Stacked.Select(piece => new EditorPart(
                piece.Id, piece.Title, "layers",
                Badge: Courses(piece.Id).Count is var n and > 0 ? $"{n} course{(n == 1 ? "" : "s")}" : "built-in")));
            rows.AddRange(Part.Single.Select(piece => new EditorPart(
                piece.Id, piece.Title, "dot",
                Badge: StyleOf(Single(piece.Id))?.Name ?? "unbound")));
            return rows;
        }
    }

    private string KnobsBadge => roof is not null ? RoofForms.Canonical(roof.Form)
        : storey is not null ? $"clear {storey.Clear}"
        : porch is not null ? $"{porch.Depth} deep"
        : "";

    protected override async Task OnInitializedAsync()
    {
        blocks = await Library.BlocksAsync();
        styles = await Library.ListAsync<StyleDto>(LibraryKinds.Styles);
    }

    /// <summary>What the draft was loaded for. A parameter set that does not move the route is the host
    /// re-rendering — reloading there would re-read the row, report the name back up, and re-render the host
    /// again.</summary>
    private string? loaded;

    protected override async Task OnParametersSetAsync()
    {
        if (loaded == $"{Part.Kind.Slug}/{Entry}") return;
        loaded = $"{Part.Kind.Slug}/{Entry}";
        note = null;
        selected = KnobsPart;
        Clear();
        if (long.TryParse(Entry, out var id)) await Load(id);
        else StartNew();
        await OnName.InvokeAsync(draftName);
        await Preview();
    }

    private void StartNew()
    {
        editingId = null;
        draftName = "";
        switch (Part.Kind.Slug)
        {
            case LibraryKinds.RoofsSlug:
                roof = new RoofStyleSaveRequest("", RoofForms.Gable, 1, 1, false, false, []);
                break;
            case LibraryKinds.StoreysSlug:
                storey = new StoreyStyleSaveRequest("", 3, 1, 2, NoWindows, []);
                break;
            default:
                porch = new PorchStyleSaveRequest("", 2, 0, PorchEdges.Front, RoofForms.Shed, OakFence);
                break;
        }
    }

    private async Task Load(long id)
    {
        switch (Part.Kind.Slug)
        {
            case LibraryKinds.RoofsSlug:
                if (await Library.GetAsync<RoofStyleDetail>(Part.Kind, id) is not { } roofDetail)
                {
                    note = Unreadable;
                    return;
                }
                (editingId, draftName) = (roofDetail.Id, roofDetail.Name);
                roof = new RoofStyleSaveRequest(
                    roofDetail.Name, roofDetail.Form, roofDetail.Pitch,
                    roofDetail.Overhang, roofDetail.RoofHole, roofDetail.RidgeCap, roofDetail.Courses,
                    roofDetail.RoofSlab, roofDetail.RoofSlabData);
                break;
            case LibraryKinds.StoreysSlug:
                if (await Library.GetAsync<StoreyStyleDetail>(Part.Kind, id) is not { } storeyDetail)
                {
                    note = Unreadable;
                    return;
                }
                (editingId, draftName) = (storeyDetail.Id, storeyDetail.Name);
                storey = new StoreyStyleSaveRequest(
                    storeyDetail.Name, storeyDetail.Clear, storeyDetail.BorderWidth, storeyDetail.InlayInset,
                    storeyDetail.Windows, storeyDetail.Courses);
                break;
            default:
                if (await Library.GetAsync<PorchStyleDetail>(Part.Kind, id) is not { } porchDetail)
                {
                    note = Unreadable;
                    return;
                }
                (editingId, draftName) = (porchDetail.Id, porchDetail.Name);
                porch = new PorchStyleSaveRequest(
                    porchDetail.Name, porchDetail.Depth, porchDetail.Inset, porchDetail.Edge,
                    porchDetail.Roof, porchDetail.RailBlock);
                break;
        }
    }

    private const string Unreadable = "That part could not be read.";

    /// <summary>What an unbound course says: a part with none keeps the finish the stamper builds in.</summary>
    private const string Unbound = "Unbound — keeps the built-in finish";

    private void Clear() => (roof, storey, porch) = (null, null, null);

    private async Task SetName(string name)
    {
        draftName = name;
        await OnName.InvokeAsync(name);
    }

    private void Pick(string part) => selected = part;

    /// <summary>Which picture is showing. The isometric and the three cuts answer different questions, so
    /// asking for one alone is the only way to read it at the size the stage can give it.</summary>
    private string view = HouseViews.All;

    private void SetView(string picked) => view = picked;

    /// <summary>The sample the preview is drawn on. It changes what the picture is taken over and nothing
    /// about what would be saved.</summary>
    private string footprint = HouseFootprints.Default;

    private Task SetFootprint(string id)
    {
        footprint = id;
        return Preview();
    }


    // ── the course stacks ──────────────────────────────────────────────────────────────────────────
    /// <summary>The courses the open draft carries, whichever kind it is. A porch has none, so it answers
    /// empty and the markup that would list them never renders.</summary>
    private IReadOnlyList<RoomCourseDto> Bindings =>
        (IReadOnlyList<RoomCourseDto>?)roof?.Courses ?? storey?.Courses ?? [];

    private List<RoomCourseDto> Courses(string part)
        => [.. Bindings.Where(course => course.Part == part).OrderBy(course => course.Ordinal)];

    private Task AddCourse(string part)
    {
        if (styles.Count == 0) return Task.CompletedTask;
        var stack = Courses(part);
        return WriteCourses(part, [.. stack, new RoomCourseDto(part, stack.Count, styles[0].Id, 1)]);
    }

    private Task RemoveCourse(string part, int ordinal)
        => WriteCourses(part, [.. Courses(part).Where(course => course.Ordinal != ordinal)]);

    private Task BindCourse(string part, int ordinal, long styleId)
        => EditCourse(part, ordinal, course => course with { StyleId = styleId });

    private Task SetCourseHeight(string part, int ordinal, ChangeEventArgs e)
        => EditCourse(part, ordinal, course => course with { Height = Math.Max(1, Parse(e, course.Height)) });

    private Task EditCourse(string part, int ordinal, Func<RoomCourseDto, RoomCourseDto> edit)
        => WriteCourses(part, [.. Courses(part).Select(course => course.Ordinal == ordinal ? edit(course) : course)]);

    /// <summary>Replace one part's stack, renumbering it so an ordinal is always its position — the unique
    /// (owner, part, ordinal) index the courses are stored under leaves no room for a gap.</summary>
    private Task WriteCourses(string part, IReadOnlyList<RoomCourseDto> stack)
    {
        var renumbered = stack.Select((course, at) => course with { Part = part, Ordinal = at });
        IReadOnlyList<RoomCourseDto> next = [.. Bindings.Where(course => course.Part != part).Concat(renumbered)];
        if (roof is not null) roof = roof with { Courses = next };
        else if (storey is not null) storey = storey with { Courses = next };
        return Preview();
    }

    private long Single(string part) => Courses(part).FirstOrDefault()?.StyleId ?? 0;

    /// <summary>Whether a part has a style on it. A zone is not a zone until something names it — the floor
    /// part shows through instead — so the numbers that shape one (a border's width, an inlay's inset) decide
    /// nothing until then, and a knob that decides nothing should say so rather than sit there turning.</summary>
    private bool Bound(string part) => Courses(part).Count > 0;

    private Task BindSingle(string part, long styleId)
        => WriteCourses(part, styleId <= 0 ? [] : [new RoomCourseDto(part, 0, styleId, 1)]);

    // ── the roof's knobs ───────────────────────────────────────────────────────────────────────────
    private bool Sloped => RoofForms.Canonical(roof?.Form) != RoofForms.Flat;

    private Task SetForm(string form) => Roof(roof => roof with { Form = RoofForms.Canonical(form) });

    private Task SetPitch(ChangeEventArgs e) => Roof(r => r with { Pitch = Math.Clamp(Parse(e, r.Pitch), 1, 4) });

    private Task SetOverhang(ChangeEventArgs e)
        => Roof(r => r with { Overhang = Math.Clamp(Parse(e, r.Overhang), 0, 4) });

    private Task ToggleHole() => Roof(r => r with { RoofHole = !r.RoofHole });

    private Task ToggleRidgeCap() => Roof(r => r with { RidgeCap = !r.RidgeCap });

    /// <summary>Whether the roof climbs half a block at a time. -1 is a roof laid in whole blocks, which is
    /// what a row is until a slab is named.</summary>
    private bool Slabbed => (roof?.RoofSlab ?? -1) >= 0;

    private Task ToggleRoofSlab()
        => Roof(r => r with { RoofSlab = r.RoofSlab >= 0 ? -1 : 126, RoofSlabData = 0 });   // wooden slab

    private Task PickRoofSlab(PaintBlockDto block)
        => Roof(r => r with { RoofSlab = block.Id, RoofSlabData = block.Data });

    // ── the storey's knobs ─────────────────────────────────────────────────────────────────────────
    private bool Glazing => WindowForms.Canonical(storey?.Windows.Form) != WindowForms.None;

    private Task SetClear(ChangeEventArgs e)
        => Storey(s => s with { Clear = Math.Clamp(Parse(e, s.Clear), 3, 16) });

    private Task SetBorderWidth(ChangeEventArgs e)
        => Storey(s => s with { BorderWidth = Math.Clamp(Parse(e, s.BorderWidth), 1, 4) });

    private Task SetInlayInset(ChangeEventArgs e)
        => Storey(s => s with { InlayInset = Math.Clamp(Parse(e, s.InlayInset), 1, 8) });

    private Task SetWindows(RoomWindowDto window) => Storey(s => s with { Windows = window });

    // ── the porch's knobs ──────────────────────────────────────────────────────────────────────────
    private Task SetPorchDepth(ChangeEventArgs e)
        => Porch(p => p with { Depth = Math.Clamp(Parse(e, p.Depth), 1, 8) });

    private Task SetPorchInset(ChangeEventArgs e)
        => Porch(p => p with { Inset = Math.Clamp(Parse(e, p.Inset), 0, 8) });

    private Task SetPorchEdge(string edge) => Porch(porch => porch with { Edge = PorchEdges.Canonical(edge) });

    private Task SetPorchRoof(string form) => Porch(porch => porch with { Roof = RoofForms.Canonical(form) });

    private Task ToggleRail() => Porch(p => p with { RailBlock = p.RailBlock > 0 ? 0 : OakFence });

    private Task PickRailBlock(PaintBlockDto block) => Porch(p => p with { RailBlock = block.Id });

    // ── editing and previewing ─────────────────────────────────────────────────────────────────────
    private Task Roof(Func<RoofStyleSaveRequest, RoofStyleSaveRequest> edit)
    {
        if (roof is null) return Task.CompletedTask;
        roof = edit(roof);
        return Preview();
    }

    private Task Storey(Func<StoreyStyleSaveRequest, StoreyStyleSaveRequest> edit)
    {
        if (storey is null) return Task.CompletedTask;
        storey = edit(storey);
        return Preview();
    }

    private Task Porch(Func<PorchStyleSaveRequest, PorchStyleSaveRequest> edit)
    {
        if (porch is null) return Task.CompletedTask;
        porch = edit(porch);
        return Preview();
    }

    /// <summary>Re-draw the sample building from the draft as it stands. Composed server-side by exactly the
    /// path a save would take, so the picture and the save cannot disagree.</summary>
    private async Task Preview()
    {
        preview = Draft() is { } draft
            ? await Library.DraftPreviewAsync<RoomStylePreviewDto>(Part.Kind, draft, footprint)
            : null;
        StateHasChanged();
    }

    /// <summary>The one draft this kind is holding, named — what both the preview and the save post.</summary>
    private object? Draft(string? name = null) => Part.Kind.Slug switch
    {
        LibraryKinds.RoofsSlug => roof is null ? null : roof with { Name = name ?? draftName },
        LibraryKinds.StoreysSlug => storey is null ? null : storey with { Name = name ?? draftName },
        _ => porch is null ? null : porch with { Name = name ?? draftName },
    };

    private async Task Save()
    {
        if (string.IsNullOrWhiteSpace(draftName)) return;
        if (Draft(draftName.Trim()) is not { } request) { note = "That could not be saved."; return; }
        var saved = editingId is { } id
            ? await Library.UpdateAsync<PartSaved>(Part.Kind, id, request)
            : await Library.CreateAsync<PartSaved>(Part.Kind, request);
        if (saved is null) { note = "That could not be saved."; return; }
        note = editingId is null ? "Added to the library." : "Saved.";
        await OnSaved.InvokeAsync("saved");
        if (editingId is null) Nav.NavigateTo($"/library/{Part.Kind.Slug}/{saved.Id}");
        else editingId = saved.Id;
    }

    /// <summary>The one field a save's answer is read for — the three part kinds each answer their own detail
    /// and only the new row's id is acted on.</summary>
    private sealed record PartSaved(long Id);

    /// <summary>Forget the open part. A part a house still binds is refused with the names of the buildings
    /// wearing it, since deleting it would silently change every one of them.</summary>
    private async Task Delete()
    {
        if (editingId is not { } id) return;
        if (await Library.DeleteAsync(Part.Kind, id) is { Deleted: false } refused)
        {
            note = refused.BoundBy.Count > 0
                ? $"Still worn by {string.Join(", ", refused.BoundBy)} — change those first."
                : "That could not be forgotten.";
            return;
        }
        Nav.NavigateTo($"/library/{Part.Kind.Slug}");
    }

    private static int Parse(ChangeEventArgs e, int fallback)
        => int.TryParse((string?)e.Value, out var value) ? value : fallback;
}
