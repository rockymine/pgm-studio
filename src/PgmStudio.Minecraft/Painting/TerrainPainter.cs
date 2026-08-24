using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Palette;
namespace PgmStudio.Minecraft.Painting;

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
    /// <summary>Paint the whole footprint with one map-wide theme (TP1–TP13). Runs last in the world build,
    /// after every stamp, so the profile reads the finished world. <paramref name="teamDamageAt"/> gives a
    /// cell's owning team as a 0–15 wool/clay damage nibble (-1 = neutral) — what a team-tinted material reads,
    /// on any bucket; the default is neutral everywhere.</summary>
    public static void Paint(VoxelWorld world, IReadOnlyDictionary<(int X, int Z), int> surfaceTop, TerrainTheme theme,
        Func<int, int, int>? teamDamageAt = null)
        => Paint(world, surfaceTop, (_, _) => theme, teamDamageAt);

    /// <summary>Paint a board one layer at a time, each storey against its own surface and its own theme.
    /// A cell on two layers is painted twice — once per surface it carries — which is what puts turf on a
    /// gallery floor and a meadow on the deck roofing it. The stone-only invariant keeps the passes from
    /// treading on each other: a course a lower layer has already finished is no longer stone.
    ///
    /// <para>Layers paint in the order the document draws them, so where two genuinely meet flush the lower
    /// one's finish is what stands.</para></summary>
    public static void Paint(VoxelWorld world,
        IReadOnlyDictionary<string, IReadOnlyDictionary<(int X, int Z), int>> surfaceByLayer,
        Func<string, int, int, TerrainTheme> themeAt, Func<int, int, int>? teamDamageAt = null)
    {
        foreach (var (layer, tops) in surfaceByLayer)
            Paint(world, tops, (x, z) => themeAt(layer, x, z), teamDamageAt);
    }

    /// <summary>Paint the footprint with a <b>per-cell</b> theme (TP10): <paramref name="themeAt"/> resolves the
    /// theme governing each cell — a piece override, its collection, or the map default. Each column resolves
    /// its bands and materials against its own theme; the profile is theme-agnostic, so per-cell theming needs
    /// no new geometry. The single-theme overload is this with a constant resolver.</summary>
    public static void Paint(VoxelWorld world, IReadOnlyDictionary<(int X, int Z), int> surfaceTop,
        Func<int, int, TerrainTheme> themeAt, Func<int, int, int>? teamDamageAt = null)
    {
        var team = teamDamageAt ?? ((_, _) => -1);
        var profile = new TerrainProfile(world, surfaceTop);
        foreach (var (cell, column) in profile.PaintableColumns())
        {
            foreach (var (y, id, data) in ColumnBlocks(cell.X, cell.Z, column, themeAt(cell.X, cell.Z), team(cell.X, cell.Z)))
            {
                if (world.GetBlock(cell.X, y, cell.Z).Id != Blocks.Stone) continue;   // stone-only invariant
                if (id != Blocks.Stone || data != 0) world.SetBlock(cell.X, y, cell.Z, id, data);
            }
        }
    }

    /// <summary>
    /// The blocks one column paints, <b>top cell first</b>, as <c>(y, id, data)</c>. This is where a resolved
    /// band becomes actual blocks, and it is deliberately the only such place: <see cref="Paint"/> walks the
    /// whole sequence and writes it into the world, while a top-down preview takes the first element and
    /// stops. Neither can resolve a cell differently from the other, and the preview pays one material
    /// resolve per column rather than one per block — the difference between reading a footprint's surface
    /// and building its every block to read one of them.
    /// <para>What the world already holds is not consulted here: the caller applies the stone-only invariant,
    /// because whether a cell may be overwritten is a fact about the world, not about the column.</para>
    /// </summary>
    public static IEnumerable<(int Y, int Id, int Data)> ColumnBlocks(
        int x, int z, ColumnProfile column, TerrainTheme theme, int teamDamage = -1)
    {
        var bands = Resolve(column, theme);
        for (var i = bands.Count - 1; i >= 0; i--)   // top band first — the preview wants only its top cell
        {
            var band = bands[i];
            var material = band.Bucket == TerrainBucket.Bedrock ? Bedrock : theme.MaterialFor(band.Bucket);
            for (var y = band.HiY - 1; y >= band.LoY; y--)
            {
                var (id, data) = material.Resolve(
                    new BucketContext(x, y, z, band.Bucket, band.HiY - 1 - y, teamDamage, column.PerimeterArc,
                        y - band.LoY, column.PerimeterTurn, column.PerimeterRun, column.Inset));
                yield return (y, id, data);
            }
        }
    }

    /// <summary>The block a column shows from directly above — <see cref="ColumnBlocks"/>'s first entry, which
    /// is the top cell of the topmost band. Null only for a column that paints nothing at all.</summary>
    public static (int Y, int Id, int Data)? TopBlock(
        int x, int z, ColumnProfile column, TerrainTheme theme, int teamDamage = -1)
    {
        foreach (var block in ColumnBlocks(x, z, column, theme, teamDamage)) return block;
        return null;
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
        // Which edges count is the theme's (TP3) — the void alone, every drop, or every plateau boundary.
        var isRim = theme.RimEdges switch
        {
            RimEdges.Void => column.VoidEdge,
            RimEdges.Boundary => column.ClosedEdge,
            _ => column.OpenEdge,
        };
        TerrainBucket topBucket;
        int topDepth;
        if (isRim && theme.Rim.Enabled) { topBucket = TerrainBucket.Rim; topDepth = Math.Max(1, theme.Rim.Depth); }
        else if (theme.Surface.Enabled) { topBucket = TerrainBucket.Surface; topDepth = Math.Max(1, theme.Surface.Depth); }
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
