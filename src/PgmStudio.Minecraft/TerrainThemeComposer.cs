namespace PgmStudio.Minecraft;

/// <summary>
/// A theme decomposed into the reusable pieces the library stores (B44, docs/world-export/finishing-model.md §3):
/// the geometry knobs, and one <see cref="ThemeStyleBinding"/> per themeable bucket — a bucket, the material
/// (as a serialized <em>style</em>), and the bucket's depth/toggle. Bedrock is fixed, never a bucket. This is the
/// shape a <c>theme</c> row + its <c>theme_bucket</c> rows (each pointing at a <c>style</c> row) reconstruct, and
/// it round-trips losslessly to and from the <see cref="TerrainTheme"/> the painter consumes.
/// </summary>
public sealed record DecomposedTheme(
    bool BedrockRelative, int BedrockValue, bool Closed, bool WallOnTerrainFaces,
    IReadOnlyList<ThemeStyleBinding> Buckets);

/// <summary>One bucket's binding in a <see cref="DecomposedTheme"/>: which bucket, the material as a
/// serialized style (its <see cref="Kind"/> discriminator and the <see cref="MaterialJson"/> a <c>style</c> row
/// stores), and the bucket's depth and toggle. <see cref="Depth"/> is meaningful for the top-claiming buckets
/// (rim/surface); wall and fill carry 0. <see cref="Enabled"/> is the rim/surface/wall toggle (fill is always on).</summary>
public sealed record ThemeStyleBinding(TerrainBucket Bucket, string Kind, string MaterialJson, int Depth, bool Enabled);

/// <summary>
/// Splits a <see cref="TerrainTheme"/> into its reusable library pieces and puts it back together — the pure
/// round-trip behind the theme/style tables (B44). <see cref="Decompose"/> yields one style per themeable bucket
/// (rim, surface, wall, fill) plus the theme's geometry knobs; <see cref="Compose"/> rebuilds the exact theme the
/// painter reads. No persistence here — the store maps these pieces to rows, and this stays a pure Minecraft-side
/// transform so the round-trip is testable without a database.
/// </summary>
public static class TerrainThemeComposer
{
    /// <summary>The four themeable buckets, in a fixed order (bedrock is fixed and never themed).</summary>
    public static readonly IReadOnlyList<TerrainBucket> ThemeableBuckets =
        [TerrainBucket.Rim, TerrainBucket.Surface, TerrainBucket.Wall, TerrainBucket.Fill];

    /// <summary>The <c>kind</c> discriminator a material serializes under — the value a <c>style.kind</c> column
    /// stores so a library can be queried by kind ("every voronoi").</summary>
    public static string KindOf(TerrainMaterial material) => material switch
    {
        SolidMaterial => "solid",
        LayeredMaterial => "layered",
        TeamTintedMaterial => "teamTint",
        VoronoiMaterial => "voronoi",
        NoiseMaterial => "noise",
        WallRunMaterial => "wallRun",
        _ => "solid",
    };

    /// <summary>Decompose a theme into its knobs + one style binding per themeable bucket.</summary>
    public static DecomposedTheme Decompose(TerrainTheme theme)
    {
        ThemeStyleBinding Bind(TerrainBucket bucket, TerrainMaterial material, int depth, bool enabled)
            => new(bucket, KindOf(material), TerrainThemeJson.Serialize(material), depth, enabled);

        return new DecomposedTheme(
            theme.Bedrock.Relative, theme.Bedrock.Value, theme.Closed, theme.WallOnTerrainFaces,
            [
                Bind(TerrainBucket.Rim, theme.Rim.Material, theme.Rim.Depth, theme.Rim.Enabled),
                Bind(TerrainBucket.Surface, theme.Surface.Material, theme.Surface.Depth, theme.Surface.Enabled),
                Bind(TerrainBucket.Wall, theme.Wall, depth: 0, theme.WallEnabled),
                Bind(TerrainBucket.Fill, theme.Fill, depth: 0, enabled: true),
            ]);
    }

    /// <summary>Rebuild the theme the painter consumes from a decomposed one. A missing bucket falls back to the
    /// shipping default for that bucket, so a partial decomposition never throws.</summary>
    public static TerrainTheme Compose(DecomposedTheme decomposed)
    {
        var byBucket = decomposed.Buckets.ToDictionary(b => b.Bucket);
        ThemeStyleBinding? Get(TerrainBucket bucket) => byBucket.TryGetValue(bucket, out var b) ? b : null;
        TerrainMaterial Material(ThemeStyleBinding? b) => b is null ? null! : TerrainThemeJson.DeserializeMaterial(b.MaterialJson);

        var def = TerrainTheme.Default;
        var rim = Get(TerrainBucket.Rim);
        var surface = Get(TerrainBucket.Surface);
        var wall = Get(TerrainBucket.Wall);
        var fill = Get(TerrainBucket.Fill);

        return new TerrainTheme
        {
            Bedrock = new BedrockSpec(decomposed.BedrockRelative, decomposed.BedrockValue),
            Closed = decomposed.Closed,
            WallOnTerrainFaces = decomposed.WallOnTerrainFaces,
            Rim = rim is null ? def.Rim : new TopBand(Material(rim), rim.Depth, rim.Enabled),
            Surface = surface is null ? def.Surface : new TopBand(Material(surface), surface.Depth, surface.Enabled),
            Wall = wall is null ? def.Wall : Material(wall),
            WallEnabled = wall?.Enabled ?? def.WallEnabled,
            Fill = fill is null ? def.Fill : Material(fill),
        };
    }
}
