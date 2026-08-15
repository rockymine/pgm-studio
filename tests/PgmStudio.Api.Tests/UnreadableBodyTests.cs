using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PgmStudio.Api.Tests;

/// <summary>
/// What the API answers to a body it cannot use.
///
/// <para><b>The contract is `docs/refusals.md`, and nine endpoints were not keeping it.</b> Posting `{}` to
/// each documented write surface returned a raw .NET stack trace from seven of them and MariaDB's own
/// <c>Column 'name' cannot be null</c> from an eighth — because every endpoint's catch names
/// <see cref="System.Text.Json.JsonException"/> alone, and a null string reaches
/// <c>JsonNode.Parse</c> as an <see cref="ArgumentNullException"/> instead. The faults below are asked of the
/// surface rather than of the fix, so they keep holding however the fix is later rearranged.</para>
/// </summary>
[NotInParallel("api-db")]
public sealed class UnreadableBodyTests
{
    /// <summary>The endpoints that took an empty body straight into an unhandled fault. A 4xx is the whole
    /// assertion: which 4xx, and what it says, is each gate's own business and is asserted below for the two
    /// that carry a field. A 5xx here is the studio blaming an author for its own defect.
    ///
    /// <para><c>room-styles/preview-snapshot</c> is deliberately not in this list. An empty object is a
    /// perfectly good style — every part has a default — so it answers 200 and always did, and asserting a
    /// refusal there would be asserting that a legitimate request fails.</para></summary>
    [Test]
    [Arguments("/api/themes")]
    [Arguments("/api/themes/preview")]
    [Arguments("/api/themes/import")]
    [Arguments("/api/room-styles")]
    [Arguments("/api/room-styles/preview")]
    [Arguments("/api/roof-styles")]
    [Arguments("/api/styles")]
    [Arguments("/api/terrain/prop-preview")]
    [Arguments("/api/terrain/material-preview")]
    public async Task An_empty_body_is_refused_and_never_crashes(string route)
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        var resp = await client.PostAsync(route, Json("{}"));

        await Assert.That((int)resp.StatusCode).IsGreaterThanOrEqualTo(400);
        await Assert.That((int)resp.StatusCode).IsLessThan(500);
    }

    /// <summary>A missing field is named, and all of them are — an author fixing one field per round trip is
    /// the cost of reporting only the first.</summary>
    [Test]
    public async Task A_missing_field_is_refused_by_its_own_name()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        var resp = await client.PostAsync("/api/styles", Json("{}"));
        var body = await resp.Content.ReadFromJsonAsync<Refusal>();

        await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(body!.Error).IsEqualTo("incomplete request");
        await Assert.That(body.Findings.Select(f => f.Field)).Contains("name");
        await Assert.That(body.Findings.Select(f => f.Rule).Distinct()).IsEquivalentTo(["RQ1"]);
    }

    /// <summary>A style part stated as null is the fault that took a whole map's export down with a bare
    /// <c>Object reference not set to an instance of an object</c>. It now arrives as a refusal naming the
    /// path inside the document, which is what a canvas highlights on click.</summary>
    [Test]
    public async Task A_part_stated_as_null_is_refused_by_its_path()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        var resp = await client.PostAsync(
            "/api/room-styles/preview-snapshot", Json("{\"roof\":{\"gableWindows\":null}}"));
        var body = await resp.Content.ReadFromJsonAsync<Refusal>();

        await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(body!.Findings.Select(f => f.Field)).Contains("roof.gableWindows");
    }

    /// <summary>A misspelled discriminator used to be the one fault that crashed while a misspelled *field*
    /// was accepted in silence — the wrong way round, and the pairing that cost an author two source reads.
    /// The kind is polymorphic, so System.Text.Json reports it as a <c>NotSupportedException</c> rather than a
    /// parse failure; that is a difference in reporting, not in what went wrong.</summary>
    [Test]
    public async Task A_material_naming_no_kind_is_refused_rather_than_crashing()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        var resp = await client.PostAsync(
            "/api/terrain/material-preview", Json("{\"kynd\":\"solid\",\"id\":1,\"data\":0}"));

        await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    /// <summary>The optional field the refusal must not swallow. <c>themes/import</c> names an unnamed import
    /// "Imported theme", so refusing a body that omits the name would break a documented convenience — which
    /// it did, until the DTO was made to say what the handler actually does.</summary>
    [Test]
    public async Task An_optional_field_is_still_optional()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        var resp = await client.PostAsync(
            "/api/themes/import", Json("{\"themeJson\":\"not a theme\"}"));

        await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        var body = await resp.Content.ReadFromJsonAsync<Refusal>();
        await Assert.That(body!.Findings.Select(f => f.Field)).DoesNotContain("name");
    }

    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

    private sealed record Refusal(string Error, string Message, IReadOnlyList<FindingBody> Findings);
    private sealed record FindingBody(string Rule, string Message, string? Field);
}
