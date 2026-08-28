namespace PgmStudio.Api.Http;

/// <summary>
/// Whether a fault that reached the unhandled-fault middleware is the caller having hung up rather than the
/// studio having failed. A request the client abandons cancels <see cref="HttpContext.RequestAborted"/>, every
/// endpoint takes that token, and whatever it was awaiting throws — so the exception looks exactly like a
/// defect while describing something the studio did nothing wrong in.
///
/// <para>The sketch tool produces these deliberately: its autosave cancels the previous in-flight PUT on every
/// edit, so a drag that outruns one save aborts it. The write is inside a transaction that never commits, so
/// the document is untouched and the next save carries the newer state.</para>
///
/// <para><b>The token is the whole test.</b> A cancellation alone says nothing — a server-side timeout throws
/// the same exception and <em>is</em> worth reporting — so what separates them is whether the request itself
/// was aborted. Both halves have to hold: a genuine defect that happens to coincide with a disconnect is
/// still a defect.</para>
/// </summary>
public static class ClientDisconnect
{
    /// <summary>Whether <paramref name="fault"/> is <paramref name="http"/>'s caller going away.</summary>
    public static bool Explains(HttpContext http, Exception fault)
        => fault is OperationCanceledException && http.RequestAborted.IsCancellationRequested;
}
