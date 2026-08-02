using PgmStudio.Data.Schema;
using PgmStudio.Data.Theme;
using PgmStudio.Minecraft;

namespace PgmStudio.Api.Services;

/// <summary>
/// The composition-root half of the theme/style library (B44): it bridges the row store
/// (<see cref="ThemeStore"/>) and the painter's material model (<see cref="TerrainThemeComposer"/> /
/// <see cref="TerrainThemeJson"/>), which live in different layers. <see cref="ComposeJsonAsync"/> assembles a
/// library theme's rows back into the exact theme JSON the painter consumes; <see cref="ImportAsync"/> takes a
/// whole theme JSON and decomposes it into one style per bucket plus a composed theme, so an existing inline
/// theme (or the built-in default) can be lifted into the library.
/// </summary>
public sealed class ThemeLibrary(ThemeStore store)
{
    /// <summary>A library theme assembled into the painter's theme JSON, or null when the theme id is unknown.</summary>
    public async Task<string?> ComposeJsonAsync(long themeId, CancellationToken ct = default)
    {
        var theme = await store.GetThemeAsync(themeId, ct);
        if (theme is null) return null;

        var bindings = (await store.GetBucketStylesAsync(themeId, ct))
            .Select(bs => new ThemeStyleBinding(ToBucket(bs.Bucket.Bucket), bs.Style.Kind, bs.Style.Params, bs.Bucket.Depth, bs.Bucket.Enabled))
            .ToList();

        var decomposed = new DecomposedTheme(theme.BedrockRelative, theme.BedrockValue, theme.Closed, theme.WallOnTerrainFaces, bindings);
        return TerrainThemeJson.Serialize(TerrainThemeComposer.Compose(decomposed));
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
            var styleId = await store.CreateStyleAsync(
                new StyleRow { Name = $"{name} · {bucketName}", Kind = binding.Kind, Params = binding.MaterialJson }, ct);
            buckets.Add(new ThemeBucketRow { Bucket = bucketName, StyleId = styleId, Depth = binding.Depth, Enabled = binding.Enabled });
        }

        var themeRow = new ThemeRow
        {
            Name = name,
            BedrockRelative = decomposed.BedrockRelative, BedrockValue = decomposed.BedrockValue,
            Closed = decomposed.Closed, WallOnTerrainFaces = decomposed.WallOnTerrainFaces,
        };
        return await store.CreateThemeAsync(themeRow, buckets, ct);
    }

    // The store's bucket string (ThemeBucket.*) ↔ the painter's TerrainBucket enum.
    private static TerrainBucket ToBucket(string bucket) => bucket switch
    {
        ThemeBucket.Rim => TerrainBucket.Rim,
        ThemeBucket.Surface => TerrainBucket.Surface,
        ThemeBucket.Wall => TerrainBucket.Wall,
        _ => TerrainBucket.Fill,
    };

    private static string FromBucket(TerrainBucket bucket) => bucket switch
    {
        TerrainBucket.Rim => ThemeBucket.Rim,
        TerrainBucket.Surface => ThemeBucket.Surface,
        TerrainBucket.Wall => ThemeBucket.Wall,
        _ => ThemeBucket.Fill,
    };
}
