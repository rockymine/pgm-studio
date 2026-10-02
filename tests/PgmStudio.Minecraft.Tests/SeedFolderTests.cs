using System.Text.Json.Nodes;
using PgmStudio.Minecraft.Library;
using PgmStudio.Minecraft.Painting;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// The seed folder (<see cref="SeedFolder"/>): every entry reads, each kind names an entry once, a pattern is a
/// pattern — never a single block, never a second copy of another, never another with only its seed changed —
/// and the finishes are real themes rather than restatements of the unthemed default.
/// </summary>
public sealed class SeedFolderTests
{
    [Test]
    public async Task Every_kind_reads_and_names_each_entry_once()
    {
        await Assert.That(SeedFolder.Patterns.Count).IsGreaterThan(0);
        await Assert.That(SeedFolder.Themes.Count).IsGreaterThan(0);
        await Assert.That(SeedFolder.Houses.Count).IsGreaterThan(0);
        await Assert.That(SeedFolder.Boulders.Count).IsGreaterThan(0);
        await Assert.That(SeedFolder.Trees.Trees.Count).IsGreaterThan(0);

        foreach (var names in new[]
                 {
                     SeedFolder.Patterns.Select(entry => entry.Name), SeedFolder.Themes.Select(entry => entry.Name),
                     SeedFolder.Houses.Select(entry => entry.Name), SeedFolder.Boulders.Select(entry => entry.Name),
                     SeedFolder.Trees.Trees.Select(entry => entry.Name),
                 })
            await Assert.That(names.GroupBy(name => name, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1)
                .Select(group => group.Key)).IsEmpty();
    }

    [Test]
    public async Task No_pattern_is_a_single_block()
        => await Assert.That(SeedFolder.Patterns
                .Where(entry => entry.Material is SolidMaterial or LaidLogMaterial).Select(entry => entry.Name))
            .IsEmpty();

    [Test]
    public async Task No_two_patterns_hold_the_same_material()
        => await Assert.That(SeedFolder.Patterns
                .GroupBy(entry => TerrainThemeJson.Serialize(entry.Material))
                .Where(group => group.Count() > 1).Select(group => string.Join(" = ", group.Select(entry => entry.Name))))
            .IsEmpty();

    /// <summary>A seed rearranges which block lands where and leaves what a wall looks like, so two patterns
    /// differing only by it are one pattern. The author's own ground patterns are theirs and are not asked.</summary>
    [Test]
    public async Task No_two_patterns_differ_only_by_their_seed()
        => await Assert.That(SeedFolder.Patterns
                .GroupBy(entry => Seedless(entry.Material))
                .Where(group => group.Count() > 1).Select(group => string.Join(" ~ ", group.Select(entry => entry.Name))))
            .IsEmpty();

    [Test]
    public async Task Meadow_is_not_the_default()
    {
        // Rim and Surface, not Wall or Fill: those two are what an author actually sees change when they pick
        // Meadow over painting nothing, so a silent re-convergence would show here first.
        await Assert.That(SeedFolder.Meadow.Rim.Material).IsNotEqualTo(TerrainTheme.Default.Rim.Material);
        await Assert.That(SeedFolder.Meadow.Surface.Material).IsNotEqualTo(TerrainTheme.Default.Surface.Material);
    }

    private static string Seedless(TerrainMaterial material)
    {
        var node = JsonNode.Parse(TerrainThemeJson.Serialize(material))!;
        void Strip(JsonNode? at)
        {
            switch (at)
            {
                case JsonObject members:
                    members.Remove("seed");
                    foreach (var (_, child) in members) Strip(child);
                    break;
                case JsonArray items:
                    foreach (var item in items) Strip(item);
                    break;
            }
        }
        Strip(node);
        return node.ToJsonString();
    }
}
