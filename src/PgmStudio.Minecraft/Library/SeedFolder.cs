using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using PgmStudio.Minecraft.Dressing;
using PgmStudio.Minecraft.Houses;
using PgmStudio.Minecraft.Painting;

namespace PgmStudio.Minecraft.Library;

/// <summary>
/// What a fresh library holds, read out of the data beside this file: <c>patterns.json</c>, a theme per file
/// under <c>themes/</c>, a house per file under <c>houses/</c>, <c>boulders.json</c>, and <c>trees.json</c>, the
/// trees cut out of the showcase world. One seeder reads it and nothing else states a seeded row.
///
/// <para>Every entry is in its own document's form — a pattern is a material, a theme the painter's theme, a
/// house the stamper's style, a recipe the dressing's — so a seeded row is a row an author could have written
/// and a board can copy one without the library. The roofs, storeys and porches a library lists are not stated
/// here: they are the houses' own, cut out of them by the seeder.</para>
/// </summary>
public static class SeedFolder
{
    private const string Prefix = "library/";

    /// <summary>The patterns, under the names they describe themselves by, the author's own ground patterns
    /// first. Every pattern a seeded house or theme lays is one of these.</summary>
    public static IReadOnlyList<(string Name, TerrainMaterial Material)> Patterns => patterns.Value;

    /// <summary>The terrain finishes, by name.</summary>
    public static IReadOnlyList<(string Name, TerrainTheme Theme)> Themes => themes.Value;

    /// <summary>The house styles, by name.</summary>
    public static IReadOnlyList<(string Name, HouseStyle Style)> Houses => houses.Value;

    /// <summary>The boulder recipes, by name.</summary>
    public static IReadOnlyList<(string Name, BoulderStyle Style)> Boulders => boulders.Value;

    /// <summary>The copied trees: the world they were cut from, and each tree with the foot it stood on there.</summary>
    public static CutTrees Trees => trees.Value;

    /// <summary>The ground every preview stands its sample on: grass over dirt, mottled.</summary>
    public static TerrainTheme Meadow => Theme("meadow");

    /// <summary>One theme by name; throws for a name the folder does not hold.</summary>
    public static TerrainTheme Theme(string name) => Themes.Single(theme => theme.Name == name).Theme;

    /// <summary>One house by name; throws for a name the folder does not hold.</summary>
    public static HouseStyle House(string name) => Houses.Single(house => house.Name == name).Style;

    private static readonly Lazy<IReadOnlyList<(string, TerrainMaterial)>> patterns = new(() =>
        [.. Named(Read("patterns.json")).Select(entry => (entry.Name, TerrainThemeJson.DeserializeMaterial(entry.Json)))]);

    private static readonly Lazy<IReadOnlyList<(string, TerrainTheme)>> themes = new(() =>
        [.. Files("themes/").Select(file => (file.Name, TerrainThemeJson.Deserialize(file.Json)))]);

    private static readonly Lazy<IReadOnlyList<(string, HouseStyle)>> houses = new(() =>
        [.. Files("houses/").Select(file => (file.Name, HouseStyleJson.Deserialize(file.Json)))]);

    private static readonly Lazy<IReadOnlyList<(string, BoulderStyle)>> boulders = new(() =>
        [.. Named(Read("boulders.json")).Select(entry => (entry.Name, (BoulderStyle)Style(entry.Json)))]);

    private static readonly Lazy<CutTrees> trees = new(() =>
    {
        var cut = JsonNode.Parse(Read("trees.json"))!.AsObject();
        return new CutTrees(
            cut["world"]!.GetValue<string>(),
            [.. cut["trees"]!.AsObject().Select(entry =>
            {
                var foot = entry.Value!["foot"]!.AsArray().Select(axis => axis!.GetValue<int>()).ToArray();
                return new CutTree(entry.Key, (foot[0], foot[1], foot[2]),
                    (TreeStyle)Style(entry.Value["style"]!.ToJsonString()));
            })]);
    });

    private static PropStyle Style(string json)
        => JsonSerializer.Deserialize<PropStyle>(json, DressingJson.Options)
           ?? throw new InvalidDataException("a seeded recipe is null");

    /// <summary>A document whose members are entries, in the order it states them.</summary>
    private static IEnumerable<(string Name, string Json)> Named(string json)
        => JsonNode.Parse(json)!.AsObject().Select(entry => (entry.Key, entry.Value!.ToJsonString()));

    private static IEnumerable<(string Name, string Json)> Files(string folder)
        => Assembly.GetManifestResourceNames()
            .Where(resource => resource.StartsWith(Prefix + folder, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(resource => (resource[(Prefix + folder).Length..^".json".Length], ReadResource(resource)));

    private static string Read(string file) => ReadResource(Prefix + file);

    private static string ReadResource(string resource)
    {
        using var stream = Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidDataException($"the seed folder holds no {resource}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static Assembly Assembly => typeof(SeedFolder).Assembly;
}

/// <summary>The trees one world was cut into, each under its name with the foot it stood on.</summary>
public sealed record CutTrees(string World, IReadOnlyList<CutTree> Trees);

/// <summary>One tree cut out of a world: its name, the foot it stood on, and the recipe it is.</summary>
public sealed record CutTree(string Name, (int X, int Y, int Z) Foot, TreeStyle Style);
