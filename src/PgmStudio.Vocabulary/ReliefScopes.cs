namespace PgmStudio.Vocabulary;

/// <summary>
/// How a shape's ground meets the relief its group is solved over. A relief states a surface across a whole
/// group; the word decides what the solver does where this shape stands.
///
/// <para><b>Absent is the default and has no word</b>: the shape is part of the group's ground and the relief
/// rolls through it, which is what a shape drawn to make a landmass wants. The three stated words each take
/// that away in a different degree — the shape follows the surface flat, holds the height it states, or leaves
/// the solve altogether.</para>
///
/// <para>Three parties spell them: the rasterizer that solves the field, the plan compiler that tags a role
/// piece, and the canvas the author picks one on. A word none of them knows reads as the default, so a fourth
/// spelling is a shape silently rolled over by terrain the author meant to stand clear of it.</para>
/// </summary>
public static class ReliefScopes
{
    /// <summary>The shape takes the height the relief solved under it, and is then held <b>flat</b> at that
    /// height. It moves with the terrain and keeps a level floor — what a room wants, since a plan states its
    /// piece's height before any ground exists and the number it carries is about a flat board.</summary>
    public const string Follow = "follow";

    /// <summary>The shape is pinned at the height it states and the relief does not move it. The surrounding
    /// surface is solved knowing where it has to arrive, so ground that wants to be far above or below meets
    /// it as a face — which is the point: a walled town the valley runs up to, and the only word that keeps an
    /// author's stated height against a relief that disagrees.</summary>
    public const string Hold = "hold";

    /// <summary>The shape's footprint leaves the solve entirely: nothing is pinned and no surface is fitted
    /// over it, so the land is whatever that outline would have made and the shape keeps its own height — a
    /// citadel on its own plinth.</summary>
    public const string Exclude = "exclude";

    /// <summary>The three a shape may state. Absent is the default and is not one of them.</summary>
    public static readonly string[] All = [Follow, Hold, Exclude];

    /// <summary>Whether the word takes the shape's ground out of the group's own surface — every stated scope
    /// does, which is what separates them from the default.</summary>
    public static bool Stated(string? scope) => scope is not null && All.Contains(scope);
}
