using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PgmStudio.Api.Tests;

/// <summary>M0 smoke test: the host boots, FastEndpoints routing is live, and /api/health responds.</summary>
[NotInParallel("api-db")]
public sealed class HealthEndpointTests
{
    [Test]
    public async Task Health_returns_ok()
    {
        await using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();

        var resp = await client.GetAsync("/api/health");

        await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body = await resp.Content.ReadFromJsonAsync<HealthDto>();
        await Assert.That(body).IsNotNull();
        await Assert.That(body!.Status).IsEqualTo("ok");
        await Assert.That(body.Service).IsEqualTo("pgm-studio");
    }

    private sealed record HealthDto(string Status, string Service);
}
