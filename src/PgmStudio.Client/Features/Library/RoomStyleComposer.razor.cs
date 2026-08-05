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
    private bool loading = true;
    private string newName = "";
    private string? note;

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
    private RoomStyleSaveRequest EmptyDraft(string name) => new(
        name, FloorDepth: 1, WallHeight: 7, RoofThickness: 1,
        Eave: RoomEaves.Flush, RoofHole: true,
        Door: doors.FirstOrDefault()?.Slug ?? "", DoorHeight: 3, Courses: []);

    private async Task StartNew()
    {
        if (string.IsNullOrWhiteSpace(newName)) return;
        editingId = null;
        draftName = newName.Trim();
        newName = "";
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
            detail.Eave, detail.RoofHole, detail.Door, detail.DoorHeight, detail.Courses);
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

    // ── the knobs ──────────────────────────────────────────────────────────────────────────────────
    private Task SetExtent(string part, ChangeEventArgs e) => Knob(d => part switch
    {
        RoomParts.Floor => d with { FloorDepth = Math.Max(1, Parse(e, d.FloorDepth)) },
        RoomParts.Roof => d with { RoofThickness = Math.Max(1, Parse(e, d.RoofThickness)) },
        _ => d with { WallHeight = Math.Max(1, Parse(e, d.WallHeight)) },
    });

    private Task ToggleEave() => Knob(d => d with
    {
        Eave = d.Eave == RoomEaves.Overlap ? RoomEaves.Flush : RoomEaves.Overlap,
    });

    private Task ToggleHole() => Knob(d => d with { RoofHole = !d.RoofHole });

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
