using PgmStudio.Vocabulary;

namespace PgmStudio.Contracts;

/// <summary>One door a room may be stamped with (<c>GET /api/room-styles/doors</c>). Served rather than
/// restated in the client, because the authoritative list is <c>Domain.DoorMaterials</c> — the same table the
/// wool-room block filter is built from, and a second copy here is exactly how a door could come to be offered
/// that the filter never whitelists.</summary>
/// <param name="Slug">What a room style names the door by.</param>
/// <param name="Label">The door as an author reads it in the picker.</param>
public sealed record DoorOptionDto(string Slug, string Label);

/// <summary>One biome a field may name (<c>GET /api/terrain/biomes</c>). Served rather than restated in the
/// client, because the ids and their names live in <c>Minecraft.Palette.Biome</c> and a second copy is how a
/// picker comes to offer a biome the export writes as something else.</summary>
/// <param name="Id">The byte a chunk's <c>Biomes</c> array carries.</param>
/// <param name="Name">The biome as an author reads it.</param>
/// <param name="Hex">The grass colour it tints ground with, so a picker shows what choosing it does. Swampland
/// is two-tone and answers the greener of its two.</param>
public sealed record BiomeOptionDto(int Id, string Name, string Hex);

/// <summary>One house-style field that names a block for its <b>geometry</b>
/// (<c>GET /api/room-styles/block-kinds</c>). A stair turns a corner by its own facing and a slab fills half
/// its cube, so a field asking for one gets nothing it can use from the other — which is what <c>HS1</c>
/// refuses, in these very words.</summary>
/// <param name="Field">The field's name in the document a style is saved as.</param>
/// <param name="Kind">The kind of block it takes.</param>
/// <param name="When">The other statement that puts the field in play — a door head's fill, a window's
/// form — or null for a field that takes its kind whenever it names a block at all.</param>
/// <param name="Means">What the geometry does with the block, and what a block of another kind builds
/// instead.</param>
/// <param name="AlsoAt">The other paths the same field is stated at.</param>
public sealed record HouseBlockFieldDto(
    string Field, [property: WordSet(typeof(BlockKinds))] string Kind, string? When, string Means,
    IReadOnlyList<string> AlsoAt);

/// <summary>One kind of block a house-style field may ask for, with every id of it
/// (<c>GET /api/room-styles/block-kinds</c>).</summary>
/// <param name="Kind">The word a field names the kind by.</param>
/// <param name="Blocks">Every block that carries the geometry, with the material it is cut from — which is
/// what <c>HS4</c> pairs a door head's stair and its slab fill by.</param>
public sealed record HouseBlockKindDto(
    [property: WordSet(typeof(BlockKinds))] string Kind, IReadOnlyList<HouseBlockOptionDto> Blocks);

/// <summary>One block of a kind, as a picker receives it.</summary>
/// <param name="Id">The block id the export places.</param>
/// <param name="Data">Its variant nibble — which wood, which stone.</param>
/// <param name="Name">The block as an author reads it.</param>
/// <param name="Material">What it is cut from — <c>sandstone</c>, <c>dark oak</c>.</param>
/// <param name="Hex">The colour the export places, so a swatch cannot promise another.</param>
public sealed record HouseBlockOptionDto(int Id, int Data, string Name, string Material, string Hex);

/// <summary>What kind of block each house-style field takes, and the blocks of each kind
/// (<c>GET /api/room-styles/block-kinds</c>).</summary>
/// <param name="Fields">Every field that names a block for its geometry.</param>
/// <param name="Kinds">The kinds those fields name, each with its ids.</param>
public sealed record HouseBlockKindsDto(
    IReadOnlyList<HouseBlockFieldDto> Fields, IReadOnlyList<HouseBlockKindDto> Kinds);

/// <summary>One block a terrain-paint material may resolve to, as the block picker receives it
/// (<c>GET /api/terrain/blocks</c>). <see cref="Hex"/> is the colour the export actually places, so a swatch
/// cannot promise a block a different colour.
/// <para><see cref="InFamily"/> says which kind of group <see cref="Group"/> names: a <b>tone family</b>, the
/// set of blocks that read as one ground and the unit a pattern is filled from, or one of the three
/// sixteen-shade colour families, whose members are shades of one block and are chosen from a swatch row
/// instead. The list arrives in group order, so grouping the flagged blocks by <see cref="Group"/> recovers
/// the families whole, in the order they are offered.</para></summary>
/// <param name="Id">The block id the export places.</param>
/// <param name="Data">Its variant nibble — which wood, which dye, which stone.</param>
/// <param name="Name">The block as an author reads it.</param>
/// <param name="Group">The family it is offered under, which <paramref name="InFamily"/> says the kind
/// of.</param>
/// <param name="Hex">The colour the export actually places, so a swatch cannot promise a different
/// one.</param>
/// <param name="InFamily">Whether <paramref name="Group"/> names a <b>tone family</b> — the set of blocks
/// that read as one ground, and the unit a pattern is filled from — or one of the sixteen-shade colour
/// families, whose members are shades of one block chosen from a swatch row.</param>
public sealed record PaintBlockDto(int Id, int Data, string Name, string Group, string Hex, bool InFamily);
