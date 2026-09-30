using PgmStudio.Analysis.Playability;
using PgmStudio.Geom;
using PgmStudio.Geom.Algorithms;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Dressing;
using PgmStudio.Minecraft.Palette;
using PgmStudio.Pgm.Sketch;

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
    /// without one nothing is bridged. <paramref name="layoutJson"/> is read for the water a player swims.</summary>
    public static WalkGround Ground(BuiltWorld built, Dict? doc, string layoutJson)
    {
        var (ground, props) = WorldColumns.ForWalk(built.World, built.Provenance, built.Columns ?? []);
        if (doc is null) return WorldWalk.OfBuilt(ground, props, [], Water(layoutJson));

        var zones = BridgeableColumns.Zones(built.World, doc);
        var granted = zones.BridgeableCells().ToHashSet();
        foreach (var (x, z, _, top) in props)
            if (IsLava(built.World.GetBlock(x, top, z).Id) && zones.PlaceableOverGroundAt((x, z)))
                granted.Add((x, z));
        return WorldWalk.OfBuilt(ground, props, granted, Water(layoutJson));
    }

    private static bool IsLava(int blockId) => blockId is Blocks.Lava or Blocks.StationaryLava;

    /// <summary>Where the board's water is, carved by the same bed the decorator lays it with. A dressing
    /// that states none answers null, which is what a plan and an undressed board both are. A bed of lava is
    /// not a swim, and the walk reads it off the world instead.</summary>
    private static HashSet<(int X, int Z)>? Water(string layoutJson)
    {
        var dressing = SketchLayout.Parse(layoutJson)?.Dressing;
        if (dressing is not { } element) return null;

        var cells = new HashSet<(int X, int Z)>();
        foreach (var prop in DressingJson.Deserialize(element.ToString()).Props.OfType<FluidProp>()
                     .Where(prop => prop.Fluid == Fluid.Water))
            foreach (var cell in FluidBed.Cells(prop.Points, prop.Radius, prop.Depth, prop.Form, prop.Edge,
                                                unchecked((uint)prop.Seed)))
                cells.Add((cell.X, cell.Z));
        return cells.Count == 0 ? null : cells;
    }
}
