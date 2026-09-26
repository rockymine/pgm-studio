using Microsoft.AspNetCore.Authorization;
using PgmStudio.Data.Map;

namespace PgmStudio.Api.Access;

/// <summary>
/// The three things a write can require, as named authorization policies. Which one a route takes is decided
/// in one place, <see cref="AccessRules"/>, rather than in each endpoint.
/// </summary>
public static class AccessPolicies
{
    /// <summary>Any person on the whitelist.</summary>
    public const string Member = "member";

    /// <summary>Someone who may change the map the route's <c>{slug}</c> names.</summary>
    public const string MapEditor = "map-editor";

    /// <summary>An admin.</summary>
    public const string Admin = "admin";

    public static void Add(AuthorizationOptions options)
    {
        foreach (var name in (string[])[Member, MapEditor, Admin])
            options.AddPolicy(name, policy => policy.AddRequirements(new AccessRequirement(name)));
    }
}

/// <summary>The requirement behind each of <see cref="AccessPolicies"/>, carrying the policy's name.</summary>
public sealed record AccessRequirement(string Policy) : IAuthorizationRequirement;

/// <summary>Decides an <see cref="AccessRequirement"/> against the caller the request resolves to. A caller
/// who is not signed in fails every one, which the pipeline answers as a challenge rather than a refusal.</summary>
public sealed class AccessHandler(Callers callers, MapRepository maps) : AuthorizationHandler<AccessRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, AccessRequirement requirement)
    {
        if (context.Resource is not HttpContext http) return;
        var caller = await callers.OfAsync(http, http.RequestAborted);
        if (!caller.Whitelisted) return;

        var granted = requirement.Policy switch
        {
            AccessPolicies.Member => true,
            AccessPolicies.Admin => caller.IsAdmin,
            // A slug no map is stored under is the route's own 404 to answer, not an access question.
            AccessPolicies.MapEditor =>
                await maps.GetBySlugAsync(http.Request.RouteValues["slug"] as string ?? "", http.RequestAborted)
                    is not { } map
                || await callers.MayEditAsync(caller, map, http.RequestAborted),
            _ => false,
        };
        if (granted) context.Succeed(requirement);
    }
}
