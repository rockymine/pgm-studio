namespace PgmStudio.Minecraft.Palette;

/// <summary>
/// What a block is <b>cut from</b>. A sandstone block, a sandstone stair and a sandstone slab are one
/// material in three shapes; a birch stair and a birch slab are another. <see cref="BlockFamilies"/> answers
/// the shape — is this a stair, is this a slab — and this answers the substance, which is the other half of
/// the question a gate asks when it holds two fields to one material.
///
/// <para><b>Metadata matters here and does not in <see cref="BlockFamilies"/>.</b> A slab's id says nothing
/// about its substance — <c>44:1</c> is sandstone and <c>44:2</c> is oak — so membership by id alone cannot
/// answer this, and the state bits are stripped through <see cref="BlockVariants.Normalize"/> first: a slab
/// laid in its upper half is the same material as one in its lower, and a log lying east–west is the same
/// wood as one standing up.</para>
///
/// <para><b>A block outside the table is its own material.</b> The table names what a gate actually compares —
/// the substances that come in more than one shape — and everything else answers a name derived from its own
/// id and variant, so two of the same block are one material and two different blocks are two. Nothing is
/// guessed: an unlisted block is never <em>merged</em> with another, only left alone.</para>
/// </summary>
public static class BlockMaterials
{
    // (id, normalized data) → the substance. Only what comes in more than one shape needs a row: a gate
    // comparing two fields is asking whether they are the same stuff, and stuff that exists as a single block
    // can only ever equal itself.
    private static readonly Dictionary<(int Id, int Data), string> Named = Build();

    /// <summary>The material <paramref name="id"/>:<paramref name="data"/> is cut from.</summary>
    public static string Of(int id, int data)
    {
        var variant = BlockVariants.Normalize(id, data);
        return Named.TryGetValue((id, variant), out var material) ? material : $"{id}:{variant}";
    }

    /// <summary>Whether two blocks are cut from the same material — a roof and the slab that continues it, a
    /// door head's stair and the slab that fills it, a window and the block it is seated in.</summary>
    public static bool Same(int id, int data, int otherId, int otherData) =>
        Of(id, data) == Of(otherId, otherData);

    // material → its single slab, read off the same table Of reads. A material with none — a log, mossy
    // cobblestone, anything outside the table — has no entry and answers null.
    private static readonly Dictionary<string, (int Id, int Data)> SlabNamed =
        Named.Where(entry => BlockFamilies.IsSlab(entry.Key.Id))
             .GroupBy(entry => entry.Value)
             .ToDictionary(group => group.Key, group => group.First().Key);

    /// <summary>The single slab cut from the same material as <paramref name="id"/>:<paramref name="data"/>,
    /// or null where that material has none. What a course continuing a whole block by halves is made of: a
    /// dark oak verge steps in a dark oak slab, and a laid log steps in nothing because no slab is cut from a
    /// log. Read off <see cref="Of"/>'s own table, so the two cannot disagree about what a material is.</summary>
    public static (int Id, int Data)? SlabOf(int id, int data) =>
        SlabNamed.TryGetValue(Of(id, data), out var slab) ? slab : null;

    private static Dictionary<(int, int), string> Build()
    {
        var table = new Dictionary<(int, int), string>();
        void Row(string material, params (int Id, int Data)[] blocks)
        {
            foreach (var block in blocks) table[block] = material;
        }

        // The six woods: planks, log, stairs, and the wooden slab that matches each.
        var woods = new[] { "oak", "spruce", "birch", "jungle", "acacia", "dark oak" };
        var stairs = new[] { 53, 134, 135, 136, 163, 164 };
        for (var wood = 0; wood < woods.Length; wood++)
            Row(woods[wood], (5, wood), (stairs[wood], 0), (126, wood), (125, wood));
        Row("oak", (17, 0));
        Row("spruce", (17, 1));
        Row("birch", (17, 2));
        Row("jungle", (17, 3));
        Row("acacia", (162, 0));
        Row("dark oak", (162, 1));
        // 44:2 is the legacy wooden slab, which is the oak plank texture whatever the plank id says.
        Row("oak", (44, 2), (43, 2));

        Row("stone", (1, 0), (44, 0), (43, 0));
        Row("granite", (1, 1), (1, 2));
        Row("diorite", (1, 3), (1, 4));
        Row("andesite", (1, 5), (1, 6));
        Row("cobblestone", (4, 0), (67, 0), (44, 3), (43, 3));
        Row("mossy cobblestone", (48, 0));
        Row("stone brick", (98, 0), (98, 2), (98, 3), (109, 0), (44, 5), (43, 5));
        Row("mossy stone brick", (98, 1));
        Row("brick", (45, 0), (108, 0), (44, 4), (43, 4));
        Row("sandstone", (24, 0), (24, 1), (24, 2), (128, 0), (44, 1), (43, 1));
        Row("red sandstone", (179, 0), (179, 1), (179, 2), (180, 0), (182, 0), (181, 0));
        Row("quartz", (155, 0), (155, 1), (155, 2), (156, 0), (44, 7), (43, 7));
        Row("nether brick", (112, 0), (114, 0), (44, 6), (43, 6));
        return table;
    }
}
