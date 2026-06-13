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

public sealed class HealthResponse
{
    public string Status { get; init; } = "ok";
    public string Service { get; init; } = "pgm-studio";
}
