namespace PgmStudio.Contracts;

/// <summary>Cast or refine the judgment of one browse board (POST /api/compose/verdict). The
/// <paramref name="Descriptor"/> is the reproducible compose request — voting persists the plan (the feed is
/// ephemeral; a plan enters the database exactly when acted on), so the body carries the descriptor rather
/// than a plan id. <paramref name="Verdict"/> is <c>up</c> or <c>down</c>; <paramref name="Tags"/> and
/// <paramref name="Note"/> are optional annotation on either direction.</summary>
public sealed record VerdictSaveRequest(
    ComposeRequestDto Descriptor,
    string Verdict,
    IReadOnlyList<string>? Tags = null,
    string? Note = null);

/// <summary>One stored verdict: the plan it judges (with the descriptor to key browse cards by, and the
/// structural bucket key), the vote, its annotation, and the evaluator's reading at vote time.</summary>
public sealed record VerdictDto(
    long PlanId,
    string Verdict,
    IReadOnlyList<string> Tags,
    string? Note,
    double Score,
    string EvaluatorVersion,
    string? Structure,
    ComposeRequestDto? Descriptor,
    DateTime UpdatedAt);

/// <summary>The seeded annotation vocabulary for verdict tags — large toggleable pills in the UI,
/// multi-select, on either vote direction. Each tag carries the layout-rules id it indicts where one exists,
/// which is what turns a downvote into an evaluator bug report: a stored verdict tagged with a rule whose
/// term did not fire names the term that should have. The set is extendable — a tag is stored as its token,
/// so a new pill needs only a new row here.</summary>
public static class VerdictTags
{
    public static readonly IReadOnlyList<VerdictTag> Catalog =
    [
        new("wools-too-close", "Wools too close", "WL2"),
        new("wools-spawns-should-swap", "Wools/spawns should swap", null),
        new("flat-front", "Flat front", null),
        new("crammed-mid", "Crammed mid", null),
        new("no-rotation", "No rotation", null),
        new("great-hub", "Great hub", null),
    ];
}

/// <summary>One verdict tag: its stored token, its pill label, and the layout-rules id it indicts (null when
/// the complaint has no rule yet — itself a signal worth counting).</summary>
public sealed record VerdictTag(string Token, string Label, string? RuleId);
