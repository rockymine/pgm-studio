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

    [Parameter, EditorRequired] public string Slug { get; set; } = "";

    /// <summary>The views: the gallery to show, why it could not be read or why the last thing asked of a view
    /// was refused, and the round its pictures are drawn in.</summary>
    [Parameter, EditorRequired] public SketchViews Views { get; set; } = default!;

    /// <summary>The board's notes, read by the host for every phase that shows them.</summary>
    [Parameter, EditorRequired] public SketchNotes Notes { get; set; } = default!;

    [Parameter] public EventCallback OnBack { get; set; }

    /// <summary>Place a view of the author's own, on the canvas.</summary>
    [Parameter] public EventCallback OnPlace { get; set; }

    /// <summary>A note to open on arriving, by id — a link into its thread.</summary>
    [Parameter] public long? LinkedNote { get; set; }

    /// <summary>The linked note's thread has been opened, so the link is spent.</summary>
    [Parameter] public EventCallback OnLinkOpened { get; set; }

    /// <summary>The numbers of the board's changes, handed on to the notes column.</summary>
    [Parameter] public IReadOnlyList<long> Changes { get; set; } = [];

    /// <summary>Open what changed after a thread's last message.</summary>
    [Parameter] public EventCallback<long> OnChangesSince { get; set; }

    /// <summary>Draw the pictures again over the board as it is stored now.</summary>
    [Parameter] public EventCallback OnRedraw { get; set; }

    /// <summary>A mark on the picture: the tool that drew it and its pixels.</summary>
    private sealed record Mark(string Kind, List<PixelDto> Pixels);

    private string? shownId;
    private bool lettingGo, picturing;
    private ElementReference main;
    private ElementReference trap;

    private bool handing;
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
    private bool stale;

    private MapViewsDto? Gallery => Views.Gallery;
    private int Round => Views.Round;

    /// <summary>The change the pictures are of: a note written on one was written at it.</summary>
    private long DrawnAt => Gallery?.Change ?? 0;

    /// <summary>A thread's pictures, compared on the big picture: the note's own, the latest after a reply carries,
    /// and the note's camera over the board now. <paramref name="Key"/> names the thread and its latest after, so
    /// a new after starts the comparison afresh.</summary>
    private sealed record Comparison(string Key, string Before, long BeforeChange, string? After, long AfterChange,
                                     string Now, long NowChange, int Width, int Height);

    /// <summary>The open thread's comparison, where its note was written on a picture.</summary>
    private Comparison? Compared
    {
        get
        {
            if (step != NotesStep.Thread || Current is not { Anchor.Camera: { } camera } note
                || note.Messages.FirstOrDefault() is not { Picture: { } before } first) return null;
            var after = note.Messages.Skip(1).LastOrDefault(message => message.Picture is not null);
            var (width, height) = (note.Anchor.Width ?? PictureWidth, note.Anchor.Height ?? PictureHeight);
            var latest = Changes.Count > 0 ? Changes[^1] : DrawnAt;
            var now = string.Create(CultureInfo.InvariantCulture,
                $"api/map/{Slug}/render/eye?eye={camera.X},{camera.Y},{camera.Z}&yaw={camera.Yaw}&pitch={camera.Pitch}&fov={camera.Fov}&width={width}&height={height}&round={Round}-{latest}");
            return new Comparison($"{note.Id}:{after?.Picture}", $"api/notes/pictures/{before}", first.Change,
                after?.Picture is { } shot ? $"api/notes/pictures/{shot}" : null, after?.Change ?? 0,
                now, latest, width, height);
        }
    }

    /// <summary>The comparison the author picked, for the thread and after it was picked on.</summary>
    private string? comparing, compareMode;

    /// <summary>What a comparison shows: what the author picked for it, else the wipe where there is an after and
    /// the board now where there is not.</summary>
    private string CompareMode(Comparison compared) =>
        comparing == compared.Key && compareMode is { } picked ? picked
        : compared.After is not null ? SketchNoteCompare.Modes.Wipe : SketchNoteCompare.Modes.Now;

    /// <summary>The board's latest change where it is newer than the pictures, or null.</summary>
    private long? Landed => Changes.Count > 0 && Changes[^1] > DrawnAt ? Changes[^1] : null;

    private MapViewDto? Shown => Gallery?.Views.FirstOrDefault(view => view.Id == shownId) ?? Gallery?.Views.FirstOrDefault();
    private IReadOnlyCollection<string> ViewIds => Gallery?.Views.Select(view => view.Id).ToHashSet() ?? [];
    private MapNoteDto? Current => Notes.All.FirstOrDefault(note => note.Id == currentId);
    private MapNoteDto? OpenHere => step == NotesStep.Thread && Current is { } note && note.Anchor.ViewId == Shown?.Id ? note : null;

    /// <summary>The marks the open thread's replies carry on the picture in view.</summary>
    private IEnumerable<NoteAnchorDto> RepliesHere
    {
        get
        {
            if (step != NotesStep.Thread || Current is not { } note) return [];
            var viewId = Compared is not null ? note.Anchor.ViewId : Shown?.Id;
            return note.Messages.Select(message => message.Mark)
                .OfType<NoteAnchorDto>()
                .Where(pinned => pinned.ViewId == viewId && pinned.Marks is { Count: > 0 });
        }
    }

    private IEnumerable<MapNoteDto> PinnedHere => Notes.All.Where(note =>
        note.Anchor.ViewId == Shown?.Id && note.Anchor.Marks is { Count: > 0 }
        && SketchNotesColumn.Passes(note, filter));

    /// <summary>Opens the linked note's thread as soon as the notes hold it.</summary>
    protected override async Task OnParametersSetAsync()
    {
        if (LinkedNote is { } linked && Notes.All.FirstOrDefault(note => note.Id == linked) is { } found)
        {
            currentId = found.Id;
            step = NotesStep.Thread;
            if (found.Anchor.ViewId is { } view) shownId = view;
            await OnLinkOpened.InvokeAsync();
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender) => await JS.InvokeVoidAsync("studio.icons");

    /// <summary>Read the notes again, after one was written or changed.</summary>
    private async Task LoadNotesAsync()
    {
        await Notes.LoadAsync();
        notesError = Notes.LoadError;
    }

    /// <summary>Hand the open notes to the agent; <paramref name="again"/> repeats a hand-off nothing was written since.</summary>
    private async Task HandOffAsync(bool again)
    {
        handing = true;
        notesError = null;
        try
        {
            using var answer = await Http.PostAsJsonAsync("api/notes/handoff", new NoteHandoffRequest(again));
            if (answer.IsSuccessStatusCode) Notes.Handoff = await answer.Content.ReadFromJsonAsync<NoteHandoffDto>();
            else notesError = $"The notes were not handed over: {await ServerRefusal.SentenceAsync(answer)}";
        }
        catch { notesError = "Couldn't hand the notes over. Check your connection and try again."; }
        finally { handing = false; }
    }

    // ── the gallery ──

    private void Show(MapViewDto view)
    {
        if (view.Id == Shown?.Id) return;
        shownId = view.Id;
        tool = null;
        drawing = null;
        if (step == NotesStep.Thread) step = NotesStep.Overview;
        ClearMark();
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
        if (Gallery is not { Views.Count: > 0 } all || Shown is not { } shown) return;
        var at = Math.Max(0, all.Views.ToList().FindIndex(view => view.Id == shown.Id));
        Show(all.Views[((at + by) % all.Views.Count + all.Views.Count) % all.Views.Count]);
    }

    private async Task LetGo(MapViewDto view)
    {
        lettingGo = true;
        try { await Views.LetGoAsync(view); }
        finally { lettingGo = false; }
        shownId = null;
    }

    private async Task MakePicture(MapViewDto view)
    {
        picturing = true;
        try { await Views.PictureAsync(view); }
        finally { picturing = false; }
    }

    /// <summary>How many notes on a view are still in play — not resolved.</summary>
    private int Waiting(string viewId) =>
        Notes.All.Count(note => note.Anchor.ViewId == viewId && note.Status != NoteStatuses.Resolved);

    private string FullSize(MapViewDto view) =>
        $"api/map/{Slug}/render/eye?{view.Query}&width=1920&height=1080&round={Round}";

    private string PictureSource(MapViewDto view) =>
        $"api/map/{Slug}/render/eye?{view.Query}&width={PictureWidth}&height={PictureHeight}&round={Round}";

    /// <summary>Where the eye stands and what it looks at, in the block coordinates the canvas reads.</summary>
    private static string Where(MapViewDto view)
    {
        var looking = string.Create(CultureInfo.InvariantCulture, $"looking at {view.LookX}, {view.LookZ}");
        if (view.FromX is not { } x || view.FromZ is not { } z) return $"Camera placed automatically · {looking}";
        var height = view.Y is { } y ? string.Create(CultureInfo.InvariantCulture, $", y {y:0.#}") : "";
        var tipped = view.Pitch is { } pitch ? string.Create(CultureInfo.InvariantCulture, $", {pitch:0}° down") : "";
        return string.Create(CultureInfo.InvariantCulture, $"Camera at {x}, {z}{height}{tipped} · {looking}");
    }

    // ── the notes column ──

    private async Task OpenNote(MapNoteDto note)
    {
        if (note.Anchor.ViewId is { } view && Gallery?.Views.Any(shown => shown.Id == view) == true) shownId = view;
        currentId = note.Id;
        step = NotesStep.Thread;
        notesError = null;
        tool = null;
        drawing = null;
        ClearMark();
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
        stale = false;
        pickRound++;
    }

    /// <summary>Whether a pick read a board the pictures are not of, saying so where it did.</summary>
    private bool Moved(EyePickDto pick)
    {
        if (pick.Change <= DrawnAt) return false;
        stale = true;
        pickNote = string.Create(CultureInfo.InvariantCulture,
            $"The board has changed since this picture was drawn: it shows change {DrawnAt}, and change {pick.Change} has landed. Draw the pictures again, then mark it afresh.");
        return true;
    }

    /// <summary>Draw the pictures again, keeping the note's text and arming the tool its mark was drawn with.</summary>
    private async Task RedrawAsync()
    {
        var kind = mark?.Kind;
        ClearMark();
        notesError = null;
        await OnRedraw.InvokeAsync();
        if (kind is not null && step == NotesStep.New) tool = kind;
    }

    private void PinToMap()
    {
        ClearMark();
        tool = null;
        wholeMap = true;
    }

    private bool Ready => wholeMap || mark is null || picked is not null;

    /// <summary>What an open thread's reply is pinned to, or null where no mark is drawn for it.</summary>
    private string? ReplyMark => step != NotesStep.Thread || mark is not { } drawn ? null
        : $"{char.ToUpperInvariant(drawn.Kind[0])}{drawn.Kind[1..]}: "
          + (pickNote ?? (picked is null ? "Finding the ground under the mark…" : Ground(picked)));

    private string AnchorTitle => wholeMap ? "The whole map"
        : mark is { } drawn ? $"{char.ToUpperInvariant(drawn.Kind[0])}{drawn.Kind[1..]} on {Shown?.Name}"
        : $"This view: {Shown?.Name}";

    private string AnchorBody => wholeMap
        ? "The note is about the whole map."
        : mark is null
            ? "The note saves this camera and picture. To pin it to a spot, pick Point, Box, or Lasso on the picture."
            : pickNote ?? (picked is null ? "Finding the ground under the mark…" : Ground(picked));

    private static string Ground(EyePickDto pick)
    {
        if (pick.Hit is { } hit)
            return string.Create(CultureInfo.InvariantCulture, $"Block at {hit.X}, {hit.Y}, {hit.Z}")
                + (pick.Ground is { } ground && ground.Y != hit.Y
                    ? string.Create(CultureInfo.InvariantCulture, $", above ground at y {ground.Y}.") : ".");
        if (pick.Columns.Count > 0 || pick.OverVoid.Count > 0)
        {
            var over = pick.OverVoid.Count > 0
                ? string.Create(CultureInfo.InvariantCulture, $"{pick.OverVoid.Count} columns over the void")
                : pick.Sky > 0 ? string.Create(CultureInfo.InvariantCulture, $"{pick.Sky} pixels of sky") : null;
            var ground = pick.Columns.Count > 0
                ? string.Create(CultureInfo.InvariantCulture, $"{pick.Columns.Count} ground columns") : null;
            return string.Join(" and ", new[] { ground, over }.OfType<string>())
                + ". Ground hidden from this camera isn't included.";
        }
        return "The mark covers only sky, so the note is pinned to the picture alone.";
    }

    private async Task<bool> SendNoteAsync(NoteWriting writing)
    {
        if (Shown is not { } view) return false;
        notesError = null;
        try
        {
            NoteAnchorDto anchor;
            string? picture = null;
            if (wholeMap) anchor = new NoteAnchorDto(NoteAnchors.Map);
            else
            {
                var camera = mark is null ? await PickAsync(view, null) : picked;
                if (camera is null || Moved(camera))
                {
                    notesError = pickNote ?? "Couldn't read this picture's camera, so the note wasn't sent.";
                    return false;
                }
                picture = await JS.InvokeAsync<string?>("studio.keepPicture", PictureSource(view));
                if (picture is null)
                {
                    notesError = "The note was not sent: the browser could not keep a copy of the picture it is written on. Send it again.";
                    return false;
                }
                anchor = AnchorOf(mark?.Kind ?? NoteAnchors.View, view.Id, view.Name, camera, mark?.Pixels);
            }
            var answer = await Http.PostAsJsonAsync($"api/map/{Slug}/notes",
                new MapNoteRequest(writing.Body, anchor, writing.Tag, picture, DrawnAt));
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
            return true;
        }
        catch
        {
            notesError = "Couldn't send the note. Check your connection and try again.";
            return false;
        }
        finally { StateHasChanged(); }
    }

    /// <summary>What a mark, or a picture with none, was pinned to on the ground: one shape for a note's anchor and
    /// a reply's mark.</summary>
    private static NoteAnchorDto AnchorOf(string kind, string? viewId, string? viewName, EyePickDto pick,
                                          IReadOnlyList<PixelDto>? pixels) =>
        new(kind, viewId, viewName, pick.Camera, PictureWidth, PictureHeight, pixels, pick.Hit, pick.Ground,
            pick.Columns.Count > 0 ? pick.Columns : null, pick.OverVoid.Count > 0 ? pick.OverVoid : null);

    private async Task<bool> ReplyAsync(string body)
    {
        if (Current is not { } note) return false;
        notesError = null;
        NoteAnchorDto? pinned = null;
        if (mark is { } drawn)
        {
            if (picked is not { } pick) return false;
            var (viewId, viewName) = Compared is not null ? (note.Anchor.ViewId, note.Anchor.ViewName) : (Shown?.Id, Shown?.Name);
            pinned = AnchorOf(drawn.Kind, viewId, viewName, pick, drawn.Pixels);
        }
        try
        {
            var answer = await Http.PostAsJsonAsync($"api/map/{Slug}/notes/{note.Id}/replies",
                new NoteReplyRequest(body, Mark: pinned));
            if (!answer.IsSuccessStatusCode)
            {
                notesError = await ServerRefusal.SentenceAsync(answer);
                return false;
            }
            await LoadNotesAsync();
            ClearMark();
            return true;
        }
        catch
        {
            notesError = "Couldn't send the reply. Check your connection and try again.";
            return false;
        }
        finally { StateHasChanged(); }
    }

    private Task ChangeAsync(string status) => PatchAsync(new NoteChangeRequest(status));

    /// <summary>Change the open thread's tag; empty clears it.</summary>
    private Task RetagAsync(string tag) => PatchAsync(new NoteChangeRequest(Tag: tag));

    private async Task PatchAsync(NoteChangeRequest change)
    {
        if (Current is not { } note) return;
        busy = true;
        notesError = null;
        try
        {
            var answer = await Http.PatchAsJsonAsync($"api/map/{Slug}/notes/{note.Id}", change);
            if (!answer.IsSuccessStatusCode) notesError = await ServerRefusal.SentenceAsync(answer);
            else await LoadNotesAsync();
        }
        catch { notesError = "Couldn't update the note. Check your connection and try again."; }
        finally { busy = false; }
    }

    // ── drawing a mark on the picture ──

    /// <summary>What a mark drawn now is for: the reply in an open thread, else a new note.</summary>
    private string Marking => step == NotesStep.Thread ? "reply" : "note";

    /// <summary>What a dock tool pins, as its title says it.</summary>
    private string Pins => step == NotesStep.Thread ? "the reply" : "a note";

    private string ToolHint => tool switch
    {
        NoteAnchors.Point => $"Click the block the {Marking} is about.",
        NoteAnchors.Box => $"Drag over the area the {Marking} is about.",
        _ => $"Draw around the area the {Marking} is about.",
    };

    /// <summary>Arming a tool pins the reply in an open thread, and otherwise opens a new note pinned to what it
    /// draws; arming it again puts it down.</summary>
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
        if (step == NotesStep.Thread) return;
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
        // A thread comparing its pictures shows its note's own camera over the board now, so a reply's mark is
        // read there; the gallery's view is read at the change its pictures are of.
        var noteCamera = step == NotesStep.Thread && Compared is not null ? Current?.Anchor.Camera : null;
        var answer = noteCamera is { } camera ? await PickAsync(Exact(camera), mark) : await PickAsync(view.Query, mark);
        if (round != pickRound || (answer is not null && noteCamera is null && Moved(answer))) return;
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
    private Task<EyePickDto?> PickAsync(MapViewDto view, Mark? drawn) => PickAsync(view.Query, drawn);

    /// <summary>The <c>render/eye</c> words that draw <paramref name="camera"/> exactly.</summary>
    private static string Exact(EyeCameraDto camera) => string.Create(CultureInfo.InvariantCulture,
        $"eye={camera.X},{camera.Y},{camera.Z}&yaw={camera.Yaw}&pitch={camera.Pitch}&fov={camera.Fov}");

    private async Task<EyePickDto?> PickAsync(string query, Mark? drawn)
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
            var answer = await Http.GetAsync($"api/map/{Slug}/render/eye/pick?{query}&width={PictureWidth}&height={PictureHeight}{shape}");
            if (answer.IsSuccessStatusCode) return await answer.Content.ReadFromJsonAsync<EyePickDto>();
            pickNote = await ServerRefusal.SentenceAsync(answer);
        }
        catch { pickNote = "Couldn't read the ground under the mark. Check your connection."; }
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
        $"--pin-x: {x / PictureWidth * 100:0.##}%; --pin-y: {y / PictureHeight * 100:0.##}%");
}
