using Microsoft.Extensions.DependencyInjection;
using PgmStudio.Api.Services;
using PgmStudio.Data.Map;
using PgmStudio.Geom.Render;

namespace PgmStudio.Api.Tests;

/// <summary>
/// What stands in front of Mojang. Two things do, and each of them answers a question that would otherwise
/// cost a request: the shape of the name, and what is already known.
///
/// <para>Every test here proves a request was <b>not</b> made, by handing the client a handler that fails the
/// test if it is reached. That is also the reading of the cache the author asked for — the same handful of
/// people are typed constantly, and after the first time the API is not called.</para>
/// </summary>
[NotInParallel("api-db")]
public sealed class PlayerLookupTests
{
    private const string NotchUuid = "069a79f4-44e9-4726-a5be-fca90e38aaf5";
    private const string JebUuid = "853c80ef-3c37-49fd-aa49-938b674adae6";

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

    /// <summary>A Mojang that answers <paramref name="status"/> to every question and counts them.</summary>
    private sealed class Answering(System.Net.HttpStatusCode status) : HttpMessageHandler
    {
        public int Asked { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Asked++;
            return Task.FromResult(new HttpResponseMessage(status));
        }
    }

    /// <summary>A name Mojang says nobody has is not asked about again for an hour, so a map naming someone who
    /// does not exist does not send Mojang a question on every request for its export. A Mojang that could not
    /// answer is asked again: that miss says nothing about the name.</summary>
    [Test]
    public async Task A_name_nobody_has_is_asked_once_and_a_failed_question_is_asked_again()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var _ = ApiTestFactory.Shared.CreateClient();
        using var scope = ApiTestFactory.Shared.Services.CreateScope();
        var kept = scope.ServiceProvider.GetRequiredService<PlayerNameStore>();

        var nobody = new Answering(System.Net.HttpStatusCode.NotFound);
        var misses = new PlayerMisses();
        for (var attempt = 0; attempt < 3; attempt++)
            await Assert.That(await new PlayerLookup(new MojangClient(new HttpClient(nobody)), kept, misses)
                .ResolveAsync("nobodyhasthis")).IsNull();
        await Assert.That(nobody.Asked).IsEqualTo(1);

        var failing = new Answering(System.Net.HttpStatusCode.TooManyRequests);
        var unremembered = new PlayerMisses();
        for (var attempt = 0; attempt < 2; attempt++)
            await Assert.That(await new PlayerLookup(new MojangClient(new HttpClient(failing)), kept, unremembered)
                .ResolveAsync("nobodyhasthis")).IsNull();
        await Assert.That(failing.Asked).IsEqualTo(2);
    }

    /// <summary>A Mojang that answers one profile whose skin lives at <paramref name="texture"/>, serves a
    /// small PNG there, and records every address it was asked.</summary>
    private sealed class SkinServer(string texture) : HttpMessageHandler
    {
        public static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];
        public List<Uri> Asked { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Asked.Add(request.RequestUri!);
            if (request.RequestUri!.Host == "sessionserver.mojang.com")
            {
                var textures = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(
                    "{\"textures\":{\"SKIN\":{\"url\":\"" + texture + "\"}}}"));
                var profile = $$"""{"id":"069a79f444e94726a5befca90e38aaf5","name":"Notch","properties":[{"name":"textures","value":"{{textures}}"}]}""";
                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                    { Content = new StringContent(profile) });
            }
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                { Content = new ByteArrayContent(Png) });
        }
    }

    /// <summary>A skin is fetched once, over https from Mojang's texture host, and read from the store after.</summary>
    [Test]
    public async Task A_skin_is_fetched_from_the_texture_host_once_and_kept()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var scope = ApiTestFactory.Shared.Services.CreateScope();
        var server = new SkinServer("http://textures.minecraft.net/texture/abc");
        var lookup = new PlayerLookup(new MojangClient(new HttpClient(server)),
            scope.ServiceProvider.GetRequiredService<PlayerNameStore>());

        await Assert.That(await lookup.SkinAsync(NotchUuid)).IsEquivalentTo(SkinServer.Png);
        await Assert.That(server.Asked.Last().ToString()).IsEqualTo("https://textures.minecraft.net/texture/abc");
        var asked = server.Asked.Count;

        await Assert.That(await lookup.SkinAsync(NotchUuid)).IsEquivalentTo(SkinServer.Png);
        await Assert.That(server.Asked.Count).IsEqualTo(asked).Because("a kept skin is not asked for again");
    }

    /// <summary>A profile naming a texture anywhere else is not followed: the studio fetches from Mojang's
    /// texture host and nowhere a profile could point it.</summary>
    [Test]
    public async Task A_texture_on_another_host_is_never_fetched()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var scope = ApiTestFactory.Shared.Services.CreateScope();
        var server = new SkinServer("http://example.com/texture/abc");
        var lookup = new PlayerLookup(new MojangClient(new HttpClient(server)),
            scope.ServiceProvider.GetRequiredService<PlayerNameStore>());

        await Assert.That(await lookup.SkinAsync(NotchUuid)).IsNull();
        await Assert.That(server.Asked.Any(uri => uri.Host == "example.com")).IsFalse();
    }

    /// <summary>The route serves a kept skin as a PNG from the studio's own origin, and answers 404 in the
    /// envelope for a uuid that is not one.</summary>
    [Test]
    public async Task The_head_route_draws_the_face_of_the_kept_skin()
    {
        // A skin with the face one colour and every other pixel another: the hat area is opaque everywhere, so
        // the game draws no hat and the head is the face alone.
        var rgb = new byte[64 * 32 * 3];
        for (var at = 0; at < rgb.Length; at += 3) (rgb[at], rgb[at + 1], rgb[at + 2]) = ((byte)20, (byte)40, (byte)60);
        for (var y = 8; y < 16; y++)
            for (var x = 8; x < 16; x++) (rgb[((y * 64) + x) * 3], rgb[((y * 64) + x) * 3 + 1], rgb[((y * 64) + x) * 3 + 2]) = ((byte)200, (byte)150, (byte)100);
        await ApiTestFactory.ResetSchemaAsync();
        using (var scope = ApiTestFactory.Shared.Services.CreateScope())
        {
            var names = scope.ServiceProvider.GetRequiredService<PlayerNameStore>();
            await names.KeepSkinAsync(NotchUuid, "Notch", PngWriter.Encode(64, 32, rgb));
            await names.KeepSkinAsync(JebUuid, "jeb_", SkinServer.Png);
        }
        using var client = ApiTestFactory.Shared.CreateClient();

        using var head = await client.GetAsync($"/api/minecraft/player/{NotchUuid}/head");
        await Assert.That(head.Content.Headers.ContentType?.MediaType).IsEqualTo("image/png");
        var drawn = PngReader.Decode(await head.Content.ReadAsByteArrayAsync());
        await Assert.That((drawn.Width, drawn.Height)).IsEqualTo((8, 8));
        await Assert.That(drawn.Rgba[..4]).IsEquivalentTo(new byte[] { 200, 150, 100, 255 });

        using var unreadable = await client.GetAsync($"/api/minecraft/player/{JebUuid}/head");
        await Assert.That(unreadable.StatusCode).IsEqualTo(System.Net.HttpStatusCode.NotFound);
        using var none = await client.GetAsync("/api/minecraft/player/not-a-uuid/head");
        await Assert.That(none.StatusCode).IsEqualTo(System.Net.HttpStatusCode.NotFound);
    }
}
