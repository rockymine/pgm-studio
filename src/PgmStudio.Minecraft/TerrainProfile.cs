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
    private static readonly (int dx, int dz)[] N4 = [(1, 0), (-1, 0), (0, 1), (0, -1)];
    private static readonly (int dx, int dz)[] N8 =
        [(1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1)];

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

    private bool InFootprint(int x, int z) => _surfaceTop.ContainsKey((x, z));
    private bool IsStructure(int x, int z) => _structure.Contains((x, z));
    private int Top(int x, int z) => _surfaceTop[(x, z)];

    private ColumnProfile Classify(int x, int z, int top)
    {
        bool openEdge = false, closedEdge = false;
        var plateau = _plateau[(x, z)];
        foreach (var (dx, dz) in N8)
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
        foreach (var (dx, dz) in N4)
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
    // boundary is seen from the terrain side). Iterative flood fill — footprints run to thousands of cells.
    private void LabelPlateaus()
    {
        var id = 0;
        var stack = new Stack<(int, int)>();
        foreach (var (start, _) in _surfaceTop)
        {
            if (_plateau.ContainsKey(start)) continue;
            var top = _surfaceTop[start];
            stack.Push(start);
            while (stack.Count > 0)
            {
                var cell = stack.Pop();
                if (_plateau.ContainsKey(cell)) continue;
                _plateau[cell] = id;
                foreach (var (dx, dz) in N4)
                {
                    var nb = (cell.Item1 + dx, cell.Item2 + dz);
                    if (InFootprint(nb.Item1, nb.Item2) && !_plateau.ContainsKey(nb) && Top(nb.Item1, nb.Item2) == top)
                        stack.Push(nb);
                }
            }
            id++;
        }
    }

    // The outer void-facing perimeter (TP13): split the footprint into connected landmasses (4-connected, all
    // elevations — a structure is solid ground here so the outline stays whole) and Moore-trace each one's outer
    // boundary, numbering its boundary cells 0..n-1 around the loop. A wall-run reads this arc so its stripes
    // wrap the whole perimeter continuously, corners included. Interior cells and internal elevation steps (which
    // face lower terrain, not void) are on no outer boundary and keep -1.
    private void LabelPerimeter()
    {
        var seen = new HashSet<(int, int)>();
        var stack = new Stack<(int, int)>();
        foreach (var start in _surfaceTop.Keys)
        {
            if (seen.Contains(start)) continue;
            var landmass = new HashSet<(int, int)>();
            stack.Push(start);
            while (stack.Count > 0)
            {
                var cell = stack.Pop();
                if (!seen.Add(cell)) continue;
                landmass.Add(cell);
                foreach (var (dx, dz) in N4)
                {
                    var nb = (cell.Item1 + dx, cell.Item2 + dz);
                    if (InFootprint(nb.Item1, nb.Item2) && !seen.Contains(nb)) stack.Push(nb);
                }
            }
            MooreTrace(landmass);
        }
    }

    // Clockwise Moore-neighbour boundary tracing of one landmass (Jacob's stopping criterion), writing each outer
    // boundary cell its 0-based arc index into _perimeterArc. Cells already indexed (a thin neck revisited) keep
    // their first index; each landmass numbers from 0 so a run wraps its own loop.
    private static readonly (int dx, int dz)[] Cw =
        [(0, -1), (1, -1), (1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1)];
    private void MooreTrace(HashSet<(int, int)> cells)
    {
        if (cells.Count == 0) return;
        bool Solid(int x, int z) => cells.Contains((x, z));
        static int Idx(int dx, int dz) { for (var i = 0; i < 8; i++) if (Cw[i].dx == dx && Cw[i].dz == dz) return i; return -1; }
        var start = cells.OrderBy(c => c.Item2).ThenBy(c => c.Item1).First();   // on the boundary, entered from the west
        var p = start;
        int backIdx = Idx(-1, 0), startBack = -1, s = 0, guard = 0;
        bool moved = false;
        if (!_perimeterArc.ContainsKey(start)) _perimeterArc[start] = s++;
        while (guard++ < 1_000_000)
        {
            int found = -1;
            for (var k = 1; k <= 8; k++)
            {
                int idx = (backIdx + k) % 8;
                if (Solid(p.Item1 + Cw[idx].dx, p.Item2 + Cw[idx].dz)) { found = idx; break; }
            }
            if (found < 0) break;                                       // isolated cell
            var c = (p.Item1 + Cw[found].dx, p.Item2 + Cw[found].dz);
            int prevIdx = (found - 1 + 8) % 8;                          // last background checked = new backtrack
            int newBack = Idx(p.Item1 + Cw[prevIdx].dx - c.Item1, p.Item2 + Cw[prevIdx].dz - c.Item2);
            if (moved && c == start && newBack == startBack) break;     // Jacob's stop: start reached, same entry
            if (!moved) { moved = true; startBack = newBack; }
            if (!_perimeterArc.ContainsKey(c)) _perimeterArc[c] = s++;
            p = c; backIdx = newBack;
        }
    }
}
