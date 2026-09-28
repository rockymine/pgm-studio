using PgmStudio.Api.Access;
using PgmStudio.Api.Endpoints;
using PgmStudio.Domain;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Services;

/// <summary>
/// The route is seconds of CPU and a share of memory on the machine every caller shares — it builds a world,
/// renders one, or composes a feed of boards — so it waits its turn in the <see cref="BuildQueue"/> rather than
/// starting at once. Inherited, so a base class of render endpoints marks every render.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class QueuedAttribute : Attribute;

/// <summary>What the build queue allows, read from <c>Builds</c> in the configuration.</summary>
/// <param name="Slots">How many queued requests run at once on the whole studio.</param>
/// <param name="PerCaller">How many of those one caller runs at once — a signed-in person by their account, a
/// visitor by their address.</param>
/// <param name="Waiting">How many requests may wait for a turn on the whole studio.</param>
/// <param name="WaitingPerCaller">How many of those may be one caller's.</param>
/// <param name="MaxWaitSeconds">How long a request waits for its turn before it is refused.</param>
public sealed record BuildQueueOptions(
    int Slots = 3, int PerCaller = 1, int Waiting = 32, int WaitingPerCaller = 8, int MaxWaitSeconds = 60)
{
    public static BuildQueueOptions From(IConfiguration configuration) =>
        configuration.GetSection("Builds").Get<BuildQueueOptions>() ?? new();
}

/// <summary>
/// The queue in front of every <see cref="QueuedAttribute"/> route. A request takes its caller's turn, then a
/// turn on the studio, waiting for each without holding a thread; it runs, and gives both back when its
/// response is written. A caller over <see cref="BuildQueueOptions.PerCaller"/> waits behind their own
/// requests, so one caller cannot take every turn, and a request that finds the queue full or waits past
/// <see cref="BuildQueueOptions.MaxWaitSeconds"/> is refused <see cref="RequestRules.Busy"/> at 429.
/// </summary>
public sealed class BuildQueue(BuildQueueOptions options)
{
    private readonly SemaphoreSlim slots = new(Math.Max(1, options.Slots));
    private readonly Dictionary<string, Lane> lanes = new(StringComparer.Ordinal);
    private int waiting;

    private sealed class Lane(int turns)
    {
        public readonly SemaphoreSlim Turns = new(turns);
        public int Users;
        public int Waiting;
    }

    /// <summary>A turn held: disposing it gives the caller's turn and the studio's back.</summary>
    public sealed class Turn(Action release) : IDisposable
    {
        private int released;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref released, 1) == 0) release();
        }
    }

    /// <summary>Wait for <paramref name="caller"/>'s turn, or null where the queue is full or the wait ran
    /// out.</summary>
    public async Task<Turn?> EnterAsync(string caller, CancellationToken ct)
    {
        Lane lane;
        lock (lanes)
        {
            if (waiting >= options.Waiting) return null;
            if (!lanes.TryGetValue(caller, out lane!)) lanes[caller] = lane = new Lane(Math.Max(1, options.PerCaller));
            if (lane.Waiting >= options.WaitingPerCaller)
            {
                if (lane.Users == 0) lanes.Remove(caller);
                return null;
            }
            lane.Users++;
            lane.Waiting++;
            waiting++;
        }

        var deadline = TimeSpan.FromSeconds(Math.Max(1, options.MaxWaitSeconds));
        var started = DateTime.UtcNow;
        var ownTurn = false;
        var studioTurn = false;
        try
        {
            ownTurn = await lane.Turns.WaitAsync(deadline, ct);
            if (ownTurn) studioTurn = await slots.WaitAsync(Remaining(deadline, started), ct);
        }
        finally
        {
            lock (lanes)
            {
                lane.Waiting--;
                waiting--;
            }
            if (!studioTurn)
            {
                if (ownTurn) lane.Turns.Release();
                Leave(caller, lane);
            }
        }
        return studioTurn ? new Turn(() => { slots.Release(); lane.Turns.Release(); Leave(caller, lane); }) : null;
    }

    private void Leave(string caller, Lane lane)
    {
        lock (lanes)
        {
            if (--lane.Users == 0) lanes.Remove(caller);
        }
    }

    private static TimeSpan Remaining(TimeSpan deadline, DateTime started)
    {
        var left = deadline - (DateTime.UtcNow - started);
        return left > TimeSpan.Zero ? left : TimeSpan.Zero;
    }

    /// <summary>Queue a request to a <see cref="QueuedAttribute"/> route, answering it
    /// <see cref="RequestRules.Busy"/> where it cannot have a turn. Runs after authorization, so a request the
    /// access rules refuse never takes one.</summary>
    public static async Task QueueAsync(HttpContext http, RequestDelegate next)
    {
        if (http.GetEndpoint()?.Metadata.GetMetadata<QueuedAttribute>() is null)
        {
            await next(http);
            return;
        }

        var queue = http.RequestServices.GetRequiredService<BuildQueue>();
        var caller = await http.RequestServices.GetRequiredService<Callers>().OfAsync(http, http.RequestAborted);
        var key = caller.Uuid is { } uuid ? $"account:{uuid}"
            : caller.Role is not null ? "local"
            : $"address:{http.Connection.RemoteIpAddress}";

        using var turn = await queue.EnterAsync(key, http.RequestAborted);
        if (turn is null)
        {
            http.Response.Headers.RetryAfter = RetryAfterSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
            await Refusals.WriteAsync(http, 429, "busy",
                [new Finding(RequestRules.Busy,
                    "the studio is running as many builds as it runs at once and this request could not wait its "
                    + $"turn — try again in {RetryAfterSeconds} seconds")], http.RequestAborted);
            return;
        }
        await next(http);
    }

    /// <summary>What a refused request is told to wait before asking again.</summary>
    public const int RetryAfterSeconds = 10;

    /// <summary>Mark a <see cref="QueuedAttribute"/> endpoint for the queue, and publish the 429 it may
    /// answer. Applied to every endpoint beside <see cref="AccessRules"/>.</summary>
    public static void Apply(FastEndpoints.EndpointDefinition endpoint)
    {
        if (!endpoint.EndpointType.IsDefined(typeof(QueuedAttribute), inherit: true)) return;
        endpoint.Description(b => b
            .WithMetadata(new QueuedAttribute())
            .Produces<Contracts.RefusalDto>(429, "application/json"));
    }
}
