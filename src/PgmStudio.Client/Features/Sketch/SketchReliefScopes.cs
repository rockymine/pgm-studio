using PgmStudio.Client.Components;
using PgmStudio.Vocabulary;

namespace PgmStudio.Client.Features.Sketch;

/// <summary>How the modifier stack meets a drawn shape, as every select that sets it offers the choice
/// (<c>docs/tools/sketch.md</c>, <i>Terraform</i>). The empty word is the default: Follow for ground, Hold for a
/// solid, so each list leads with its default and states the other two.</summary>
public static class SketchReliefScopes
{
    private static readonly SelectOption Follow =
        new(ReliefScopes.Follow, "Follow", "The modifiers reshape it with the ground around it.");

    private static readonly SelectOption Hold =
        new(ReliefScopes.Hold, "Hold", "The modifiers leave it at its own height.");

    private static readonly SelectOption Ride =
        new(ReliefScopes.Ride, "Ride", "Keeps its form and rises or sinks with the ground under it.");

    /// <summary>The choice on a ground shape, which follows unless it says otherwise.</summary>
    public static readonly IReadOnlyList<SelectOption> Ground = [Follow with { Value = "" }, Hold, Ride];

    /// <summary>The choice on a solid, which holds unless it says otherwise.</summary>
    public static readonly IReadOnlyList<SelectOption> Solid = [Hold with { Value = "" }, Follow, Ride];
}
