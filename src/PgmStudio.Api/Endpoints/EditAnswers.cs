using Microsoft.AspNetCore.Http;
using PgmStudio.Api.Services;

namespace PgmStudio.Api.Endpoints;

/// <summary>
/// Rendering what an edit came to. <see cref="MapEdit"/> decides what happened and this decides how it is
/// said, which is the whole division between the two: the operation names a status and a gate, and only here
/// does that become a body and a header.
/// </summary>
internal static class EditAnswers
{
    /// <summary>The body the route sends, and the <c>ETag</c> beside it where the write landed — a caller
    /// that is not handed the revision it now holds writes unguarded next time with nothing saying so, which
    /// is why the header is written here rather than left to each route.</summary>
    public static object Body(this EditApplied applied, HttpContext http)
    {
        if (applied.Revision is { } revision) Revisions.Answer(http, revision);
        return applied.Refusal is { } refusal
            ? Refusals.Of(refusal.Error, refusal.Findings)
            : applied.Result!;
    }

    /// <summary>The status it is sent under: the refusal's own, or 200.</summary>
    public static int Status(this EditApplied applied) => applied.Refusal?.Status ?? 200;
}
