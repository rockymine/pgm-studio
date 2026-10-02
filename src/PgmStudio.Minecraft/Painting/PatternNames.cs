using System.Text;
using PgmStudio.Minecraft.Palette;

namespace PgmStudio.Minecraft.Painting;

/// <summary>
/// The name a pattern describes itself by: the blocks it lays, in the order it first lays them, then the kind
/// of pattern laying them — <c>cobblestone-andesite-noise</c>, <c>stone-bricks-andesite-polished-andesite-cells</c>,
/// <c>team-stained-clay-light-gray-neutral</c>. A pattern lifted out of a theme is named this way, and so is
/// every pattern the library is seeded with, so a name says what the pattern contains.
/// </summary>
public static class PatternNames
{
    /// <summary>The most blocks a name lists; a pattern laying more ends its list with <c>mix</c>.</summary>
    private const int NamedBlocks = 3;

    /// <summary>The name <paramref name="material"/> describes itself by.</summary>
    public static string Describe(TerrainMaterial material)
    {
        var words = new List<string>();
        var blocks = Materials.Laid(material).Distinct().ToList();
        if (material is TeamTintedMaterial tinted)
        {
            var family = Slug(Family(tinted.BlockId));
            words.Add("team");
            words.Add(family);
            // A neutral of the tint's own block is named by its colour alone: the block is already said.
            words.AddRange(blocks.Take(NamedBlocks).Select(block =>
            {
                var name = Slug(BlockPalette.Name(block.Id, block.Data));
                return block.Id == tinted.BlockId && name.EndsWith("-" + family, StringComparison.Ordinal)
                    ? name[..^(family.Length + 1)]
                    : name;
            }));
            words.Add("neutral");
            return string.Join('-', words);
        }
        words.AddRange(blocks.Take(NamedBlocks).Select(block => Slug(BlockPalette.Name(block.Id, block.Data))));
        if (blocks.Count > NamedBlocks) words.Add("mix");
        words.Add(KindWord(material));
        return string.Join('-', words.Where(word => word.Length > 0));
    }

    /// <summary>The blocks a material lays, as the words a name lists them by, in the order it first lays them.</summary>
    public static IEnumerable<string> BlockWords(TerrainMaterial material)
        => Materials.Laid(material).Distinct().Select(block => Slug(BlockPalette.Name(block.Id, block.Data)));

    /// <summary>One block as the word a name lists it by.</summary>
    public static string BlockWord(int id, int data) => Slug(BlockPalette.Name(id, data));

    /// <summary>A name not yet in <paramref name="taken"/>: the description itself, else the description with
    /// the first free count after it.</summary>
    public static string Unique(string described, ISet<string> taken)
    {
        if (!taken.Contains(described)) return described;
        for (var count = 2; ; count++)
            if (!taken.Contains($"{described}-{count}")) return $"{described}-{count}";
    }

    private static string KindWord(TerrainMaterial material) => material switch
    {
        LayeredMaterial { Axis: BandAxis.Inward } => "rings",
        LayeredMaterial { Axis: BandAxis.Height } => "strata",
        LayeredMaterial { Axis: BandAxis.Slope } => "slope-bands",
        LayeredMaterial => "layers",
        VoronoiMaterial => "voronoi",
        CellMaterial => "cells",
        NoiseMaterial => "noise",
        TurbulenceMaterial => "turbulence",
        ElectricMaterial => "electric",
        CheckerMaterial or LogCheckerMaterial => "checker",
        WallRunMaterial => "stripes",
        WallDiagonalMaterial => "diagonal-stripes",
        WallFrameMaterial => "frame",
        LaidLogMaterial => "laid",
        _ => "",
    };

    /// <summary>What a team tint's block is called once its colour is the team's: the block's name with the
    /// colour taken off.</summary>
    private static string Family(int blockId) => blockId switch
    {
        Blocks.StainedClay => "stained clay",
        Blocks.Wool => "wool",
        Blocks.StainedGlass => "stained glass",
        Blocks.StainedGlassPane => "stained glass pane",
        _ => BlockPalette.Name(blockId, 0),
    };

    private static string Slug(string name)
    {
        var slug = new StringBuilder(name.Length);
        foreach (var character in name.ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(character)) slug.Append(character);
            else if (slug.Length > 0 && slug[^1] != '-') slug.Append('-');
        }
        return slug.ToString().Trim('-');
    }
}
