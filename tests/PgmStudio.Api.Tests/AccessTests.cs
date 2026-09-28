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
        await Assert.That((await owner.GetFromJsonAsync<MapAccessDto>($"/api/map/{slug}/access"))!.MayEdit).IsTrue();
        await Assert.That((await stranger.GetFromJsonAsync<MapAccessDto>($"/api/map/{slug}/access"))!.MayEdit).IsFalse();
        using var signedOut = InvitedFactory.Shared.CreateClient();
        await Assert.That((await signedOut.GetFromJsonAsync<MapAccessDto>($"/api/map/{slug}/access"))!.MayEdit).IsFalse();

        using var refused = await stranger.DeleteAsync($"/api/map/{slug}");
        await AssertRefusedAsync(refused, HttpStatusCode.Forbidden, "RQ8");

        using var deleted = await owner.DeleteAsync($"/api/map/{slug}");
        await Assert.That(deleted.IsSuccessStatusCode).IsTrue().Because(await deleted.Content.ReadAsStringAsync());
    }

    [Test]
    public async Task A_map_is_credited_to_whoever_originated_it_and_they_stay_its_owner_without_the_credit()
    {
        await ApiTestFactory.ResetSchemaAsync();
        await WhitelistAsync(Owner, "member");

        using var owner = InvitedFactory.As(Owner);
        var slug = await OriginateAsync(owner, "Weirgate");
        var doc = await owner.GetFromJsonAsync<JsonElement>($"/api/map/{slug}");
        var credits = doc.GetProperty("authors").EnumerateArray()
            .Select(author => (author.GetProperty("uuid").GetString(), author.GetProperty("name").GetString())).ToList();
        await Assert.That(credits).IsEquivalentTo(new[] { ((string?)Owner, (string?)NameOf(Owner)) });

        using var dropped = await owner.PatchAsJsonAsync($"/api/map/{slug}/metadata", new { authors = Array.Empty<object>() });
        await Assert.That(dropped.IsSuccessStatusCode).IsTrue().Because(await dropped.Content.ReadAsStringAsync());
        await Assert.That(await ScalarAsync($"SELECT COUNT(*) FROM author a JOIN map m ON m.id = a.map_id WHERE m.slug = '{slug}'"))
            .IsEqualTo("0");
        await Assert.That((await owner.GetFromJsonAsync<MapAccessDto>($"/api/map/{slug}/access"))!.MayEdit).IsTrue();
    }

    [Test]
    public async Task An_untouched_draft_is_discarded_though_it_credits_its_originator_and_one_crediting_another_is_kept()
    {
        await ApiTestFactory.ResetSchemaAsync();
        await WhitelistAsync(Owner, "member");

        using var owner = InvitedFactory.As(Owner);
        var untouched = await OriginateAsync(owner, "Untitled sketch");
        var discarded = await owner.DeleteFromJsonAsync<JsonElement>($"/api/map/{untouched}/discard-if-empty");
        await Assert.That(discarded.GetProperty("discarded").GetBoolean()).IsTrue();
        await Assert.That(await ScalarAsync($"SELECT COUNT(*) FROM map WHERE slug = '{untouched}'")).IsEqualTo("0");

        var credited = await OriginateAsync(owner, "Untitled sketch");
        await ApiTestFactory.ExecuteAsync(
            $"INSERT INTO author (map_id, uuid, role) SELECT id, '{Credited}', 'author' FROM map WHERE slug = '{credited}'");
        var kept = await owner.DeleteFromJsonAsync<JsonElement>($"/api/map/{credited}/discard-if-empty");
        await Assert.That(kept.GetProperty("discarded").GetBoolean()).IsFalse();
    }

    /// <summary>One plate, as the Sketch page posts its live layout to the routes that draw it.</summary>
    private const string Plate = """
        {"setup":{"mirror_mode":"none","center":{"cx":0,"cz":0}},"layers":[{"id":"ground","base_y":0,"layout":{"shapes":[
          {"id":"a","type":"rectangle","operation":"add","min_x":0,"min_z":0,"max_x":24,"max_z":24,"base_height":6}],
         "groups":[{"id":"g","name":"Plate","mirrors":false,"shapeIds":["a"]}]}}]}
        """;

    /// <summary>The views a Sketch page is drawn from — its paint, its relief and the built world — are reads
    /// sent as a <c>POST</c>, so anyone signed in sees them on a map they may not change, and the same person is
    /// still refused a write to it. Signed out, they are refused like a write.</summary>
    [Test]
    public async Task Anyone_signed_in_sees_a_maps_sketch_views_and_only_its_editors_change_it()
    {
        await ApiTestFactory.ResetSchemaAsync();
        await WhitelistAsync(Owner, "member");
        await WhitelistAsync(Stranger, "member");

        using var owner = InvitedFactory.As(Owner);
        var slug = await OriginateAsync(owner, "Weirgate");

        using var stranger = InvitedFactory.As(Stranger);
        foreach (var view in (string[])["paint", "relief", "columns"])
        {
            using var seen = await stranger.PostAsync($"/api/map/{slug}/sketch/{view}",
                new StringContent(Plate, System.Text.Encoding.UTF8, "application/json"));
            await Assert.That(seen.StatusCode).IsEqualTo(HttpStatusCode.OK)
                .Because($"sketch/{view} answered {await seen.Content.ReadAsStringAsync()}");
        }

        using var write = await stranger.PostAsJsonAsync($"/api/map/{slug}/sketch/props",
            new { kind = "tree", id = "t", x = 4, z = 4, seed = 1 });
        await AssertRefusedAsync(write, HttpStatusCode.Forbidden, "RQ8");

        using var signedOut = InvitedFactory.Shared.CreateClient();
        using var unseen = await signedOut.PostAsync($"/api/map/{slug}/sketch/columns",
            new StringContent(Plate, System.Text.Encoding.UTF8, "application/json"));
        await AssertRefusedAsync(unseen, HttpStatusCode.Unauthorized, "RQ7");
    }

    /// <summary>A client with no browser sends the token it was issued and is the person it was issued for:
    /// the same account, the same role and the same maps, credited the same way. Revoked, it signs nobody in,
    /// and the studio keeps the token's hash rather than the token.</summary>
    [Test]
    public async Task A_token_signs_a_caller_without_a_browser_in_as_the_person_it_was_issued_for()
    {
        await ApiTestFactory.ResetSchemaAsync();
        await WhitelistAsync(Owner, "member");
        using var owner = InvitedFactory.As(Owner);

        var issued = await (await owner.PostAsJsonAsync("/api/users/me/tokens", new StudioTokenRequest("drive.py")))
            .Content.ReadFromJsonAsync<StudioTokenIssuedDto>();
        await Assert.That(issued!.Token).StartsWith(TokenAccessHandler.Prefix);
        await Assert.That(issued.ActsAs).IsEqualTo(NameOf(Owner));
        await Assert.That(await ScalarAsync($"SELECT hash FROM studio_token WHERE id = {issued.Id}"))
            .IsEqualTo(StudioSecret.HashOf(issued.Token));

        using var agent = WithToken(issued.Token);
        var me = await agent.GetFromJsonAsync<CallerDto>("/api/me");
        await Assert.That((me!.SignedIn, me.Uuid, me.Role)).IsEqualTo((true, Owner, "member"));
        var slug = await OriginateAsync(agent, "Weirgate");
        await Assert.That(await ScalarAsync($"SELECT owner_uuid FROM map WHERE slug = '{slug}'")).IsEqualTo(Owner);

        var listed = await owner.GetFromJsonAsync<List<StudioTokenDto>>("/api/users/me/tokens");
        await Assert.That(listed!.Single().Label).IsEqualTo("drive.py");
        await Assert.That(listed!.Single().LastUsedAt).IsNotNull();

        using var revoked = await owner.DeleteAsync($"/api/users/me/tokens/{issued.Id}");
        await Assert.That(revoked.IsSuccessStatusCode).IsTrue();
        using var refused = await agent.PostAsJsonAsync("/api/sketch", new { name = "Weirgate" });
        await AssertRefusedAsync(refused, HttpStatusCode.Unauthorized, "RQ7");
    }

    /// <summary>An admin issues a token for someone else, and nobody else may. A token acts as its person only
    /// while the whitelist holds them: taking them off revokes it. A token the studio never issued signs nobody
    /// in, and a visitor has no tokens to list.</summary>
    [Test]
    public async Task A_token_is_issued_by_its_person_or_an_admin_and_ends_with_their_place_on_the_whitelist()
    {
        await ApiTestFactory.ResetSchemaAsync();
        await WhitelistAsync(Owner, "member");
        await WhitelistAsync(Stranger, "member");

        using var owner = InvitedFactory.As(Owner);
        using var notTheirs = await owner.PostAsJsonAsync($"/api/users/{Stranger}/tokens", new StudioTokenRequest("x"));
        await AssertRefusedAsync(notTheirs, HttpStatusCode.Forbidden, "RQ8");

        using var admin = InvitedFactory.As(Admin);
        var issued = await (await admin.PostAsJsonAsync($"/api/users/{Stranger}/tokens", new StudioTokenRequest(null)))
            .Content.ReadFromJsonAsync<StudioTokenIssuedDto>();
        await Assert.That(issued!.Label).IsEqualTo("token");
        using var agent = WithToken(issued.Token);
        await Assert.That((await agent.GetFromJsonAsync<CallerDto>("/api/me"))!.Uuid).IsEqualTo(Stranger);

        using var removed = await admin.DeleteAsync($"/api/users/{Stranger}");
        await Assert.That(removed.IsSuccessStatusCode).IsTrue();
        await Assert.That(await ScalarAsync("SELECT COUNT(*) FROM studio_token")).IsEqualTo("0");
        using var refused = await agent.PostAsJsonAsync("/api/sketch", new { name = "Weirgate" });
        await AssertRefusedAsync(refused, HttpStatusCode.Unauthorized, "RQ7");

        using var forged = WithToken(TokenAccessHandler.Prefix + "never-issued");
        using var forgedWrite = await forged.PostAsJsonAsync("/api/sketch", new { name = "Weirgate" });
        await AssertRefusedAsync(forgedWrite, HttpStatusCode.Unauthorized, "RQ7");

        using var visitor = InvitedFactory.Shared.CreateClient();
        await Assert.That(await visitor.GetFromJsonAsync<List<StudioTokenDto>>("/api/users/me/tokens")).IsEmpty();
    }

    /// <summary>A token acts as its person on their own maps and nothing wider. An admin's token is a member's
    /// — it keeps no whitelist and changes no map its person does not own — and no token issues a token, so one
    /// that leaks cannot outlive its revocation. It may still revoke itself.</summary>
    [Test]
    public async Task A_token_never_carries_an_admins_rights_and_never_issues_a_token()
    {
        await ApiTestFactory.ResetSchemaAsync();
        await WhitelistAsync(Owner, "member");
        using var owner = InvitedFactory.As(Owner);
        var slug = await OriginateAsync(owner, "Weirgate");

        using var admin = InvitedFactory.As(Admin);
        var issued = await (await admin.PostAsJsonAsync("/api/users/me/tokens", new StudioTokenRequest("agent")))
            .Content.ReadFromJsonAsync<StudioTokenIssuedDto>();
        using var agent = WithToken(issued!.Token);

        await Assert.That((await agent.GetFromJsonAsync<CallerDto>("/api/me"))!.Role).IsEqualTo("member");
        using var whitelist = await agent.GetAsync("/api/users");
        await AssertRefusedAsync(whitelist, HttpStatusCode.Forbidden, "RQ8");
        using var othersMap = await agent.DeleteAsync($"/api/map/{slug}");
        await AssertRefusedAsync(othersMap, HttpStatusCode.Forbidden, "RQ8");

        using var another = await agent.PostAsJsonAsync("/api/users/me/tokens", new StudioTokenRequest("second"));
        await AssertRefusedAsync(another, HttpStatusCode.Forbidden, "RQ8");
        using var forSomeone = await agent.PostAsJsonAsync($"/api/users/{Owner}/tokens", new StudioTokenRequest("x"));
        await AssertRefusedAsync(forSomeone, HttpStatusCode.Forbidden, "RQ8");
        await Assert.That(await ScalarAsync("SELECT COUNT(*) FROM studio_token")).IsEqualTo("1");

        using var revoked = await agent.DeleteAsync($"/api/users/me/tokens/{issued.Id}");
        await Assert.That(revoked.IsSuccessStatusCode).IsTrue();
    }

    private static HttpClient WithToken(string token)
    {
        var client = InvitedFactory.Shared.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
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

    /// <summary>The routes that state their own access rather than taking the rule's.</summary>
    private static readonly HashSet<string> StatesItsOwnAccess =
        ["GET /api/users", "POST /api/auth/sign-out", "GET /api/auth/discord/complete"];

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
            var name = $"{verb.Name.ToUpperInvariant()} {path.Name}";
            if (guarded == read && !StatesItsOwnAccess.Contains(name)) wrong.Add(name);
        }
        await Assert.That(wrong).IsEmpty()
            .Because($"these routes publish the wrong access answers: {string.Join(", ", wrong)}");
    }

    [Test]
    public async Task A_studio_with_no_Discord_application_refuses_sign_in_with_RQ9()
    {
        using var client = ApiTestFactory.Shared.CreateClient();
        using var resp = await client.GetAsync("/api/auth/discord");
        await AssertRefusedAsync(resp, HttpStatusCode.ServiceUnavailable, "RQ9");
    }

    [Test]
    public async Task Signing_in_sends_the_browser_to_Discord_asking_for_identify_alone()
    {
        using var client = InvitedFactory.Shared.CreateClient(new() { AllowAutoRedirect = false });
        using var resp = await client.GetAsync("/api/auth/discord?returnUrl=/maps");
        await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.Redirect);

        var location = resp.Headers.Location!;
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(location.Query);
        await Assert.That(location.GetLeftPart(UriPartial.Path)).IsEqualTo("https://discord.com/oauth2/authorize");
        await Assert.That(query["client_id"].ToString()).IsEqualTo("1553430351484026980");
        await Assert.That(query["scope"].ToString()).IsEqualTo("identify");
        await Assert.That(query["redirect_uri"].ToString()).EndsWith("/api/auth/discord/callback");
        await Assert.That(query["code_challenge_method"].ToString()).IsEqualTo("S256");
    }

    [Test]
    public async Task An_admin_opens_an_invitation_and_only_an_open_one_is_followed()
    {
        await ApiTestFactory.ResetSchemaAsync();
        await WhitelistAsync(Owner, "member");

        using var member = InvitedFactory.As(Owner);
        using var refused = await member.PostAsync($"/api/users/{Owner}/invite", null);
        await AssertRefusedAsync(refused, HttpStatusCode.Forbidden, "RQ8");

        using var admin = InvitedFactory.As(Admin);
        using var nobody = await admin.PostAsync($"/api/users/{Stranger}/invite", null);
        await AssertRefusedAsync(nobody, HttpStatusCode.NotFound, "RQ4");

        using var issued = await admin.PostAsync($"/api/users/{Owner}/invite", null);
        var invite = await issued.Content.ReadFromJsonAsync<InviteDto>();
        var path = new Uri(invite!.Link).AbsolutePath;
        await Assert.That(path).StartsWith("/api/auth/invite/");
        await Assert.That(await ScalarAsync($"SELECT invite_hash FROM studio_user WHERE uuid = '{Owner}'"))
            .IsEqualTo(StudioSecret.HashOf(path["/api/auth/invite/".Length..]))
            .Because("the whitelist stores the code's hash, never the code");

        using var browser = InvitedFactory.Shared.CreateClient(new() { AllowAutoRedirect = false });
        using var followed = await browser.GetAsync(path);
        await Assert.That(followed.StatusCode).IsEqualTo(HttpStatusCode.Redirect);
        await Assert.That(followed.Headers.Location!.Host).IsEqualTo("discord.com");

        using var unknown = await browser.GetAsync("/api/auth/invite/not-a-code");
        await AssertRefusedAsync(unknown, HttpStatusCode.NotFound, "RQ4");
    }

    [Test]
    public async Task An_invitation_binds_the_first_Discord_account_to_follow_it_and_closes()
    {
        await ApiTestFactory.ResetSchemaAsync();
        await WhitelistAsync(Owner, "member");
        await WhitelistAsync(Stranger, "member");
        using var db = new PgmStudio.Data.Schema.PgmDb(
            PgmStudio.Data.Schema.PgmDataOptions.ForConnectionString(ApiTestFactory.ConnectionString));
        var users = new PgmStudio.Data.Access.StudioUserStore(db);

        var (code, hash) = StudioSecret.New();
        await users.OpenInviteAsync(Owner, hash, DateTime.UtcNow.AddDays(1));

        await Assert.That(await DiscordSignIn.ResolveAsync(users, "111", invite: null, default)).IsNull()
            .Because("an account bound to nobody signs in as nobody");
        var bound = await DiscordSignIn.ResolveAsync(users, "111", code, default);
        await Assert.That(bound!.Uuid).IsEqualTo(Owner);
        await Assert.That((await DiscordSignIn.ResolveAsync(users, "111", invite: null, default))!.Uuid).IsEqualTo(Owner);
        await Assert.That(await DiscordSignIn.ResolveAsync(users, "222", code, default)).IsNull()
            .Because("an invitation is followed once");

        var (lapsed, lapsedHash) = StudioSecret.New();
        await users.OpenInviteAsync(Stranger, lapsedHash, DateTime.UtcNow.AddMinutes(-1));
        await Assert.That(await DiscordSignIn.ResolveAsync(users, "333", lapsed, default)).IsNull();

        // One Discord account signs in as one person: following another invitation moves it.
        var (second, secondHash) = StudioSecret.New();
        await users.OpenInviteAsync(Stranger, secondHash, DateTime.UtcNow.AddDays(1));
        await Assert.That((await DiscordSignIn.ResolveAsync(users, "111", second, default))!.Uuid).IsEqualTo(Stranger);
        await Assert.That((await DiscordSignIn.ResolveAsync(users, "111", invite: null, default))!.Uuid).IsEqualTo(Stranger);
        await Assert.That(await ScalarAsync($"SELECT discord_id FROM studio_user WHERE uuid = '{Owner}'")).IsNull();
    }

    [Test]
    public async Task Signing_out_is_open_to_anyone()
    {
        using var client = InvitedFactory.Shared.CreateClient();
        using var resp = await client.PostAsync("/api/auth/sign-out", null);
        await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    private static async Task<string> OriginateAsync(HttpClient client, string name)
    {
        using var resp = await client.PostAsJsonAsync("/api/sketch", new { name });
        var text = await resp.Content.ReadAsStringAsync();
        await Assert.That(resp.IsSuccessStatusCode).IsTrue().Because(text);
        return JsonDocument.Parse(text).RootElement.GetProperty("slug").GetString()!;
    }

    private static Task WhitelistAsync(string uuid, string role) => ApiTestFactory.ExecuteAsync(
        $"INSERT INTO studio_user (uuid, name, role, created_at) VALUES ('{uuid}', '{NameOf(uuid)}', '{role}', UTC_TIMESTAMP())");

    /// <summary>The Minecraft name the test whitelist and the test session both give a uuid.</summary>
    private static string NameOf(string uuid) => $"p{uuid[^4..]}";

    private static async Task<string?> ScalarAsync(string sql)
    {
        await using var conn = new MySqlConnector.MySqlConnection(ApiTestFactory.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new MySqlConnector.MySqlCommand(sql, conn);
        return await cmd.ExecuteScalarAsync() is { } value and not DBNull ? value.ToString() : null;
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
    /// <c>X-Test-Uuid</c> header names, and read as the studio reads it without one.</summary>
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
                ["Discord:ClientSecret"] = "test-secret",
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
            // A request naming nobody here is read the way the studio reads it, so a bearer token reaches the
            // token scheme through the studio's own selector.
            if (Request.Headers[Header].ToString() is not { Length: > 0 } uuid)
                return Context.AuthenticateAsync(AccessOptions.Scheme);
            var identity = new ClaimsIdentity(
                [new Claim(StudioClaims.Uuid, uuid), new Claim(StudioClaims.Name, NameOf(uuid))], Name);
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), Name)));
        }
    }
}
