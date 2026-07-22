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

/// <summary>One card in the browse feed: its <paramref name="Descriptor"/> (identity + reproduction key), the
/// evaluator <paramref name="Score"/> (lower is better), the base-unit <paramref name="WoolCount"/>, any fired
/// hard-term ids, the top soft contributors, and the ready-to-inject board <paramref name="Svg"/>.</summary>
public sealed record ComposeCard(
    ComposeRequestDto Descriptor,
    double Score,
    int WoolCount,
    IReadOnlyList<string> HardTerms,
    IReadOnlyList<TermContribDto> TopSoft,
    string Svg);

/// <summary>A page of browse cards. <paramref name="NextSeed"/> is the seed cursor to resume from (feed
/// forward for infinite scroll); <paramref name="Exhausted"/> is true when the scan cap was reached before
/// filling the page, so the client can stop requesting.</summary>
public sealed record ComposePage(
    IReadOnlyList<ComposeCard> Cards,
    int NextSeed,
    bool Exhausted);
