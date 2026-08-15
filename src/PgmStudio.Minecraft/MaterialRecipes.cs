namespace PgmStudio.Minecraft;

/// <summary>
/// A material named rather than constructed: a tone family resolved to blocks that stack, and a pattern laid
/// over one at the seed and scale it reads best at.
///
/// <para><b>This is the layer between the block vocabulary and a theme.</b> <see cref="TerrainPalette"/> says
/// which blocks belong to a tone family — <c>cobble</c>, <c>verdant</c>, <c>azure</c> — which is a fact about
/// blocks. <see cref="MaterialVocabulary"/> says which material kinds exist and what each one reads, which is
/// a fact about the painter. Neither says what <c>"voronoi over sand"</c> is, and that answer is the one a
/// caller naming paint actually needs. It lived in two <c>tools/</c> files that happened to agree, character
/// for character, down to the five patterns' seeds and scales and the docstring explaining the filter below;
/// the studio itself could reach neither.</para>
///
/// <para>The seeds and scales are constants rather than parameters because a <em>named</em> pattern is one
/// look, not a family of them. A caller wanting its own seed builds the material itself — every kind is public
/// and <see cref="MaterialVocabulary"/> lists its fields — and a caller naming one is asking for the studio's
/// answer.</para>
/// </summary>
public static class MaterialRecipes
{
    /// <summary>
    /// Grass, podzol and mycelium carry a distinct top over dirt sides, so they read only where exactly one
    /// course of them is seen from above. Stacked in a layer or turned side-on down a riser they repeat as
    /// banded dirt — and they sit inside the verdant, loam and mauve families, so a pattern filled from a whole
    /// family would scatter them down a wall.
    ///
    /// <para>They come out of every family read below, once, for every caller. <see cref="Grass"/> is the one
    /// place a top-faced block is laid, as the single course it reads at.</para>
    /// </summary>
    public static bool IsTopFaced(int id, int data) => (id, data) is (2, 0) or (3, 2) or (110, 0);

    /// <summary>Every block of a named tone family as a material, minus the ones that only read from above.
    /// <paramref name="take"/> caps how many are kept, which is what a pattern wanting two or three stops out
    /// of a wide family asks for.</summary>
    public static IReadOnlyList<TerrainMaterial> Family(string name, int take = 99)
    {
        var family = TerrainPalette.Families.FirstOrDefault(
            entry => entry.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (family.Name is null)
            throw new ArgumentException(
                $"no palette family '{name}' — have: {string.Join(", ", TerrainPalette.Families.Select(f => f.Name))}");

        var blocks = family.Blocks
            .Where(block => !IsTopFaced(block.Id, block.Data))
            .Take(take)
            .Select(block => (TerrainMaterial)new SolidMaterial(block.Id, block.Data))
            .ToList();
        if (blocks.Count == 0) throw new ArgumentException($"palette family '{name}' has nothing that stacks");
        return blocks;
    }

    /// <summary>One block out of a family — what a solid wants. The index is clamped rather than refused: a
    /// family is a curated set whose length is not the caller's business, so asking for its third block when it
    /// holds two is a request for the last one, not a mistake.</summary>
    public static TerrainMaterial One(string name, int index = 0)
    {
        var family = Family(name);
        return family[Math.Clamp(index, 0, family.Count - 1)];
    }

    /// <summary>A family laid as wall stripes, one stripe per block at a shared width.</summary>
    public static IReadOnlyList<WallStripe> Stripes(string family, int width, int take = 3) =>
        [.. Family(family, take).Select(material => new WallStripe(material, width))];

    /// <summary>One course of grass over dirt, and only one course — the one place a top-faced block reads.
    /// Dirt below is what keeps it a single course; a surface that repeats grass down every course is the
    /// most-repeated authoring mistake in this repository.</summary>
    public static readonly TerrainMaterial Grass = new LayeredMaterial(new BandStack([
        new Band(new SolidMaterial(2, 0), 1),
        new Band(new SolidMaterial(3, 0), 2)]));

    /// <summary>The five area patterns a name can ask for, each filled from a tone family. Every one carries a
    /// <c>Rise</c>, so a wall wears the same fabric its surface does rather than streaking vertically where the
    /// ground steps.</summary>
    public static TerrainMaterial Pattern(string pattern, string family) => pattern.ToLowerInvariant() switch
    {
        "solid" or "" => One(family),
        "voronoi"     => new VoronoiMaterial(1, 9, [.. Family(family).Select(m => new VoronoiBand(m, 1))], Rise: 8),
        "cell"        => new CellMaterial(2, 10, 55, 4, Family(family), Rise: 8),
        "noise"       => new NoiseMaterial(3, 14, 3, Family(family), Rise: 8),
        "turbulence"  => new TurbulenceMaterial(4, 14, 3, Family(family), Rise: 8),
        "electric"    => new ElectricMaterial(5, 16, 3, Family(family), Rise: 8),
        _ => throw new ArgumentException(
            $"no pattern '{pattern}' — have: {string.Join(", ", AreaPatterns)}"),
    };

    /// <summary>The names <see cref="Pattern"/> accepts, so a caller can offer them rather than guess.</summary>
    public static readonly IReadOnlyList<string> AreaPatterns =
        ["solid", "voronoi", "cell", "noise", "turbulence", "electric"];
}
