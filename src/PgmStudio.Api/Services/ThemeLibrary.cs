using PgmStudio.Contracts;
using PgmStudio.Data.Schema;
using PgmStudio.Data.Theme;
using PgmStudio.Minecraft;

namespace PgmStudio.Api.Services;

/// <summary>
/// The composition-root half of the theme/style library (B44): it bridges the row store
/// (<see cref="ThemeStore"/>) and the painter's material model (<see cref="TerrainThemeComposer"/> /
/// <see cref="TerrainThemeJson"/>), which live in different layers. <see cref="ComposeAsync"/> assembles a
/// library theme's rows back into the theme the painter consumes (and <see cref="ComposeJsonAsync"/> into the
/// exact JSON a map snapshots); <see cref="ImportAsync"/> takes a whole theme JSON and decomposes it into one
/// style per bucket plus a composed theme, so an existing inline theme (or the built-in default) can be lifted
/// into the library.
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
    /// holds) keeps the built-in material, which is the composer's own fallback — but its depth and its toggle
    /// still apply, so a preview shows the rim switched off without a rim material having been chosen.</summary>
    public async Task<TerrainTheme> ComposeDraftAsync(ThemeSaveRequest draft, CancellationToken ct = default)
    {
        var styles = (await store.GetStylesAsync(draft.Buckets.Select(b => b.StyleId), ct))
            .ToDictionary(style => style.Id);
        var bindings = draft.Buckets
            .Select(binding =>
            {
                var style = styles.GetValueOrDefault(binding.StyleId);
                return new ThemeStyleBinding(
                    ToBucket(binding.Bucket), style?.Kind, style?.Params, binding.Depth, binding.Enabled);
            })
            .ToList();
        return TerrainThemeComposer.Compose(new DecomposedTheme(
            draft.BedrockRelative, draft.BedrockValue, ToRimEdges(draft.RimEdges), draft.WallOnTerrainFaces, bindings));
    }

    /// <summary>Decompose a whole theme JSON into the library: one style per bucket (named
    /// "&lt;name&gt; · &lt;bucket&gt;"), then a theme binding them. Returns the new theme id. Throws on invalid JSON.</summary>
    public async Task<long> ImportAsync(string name, string themeJson, CancellationToken ct = default)
    {
        var decomposed = TerrainThemeComposer.Decompose(TerrainThemeJson.Deserialize(themeJson));

        var buckets = new List<ThemeBucketRow>();
        foreach (var binding in decomposed.Buckets)
        {
            var bucketName = FromBucket(binding.Bucket);
            // A binding that names no material makes no style: there is nothing to save and nothing to reuse,
            // and the binding's depth and toggle are the whole of what it says.
            long? styleId = binding is { Kind: { } kind, MaterialJson: { } material }
                ? await store.CreateStyleAsync(
                    new StyleRow { Name = $"{name} · {bucketName}", Kind = kind, Params = material }, ct)
                : null;
            buckets.Add(new ThemeBucketRow { Bucket = bucketName, StyleId = styleId, Depth = binding.Depth, Enabled = binding.Enabled });
        }

        var themeRow = new ThemeRow
        {
            Name = name,
            BedrockRelative = decomposed.BedrockRelative, BedrockValue = decomposed.BedrockValue,
            RimEdges = FromRimEdges(decomposed.RimEdges), WallOnTerrainFaces = decomposed.WallOnTerrainFaces,
        };
        return await store.CreateThemeAsync(themeRow, buckets, ct);
    }

    private static TerrainTheme Compose(ThemeRow theme, IReadOnlyList<(ThemeBucketRow Bucket, StyleRow? Style)> bindings)
    {
        var styleBindings = bindings
            .Select(binding => new ThemeStyleBinding(
                ToBucket(binding.Bucket.Bucket), binding.Style?.Kind, binding.Style?.Params,
                binding.Bucket.Depth, binding.Bucket.Enabled))
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

    private static string FromBucket(TerrainBucket bucket) => bucket switch
    {
        TerrainBucket.Rim => ThemeBuckets.Rim,
        TerrainBucket.Surface => ThemeBuckets.Surface,
        TerrainBucket.Wall => ThemeBuckets.Wall,
        _ => ThemeBuckets.Fill,
    };
}
