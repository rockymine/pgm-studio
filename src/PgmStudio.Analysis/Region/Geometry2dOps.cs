using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Prepared;

namespace PgmStudio.Analysis.Region;

/// <summary>
/// Shared NetTopologySuite footprint operations — point-in-polygon and intersection-over-union over the
/// 2D region footprints produced by <see cref="RegionGeometry2d"/>. This is the single place that encodes
/// the block-cell sampling convention: a block at integer <c>(x,z)</c> is covered iff its cell centre
/// <c>(x+0.5, z+0.5)</c> lies inside the footprint. (The scalar affine transforms live in PgmStudio.Geom.)
/// </summary>
public static class Geometry2dOps
{
    /// <summary>True when the footprint covers the block cell at integer (x,z) — samples the cell centre.</summary>
    public static bool CoversCell(this Geometry geom, int x, int z) => geom.Contains(CellCentre(x, z));

    /// <summary>Prepared-geometry variant for whole-grid scans (the inner-loop hot path).</summary>
    public static bool CoversCell(this IPreparedGeometry prep, int x, int z) => prep.Contains(CellCentre(x, z));

    /// <summary>Intersection-over-union of two footprints; 0 when their union has no measurable area.</summary>
    public static double IoU(Geometry a, Geometry b)
    {
        var unionArea = a.Union(b).Area;
        return unionArea < 1e-6 ? 0.0 : a.Intersection(b).Area / unionArea;
    }

    private static Point CellCentre(int x, int z) => new(x + 0.5, z + 0.5);
}
