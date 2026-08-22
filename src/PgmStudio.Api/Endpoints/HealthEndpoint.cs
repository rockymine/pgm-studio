using FastEndpoints;

namespace PgmStudio.Api.Endpoints;

/// <summary>Liveness probe — confirms the API host and FastEndpoints routing are up.</summary>
public sealed class HealthEndpoint : EndpointWithoutRequest<HealthResponse>
{
    public override void Configure()
    {
        Get("/health");
        AllowAnonymous();
    }

    public override Task HandleAsync(CancellationToken ct)
        => Send.OkAsync(new HealthResponse(), ct);
}

/// <summary>What a liveness probe reads: that something answered, and that it was this something.</summary>
public sealed class HealthResponse
{
    /// <summary>Always <c>ok</c> — a route that answers at all is the whole of the liveness claim, and a
    /// value here that varied would be a health check rather than a probe.</summary>
    public string Status { get; init; } = "ok";

    /// <summary>Which service answered, so a probe pointed at the wrong host says so rather than passing on
    /// somebody else's <c>ok</c>.</summary>
    public string Service { get; init; } = "pgm-studio";
}
