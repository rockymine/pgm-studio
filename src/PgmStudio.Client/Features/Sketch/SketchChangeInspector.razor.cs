using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using PgmStudio.Contracts;
using PgmStudio.Vocabulary;

namespace PgmStudio.Client.Features.Sketch;

/// <summary>The History phase's inspector: what one change did, or the changes since one.</summary>
public partial class SketchChangeInspector
{
    [Parameter, EditorRequired] public SketchHistory History { get; set; } = default!;

    /// <summary>The board's notes, which name the threads written in the span shown.</summary>
    [Parameter, EditorRequired] public SketchNotes Notes { get; set; } = default!;

    /// <summary>Open a thread in In game, by its note's id.</summary>
    [Parameter] public EventCallback<long> OnOpenNote { get; set; }

    /// <summary>The edits listed before the rest are only counted.</summary>
    private const int Shown = 60;

    /// <summary>The change the span ends at, whose writer and note are shown; null before one is picked.</summary>
    private MapChangeDto? Change => History.SpanChange;

    /// <summary>The change the span starts from — the one before <see cref="To"/> for a single change.</summary>
    private long From => History.SpanFrom;

    private long To => History.SpanTo ?? 0;

    /// <summary>The board's latest change.</summary>
    private long Latest => History.LatestChange;

    /// <summary>The edits between the two, or null while they are compared.</summary>
    private MapDiffDto? Diff => History.SpanDiff;

    /// <summary>The columns the two builds disagree on, or null while both are built.</summary>
    private WorldChangesDto? World => History.SpanWorld;

    private string? WorldError => History.SpanWorldError;

    private bool Restoring => History.Restoring;

    private string? RestoreError => History.RestoreError;

    /// <summary>The threads with a message written in the span, each with the latest such message — what a change
    /// answered, and the notes written on its board.</summary>
    private IReadOnlyList<(MapNoteDto Note, NoteMessageDto Message)> SpanNotes => History.NotesInSpan(Notes.All);

    private string Title => To - From == 1 || From == 0 && To == 1 ? $"Change #{To}" : $"#{From} → #{To}";

    private string RestoreReadout =>
        From == 0 ? "There is nothing before the first change to restore."
        : From == Latest ? "This is the current version."
        : Latest - From == 1 ? $"Undoes #{Latest} as a new change."
        : $"Undoes #{From + 1} to #{Latest} ({Latest - From} changes) as one new change.";

    private string? OriginLink =>
        Change?.Origin is { Repo: { } repo } origin && Regex.IsMatch(repo, @"^[\w.-]+/[\w.-]+$")
            ? origin.Commit is { Length: > 0 } commit
                ? $"https://github.com/{repo}/tree/{commit}/{origin.Path ?? ""}".TrimEnd('/')
                : $"https://github.com/{repo}"
            : null;

    private string OriginText =>
        Change?.Origin is { } origin
            ? $"{origin.Repo}@{origin.Commit}{(origin.Dirty == true ? " (with uncommitted changes)" : "")} {origin.Path}"
            : "";

    /// <summary>What an edit is about: the id of what it adds, the id its path addresses last, or the path
    /// itself where it names none.</summary>
    private static string Subject(DocumentEdit edit)
    {
        if (edit.Op == DocumentEdit.Add && edit.Value.ValueKind == System.Text.Json.JsonValueKind.Object
            && edit.Value.TryGetProperty("id", out var id) && id.GetString() is { Length: > 0 } added) return added;
        var named = Regex.Matches(edit.Path, @"\[([^\]]+)\]").Select(match => match.Groups[1].Value)
            .LastOrDefault(key => !int.TryParse(key, out _));
        return named ?? edit.Path;
    }
}
