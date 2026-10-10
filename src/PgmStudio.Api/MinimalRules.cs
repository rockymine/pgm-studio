using PgmStudio.Api.Endpoints;
using PgmStudio.Domain;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api;

/// <summary>
/// The rules the minimal policy still enforces (<c>Rules:Mode=minimal</c>): what keeps a request readable, a
/// map loadable by PGM, its objectives sound, and its things physically apart. Every other rule answers as a
/// complaint, which is what <c>docs/refusals.md</c> § <i>The minimal policy</i> describes.
/// </summary>
public static class MinimalRules
{
    /// <summary>Categories whose every rule is about whether a request can be read or served at all.</summary>
    private static readonly HashSet<RuleCategory> Categories =
    [
        RuleCategory.Malformed, RuleCategory.Unknown, RuleCategory.Unfinished, RuleCategory.Forbidden,
        RuleCategory.Unavailable, RuleCategory.Internal,
    ];

    /// <summary>Families kept whole: requests, refinements, imports and the library.</summary>
    private static readonly HashSet<string> Families = ["RQ", "SR", "IM", "LB"];

    /// <summary>Rules kept from the categories the policy otherwise relaxes.</summary>
    private static readonly HashSet<string> Kept =
    [
        // a map PGM loads, with teams, spawns and objectives that work
        "EX2", "EX4", "EX5", "EX6", "PL1", "PL2", "PL3", "PL6", "PL8", "PL11", "PL15", "PL16", "SH1",
        "OB14", "OB17", "OB24", "OB25", "OB26", "OB27", "OB28", "OB29", "OB30", "OB31", "OB32", "OB33", "DC3",
        "ED2", "ED3", "ED4",
        // geometry that can be built at all
        "SK2", "SK4", "SK5", "SK7", "SK12", "SK21", "SK22", "SK23", "SK31", "PC-C", "PT3",
        // one thing never inside another: a house in a house, a tree in a house
        "SK18", "HJ1", "DR-CLAIM", "DR-CUT",
        // a house that can be stamped
        "HP1", "HP2", "HP3", "HJ2", "HJ3", "HJ4", "HJ5", "HS2", "HS8", "DR-SIZE", "DR-SITE",
        // a spawn or wool pad that fits its room
        "WX2", "WX3", "WX4",
    ];

    /// <summary>Every rule id the minimal policy enforces.</summary>
    public static IEnumerable<string> Enforced() =>
        RuleCatalog.Read(RulesEndpoint.Declaring)
            .Where(rule => rule.Category is not { } category || Categories.Contains(category)
                           || Families.Contains(rule.Family) || Kept.Contains(rule.Rule))
            .Select(rule => rule.Rule);
}
