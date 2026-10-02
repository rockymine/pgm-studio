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

/// <summary>A new note as the author wrote it: its text and, where they chose one, its tag.</summary>
public sealed record NoteWriting(string Body, string? Tag);

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
    [Parameter] public EventCallback OnReopen { get; set; }

    /// <summary>Send a new note; answers whether it landed, which is what clears the text box.</summary>
    [Parameter] public Func<NoteWriting, Task<bool>>? OnSend { get; set; }

    /// <summary>Send a reply; answers whether it landed.</summary>
    [Parameter] public Func<string, Task<bool>>? OnReply { get; set; }

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
    private string? tag;
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
        if (!await SendingAsync(() => OnSend(new NoteWriting(draft.Trim(), tag)))) return;
        draft = "";
        tag = null;
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

    /// <summary>A message's class: an agent's, the caller's own, or another person's, which is where it sits
    /// and what colour its bubble is.</summary>
    private string MessageClass(NoteMessageDto message) =>
        message.Token is not null ? "note-message note-message--agent"
        : string.Equals(message.AuthorUuid, MeUuid, StringComparison.OrdinalIgnoreCase) ? "note-message note-message--mine"
        : "note-message";

    private static string Glyph(MapNoteDto note) =>
        note.Anchor.Kind == NoteAnchors.Map ? "" : note.Id.ToString(CultureInfo.InvariantCulture);

    /// <summary>The pin's class: its shape says whether it is on the map or a picture, its colour the status.</summary>
    public static string PinClass(MapNoteDto note) =>
        $"note-pin note-pin--{note.Status}" + (note.Anchor.Kind == NoteAnchors.Map ? " note-pin--map" : "");

    private static string StatusClass(string status) => $"note-status note-status--{status}";

    private static string Ago(DateTime at) => Moments.Ago(at);

    /// <summary>How many of the board's changes landed after the thread's last message was written.</summary>
    private int ChangesSince(MapNoteDto note) =>
        note.Messages.LastOrDefault() is { } last ? Changes.Count(number => number > last.Change) : 0;

    /// <summary>The change the thread's last message was written at, which is where its diff starts.</summary>
    private static long LastChange(MapNoteDto note) => note.Messages.LastOrDefault()?.Change ?? 0;
}

/// <summary>When something happened, in the words the Sketch tool's columns read in.</summary>
internal static class Moments
{
    public static string Ago(DateTime at)
    {
        var since = DateTime.UtcNow - DateTime.SpecifyKind(at, DateTimeKind.Utc);
        return since.TotalMinutes < 1 ? "just now"
            : since.TotalHours < 1 ? $"{(int)since.TotalMinutes} min ago"
            : since.TotalDays < 1 ? $"{(int)since.TotalHours} h ago"
            : since.TotalDays < 2 ? "yesterday"
            : since.TotalDays < 7 ? $"{(int)since.TotalDays} days ago"
            : at.ToLocalTime().ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
    }
}
