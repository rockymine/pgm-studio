using Microsoft.Extensions.DependencyInjection;
using PgmStudio.Api.Services;
using PgmStudio.Data.Map;

namespace PgmStudio.Api.Tests;

/// <summary>
/// What stands in front of Mojang. Two things do, and each of them answers a question that would otherwise
/// cost a request: the shape of the name, and what is already known (<c>C45</c>).
///
/// <para>Every test here proves a request was <b>not</b> made, by handing the client a handler that fails the
/// test if it is reached. That is also the reading of the cache the author asked for — the same handful of
/// people are typed constantly, and after the first time the API is not called.</para>
/// </summary>
[NotInParallel("api-db")]
public sealed class PlayerLookupTests
{
    private const string NotchUuid = "069a79f4-44e9-4726-a5be-fca90e38aaf5";

    /// <summary>A handler that never answers: reaching it is the failure the test is looking for.</summary>
    private sealed class Unreachable : HttpMessageHandler
    {
        public bool WasAsked { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            WasAsked = true;
            throw new InvalidOperationException($"Mojang was asked for {request.RequestUri}");
        }
    }

    private static (PlayerLookup Lookup, Unreachable Http, PlayerNameStore Kept) Offline(IServiceScope scope)
    {
        var handler = new Unreachable();
        var mojang = new MojangClient(new HttpClient(handler));
        var kept = scope.ServiceProvider.GetRequiredService<PlayerNameStore>();
        return (new PlayerLookup(mojang, kept), handler, kept);
    }

    /// <summary>A name no account could carry is a pseudonym before a request is made. Mojang is not asked
    /// what <c>Opus 5</c>'s uuid is, because no Minecraft account is called that — the space alone settles
    /// it.</summary>
    [Test]
    [Arguments("Opus 5")]
    [Arguments("Haiku 4.5")]
    [Arguments("ab")]
    [Arguments("Jean-Luc")]
    public async Task A_name_that_is_not_shaped_like_an_account_is_never_asked_of_Mojang(string name)
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var _ = ApiTestFactory.Shared.CreateClient();
        using var scope = ApiTestFactory.Shared.Services.CreateScope();
        var (lookup, http, _) = Offline(scope);

        await Assert.That(await lookup.ResolveAsync(name)).IsNull();
        await Assert.That(http.WasAsked).IsFalse();
    }

    /// <summary>And a name that is already known is answered from what is known. This is the whole of the
    /// cache's value: the person authoring the map is typed on every board they make.</summary>
    [Test]
    public async Task A_player_already_resolved_is_answered_without_asking_again()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var _ = ApiTestFactory.Shared.CreateClient();
        using var scope = ApiTestFactory.Shared.Services.CreateScope();
        var (lookup, http, kept) = Offline(scope);
        await kept.KeepAsync(NotchUuid, "Notch");

        await Assert.That(await lookup.ResolveAsync("Notch")).IsEqualTo((NotchUuid, "Notch"));
        await Assert.That(await lookup.ResolveAsync(NotchUuid)).IsEqualTo((NotchUuid, "Notch"));
        await Assert.That(http.WasAsked).IsFalse();
    }

    /// <summary>A host that cannot be reached answers the same as a name no account carries: null, which the
    /// caller reads as a pseudonym. An author working offline states the people on their map and the credits
    /// stand — the alternative is an editor that silently drops them.</summary>
    [Test]
    public async Task An_unreachable_host_leaves_the_name_a_pseudonym_rather_than_an_error()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var _ = ApiTestFactory.Shared.CreateClient();
        using var scope = ApiTestFactory.Shared.Services.CreateScope();
        var (lookup, http, _) = Offline(scope);

        await Assert.That(await lookup.ResolveAsync("rockymine")).IsNull();
        await Assert.That(http.WasAsked).IsTrue()
            .Because("the name IS shaped like an account, so the question was worth asking");
    }
}
