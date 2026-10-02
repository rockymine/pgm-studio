using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PgmStudio.Api.Endpoints;
using PgmStudio.Api.Services;
using PgmStudio.Contracts;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Tests;

/// <summary>
/// A row the seed folder states is the folder's (<see cref="SeededRows"/>): on every kind it reads back
/// <c>seeded</c>, and an edit or a delete of it is refused 409 <c>LB6</c> and leaves it as it was, while an author's
/// row of the same kind saves. Runs against <c>pgm_studio_test</c>; resets the schema, so it runs serially with the
/// rest.
/// </summary>
[NotInParallel("api-db")]
public sealed class SeededRowsTests
{
    private static readonly string[] Routes =
    [
        "styles", "themes", "room-styles", "roof-styles", "storey-styles", "porch-styles", "tree-styles",
        "boulder-styles", "biome-patterns",
    ];

    [Test]
    public async Task A_seeded_row_of_every_kind_refuses_an_edit_and_a_delete()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();
        using (var scope = ApiTestFactory.Shared.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<LibrarySeed>().SeedAsync();

        foreach (var route in Routes)
        {
            var rows = await client.GetFromJsonAsync<JsonElement>($"/api/{route}");
            var id = rows.EnumerateArray().First().GetProperty("id").GetInt64();
            var before = await client.GetFromJsonAsync<JsonElement>($"/api/{route}/{id}");
            await Assert.That(before.GetProperty("seeded").GetBoolean()).IsTrue().Because($"{route} {id} is seeded");

            var edited = await client.PutAsJsonAsync($"/api/{route}/{id}", before);
            await Assert.That(edited.StatusCode).IsEqualTo(HttpStatusCode.Conflict).Because($"{route} {id} edited");
            await Assert.That((await edited.Content.ReadFromJsonAsync<RefusalDto>())!.Findings.Single().Rule)
                .IsEqualTo("LB6");

            var deleted = await client.DeleteAsync($"/api/{route}/{id}");
            await Assert.That(deleted.StatusCode).IsEqualTo(HttpStatusCode.Conflict).Because($"{route} {id} deleted");

            var after = await client.GetFromJsonAsync<JsonElement>($"/api/{route}/{id}");
            await Assert.That(after.GetProperty("name").GetString()).IsEqualTo(before.GetProperty("name").GetString());
        }
    }

    [Test]
    public async Task An_authors_row_is_not_seeded_and_saves()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        var made = await (await client.PostAsJsonAsync("/api/themes",
            new ThemeSaveRequest("Frost night", false, 1, RimEdgeModes.Drop, true, [])))
            .Content.ReadFromJsonAsync<ThemeDetail>();
        await Assert.That((await client.GetFromJsonAsync<ThemeDetail>($"/api/themes/{made!.Id}"))!.Seeded).IsFalse();

        var kept = await client.PutAsJsonAsync($"/api/themes/{made.Id}",
            new ThemeSaveRequest("Frost night", true, 2, RimEdgeModes.Drop, true, []));
        await Assert.That(kept.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That((await client.DeleteAsync($"/api/themes/{made.Id}")).StatusCode)
            .IsEqualTo(HttpStatusCode.NoContent);
    }
}
