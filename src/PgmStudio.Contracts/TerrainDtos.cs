namespace PgmStudio.Contracts;

/// <summary>One door a room may be stamped with (<c>GET /api/room-styles/doors</c>). Served rather than
/// restated in the client, because the authoritative list is <c>Domain.DoorMaterials</c> — the same table the
/// wool-room block filter is built from, and a second copy here is exactly how a door could come to be offered
/// that the filter never whitelists.</summary>
public sealed record DoorOptionDto(string Slug, string Label);

/// <summary>One block a terrain-paint material may resolve to, as the block picker receives it
/// (<c>GET /api/terrain/blocks</c>). <see cref="Hex"/> is the colour the export actually places, so a swatch
/// cannot promise a block a different colour.
/// <para><see cref="InFamily"/> says which kind of group <see cref="Group"/> names: a <b>tone family</b>, the
/// set of blocks that read as one ground and the unit a pattern is filled from, or one of the three
/// sixteen-shade colour families, whose members are shades of one block and are chosen from a swatch row
/// instead. The list arrives in group order, so grouping the flagged blocks by <see cref="Group"/> recovers
/// the families whole, in the order they are offered.</para></summary>
public sealed record PaintBlockDto(int Id, int Data, string Name, string Group, string Hex, bool InFamily);
