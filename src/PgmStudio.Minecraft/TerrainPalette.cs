namespace PgmStudio.Minecraft;

/// <summary>One block a terrain-paint material may resolve to, with everything a picker needs to show it:
/// the id/data pair a <see cref="SolidMaterial"/> carries, its display <see cref="Name"/>, the
/// <see cref="Group"/> it is offered under, and the <see cref="Hex"/> swatch colour.</summary>
public readonly record struct PaintBlock(int Id, int Data, string Name, string Group, string Hex);

/// <summary>
/// The blocks a terrain-paint theme may be built from (docs/world-export/terrain-painting.md §3) — the
/// vocabulary an authoring surface offers, grouped for a picker. A material can name any block id, so this is
/// a curated shortlist rather than a restriction: full opaque blocks that read as terrain, with the two
/// sixteen-colour families that make a palette. Names and swatch colours come from <see cref="BlockPalette"/>,
/// the same table the preview and the surface render use, so a picker cannot show a colour the export will not
/// place.
/// </summary>
public static class TerrainPalette
{
    // (group, ids) in offer order. The stained families expand to their sixteen damage values below.
    private static readonly (string Group, int[] Ids)[] Plain =
    [
        ("Rock",    [Blocks.Stone, 4, 98, 48, Blocks.Obsidian, Blocks.Bedrock, 87, 112, Blocks.EndStone, Blocks.QuartzBlock, 168, 169]),
        ("Earth",   [Blocks.Grass, Blocks.Dirt, 110, 13, 12, 24, 179, 88, 82, 172]),
        ("Ice",     [79, 174, 80]),
        ("Wood",    [5, 17, 162, 47, 170]),
        ("Mineral", [Blocks.GoldBlock, Blocks.IronBlock, 57, Blocks.EmeraldBlock, 22, 152, 173]),
        ("Glass",   [20]),
    ];

    private static readonly (string Group, int Id)[] Stained =
    [
        ("Stained clay", Blocks.StainedClay),
        ("Wool", Blocks.Wool),
        ("Stained glass", Blocks.StainedGlass),
    ];

    /// <summary>Every offered block, in group order — plain blocks first, then the sixteen-colour families.</summary>
    public static IReadOnlyList<PaintBlock> Paintable { get; } = Build();

    private static List<PaintBlock> Build()
    {
        var blocks = new List<PaintBlock>();
        var seen = new HashSet<(int, int)>();
        void Offer(int id, int data, string group)
        {
            if (seen.Add((id, data)))
                blocks.Add(new PaintBlock(id, data, BlockPalette.Name(id, data), group, BlockPalette.Hex(id, data)));
        }

        foreach (var (group, ids) in Plain)
            foreach (var id in ids) Offer(id, 0, group);
        foreach (var (group, id) in Stained)
            for (var data = 0; data < 16; data++) Offer(id, data, group);
        return blocks;
    }
}
