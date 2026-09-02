using Microsoft.AspNetCore.Components;
using PgmStudio.Client.Components;
using PgmStudio.Contracts;
using PgmStudio.Vocabulary;

namespace PgmStudio.Client.Features.Library;

/// <summary>
/// Composes one whole building. Each part of the shell takes an ordered stack of saved styles, plus the knobs
/// that are not materials at all. The draft it edits is the <see cref="RoomStyleSaveRequest"/> itself, so what
/// is previewed and what is saved are the same value and the picture cannot promise a shell the save would not
/// build.
///
/// <para>A part with no courses is one the style does not override: it keeps the built-in finish, the way an
/// unbound theme bucket does. That is what makes the library worth having for a style that only changes its
/// roof.</para>
/// </summary>
public partial class HouseEditor
{
    [Parameter, EditorRequired] public string Entry { get; set; } = "";
    [Parameter] public EventCallback<string?> OnSaved { get; set; }
    [Parameter] public EventCallback<string> OnName { get; set; }

    /// <summary>The outline rows that are not one of the shell's own parts.</summary>
    private const string ComposedPart = "composed";
    private const string TrimPart = "trim";
    private const string PorchPart = "porch";
    private const string DoorPart = "door";

    private IReadOnlyList<StyleDto> styles = [];
    private IReadOnlyList<DoorOptionDto> doors = [];

    // The parts this house may be composed from. Names and ids only: the editor binds one by picking it, and
    // what it looks like is the picture that part's own library draws.
    private IReadOnlyList<RoofStyleSummary> roofs = [];
    private IReadOnlyList<StoreyStyleSummary> storeys = [];
    private IReadOnlyList<PorchStyleSummary> porches = [];

    /// <summary>The block shortlist the window and rail pickers offer. Windows and railings are chosen as a
    /// block rather than as a bound style: their metadata is geometry — which way a stair climbs, which half a
    /// slab fills — and a material would resolve that from where the cell sits.</summary>
    private IReadOnlyList<PaintBlockDto> blocks = [];

    private RoomStyleSaveRequest? draft;
    private long? editingId;
    private string draftName = "";
    private string selected = ComposedPart;
    private string? note;
    private RoomStylePreviewDto? preview;

    private IEnumerable<IGrouping<string, StyleDto>> StylesByKind => styles.GroupBy(style => style.Kind);

    private StyleDto? StyleOf(long id) => styles.FirstOrDefault(style => style.Id == id);

    /// <summary>The shell part the outline has picked, or null when it has picked a row that is not one.</summary>
    private RoomPartInfo? Piece => RoomPartInfo.All.FirstOrDefault(part => part.Id == selected);

    private IReadOnlyList<EditorPart> Outline
    {
        get
        {
            if (draft is null) return [];
            List<EditorPart> rows =
            [
                new(ComposedPart, "Composed from", "blocks", Badge: BoundParts),
                .. RoomPartInfo.All.Select(part => new EditorPart(
                    part.Id, part.Title, "layers", Badge: PartBadge(part))),
                new(TrimPart, "Frame and trim", "dot", Badge: TrimBadge),
                new(PorchPart, "Porch", "door-open",
                    Badge: draft.Porch is null ? "none" : PorchEdges.Canonical(draft.Porch.Edge)),
                new(DoorPart, "Doorway", "door-open", Badge: draft.Door),
            ];
            return rows;
        }
    }

    private string BoundParts
    {
        get
        {
            var bound = (draft!.RoofStyleId is not null ? 1 : 0) + (draft.PorchStyleId is not null ? 1 : 0)
                + (draft.StoreyStack.Count > 0 ? 1 : 0);
            return bound == 0 ? "own" : $"{bound} bound";
        }
    }

    private string PartBadge(RoomPartInfo part)
    {
        if (!part.Stacked) return StyleOf(Single(part.Id))?.Name ?? "built-in";
        var count = Courses(part.Id).Count;
        return count == 0 ? "built-in" : $"{count} course{(count == 1 ? "" : "s")}";
    }

    private string TrimBadge
    {
        get
        {
            var bound = RoomPartInfo.Trim.Count(part => Single(part.Id) > 0);
            return bound == 0 ? "plain" : $"{bound} bound";
        }
    }

    private string Footnote => draft is null
        ? ""
        : $"{draft.Storeys} storey{(draft.Storeys == 1 ? "" : "s")} · {RoofForms.Canonical(draft.RoofForm)} roof"
          + $" · door {draft.DoorHeight} tall";

    protected override async Task OnInitializedAsync()
    {
        doors = await Library.RoomDoorsAsync();
        blocks = await Library.BlocksAsync();
        styles = await Library.ListAsync<StyleDto>(LibraryKinds.Styles);
        roofs = await Library.ListAsync<RoofStyleSummary>(LibraryKinds.Roofs);
        storeys = await Library.ListAsync<StoreyStyleSummary>(LibraryKinds.Storeys);
        porches = await Library.ListAsync<PorchStyleSummary>(LibraryKinds.Porches);
    }

    /// <summary>What the draft was loaded for. A parameter set that does not move the route is the host
    /// re-rendering — reloading there would re-read the row, report the name back up, and re-render the host
    /// again.</summary>
    private string? loaded;

    protected override async Task OnParametersSetAsync()
    {
        if (loaded == Entry) return;
        loaded = Entry;
        note = null;
        selected = ComposedPart;
        if (long.TryParse(Entry, out var id)) await Load(id);
        else StartNew();
        await OnName.InvokeAsync(draftName);
        await Preview();
    }

    private async Task SetName(string name)
    {
        draftName = name;
        await OnName.InvokeAsync(name);
    }

    /// <summary>Open a row — and redraw, because the pictures are cut to whatever is open: picking the roof is
    /// how an author asks to see the roof.</summary>
    private Task Pick(string part)
    {
        selected = part;
        return Preview();
    }

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


    // ── the draft ──────────────────────────────────────────────────────────────────────────────────
    /// <summary>What a new house starts as: the shipped shell — a flat lid with a hole in it, no windows and
    /// no porch — so the first thing an author sees is what the export builds today and every knob turned from
    /// here is a visible change to it.</summary>
    private RoomStyleSaveRequest EmptyDraft(string name) => new(
        name, FloorDepth: 1, WallHeight: 7,
        RoofForm: RoofForms.Flat, Pitch: 1, Overhang: 0, RoofHole: true, RidgeCap: false,
        BorderWidth: 1, InlayInset: 2, Storeys: 1, StoreyClear: 0,
        Windows: NoWindows, Porch: null,
        Door: doors.FirstOrDefault()?.Slug ?? "", DoorHeight: 3,
        RoofStyleId: null, PorchStyleId: null, StoreyStack: [], Courses: []);

    private static readonly RoomWindowDto NoWindows =
        new(WindowForms.None, Block: 102, Data: 0, Sill: 2, Width: 2, Height: 2, Spacing: 3);

    /// <summary>The porch a style gets the moment one is switched on: two blocks of the front wall's strip,
    /// running its full width, under a lean-to with a fence along its open edges.</summary>
    private static readonly RoomPorchDto DefaultPorch =
        new(Depth: 2, Inset: 0, Edge: PorchEdges.Front, Roof: RoofForms.Shed, RailBlock: 85);

    private void StartNew()
    {
        editingId = null;
        draftName = "";
        draft = EmptyDraft(draftName);
    }

    private async Task Load(long id)
    {
        if (await Library.GetAsync<RoomStyleDetail>(LibraryKinds.Houses, id) is not { } detail)
        {
            note = "That house could not be read.";
            draft = null;
            return;
        }
        editingId = detail.Id;
        draftName = detail.Name;
        // Every field the row states, not the ones the editor happens to draw a control for: a house loaded
        // through a shorter list and saved back writes the rest away, so a beam, a door head or a slab roof
        // would be lost by opening the row and pressing Save.
        draft = detail.AsSaveRequest();
    }

    // ── the course stacks ──────────────────────────────────────────────────────────────────────────
    /// <summary>One part's courses in stack order — what the rail lays out, and the order the ordinals are
    /// renumbered into after every edit so they stay 0..n-1 with no gaps.</summary>
    private List<RoomCourseDto> Courses(string part)
        => [.. draft!.Courses.Where(course => course.Part == part).OrderBy(course => course.Ordinal)];

    // Only the parts that stack have an extent, so the roof is not among them: its depth at a cell is whatever
    // closes the step down to its neighbour (B72 retires the column the roof's own extent was stored in).
    private int Extent(string part) => part switch
    {
        RoomParts.Floor => draft!.FloorDepth,
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

    private Task BindCourse(string part, int ordinal, long styleId)
        => EditCourse(part, ordinal, course => course with { StyleId = styleId });

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

    private Task BindSingle(string part, long styleId)
        => WriteCourses(part, styleId <= 0 ? [] : [new RoomCourseDto(part, 0, styleId, 1)]);

    // ── the knobs ──────────────────────────────────────────────────────────────────────────────────
    private Task SetExtent(string part, ChangeEventArgs e) => Knob(d => part switch
    {
        RoomParts.Floor => d with { FloorDepth = Math.Max(1, Parse(e, d.FloorDepth)) },
        _ => d with { WallHeight = Math.Max(1, Parse(e, d.WallHeight)) },
    });

    /// <summary>Which of the six roofs. The pitch and the ridge cap only mean anything on a sloped one and the
    /// hole only on the lid, so each is offered under the form rather than as a knob of its own.</summary>
    private Task SetForm(string form) => Knob(house => house with { RoofForm = RoofForms.Canonical(form) });

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

    // ── the parts this house binds ─────────────────────────────────────────────────────────────────
    /// <summary>Bind a roof, or unbind it. Unbound is not "no roof" — it is this house describing its own,
    /// which is what every room style did before there were parts.</summary>
    private Task BindRoofStyle(string value) => Knob(house => house with { RoofStyleId = Picked(value) });

    private Task BindPorchStyle(string value) => Knob(house => house with { PorchStyleId = Picked(value) });

    /// <summary>A part id, or null for the unbound row — which is a house describing that part itself rather
    /// than not having one.</summary>
    private static long? Picked(string value) => long.TryParse(value, out var id) && id > 0 ? id : null;

    /// <summary>The rows a part list offers, newest first as the library answers them.</summary>
    private static IReadOnlyList<SelectOption> Parts(IEnumerable<(long Id, string Name)> rows)
        => [.. rows.Select(row => new SelectOption(row.Id.ToString(), row.Name))];

    /// <summary>The doors a room may be stamped with, as the library serves them.</summary>
    private IReadOnlyList<SelectOption> Doors
        => [.. doors.Select(door => new SelectOption(door.Slug, door.Label))];

    /// <summary>What an unbound course says: a part with none keeps the finish the stamper builds in.</summary>
    private const string Unbound = "Unbound — keeps the built-in finish";

    /// <summary>Add a storey on top. The stack reads ground-first, so a new one lands at the end — a building
    /// grows upward, and an author adding a floor is adding the one above the last.</summary>
    private Task AddStorey()
        => storeys.Count == 0
            ? Task.CompletedTask
            : WriteStack([.. draft!.StoreyStack, new RoomStoreyDto(storeys[0].Id, 0)]);

    private Task RemoveStorey(int index)
        => WriteStack([.. draft!.StoreyStack.Where((_, at) => at != index)]);

    private Task BindStorey(int index, string value)
        => EditStorey(index, storey => storey with
        {
            StoreyStyleId = long.TryParse(value, out var id) ? id : storey.StoreyStyleId,
        });

    /// <summary>The clear this storey takes <em>here</em>. Zero keeps the storey style's own, which is what
    /// lets one preset be a tall ground floor in one house and an ordinary room in another.</summary>
    private Task SetStoreyStackClear(int index, ChangeEventArgs e)
        => EditStorey(index, storey => storey with { Clear = Math.Clamp(Parse(e, storey.Clear), 0, 16) });

    private Task EditStorey(int index, Func<RoomStoreyDto, RoomStoreyDto> edit)
        => WriteStack([.. draft!.StoreyStack.Select((storey, at) => at == index ? edit(storey) : storey)]);

    /// <summary>Move a storey one place through the building. The list <em>is</em> the order, so this is the
    /// whole of reordering — there is no ordinal to renumber.</summary>
    private Task MoveStorey(int index, int by)
    {
        var stack = draft!.StoreyStack.ToList();
        var to = index + by;
        if (to < 0 || to >= stack.Count) return Task.CompletedTask;
        (stack[index], stack[to]) = (stack[to], stack[index]);
        return WriteStack(stack);
    }

    private Task WriteStack(IReadOnlyList<RoomStoreyDto> stack) => Knob(d => d with { StoreyStack = stack });

    /// <summary>Courses of wall the bound stack spends: each storey's clear, plus one for the slab under every
    /// storey but the ground. Computed here rather than asked for, since the caption is about the stack the
    /// author is holding and the summary already carries every clear it needs.</summary>
    private int StackCourses => draft!.StoreyStack
        .Select(storey => Math.Max(3, storey.Clear > 0 ? storey.Clear : ClearOf(storey.StoreyStyleId)))
        .Sum() + Math.Max(0, draft.StoreyStack.Count - 1);

    /// <summary>A bound storey style's own clear, or the three a room is at least where the binding names one
    /// this library no longer holds.</summary>
    private int ClearOf(long storeyStyleId)
        => storeys.FirstOrDefault(storey => storey.Id == storeyStyleId)?.Clear ?? 3;

    // ── the storeys the house counts for itself ────────────────────────────────────────────────────
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
    private Task SetWindowForm(string picked) => Window(window =>
    {
        var form = WindowForms.Canonical(picked);
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

    private Task SetWindows(RoomWindowDto window) => Knob(d => d with { Windows = window });

    // ── the gable ──────────────────────────────────────────────────────────────────────────────────
    /// <summary>Whether the gable carries an opening of its own. Absent is a blank gable, not an opening of
    /// form "none" — the form it would come back at is the one the author last chose.</summary>
    private bool Gabled => draft?.GableWindows is not null;

    private Task ToggleGableWindows() => Knob(d => d with
    {
        // A gable starts from the wall's own window, because a building whose gable is glazed differently
        // from its wall is a decision rather than a default.
        GableWindows = d.GableWindows is null ? (d.Windows ?? NoWindows) with { Sill = 1 } : null,
    });

    private Task SetGableWindows(RoomWindowDto window) => Knob(d => d with { GableWindows = window });

    // ── the timber frame ───────────────────────────────────────────────────────────────────────────
    /// <summary>Whether the wall is framed. A beam is a log and only a log (<c>HS1</c>), so the frame an
    /// author switches on starts as one rather than as a block the gate would refuse.</summary>
    private bool Beamed => draft?.Beams is not null;

    private Task ToggleBeams() => Knob(d => d with
    {
        Beams = d.Beams is null ? new RoomBeamDto(Block: 17, Data: 0, Reach: 3) : null,
    });

    private Task PickBeamBlock(PaintBlockDto block)
        => Beams(beams => beams with { Block = block.Id, Data = block.Data });

    private Task SetBeamReach(ChangeEventArgs e)
        => Beams(beams => beams with { Reach = Math.Clamp(Parse(e, beams.Reach), 0, 16) });

    private Task Beams(Func<RoomBeamDto, RoomBeamDto> edit)
        => Knob(d => d.Beams is null ? d : d with { Beams = edit(d.Beams) });

    // ── the slab a roof steps in ───────────────────────────────────────────────────────────────────
    /// <summary>Whether the roof climbs half a block at a time. -1 is a roof laid in whole blocks, which is
    /// what every style is until a slab is named.</summary>
    private bool Slabbed => (draft?.RoofSlab ?? -1) >= 0;

    private Task ToggleRoofSlab() => Knob(d => d with
    {
        RoofSlab = d.RoofSlab >= 0 ? -1 : 126,     // wooden slab
        RoofSlabData = 0,
    });

    private Task PickRoofSlab(PaintBlockDto block)
        => Knob(d => d with { RoofSlab = block.Id, RoofSlabData = block.Data });

    // ── the doorway ────────────────────────────────────────────────────────────────────────────────
    private Task SetDoorWidth(ChangeEventArgs e)
        => Knob(d => d with { DoorWidth = Math.Clamp(Parse(e, d.DoorWidth), 2, 8) });

    /// <summary>Whether a lintel is stated. Without one the wall simply carries over the opening.</summary>
    private bool Headed => draft?.DoorHead is not null;

    /// <summary>Whether the head's middle is spanned at all — a square head is its two corners and nothing
    /// between, so it has no fill to choose.</summary>
    private bool DoorHeadFilled => DoorHeadForms.Canonical(draft?.DoorHead?.Form) == DoorHeadForms.Arched;

    private static readonly IReadOnlyList<SelectOption> DoorHeadFormOptions =
        [.. DoorHeadForms.All.Select(form => new SelectOption(form.Id, form.Name))];

    private static readonly IReadOnlyList<SelectOption> DoorHeadFillOptions =
        [.. DoorHeadFills.All.Select(fill => new SelectOption(fill.Id, fill.Name))];

    private Task ToggleDoorHead() => Knob(d => d with
    {
        // Oak stairs over an upside-down oak slab: one material, which is what HS4 asks of the pair.
        DoorHead = d.DoorHead is null
            ? new RoomDoorHeadDto(DoorHeadForms.Arched, Block: 53, DoorHeadFills.UpperSlab, FillBlock: 126, FillData: 0)
            : null,
    });

    private Task SetDoorHeadForm(string picked)
        => Head(head => head with { Form = DoorHeadForms.Canonical(picked) });

    private Task SetDoorHeadFill(string picked)
        => Head(head => head with { Fill = DoorHeadFills.Canonical(picked) });

    private Task PickDoorHeadBlock(PaintBlockDto block) => Head(head => head with { Block = block.Id });

    private Task PickDoorHeadFill(PaintBlockDto block)
        => Head(head => head with { FillBlock = block.Id, FillData = block.Data });

    private Task Head(Func<RoomDoorHeadDto, RoomDoorHeadDto> edit)
        => Knob(d => d.DoorHead is null ? d : d with { DoorHead = edit(d.DoorHead) });

    // ── the porch ──────────────────────────────────────────────────────────────────────────────────
    private RoomPorchDto? Porch => draft?.Porch;

    /// <summary>A porch is present or it is not — an absent one is not a porch of depth nothing, because the
    /// depth it would come back at is the one the author last set.</summary>
    private Task TogglePorch() => Knob(d => d with { Porch = d.Porch is null ? DefaultPorch : null });

    private Task SetPorchDepth(ChangeEventArgs e) =>
        Deck(porch => porch with { Depth = Math.Clamp(Parse(e, porch.Depth), 1, 8) });

    private Task SetPorchInset(ChangeEventArgs e) =>
        Deck(porch => porch with { Inset = Math.Clamp(Parse(e, porch.Inset), 0, 8) });

    private Task SetPorchEdge(string edge) => Deck(porch => porch with { Edge = PorchEdges.Canonical(edge) });

    private Task SetPorchRoof(string form) => Deck(porch => porch with { Roof = RoofForms.Canonical(form) });

    private Task PickRailBlock(PaintBlockDto block) => Deck(porch => porch with { RailBlock = block.Id });

    private Task ToggleRail() =>
        Deck(porch => porch with { RailBlock = porch.RailBlock > 0 ? 0 : DefaultPorch.RailBlock });

    private Task Deck(Func<RoomPorchDto, RoomPorchDto> edit)
        => draft?.Porch is { } porch ? Knob(d => d with { Porch = edit(porch) }) : Task.CompletedTask;

    private Task SetDoor(string door) => Knob(house => house with { Door = door });

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
        preview = draft is null
            ? null
            : await Library.DraftPreviewAsync<RoomStylePreviewDto>(
                LibraryKinds.Houses, Saveable(draft), footprint, CutPart);
        StateHasChanged();
    }

    /// <summary>Which part the pictures are cut to: the outline row that is open, where that row is a part of
    /// the building. The rows that are not — what it is composed from, its trim, its porch, its doorway — are
    /// read against the whole shell, because none of them is a band of it.</summary>
    private string? CutPart => RoomPartInfo.All.Any(part => part.Id == selected) ? selected : null;

    private RoomStyleSaveRequest Saveable(RoomStyleSaveRequest current) => current with
    {
        Name = string.IsNullOrWhiteSpace(draftName) ? current.Name : draftName.Trim(),
    };

    private async Task Save()
    {
        if (draft is null || string.IsNullOrWhiteSpace(draftName)) return;
        var request = Saveable(draft);
        var saved = editingId is { } id
            ? await Library.UpdateAsync<RoomStyleDetail>(LibraryKinds.Houses, id, request)
            : await Library.CreateAsync<RoomStyleDetail>(LibraryKinds.Houses, request);
        if (saved is null) { note = "The library refused that house."; return; }
        note = editingId is null ? "Added to the library." : "Saved.";
        await OnSaved.InvokeAsync("saved");
        if (editingId is null) Nav.NavigateTo($"/library/{LibraryKinds.HousesSlug}/{saved.Id}");
        else editingId = saved.Id;
    }

    private async Task SaveAsCopy()
    {
        if (draft is null) return;
        var copy = await Library.CreateAsync<RoomStyleDetail>(LibraryKinds.Houses,
            Saveable(draft) with { Name = $"{draftName.Trim()} copy" });
        if (copy is null) { note = "The library refused that house."; return; }
        Nav.NavigateTo($"/library/{LibraryKinds.HousesSlug}/{copy.Id}");
    }

    private async Task Delete()
    {
        if (editingId is not { } id) return;
        await Library.DeleteAsync(LibraryKinds.Houses, id);
        Nav.NavigateTo($"/library/{LibraryKinds.HousesSlug}");
    }
}
