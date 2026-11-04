using System.Net;

namespace PgmStudio.Client.Components;

/// <summary>
/// The handler under the client's one <see cref="HttpClient"/> that notices a session ending while a page is
/// open: any request the server answers 401 — <c>RQ7</c>, signed in as nobody — raises <see cref="Ended"/>, which
/// <see cref="StudioAccess"/> takes as the caller it holds no longer being true.
/// </summary>
public sealed class SessionWatch : DelegatingHandler
{
    public SessionWatch() => InnerHandler = new HttpClientHandler();

    /// <summary>Raised after a response that says the request was signed in as nobody.</summary>
    public event Action? Ended;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized) Ended?.Invoke();
        return response;
    }
}
