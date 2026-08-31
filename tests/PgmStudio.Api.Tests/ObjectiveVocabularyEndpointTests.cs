using System.Net.Http.Json;
using System.Text.Json;
using PgmStudio.Domain;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Tests;

/// <summary>
/// GET /api/objectives/vocabulary — what the plan tool's objective inspector is allowed to offer, and what
/// each field falls back to when a marker leaves it unset.
///
/// <para>The reason it is served at all is the reason to test it: the Blazor client cannot reach
/// <see cref="ObjectiveDefaults"/>, so these numbers are the only thing standing between an author and a
/// picker that shows a default the stamper does not build. Each assertion compares against the constant
/// rather than a literal, so the endpoint cannot drift from the generator — only both together.</para>
///
/// <para>Reads no map and touches no row, but booting the host still checks the schema — so it joins the
/// serialised group rather than starting while another class is dropping every table.</para>
/// </summary>
[NotInParallel("api-db")]
public sealed class ObjectiveVocabularyEndpointTests
{
    private static async Task<JsonElement> GetAsync()
    {
        using var client = ApiTestFactory.Shared.CreateClient();
        return await client.GetFromJsonAsync<JsonElement>("/api/objectives/vocabulary");
    }

    [Test]
    public async Task The_core_defaults_are_the_generators_own()
    {
        var core = (await GetAsync()).GetProperty("core");
        await Assert.That(core.GetProperty("lava").GetInt32()).IsEqualTo(ObjectiveDefaults.CoreLava);
        await Assert.That(core.GetProperty("lavaHeight").GetInt32()).IsEqualTo(ObjectiveDefaults.CoreLavaHeight);
        // The offered footprints and heights travel with the defaults: a core is chosen from them, so a
        // client that had to invent the list would be inventing the vocabulary.
        await Assert.That(core.GetProperty("lavaRange").GetArrayLength()).IsEqualTo(4);
        await Assert.That(core.GetProperty("lavaHeightRange").GetArrayLength()).IsEqualTo(4);
        await Assert.That(core.GetProperty("float").GetInt32()).IsEqualTo(ObjectiveDefaults.CoreFloat);
        await Assert.That(core.GetProperty("leak").GetInt32()).IsEqualTo(ObjectiveDefaults.CoreLeak);
        // A capped casing is the norm; open-top is the minority style, so it is a flag rather than a default.
        await Assert.That(core.GetProperty("openTop").GetBoolean()).IsFalse();
    }

    [Test]
    public async Task The_destroyable_defaults_are_the_generators_own()
    {
        var destroyable = (await GetAsync()).GetProperty("destroyable");
        await Assert.That(destroyable.GetProperty("style").GetString())
            .IsEqualTo(DestroyableStyles.Slug(ObjectiveDefaults.Style));
        await Assert.That(destroyable.GetProperty("materials").GetString()).IsEqualTo(ObjectiveDefaults.Materials);
        await Assert.That(destroyable.GetProperty("float").GetInt32()).IsEqualTo(ObjectiveDefaults.DestroyableFloat);
    }

    [Test]
    public async Task The_offered_designs_are_every_style_the_generator_knows()
    {
        var destroyable = (await GetAsync()).GetProperty("destroyable");
        var styles = destroyable.GetProperty("styles").EnumerateArray().Select(s => s.GetString()).ToList();
        await Assert.That(styles).IsEquivalentTo(DestroyableStyles.All.ToList());

        // A picker's default has to be one of its own options, or the select renders with nothing chosen.
        await Assert.That(styles).Contains(destroyable.GetProperty("style").GetString());
    }

    [Test]
    public async Task The_offered_materials_are_the_ones_the_stamper_can_build()
    {
        var destroyable = (await GetAsync()).GetProperty("destroyable");
        var materials = destroyable.GetProperty("materialChoices").EnumerateArray().Select(m => m.GetString()).ToList();
        await Assert.That(materials).IsEquivalentTo(DestroyableMaterials.All.ToList());
        await Assert.That(materials).Contains(destroyable.GetProperty("materials").GetString());
    }

    [Test]
    public async Task The_offered_dyes_are_every_colour_a_wool_may_be()
    {
        var wool = (await GetAsync()).GetProperty("wool");
        var colors = wool.GetProperty("colors").EnumerateArray().Select(c => c.GetProperty("name").GetString()).ToList();
        await Assert.That(colors).IsEquivalentTo(WoolColors.All.ToList());

        // Each dye carries its own swatch: an inspector that had to derive a colour from a name would be
        // inventing one, and the plan board's marker would then disagree with the picker beside it.
        foreach (var dye in wool.GetProperty("colors").EnumerateArray())
            await Assert.That(dye.GetProperty("hex").GetString())
                .IsEqualTo(WoolColors.Swatch[dye.GetProperty("name").GetString()!]);
    }

    [Test]
    public async Task A_wool_offers_no_default_colour_because_the_compiler_resolves_one()
    {
        // Unlike a destroyable's design there is no colour to show for an unstated wool: the compiler reads
        // the owning team and the wools before it. The vocabulary says so with a word rather than a colour,
        // and that word must not be a dye — an author picking it would be picking a real colour by accident.
        var wool = (await GetAsync()).GetProperty("wool");
        var auto = wool.GetProperty("auto").GetString();
        await Assert.That(auto).IsNotNull();
        await Assert.That(WoolColors.IsColor(auto)).IsFalse();
    }

    [Test]
    public async Task It_answers_without_a_map()
    {
        // The plan editor opens on /plan-editor with no map behind it, so a map-scoped route could not serve
        // the inspector at all.
        using var client = ApiTestFactory.Shared.CreateClient();
        var resp = await client.GetAsync("/api/objectives/vocabulary");
        await Assert.That(resp.IsSuccessStatusCode).IsTrue();
    }
}
