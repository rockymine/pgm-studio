using PgmStudio.Pgm.Shapes;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Compose;

/// <summary>
/// The wool binding over <see cref="ShapeEmitter"/> — the composer's mirror of the categorizer's shape
/// read. The emitter owns the family geometry; this binding fits it to a box: the minimum box in the dock's
/// frame, and the turn that lays the mouth-up emission onto the edge the box docks. Laying the compound gives
/// its room the <see cref="PlanRoles.WoolRoom"/> role (<see cref="UnitPieces.Lay(Compound, Box)"/>).
/// </summary>
public static class WoolBoxEmitter
{
    /// <summary>The dead-end room's depth along the final corridor, in cells — a two-cell (~10-block)
    /// plateau that clears the export stamp.</summary>
    public const int RoomDepthCells = ShapeEmitter.RoomDepthCells;

    /// <summary>The minimum box a wool <paramref name="family"/> needs to dock a hub edge, as
    /// <c>(along-the-edge, outward-depth)</c> cells — its <see cref="ShapeEmitter.MinBox"/> mapped from the
    /// family's own emit frame into the dock frame. A <b>lateral-mouth</b> family (the donut/clamp open to the
    /// side, so their mouth runs along the box's height and their depth along its width) <b>transposes</b>; a
    /// top-mouth family (I/L/Z/U/H) maps straight through. This is the one place the per-family mouth facing is
    /// resolved, so the allocator sizes every family the same way.</summary>
    public static (int Along, int Depth) MouthBox(
        ShapeFamily family, int laneWidth, RoomPlacement roomPlacement = RoomPlacement.Inline, bool woolAtEnd = false)
    {
        var (w, h) = ShapeEmitter.MinBox(family, laneWidth, roomPlacement, woolAtEnd: woolAtEnd);
        return ShapeEmitter.MouthEdge(family) is BoxEdge.Left or BoxEdge.Right ? (h, w) : (w, h);
    }

    /// <summary>Fill a typed <paramref name="box"/> whose mouth is <paramref name="mouth"/> — any of the four
    /// box edges — with one family: the compound, box-local, ending in the wool room. The shape is emitted
    /// mouth-up (<see cref="ShapeEmitter.OrientMouthTop"/>) in the mouth's <b>along × depth</b> frame, then turned
    /// so its entry lands on <paramref name="mouth"/> and it fills the box's dims. A box docks the host on
    /// whichever edge faces it, so the allocator drives all four mouths. <c>null</c> with
    /// <paramref name="rejection"/> set when the family does not fit the box or the knobs are not ones the
    /// emitter builds — "no shape fits" is a signal, never a throw.</summary>
    public static Compound? Fill(
        Box box, BoxEdge mouth, ShapeFamily family, int laneWidth, out FillRejection? rejection,
        bool flip = false, RoomPlacement roomPlacement = RoomPlacement.Inline,
        bool woolAtEnd = false, int attachmentWidth = 0, RingWalls? ringWalls = null)
    {
        rejection = null;
        // the mouth's frame: its along-edge length and the depth perpendicular to it. Top/Bottom run along the
        // box width; Left/Right run along its height (the shape is rotated a quarter turn onto them).
        var lateral = mouth is BoxEdge.Left or BoxEdge.Right;
        var (alongLen, depth) = lateral ? (box.Rect.Height, box.Rect.Width) : (box.Rect.Width, box.Rect.Height);

        // canonical dims: the mouth-up normalization transposes left/right-mouth families, so size the canonical
        // frame with the along/depth swapped back through the same map
        var famTransposes = ShapeEmitter.MouthEdge(family) is BoxEdge.Left or BoxEdge.Right;
        var (canonW, canonH) = famTransposes ? (depth, alongLen) : (alongLen, depth);
        var (minW, minH) = ShapeEmitter.MinBox(family, laneWidth, roomPlacement, woolAtEnd: woolAtEnd);
        // a widened donut hub-entry raises the ring-edge floor past the default min box — gate it here so an
        // oversized attachment returns the directed TooSmall instead of throwing out of the emitter
        if (family == ShapeFamily.Donut && attachmentWidth > laneWidth)
            minH = Math.Max(minH, attachmentWidth + laneWidth);
        if (canonW < minW || canonH < minH)
        {
            int minAlong = famTransposes ? minH : minW, minDepth = famTransposes ? minW : minH;
            rejection = lateral ? new FillRejection.TooSmall(minDepth, minAlong)
                                : new FillRejection.TooSmall(minAlong, minDepth);
            return null;
        }

        // A knob combination the emitter does not build (a side-tuck room on an L, say) is a directed rejection
        // like every other refusal here.
        Compound raw;
        try
        {
            raw = ShapeEmitter.Emit(family, canonW, canonH, laneWidth, flip, roomPlacement,
                woolAtEnd: woolAtEnd, attachmentWidth: attachmentWidth, ringWalls: ringWalls);
        }
        catch (ArgumentException e)
        {
            rejection = new FillRejection.UnsupportedKnobs(e.Message);
            return null;
        }
        var (mouthTop, w, h) = ShapeEmitter.OrientMouthTop(raw, family, canonW, canonH);
        return mouthTop.Turned(mouth.TopOnto(), w, h);
    }
}
