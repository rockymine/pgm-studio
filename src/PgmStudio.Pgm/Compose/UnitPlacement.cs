namespace PgmStudio.Pgm.Compose;

/// <summary>
/// Where a finished team unit sits <b>across</b> the symmetry axis — the placement half of "build the unit,
/// then place it".
///
/// <para>The allocator lays a unit out around a hub whose lateral span it centres on the axis, and the
/// frontline is then seated onto whatever run the hub offers. So the face — the front-most row of terrain,
/// which is the only thing the mid actually docks — ends up wherever hub seating happened to put it. Under a
/// laterally-flipping symmetry that is the wrong anchor: the opposing image reflects <c>v</c>, so an
/// off-centre face and its own image land on opposite sides of the axis and <see cref="MidCarver"/> must span
/// the hull of both, leaving band that borders neither front.</para>
///
/// <para>This re-anchors the unit on its face. The unit is translated across the axis so the face is centred
/// on the axis point, which makes the face and its image coincide and the band exactly the face. Nothing
/// inside the unit moves relative to anything else — the funnel is preserved, because a face seated
/// off-centre <em>on its hub</em> stays off-centre on its hub; what changes is that the hub, not the face,
/// is now the thing allowed to sit off the axis. The two hubs consequently no longer mirror onto the same
/// lateral position, which is the point.</para>
///
/// <para>A mirror symmetry needs none of this — it preserves the cross axis, so a face already coincides with
/// its image wherever it sits, and re-anchoring would only cost lateral variety.</para>
/// </summary>
public static class UnitPlacement
{
    /// <summary>The face's lateral extent in frame coordinates: the <c>v</c> hull of the front-most row of
    /// terrain. Read off the emitted pieces rather than the frontline's box, because a branch front's face is
    /// its arm tips — the box can be wider than what actually fronts the mid.</summary>
    internal static (int Lo, int Hi)? FaceExtent(GrownUnit unit, Frame frame)
    {
        if (unit.Pieces.Count == 0) return null;
        var uv = unit.Pieces.Select(p => frame.FromRect(p.Rect)).ToList();
        var minU = uv.Min(r => r.UMin);
        var front = uv.Where(r => r.UMin == minU).ToList();
        return (front.Min(r => r.VMin), front.Max(r => r.VMin + r.VSpan));
    }

    /// <summary>Translate <paramref name="unit"/> across the axis so its face is centred on the axis point,
    /// under a laterally-flipping <paramref name="symmetry"/>; returns it untouched otherwise, and untouched
    /// when it has no terrain to measure.
    ///
    /// <para>An odd face hull cannot centre on whole cells — its midpoint falls between two of them — so the
    /// shift is rounded and the residual half cell is left for the composer's own front-slack gate to judge.
    /// The face-parity law keeps the common case exact.</para></summary>
    public static GrownUnit CentreFaceOnAxis(GrownUnit unit, string symmetry)
    {
        if (!MidCarver.LateralFlip(symmetry)) return unit;
        var frame = Frame.For(symmetry);
        if (FaceExtent(unit, frame) is not { } face) return unit;

        // the shift that puts the face's midpoint on the axis point; (lo + hi) is even exactly when the hull is
        // even, so this is a whole cell whenever the parity law held
        var shift = -(int)Math.Round((face.Lo + face.Hi) / 2.0, MidpointRounding.AwayFromZero);
        if (shift == 0) return unit;

        // map the frame-space shift onto the board axes without restating which axis v is
        var (ox, oz) = frame.ToPoint(0, 0);
        var (px, pz) = frame.ToPoint(0, shift);
        int dx = (int)Math.Round(px - ox), dz = (int)Math.Round(pz - oz);

        // markers are piece-relative offsets, so moving the pieces moves the objectives with them
        return unit with
        {
            Pieces = unit.Pieces
                .Select(p => p with { Rect = new(p.Rect.X + dx, p.Rect.Z + dz, p.Rect.Width, p.Rect.Height) })
                .ToList(),
        };
    }
}
