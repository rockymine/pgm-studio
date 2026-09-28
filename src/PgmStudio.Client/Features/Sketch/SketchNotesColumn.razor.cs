using System.Globalization;
using Microsoft.AspNetCore.Components;
using PgmStudio.Contracts;
using PgmStudio.Vocabulary;

namespace PgmStudio.Client.Features.Sketch;

/// <summary>Which step the notes column is on.</summary>
public enum NotesStep { Overview, Thread, New }

/// <summary>A new note as the author wrote it: its text and, where they chose one, its tag.</summary>
public sealed record NoteWriting(string Body, string? Tag);

/// <summary>The notes column: the overview under its filters, one thread, or a new note.</summary>
public partial class SketchNotesColumn
{
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

    private string draft = "";
    private string? tag;
    private string reply = "";

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

    private async Task SendNoteAsync()
    {
        if (OnSend is null || string.IsNullOrWhiteSpace(draft)) return;
        if (!await OnSend(new NoteWriting(draft.Trim(), tag))) return;
        draft = "";
        tag = null;
    }

    private async Task SendReplyAsync()
    {
        if (OnReply is null || string.IsNullOrWhiteSpace(reply)) return;
        if (await OnReply(reply.Trim())) reply = "";
    }

    /// <summary>What a note is pinned to, in a few words.</summary>
    public static string Title(MapNoteDto note) => note.Anchor.Kind switch
    {
        NoteAnchors.Map => "Whole map",
        NoteAnchors.View => $"This view{Of(note)}",
        var kind => $"{char.ToUpperInvariant(kind[0])}{kind[1..]}{Of(note)}",
    };

    private static string Of(MapNoteDto note) => note.Anchor.ViewName is { Length: > 0 } name ? $" on {name}" : "";

    /// <summary>The note's last message, as the overview shows it.</summary>
    private static string Last(MapNoteDto note) =>
        note.Messages.LastOrDefault() is { } last ? (last.Token is null ? last.Body : $"{last.Author}: {last.Body}") : "";

    private static string Glyph(MapNoteDto note) =>
        note.Anchor.Kind == NoteAnchors.Map ? "" : note.Id.ToString(CultureInfo.InvariantCulture);

    /// <summary>The pin's class: its shape says whether it is on the map or a picture, its colour the status.</summary>
    public static string PinClass(MapNoteDto note) =>
        $"note-pin note-pin--{note.Status}" + (note.Anchor.Kind == NoteAnchors.Map ? " note-pin--map" : "");

    private static string StatusClass(string status) => $"note-status note-status--{status}";

    /// <summary>How long ago, in the words the thread reads in.</summary>
    private static string Ago(DateTime at)
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
