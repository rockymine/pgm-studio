namespace PgmStudio.Minecraft;

/// <summary>
/// A theme decomposed into the reusable pieces the library stores (B44, docs/tools/library.md):
/// the geometry knobs, and one <see cref="ThemeStyleBinding"/> per themeable bucket — a bucket, the material
/// (as a serialized <em>style</em>), and the bucket's depth/toggle. Bedrock is fixed, never a bucket. This is the
/// shape a <c>theme</c> row + its <c>theme_bucket</c> rows (each pointing at a <c>style</c> row) reconstruct, and
/// it round-trips losslessly to and from the <see cref="TerrainTheme"/> the painter consumes.
/// </summary>
public sealed record DecomposedTheme(
    bool BedrockRelative, int BedrockValue, RimEdges RimEdges, bool WallOnTerrainFaces,
    IReadOnlyList<ThemeStyleBinding> Buckets);

/// <summary>One bucket's binding in a <see cref="DecomposedTheme"/>: which bucket, the material as a
/// serialized style (its <see cref="Kind"/> discriminator and the <see cref="MaterialJson"/> a <c>style</c> row
/// stores), and the bucket's depth and toggle. <see cref="Depth"/> is meaningful for the top-claiming buckets
/// (rim/surface); wall and fill carry 0. <see cref="Enabled"/> is the rim/surface/wall toggle (fill is always on).
/// <para>A <b>null</b> <see cref="MaterialJson"/> is a binding to no style: the bucket keeps the built-in
/// material and the binding is carried only for its depth and its toggle. That is what lets a theme say "no
/// rim" — switching the rim off needs no material for it, and demanding one to store the refusal would be
/// asking an author to pick the colour of something they have just said they do not want.</para></summary>
public sealed record ThemeStyleBinding(TerrainBucket Bucket, string? Kind, string? MaterialJson, int Depth, bool Enabled);

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
        CellMaterial => "cell",
        NoiseMaterial => "noise",
        TurbulenceMaterial => "turbulence",
        ElectricMaterial => "electric",
        WallRunMaterial => "wallRun",
        WallDiagonalMaterial => "wallDiagonal",
        CheckerMaterial => "checker",
        LogCheckerMaterial => "logChecker",
        LaidLogMaterial => "laidLog",
        WallFrameMaterial => "wallFrame",
        _ => "solid",
    };

    /// <summary>Decompose a theme into its knobs + one style binding per themeable bucket.</summary>
    public static DecomposedTheme Decompose(TerrainTheme theme)
    {
        ThemeStyleBinding Bind(TerrainBucket bucket, TerrainMaterial material, int depth, bool enabled)
            => new(bucket, KindOf(material), TerrainThemeJson.Serialize(material), depth, enabled);

        return new DecomposedTheme(
            theme.Bedrock.Relative, theme.Bedrock.Value, theme.RimEdges, theme.WallOnTerrainFaces,
            [
                Bind(TerrainBucket.Rim, theme.Rim.Material, theme.Rim.Depth, theme.Rim.Enabled),
                Bind(TerrainBucket.Surface, theme.Surface.Material, theme.Surface.Depth, theme.Surface.Enabled),
                Bind(TerrainBucket.Wall, theme.Wall, depth: 0, theme.WallEnabled),
                Bind(TerrainBucket.Fill, theme.Fill, depth: 0, enabled: true),
            ]);
    }

    /// <summary>Rebuild the theme the painter consumes from a decomposed one. A missing bucket — and a bucket
    /// bound to no style — falls back to the shipping default for that bucket, so a partial decomposition never
    /// throws. The difference between the two is the depth and the toggle: a missing bucket has none to apply,
    /// a styleless one still carries both, which is how "no rim" is stored.</summary>
    public static TerrainTheme Compose(DecomposedTheme decomposed)
    {
        var byBucket = decomposed.Buckets.ToDictionary(b => b.Bucket);
        ThemeStyleBinding? Get(TerrainBucket bucket) => byBucket.TryGetValue(bucket, out var b) ? b : null;
        TerrainMaterial? Material(ThemeStyleBinding? binding)
            => binding?.MaterialJson is { } json ? TerrainThemeJson.DeserializeMaterial(json) : null;

        var def = TerrainTheme.Default;
        var rim = Get(TerrainBucket.Rim);
        var surface = Get(TerrainBucket.Surface);
        var wall = Get(TerrainBucket.Wall);
        var fill = Get(TerrainBucket.Fill);

        return new TerrainTheme
        {
            Bedrock = new BedrockSpec(decomposed.BedrockRelative, decomposed.BedrockValue),
            RimEdges = decomposed.RimEdges,
            WallOnTerrainFaces = decomposed.WallOnTerrainFaces,
            Rim = rim is null ? def.Rim : new TopBand(Material(rim) ?? def.Rim.Material, rim.Depth, rim.Enabled),
            Surface = surface is null ? def.Surface : new TopBand(Material(surface) ?? def.Surface.Material, surface.Depth, surface.Enabled),
            Wall = Material(wall) ?? def.Wall,
            WallEnabled = wall?.Enabled ?? def.WallEnabled,
            Fill = Material(fill) ?? def.Fill,
        };
    }
}
