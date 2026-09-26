using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PgmStudio.Api.Access;
using PgmStudio.Contracts;

namespace PgmStudio.Api.Tests;

/// <summary>
/// Who may write, in an <c>invited</c> studio: reading is open to anyone, writing needs a person on the
/// whitelist, a map is changed only by its owner, an author it credits or an admin, and a shared library row
/// is removed only by an admin. Every refusal is the envelope, <c>RQ7</c> at 401 and <c>RQ8</c> at 403.
/// </summary>
[NotInParallel("api-db")]
public sealed class AccessTests
{
    private const string Admin = "00000000-0000-0000-0000-0000000000ad";
    private const string Owner = "00000000-0000-0000-0000-000000000001";
    private const string Stranger = "00000000-0000-0000-0000-000000000002";
    private const string Credited = "00000000-0000-0000-0000-000000000003";
    private const string Unlisted = "00000000-0000-0000-0000-000000000004";

    [Test]
    public async Task A_signed_out_request_reads_and_is_refused_a_write_with_RQ7()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = InvitedFactory.Shared.CreateClient();

        using var list = await client.GetAsync("/api/maps");
        await Assert.That(list.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var me = await client.GetFromJsonAsync<CallerDto>("/api/me");
        await Assert.That(me!.Mode).IsEqualTo("invited");
        await Assert.That(me.SignedIn).IsFalse();
        await Assert.That(me.Role).IsNull();

        using var write = await client.PostAsJsonAsync("/api/sketch", new { name = "Weirgate" });
        await AssertRefusedAsync(write, HttpStatusCode.Unauthorized, "RQ7");
    }

    [Test]
    public async Task A_signed_in_account_the_whitelist_does_not_hold_is_refused_with_RQ8()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = InvitedFactory.As(Unlisted);

        var me = await client.GetFromJsonAsync<CallerDto>("/api/me");
        await Assert.That(me!.SignedIn).IsTrue();
        await Assert.That(me.Role).IsNull();

        using var write = await client.PostAsJsonAsync("/api/sketch", new { name = "Weirgate" });
        await AssertRefusedAsync(write, HttpStatusCode.Forbidden, "RQ8");
    }

    [Test]
    public async Task A_map_belongs_to_whoever_originated_it_and_to_nobody_else_on_the_whitelist()
    {
        await ApiTestFactory.ResetSchemaAsync();
        await WhitelistAsync(Owner, "member");
        await WhitelistAsync(Stranger, "member");

        using var owner = InvitedFactory.As(Owner);
        var slug = await OriginateAsync(owner, "Weirgate");
        var stored = await ScalarAsync($"SELECT owner_uuid FROM map WHERE slug = '{slug}'");
        await Assert.That(stored).IsEqualTo(Owner);

        using var stranger = InvitedFactory.As(Stranger);
        using var refused = await stranger.DeleteAsync($"/api/map/{slug}");
        await AssertRefusedAsync(refused, HttpStatusCode.Forbidden, "RQ8");

        using var deleted = await owner.DeleteAsync($"/api/map/{slug}");
        await Assert.That(deleted.IsSuccessStatusCode).IsTrue().Because(await deleted.Content.ReadAsStringAsync());
    }

    [Test]
    public async Task An_author_a_map_credits_may_change_it_and_a_contributor_may_not()
    {
        await ApiTestFactory.ResetSchemaAsync();
        await WhitelistAsync(Owner, "member");
        await WhitelistAsync(Credited, "member");
        await WhitelistAsync(Stranger, "member");

        using var owner = InvitedFactory.As(Owner);
        var slug = await OriginateAsync(owner, "Weirgate");
        await ApiTestFactory.ExecuteAsync(
            $"INSERT INTO author (map_id, uuid, role) SELECT id, '{Credited}', 'author' FROM map WHERE slug = '{slug}';"
            + $"INSERT INTO author (map_id, uuid, role) SELECT id, '{Stranger}', 'contributor' FROM map WHERE slug = '{slug}'");

        using var contributor = InvitedFactory.As(Stranger);
        using var refused = await contributor.DeleteAsync($"/api/map/{slug}");
        await AssertRefusedAsync(refused, HttpStatusCode.Forbidden, "RQ8");

        using var author = InvitedFactory.As(Credited);
        using var deleted = await author.DeleteAsync($"/api/map/{slug}");
        await Assert.That(deleted.IsSuccessStatusCode).IsTrue().Because(await deleted.Content.ReadAsStringAsync());
    }

    [Test]
    public async Task An_admin_changes_any_map_and_removes_a_library_row_a_member_may_not()
    {
        await ApiTestFactory.ResetSchemaAsync();
        await WhitelistAsync(Owner, "member");

        using var owner = InvitedFactory.As(Owner);
        var slug = await OriginateAsync(owner, "Weirgate");

        using var member = await owner.DeleteAsync("/api/themes/1");
        await AssertRefusedAsync(member, HttpStatusCode.Forbidden, "RQ8");

        // An admin named in Access:Admins needs no whitelist row, and is past the gate.
        using var admin = InvitedFactory.As(Admin);
        using var library = await admin.DeleteAsync("/api/themes/1");
        await Assert.That(library.IsSuccessStatusCode).IsTrue().Because(await library.Content.ReadAsStringAsync());
        using var map = await admin.DeleteAsync($"/api/map/{slug}");
        await Assert.That(map.IsSuccessStatusCode).IsTrue().Because(await map.Content.ReadAsStringAsync());
    }

    [Test]
    public async Task Loading_documents_over_someone_elses_map_is_refused_and_leaves_it_alone()
    {
        await ApiTestFactory.ResetSchemaAsync();
        await WhitelistAsync(Owner, "member");
        await WhitelistAsync(Stranger, "member");

        using var owner = InvitedFactory.As(Owner);
        var slug = await OriginateAsync(owner, "Weirgate");
        var before = await ScalarAsync($"SELECT id FROM map WHERE slug = '{slug}'");

        using var stranger = InvitedFactory.As(Stranger);
        using var refused = await stranger.PostAsJsonAsync("/api/map/from-documents", new
        {
            plan = JsonDocument.Parse("""{"cell":9,"pieces":[]}""").RootElement,
            layout = JsonDocument.Parse("""{"layers":[]}""").RootElement,
            intent = JsonDocument.Parse("""{"meta":{"name":"Weirgate"}}""").RootElement,
            slug,
        });
        await AssertRefusedAsync(refused, HttpStatusCode.Forbidden, "RQ8");
        await Assert.That(await ScalarAsync($"SELECT id FROM map WHERE slug = '{slug}'")).IsEqualTo(before);
    }

    [Test]
    public async Task The_whitelist_is_kept_by_an_admin_alone()
    {
        await ApiTestFactory.ResetSchemaAsync();
        await WhitelistAsync(Owner, "member");
        await ApiTestFactory.ExecuteAsync(
            $"INSERT INTO minecraft_player (uuid, name, fetched_at) VALUES ('{Stranger}', 'Weirgater', UTC_TIMESTAMP())");

        using var member = InvitedFactory.As(Owner);
        using var listed = await member.GetAsync("/api/users");
        await AssertRefusedAsync(listed, HttpStatusCode.Forbidden, "RQ8");

        using var admin = InvitedFactory.As(Admin);
        using var put = await admin.PostAsJsonAsync("/api/users", new { player = "Weirgater", role = "member" });
        await Assert.That(put.StatusCode).IsEqualTo(HttpStatusCode.OK).Because(await put.Content.ReadAsStringAsync());
        var added = await put.Content.ReadFromJsonAsync<StudioUserDto>();
        await Assert.That(added!.Uuid).IsEqualTo(Stranger);

        using var stranger = InvitedFactory.As(Stranger);
        var me = await stranger.GetFromJsonAsync<CallerDto>("/api/me");
        await Assert.That(me!.Role).IsEqualTo("member");

        using var removed = await admin.DeleteAsync($"/api/users/{Stranger}");
        await Assert.That(removed.IsSuccessStatusCode).IsTrue();
        me = await stranger.GetFromJsonAsync<CallerDto>("/api/me");
        await Assert.That(me!.Role).IsNull().Because("taking someone off the whitelist holds at once");
    }

    [Test]
    public async Task Every_write_publishes_401_and_403_and_no_read_does()
    {
        using var client = ApiTestFactory.Shared.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/api/openapi/v1.json"));
        var wrong = new List<string>();
        foreach (var path in document.RootElement.GetProperty("paths").EnumerateObject())
        foreach (var verb in path.Value.EnumerateObject())
        {
            var responses = verb.Value.GetProperty("responses");
            var guarded = responses.TryGetProperty("401", out _) && responses.TryGetProperty("403", out _);
            var read = verb.Name is "get" or "head";
            if (guarded == read && !(read && path.Name == "/api/users"))
                wrong.Add($"{verb.Name.ToUpperInvariant()} {path.Name}");
        }
        await Assert.That(wrong).IsEmpty()
            .Because($"these routes publish the wrong access answers: {string.Join(", ", wrong)}");
    }

    private static async Task<string> OriginateAsync(HttpClient client, string name)
    {
        using var resp = await client.PostAsJsonAsync("/api/sketch", new { name });
        var text = await resp.Content.ReadAsStringAsync();
        await Assert.That(resp.IsSuccessStatusCode).IsTrue().Because(text);
        return JsonDocument.Parse(text).RootElement.GetProperty("slug").GetString()!;
    }

    private static Task WhitelistAsync(string uuid, string role) => ApiTestFactory.ExecuteAsync(
        $"INSERT INTO studio_user (uuid, name, role, created_at) VALUES ('{uuid}', 'p{uuid[^4..]}', '{role}', UTC_TIMESTAMP())");

    private static async Task<string?> ScalarAsync(string sql)
    {
        await using var conn = new MySqlConnector.MySqlConnection(ApiTestFactory.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new MySqlConnector.MySqlCommand(sql, conn);
        return (await cmd.ExecuteScalarAsync())?.ToString();
    }

    private static async Task AssertRefusedAsync(HttpResponseMessage resp, HttpStatusCode status, string rule)
    {
        var text = await resp.Content.ReadAsStringAsync();
        await Assert.That(resp.StatusCode).IsEqualTo(status).Because(text);
        var finding = JsonDocument.Parse(text).RootElement.GetProperty("findings")[0];
        await Assert.That(finding.GetProperty("rule").GetString()).IsEqualTo(rule);
    }

    /// <summary>The studio in <c>invited</c> mode, with <see cref="Admin"/> named in <c>Access:Admins</c> and a
    /// test scheme in place of the session cookie: a request is signed in as the uuid its
    /// <c>X-Test-Uuid</c> header names, and signed out without one.</summary>
    private sealed class InvitedFactory : WebApplicationFactory<Program>
    {
        public static InvitedFactory Shared { get; } = new();

        public static HttpClient As(string uuid)
        {
            var client = Shared.CreateClient();
            client.DefaultRequestHeaders.Add(HeaderScheme.Header, uuid);
            return client;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PgmStudio"] = ApiTestFactory.ConnectionString,
                ["Access:Mode"] = "invited",
                ["Access:Admins:0"] = Admin,
            }));
            builder.ConfigureTestServices(services => services
                .AddAuthentication(HeaderScheme.Name)
                .AddScheme<AuthenticationSchemeOptions, HeaderScheme>(HeaderScheme.Name, null));
        }
    }

    private sealed class HeaderScheme(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string Name = "test";
        public const string Header = "X-Test-Uuid";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (Request.Headers[Header].ToString() is not { Length: > 0 } uuid)
                return Task.FromResult(AuthenticateResult.NoResult());
            var identity = new ClaimsIdentity([new Claim(StudioClaims.Uuid, uuid)], Name);
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), Name)));
        }
    }
}
