using PgmStudio.Vocabulary;
namespace PgmStudio.Pgm.Compose;

/// <summary>Thrown when the composer cannot produce a plan satisfying its hard invariants within its bounded
/// retry budget — a structural failure of the request (an impossible combination of inputs), not a transient
/// one a caller should retry.</summary>
public sealed class ComposeException(string message) : Exception(message);

/// <summary>What the composer refuses, as opposed to what a descriptor gets wrong on the way in.</summary>
public static class ComposeRules
{
    /// <summary>The descriptor is well-formed and names a board the composer cannot emit: a spawn profile
    /// outside the two forms it builds, a hub arm count it has no shape for, a frontline form off its menu, a
    /// unit whose hub publishes no offer to consume. The sentence carries which, because the emitter that
    /// stopped is the only thing that knows. 422 — the request was read and acted on, and there is no board
    /// at the end of it.</summary>
    /// <remarks>Change the descriptor toward what the composer builds: the finding names the knob and the value it was given. <c>GET /api/shapes/catalog</c> answers every form each family emits.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Plan, RuleConcern.Studio)]
    public const string Uncomposable = "CO1";
}
