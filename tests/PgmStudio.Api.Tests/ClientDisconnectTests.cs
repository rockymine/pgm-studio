using Microsoft.AspNetCore.Http;
using PgmStudio.Api.Http;

namespace PgmStudio.Api.Tests;

/// <summary>
/// What separates a caller hanging up from a fault. The unhandled-fault middleware sees both as an exception,
/// and the discrimination is the whole of what makes one silent — so what is pinned here is that it takes
/// both halves, since either alone is a real fault the log has to keep.
/// </summary>
public sealed class ClientDisconnectTests
{
    private static HttpContext Aborted()
    {
        var http = new DefaultHttpContext();
        var source = new CancellationTokenSource();
        source.Cancel();
        http.RequestAborted = source.Token;
        return http;
    }

    private static HttpContext Live() => new DefaultHttpContext { RequestAborted = CancellationToken.None };

    [Test]
    public async Task A_cancellation_on_an_aborted_request_is_the_caller_going_away()
    {
        await Assert.That(ClientDisconnect.Explains(Aborted(), new OperationCanceledException())).IsTrue();
    }

    [Test]
    public async Task The_cancellation_a_client_throws_counts_too()
    {
        // What HttpClient raises when its own token trips — a subtype, and the same event.
        await Assert.That(ClientDisconnect.Explains(Aborted(), new TaskCanceledException())).IsTrue();
    }

    [Test]
    public async Task A_cancellation_on_a_live_request_is_the_studio_giving_up_and_stays_a_fault()
    {
        // A server-side timeout or a linked source throws the same exception while the caller is still
        // waiting for an answer. Reading the exception alone would silence it.
        await Assert.That(ClientDisconnect.Explains(Live(), new OperationCanceledException())).IsFalse();
    }

    [Test]
    public async Task A_defect_that_coincides_with_a_disconnect_is_still_a_defect()
    {
        // Reading the request alone would silence every fault that raced a caller closing the tab.
        await Assert.That(ClientDisconnect.Explains(Aborted(), new InvalidOperationException("nope"))).IsFalse();
    }
}
