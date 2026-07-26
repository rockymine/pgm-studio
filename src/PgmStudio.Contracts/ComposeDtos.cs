namespace PgmStudio.Contracts;

/// <summary>The wire form of a generated plan's canonical versioned request descriptor — a browse card's
/// identity. Reproducible within a composer version: the server re-composes the exact plan from these fields
/// to pin or open it, so a card never has to carry its plan JSON.</summary>
public sealed record ComposeRequestDto(
    int Players,
    int Teams,
    string Symmetry,
    int Cell,
    ulong Seed,
    string ComposerVersion,
    int Schema);

/// <summary>One fired soft term's contribution to a board's score (weight × distance), for the detail
/// breakdown.</summary>
public sealed record TermContribDto(string TermId, string RuleId, double Contribution);

/// <summary>A board's structural read — the sieve/bucket vocabulary as display tokens: the sorted wool
/// approach families, the hub body form, and the frontline form (<c>none</c> when the unit has no frontline).
/// The tokens double as the card badges and the filter values.</summary>
public sealed record StructureSummaryDto(
    IReadOnlyList<string> Wools,
    string Hub,
    string Frontline);

/// <summary>One card in the browse feed: its <paramref name="Descriptor"/> (identity + reproduction key), the
/// evaluator <paramref name="Score"/> (lower is better), the base-unit <paramref name="WoolCount"/>, its
/// <paramref name="Structure"/> read (families/forms, for badges + filtering), any fired hard-term ids, the
/// top soft contributors, and the ready-to-inject board <paramref name="Svg"/>.</summary>
public sealed record ComposeCard(
    ComposeRequestDto Descriptor,
    double Score,
    int WoolCount,
    StructureSummaryDto Structure,
    IReadOnlyList<string> HardTerms,
    IReadOnlyList<TermContribDto> TopSoft,
    string Svg);

/// <summary>What the structural vocabulary actually turned up over the boards a page composed, counted
/// <b>before</b> the sieve so a filter never hides the alternatives it is filtering against.
/// <paramref name="Boards"/> is how many boards the counts are drawn from (composed, not matched).
///
/// <para>This is what makes a filter chip say something. Which forms a request can produce is a property of the
/// <em>request</em>, not a constant — it rides on the box sizes the land budget buys, so a small board never
/// reaches the wide hub forms at all. Rather than predict that, the feed reports what it saw, and a token
/// missing from a large enough sample is the honest version of "this request does not make one".</para></summary>
public sealed record ObservedForms(
    int Boards,
    IReadOnlyDictionary<string, int> Wools,
    IReadOnlyDictionary<string, int> Hubs,
    IReadOnlyDictionary<string, int> Frontlines);

/// <summary>A page of browse cards. <paramref name="NextSeed"/> is the seed cursor to resume from (feed
/// forward for infinite scroll); <paramref name="Exhausted"/> is true when the per-request scan budget was
/// reached before filling the page, so the client can stop requesting; <paramref name="Scanned"/> is how many
/// seeds this page composed (matched = Cards.Count) — under a strict structural filter the low match rate is
/// itself the signal to promote that filter to a held target. <paramref name="Observed"/> is the structural
/// census over those same boards, which the filter chips read to say what this request produces.</summary>
public sealed record ComposePage(
    IReadOnlyList<ComposeCard> Cards,
    int NextSeed,
    bool Exhausted,
    int Scanned,
    ObservedForms? Observed = null);
