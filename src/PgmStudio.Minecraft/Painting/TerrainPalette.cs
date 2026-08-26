using PgmStudio.Minecraft.Palette;
namespace PgmStudio.Minecraft.Painting;

/// <summary>One block a terrain-paint material may resolve to, with everything a picker needs to show it:
/// the id/data pair a <see cref="SolidMaterial"/> carries, its display <see cref="Name"/>, the
/// <see cref="Group"/> it is offered under, and the <see cref="Hex"/> swatch colour.</summary>
/// <param name="InFamily">Whether <paramref name="Group"/> names a tone family — the unit a pattern is filled
/// from — rather than one of the three sixteen-shade colour families.</param>
public readonly record struct PaintBlock(int Id, int Data, string Name, string Group, string Hex, bool InFamily);

/// <summary>One family, whole — the blocks it holds in the order they are offered, and the <see cref="Rgb"/>
/// the family as a whole reads as. A pattern takes a family as one choice, so the family has to be addressable
/// as one thing rather than as the blocks that happen to share a <see cref="PaintBlock.Group"/>.</summary>
public readonly record struct PaintFamily(string Name, int Rgb, IReadOnlyList<PaintBlock> Blocks)
{
    /// <summary>The family's own colour as a swatch string, in the form <see cref="PaintBlock.Hex"/> uses.</summary>
    public string Hex => $"#{Rgb:x6}";
}

/// <summary>
/// The blocks a terrain-paint theme may be built from (docs/world-export/terrain-painting.md §3) — the
/// vocabulary an authoring surface offers. A material can name any block id, so this is a curated shortlist
/// rather than a restriction: a block it does not carry is typed as a pair instead of being unreachable.
/// Names and swatch colours come from <see cref="BlockPalette"/>, the same table the preview and the surface
/// render use, so a picker cannot show a colour the export will not place.
///
/// <para><b>The shortlist is grouped by tone, not by taxonomy.</b> A map is not laid out one block at a time —
/// an author reaches for "the green", "the earth", "the grey stone" — so a family is the set of blocks that
/// read as one ground with a texture, and it is the unit a pattern's stops, bands or patches are filled from.
/// Grouping by what a block *is* cannot do that job: it puts stone beside obsidian and bedrock, which is a true
/// statement about rock and a useless palette for a field.</para>
///
/// <para><b>Only full cubes are named.</b> These families are a vocabulary for building ground, and a slab, a
/// stair, a fence or a flower is something that stands on ground rather than something ground is made of; a
/// double slab is a full block and belongs, its single-slab sibling does not. An ore sits with the stone it is
/// embedded in rather than in a family of its own, since it is a stone with something in it and reads as that
/// stone. Logs are out for the reason leaves are — bark is a tree, not ground — while the planks cut from them
/// are in, since a plank floor is ground a player stands on. Bedrock is a full cube and still has no family: it
/// is the map's floor and the shell of its walls, the one block that means the world ends here, so offering it
/// as a grey would invite a boundary into terrain.</para>
///
/// <para><b>A neutral family holds neutral blocks.</b> Where a family's own colour is a grey — pale stone,
/// ash, grey stone — every block in it reads as one, and a warm block among them is a block an author cannot
/// reach for: asking for the pale grey and getting a yellow-tan is the picker lying about what it offers. The
/// two mushroom blocks are warm and sit with sand, whose colour they actually read as.</para>
///
/// <para><b>A family is a use as much as a colour, and where the two disagree the use wins.</b> Gravel reads
/// grey enough to sit with stone, but it is laid where cobble is laid — the low, wet ground at a water's edge —
/// and grouping it by colour buries that: with gravel and mossy cobble in the cobble family the two stone
/// families separate by relief, cobble sitting low and shallow and grey stone standing high and steep, which is
/// the distinction an author is actually drawing.</para>
///
/// <para><b>All sixteen stained-clay shades now read as a tone.</b> Eight — orange, lime, green, brown, cyan,
/// blue, black — were placed from the start; the other eight join the family their fired colour actually reads
/// as, not the wool or glass block of the same dye name, since a family names a colour rather than a dye
/// number: magenta and purple stained clay are both a dusty violet-grey closer to mauve than to any brick or
/// soil tone, and light gray stained clay is a warm neutral brown rather than a grey. Hay bale joins gold for
/// the same ochre yellow wool already offers there; packed ice was already the second member of ice.</para>
///
/// <para>The three sixteen-shade families are not tones and keep their own groups: their data value is a
/// <em>shade</em> of one block rather than a different block, so a picker offers the family once and the shade
/// is a swatch click. A shade a tone family claims is offered under that tone, and the colour row still reaches
/// every one of the sixteen.</para>
/// </summary>
public static class TerrainPalette
{
    // The families in reading order — green through earth and stone to white — each with the colour it reads as
    // and its blocks ordered light to dark. Shared with the surface analysis, which names a world's ground by
    // this same table: one vocabulary, so what an author paints and what a report measures cannot drift apart.
    private static readonly (string Name, int Rgb, (int Id, int Data)[] Blocks)[] Grouped =
    [
        ("verdant",   0x4E8A33, [(159, 5), (2, 0), (159, 13), (35, 13), (168, 2)]),
        ("spring",    0x78C13A, [(165, 0), (35, 5), (133, 0)]),
        ("turquoise", 0x3E9E8C, [(168, 0), (168, 1)]),
        ("loam",      0x5A4126, [(3, 2), (159, 12), (88, 0), (5, 5), (35, 12)]),
        ("dirt",      0x8A6743, [(5, 0), (5, 3), (159, 8), (3, 0), (3, 1), (5, 1)]),
        ("brick",     0x9A6250, [(1, 1), (1, 2), (45, 0), (172, 0), (159, 6)]),
        ("rust",      0xA85A28, [(5, 4), (159, 1), (12, 1), (179, 0), (181, 8), (159, 14)]),
        ("sand",      0xD6C894, [(12, 0), (5, 2), (159, 0), (24, 0), (24, 2), (43, 9), (121, 0), (99, 15), (99, 0)]),
        ("gold",      0xC6A62F, [(35, 4), (19, 0), (19, 1), (103, 0), (159, 4), (170, 0)]),
        ("pale stone",0xB4B2AE, [(1, 3), (1, 4)]),
        ("ash",       0x9DA0A0, [(35, 8), (42, 0), (82, 0), (43, 8), (43, 0)]),
        ("grey stone",0x7C817A, [(1, 0), (1, 5), (1, 6), (98, 0), (98, 3), (15, 0), (16, 0)]),
        ("cobble",    0x767B72, [(13, 0), (4, 0), (98, 2), (98, 1), (48, 0)]),
        ("mauve",     0x8A7A8E, [(110, 0), (159, 2), (159, 3), (159, 10)]),
        ("azure",     0x3B5DA8, [(35, 11), (22, 0), (159, 11)]),
        ("slate",     0x5A6066, [(159, 9), (35, 7)]),
        ("dark",      0x2E2B2B, [(159, 7), (112, 0), (159, 15), (35, 15), (173, 0), (49, 0)]),
        ("ice",       0xA9CBE0, [(79, 0), (174, 0)]),
        ("bright",    0xE8E9E4, [(80, 0), (155, 0), (155, 1)]),
    ];

    // The sixteen-shade colour families, expanded to all of their damage values below.
    private static readonly (string Group, int Id)[] Shaded =
    [
        ("Stained clay", Blocks.StainedClay),
        ("Wool", Blocks.Wool),
        ("Stained glass", Blocks.StainedGlass),
    ];

    /// <summary>Every offered block, in group order — the tone families first, then the sixteen-shade ones.</summary>
    public static IReadOnlyList<PaintBlock> Paintable { get; } = Build();

    /// <summary>The tone families whole, each addressable as one choice. The sixteen-shade colour families are
    /// not here: they are a shade row, not a ground an author reaches for.</summary>
    public static IReadOnlyList<PaintFamily> Families { get; } = BuildFamilies();

    private static readonly Dictionary<(int Id, int Data), string> FamilyIndex =
        Grouped.SelectMany(family => family.Blocks.Select(block => (block, family.Name)))
               .ToDictionary(entry => entry.block, entry => entry.Name);

    private static readonly Dictionary<string, int> FamilyColour =
        Grouped.ToDictionary(family => family.Name, family => family.Rgb);

    /// <summary>The family a block belongs to, or null for one no family names — a partial block, a fixture, or
    /// simply a block outside the vocabulary.</summary>
    public static string? FamilyOf(int id, int data) =>
        FamilyIndex.TryGetValue((id, data), out var name) ? name : null;

    /// <summary>The colour a family reads as, or <paramref name="fallback"/> for a name that is not one.</summary>
    public static int ColourOf(string family, int fallback = 0xC020C0) =>
        FamilyColour.TryGetValue(family, out var rgb) ? rgb : fallback;

    private static PaintBlock Describe(int id, int data, string group, bool inFamily) =>
        new(id, data, BlockPalette.Name(id, data), group, BlockPalette.Hex(id, data), inFamily);

    private static List<PaintBlock> Build()
    {
        var blocks = new List<PaintBlock>();
        var seen = new HashSet<(int, int)>();
        void Offer(int id, int data, string group, bool inFamily)
        {
            if (seen.Add((id, data))) blocks.Add(Describe(id, data, group, inFamily));
        }

        foreach (var (name, _, members) in Grouped)
            foreach (var (id, data) in members) Offer(id, data, name, inFamily: true);
        // After the families, so a shade a family claims keeps that family's group; the shade row reads every
        // damage value of an id regardless of group, so the sixteen stay whole either way.
        foreach (var (group, id) in Shaded)
            for (var data = 0; data < 16; data++) Offer(id, data, group, inFamily: false);
        return blocks;
    }

    private static List<PaintFamily> BuildFamilies() =>
        [.. Grouped.Select(family => new PaintFamily(
            family.Name, family.Rgb,
            [.. family.Blocks.Select(block => Describe(block.Id, block.Data, family.Name, inFamily: true))]))];
}
