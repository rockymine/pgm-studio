using System.Reflection;
using FastEndpoints;
using PgmStudio.Contracts;
using PgmStudio.Domain;
using PgmStudio.Export;
using PgmStudio.Minecraft;
using PgmStudio.Pgm.Plan;
using PgmStudio.Minecraft.Houses;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Endpoints;

/// <summary>
/// <c>GET /api/rules</c> — <b>every rule the studio can cite, with what it means and what to do about it.</b>
///
/// <para>A refusal carries an id and one sentence about the document it was refused over. The id is stable
/// forever and outlives the task that added it, which is what makes it worth keying on — and this is what
/// answers the other question a reader has on meeting one: <i>what is <c>SP7</c></i>. The ids live in eight
/// <c>*Rules</c> classes across five projects, so a reader who does not already know which family an id
/// belongs to has one place to ask rather than eight to search.</para>
///
/// <para>Filter with <c>?family=PL</c> for one family, <c>?rule=SP7</c> for one rule,
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
    public override void Configure() { Get("/rules"); AllowAnonymous(); }

    /// <summary>The assemblies holding rule ids, named rather than discovered. An assembly nothing has touched
    /// yet is not loaded, so sweeping <c>AppDomain</c> would drop a family depending on what the process
    /// happened to do first; naming a type in each one forces the load and makes the list checkable by eye.
    /// A new <c>*Rules</c> class in a project not listed here is caught by <c>RuleCatalogTests</c>.</summary>
    private static readonly Assembly[] Declaring =
    [
        typeof(ObjectiveRules).Assembly,        // Domain
        typeof(PlanRules).Assembly,             // Pgm
        typeof(WingJointRules).Assembly,        // Minecraft
        typeof(MapExportComposer).Assembly,     // Export
        typeof(RulesEndpoint).Assembly,         // Api
    ];

    private static readonly List<RuleDto> Catalog =
    [
        .. RuleCatalog.Read(Declaring)
            .Select(rule => new RuleDto(
                rule.Rule, rule.Family, rule.Owner, rule.Means, rule.Fix, rule.Evidence,
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
                .Where(row => concerns.All(concern => row.Concerns?.Contains(concern) ?? false)),
        ], ct);
    }

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
                $"'{parameter}' is one of "
                + string.Join(", ", Enum.GetNames<TWord>().Select(name => name.ToLowerInvariant())),
                Field: parameter)],
            ct);
}
