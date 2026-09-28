using System.Globalization;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using PgmStudio.Client.Components;
using PgmStudio.Contracts;
using PgmStudio.Vocabulary;

namespace PgmStudio.Client.Features.Sketch;

/// <summary>The In game phase: one picture of the board enlarged, the gallery under it, and the notes column
/// beside it for a caller who may read and answer notes.</summary>
public partial class SketchInGamePhase
{
    /// <summary>The size the enlarged picture is drawn at, which every mark on it is counted in.</summary>
    private const int PictureWidth = 1280, PictureHeight = 720;

    /// <summary>The most points a lasso's outline sends, which keeps the pick's query inside a request line.</summary>
    private const int MostOutline = 300;

    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private HttpClient Http { get; set; } = default!;
    [Inject] private StudioAccess Access { get; set; } = default!;

    [Parameter, EditorRequired] public string Slug { get; set; } = "";

    /// <summary>The views to show, or null while they are being read.</summary>
    [Parameter] public MapViewsDto? Views { get; set; }

    /// <summary>Why the views could not be read, or null.</summary>
    [Parameter] public string? Error { get; set; }

    /// <summary>Bumped by the host whenever the board may have changed since the pictures were asked for.</summary>
    [Parameter] public int Round { get; set; }

    [Parameter] public EventCallback OnBack { get; set; }

    /// <summary>Place a view of the author's own, on the canvas.</summary>
    [Parameter] public EventCallback OnPlace { get; set; }

    /// <summary>Stop keeping a view.</summary>
    [Parameter] public EventCallback<MapViewDto> OnLetGo { get; set; }

    /// <summary>A note to open on arriving, by id — a link into its thread.</summary>
    [Parameter] public long? LinkedNote { get; set; }

    /// <summary>A mark on the picture: the tool that drew it and its pixels.</summary>
    private sealed record Mark(string Kind, List<PixelDto> Pixels);

    private string? shownId;
    private bool lettingGo;
    private ElementReference main;
    private ElementReference trap;

    private bool mayNote;
    private bool openedFromLink;
    private List<MapNoteDto> notes = [];
    private int notesRound = -1;
    private NotesStep step = NotesStep.Overview;
    private long? currentId;
    private string filter = SketchNotesColumn.All;
    private string? notesError;
    private bool busy;
    private bool showMarks = true;

    private string? tool;
    private Mark? drawing;
    private Mark? mark;
    private bool wholeMap;
    private EyePickDto? picked;
    private string? pickNote;
    private int pickRound;
    private double scale = 1;

    private MapViewDto? Shown => Views?.Views.FirstOrDefault(view => view.Id == shownId) ?? Views?.Views.FirstOrDefault();
    private IReadOnlyCollection<string> ViewIds => Views?.Views.Select(view => view.Id).ToHashSet() ?? [];
    private MapNoteDto? Current => notes.FirstOrDefault(note => note.Id == currentId);
    private MapNoteDto? OpenHere => step == NotesStep.Thread && Current is { } note && note.Anchor.ViewId == Shown?.Id ? note : null;

    private IEnumerable<MapNoteDto> PinnedHere => notes.Where(note =>
        note.Anchor.ViewId == Shown?.Id && note.Anchor.Marks is { Count: > 0 }
        && SketchNotesColumn.Passes(note, filter));

    protected override async Task OnInitializedAsync() => mayNote = (await Access.MeAsync()).Notes;

    protected override async Task OnParametersSetAsync()
    {
        if (!mayNote || notesRound == Round) return;
        notesRound = Round;
        await LoadNotesAsync();
        if (!openedFromLink && LinkedNote is { } linked && notes.FirstOrDefault(note => note.Id == linked) is { } found)
        {
            openedFromLink = true;
            currentId = found.Id;
            step = NotesStep.Thread;
            if (found.Anchor.ViewId is { } view) shownId = view;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender) => await JS.InvokeVoidAsync("studio.icons");

    private async Task LoadNotesAsync()
    {
        try
        {
            notes = await Http.GetFromJsonAsync<List<MapNoteDto>>($"api/map/{Slug}/notes") ?? [];
            notesError = null;
        }
        catch { notesError = "The notes could not be read — the studio could not be reached."; }
    }

    // ── the gallery ──

    private void Show(MapViewDto view)
    {
        if (view.Id == Shown?.Id) return;
        shownId = view.Id;
        tool = null;
        drawing = null;
        if (step == NotesStep.New) ClearMark();
        else if (step == NotesStep.Thread) step = NotesStep.Overview;
    }

    private async Task OnKey(KeyboardEventArgs key)
    {
        switch (key.Key)
        {
            case "Escape" when tool is not null: tool = null; drawing = null; break;
            case "Escape": await BackToOverview(); break;
            case "ArrowLeft": Step(-1); break;
            case "ArrowRight": Step(1); break;
        }
    }

    /// <summary>Show the view <paramref name="by"/> places along from the one shown, wrapping at either end.</summary>
    private void Step(int by)
    {
        if (Views is not { Views.Count: > 0 } all || Shown is not { } shown) return;
        var at = Math.Max(0, all.Views.ToList().FindIndex(view => view.Id == shown.Id));
        Show(all.Views[((at + by) % all.Views.Count + all.Views.Count) % all.Views.Count]);
    }

    private async Task LetGo(MapViewDto view)
    {
        lettingGo = true;
        try { await OnLetGo.InvokeAsync(view); }
        finally { lettingGo = false; }
        shownId = null;
    }

    /// <summary>How many notes on a view are still in play — not resolved.</summary>
    private int Waiting(string viewId) =>
        notes.Count(note => note.Anchor.ViewId == viewId && note.Status != NoteStatuses.Resolved);

    private string FullSize(MapViewDto view) =>
        $"api/map/{Slug}/render/eye?{view.Query}&width=1920&height=1080&round={Round}";

    private string PictureSource(MapViewDto view) =>
        $"api/map/{Slug}/render/eye?{view.Query}&width={PictureWidth}&height={PictureHeight}&round={Round}";

    /// <summary>Where the eye stands and what it looks at, in the block coordinates the canvas reads.</summary>
    private static string Where(MapViewDto view)
    {
        var looking = string.Create(CultureInfo.InvariantCulture, $"looking at {view.LookX}, {view.LookZ}");
        if (view.FromX is not { } x || view.FromZ is not { } z) return $"{looking} — the eye finds its own place";
        var height = view.Y is { } y ? string.Create(CultureInfo.InvariantCulture, $", eye at y {y:0.#}") : "";
        var tipped = view.Pitch is { } pitch ? string.Create(CultureInfo.InvariantCulture, $", {pitch:0}° down") : "";
        return string.Create(CultureInfo.InvariantCulture, $"standing at {x}, {z}{height}{tipped}, {looking}");
    }

    // ── the notes column ──

    private async Task OpenNote(MapNoteDto note)
    {
        if (note.Anchor.ViewId is { } view && Views?.Views.Any(shown => shown.Id == view) == true) shownId = view;
        currentId = note.Id;
        step = NotesStep.Thread;
        notesError = null;
        tool = null;
        drawing = null;
        await main.FocusAsync();
    }

    private Task BackToOverview()
    {
        step = NotesStep.Overview;
        tool = null;
        drawing = null;
        mark = null;
        picked = null;
        notesError = null;
        return Task.CompletedTask;
    }

    private void StartNote()
    {
        step = NotesStep.New;
        wholeMap = false;
        notesError = null;
    }

    private void ClearMark()
    {
        mark = null;
        picked = null;
        pickNote = null;
        pickRound++;
    }

    private void PinToMap()
    {
        ClearMark();
        tool = null;
        wholeMap = true;
    }

    private bool Ready => wholeMap || mark is null || picked is not null;

    private string AnchorTitle => wholeMap ? "The whole map"
        : mark is { } drawn ? $"{char.ToUpperInvariant(drawn.Kind[0])}{drawn.Kind[1..]} on {Shown?.Name}"
        : $"This view: {Shown?.Name}";

    private string AnchorBody => wholeMap
        ? "Nothing spatial — the note is about the map as a whole."
        : mark is null
            ? "The note keeps the camera and a copy of this picture. Arm Point, Box or Lasso in the dock to pin it to ground on it."
            : pickNote ?? (picked is null ? "Reading the ground under the mark…" : Ground(picked));

    private static string Ground(EyePickDto pick)
    {
        if (pick.Hit is { } hit)
            return string.Create(CultureInfo.InvariantCulture, $"The block at {hit.X}, {hit.Y}, {hit.Z}")
                + (pick.Ground is { } ground && ground.Y != hit.Y
                    ? string.Create(CultureInfo.InvariantCulture, $", over ground at y {ground.Y}.") : ".")
                + " The note keeps it, the camera and a copy of this picture.";
        if (pick.Columns.Count > 0)
            return string.Create(CultureInfo.InvariantCulture, $"{pick.Columns.Count} ground columns")
                + (pick.Sky > 0 ? string.Create(CultureInfo.InvariantCulture, $" and {pick.Sky} pixels of sky") : "")
                + ". Ground hidden from this camera is not in it. The note keeps it, the camera and a copy of this picture.";
        return "The mark is all sky, so it names no ground — only this picture.";
    }

    private async Task<bool> SendNoteAsync(NoteWriting writing)
    {
        if (Shown is not { } view) return false;
        busy = true;
        notesError = null;
        try
        {
            NoteAnchorDto anchor;
            string? picture = null;
            if (wholeMap) anchor = new NoteAnchorDto(NoteAnchors.Map);
            else
            {
                var camera = mark is null ? await PickAsync(view, null) : picked;
                if (camera is null)
                {
                    notesError = pickNote ?? "The camera this picture was drawn with could not be read, so the note was not sent.";
                    return false;
                }
                picture = await JS.InvokeAsync<string?>("studio.keepPicture", PictureSource(view));
                anchor = new NoteAnchorDto(mark?.Kind ?? NoteAnchors.View, view.Id, view.Name, camera.Camera,
                    PictureWidth, PictureHeight, mark?.Pixels, camera.Hit, camera.Ground,
                    camera.Columns.Count > 0 ? camera.Columns : null);
            }
            var answer = await Http.PostAsJsonAsync($"api/map/{Slug}/notes", new MapNoteRequest(writing.Body, anchor, writing.Tag, picture));
            if (!answer.IsSuccessStatusCode)
            {
                notesError = await ServerRefusal.SentenceAsync(answer);
                return false;
            }
            var written = await answer.Content.ReadFromJsonAsync<MapNoteDto>();
            await LoadNotesAsync();
            mark = null;
            picked = null;
            wholeMap = false;
            currentId = written?.Id;
            step = written is null ? NotesStep.Overview : NotesStep.Thread;
            if (picture is null && anchor.Kind != NoteAnchors.Map)
                notesError = "The note was sent without its picture — the browser could not keep a copy of it.";
            return true;
        }
        catch
        {
            notesError = "The note was not sent — the studio could not be reached.";
            return false;
        }
        finally
        {
            busy = false;
            StateHasChanged();
        }
    }

    private async Task<bool> ReplyAsync(string body)
    {
        if (Current is not { } note) return false;
        busy = true;
        notesError = null;
        try
        {
            var answer = await Http.PostAsJsonAsync($"api/map/{Slug}/notes/{note.Id}/replies", new NoteReplyRequest(body));
            if (!answer.IsSuccessStatusCode)
            {
                notesError = await ServerRefusal.SentenceAsync(answer);
                return false;
            }
            await LoadNotesAsync();
            return true;
        }
        catch
        {
            notesError = "The reply was not sent — the studio could not be reached.";
            return false;
        }
        finally
        {
            busy = false;
            StateHasChanged();
        }
    }

    private async Task ChangeAsync(string status)
    {
        if (Current is not { } note) return;
        busy = true;
        notesError = null;
        try
        {
            var answer = await Http.PatchAsJsonAsync($"api/map/{Slug}/notes/{note.Id}", new NoteChangeRequest(status));
            if (!answer.IsSuccessStatusCode) notesError = await ServerRefusal.SentenceAsync(answer);
            else await LoadNotesAsync();
        }
        catch { notesError = "The thread was not changed — the studio could not be reached."; }
        finally { busy = false; }
    }

    // ── drawing a mark on the picture ──

    private string ToolHint => tool switch
    {
        NoteAnchors.Point => "Click the block the note is about.",
        NoteAnchors.Box => "Drag a rectangle over the ground the note is about.",
        _ => "Draw round the ground the note is about.",
    };

    /// <summary>Arming a tool opens a new note pinned to what it draws; arming it again puts it down.</summary>
    private void Arm(string kind)
    {
        if (tool == kind)
        {
            tool = null;
            return;
        }
        tool = kind;
        drawing = null;
        wholeMap = false;
        if (step != NotesStep.New)
        {
            step = NotesStep.New;
            notesError = null;
        }
    }

    private async Task PointerDown(PointerEventArgs pointer)
    {
        if (tool is null) return;
        var size = await JS.InvokeAsync<ElementSize>("studio.elementSize", trap);
        scale = size.Width > 0 ? PictureWidth / size.Width : 1;
        var at = Pixel(pointer);
        drawing = new Mark(tool, tool == NoteAnchors.Box ? [at, at] : [at]);
    }

    private void PointerMove(PointerEventArgs pointer)
    {
        if (drawing is not { } live || pointer.Buttons == 0) return;
        var at = Pixel(pointer);
        if (live.Kind == NoteAnchors.Box) live.Pixels[1] = at;
        else if (live.Kind == NoteAnchors.Lasso
                 && (Math.Abs(live.Pixels[^1].X - at.X) + Math.Abs(live.Pixels[^1].Y - at.Y)) >= 4)
            live.Pixels.Add(at);
    }

    private async Task PointerUp(PointerEventArgs pointer)
    {
        if (drawing is not { } finished) return;
        drawing = null;
        var pixels = finished.Kind switch
        {
            NoteAnchors.Point => finished.Pixels,
            NoteAnchors.Box when Math.Abs(finished.Pixels[0].X - finished.Pixels[1].X) < 2
                                 || Math.Abs(finished.Pixels[0].Y - finished.Pixels[1].Y) < 2 => null,
            NoteAnchors.Box => finished.Pixels,
            _ when finished.Pixels.Count < 3 => null,
            _ => Thin(finished.Pixels),
        };
        if (pixels is null) return;
        tool = null;
        mark = finished with { Pixels = pixels };
        picked = null;
        pickNote = null;
        var round = ++pickRound;
        if (Shown is not { } view) return;
        var answer = await PickAsync(view, mark);
        if (round != pickRound) return;
        picked = answer;
    }

    private PixelDto Pixel(PointerEventArgs pointer) => new(
        Math.Clamp((int)Math.Floor(pointer.OffsetX * scale), 0, PictureWidth - 1),
        Math.Clamp((int)Math.Floor(pointer.OffsetY * scale), 0, PictureHeight - 1));

    /// <summary>An outline cut down to at most <see cref="MostOutline"/> points, evenly along it.</summary>
    private static List<PixelDto> Thin(List<PixelDto> outline) =>
        outline.Count <= MostOutline ? outline
            : [.. Enumerable.Range(0, MostOutline).Select(i => outline[i * outline.Count / MostOutline])];

    /// <summary>What <paramref name="drawn"/> is on the ground under <paramref name="view"/>'s picture, or the
    /// camera alone where nothing is drawn. Null where the studio could not say, with the reason kept.</summary>
    private async Task<EyePickDto?> PickAsync(MapViewDto view, Mark? drawn)
    {
        var shape = drawn switch
        {
            null => "",
            { Kind: NoteAnchors.Point } => $"&at={drawn.Pixels[0].X},{drawn.Pixels[0].Y}",
            { Kind: NoteAnchors.Box } => $"&box={drawn.Pixels[0].X},{drawn.Pixels[0].Y},{drawn.Pixels[1].X},{drawn.Pixels[1].Y}",
            _ => "&lasso=" + string.Join(';', drawn.Pixels.Select(pixel => $"{pixel.X},{pixel.Y}")),
        };
        try
        {
            var answer = await Http.GetAsync($"api/map/{Slug}/render/eye/pick?{view.Query}&width={PictureWidth}&height={PictureHeight}{shape}");
            if (answer.IsSuccessStatusCode) return await answer.Content.ReadFromJsonAsync<EyePickDto>();
            pickNote = await ServerRefusal.SentenceAsync(answer);
        }
        catch { pickNote = "The ground under the mark could not be read — the studio could not be reached."; }
        return null;
    }

    private sealed record ElementSize(double Width, double Height);

    /// <summary>Where a note's pin stands on the picture: its point, or the middle of its area.</summary>
    private static (double X, double Y) PinAt(MapNoteDto note)
    {
        var marks = note.Anchor.Marks!;
        return (marks.Average(pixel => pixel.X), marks.Average(pixel => pixel.Y));
    }

    private static string PinStyle(double x, double y) => string.Create(CultureInfo.InvariantCulture,
        $"left: {x / PictureWidth * 100:0.##}%; top: {y / PictureHeight * 100:0.##}%");
}
