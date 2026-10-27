namespace PgmStudio.Vocabulary;

/// <summary>
/// How a shape's ground meets the sketch's modifier stack. Absent is the default: a ground shape follows, so
/// every modifier whose mask reaches it shapes it, which is what a shape drawn to make a landmass wants, and a
/// solid (box, wedge, cylinder, sphere, torus, arch) holds. A stated word takes the shape out of the stack,
/// has it ride the stack, or states the follow a solid would not otherwise have.
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

    /// <summary>The shape keeps its form and moves with the ground: each of its columns rises or falls by the
    /// median change the stack made to the ground under the shape, so it stays one flat thing standing proud.
    /// A building on a hill the modifiers raise.</summary>
    public const string Ride = "ride";

    /// <summary>The modifiers shape the shape's columns as they shape any ground. What a ground shape does
    /// without saying so, and so the one word a solid states to be reshaped by a hill beside it.</summary>
    public const string Follow = "follow";

    /// <summary>The words a shape may state.</summary>
    public static readonly string[] All = [Hold, Ride, Follow];
}
