using PgmStudio.Contracts;
using PgmStudio.Data.Schema;
using PgmStudio.Data.Theme;
using PgmStudio.Minecraft;
using PgmStudio.Minecraft.Painting;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Services;

/// <summary>
/// The composition-root half of the theme/style library (B44): it bridges the row store
/// (<see cref="ThemeStore"/>) and the painter's material model (<see cref="TerrainThemeComposer"/> /
/// <see cref="TerrainThemeJson"/>), which live in different layers. <see cref="ComposeAsync"/> assembles a
/// library theme's rows back into the theme the painter consumes (and <see cref="ComposeJsonAsync"/> into the
/// exact JSON a map snapshots); <see cref="ImportAsync"/> takes a whole theme JSON and decomposes it into a
/// block or a pattern per bucket plus a composed theme, so a theme a board or an agent already holds inline can
/// be lifted into the library.
/// </summary>
public sealed class ThemeLibrary(ThemeStore store)
{
    /// <summary>A library theme assembled into the theme the painter consumes, or null when the id is unknown.</summary>
    public async Task<TerrainTheme?> ComposeAsync(long themeId, CancellationToken ct = default)
    {
        var theme = await store.GetThemeAsync(themeId, ct);
        if (theme is null) return null;
        return Compose(theme, await store.GetBucketStylesAsync(themeId, ct));
    }

    /// <summary>A library theme assembled into the painter's theme JSON, or null when the theme id is unknown.</summary>
    public async Task<string?> ComposeJsonAsync(long themeId, CancellationToken ct = default)
        => await ComposeAsync(themeId, ct) is { } theme ? TerrainThemeJson.Serialize(theme) : null;

    /// <summary>Every library theme with the painter theme it composes to, newest first — the whole library in
    /// two reads, so listing themes with a picture of each does not cost a query per row.</summary>
    public async Task<List<(ThemeRow Row, TerrainTheme Theme)>> ComposeAllAsync(CancellationToken ct = default)
    {
        var rows = await store.ListThemesAsync(ct);
        var bindings = (await store.GetAllBucketStylesAsync(ct)).ToLookup(binding => binding.Bucket.ThemeId);
        return rows.Select(row => (row, Compose(row, bindings[row.Id].ToList()))).ToList();
    }

    /// <summary>The theme a set of bindings composes to without any of it being saved — what a theme editor
    /// previews while it is being assembled. A bucket bound to no style (or to one this library no longer
    /// holds) resolves to stone, the composer's own fallback — but its depth and its toggle still apply, so a
    /// preview shows the rim switched off without a rim material having been chosen.</summary>
    public async Task<TerrainTheme> ComposeDraftAsync(ThemeSaveRequest draft, CancellationToken ct = default)
    {
        var styles = (await store.GetStylesAsync(draft.Buckets.Select(b => b.StyleId), ct))
            .ToDictionary(style => style.Id);
        var bindings = draft.Buckets
            .Select(binding => Binding(
                binding.Bucket, binding.Block, styles.GetValueOrDefault(binding.StyleId), binding.Depth, binding.Enabled))
            .ToList();
        return TerrainThemeComposer.Compose(new DecomposedTheme(
            draft.BedrockRelative, draft.BedrockValue, ToRimEdges(draft.RimEdges), draft.WallOnTerrainFaces, bindings));
    }

    /// <summary>Decompose a whole theme JSON into the library and return the new theme id. A bucket filled with
    /// one block binds that block; a bucket filled with a pattern binds the library's own copy of it where the
    /// library already holds one, and a new pattern named for what it contains (<see cref="PatternNames"/>)
    /// where it does not. Throws on invalid JSON.</summary>
    public async Task<long> ImportAsync(string name, string themeJson, CancellationToken ct = default)
    {
        var decomposed = TerrainThemeComposer.Decompose(TerrainThemeJson.Deserialize(themeJson));

        var buckets = new List<ThemeBucketRow>();
        foreach (var binding in decomposed.Buckets)
        {
            var row = new ThemeBucketRow { Bucket = FromBucket(binding.Bucket), Depth = binding.Depth, Enabled = binding.Enabled };
            // A binding that names no material binds nothing: its depth and toggle are the whole of what it says.
            if (binding is { MaterialJson: { } json })
            {
                var material = TerrainThemeJson.DeserializeMaterial(json);
                if (Slots.AsBlock(material) is { } block)
                    (row.BlockId, row.BlockData, row.BlockLaid) = (block.Id, block.Data, block.Laid);
                else row.StyleId = await PatternOfAsync(material, ct);
            }
            buckets.Add(row);
        }

        var themeRow = new ThemeRow
        {
            Name = name,
            BedrockRelative = decomposed.BedrockRelative, BedrockValue = decomposed.BedrockValue,
            RimEdges = FromRimEdges(decomposed.RimEdges), WallOnTerrainFaces = decomposed.WallOnTerrainFaces,
        };
        return await store.CreateThemeAsync(themeRow, buckets, ct);
    }

    /// <summary>The library's pattern holding exactly <paramref name="material"/>, created under the name it
    /// describes itself by when there is none.</summary>
    public async Task<long> PatternOfAsync(TerrainMaterial material, CancellationToken ct = default)
    {
        var content = TerrainThemeJson.Serialize(material);
        var styles = await store.ListStylesAsync(ct: ct);
        if (styles.FirstOrDefault(style => ContentOf(style.Params) == content) is { } held) return held.Id;
        var taken = styles.Select(style => style.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return await store.CreateStyleAsync(new StyleRow
        {
            Name = PatternNames.Unique(PatternNames.Describe(material), taken),
            Kind = TerrainThemeComposer.KindOf(material),
            Params = content,
        }, ct);
    }

    /// <summary>A pattern's params in the one form two equal patterns share: read and written back by the
    /// painter's own serializer, so key order and spacing say nothing. Params this build cannot read are their
    /// own form, equal only to themselves.</summary>
    public static string ContentOf(string paramsJson)
    {
        try { return TerrainThemeJson.Serialize(TerrainThemeJson.DeserializeMaterial(paramsJson)); }
        catch { return paramsJson; }
    }

    /// <summary>One bucket as the composer reads it: the block it holds, else the pattern it binds, else
    /// nothing.</summary>
    private static ThemeStyleBinding Binding(string bucket, SlotBlockDto? block, StyleRow? style, int depth, bool enabled)
    {
        if (block is not null)
        {
            var (kind, json) = Slots.Serialized(block);
            return new ThemeStyleBinding(ToBucket(bucket), kind, json, depth, enabled);
        }
        return new ThemeStyleBinding(ToBucket(bucket), style?.Kind, style?.Params, depth, enabled);
    }

    private static TerrainTheme Compose(ThemeRow theme, IReadOnlyList<(ThemeBucketRow Bucket, StyleRow? Style)> bindings)
    {
        var styleBindings = bindings
            .Select(binding => Binding(
                binding.Bucket.Bucket,
                Slots.BlockOf(binding.Bucket.BlockId, binding.Bucket.BlockData, binding.Bucket.BlockLaid),
                binding.Style, binding.Bucket.Depth, binding.Bucket.Enabled))
            .ToList();
        return TerrainThemeComposer.Compose(new DecomposedTheme(
            theme.BedrockRelative, theme.BedrockValue, ToRimEdges(theme.RimEdges), theme.WallOnTerrainFaces,
            styleBindings));
    }

    // The stored / wire rim-edge word (RimEdgeModes.*) ↔ the painter's RimEdges enum. An unknown word reads as
    // the default rather than throwing, the same tolerance every other hand-editable vocabulary gets.
    internal static RimEdges ToRimEdges(string? mode) => RimEdgeModes.Canonical(mode) switch
    {
        RimEdgeModes.Void => RimEdges.Void,
        RimEdgeModes.Boundary => RimEdges.Boundary,
        _ => RimEdges.Drop,
    };

    internal static string FromRimEdges(RimEdges edges) => edges switch
    {
        RimEdges.Void => RimEdgeModes.Void,
        RimEdges.Boundary => RimEdgeModes.Boundary,
        _ => RimEdgeModes.Drop,
    };

    // The stored bucket string (ThemeBuckets.*) ↔ the painter's TerrainBucket enum.
    private static TerrainBucket ToBucket(string bucket) => bucket switch
    {
        ThemeBuckets.Rim => TerrainBucket.Rim,
        ThemeBuckets.Surface => TerrainBucket.Surface,
        ThemeBuckets.Wall => TerrainBucket.Wall,
        _ => TerrainBucket.Fill,
    };

    internal static string FromBucket(TerrainBucket bucket) => bucket switch
    {
        TerrainBucket.Rim => ThemeBuckets.Rim,
        TerrainBucket.Surface => ThemeBuckets.Surface,
        TerrainBucket.Wall => ThemeBuckets.Wall,
        _ => ThemeBuckets.Fill,
    };
}
