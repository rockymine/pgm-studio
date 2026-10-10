using System.Reflection;
using FastEndpoints;
using PgmStudio.Contracts;
using PgmStudio.Domain;
using PgmStudio.Analysis.Playability;
using PgmStudio.Export;
using PgmStudio.Minecraft;
using PgmStudio.Pgm.Plan;
using PgmStudio.Minecraft.Houses;
using PgmStudio.Vocabulary;
using PgmStudio.Api.Access;
using PgmStudio.Data.Access;

namespace PgmStudio.Api.Endpoints;

/// <summary>
/// <c>GET /api/rules</c> — <b>every rule the studio can cite, with what it means and what to do about it.</b>
///
/// <para>A refusal carries an id and one sentence about the document it was refused over. The id is stable
/// forever and outlives the task that added it, which is what makes it worth keying on — and this is what
/// answers the other question a reader has on meeting one: <i>what is <c>WL2</c></i>. The ids live in eight
/// <c>*Rules</c> classes across five projects, so a reader who does not already know which family an id
/// belongs to has one place to ask rather than eight to search.</para>
///
/// <para>Filter with <c>?family=PL</c> for one family, <c>?rule=WL2</c> for one rule,
/// <c>?category=unplayable</c> for every rule a caller would answer the same way, or <c>?concerns=objective</c>
/// for every rule that touches one. <c>concerns</c> may be repeated and <b>narrows</b>:
/// <c>?concerns=objective&amp;concerns=plan</c> answers the rules that are about both, which is how the
/// escalation <c>refusals.md</c> § <i>One question, asked at every grain</i> states in prose becomes a query.
/// All four are matched case-insensitively and none is an error when it matches nothing — an empty list is the
/// honest answer to "is there a rule called that", and answering 404 would make a caller tell an absent rule
/// from a typo by the status code. A word that is not in the closed set is <c>RQ1</c>, because a caller that
/// mistyped a category would otherwise read an empty list as "no rules do that".</para>
///
/// <para>Anonymous and map-independent: a rule is a property of the studio, not of anything stored in it.</para>
/// </summary>
public sealed class RulesEndpoint : EndpointWithoutRequest<List<RuleDto>>
{
    public override void Configure()
    {
        Get("/rules");
        Description(b => b.Reads(
            new QueryWord("family", "Only the rules of one family, by its letters — `WL`, `SR`. Absent is every "
                + "family."),
            new QueryWord("rule", "One rule, by its id — `WL2`."),
            new QueryWord("category", "Only the rules a caller answers the same way.",
                [.. Enum.GetNames<RuleCategory>().Select(Camel)]),
            new QueryWord("concerns", "Only the rules about this. Repeated, it narrows: the rules about every one "
                + "named.", [.. Enum.GetNames<RuleConcern>().Select(Camel)])));
    }

    private static string Camel(string name) => char.ToLowerInvariant(name[0]) + name[1..];

    /// <summary>The assemblies holding rule ids, named rather than discovered. An assembly nothing has touched
    /// yet is not loaded, so sweeping <c>AppDomain</c> would drop a family depending on what the process
    /// happened to do first; naming a type in each one forces the load and makes the list checkable by eye.
    /// A new <c>*Rules</c> class in a project not listed here is caught by <c>RuleCatalogTests</c>.</summary>
    internal static readonly Assembly[] Declaring =
    [
        typeof(ObjectiveRules).Assembly,        // Domain
        typeof(PlanRules).Assembly,             // Pgm
        typeof(WingJointRules).Assembly,        // Minecraft
        typeof(MapExportComposer).Assembly,     // Export
        typeof(ReliefRules).Assembly,           // Analysis
        typeof(RulesEndpoint).Assembly,         // Api
    ];

    private static readonly List<RuleDto> Catalog =
    [
        .. RuleCatalog.Read(Declaring)
            .Select(rule => new RuleDto(
                rule.Rule, rule.Family, rule.Owner, rule.Means, rule.Fix,
                rule.Category, rule.Concerns is { Count: > 0 } about ? about : null)),
    ];

    public override async Task HandleAsync(CancellationToken ct)
    {
        var family = Query<string>("family", isRequired: false);
        var rule = Query<string>("rule", isRequired: false);

        if (!Word<RuleCategory>("category", out var category)) { await Mistyped<RuleCategory>("category", ct); return; }

        var concerns = new List<RuleConcern>();
        foreach (var asked in HttpContext.Request.Query["concerns"])
        {
            if (!Enum.TryParse<RuleConcern>(asked, ignoreCase: true, out var concern))
            {
                await Mistyped<RuleConcern>("concerns", ct);
                return;
            }
            concerns.Add(concern);
        }

        await Send.OkAsync(
        [
            .. Catalog
                .Where(row => family is null || string.Equals(row.Family, family, StringComparison.OrdinalIgnoreCase))
                .Where(row => rule is null || string.Equals(row.Rule, rule, StringComparison.OrdinalIgnoreCase))
                .Where(row => category is null || row.Category == category)
                .Where(row => concerns.All(concern => row.Concerns?.Contains(concern) ?? false))
                .Select(Leveled),
        ], ct);
    }

    /// <summary>Every rule id the studio declares.</summary>
    internal static readonly HashSet<string> Ids = [.. Catalog.Select(row => row.Rule)];

    /// <summary>One rule as this studio answers it, carrying the level a setting states for it.</summary>
    internal static RuleDto Leveled(RuleDto rule) =>
        RulePolicy.LevelOf(rule.Rule) is { } level ? rule with { Level = level.Level, LevelSource = level.Source } : rule;

    /// <summary>The catalogue's row for <paramref name="rule"/>, as this studio answers it.</summary>
    internal static RuleDto? Find(string rule) =>
        Catalog.FirstOrDefault(row => row.Rule == rule) is { } found ? Leveled(found) : null;

    /// <summary>Read a closed-set query parameter. False when one was asked for and is not a word of the set;
    /// true with <paramref name="word"/> null when it was not asked for at all.</summary>
    private bool Word<TWord>(string parameter, out TWord? word) where TWord : struct, Enum
    {
        word = null;
        if (Query<string>(parameter, isRequired: false) is not { } asked) return true;
        if (!Enum.TryParse<TWord>(asked, ignoreCase: true, out var parsed)) return false;
        word = parsed;
        return true;
    }

    /// <summary>A closed-set parameter that is not one of the words. It is <c>RQ1</c> at 400 rather than an
    /// empty list, because a caller that mistyped a category would read an empty list as "no rules do that".
    /// The sentence names the whole set, spelled the way the answer spells it.</summary>
    private Task Mistyped<TWord>(string parameter, CancellationToken ct) where TWord : struct, Enum =>
        Refusals.WriteAsync(HttpContext, 400, $"unknown {parameter}",
            [new Finding(
                RequestRules.Unreadable,
                $"the request's `{parameter}` is not one of "
                + string.Join(", ", Enum.GetNames<TWord>().Select(name => name.ToLowerInvariant())),
                Field: parameter)],
            ct);
}

/// <summary>
/// PUT /api/rules/{rule}/level — set what a finding citing <c>{rule}</c> does on this studio: <c>refuse</c> stops
/// the work wherever a gate asks, <c>hint</c> lets it go ahead with the finding beside it. The setting is stored
/// and wins over the configuration and the mode. Admin only; answers the rule as it now stands.
/// </summary>
public sealed class RuleLevelPutEndpoint(RuleLevelStore levels, Callers callers) : Endpoint<RuleLevelRequest, RuleDto>
{
    public override void Configure()
    {
        Put("/rules/{rule}/level");
        Policies(AccessPolicies.Admin);
        Description(b => b.Refuses(400, 404));
    }

    public override async Task HandleAsync(RuleLevelRequest request, CancellationToken ct)
    {
        var rule = Route<string>("rule") ?? "";
        if (!RulesEndpoint.Ids.Contains(rule))
        {
            await RuleLevelRefusals.NoRuleAsync(HttpContext, rule, ct);
            return;
        }
        if (!RuleLevels.IsValid(request.Level))
        {
            await Refusals.UnreadableAsync(HttpContext, "no such level",
                $"the request's `level` '{request.Level}' is not one of {string.Join(", ", RuleLevels.All)}",
                ct, field: "level");
            return;
        }
        var caller = await callers.OfAsync(HttpContext, ct);
        await levels.PutAsync(rule, request.Level, caller.Name ?? "local", ct);
        RulePolicy.Store(await levels.AllAsync(ct));
        await Send.OkAsync(RulesEndpoint.Find(rule)!, ct);
    }
}

/// <summary>
/// DELETE /api/rules/{rule}/level — take an admin's level off <c>{rule}</c>, so it does what the configuration, or
/// its code, says. Admin only; answers the rule as it now stands, whether or not it had a level.
/// </summary>
public sealed class RuleLevelRemoveEndpoint(RuleLevelStore levels) : EndpointWithoutRequest<RuleDto>
{
    public override void Configure()
    {
        Delete("/rules/{rule}/level");
        Policies(AccessPolicies.Admin);
        Description(b => b.Refuses(404));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var rule = Route<string>("rule") ?? "";
        if (!RulesEndpoint.Ids.Contains(rule))
        {
            await RuleLevelRefusals.NoRuleAsync(HttpContext, rule, ct);
            return;
        }
        await levels.RemoveAsync(rule, ct);
        RulePolicy.Store(await levels.AllAsync(ct));
        await Send.OkAsync(RulesEndpoint.Find(rule)!, ct);
    }
}

internal static class RuleLevelRefusals
{
    public static Task NoRuleAsync(HttpContext http, string rule, CancellationToken ct) =>
        Refusals.WriteAsync(http, 404, "no rule",
            [new Finding(RequestRules.NoSuchSubject, $"rule '{rule}' is not a rule the studio declares", Field: "rule")],
            ct);
}
