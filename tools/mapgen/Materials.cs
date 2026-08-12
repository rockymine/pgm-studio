using PgmStudio.Minecraft;

namespace PgmStudio.MapGen;

/// <summary>
/// Palette families resolved by name, so a spec says <c>"wall": "grey stone"</c> and never a block id.
///
/// <para>Grass, podzol and mycelium carry a distinct top over dirt sides, so they read only where exactly one
/// course of them is seen from above. Stacked in a layer or turned side-on down a riser they repeat as banded
/// dirt — and they sit inside the verdant, loam and mauve families, so a pattern filled from a whole family
/// would scatter them down a wall. They come out here, once, for every caller; <see cref="Grass"/> is the one
/// place a top-faced block is laid, as the single course it reads at.</para>
/// </summary>
internal static class Materials
{
    private static bool TopFaced(int id, int data) => (id, data) is (2, 0) or (3, 2) or (110, 0);

    /// <summary>Every block of a named family, minus the ones that only read from above.</summary>
    internal static IReadOnlyList<TerrainMaterial> Family(string name, int take = 99)
    {
        var family = TerrainPalette.Families.FirstOrDefault(
            entry => entry.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (family.Name is null)
            throw new ArgumentException(
                $"no palette family '{name}' — have: {string.Join(", ", TerrainPalette.Families.Select(f => f.Name))}");

        var blocks = family.Blocks
            .Where(block => !TopFaced(block.Id, block.Data))
            .Take(take)
            .Select(block => (TerrainMaterial)new SolidMaterial(block.Id, block.Data))
            .ToList();
        if (blocks.Count == 0) throw new ArgumentException($"palette family '{name}' has nothing that stacks");
        return blocks;
    }

    /// <summary>One block out of a family — what a solid wants.</summary>
    internal static TerrainMaterial One(string name, int index = 0)
    {
        var family = Family(name);
        return family[Math.Clamp(index, 0, family.Count - 1)];
    }

    /// <summary>One course of grass over dirt, and only one course — the one place a top-faced block reads.</summary>
    internal static readonly TerrainMaterial Grass = new LayeredMaterial([
        new MaterialLayer(new SolidMaterial(2, 0), 1),
        new MaterialLayer(new SolidMaterial(3, 0), 2)]);

    /// <summary>A family laid as a named pattern. Every pattern carries a <c>Rise</c>, so a wall wears the
    /// same fabric its surface does rather than streaking vertically where the ground steps.</summary>
    internal static TerrainMaterial Pattern(string pattern, string family) => pattern.ToLowerInvariant() switch
    {
        "solid" or "" => One(family),
        "voronoi"     => new VoronoiMaterial(1, 9, [.. Family(family).Select(m => new VoronoiBand(m, 1))], Rise: 8),
        "cell"        => new CellMaterial(2, 10, 55, 4, Family(family), Rise: 8),
        "noise"       => new NoiseMaterial(3, 14, 3, Family(family), Rise: 8),
        "turbulence"  => new TurbulenceMaterial(4, 14, 3, Family(family), Rise: 8),
        "electric"    => new ElectricMaterial(5, 16, 3, Family(family), Rise: 8),
        _ => throw new ArgumentException(
            $"no pattern '{pattern}' — have: solid, voronoi, cell, noise, turbulence, electric"),
    };
}
