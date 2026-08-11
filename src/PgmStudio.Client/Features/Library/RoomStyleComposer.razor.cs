using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PgmStudio.Client.Components;
using PgmStudio.Contracts;

namespace PgmStudio.Client.Features.Library;

/// <summary>
/// The library's room half (G34b): a room style is composed, not drawn — each part of the shell takes an
/// ordered stack of saved styles, plus the knobs that are not materials at all. The draft it edits is the
/// <see cref="RoomStyleSaveRequest"/> itself, so what is previewed and what is saved are the same value and
/// the picture cannot promise a shell the save would not build.
///
/// <para>A part with no courses is one the style does not override: it keeps the built-in finish, the way an
/// unbound theme bucket does. That is what makes the library worth having for a style that only changes its
/// roof.</para>
/// </summary>
public partial class RoomStyleComposer
{
    [Inject] public TerrainLibraryClient Library { get; set; } = default!;
    [Inject] public IJSRuntime JS { get; set; } = default!;

    // Every edit renders fresh <i data-lucide> nodes, and lucide only processes what exists when it runs.
    protected override async Task OnAfterRenderAsync(bool firstRender) => await JS.InvokeVoidAsync("studio.icons");

    private IReadOnlyList<RoomStyleSummary> rooms = [];
    private IReadOnlyList<StyleDto> styles = [];
    private IReadOnlyList<DoorOptionDto> doors = [];

    /// <summary>The block shortlist the window and rail pickers offer. Windows and railings are chosen as a
    /// block rather than as a bound style: their metadata is geometry — which way a stair climbs, which half a
    /// slab fills — and a material would resolve that from where the cell sits.</summary>
    private IReadOnlyList<PaintBlockDto> blocks = [];

    private bool loading = true;
    private string? note;

    /// <summary>Whether the "?" beside the rail's title is showing what the pinned sample is.</summary>
    private bool figureHelp;

    /// <summary>The room style open in the rail, or null when nothing is. Null id = not in the library yet.</summary>
    private RoomStyleSaveRequest? draft;
    private long? editingId;
    private string draftName = "";
    private RoomStylePreviewDto? preview;

    private IEnumerable<IGrouping<string, StyleDto>> StylesByKind => styles.GroupBy(style => style.Kind);

    private StyleDto? StyleOf(long id) => styles.FirstOrDefault(style => style.Id == id);

    protected override async Task OnInitializedAsync()
    {
        doors = await Library.RoomDoorsAsync();
        blocks = await Library.BlocksAsync();
        await Reload();
    }

    private async Task Reload()
    {
        loading = true;
        rooms = await Library.RoomStylesAsync();
        styles = await Library.StylesAsync();
        loading = false;
        StateHasChanged();
    }

    // ── the draft ──────────────────────────────────────────────────────────────────────────────────
    /// <summary>What a new room style starts as: the shipped shell — a flat lid with a hole in it, no windows
    /// and no porch — so the first thing an author sees is what the export builds today and every knob turned
    /// from here is a visible change to it.</summary>
    private RoomStyleSaveRequest EmptyDraft(string name) => new(
        name, FloorDepth: 1, WallHeight: 7, RoofThickness: 1,
        RoofForm: RoofForms.Flat, Pitch: 1, Overhang: 0, RoofHole: true, RidgeCap: false,
        BorderWidth: 1, InlayInset: 2, Storeys: 1, StoreyClear: 0,
        Windows: NoWindows, Porch: null,
        Door: doors.FirstOrDefault()?.Slug ?? "", DoorHeight: 3, Courses: []);

    private static readonly RoomWindowDto NoWindows =
        new(WindowForms.None, Block: 102, Data: 0, Sill: 2, Width: 2, Height: 2, Spacing: 3);

    /// <summary>The porch a style gets the moment one is switched on: two blocks of the front wall's strip,
    /// running its full width, under a lean-to with a fence along its open edges.</summary>
    private static readonly RoomPorchDto DefaultPorch =
        new(Depth: 2, Inset: 0, Edge: PorchEdges.Front, Roof: RoofForms.Shed, RailBlock: 85);

    /// <summary>Open the rail on a room style that is not in the library yet. It is unnamed: the name is the
    /// rail's first field, the same as a style's, and the save stays disabled until it is filled in.</summary>
    private async Task StartNew()
    {
        editingId = null;
        draftName = "";
        note = null;
        draft = EmptyDraft(draftName);
        await Preview();
    }

    private async Task Edit(long id)
    {
        var detail = await Library.RoomStyleAsync(id);
        if (detail is null) { note = "That room style could not be read."; return; }
        editingId = detail.Id;
        draftName = detail.Name;
        note = null;
        draft = new RoomStyleSaveRequest(
            detail.Name, detail.FloorDepth, detail.WallHeight, detail.RoofThickness,
            detail.RoofForm, detail.Pitch, detail.Overhang, detail.RoofHole, detail.RidgeCap,
            detail.BorderWidth, detail.InlayInset, detail.Storeys, detail.StoreyClear,
            detail.Windows, detail.Porch,
            detail.Door, detail.DoorHeight, detail.Courses);
        await Preview();
    }

    private void Close()
    {
        draft = null;
        editingId = null;
        preview = null;
        note = null;
    }

    // ── the course stacks ──────────────────────────────────────────────────────────────────────────
    /// <summary>One part's courses in stack order — what the rail lays out, and the order the ordinals are
    /// renumbered into after every edit so they stay 0..n-1 with no gaps.</summary>
    private List<RoomCourseDto> Courses(string part)
        => [.. draft!.Courses.Where(course => course.Part == part).OrderBy(course => course.Ordinal)];

    private int Extent(string part) => part switch
    {
        RoomParts.Floor => draft!.FloorDepth,
        RoomParts.Roof => draft!.RoofThickness,
        _ => draft!.WallHeight,
    };

    private Task AddCourse(string part)
    {
        if (draft is null || styles.Count == 0) return Task.CompletedTask;
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
    /// (room, part, ordinal) index the courses are stored under leaves no room for a gap.</summary>
    private Task WriteCourses(string part, IReadOnlyList<RoomCourseDto> stack)
    {
        if (draft is null) return Task.CompletedTask;
        var renumbered = stack.Select((course, at) => course with { Part = part, Ordinal = at });
        draft = draft with
        {
            Courses = [.. draft.Courses.Where(course => course.Part != part).Concat(renumbered)],
        };
        return Preview();
    }

    /// <summary>The one style bound to a part that takes a material rather than a stack — a post, a sill, a
    /// verge, one zone of the floor's top course. Zero unbinds it, which is the part keeping the built-in
    /// finish rather than resolving to nothing.</summary>
    private long Single(string part) => Courses(part).FirstOrDefault()?.StyleId ?? 0;

    private Task BindSingle(string part, ChangeEventArgs e)
    {
        var id = long.TryParse((string?)e.Value, out var picked) ? picked : 0;
        return WriteCourses(part, id <= 0 ? [] : [new RoomCourseDto(part, 0, id, 1)]);
    }

    // ── the knobs ──────────────────────────────────────────────────────────────────────────────────
    private Task SetExtent(string part, ChangeEventArgs e) => Knob(d => part switch
    {
        RoomParts.Floor => d with { FloorDepth = Math.Max(1, Parse(e, d.FloorDepth)) },
        RoomParts.Roof => d with { RoofThickness = Math.Max(1, Parse(e, d.RoofThickness)) },
        _ => d with { WallHeight = Math.Max(1, Parse(e, d.WallHeight)) },
    });

    /// <summary>Which of the six roofs. The pitch and the ridge cap only mean anything on a sloped one and the
    /// hole only on the lid, so each is offered under the form rather than as a knob of its own.</summary>
    private Task SetForm(ChangeEventArgs e) =>
        Knob(d => d with { RoofForm = RoofForms.Canonical((string?)e.Value) });

    private bool Sloped => RoofForms.Canonical(draft?.RoofForm) != RoofForms.Flat;

    private Task SetPitch(ChangeEventArgs e) => Knob(d => d with { Pitch = Math.Clamp(Parse(e, d.Pitch), 1, 4) });

    private Task SetOverhang(ChangeEventArgs e) =>
        Knob(d => d with { Overhang = Math.Clamp(Parse(e, d.Overhang), 0, 4) });

    private Task ToggleHole() => Knob(d => d with { RoofHole = !d.RoofHole });

    private Task ToggleRidgeCap() => Knob(d => d with { RidgeCap = !d.RidgeCap });

    private Task SetBorderWidth(ChangeEventArgs e) =>
        Knob(d => d with { BorderWidth = Math.Clamp(Parse(e, d.BorderWidth), 1, 4) });

    private Task SetInlayInset(ChangeEventArgs e) =>
        Knob(d => d with { InlayInset = Math.Clamp(Parse(e, d.InlayInset), 1, 8) });

    // ── the storeys ────────────────────────────────────────────────────────────────────────────────
    /// <summary>How many storeys are stacked inside. One is the building every style was before there were
    /// storeys, so the whole feature starts switched off.</summary>
    private Task SetStoreys(ChangeEventArgs e) =>
        Knob(d => d with { Storeys = Math.Clamp(Parse(e, d.Storeys), 1, 8) });

    /// <summary>The air in each storey. Zero defers to the wall height, so a style that never touches this
    /// stacks storeys as tall as its wall already was.</summary>
    private Task SetStoreyClear(ChangeEventArgs e) =>
        Knob(d => d with { StoreyClear = Math.Clamp(Parse(e, d.StoreyClear), 0, 16) });

    /// <summary>The clear height each storey actually builds at — what the caption reports, since a stored 0
    /// means "the wall height" and an author reading "0 blocks of air" would have to work that out.</summary>
    private int EffectiveClear => Math.Max(3, draft!.StoreyClear > 0 ? draft.StoreyClear : draft.WallHeight);

    // ── the windows ────────────────────────────────────────────────────────────────────────────────
    private RoomWindowDto Windows => draft?.Windows ?? NoWindows;

    private bool Glazing => WindowForms.Canonical(Windows.Form) != WindowForms.None;

    /// <summary>Switching form carries the block with it only where the new form can use it — a lattice needs
    /// stairs and a band needs slabs, and a pane block turned into a stair facing is a solid patch of wall — so
    /// each form brings its own default block rather than inheriting the last one.</summary>
    private Task SetWindowForm(ChangeEventArgs e) => Window(window =>
    {
        var form = WindowForms.Canonical((string?)e.Value);
        return window with { Form = form, Block = DefaultWindowBlock(form), Data = 0 };
    });

    private static int DefaultWindowBlock(string form) => form switch
    {
        WindowForms.StairLattice => 53,      // oak stairs
        WindowForms.SlabBanded => 126,       // wooden slab
        _ => 102,                            // glass pane
    };

    private Task PickWindowBlock(PaintBlockDto block)
        => Window(window => window with { Block = block.Id, Data = block.Data });

    private Task SetWindowSill(ChangeEventArgs e) =>
        Window(window => window with { Sill = Math.Clamp(Parse(e, window.Sill), 1, 16) });

    private Task SetWindowWidth(ChangeEventArgs e) =>
        Window(window => window with { Width = Math.Clamp(Parse(e, window.Width), 1, 8) });

    private Task SetWindowHeight(ChangeEventArgs e) =>
        Window(window => window with { Height = Math.Clamp(Parse(e, window.Height), 1, 8) });

    private Task SetWindowSpacing(ChangeEventArgs e) =>
        Window(window => window with { Spacing = Math.Clamp(Parse(e, window.Spacing), 0, 16) });

    private Task Window(Func<RoomWindowDto, RoomWindowDto> edit)
        => Knob(d => d with { Windows = edit(d.Windows ?? NoWindows) });

    // ── the porch ──────────────────────────────────────────────────────────────────────────────────
    private RoomPorchDto? Porch => draft?.Porch;

    /// <summary>A porch is present or it is not — an absent one is not a porch of depth nothing, because the
    /// depth it would come back at is the one the author last set.</summary>
    private Task TogglePorch() => Knob(d => d with { Porch = d.Porch is null ? DefaultPorch : null });

    private Task SetPorchDepth(ChangeEventArgs e) =>
        Deck(porch => porch with { Depth = Math.Clamp(Parse(e, porch.Depth), 1, 8) });

    private Task SetPorchInset(ChangeEventArgs e) =>
        Deck(porch => porch with { Inset = Math.Clamp(Parse(e, porch.Inset), 0, 8) });

    private Task SetPorchEdge(ChangeEventArgs e) =>
        Deck(porch => porch with { Edge = PorchEdges.Canonical((string?)e.Value) });

    private Task SetPorchRoof(ChangeEventArgs e) =>
        Deck(porch => porch with { Roof = RoofForms.Canonical((string?)e.Value) });

    private Task PickRailBlock(PaintBlockDto block) => Deck(porch => porch with { RailBlock = block.Id });

    private Task ToggleRail() =>
        Deck(porch => porch with { RailBlock = porch.RailBlock > 0 ? 0 : DefaultPorch.RailBlock });

    private Task Deck(Func<RoomPorchDto, RoomPorchDto> edit)
        => draft?.Porch is { } porch ? Knob(d => d with { Porch = edit(porch) }) : Task.CompletedTask;

    private Task SetDoor(ChangeEventArgs e) => Knob(d => d with { Door = (string?)e.Value ?? d.Door });

    private Task SetDoorHeight(ChangeEventArgs e) => Knob(d => d with { DoorHeight = Math.Max(1, Parse(e, d.DoorHeight)) });

    private Task Knob(Func<RoomStyleSaveRequest, RoomStyleSaveRequest> edit)
    {
        if (draft is null) return Task.CompletedTask;
        draft = edit(draft);
        return Preview();
    }

    private static int Parse(ChangeEventArgs e, int fallback)
        => int.TryParse((string?)e.Value, out var value) ? value : fallback;

    // ── preview + save ─────────────────────────────────────────────────────────────────────────────
    // Both go through the same request value: the preview is what the save would compose to.
    private async Task Preview()
    {
        preview = draft is null ? null : await Library.RoomStyleDraftPreviewAsync(Saveable(draft));
        StateHasChanged();
    }

    private RoomStyleSaveRequest Saveable(RoomStyleSaveRequest current) => current with
    {
        Name = string.IsNullOrWhiteSpace(draftName) ? current.Name : draftName.Trim(),
    };

    private async Task Save()
    {
        if (draft is null || string.IsNullOrWhiteSpace(draftName)) return;
        var request = Saveable(draft);
        var saved = editingId is { } id
            ? await Library.UpdateRoomStyleAsync(id, request)
            : await Library.CreateRoomStyleAsync(request);
        if (saved is null) { note = "The library refused that room style."; return; }
        note = editingId is null ? "Added to the library." : "Saved.";
        editingId = saved.Id;
        await Reload();
    }

    private async Task SaveAsCopy()
    {
        if (draft is null) return;
        var copy = await Library.CreateRoomStyleAsync(Saveable(draft) with { Name = $"{draftName.Trim()} copy" });
        if (copy is null) { note = "The library refused that room style."; return; }
        editingId = copy.Id;
        draftName = copy.Name;
        note = "Saved as a new room style; the one it was copied from is unchanged.";
        await Reload();
    }

    private async Task Delete()
    {
        if (editingId is not { } id) return;
        await Library.DeleteRoomStyleAsync(id);
        Close();
        await Reload();
    }
}
