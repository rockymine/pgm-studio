namespace PgmStudio.Client.Components;

/// <summary>The units and qualifiers a field's label may carry after it, in parentheses. A height is in world y and
/// a length in blocks; anything longer than one of these is a note or help (<c>docs/client/writing-for-the-ui.md</c>).</summary>
public static class FieldUnits
{
    public const string Blocks = "blocks", WorldY = "world y", Degrees = "°", Percent = "%", Seconds = "seconds",
        Ticks = "ticks", Players = "players", Cells = "cells", Optional = "optional";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
        { Blocks, WorldY, Degrees, Percent, Seconds, Ticks, Players, Cells, Optional };
}
