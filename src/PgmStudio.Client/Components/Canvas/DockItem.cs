namespace PgmStudio.Client.Components;

/// <summary>
/// One option inside a collapsed dock group (<c>DockFlyoutGroup</c>) — a tool that could occupy the group's
/// single visible slot. It carries exactly what a <c>DockButton</c> needs to draw itself: a glyph when the
/// thing it makes has a shape, or a colour (and, for a patterned fill, a swatch modifier) when the thing it
/// makes is told apart by how it looks on the canvas.
/// </summary>
/// <param name="Key">Identifies the option inside its own group. Groups do not share a key space — two
/// groups may both offer a "spawn", because each group's pick handler knows which one it means.</param>
/// <param name="Title">The tooltip. In an icon-only flyout it is the only name the option has.</param>
public sealed record DockItem(
    string Key,
    string Title,
    string? Icon = null,
    string? Swatch = null,
    string? SwatchClass = null);
