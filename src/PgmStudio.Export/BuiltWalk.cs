using PgmStudio.Analysis.Playability;
using PgmStudio.Geom;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Palette;

namespace PgmStudio.Export;

using Dict = Dictionary<string, object?>;

/// <summary>
/// The ground a walk runs over on a board the studio built from its own layout — the one every connectivity
/// read of a sketch-origin map takes: <c>/walk</c>, traversability, kit reach, coverage, dead ground, the
/// pre-flight and the export's <c>EX1</c>.
///
/// <para>What stands is read off the <b>world</b>, not off the rasterizer's columns, which are the terrain a
/// build stood on and hold no house, tree, pool or structure. A tree's and a boulder's blocks and a bed of lava
/// are kept apart (<see cref="WorldColumns.ForWalk"/>): solid, so they block and roof, and never a place to
/// stand. Where a block may be laid across is the map document's own answer (<see cref="BridgeableColumns"/>),
/// the pass the editability read and the reach picture take, so a walk bridges exactly what a player may.</para>
///
/// <para><b>Lava over drawn ground is crossed the way void in a build zone is</b>, a block a column: a block
/// placed into lava replaces it, and on ground at y=0 the map's own void rule lets it be placed. Lava where the
/// map forbids placing — or over the void outside every grant — is a wall.</para>
/// </summary>
public static class BuiltWalk
{
    /// <summary>The walk's ground on a built board. <paramref name="doc"/> states where building is granted;
    /// without one nothing is bridged. Water is read off the world like everything else that stands in it: a
    /// basin's water is wherever its ground was lower than its line, which nothing but the built world
    /// knows.</summary>
    public static WalkGround Ground(BuiltWorld built, Dict? doc)
    {
        var (ground, props, swum) = WorldColumns.ForWalk(built.World, built.Provenance, built.Columns ?? []);
        var water = swum.Count == 0 ? null : swum;
        if (doc is null) return WorldWalk.OfBuilt(ground, props, [], water);

        var zones = BridgeableColumns.Zones(built.World, doc);
        var granted = zones.BridgeableCells().ToHashSet();
        foreach (var (x, z, _, top) in props)
            if (IsLava(built.World.GetBlock(x, top, z).Id) && zones.PlaceableOverGroundAt((x, z)))
                granted.Add((x, z));
        return WorldWalk.OfBuilt(ground, props, granted, water);
    }

    private static bool IsLava(int blockId) => blockId is Blocks.Lava or Blocks.StationaryLava;
}
