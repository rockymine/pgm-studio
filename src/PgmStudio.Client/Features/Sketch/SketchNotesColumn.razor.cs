using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using PgmStudio.Client.Components;
using PgmStudio.Contracts;
using PgmStudio.Vocabulary;

namespace PgmStudio.Client.Features.Sketch;

/// <summary>Which step the notes column is on.</summary>
public enum NotesStep { Overview, Thread, New }

/// <summary>The notes column: the overview under its filters, one thread, or a new note.</summary>
public partial class SketchNotesColumn : IDisposable
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <summary>The four filters, by what the author is looking for: everything, what waits on them, what
    /// waits on an agent, and what is done.</summary>
    public const string All = "all", WaitingOnYou = "you", WithAgent = "agent", Done = "resolved";

    private static readonly (string Id, string Label)[] Filters =
        [(All, "All"), (WaitingOnYou, "Waiting on you"), (WithAgent, "With agent"), (Done, "Resolved")];

    [Parameter] public NotesStep Step { get; set; }
    [Parameter] public IReadOnlyList<MapNoteDto> Notes { get; set; } = [];

    /// <summary>The gallery views there are, so a note on a picture no longer offered is told apart.</summary>
    [Parameter] public IReadOnlyCollection<string> ViewIds { get; set; } = [];
    [Parameter] public string? ViewId { get; set; }
    [Parameter] public string? ViewName { get; set; }
    [Parameter] public MapNoteDto? Current { get; set; }
    [Parameter] public string Filter { get; set; } = All;

    /// <summary>What a new note is pinned to, as a title and a sentence.</summary>
    [Parameter] public string AnchorTitle { get; set; } = "";
    [Parameter] public string AnchorBody { get; set; } = "";
    [Parameter] public bool HasMark { get; set; }

    /// <summary>What the reply in an open thread is pinned to, or null where no mark is drawn for it.</summary>
    [Parameter] public string? ReplyMark { get; set; }

    /// <summary>Whether the pictures are of a board that has changed since, so a mark on one cannot be read.</summary>
    [Parameter] public bool Stale { get; set; }
    [Parameter] public EventCallback OnRedraw { get; set; }
    [Parameter] public bool WholeMap { get; set; }

    /// <summary>Whether a new note has what it needs — a mark's ground is read before it can be sent.</summary>
    [Parameter] public bool Ready { get; set; } = true;
    [Parameter] public bool Busy { get; set; }
    [Parameter] public string? Error { get; set; }

    [Parameter] public EventCallback<string> OnFilter { get; set; }
    [Parameter] public EventCallback<MapNoteDto> OnOpen { get; set; }
    [Parameter] public EventCallback OnBack { get; set; }
    [Parameter] public EventCallback OnNew { get; set; }
    [Parameter] public EventCallback OnClearMark { get; set; }
    [Parameter] public EventCallback OnWholeMap { get; set; }
    [Parameter] public EventCallback OnResolve { get; set; }
    [Parameter] public EventCallback OnWontDo { get; set; }

    [Parameter] public EventCallback OnReopen { get; set; }

    /// <summary>Send a new note; answers whether it landed, which is what clears the text box.</summary>
    [Parameter] public Func<string, Task<bool>>? OnSend { get; set; }

    /// <summary>Send a reply; answers whether it landed.</summary>
    [Parameter] public Func<string, Task<bool>>? OnReply { get; set; }

    /// <summary>Where the open notes stand with the agent, or null where it could not be read.</summary>
    [Parameter] public NoteHandoffDto? Handoff { get; set; }
    [Parameter] public bool Handing { get; set; }

    /// <summary>Hand the open notes to the agent; true repeats a hand-off nothing was written since.</summary>
    [Parameter] public EventCallback<bool> OnHandOff { get; set; }

    /// <summary>The caller's account, which tells their own messages from the rest; null for an open studio's
    /// local admin, whose messages are unsigned too.</summary>
    [Parameter] public string? MeUuid { get; set; }

    /// <summary>The numbers of the board's changes, which is what a thread counts the changes since its last
    /// message against.</summary>
    [Parameter] public IReadOnlyList<long> Changes { get; set; } = [];

    /// <summary>Open what changed after a thread's last message, from the change it was written at.</summary>
    [Parameter] public EventCallback<long> OnChangesSince { get; set; }

    /// <summary>How long "sent" stays beside the button.</summary>
    private static readonly TimeSpan ConfirmFor = TimeSpan.FromSeconds(4);

    private string draft = "";
    private string reply = "";
    private bool sending;
    private string? sent;
    private CancellationTokenSource? confirming;
    private ElementReference thread;
    private (long Id, int Messages)? followed;
    private bool toEnd;

    private IEnumerable<MapNoteDto> OnThisPicture => Notes.Where(note => note.Anchor.ViewId is { } view && view == ViewId);
    private IEnumerable<MapNoteDto> OnTheMap => Notes.Where(note => note.Anchor.Kind == NoteAnchors.Map);
    private IEnumerable<MapNoteDto> Elsewhere =>
        Notes.Where(note => note.Anchor.ViewId is { } view && view != ViewId && !ViewIds.Contains(view));

    private bool Passes(MapNoteDto note) => Passes(note, Filter);

    /// <summary>Whether <paramref name="note"/> is one <paramref name="filter"/> shows.</summary>
    public static bool Passes(MapNoteDto note, string filter) => filter switch
    {
        WaitingOnYou => NoteStatuses.WaitsOnAuthor(note.Status),
        WithAgent => note.Status == NoteStatuses.Open,
        Done => note.Status == NoteStatuses.Resolved,
        _ => true,
    };

    private int Count(string filter) =>
        OnThisPicture.Concat(OnTheMap).Concat(Elsewhere).Count(note => Passes(note, filter));

    /// <summary>Send the new note. The column owns the sending, so the button says so from the click on and a
    /// second click sends nothing; the text box clears only once the note has landed.</summary>
    private async Task SendNoteAsync()
    {
        if (sending || OnSend is null || string.IsNullOrWhiteSpace(draft)) return;
        if (!await SendingAsync(() => OnSend(draft.Trim()))) return;
        draft = "";
        _ = ConfirmAsync("Note sent");
    }

    private async Task SendReplyAsync()
    {
        if (sending || OnReply is null || string.IsNullOrWhiteSpace(reply)) return;
        if (!await SendingAsync(() => OnReply(reply.Trim()))) return;
        reply = "";
        _ = ConfirmAsync("Reply sent");
    }

    private async Task<bool> SendingAsync(Func<Task<bool>> send)
    {
        sending = true;
        sent = null;
        StateHasChanged();
        try { return await send(); }
        finally { sending = false; }
    }

    /// <summary>Say what was sent beside the button, for a few seconds.</summary>
    private async Task ConfirmAsync(string what)
    {
        confirming?.Cancel();
        var shown = confirming = new CancellationTokenSource();
        sent = what;
        StateHasChanged();
        try { await Task.Delay(ConfirmFor, shown.Token); }
        catch (TaskCanceledException) { return; }
        sent = null;
        StateHasChanged();
    }

    /// <summary>A thread opens on its newest message, and follows it as messages land.</summary>
    protected override void OnParametersSet()
    {
        (long, int)? showing = Step == NotesStep.Thread && Current is { } note ? (note.Id, note.Messages.Count) : null;
        if (showing is not null && showing != followed) toEnd = true;
        followed = showing;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!toEnd || Step != NotesStep.Thread) return;
        toEnd = false;
        await JS.InvokeVoidAsync("studio.scrollToEnd", thread);
    }

    /// <summary>Ctrl+Enter (⌘+Enter on a Mac) in a text box sends it.</summary>
    private Task SendOnKey(KeyboardEventArgs key, Func<Task> send) =>
        key.Key == "Enter" && (key.CtrlKey || key.MetaKey) ? send() : Task.CompletedTask;

    public void Dispose() => confirming?.Cancel();

    /// <summary>What a reply's mark is on, in a few words: the tool, the view and the ground it names.</summary>
    private static string MarkLine(NoteAnchorDto mark)
    {
        var on = mark.ViewName is { Length: > 0 } name ? $" on {name}" : "";
        var ground = mark.Hit is { } hit ? string.Create(CultureInfo.InvariantCulture, $" · block at {hit.X}, {hit.Y}, {hit.Z}")
            : string.Join("", new[]
            {
                mark.Columns is { Count: > 0 } columns ? string.Create(CultureInfo.InvariantCulture, $" · {columns.Count} ground columns") : "",
                mark.OverVoid is { Count: > 0 } overVoid ? string.Create(CultureInfo.InvariantCulture, $" · {overVoid.Count} over the void") : "",
            });
        return $"{char.ToUpperInvariant(mark.Kind[0])}{mark.Kind[1..]}{on}{ground}";
    }

    /// <summary>What a note is pinned to, in a few words.</summary>
    public static string Title(MapNoteDto note) => note.Anchor.Kind switch
    {
        NoteAnchors.Map => "Whole map",
        NoteAnchors.View => $"This view{Of(note)}",
        var kind => $"{char.ToUpperInvariant(kind[0])}{kind[1..]}{Of(note)}",
    };

    private static string Of(MapNoteDto note) => note.Anchor.ViewName is { Length: > 0 } name ? $" on {name}" : "";

    /// <summary>The note's last message, as the overview shows it: an agent's under its name.</summary>
    private static string Last(MapNoteDto note) =>
        note.Messages.LastOrDefault() is { } last
            ? (last.Token is null ? last.Body : $"{Writers.Name(last.Author, last.Token)}: {last.Body}") : "";

    /// <summary>How close together two messages by one writer have to be for the second to continue the first.</summary>
    private static readonly TimeSpan ContinuesWithin = TimeSpan.FromMinutes(10);

    /// <summary>The class of message <paramref name="index"/>: an agent's, the caller's own, or another person's,
    /// which is where it sits and what colour its bubble is, and whether it continues the one before it.</summary>
    private string MessageClass(MapNoteDto note, int index)
    {
        var message = note.Messages[index];
        var kind = message.Token is not null ? "note-message note-message--agent"
            : string.Equals(message.AuthorUuid, MeUuid, StringComparison.OrdinalIgnoreCase) ? "note-message note-message--mine"
            : "note-message";
        return Continues(note, index) ? kind + " note-message--cont" : kind;
    }

    /// <summary>Whether message <paramref name="index"/> continues the one before it: the same writer, within
    /// <see cref="ContinuesWithin"/>, with no change landed between them.</summary>
    private bool Continues(MapNoteDto note, int index)
    {
        if (index == 0) return false;
        var before = note.Messages[index - 1];
        var message = note.Messages[index];
        return message.Author == before.Author && message.Token == before.Token
            && message.At - before.At < ContinuesWithin
            && Landed(before.Change, message.Change) == 0;
    }

    /// <summary>What a note's pin carries: its number, or nothing for a note on the whole map.</summary>
    public static string Glyph(MapNoteDto note) =>
        note.Anchor.Kind == NoteAnchors.Map ? "" : note.Id.ToString(CultureInfo.InvariantCulture);

    /// <summary>The pin's class: its shape says whether it is on the map or a picture, its colour the status.</summary>
    public static string PinClass(MapNoteDto note) =>
        $"note-pin note-pin--{note.Status}" + (note.Anchor.Kind == NoteAnchors.Map ? " note-pin--map" : "");

    private static string Ago(DateTime at) => Moments.Ago(at);

    /// <summary>What the hand-off line says beside its button.</summary>
    private static string Handed(NoteHandoffDto handoff) =>
        handoff.Waiting == 0 ? "No note is waiting for the agent."
        : handoff.HandedAt is not { } at ? $"{handoff.Waiting} waiting on every map. Nothing runs until they are handed over."
        : handoff.Fresh > 0 ? $"{handoff.Fresh} of {handoff.Waiting} written since the last hand-off, {Moments.Ago(at)}."
        : $"All {handoff.Waiting} handed over {Moments.Ago(at)}.";

    /// <summary>How many of the board's changes landed after <paramref name="after"/> and up to
    /// <paramref name="upTo"/>, or after it at all where <paramref name="upTo"/> is null.</summary>
    private int Landed(long after, long? upTo) =>
        Changes.Count(number => number > after && (upTo is not { } last || number <= last));

    private static string Changed(int count) => count == 1 ? "1 change landed" : $"{count} changes landed";
}
