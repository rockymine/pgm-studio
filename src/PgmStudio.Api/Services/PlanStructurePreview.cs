using System.Text.Json;
using PgmStudio.Domain;
using PgmStudio.Export;
using PgmStudio.Minecraft;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Sketch;
using PgmStudio.Minecraft.Houses;
using PgmStudio.Minecraft.Stamping;

namespace PgmStudio.Api.Services;

/// <summary>One structure the world build will stamp, as the box the plan editor's iso view draws: an
/// axis-aligned prism in absolute block coordinates, already fanned across the symmetry orbit.
/// <para><see cref="MinX"/>/<see cref="MinZ"/>/<see cref="Floor"/> are inclusive, <see cref="MaxX"/>/
/// <see cref="MaxZ"/>/<see cref="Top"/> exclusive — a continuous frame, so a block at <c>x</c> spans
/// <c>[x, x+1)</c>. The stampers' own footprint conventions differ per structure and are normalized to
/// this frame in <see cref="PlanStructurePreview"/>.</para>
/// <para><see cref="Kind"/> is the structure family (<c>spawn-cube</c>, <c>wool-cage</c>, <c>iron</c>,
/// <c>destroyable</c>, <c>core</c>, <c>wall</c>); <see cref="Color"/> is a colour slug the client maps through its dye
/// palette, or null where the kind carries its own fixed material colour.</para></summary>
public readonly record struct StructureBox(
    string Kind, string? Color, int MinX, int MinZ, int MaxX, int MaxZ, int Floor, int Top);

/// <summary>
/// Derives the boxes the world build stamps — spawn cubes, wool cages, iron cubes and approach walls — from a
/// plan, without building a world. The plan editor's iso view draws these so the author sees what will land in
/// the columns they drew (the shells only; interiors are not modelled).
///
/// <para>The boxes must agree with <see cref="WorldBuilder"/> block for block, or the preview lies about
/// the map. So everything is taken from the build's own sources rather than re-derived: the geometry from
/// <see cref="PlanCompiler"/> output, the sizes from the stampers' constants and footprint helpers
/// (<see cref="HouseStamper"/>, <see cref="StructureStamper.IronCubeFootprint"/>), and every floor from the same
/// per-column surface map the stampers rest structures on — including its fallback for a marker whose column
/// carries no terrain, which drops that structure to the bottom of the world. Reading the floor from the
/// marker's plan surface instead would look equivalent and silently disagree in exactly that case.</para>
/// </summary>
public static class PlanStructurePreview
{
    // A cube spans layer 0 (its floor) through its style's top layer inclusive — hence the +1 to an exclusive
    // top. The built-in styles, deliberately: this draws a *plan*, and a plan carries no room-style binding —
    // the compiler builds its layout from the plan, and the binding rides a sketch's layout (structures.md §9).
    // A plan editor showing a bound shell would need the plan to carry one first.
    private static readonly int CubeHeight = HouseStyle.MaxTopLayer + 1;

    /// <summary>The structure boxes for <paramref name="plan"/>, or an empty list when it compiles to none.</summary>
    public static IReadOnlyList<StructureBox> Build(PlanModel plan)
    {
        var (layout, intent) = PlanCompiler.Compile(plan);
        var surface = TerrainBuilder.SurfaceTops(
            SketchRasterizer.RasterizeColumns(JsonSerializer.Serialize(layout, SketchLayout.Json)));

        var boxes = new List<StructureBox>();

        // Spawn cubes + wool cages: the frame-resolved shell (its plan piece, or the marker-anchored
        // default), resting on the columns it spans — the same WorldBuilder frames the build stamps.
        var teamColor = (intent.Teams ?? []).ToDictionary(t => t.Id, t => t.Color);
        foreach (var s in intent.Spawns)
        {
            var room = WorldBuilder.SpawnRoom(s, walled: true);
            boxes.Add(RoomBox("spawn-cube", teamColor.GetValueOrDefault(s.Team), room.Frame, surface));
            // Only a placeable iron cube is drawn (WX9): an unplaceable marker stamps nothing, so a box
            // for it would show a structure the export refuses to place.
            foreach (var iron in room.Iron.Where(i => i.Placeable))
            {
                var maxX = iron.MinX + iron.Size - 1;
                var maxZ = iron.MinZ + iron.Size - 1;
                var ironBase = PositionSnap.SurfaceYOver(surface, iron.MinX, iron.MinZ, maxX, maxZ, 1);
                boxes.Add(new StructureBox("iron", null, iron.MinX, iron.MinZ, maxX + 1, maxZ + 1,
                    ironBase, ironBase + iron.Size));
            }
        }
        foreach (var w in intent.Wools ?? [])
            boxes.Add(RoomBox("wool-cage", w.Color, WorldBuilder.WoolFrame(w, walled: true), surface));

        // Destroyables: the same ObjectiveStamper.DestroyableBox the world build stamps from, so the preview
        // cannot show a structure the export would not place (OB8). Inclusive box → +1 for the exclusive frame.
        foreach (var b in intent.Destroyables ?? [])
        {
            if (!DestroyableStyles.TryParse(b.Style, out var style)) continue;
            var (ax, az) = PositionSnap.SnapXZ(b.Anchor.X, b.Anchor.Z);
            var box = ObjectiveStamper.DestroyableBox(surface, ax, az, style, b.Float);
            boxes.Add(new StructureBox("destroyable", null, box.MinX, box.MinZ,
                box.MaxX + 1, box.MaxZ + 1, box.MinY, box.MaxY + 1));
        }

        // Cores: the same ObjectiveStamper.CoreBox the world build stamps from (OB8).
        foreach (var c in intent.Cores ?? [])
        {
            var (ax, az) = PositionSnap.SnapXZ(c.Anchor.X, c.Anchor.Z);
            var box = ObjectiveStamper.CoreBox(surface, ax, az, c.Size, c.Height, c.Float);
            boxes.Add(new StructureBox("core", null, box.MinX, box.MinZ,
                box.MaxX + 1, box.MaxZ + 1, box.MinY, box.MaxY + 1));
        }

        var st = intent.Structures;
        if (st is null) return boxes;

        // Iron: IronCubeFootprint is max-INCLUSIVE, so +1 to reach the exclusive frame. The base is the
        // footprint's surface, unclamped — StampIronCube takes it raw.
        foreach (var ic in st.IronCubes)
        {
            var (minX, minZ, maxX, maxZ) = StructureStamper.IronCubeFootprint(ic.X, ic.Z);
            var baseY = PositionSnap.SurfaceYOver(surface, minX, minZ, maxX, maxZ, 1);
            boxes.Add(new StructureBox("iron", null, minX, minZ, maxX + 1, maxZ + 1,
                baseY, baseY + StructureStamper.IronCubeSize));
        }

        // Walls: the footprint is already max-exclusive; TopY is inclusive, and the stamper lays one course of
        // cobweb over it, so the drawn box tops out two above.
        foreach (var w in st.Walls)
            boxes.Add(new StructureBox("wall", null, w.MinX, w.MinZ, w.MaxX, w.MaxZ, 0, w.TopY + 2));

        return boxes;
    }

    // A frame-resolved room shell, resting on the columns its footprint spans (the frame is already
    // exclusive-max, matching this drawing frame directly).
    private static StructureBox RoomBox(
        string kind, string? color, RoomFrame frame, IReadOnlyDictionary<(int X, int Z), int> surface)
    {
        var floor = WorldBuilder.FrameFloor(frame, surface);
        return new StructureBox(kind, color, frame.MinX, frame.MinZ, frame.MaxX, frame.MaxZ, floor, floor + CubeHeight);
    }
}
