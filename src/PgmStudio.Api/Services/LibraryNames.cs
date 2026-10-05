using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PgmStudio.Contracts;
using PgmStudio.Data.Theme;
using PgmStudio.Minecraft.Dressing;
using PgmStudio.Minecraft.Houses;
using PgmStudio.Pgm.Plan;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Services;

/// <summary>A refinement with its library names resolved: the document the apply reads, every name replaced by a
/// copy of the row it names with the fields stated beside it laid over; the document the map keeps, each name
/// recorded with the row it resolved to and a hash of what was copied; the rows the refinement's themes and biome
/// were copied from; and a finding for every name that names no single row.</summary>
public sealed record LibraryResolved(
    string Applied, string Kept, IReadOnlyDictionary<string, long> ThemeRows, long? BiomeRow, Findings Findings);

/// <summary>
/// A refinement's library names: <c>{"library": "dunes"}</c> wherever a material, a theme, a room style, a prop
/// style or a biome is stated, naming a row of the studio's library by its name or its id, with the fields that
/// differ stated beside it. What the name stands for is decided by where it stands — an entry of <c>themes</c> is a
/// theme, of <c>roomStyles</c> a room style, a house prop's own <c>style</c> a room style too, of
/// <c>dressing.styles</c> the prop style its <c>kind</c> says, the <c>biome</c> a biome, and anything else a
/// material.
///
/// <para>A name is resolved at apply into the copy the stored layout holds, so a library edit never rebuilds a
/// stored board. The refinement the map keeps records each name with the row it resolved to and a hash of what
/// was copied, which is what lets <see cref="BehindAsync"/> say which rows have moved on since.</para>
/// </summary>
public sealed partial class LibraryNames(
    ThemeStore styles, ThemeLibrary themes, RoomStyleStore rooms, RoomStyleLibrary roomLibrary, PropStyleStore props)
{
    public const string Material = "material", Theme = "theme", RoomStyle = "room style", Tree = "tree",
        Boulder = "boulder", House = "house", Biome = "biome";

    /// <summary>The refinement's members a library name may stand in.</summary>
    private static readonly string[] Members =
        ["materials", "themes", "roomStyles", "dressing", "biome", "addShapes", "addLayers", "shapePropsById", "shapePropsByHeight"];


    public async Task<LibraryResolved> ResolveAsync(string refinementJson, CancellationToken ct)
    {
        var applied = JsonNode.Parse(refinementJson) as JsonObject ?? [];
        var kept = applied.DeepClone().AsObject();
        var resolution = new Resolution();
        foreach (var member in Members)
            if (applied[member] is { } node)
                await WalkAsync(node, member, kept[member]!, replacement => applied[member] = replacement, resolution, ct);
        return new(applied.ToJsonString(), kept.ToJsonString(), resolution.ThemeRows, resolution.BiomeRow,
            new Findings(resolution.Findings));
    }

    /// <summary>The layout a refinement built, with where its copies came from recorded where a board records it:
    /// a theme the refinement names from the library under <c>themeSources</c>, and a biome under
    /// <c>biomeSource</c>. A refinement stating its themes or its biome states the whole of them, so the records
    /// are replaced with them.</summary>
    public static string Recorded(string layoutJson, LibraryResolved named)
    {
        if (JsonNode.Parse(layoutJson) is not JsonObject layout
            || JsonNode.Parse(named.Applied) is not JsonObject refinement) return layoutJson;
        if (refinement.ContainsKey("themes"))
        {
            if (named.ThemeRows.Count > 0)
                layout["themeSources"] = new JsonObject(named.ThemeRows.Select(pair =>
                    KeyValuePair.Create(pair.Key, (JsonNode?)JsonValue.Create(pair.Value))));
            else layout.Remove("themeSources");
        }
        if (refinement.ContainsKey("biome"))
        {
            if (named.BiomeRow is { } row) layout["biomeSource"] = row;
            else layout.Remove("biomeSource");
        }
        return layout.ToJsonString();
    }

    /// <summary>The names a map's kept refinement holds whose row has moved on since the source was applied: the
    /// row composes to something other than what was copied, or is gone.</summary>
    public async Task<IReadOnlyList<LibraryBehindDto>> BehindAsync(string? keptJson, CancellationToken ct)
    {
        var behind = new List<LibraryBehindDto>();
        if (string.IsNullOrWhiteSpace(keptJson) || JsonNode.Parse(keptJson) is not JsonObject kept) return behind;
        foreach (var member in Members)
            if (kept[member] is { } node)
                foreach (var (path, named) in Named(node, member))
                {
                    if (named[StatedName.RowKey] is not JsonValue row || !row.TryGetValue<long>(out var id)) continue;
                    var kind = KindAt(path, named);
                    var now = await CopyAsync(kind, id, ct);
                    var hash = named[StatedName.HashKey]?.GetValue<string>();
                    if (now is null || Hashed(now.Value.Json) != hash)
                        behind.Add(new LibraryBehindDto(path, kind, id, now?.Name, Gone: now is null));
                }
        return behind;
    }

    private sealed class Resolution
    {
        public readonly Dictionary<string, long> ThemeRows = new(StringComparer.Ordinal);
        public long? BiomeRow;
        public readonly List<Finding> Findings = [];
    }

    // Pre-order: a name's copy is laid over with what was stated beside it, and a name inside that — a material
    // stated in a theme's override — is resolved in turn.
    private async Task WalkAsync(
        JsonNode node, string path, JsonNode kept, Action<JsonNode> replace, Resolution resolution, CancellationToken ct)
    {
        if (node is JsonObject stated && stated[StatedName.LibraryKey] is JsonValue name)
        {
            var kind = KindAt(path, stated);
            var found = await FindAsync(kind, name, path, resolution, ct);
            if (found is not { } copy) return;
            var beside = new JsonObject();
            foreach (var (key, value) in stated)
                if (key is not (StatedName.LibraryKey or StatedName.RowKey or StatedName.HashKey))
                    beside[key] = value?.DeepClone();
            var merged = Refinement.LaidOver(JsonNode.Parse(copy.Json), beside)!;
            replace(merged);
            if (kept is JsonObject record)
            {
                record[StatedName.RowKey] = copy.Id;
                record[StatedName.HashKey] = Hashed(copy.Json);
            }
            if (path.StartsWith("themes.", StringComparison.Ordinal) && path.Count(c => c == '.') == 1)
                resolution.ThemeRows[path["themes.".Length..]] = copy.Id;
            if (path == "biome") resolution.BiomeRow = copy.Id;
            node = merged;
        }

        switch (node)
        {
            case JsonObject members:
                foreach (var key in members.Select(pair => pair.Key).ToList())
                    if (members[key] is { } child && kept[key] is { } keptChild)
                        await WalkAsync(child, $"{path}.{key}", keptChild, replacement => members[key] = replacement,
                            resolution, ct);
                break;
            case JsonArray items:
                for (var at = 0; at < items.Count; at++)
                {
                    var index = at;
                    if (items[index] is { } child && kept is JsonArray keptItems && index < keptItems.Count
                        && keptItems[index] is { } keptChild)
                        await WalkAsync(child, $"{path}[{index}]", keptChild, replacement => items[index] = replacement,
                            resolution, ct);
                }
                break;
        }
    }

    private static IEnumerable<(string Path, JsonObject Named)> Named(JsonNode node, string path)
    {
        if (node is JsonObject named && named[StatedName.LibraryKey] is not null) yield return (path, named);
        switch (node)
        {
            case JsonObject members:
                foreach (var (key, child) in members)
                    if (child is not null)
                        foreach (var found in Named(child, $"{path}.{key}")) yield return found;
                break;
            case JsonArray items:
                for (var at = 0; at < items.Count; at++)
                    if (items[at] is { } child)
                        foreach (var found in Named(child, $"{path}[{at}]")) yield return found;
                break;
        }
    }

    // What a name stands for is decided by where it stands.
    private static string KindAt(string path, JsonObject stated)
    {
        var depth = path.Count(c => c == '.');
        if (path.StartsWith("themes.", StringComparison.Ordinal) && depth == 1) return Theme;
        if (path.StartsWith("roomStyles.", StringComparison.Ordinal) && depth == 1) return RoomStyle;
        if (HouseOwnStyle().IsMatch(path)) return RoomStyle;
        if (path == "biome") return Biome;
        if (path.StartsWith("dressing.styles.", StringComparison.Ordinal) && depth == 2)
            return stated["kind"]?.GetValue<string>() switch
            {
                "boulder" => Boulder,
                "house" => House,
                _ => Tree,
            };
        return Material;
    }

    private readonly record struct Copy(long Id, string Name, string Json);

    private async Task<Copy?> FindAsync(string kind, JsonValue name, string path, Resolution resolution, CancellationToken ct)
    {
        var rows = await RowsAsync(kind, ct);
        var match = name.TryGetValue<long>(out var id) ? rows.FirstOrDefault(row => row.Id == id)
            : name.TryGetValue<string>(out var called)
                ? rows.FirstOrDefault(row => string.Equals(row.Name, called, StringComparison.OrdinalIgnoreCase))
            : default;
        if (match.Name is not null) return await CopyAsync(kind, match.Id, ct);

        var said = name.TryGetValue<string>(out var text) ? $"'{text}'" : name.ToJsonString();
        resolution.Findings.Add(new Finding(SourceRules.NamesNoLibraryEntry,
            $"`{path}` names library entry {said}, which the library does not have",
            Field: $"refinement.{path}.library"));
        return null;
    }

    private async Task<List<(long Id, string Name)>> RowsAsync(string kind, CancellationToken ct) => kind switch
    {
        Material => [.. (await styles.ListStylesAsync(ct: ct)).Select(row => (row.Id, row.Name))],
        Theme => [.. (await styles.ListThemesAsync(ct)).Select(row => (row.Id, row.Name))],
        RoomStyle or House => [.. (await rooms.ListAsync(ct)).Select(row => (row.Id, row.Name))],
        Tree => [.. (await props.ListTreesAsync(ct)).Select(row => (row.Id, row.Name))],
        Boulder => [.. (await props.ListBouldersAsync(ct)).Select(row => (row.Id, row.Name))],
        _ => [.. (await styles.ListBiomesAsync(ct: ct)).Select(row => (row.Id, row.Name))],
    };

    // The row as a board holds a copy of it.
    private async Task<Copy?> CopyAsync(string kind, long id, CancellationToken ct)
    {
        switch (kind)
        {
            case Material:
                return await styles.GetStyleAsync(id, ct) is { } style ? new Copy(id, style.Name, style.Params) : null;
            case Theme:
                var theme = await styles.GetThemeAsync(id, ct);
                return theme is not null && await themes.ComposeJsonAsync(id, ct) is { } json
                    ? new Copy(id, theme.Name, json) : null;
            case RoomStyle or House:
                var room = await rooms.GetAsync(id, ct);
                if (room is null || await roomLibrary.ComposeAsync(id, ct) is not { } shell) return null;
                return new Copy(id, room.Name, kind == House
                    ? DressingJson.SerializeStyle(new HouseStyleRef { Shell = shell })
                    : HouseStyleJson.Serialize(shell));
            case Tree:
                return await props.GetTreeAsync(id, ct) is { } tree
                    ? new Copy(id, tree.Name, DressingJson.SerializeStyle(PropStyleLibrary.TreeOf(tree))) : null;
            case Boulder:
                return await props.GetBoulderAsync(id, ct) is { } boulder
                    ? new Copy(id, boulder.Name, DressingJson.SerializeStyle(PropStyleLibrary.BoulderOf(boulder))) : null;
            default:
                return await styles.GetBiomeAsync(id, ct) is { } biome ? new Copy(id, biome.Name, biome.Params) : null;
        }
    }

    // A house prop states its style in place, as the shell a room style composes to.
    [GeneratedRegex(@"^dressing\.props\[\d+\]\.style$")]
    private static partial Regex HouseOwnStyle();

    private static string Hashed(string json) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json)))[..16];
}
