using Microsoft.AspNetCore.Components;
using PgmStudio.Contracts;

namespace PgmStudio.Client.Features.Sketch;

/// <summary>The History phase's list of the board's changes.</summary>
public partial class SketchHistoryList
{
    /// <summary>The board's changes, oldest first as the studio answers them; null while they are read.</summary>
    [Parameter] public IReadOnlyList<MapChangeDto>? Changes { get; set; }

    /// <summary>The change drawn on the canvas, if one is.</summary>
    [Parameter] public long? Picked { get; set; }

    /// <summary>Why the changes could not be read.</summary>
    [Parameter] public string? Error { get; set; }

    [Parameter] public EventCallback<long> OnPick { get; set; }

    /// <summary>What a change is, in its writer's words where they gave some and by what it wrote where not.</summary>
    internal static string Said(MapChangeDto change) =>
        change.Note is { Length: > 0 } note ? note : $"Changed the {string.Join(", ", change.Documents)}";

    /// <summary>Who wrote a change: the person, and the token's label where a token wrote it.</summary>
    internal static string Who(MapChangeDto change) =>
        (change.Writer ?? "Unknown") + (change.Token is { } token ? $" (token “{token}”)" : "");
}
