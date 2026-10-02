using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using PgmStudio.Contracts;
using PgmStudio.Vocabulary;

namespace PgmStudio.Client.Features.Sketch;

/// <summary>The History phase's inspector: what one change did, or the changes since one.</summary>
public partial class SketchChangeInspector
{
    /// <summary>The change the span ends at, whose writer and note are shown; null before one is picked.</summary>
    [Parameter] public MapChangeDto? Change { get; set; }

    /// <summary>The change the span starts from — the one before <see cref="To"/> for a single change.</summary>
    [Parameter] public long From { get; set; }

    [Parameter] public long To { get; set; }

    /// <summary>The board's latest change.</summary>
    [Parameter] public long Latest { get; set; }

    /// <summary>The edits between the two, or null while they are compared.</summary>
    [Parameter] public MapDiffDto? Diff { get; set; }

    /// <summary>The columns the two builds disagree on, or null while both are built.</summary>
    [Parameter] public WorldChangesDto? World { get; set; }

    [Parameter] public string? WorldError { get; set; }

    [Parameter] public bool Restoring { get; set; }

    [Parameter] public string? RestoreError { get; set; }

    /// <summary>Put the board back as it stood at a change.</summary>
    [Parameter] public EventCallback<long> OnRestore { get; set; }

    /// <summary>The edits listed before the rest are only counted.</summary>
    private const int Shown = 60;

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
