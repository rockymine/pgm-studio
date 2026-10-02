using System.Net;
using System.Net.Http.Json;
using PgmStudio.Api.Endpoints;
using PgmStudio.Contracts;
using PgmStudio.Minecraft.Painting;
using PgmStudio.Minecraft.Palette;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Tests;

/// <summary>
/// A library row's name (<see cref="LibraryNaming"/>): letters, digits, spaces, dashes and underscores, with no space
/// at either end and none doubled (<c>LB4</c>), and one row of its kind, compared without case (<c>LB5</c>) — on
/// every save. Runs against <c>pgm_studio_test</c>; resets the schema, so it runs serially with the rest.
/// </summary>
[NotInParallel("api-db")]
public sealed class LibraryNamingTests
{
    [Test]
    [Arguments("grass clay surface", true)]
    [Arguments("cobblestone-andesite-noise", true)]
    [Arguments("oak_2", true)]
    [Arguments("Mesa (Bryce)", false)]
    [Arguments("barn · roof", false)]
    [Arguments(" leading", false)]
    [Arguments("trailing ", false)]
    [Arguments("two  spaces", false)]
    [Arguments("", false)]
    public async Task A_name_is_letters_digits_spaces_dashes_and_underscores(string name, bool valid)
        => await Assert.That(LibraryNaming.Valid(name)).IsEqualTo(valid);

    [Test]
    [Arguments("Mesa (Bryce)", "Mesa Bryce")]
    [Arguments("Extreme hills+ M", "Extreme hills plus M")]
    [Arguments("barn · roof", "barn roof")]
    [Arguments("  ", "unnamed")]
    public async Task Text_tidied_is_a_name(string text, string tidy)
    {
        await Assert.That(LibraryNaming.Tidy(text)).IsEqualTo(tidy);
        await Assert.That(LibraryNaming.Valid(LibraryNaming.Tidy(text))).IsTrue();
    }

    [Test]
    public async Task A_name_holding_other_characters_is_refused()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        var refused = await client.PostAsJsonAsync("/api/themes",
            new ThemeSaveRequest("dusk & dawn", false, 1, RimEdgeModes.Drop, true, []));
        await Assert.That(refused.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        var why = await refused.Content.ReadFromJsonAsync<RefusalDto>();
        await Assert.That(why!.Findings.Single().Rule).IsEqualTo("LB4");
    }

    [Test]
    public async Task A_name_its_kind_already_carries_is_refused_and_a_row_keeps_its_own()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        var first = await (await client.PostAsJsonAsync("/api/themes",
            new ThemeSaveRequest("Frost night", false, 1, RimEdgeModes.Drop, true, [])))
            .Content.ReadFromJsonAsync<ThemeDetail>();

        var taken = await client.PostAsJsonAsync("/api/themes",
            new ThemeSaveRequest("frost NIGHT", false, 1, RimEdgeModes.Drop, true, []));
        await Assert.That(taken.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
        var why = await taken.Content.ReadFromJsonAsync<RefusalDto>();
        await Assert.That(why!.Findings.Single().Rule).IsEqualTo("LB5");
        await Assert.That(why.Findings.Single().SubjectIds).Contains("Frost night");

        var kept = await client.PutAsJsonAsync($"/api/themes/{first!.Id}",
            new ThemeSaveRequest("Frost night", true, 2, RimEdgeModes.Drop, true, []));
        await Assert.That(kept.StatusCode).IsEqualTo(HttpStatusCode.OK);

        // One name is one row of its kind: another kind may carry it.
        var pattern = await client.PostAsJsonAsync("/api/styles", new StyleSaveRequest("Frost night", MaterialKind.Noise,
            TerrainThemeJson.Serialize(new NoiseMaterial(3, 2, 1, [new SolidMaterial(80), new SolidMaterial(79)]))));
        await Assert.That(pattern.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task An_unnamed_import_counts_on_from_the_one_the_library_holds()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        var themeJson = TerrainThemeJson.Serialize(TerrainTheme.Default);
        foreach (var _ in Enumerable.Range(0, 2))
            await Assert.That((await client.PostAsJsonAsync("/api/themes/import", new ThemeImportRequest(null, themeJson)))
                .StatusCode).IsEqualTo(HttpStatusCode.OK);
        var names = (await client.GetFromJsonAsync<List<ThemeSummary>>("/api/themes"))!.Select(theme => theme.Name);
        await Assert.That(names).IsEquivalentTo(["Imported theme", "Imported theme-2"]);
    }
}
