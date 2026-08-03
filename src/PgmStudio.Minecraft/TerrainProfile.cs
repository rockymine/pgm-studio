using PgmStudio.Geom.Algorithms;

namespace PgmStudio.Minecraft;

/// <summary>The theme-agnostic geometric facts of one paintable terrain column
/// (docs/world-export/terrain-painting.md §5, stage 1). <see cref="OpenEdge"/> fires on a void or lower
/// neighbour (the base rim), <see cref="ClosedEdge"/> on any plateau boundary (void, a structure, or a
/// different plateau — TP3). <see cref="VoidDrop"/>/<see cref="TerrainDrop"/> are the shallowest exposed
/// drop's floor Y toward the void / toward lower terrain, or -1 when there is none — the wall's lower bound,
/// split so the TP9 toggle can take the void one alone. <see cref="PerimeterArc"/> is the column's arc index
/// along its landmass's outer void-facing perimeter (0-based around the loop), or -1 when the column is not on
/// an outer boundary — what a wall-run pattern reads to wrap the perimeter (TP13).</summary>
public readonly record struct ColumnProfile(int SurfaceTop, bool OpenEdge, bool ClosedEdge, int VoidDrop, int TerrainDrop, int PerimeterArc = -1);

/// <summary>
/// The shared core of terrain painting (docs/world-export/terrain-painting.md §5, stage 1): classifies every
/// stone column of a finished world into <see cref="ColumnProfile"/> facts, reading nothing but the world and
/// the per-cell surface top. Because it runs on the <em>finished</em> world, consulting the stamps is free —
/// a column whose top block is not stone is a structure (a room plateau, a bedrock approach wall, an
/// objective), excluded from painting and read as a height-bearing, face-sealing neighbour (TP6). Pure and
/// theme-agnostic, so the same profile serves every theme, scope and pattern.
/// </summary>
public sealed class TerrainProfile
{
    private readonly IReadOnlyDictionary<(int X, int Z), int> _surfaceTop;
    private readonly HashSet<(int, int)> _structure = [];
    private readonly Dictionary<(int, int), int> _plateau = [];
    private readonly Dictionary<(int, int), int> _perimeterArc = [];
    private readonly Dictionary<(int, int), ColumnProfile> _columns = [];

    public TerrainProfile(VoxelWorld world, IReadOnlyDictionary<(int X, int Z), int> surfaceTop)
    {
        _surfaceTop = surfaceTop;

        // A column is a structure (not paintable) when it has no stone to paint — its surface block is not
        // stone (a stamp's bedrock/wool/obsidian sits there or the column is a bare bedrock course).
        foreach (var (cell, top) in surfaceTop)
            if (top <= 1 || world.GetBlock(cell.X, top - 1, cell.Z).Id != Blocks.Stone) _structure.Add(cell);

        LabelPlateaus();
        LabelPerimeter();

        foreach (var (cell, top) in surfaceTop)
        {
            if (_structure.Contains(cell)) continue;    // structures are never painted (TP6)
            _columns[cell] = Classify(cell.X, cell.Z, top);
        }
    }

    /// <summary>Every paintable column with its facts — what the band resolver consumes.</summary>
    public IEnumerable<((int X, int Z) Cell, ColumnProfile Profile)> PaintableColumns()
        => _columns.Select(kv => (kv.Key, kv.Value));

    /// <summary>One cell's facts, or false where the cell is not paintable (off the footprint, or a structure
    /// column the paint leaves alone — TP6). For a caller that walks the footprint itself and needs to know,
    /// per cell, whether the painter has an opinion about it.</summary>
    public bool TryGetColumn((int X, int Z) cell, out ColumnProfile column) => _columns.TryGetValue(cell, out column);

    private bool InFootprint(int x, int z) => _surfaceTop.ContainsKey((x, z));
    private bool IsStructure(int x, int z) => _structure.Contains((x, z));
    private int Top(int x, int z) => _surfaceTop[(x, z)];

    private ColumnProfile Classify(int x, int z, int top)
    {
        bool openEdge = false, closedEdge = false;
        var plateau = _plateau[(x, z)];
        foreach (var (dx, dz) in GridComponents.N8)
        {
            var (nx, nz) = (x + dx, z + dz);
            if (!InFootprint(nx, nz)) { openEdge = true; closedEdge = true; continue; }
            if (IsStructure(nx, nz)) { closedEdge = true; continue; }
            if (Top(nx, nz) < top) openEdge = true;                 // a drop — base rim + wall
            if (_plateau[(nx, nz)] != plateau) closedEdge = true;    // any plateau boundary — closed rim
        }

        // Wall lower bound: the shallowest orthogonal drop's floor. Void drops to the bedrock course (Y=1);
        // a terrain drop stops at the lower neighbour's surface. Structures are never a drop (TP6).
        int voidDrop = -1, terrainDrop = -1;
        foreach (var (dx, dz) in GridComponents.N4)
        {
            var (nx, nz) = (x + dx, z + dz);
            if (!InFootprint(nx, nz)) { voidDrop = 1; continue; }
            if (IsStructure(nx, nz)) continue;
            var nt = Top(nx, nz);
            if (nt < top) terrainDrop = terrainDrop < 0 ? nt : Math.Min(terrainDrop, nt);
        }
        return new ColumnProfile(top, openEdge, closedEdge, voidDrop, terrainDrop, _perimeterArc.GetValueOrDefault((x, z), -1));
    }

    // 4-connected components of equal surface top over the whole footprint (structures included, so a plateau
    // boundary is seen from the terrain side). Ids are used only for equality, so any consistent numbering does.
    private void LabelPlateaus()
    {
        var components = GridComponents.Label(_surfaceTop.Keys, connectivity: 4,
            canJoin: (a, b) => _surfaceTop[a] == _surfaceTop[b]);
        for (var id = 0; id < components.Count; id++)
            foreach (var cell in components[id]) _plateau[cell] = id;
    }

    // The outer void-facing perimeter (TP13): split the footprint into connected landmasses (4-connected, all
    // elevations — a structure is solid ground here so the outline stays whole) and Moore-trace each one's outer
    // boundary, numbering its boundary cells 0..n-1 around the loop. A wall-run reads this arc so its stripes
    // wrap the whole perimeter continuously, corners included. Interior cells and internal elevation steps (which
    // face lower terrain, not void) are on no outer boundary and keep -1.
    private void LabelPerimeter()
    {
        foreach (var landmass in GridComponents.Label(_surfaceTop.Keys, connectivity: 4))
            foreach (var (cell, arc) in GridBoundary.TracePerimeter(landmass))
                _perimeterArc[cell] = arc;
    }
}
