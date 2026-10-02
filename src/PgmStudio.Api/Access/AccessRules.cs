using FastEndpoints;
using PgmStudio.Contracts;

namespace PgmStudio.Api.Access;

/// <summary>
/// Which of <see cref="AccessPolicies"/> each route takes, decided from the route itself so that no endpoint
/// states its own access and none can forget to.
///
/// <list type="bullet">
/// <item>A <see cref="CostlyReadAttribute"/> route builds what it answers, so it needs a person on the whitelist
/// and nothing more, whichever verb it takes and whichever map it names.</item>
/// <item>Any other read — <c>GET</c> or <c>HEAD</c> — is open to anyone.</item>
/// <item>A write to a route under <c>{slug}</c> changes that map, so it needs someone who may edit it.</item>
/// <item>A <c>DELETE</c> of anything else removes a shared library row other maps may use, so it needs an
/// admin.</item>
/// <item>Every other write needs a person on the whitelist.</item>
/// </list>
///
/// An endpoint that states its own access keeps it: the whitelist's routes are an admin's, list included, and
/// signing out is anyone's.
/// </summary>
public static class AccessRules
{
    public static void Apply(EndpointDefinition endpoint)
    {
        if (endpoint.AnonymousVerbs is { Length: > 0 }) return;
        if (endpoint.PreBuiltUserPolicies is not { Count: > 0 })
        {
            if (endpoint.Verbs.All(IsRead) && !IsCostly(endpoint))
            {
                endpoint.AllowAnonymous();
                return;
            }
            endpoint.Policies(PolicyOf(endpoint));
        }
        endpoint.Description(b => b
            .Produces<RefusalDto>(401, "application/json")
            .Produces<RefusalDto>(403, "application/json"));
    }

    private static bool IsCostly(EndpointDefinition endpoint) =>
        endpoint.EndpointType.IsDefined(typeof(CostlyReadAttribute), inherit: false);

    private static string PolicyOf(EndpointDefinition endpoint) =>
        IsCostly(endpoint) ? AccessPolicies.Member
        : endpoint.Routes.Any(route => route.Contains("{slug}")) ? AccessPolicies.MapEditor
        : endpoint.Verbs.Contains("DELETE", StringComparer.OrdinalIgnoreCase) ? AccessPolicies.Admin
        : AccessPolicies.Member;

    private static bool IsRead(string verb) =>
        verb.Equals("GET", StringComparison.OrdinalIgnoreCase) || verb.Equals("HEAD", StringComparison.OrdinalIgnoreCase);
}
