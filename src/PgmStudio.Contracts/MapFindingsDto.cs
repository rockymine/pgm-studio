using System.Text.Json.Serialization;
using PgmStudio.Vocabulary;

namespace PgmStudio.Contracts;

/// <summary>
/// A gate this read did not ask, and where to ask it.
///
/// <para>Naming it is the whole point. A findings list that is silent about what it skipped reads as "nothing
/// is wrong", and a driver acts on that; one that says which gate it did not reach, why, and which route pays
/// for the answer is a complete answer to a bounded question.</para>
/// </summary>
/// <param name="Gate">What the gate judges, in the words its rules use — <c>traversability</c>,
/// <c>objective placement</c>.</param>
/// <param name="Why">What it needs that a read does not have.</param>
/// <param name="Ask">The route that does pay for it.</param>
public sealed record UnaskedGate(string Gate, string Why, string Ask);

/// <summary>
/// Everything wrong with a map right now, as far as its stored documents can say.
///
/// <para>Every other gate in the studio is reached through the step it lives behind, so a fault authored at
/// one step is heard at another — a driver's loop is <em>act and hope the next call mentions it</em> rather
/// than <em>act, then ask</em>. This is the asking. It calls the gates rather than restating them, since a
/// summary that re-implements one is a second copy free to disagree with it.</para>
///
/// <para><b>A read does not pay for a build.</b> Which gates can answer is decided by which documents the map
/// holds, and every gate needing the rasterized world is named in <see cref="Unasked"/> rather than costing
/// seconds on a call that wanted the plan.</para>
/// </summary>
/// <param name="Stage">Where the map has got to, which is what decides how much of the list can be
/// answered.</param>
public sealed record MapFindingsDto(
    string Stage,
    IReadOnlyList<Finding> Findings,
    IReadOnlyList<UnaskedGate> Unasked)
{
    /// <summary>Whether anything here stops the work — the same question <c>Findings.Refuses</c> answers, so a
    /// caller reads it by the same name it reads every other gate by rather than counting.</summary>
    [JsonPropertyName("refuses")]
    public bool Refuses => Findings.Any(finding => finding.Refuses);
}
