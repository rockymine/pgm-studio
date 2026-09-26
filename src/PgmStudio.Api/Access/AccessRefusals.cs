using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using PgmStudio.Api.Endpoints;
using PgmStudio.Domain;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Access;

/// <summary>
/// A request the access rules turn away answers in the one refusal envelope, like every other gate:
/// <c>RQ7</c> at 401 for a caller who is not signed in, <c>RQ8</c> at 403 for one who is and may not.
/// Nothing redirects to a sign-in page; the caller is an API client, and the envelope is what it reads.
/// </summary>
public sealed class AccessRefusals : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler fallback = new();

    public Task HandleAsync(
        RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult result)
    {
        if (result.Succeeded || !context.Request.Path.StartsWithSegments("/api"))
            return fallback.HandleAsync(next, context, policy, result);

        return result.Challenged
            ? Refusals.WriteAsync(context, 401, "not signed in",
                [new Finding(RequestRules.SignedOut,
                    "this route writes, and writing needs someone on the studio's whitelist to be signed in")],
                context.RequestAborted)
            : Refusals.WriteAsync(context, 403, "not permitted",
                [new Finding(RequestRules.NotPermitted, Sentence(policy))], context.RequestAborted);
    }

    private static string Sentence(AuthorizationPolicy policy) =>
        policy.Requirements.OfType<AccessRequirement>().FirstOrDefault()?.Policy switch
        {
            AccessPolicies.MapEditor =>
                "only this map's owner, an author it credits, or an admin may change it",
            AccessPolicies.Admin => "only an admin may do this",
            _ => "only someone on the studio's whitelist may write",
        };
}
