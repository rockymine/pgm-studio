namespace PgmStudio.Minecraft;

/// <summary>One vertical run of a column assigned to a bucket, min-inclusive / max-exclusive in Y.</summary>
public readonly record struct TerrainBand(int LoY, int HiY, TerrainBucket Bucket);

/// <summary>
/// Terrain painting (docs/world-export/terrain-painting.md): dresses the raw stone a finished world exports —
/// clay walls, quartz rims, grass surface — reading the <see cref="TerrainProfile"/> core and a
/// <see cref="TerrainTheme"/>. Two remaining stages sit here: the pure band <see cref="Resolve"/> (which Y is
/// bedrock / fill / wall / surface / rim, TP7/TP8/TP11/TP12) and the paint loop that resolves each band's
/// material and writes it. It touches <b>only stone</b>, so bedrock and every stamped structure are left
/// exactly as the terrain builder and stampers placed them (TP6) and a re-run is idempotent.
/// </summary>
public static class TerrainPainter
{
    /// <summary>Paint the whole footprint with one map-wide theme. Runs last in the world build, after every
    /// stamp, so the profile reads the finished world. <paramref name="teamDamageAt"/> gives a cell's owning
    /// team as a 0–15 wool/clay damage nibble (-1 = neutral) — what a team-tinted material reads, on any
    /// bucket; the default is neutral everywhere.</summary>
    public static void Paint(VoxelWorld world, IReadOnlyDictionary<(int X, int Z), int> surfaceTop, TerrainTheme theme,
        Func<int, int, int>? teamDamageAt = null)
    {
        var team = teamDamageAt ?? ((_, _) => -1);
        var profile = new TerrainProfile(world, surfaceTop);
        foreach (var (cell, column) in profile.PaintableColumns())
        {
            var teamData = team(cell.X, cell.Z);
            foreach (var band in Resolve(column, theme))
            {
                var material = band.Bucket == TerrainBucket.Bedrock ? Bedrock : theme.MaterialFor(band.Bucket);
                for (var y = band.LoY; y < band.HiY; y++)
                {
                    if (world.GetBlock(cell.X, y, cell.Z).Id != Blocks.Stone) continue;   // stone-only invariant
                    var (id, data) = material.Resolve(new BucketContext(cell.X, y, cell.Z, band.Bucket, band.HiY - 1 - y, teamData));
                    if (id != Blocks.Stone || data != 0) world.SetBlock(cell.X, y, cell.Z, id, data);
                }
            }
        }
    }

    private static readonly TerrainMaterial Bedrock = new SolidMaterial(Blocks.Bedrock);

    /// <summary>The pure band resolver: split one column into bedrock / fill / wall / (rim|surface) by the
    /// theme's depth knobs and toggles. Resolution order is bottom-up bedrock, then top-down rim-or-surface,
    /// then the wall filling the exposed riser below the top course, then fill takes the middle — each band
    /// claiming only what the band above it left.</summary>
    public static IReadOnlyList<TerrainBand> Resolve(ColumnProfile column, TerrainTheme theme)
    {
        var top = column.SurfaceTop;
        var bands = new List<TerrainBand>();

        // Bedrock claims the bottom (TP8). Nothing left above ⇒ the whole column is bedrock, no rim/wall.
        var paintFloor = theme.Bedrock.PaintFloor(top);
        bands.Add(new TerrainBand(0, paintFloor, TerrainBucket.Bedrock));
        if (paintFloor >= top) return bands;

        // The top course: the rim on an edge (else it falls to the surface, TP12), the surface on an interior.
        var isRim = theme.Closed ? column.ClosedEdge : column.OpenEdge;
        TerrainBucket topBucket;
        int topDepth;
        if (isRim && theme.RimEnabled) { topBucket = TerrainBucket.Rim; topDepth = Math.Max(1, theme.RimDepth); }
        else if (theme.SurfaceEnabled) { topBucket = TerrainBucket.Surface; topDepth = Math.Max(1, theme.SurfaceDepth); }
        else { topBucket = TerrainBucket.Fill; topDepth = 0; }
        var treatLo = topDepth > 0 ? Math.Max(paintFloor, top - topDepth) : top;

        // The wall is the exposed riser below the top course, from the shallowest drop up (TP4/TP9). Off, or a
        // drop shallower than one course, leaves no wall — those blocks stay fill.
        var drop = -1;
        if (theme.WallEnabled)
            drop = MinNonNeg(column.VoidDrop, theme.WallOnTerrainFaces ? column.TerrainDrop : -1);
        var wallLo = drop >= 0 ? Math.Clamp(drop, paintFloor, treatLo) : treatLo;

        if (paintFloor < wallLo) bands.Add(new TerrainBand(paintFloor, wallLo, TerrainBucket.Fill));
        if (wallLo < treatLo) bands.Add(new TerrainBand(wallLo, treatLo, TerrainBucket.Wall));
        if (treatLo < top) bands.Add(new TerrainBand(treatLo, top, topBucket));
        return bands;
    }

    private static int MinNonNeg(int a, int b)
        => a < 0 ? b : b < 0 ? a : Math.Min(a, b);
}
