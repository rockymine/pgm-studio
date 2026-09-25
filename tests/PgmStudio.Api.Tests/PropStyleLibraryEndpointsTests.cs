using System.Net.Http.Json;
using PgmStudio.Contracts;
using PgmStudio.Minecraft.Dressing;
using PgmStudio.Minecraft.Palette;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Tests;

/// <summary>
/// The tree library's save gate: a <c>copied</c> recipe is one cut out of a world, so a save claiming the form
/// states where it was cut or is refused, and a cut that is stated is stored and read back. Runs against
/// <c>pgm_studio_test</c>; each test resets the schema, so they run serially.
/// </summary>
[NotInParallel("api-db")]
public sealed class PropStyleLibraryEndpointsTests
{
    private static readonly int[][] Body =
    [
        [0, 0, 0, Blocks.Log, 0], [0, 1, 0, Blocks.Log, 0], [0, 2, 0, Blocks.Leaves, 0],
    ];

    private static readonly TreeCut Cut =
        new("/repos/pgm-studio-mapgen/corpus/tree-showcase", -112, 64, 38, new DateTime(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc));

    private static TreeStyleSaveRequest Copied(string name, TreeCut? cut)
        => new(name, TreeForms.Copied, "oak", Height: 12, Body, cut);

    [Test]
    public async Task A_copied_tree_with_no_cut_is_refused()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        var refused = await client.PostAsJsonAsync("/api/tree-styles", Copied("crate", cut: null));
        await Assert.That((int)refused.StatusCode).IsEqualTo(400);
        var envelope = await refused.Content.ReadFromJsonAsync<RefusalDto>();
        await Assert.That(envelope!.Findings.Select(f => f.Rule)).Contains(DressingRules.UncutCopy);

        var listed = await client.GetFromJsonAsync<List<TreeStyleSummary>>("/api/tree-styles");
        await Assert.That(listed!.Any(tree => tree.Name == "crate")).IsFalse();
    }

    [Test]
    public async Task A_copied_tree_stores_its_cut_and_a_resave_must_carry_it()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        var created = await (await client.PostAsJsonAsync("/api/tree-styles", Copied("showcase-r1-1", Cut)))
            .Content.ReadFromJsonAsync<TreeStyleDetail>();
        var detail = await client.GetFromJsonAsync<TreeStyleDetail>($"/api/tree-styles/{created!.Id}");
        await Assert.That(detail!.Cut).IsEqualTo(Cut);

        var stripped = await client.PutAsJsonAsync($"/api/tree-styles/{detail.Id}", Copied("renamed", cut: null));
        await Assert.That((int)stripped.StatusCode).IsEqualTo(400);

        var renamed = await client.PutAsJsonAsync($"/api/tree-styles/{detail.Id}",
            new TreeStyleSaveRequest("renamed", detail.Form, detail.Species, detail.Height, detail.Body, detail.Cut));
        await Assert.That((int)renamed.StatusCode).IsEqualTo(200);
        var reread = await client.GetFromJsonAsync<TreeStyleDetail>($"/api/tree-styles/{detail.Id}");
        await Assert.That(reread!.Name).IsEqualTo("renamed");
        await Assert.That(reread.Cut).IsEqualTo(Cut);
    }

    [Test]
    public async Task A_template_tree_needs_no_cut_and_keeps_none()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        var saved = await client.PostAsJsonAsync("/api/tree-styles",
            new TreeStyleSaveRequest("oak", TreeForms.Template, "oak", Height: 12, Body: null, Cut));
        await Assert.That((int)saved.StatusCode).IsEqualTo(200);
        var detail = await saved.Content.ReadFromJsonAsync<TreeStyleDetail>();
        await Assert.That(detail!.Cut).IsNull();
    }
}
