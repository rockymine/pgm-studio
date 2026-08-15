namespace PgmStudio.Geom;

/// <summary>
/// An axis-aligned footprint in world XZ: a <b>corner pair</b> in blocks, both ends inclusive, and fractional
/// because a spawn, a protection region and a wool room are all placed on the half-block lattice a marker
/// snaps to.
///
/// <para><b>Not <see cref="CellRect"/></b>, and the difference is the unit rather than the arithmetic:
/// <see cref="CellRect"/> is an origin plus an extent on the integer <em>cell</em> grid with an exclusive far
/// edge, this is a min/max pair in world <em>blocks</em>. A cell is several blocks wide, so a value of one
/// standing in for the other is off by that factor and compiles — which is why they stay two types.</para>
///
/// <para>It lives in <c>Geom</c> because both halves of the studio need it and only <c>Geom</c> is reachable
/// from both. The client is WASM and sees <c>Contracts</c> and <c>Geom</c> alone, so every Configure step that
/// draws a footprint had re-declared this shape — five times, beside the copy in <c>MapIntent</c> that is the
/// wire form and the one in <c>ResourceRenewables</c> in the same project as that. The declarations were never
/// a decision; each was one grep further away than reaching for the last.</para>
/// </summary>
/// <param name="MinX">Min-corner block on the x axis.</param>
/// <param name="MinZ">Min-corner block on the z axis.</param>
/// <param name="MaxX">Max-corner block on the x axis, inclusive.</param>
/// <param name="MaxZ">Max-corner block on the z axis, inclusive.</param>
public readonly record struct Rect(double MinX, double MinZ, double MaxX, double MaxZ)
{
    /// <summary>Whether the point lies on or inside the footprint. <b>Inclusive on both edges</b>, unlike
    /// <see cref="CellRect.Contains"/>, which is half-open: a block on the far edge of a protection region is
    /// inside it, where a cell at <c>MaxX</c> is the first one past the rect.</summary>
    public bool Covers(double x, double z) => x >= MinX && x <= MaxX && z >= MinZ && z <= MaxZ;
}
