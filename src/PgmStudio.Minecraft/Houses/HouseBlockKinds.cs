using PgmStudio.Minecraft.Palette;
using PgmStudio.Vocabulary;

namespace PgmStudio.Minecraft.Houses;

/// <summary>One field of a house style that names a block for its <b>geometry</b>, and what that geometry is.
/// </summary>
/// <param name="Field">The field's name in the document a style is saved as.</param>
/// <param name="Kind">The kind of block it takes, from <see cref="BlockKinds"/>.</param>
/// <param name="When">The other statement that puts the field in play — a door head's fill, a window's form —
/// or null for a field that takes its kind whenever it names a block at all.</param>
/// <param name="Means">What the geometry does with the block, and what a block of another kind builds
/// instead. It is the sentence <c>HS1</c> refuses with, so the rule and the catalogue cannot disagree about
/// why.</param>
/// <param name="AlsoAt">The other paths the same field is stated at — a house's windows are stated three
/// times over and the rule is one.</param>
public sealed record HouseBlockField(
    string Field,
    [property: WordSet(typeof(BlockKinds))] string Kind,
    string? When,
    string Means,
    IReadOnlyList<string> AlsoAt);

/// <summary>One block a kind-constrained field may name: the pair, the material it is cut from, and what a
/// picker shows it as.</summary>
public sealed record HouseBlockOption(int Id, int Data, string Name, string Material, string Hex);

/// <summary>
/// Which kind of block each house-style field takes, and the blocks of each kind.
///
/// <para><b>A field naming a block for a geometric role gets nothing it can use from a block of another
/// role.</b> A door head turns two corners by a stair's own facing, a slab band raises half a cube for a
/// sill, a beam is the end of a floor timber — so a stair id under <c>roofSlab</c> or a slab under
/// <c>doorHead.block</c> builds something the form is not named for, which is what <c>HS1</c> refuses. This
/// is the catalogue an author reads instead of finding out by being refused: the fields, the kind each takes,
/// and every id of that kind with the material it is cut from.</para>
///
/// <para>The kinds are <see cref="BlockFamilies"/>' membership and the materials are
/// <see cref="BlockMaterials"/>' table, so a block offered here is a block the gate accepts and the material
/// shown is the one <c>HS4</c> pairs by.</para>
/// </summary>
public static class HouseBlockKinds
{
    /// <summary>The head's corner stair. Present whenever the head states a form other than none.</summary>
    public static readonly HouseBlockField DoorHeadBlock = new(
        "doorHead.block", BlockKinds.Stair, null,
        "An arched head turns its two corners by a stair's own facing; anything else lays a solid lintel "
        + "across the doorway instead of an arch.",
        []);

    /// <summary>The half-cube between the head's corners.</summary>
    public static readonly HouseBlockField DoorHeadFill = new(
        "doorHead.fillBlock", BlockKinds.Slab, "fill: upperSlab",
        "The fill raises half of its own cube to read as one line with the corners; a block without a half — "
        + "a double slab included — reads as a full cube instead.",
        []);

    /// <summary>A window built from turned corners.</summary>
    public static readonly HouseBlockField WindowStair = new(
        "windows.block", BlockKinds.Stair, "form: stairLattice or arched",
        "The form turns its corners by a stair's own facing; anything else builds without the diamond or the "
        + "rounded corners the form is named for.",
        ["gableWindows.block", "storeys[].windows.block"]);

    /// <summary>A window built from a sill and a lintel of halves.</summary>
    public static readonly HouseBlockField WindowSlab = new(
        "windows.block", BlockKinds.Slab, "form: slabBanded",
        "A slab band raises half a cube for the sill and lowers half for the lintel; anything else — a double "
        + "slab included — leaves no half-block of clear air above the sill or below the lintel.",
        ["gableWindows.block", "storeys[].windows.block"]);

    /// <summary>The roof's half course.</summary>
    public static readonly HouseBlockField RoofSlab = new(
        "roofSlab", BlockKinds.Slab, null,
        "A half-course roof steps in the slab's own half on every odd course; anything else — a double slab "
        + "included — comes out a full cube and the slope stops climbing by halves.",
        []);

    /// <summary>The timber ends that run out past a corner.</summary>
    public static readonly HouseBlockField Beams = new(
        "beams.block", BlockKinds.Log, null,
        "A beam is the end of a floor timber and docks against the posts; a log is what one is cut from.",
        []);

    /// <summary>Every field that names a block for its geometry, in the order a style states them.</summary>
    public static IReadOnlyList<HouseBlockField> Fields { get; } =
        [DoorHeadBlock, DoorHeadFill, WindowStair, WindowSlab, RoofSlab, Beams];

    /// <summary>Whether <paramref name="blockId"/> carries the geometry <paramref name="kind"/> names. An
    /// unknown kind accepts nothing: a field whose kind the catalogue cannot name is a field nothing can
    /// check.</summary>
    public static bool Accepts(string kind, int blockId) => kind switch
    {
        BlockKinds.Stair => BlockFamilies.IsStair(blockId),
        BlockKinds.Slab => BlockFamilies.IsSlab(blockId),
        BlockKinds.Log => BlockFamilies.IsLog(blockId),
        _ => false,
    };

    /// <summary>The blocks of one kind, by material, with the name and swatch a picker shows. Empty for a
    /// word that is not a kind.</summary>
    public static IReadOnlyList<HouseBlockOption> BlocksOf(string kind) =>
        Offered.TryGetValue(kind, out var blocks) ? blocks : [];

    /// <summary>What an author writes where a kind's own word would read as jargon — the article and noun a
    /// refusal names the kind by.</summary>
    public static string Spoken(string kind) => kind switch
    {
        BlockKinds.Stair => "a stair",
        BlockKinds.Slab => "a single slab",
        BlockKinds.Log => "a log",
        _ => kind,
    };

    private static readonly Dictionary<string, IReadOnlyList<HouseBlockOption>> Offered =
        BlockKinds.All.ToDictionary(kind => kind, Build);

    private static IReadOnlyList<HouseBlockOption> Build(string kind) =>
    [
        .. BlockMaterials.Catalogue
            .Where(block => Accepts(kind, block.Id))
            .Select(block => new HouseBlockOption(
                block.Id, block.Data, BlockPalette.Name(block.Id, block.Data), block.Material,
                BlockPalette.Hex(block.Id, block.Data))),
    ];
}
