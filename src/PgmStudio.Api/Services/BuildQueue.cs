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
/// requests, so one caller cannot take every turn. A request that finds the queue full is refused
/// <see cref="RequestRules.QueueFull"/>, and one that waits past <see cref="BuildQueueOptions.MaxWaitSeconds"/>
/// <see cref="RequestRules.Busy"/>, both at 429.
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

    /// <summary>A turn, or the rule a request that has none is refused under.</summary>
    public readonly record struct Entry(Turn? Turn, string? Refused);

    /// <summary>Wait for <paramref name="caller"/>'s turn, or null where the queue is full or the wait ran
    /// out.</summary>
    public async Task<Turn?> EnterAsync(string caller, CancellationToken ct) => (await TryEnterAsync(caller, ct)).Turn;

    /// <summary>Wait for <paramref name="caller"/>'s turn, or answer why there is none:
    /// <see cref="RequestRules.QueueFull"/> where the queue is full, <see cref="RequestRules.Busy"/> where the
    /// wait ran out.</summary>
    public async Task<Entry> TryEnterAsync(string caller, CancellationToken ct)
    {
        Lane lane;
        lock (lanes)
        {
            if (waiting >= options.Waiting) return new(null, RequestRules.QueueFull);
            if (!lanes.TryGetValue(caller, out lane!)) lanes[caller] = lane = new Lane(Math.Max(1, options.PerCaller));
            if (lane.Waiting >= options.WaitingPerCaller)
            {
                if (lane.Users == 0) lanes.Remove(caller);
                return new(null, RequestRules.QueueFull);
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
        return studioTurn
            ? new(new Turn(() => { slots.Release(); lane.Turns.Release(); Leave(caller, lane); }), null)
            : new(null, RequestRules.Busy);
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

        var entry = await http.RequestServices.GetRequiredService<BuildQueue>().TurnOfAsync(http);
        using var turn = entry.Turn;
        if (turn is null)
        {
            await RefuseBusyAsync(http, entry);
            return;
        }
        await next(http);
    }

    /// <summary>Wait for the turn of the caller behind <paramref name="http"/> — a signed-in person by their
    /// account, a visitor by their address — or answer why they cannot have one. For a route that builds on
    /// only some of its requests, which takes a turn where it does rather than being <see cref="QueuedAttribute"/>.</summary>
    public async Task<Entry> TurnOfAsync(HttpContext http)
    {
        var caller = await http.RequestServices.GetRequiredService<Callers>().OfAsync(http, http.RequestAborted);
        var key = caller.Uuid is { } uuid ? $"account:{uuid}"
            : caller.Role is not null ? "local"
            : $"address:{http.Connection.RemoteIpAddress}";
        return await TryEnterAsync(key, http.RequestAborted);
    }

    /// <summary>Answer a request that could not have a turn: 429, the rule its entry was refused under, and
    /// when to ask again.</summary>
    public static async Task RefuseBusyAsync(HttpContext http, Entry entry)
    {
        http.Response.Headers.RetryAfter = RetryAfterSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
        await Refusals.WriteAsync(http, 429, "busy",
            [entry.Refused == RequestRules.QueueFull
                ? new Finding(RequestRules.QueueFull, "the build queue holds as many requests as it takes")
                : new Finding(RequestRules.Busy, "the build queue gave the request no turn")], http.RequestAborted);
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
