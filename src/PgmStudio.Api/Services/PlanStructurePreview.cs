using System.Text.Json;
using PgmStudio.Domain;
using PgmStudio.Minecraft;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Sketch;

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
/// <para>The boxes must agree with <see cref="SketchWorldBuilder"/> block for block, or the preview lies about
/// the map. So everything is taken from the build's own sources rather than re-derived: the geometry from
/// <see cref="PlanCompiler"/> output, the sizes from the stampers' constants and footprint helpers
/// (<see cref="CubeStamper"/>, <see cref="StructureStamper.IronCubeFootprint"/>), and every floor from the same
/// per-column surface map the stampers rest structures on — including its fallback for a marker whose column
/// carries no terrain, which drops that structure to the bottom of the world. Reading the floor from the
/// marker's plan surface instead would look equivalent and silently disagree in exactly that case.</para>
///
/// <para>Wool-room floors are deliberately absent: <c>StructureIntent.RoomFloors</c> is exactly the wool-room
/// piece's fanned rect, which the editor already draws as that piece's prism — it tints that prism bedrock
/// rather than stacking a coincident second box.</para>
/// </summary>
public static class PlanStructurePreview
{
    // A cube spans layer 0 (its floor) through CubeStamper.RoofLayer inclusive — hence the +1 to an exclusive top.
    private const int CubeHeight = CubeStamper.RoofLayer + 1;

    /// <summary>The structure boxes for <paramref name="plan"/>, or an empty list when it compiles to none.</summary>
    public static IReadOnlyList<StructureBox> Build(PlanModel plan)
    {
        var (layout, intent) = PlanCompiler.Compile(plan);
        var surface = SketchTerrainBuilder.SurfaceTops(
            SketchRasterizer.RasterizeColumns(JsonSerializer.Serialize(layout, SketchLayout.Json)));

        var boxes = new List<StructureBox>();

        // Spawn cubes + wool cages: the frame-resolved shell (its plan piece, or the marker-anchored
        // default), resting on the columns it spans — the same SketchWorldBuilder frames the build stamps.
        var teamColor = (intent.Teams ?? []).ToDictionary(t => t.Id, t => t.Color);
        foreach (var s in intent.Spawns)
            boxes.Add(RoomBox("spawn-cube", teamColor.GetValueOrDefault(s.Team), SketchWorldBuilder.SpawnFrame(s), surface));
        foreach (var w in intent.Wools ?? [])
            boxes.Add(RoomBox("wool-cage", w.Color, SketchWorldBuilder.WoolFrame(w), surface));

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

        // Walls: the footprint is already max-exclusive; TopY is inclusive, so +1.
        foreach (var w in st.Walls)
            boxes.Add(new StructureBox("wall", null, w.MinX, w.MinZ, w.MaxX, w.MaxZ, 0, w.TopY + 1));

        return boxes;
    }

    // A frame-resolved room shell, resting on the columns its footprint spans (the frame is already
    // exclusive-max, matching this drawing frame directly).
    private static StructureBox RoomBox(
        string kind, string? color, RoomFrame frame, IReadOnlyDictionary<(int X, int Z), int> surface)
    {
        var floor = SketchWorldBuilder.FrameFloor(frame, surface);
        return new StructureBox(kind, color, frame.MinX, frame.MinZ, frame.MaxX, frame.MaxZ, floor, floor + CubeHeight);
    }
}
