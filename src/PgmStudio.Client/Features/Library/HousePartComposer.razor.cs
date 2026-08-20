using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PgmStudio.Client.Components;
using PgmStudio.Contracts;
using PgmStudio.Vocabulary;

namespace PgmStudio.Client.Features.Library;

/// <summary>
/// The library's parts half (B71): roofs, storeys and porches, each authored once and bound by the houses that
/// want them. One composer for the three because they are the same act — pick a kind, bind styles to that
/// kind's parts, turn that kind's knobs — and what differs between them is <see cref="PartKindInfo"/>, which is
/// data rather than a third editor.
///
/// <para>The draft it edits is the save request itself, so what is previewed and what is saved are the same
/// value and the picture cannot promise a part the save would not build. A part is not a building, so every
/// picture stands it on a plain sample one: what differs between two cards is then the part and never the house
/// around it.</para>
/// </summary>
public partial class HousePartComposer
{
    [Inject] public TerrainLibraryClient Library { get; set; } = default!;
    [Inject] public IJSRuntime JS { get; set; } = default!;

    // Every edit renders fresh <i data-lucide> nodes, and lucide only processes what exists when it runs.
    protected override async Task OnAfterRenderAsync(bool firstRender) => await JS.InvokeVoidAsync("studio.icons");

    private PartKindInfo kind = PartKindInfo.All[0];

    /// <summary>The cards showing, flattened out of whichever kind is on — the three summaries differ only in
    /// their type, and a list that had to be three lists would make the shared markup three copies.</summary>
    private List<(long Id, string Name, string Preview)> entries = [];

    private IReadOnlyList<StyleDto> styles = [];
    private IReadOnlyList<PaintBlockDto> blocks = [];

    private bool loading = true;
    private bool figureHelp;
    private string? note;

    private long? editingId;
    private string draftName = "";
    private RoomStylePreviewDto? preview;

    // Exactly one of the three is non-null, and which one is the kind. Kept as the request types rather than as
    // one union so the save is the value the editor was holding all along.
    private RoofStyleSaveRequest? roof;
    private StoreyStyleSaveRequest? storey;
    private PorchStyleSaveRequest? porch;

    /// <summary>Whether anything is open in the rail. The three drafts are one editor, so the rail asks this
    /// rather than which of them is set.</summary>
    private bool Editing => roof is not null || storey is not null || porch is not null;

    private IEnumerable<IGrouping<string, StyleDto>> StylesByKind => styles.GroupBy(style => style.Kind);

    private StyleDto? StyleOf(long id) => styles.FirstOrDefault(style => style.Id == id);

    protected override async Task OnInitializedAsync()
    {
        blocks = await Library.BlocksAsync();
        await Reload();
    }

    private async Task Switch(PartKindInfo next)
    {
        if (next.Id == kind.Id) return;
        kind = next;
        Close();
        await Reload();
    }

    private async Task Reload()
    {
        loading = true;
        styles = await Library.StylesAsync();
        entries = kind.Id switch
        {
            PartKindInfo.Roof => [.. (await Library.RoofStylesAsync()).Select(r => (r.Id, r.Name, r.Preview))],
            PartKindInfo.Storey => [.. (await Library.StoreyStylesAsync()).Select(r => (r.Id, r.Name, r.Preview))],
            _ => [.. (await Library.PorchStylesAsync()).Select(r => (r.Id, r.Name, r.Preview))],
        };
        loading = false;
        StateHasChanged();
    }

    // ── the draft ──────────────────────────────────────────────────────────────────────────────────
    private async Task StartNew()
    {
        editingId = null;
        draftName = "";
        note = null;
        Clear();
        switch (kind.Id)
        {
            case PartKindInfo.Roof:
                roof = new RoofStyleSaveRequest("", RoofForms.Gable, 1, 1, false, false, []);
                break;
            case PartKindInfo.Storey:
                storey = new StoreyStyleSaveRequest("", 3, 1, 2, NoWindows, []);
                break;
            default:
                porch = new PorchStyleSaveRequest("", 2, 0, PorchEdges.Front, RoofForms.Shed, OakFence);
                break;
        }
        await Preview();
    }

    // The few block ids the editor names. Literals because the client references Contracts only — Blocks lives
    // in Minecraft, which is the export's layer and not the browser's.
    private const int OakStairs = 53, GlassPane = 102, WoodenSlab = 126, OakFence = 85;

    private static readonly RoomWindowDto NoWindows =
        new(WindowForms.None, Block: GlassPane, Data: 0, Sill: 2, Width: 2, Height: 2, Spacing: 3);

    private async Task Edit(long id)
    {
        note = null;
        Clear();
        switch (kind.Id)
        {
            case PartKindInfo.Roof:
                if (await Library.RoofStyleAsync(id) is not { } roofDetail) { note = Unreadable; return; }
                (editingId, draftName) = (roofDetail.Id, roofDetail.Name);
                roof = new RoofStyleSaveRequest(
                    roofDetail.Name, roofDetail.Form, roofDetail.Pitch,
                    roofDetail.Overhang, roofDetail.RoofHole, roofDetail.RidgeCap, roofDetail.Courses,
                    roofDetail.RoofSlab, roofDetail.RoofSlabData);
                break;
            case PartKindInfo.Storey:
                if (await Library.StoreyStyleAsync(id) is not { } storeyDetail) { note = Unreadable; return; }
                (editingId, draftName) = (storeyDetail.Id, storeyDetail.Name);
                storey = new StoreyStyleSaveRequest(
                    storeyDetail.Name, storeyDetail.Clear, storeyDetail.BorderWidth, storeyDetail.InlayInset,
                    storeyDetail.Windows, storeyDetail.Courses);
                break;
            default:
                if (await Library.PorchStyleAsync(id) is not { } porchDetail) { note = Unreadable; return; }
                (editingId, draftName) = (porchDetail.Id, porchDetail.Name);
                porch = new PorchStyleSaveRequest(
                    porchDetail.Name, porchDetail.Depth, porchDetail.Inset, porchDetail.Edge,
                    porchDetail.Roof, porchDetail.RailBlock);
                break;
        }
        await Preview();
    }

    private const string Unreadable = "That part could not be read.";

    private void Clear() => (roof, storey, porch) = (null, null, null);

    private void Close()
    {
        Clear();
        editingId = null;
        preview = null;
        note = null;
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

    private Task BindCourse(string part, int ordinal, ChangeEventArgs e)
        => EditCourse(part, ordinal, course => course with
        {
            StyleId = long.TryParse((string?)e.Value, out var id) ? id : course.StyleId,
        });

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

    private Task BindSingle(string part, ChangeEventArgs e)
    {
        var id = long.TryParse((string?)e.Value, out var picked) ? picked : 0;
        return WriteCourses(part, id <= 0 ? [] : [new RoomCourseDto(part, 0, id, 1)]);
    }

    // ── the roof's knobs ───────────────────────────────────────────────────────────────────────────
    private bool Sloped => RoofForms.Canonical(roof?.Form) != RoofForms.Flat;

    private Task SetForm(ChangeEventArgs e)
        => Roof(r => r with { Form = RoofForms.Canonical((string?)e.Value) });

    private Task SetPitch(ChangeEventArgs e) => Roof(r => r with { Pitch = Math.Clamp(Parse(e, r.Pitch), 1, 4) });

    private Task SetOverhang(ChangeEventArgs e)
        => Roof(r => r with { Overhang = Math.Clamp(Parse(e, r.Overhang), 0, 4) });

    private Task ToggleHole() => Roof(r => r with { RoofHole = !r.RoofHole });

    private Task ToggleRidgeCap() => Roof(r => r with { RidgeCap = !r.RidgeCap });

    // ── the storey's knobs ─────────────────────────────────────────────────────────────────────────
    private bool Glazing => WindowForms.Canonical(storey?.Windows.Form) != WindowForms.None;

    private Task SetClear(ChangeEventArgs e)
        => Storey(s => s with { Clear = Math.Clamp(Parse(e, s.Clear), 3, 16) });

    private Task SetBorderWidth(ChangeEventArgs e)
        => Storey(s => s with { BorderWidth = Math.Clamp(Parse(e, s.BorderWidth), 1, 4) });

    private Task SetInlayInset(ChangeEventArgs e)
        => Storey(s => s with { InlayInset = Math.Clamp(Parse(e, s.InlayInset), 1, 8) });

    /// <summary>Switching form brings that form's own default block rather than carrying the last one: a
    /// lattice needs stairs and a band needs slabs, and a pane block turned into a stair facing is a solid
    /// patch of wall.</summary>
    private Task SetWindowForm(ChangeEventArgs e) => Window(window =>
    {
        var form = WindowForms.Canonical((string?)e.Value);
        return window with { Form = form, Block = DefaultWindowBlock(form), Data = 0 };
    });

    private static int DefaultWindowBlock(string form) => form switch
    {
        WindowForms.StairLattice => OakStairs,
        WindowForms.SlabBanded => WoodenSlab,
        _ => GlassPane,
    };

    private Task PickWindowBlock(PaintBlockDto block)
        => Window(window => window with { Block = block.Id, Data = block.Data });

    private Task SetWindowSill(ChangeEventArgs e)
        => Window(window => window with { Sill = Math.Clamp(Parse(e, window.Sill), 1, 16) });

    private Task SetWindowWidth(ChangeEventArgs e)
        => Window(window => window with { Width = Math.Clamp(Parse(e, window.Width), 1, 8) });

    private Task SetWindowHeight(ChangeEventArgs e)
        => Window(window => window with { Height = Math.Clamp(Parse(e, window.Height), 1, 8) });

    private Task SetWindowSpacing(ChangeEventArgs e)
        => Window(window => window with { Spacing = Math.Clamp(Parse(e, window.Spacing), 0, 16) });

    private Task Window(Func<RoomWindowDto, RoomWindowDto> edit)
        => Storey(s => s with { Windows = edit(s.Windows) });

    // ── the porch's knobs ──────────────────────────────────────────────────────────────────────────
    private Task SetPorchDepth(ChangeEventArgs e)
        => Porch(p => p with { Depth = Math.Clamp(Parse(e, p.Depth), 1, 8) });

    private Task SetPorchInset(ChangeEventArgs e)
        => Porch(p => p with { Inset = Math.Clamp(Parse(e, p.Inset), 0, 8) });

    private Task SetPorchEdge(ChangeEventArgs e)
        => Porch(p => p with { Edge = PorchEdges.Canonical((string?)e.Value) });

    private Task SetPorchRoof(ChangeEventArgs e)
        => Porch(p => p with { Roof = RoofForms.Canonical((string?)e.Value) });

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

    private static int Parse(ChangeEventArgs e, int fallback)
        => int.TryParse((string?)e.Value, out var value) ? value : fallback;

    /// <summary>Re-draw the sample building from the draft as it stands. Composed server-side by exactly the
    /// path a save would take, so the picture and the save cannot disagree.</summary>
    private async Task Preview()
    {
        preview = kind.Id switch
        {
            PartKindInfo.Roof when roof is not null
                => await Library.RoofStyleDraftPreviewAsync(roof with { Name = draftName }),
            PartKindInfo.Storey when storey is not null
                => await Library.StoreyStyleDraftPreviewAsync(storey with { Name = draftName }),
            PartKindInfo.Porch when porch is not null
                => await Library.PorchStyleDraftPreviewAsync(porch with { Name = draftName }),
            _ => null,
        };
        StateHasChanged();
    }

    // ── saving ─────────────────────────────────────────────────────────────────────────────────────
    private async Task Save()
    {
        if (string.IsNullOrWhiteSpace(draftName)) return;
        var name = draftName.Trim();
        var saved = kind.Id switch
        {
            PartKindInfo.Roof => await SaveRoof(name),
            PartKindInfo.Storey => await SaveStorey(name),
            _ => await SavePorch(name),
        };
        if (!saved) { note = "That could not be saved."; return; }
        note = null;
        Close();
        await Reload();
    }

    private async Task<bool> SaveRoof(string name)
    {
        if (roof is null) return false;
        var request = roof with { Name = name };
        return (editingId is { } id
            ? await Library.UpdateRoofStyleAsync(id, request)
            : await Library.CreateRoofStyleAsync(request)) is not null;
    }

    private async Task<bool> SaveStorey(string name)
    {
        if (storey is null) return false;
        var request = storey with { Name = name };
        return (editingId is { } id
            ? await Library.UpdateStoreyStyleAsync(id, request)
            : await Library.CreateStoreyStyleAsync(request)) is not null;
    }

    private async Task<bool> SavePorch(string name)
    {
        if (porch is null) return false;
        var request = porch with { Name = name };
        return (editingId is { } id
            ? await Library.UpdatePorchStyleAsync(id, request)
            : await Library.CreatePorchStyleAsync(request)) is not null;
    }

    /// <summary>Forget the open part. A part a house still binds is refused with the names of the buildings
    /// wearing it, since deleting it would silently change every one of them.</summary>
    private async Task Delete()
    {
        if (editingId is not { } id) return;
        var response = kind.Id switch
        {
            PartKindInfo.Roof => await Library.DeleteRoofStyleAsync(id),
            PartKindInfo.Storey => await Library.DeleteStoreyStyleAsync(id),
            _ => await Library.DeletePorchStyleAsync(id),
        };
        if ((int)response.StatusCode == 409)
        {
            note = "Still used by a room style — change those first.";
            return;
        }
        Close();
        await Reload();
    }
}
