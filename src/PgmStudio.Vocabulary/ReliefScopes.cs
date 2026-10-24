namespace PgmStudio.Vocabulary;

/// <summary>
/// How a shape's ground meets the sketch's modifier stack. Absent is the default and has no word: the shape is
/// ordinary ground and every modifier whose mask reaches it shapes it, which is what a shape drawn to make a
/// landmass wants. The one stated word takes the shape out of the stack.
///
/// <para>Three parties spell it: the rasterizer that runs the stack, the plan compiler that tags a role piece,
/// and the canvas the author picks it on. A word none of them knows reads as the default, so a second spelling
/// is a shape silently reshaped by terrain the author meant to stand clear of it.</para>
/// </summary>
public static class ReliefScopes
{
    /// <summary>The shape's columns stay at the top it states: no modifier raises, lowers, levels or roughens
    /// them, and the ground round them is shaped up to their edge. A walled town on its own plinth, a spawn
    /// floor, a monument's footing.</summary>
    public const string Hold = "hold";

    /// <summary>The words a shape may state. Absent is the default and is not one of them.</summary>
    public static readonly string[] All = [Hold];
}
