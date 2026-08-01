namespace PgmStudio.Minecraft;

/// <summary>The five buckets every paintable terrain block sorts into (docs/world-export/terrain-painting.md
/// §3). <see cref="Fill"/> is the required base — it claims whatever no other bucket took.</summary>
public enum TerrainBucket { Bedrock, Fill, Wall, Surface, Rim }

/// <summary>Where a block sits for the material resolver: its world coordinate, its bucket, its depth below
/// the top of that bucket's band (0 = the band's top course) — the parameter a layered material (grass over
/// dirt) reads — and the <see cref="TeamData"/> of the team that owns the cell (a 0–15 wool/clay damage
/// nibble, -1 = neutral). A pattern (TP13) will read the fuller context; this is the base seam.</summary>
public readonly record struct BucketContext(int X, int Y, int Z, TerrainBucket Bucket, int DepthFromTop, int TeamData = -1)
{
    /// <summary>Whether the cell belongs to a team (a colour is available for a team-tinted material).</summary>
    public bool HasTeam => TeamData >= 0;
}

/// <summary>
/// A bucket's material — the block(s) its cells resolve to (docs/world-export/terrain-painting.md §3). Today
/// a single block or a vertical layer stack; the same seam later grows area/perimeter patterns (TP13). The
/// examples in the default theme (grass, quartz, stained clay) are illustrative, not canonical.
/// </summary>
public abstract record TerrainMaterial
{
    public abstract (int Id, int Data) Resolve(in BucketContext ctx);
}

/// <summary>One block everywhere in the bucket.</summary>
public sealed record SolidMaterial(int Id, int Data = 0) : TerrainMaterial
{
    public override (int Id, int Data) Resolve(in BucketContext ctx) => (Id, Data);
}

/// <summary>A vertical stack claimed from the top of the bucket — grass over two dirt, a wall's banded riser
/// (TP11). Each layer carries a thickness; the last layer repeats past the stack's depth so a deeper-than-
/// declared band never falls through.</summary>
public sealed record LayeredMaterial(IReadOnlyList<(TerrainMaterial Material, int Thickness)> Layers) : TerrainMaterial
{
    public override (int Id, int Data) Resolve(in BucketContext ctx)
    {
        var depth = ctx.DepthFromTop;
        foreach (var (material, thickness) in Layers)
        {
            if (depth < thickness) return material.Resolve(in ctx);
            depth -= thickness;
        }
        return Layers.Count > 0 ? Layers[^1].Material.Resolve(in ctx) : (Blocks.Stone, 0);
    }
}

/// <summary>The bucket's block tinted by the team that owns the cell — the same 0–15 damage scale wool uses,
/// so a clay / wool / stained-glass block takes the team's colour (docs/world-export/terrain-painting.md §3).
/// A cell with no team (a neutral mid) falls back to <paramref name="Neutral"/>. Usable on <b>any</b> bucket,
/// and composable inside a <see cref="LayeredMaterial"/> or a pattern (the tint reads from the shared
/// <see cref="BucketContext"/>, so it is not wall-specific).</summary>
public sealed record TeamTintedMaterial(int BlockId, TerrainMaterial Neutral) : TerrainMaterial
{
    public override (int Id, int Data) Resolve(in BucketContext ctx)
        => ctx.HasTeam ? (BlockId, ctx.TeamData) : Neutral.Resolve(in ctx);
}

/// <summary>How thick the bedrock floor is (TP8): a fixed block count, or the remainder under a fixed painted
/// terrain depth (bedrock = column height − terrain depth, per column). Always ≥1, never taller than the
/// column.</summary>
public sealed record BedrockSpec(bool Relative, int Value)
{
    /// <summary>A fixed <paramref name="thickness"/>-block bedrock floor.</summary>
    public static BedrockSpec Absolute(int thickness) => new(false, Math.Max(1, thickness));

    /// <summary>Bedrock takes everything under the top <paramref name="terrainDepth"/> painted blocks.</summary>
    public static BedrockSpec TerrainRelative(int terrainDepth) => new(true, Math.Max(0, terrainDepth));

    /// <summary>The first Y that is <em>not</em> bedrock, given a column whose surface top is
    /// <paramref name="surfaceTop"/> — clamped to [1, surfaceTop]. Equal to surfaceTop means the whole column
    /// is bedrock and nothing paints (TP8's stop).</summary>
    public int PaintFloor(int surfaceTop)
        => Math.Clamp(Relative ? surfaceTop - Value : Value, 1, surfaceTop);
}

/// <summary>
/// A terrain-paint theme (docs/world-export/terrain-painting.md §5): the depth knobs plus one material per
/// bucket. The default is the validated base model — a one-block quartz rim, a stained-clay wall, a grass
/// surface, and stone left as fill, over a one-block bedrock floor with the open rim. Every field has a base
/// default, so <see cref="Default"/> is the shipping finish and any single knob can be overridden alone.
/// </summary>
public sealed record TerrainTheme
{
    // ── geometry knobs ──
    /// <summary>How many top blocks the rim claims (TP7). Default 1.</summary>
    public int RimDepth { get; init; } = 1;
    /// <summary>How many top blocks the interior surface claims (TP11). Default 1.</summary>
    public int SurfaceDepth { get; init; } = 1;
    /// <summary>The bedrock floor thickness (TP8). Default one block.</summary>
    public BedrockSpec Bedrock { get; init; } = BedrockSpec.Absolute(1);
    /// <summary>Trace the full plateau outline, not only drops (TP3). Default off.</summary>
    public bool Closed { get; init; } = false;
    /// <summary>Paint wall on terrain-to-terrain faces, not only void-facing ones (TP9). Default on.</summary>
    public bool WallOnTerrainFaces { get; init; } = true;

    // ── bucket toggles (TP12); fill is required and has no toggle ──
    public bool RimEnabled { get; init; } = true;
    public bool WallEnabled { get; init; } = true;
    public bool SurfaceEnabled { get; init; } = true;

    // ── materials ──
    public TerrainMaterial Rim { get; init; } = new SolidMaterial(Blocks.QuartzBlock);
    // team-tinted clay, falling back to light-grey clay on a neutral cell (team-tint works on any bucket).
    public TerrainMaterial Wall { get; init; } = new TeamTintedMaterial(Blocks.StainedClay, new SolidMaterial(Blocks.StainedClay, 8));
    public TerrainMaterial Surface { get; init; } = new SolidMaterial(Blocks.Grass);
    public TerrainMaterial Fill { get; init; } = new SolidMaterial(Blocks.Stone);

    /// <summary>The shipping finish — the validated base model with example materials.</summary>
    public static TerrainTheme Default { get; } = new();

    /// <summary>The material a bucket resolves through (bedrock is fixed, never themeable).</summary>
    public TerrainMaterial MaterialFor(TerrainBucket bucket) => bucket switch
    {
        TerrainBucket.Rim => Rim,
        TerrainBucket.Wall => Wall,
        TerrainBucket.Surface => Surface,
        _ => Fill,
    };
}
