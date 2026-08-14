using PgmStudio.Minecraft;

namespace PgmStudio.Api.Endpoints;

/// <summary>The 400 a house-style save answers with when <see cref="HouseStyleValidation"/> finds something
/// wrong — shared by the room, roof and storey style endpoints, whose success responses are each a different
/// DTO and so cannot carry this shape through <c>Send.ResponseAsync</c>, which is typed to the endpoint's own
/// response.</summary>
internal static class HouseStyleGateResponse
{
    public static Task WriteAsync(
        HttpContext http, IReadOnlyList<HouseStyleFinding> findings, CancellationToken ct)
    {
        http.Response.StatusCode = 400;
        return http.Response.WriteAsJsonAsync(new { error = "invalid house style", findings }, ct);
    }
}
