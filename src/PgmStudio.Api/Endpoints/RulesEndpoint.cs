using System.Reflection;
using FastEndpoints;
using PgmStudio.Contracts;
using PgmStudio.Domain;
using PgmStudio.Export;
using PgmStudio.Minecraft;
using PgmStudio.Pgm.Plan;

namespace PgmStudio.Api.Endpoints;

/// <summary>
/// <c>GET /api/rules</c> — <b>every rule the studio can cite, with what it means and what to do about it.</b>
///
/// <para>A refusal carries an id and one sentence about the document it was refused over. The id is stable
/// forever and outlives the task that added it, which is what makes it worth keying on — and until now
/// nothing answered the other question a reader has on meeting one: <i>what is <c>SP7</c></i>. The ids were
/// spread across eight <c>*Rules</c> classes and one 644-line document, and a reader who did not already know
/// which family an id belonged to had nowhere to start.</para>
///
/// <para>Filter with <c>?family=PL</c> for one family, or <c>?rule=SP7</c> for one rule. Both are matched
/// case-insensitively and neither is an error when it matches nothing — an empty list is the honest answer to
/// "is there a rule called that", and answering 404 would make a caller tell an absent rule from a typo by the
/// status code.</para>
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
            .Select(rule => new RuleDto(rule.Rule, rule.Family, rule.Owner, rule.Means, rule.Fix, rule.Evidence)),
    ];

    public override Task HandleAsync(CancellationToken ct)
    {
        var family = Query<string>("family", isRequired: false);
        var rule = Query<string>("rule", isRequired: false);

        return Send.OkAsync(
        [
            .. Catalog
                .Where(row => family is null || string.Equals(row.Family, family, StringComparison.OrdinalIgnoreCase))
                .Where(row => rule is null || string.Equals(row.Rule, rule, StringComparison.OrdinalIgnoreCase)),
        ], ct);
    }
}
