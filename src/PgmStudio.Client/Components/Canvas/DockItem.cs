namespace PgmStudio.Client.Components;

/// <summary>
/// One option inside a collapsed dock group (<c>DockFlyoutGroup</c>) — a tool that could occupy the group's
/// single visible slot. It carries exactly what a <c>DockButton</c> needs to draw itself: a glyph when the
/// thing it makes has a shape, or a colour (and, for a patterned fill, a swatch modifier) when the thing it
/// makes is told apart by how it looks on the canvas.
/// </summary>
/// <param name="Key">Identifies the option inside its own group. Groups do not share a key space — two
/// groups may both offer a "spawn", because each group's pick handler knows which one it means.</param>
/// <param name="Name">What the option is called — two or three words, printed in the flyout and shown as the
/// slot's tooltip. It has to stand on its own: a flyout shows one family at a time, with nothing around it
/// to say which family, so a name that collides across families says which it is ("Wool marker", "Wool
/// box"). It names the tool and does not explain it; how a lasso or a polygon is used is not a caption's
/// job, and a sentence in every tooltip is a sentence in the way.</param>
public sealed record DockItem(
    string Key,
    string Name,
    string? Icon = null,
    string? Swatch = null,
    string? SwatchClass = null);
