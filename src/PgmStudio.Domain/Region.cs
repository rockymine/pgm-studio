namespace PgmStudio.Domain;

/// <summary>A normalized 2D footprint (min ≤ max), the parser's derived <c>bounds_2d</c>.</summary>
public sealed class Bounds2d
{
    public double MinX, MinZ, MaxX, MaxZ;

    /// <summary>Build a normalized bounds (min always ≤ max) — mirrors regions._b2d.</summary>
    public static Bounds2d Of(double minX, double minZ, double maxX, double maxZ) => new()
    {
        MinX = Math.Min(minX, maxX),
        MinZ = Math.Min(minZ, maxZ),
        MaxX = Math.Max(minX, maxX),
        MaxZ = Math.Max(minZ, maxZ),
    };
}

/// <summary>
/// A PGM region, flat across all 17 types (discriminated by <see cref="Type"/>). Only the
/// fields relevant to a given type are populated; the serializer emits exactly the type's
/// field set (mirrors the Python <c>Region</c> hierarchy + serializer <c>_encode_region</c>).
/// Coordinate fields are nullable: <c>null</c> models a template-variable component.
/// </summary>
public sealed class Region
{
    public string Id = "";
    public string Type = "unknown";
    public Bounds2d? Bounds2d;

    // cuboid / rectangle (rectangle uses only the X/Z pair)
    public double? MinX, MinY, MinZ, MaxX, MaxY, MaxZ;
    // cylinder
    public double? BaseX, BaseY, BaseZ, Height;
    public double? Radius;            // cylinder / circle / sphere
    // circle
    public double? CenterX, CenterZ;
    // sphere / half / mirror origin
    public double? OriginX, OriginY, OriginZ;
    // block / point position
    public double? PosX, PosY, PosZ;
    // composites (union / negative / complement / intersect)
    public List<string>? Children;
    // transforms
    public string? SourceId;
    public double? NormalX, NormalY, NormalZ;   // half / mirror
    public double? OffsetX, OffsetY, OffsetZ;    // translate
    // reference
    public string? RefId;
    // above
    public double? AboveY;
}
