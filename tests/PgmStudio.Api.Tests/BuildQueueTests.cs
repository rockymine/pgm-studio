using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using PgmStudio.Api.Services;

namespace PgmStudio.Api.Tests;

/// <summary>
/// The queue in front of the routes that build or render a world: the studio runs so many at once, a caller
/// runs one at a time and waits behind their own, another caller does not wait behind them, and a request that
/// finds the queue full or waits too long is refused <c>RQ11</c> at 429 rather than piling up.
/// </summary>
public sealed class BuildQueueTests
{
    private static BuildQueue Queue(int slots = 2, int perCaller = 1, int waiting = 8, int waitingPerCaller = 4,
                                    int maxWaitSeconds = 5) =>
        new(new BuildQueueOptions(slots, perCaller, waiting, waitingPerCaller, maxWaitSeconds));

    [Test]
    public async Task A_caller_waits_behind_their_own_request_and_another_caller_does_not()
    {
        var queue = Queue();
        var first = await queue.EnterAsync("a", default);
        await Assert.That(first).IsNotNull();

        var second = queue.EnterAsync("a", default);
        await Task.Delay(200);
        await Assert.That(second.IsCompleted).IsFalse().Because("a caller runs one queued request at a time");

        using var other = await queue.EnterAsync("b", default);
        await Assert.That(other).IsNotNull().Because("another caller takes the studio's second turn");

        first!.Dispose();
        using var turn = await second;
        await Assert.That(turn).IsNotNull();
    }

    [Test]
    public async Task The_studio_runs_no_more_than_its_turns_at_once()
    {
        var queue = Queue(slots: 1);
        var first = await queue.EnterAsync("a", default);
        var second = queue.EnterAsync("b", default);
        await Task.Delay(200);
        await Assert.That(second.IsCompleted).IsFalse();

        first!.Dispose();
        using var turn = await second;
        await Assert.That(turn).IsNotNull();
    }

    /// <summary>The queue holds so many waiting requests, and so many of one caller's; past either, a request is
    /// refused at once. One that waits longer than a request waits is refused when its time runs out.</summary>
    [Test]
    public async Task A_full_queue_and_a_wait_past_its_limit_are_refused()
    {
        var queue = Queue(slots: 1, waiting: 2, waitingPerCaller: 1, maxWaitSeconds: 1);
        using var running = await queue.EnterAsync("a", default);

        var waitingA = queue.EnterAsync("a", default);
        await Assert.That(await queue.EnterAsync("a", default)).IsNull().Because("a holds one waiting request already");
        var waitingB = queue.EnterAsync("b", default);
        await Assert.That(await queue.EnterAsync("c", default)).IsNull().Because("the queue holds two waiting requests");

        await Assert.That(await waitingA).IsNull().Because("it waited past the limit");
        await Assert.That(await waitingB).IsNull();
    }

    /// <summary>Over HTTP a refused request answers the refusal envelope under <c>RQ11</c> with a
    /// <c>Retry-After</c>, before its handler runs; a route that builds nothing is not queued at all.</summary>
    [Test]
    public async Task A_route_that_cannot_wait_its_turn_is_refused_RQ11_and_a_cheap_route_is_not_queued()
    {
        using var factory = new ClosedQueueFactory();
        using var client = factory.CreateClient();

        using var render = await client.GetAsync("/api/map/nowhere/render/topdown");
        await Assert.That(render.StatusCode).IsEqualTo(HttpStatusCode.TooManyRequests);
        await Assert.That(render.Headers.RetryAfter?.Delta).IsEqualTo(TimeSpan.FromSeconds(BuildQueue.RetryAfterSeconds));
        var finding = JsonDocument.Parse(await render.Content.ReadAsStringAsync()).RootElement.GetProperty("findings")[0];
        await Assert.That(finding.GetProperty("rule").GetString()).IsEqualTo("RQ11");

        using var health = await client.GetAsync("/api/health");
        await Assert.That(health.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    /// <summary>The studio with a queue that keeps nobody waiting, so every queued request is refused.</summary>
    private sealed class ClosedQueueFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PgmStudio"] = ApiTestFactory.ConnectionString,
                ["Access:Mode"] = "open",
                ["Builds:Slots"] = "1",
                ["Builds:Waiting"] = "0",
            }));
        }
    }
}
