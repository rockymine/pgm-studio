using System.Text.Json.Serialization;
using PgmStudio.Geom.Algorithms;
using PgmStudio.Minecraft.Palette;

namespace PgmStudio.Minecraft.Painting;

/// <summary>
/// Which biome each chunk of the exported world carries — the byte a client reads to tint grass, leaves and
/// water, so a board varies in colour without a single extra block.
///
/// <para><b>It is answered per column, and its numbers are in blocks</b> — the same units a terrain pattern
/// is written in, so one vocabulary covers both. A chunk carries 256 of these bytes and they need not agree:
/// answering one per chunk turns any field into a grid of rectangles, and a board a few chunks across has too
/// few of them to hold a pattern at all.</para>
///
/// <para><b>What a boundary costs is softness, not correctness.</b> The client blends a biome's tint across a
/// small neighbourhood, so a region never reaches its own colour within a few blocks of its edge. That is a
/// reason to choose a <em>scale</em> — a pattern whose features are a handful of blocks across blends into an
/// even wash — and not a reason to quantise the field: at any scale worth looking at, the blend reads as a
/// soft edge rather than as a lost one.</para>
///
/// <para><b>It is a family of its own rather than a <see cref="TerrainMaterial"/> read for its id.</b> A
/// biome is not a block: reusing a material would put a block picker in front of a choice between forest and
/// river, and a <c>solid</c> of 4 would mean cobblestone to every reader of the document and forest to the one
/// pass that consumed it. The shapes are named after the material ones on purpose — an author who knows what
/// <c>cell</c> and <c>noise</c> do to a wall knows what they do here — and both rest on the same
/// <see cref="Voronoi"/> and <see cref="PatternNoise"/> primitives underneath, which is where the sharing
/// belongs.</para>
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(SolidBiome), "solid")]
[JsonDerivedType(typeof(CellBiome), "cell")]
[JsonDerivedType(typeof(NoiseBiome), "noise")]
public abstract record BiomeField
{
    /// <summary>The biome the column at these <b>block</b> coordinates carries.</summary>
    public abstract byte At(int x, int z);
}

/// <summary>One biome over the whole map — what an unstated field is, and the plainest thing to say.</summary>
public sealed record SolidBiome(byte Id = Biome.Plains) : BiomeField
{
    public override byte At(int x, int z) => Id;
}

/// <summary>
/// Jittered regions, each taking one biome from the palette — the shape a biome map actually has, and the
/// reason this is the kind to reach for first. <paramref name="CellSize"/> is in <b>blocks</b>: at 48 a region
/// is roughly three chunks across, and its edge is a jittered cell boundary rather than a chunk line.
/// </summary>
public sealed record CellBiome(uint Seed, int CellSize, int Jitter, IReadOnlyList<byte> Palette) : BiomeField
{
    public override byte At(int x, int z)
    {
        if (Palette is not { Count: > 0 }) return Biome.Plains;
        var (gx, gz) = Voronoi.NearestSite(x, z, Seed, Math.Max(1, CellSize), Math.Clamp(Jitter / 100.0, 0, 1));
        return Palette[(int)(PatternNoise.Hash(gx, gz, Seed) % (uint)Palette.Count)];
    }

    public bool Equals(CellBiome? other) => other is not null
        && Seed == other.Seed && CellSize == other.CellSize && Jitter == other.Jitter
        && Palette.SequenceEqual(other.Palette);

    public override int GetHashCode() => HashCode.Combine(Seed, CellSize, Jitter, BiomeHash.Of(Palette));
}

/// <summary>
/// A fractal field cut into bands, one biome per band — regions that wander into one another rather than
/// meeting on a cell wall, which is what a coast of taiga into forest wants. <paramref name="Scale"/> is in
/// <b>blocks</b>, like a cell's size.
/// </summary>
public sealed record NoiseBiome(uint Seed, int Scale, int Octaves, IReadOnlyList<byte> Stops) : BiomeField
{
    public override byte At(int x, int z)
    {
        if (Stops is not { Count: > 0 }) return Biome.Plains;
        var v = PatternNoise.Field(x, z, Seed, Math.Max(1, Scale), Math.Max(1, Octaves),
            PatternNoise.NoiseShape.Plain);
        return Stops[Math.Clamp((int)(v * Stops.Count), 0, Stops.Count - 1)];
    }

    public bool Equals(NoiseBiome? other) => other is not null
        && Seed == other.Seed && Scale == other.Scale && Octaves == other.Octaves
        && Stops.SequenceEqual(other.Stops);

    public override int GetHashCode() => HashCode.Combine(Seed, Scale, Octaves, BiomeHash.Of(Stops));
}

/// <summary>The hash a field holding a list needs once its equality walks that list — written once because
/// two of them do it and a per-site copy is how one ends up disagreeing with its own <c>Equals</c>.</summary>
internal static class BiomeHash
{
    public static int Of(IEnumerable<byte> items)
    {
        var hash = new HashCode();
        foreach (var item in items) hash.Add(item);
        return hash.ToHashCode();
    }
}
