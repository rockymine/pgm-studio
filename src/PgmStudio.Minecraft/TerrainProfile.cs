using PgmStudio.Geom.Algorithms;

namespace PgmStudio.Minecraft;

/// <summary>The theme-agnostic geometric facts of one paintable terrain column
/// (docs/world-export/terrain-painting.md §5, stage 1). Three nested edge tests, widest last:
/// <see cref="VoidEdge"/> fires only where the column borders the void — the landmass's true outside;
/// <see cref="OpenEdge"/> adds a lower neighbour, so any drop counts (the base rim); <see cref="ClosedEdge"/>
/// adds every other plateau boundary (a structure, or a different plateau at the same height — TP3). A
/// staircase of stacked plateaus is open-edged and closed-edged all the way up its treads, and void-edged only
/// at its outer face, which is the distinction a rim mode picks between.
/// <see cref="VoidDrop"/>/<see cref="TerrainDrop"/> are the shallowest exposed
/// drop's floor Y toward the void / toward lower terrain, or -1 when there is none — the wall's lower bound,
/// split so the TP9 toggle can take the void one alone. <see cref="PerimeterArc"/> is the column's arc index
/// along its landmass's outer void-facing perimeter (0-based around the loop), or -1 when the column is not on
/// an outer boundary — what a wall-run pattern reads to wrap the perimeter (TP13).</summary>
public readonly record struct ColumnProfile(
    int SurfaceTop, bool VoidEdge, bool OpenEdge, bool ClosedEdge, int VoidDrop, int TerrainDrop,
    int PerimeterArc = -1);

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
    /// <summary>Everything the classifier asks about one cell, answered by a single lookup. Kept together
    /// because the pass reads a dozen neighbours per cell and the lookups, not the work between them, are the
    /// cost: held as three separate tables this was three or four hashes of the same coordinate pair per
    /// neighbour.</summary>
    private readonly record struct CellFacts(int Top, bool IsStructure, int Plateau);

    private readonly Dictionary<(int, int), CellFacts> _facts;
    private readonly Dictionary<(int, int), int> _perimeterArc = [];
    private readonly Dictionary<(int, int), ColumnProfile> _columns = [];

    public TerrainProfile(VoxelWorld world, IReadOnlyDictionary<(int X, int Z), int> surfaceTop)
    {
        // A column is a structure (not paintable) when it has no stone to paint — its surface block is not
        // stone (a stamp's bedrock/wool/obsidian sits there or the column is a bare bedrock course).
        var structures = new HashSet<(int, int)>();
        foreach (var (cell, top) in surfaceTop)
            if (top <= 1 || world.GetBlock(cell.X, top - 1, cell.Z).Id != Blocks.Stone) structures.Add(cell);

        var plateaus = LabelPlateaus(surfaceTop);

        _facts = new Dictionary<(int, int), CellFacts>(surfaceTop.Count);
        foreach (var (cell, top) in surfaceTop)
            _facts[cell] = new CellFacts(top, structures.Contains(cell), plateaus[cell]);

        LabelPerimeter(surfaceTop.Keys);

        foreach (var (cell, facts) in _facts)
        {
            if (facts.IsStructure) continue;    // structures are never painted (TP6)
            _columns[cell] = Classify(cell.Item1, cell.Item2, facts);
        }
    }

    /// <summary>Every paintable column with its facts — what the band resolver consumes.</summary>
    public IEnumerable<((int X, int Z) Cell, ColumnProfile Profile)> PaintableColumns()
        => _columns.Select(kv => (kv.Key, kv.Value));

    /// <summary>One cell's facts, or false where the cell is not paintable (off the footprint, or a structure
    /// column the paint leaves alone — TP6). For a caller that walks the footprint itself and needs to know,
    /// per cell, whether the painter has an opinion about it.</summary>
    public bool TryGetColumn((int X, int Z) cell, out ColumnProfile column) => _columns.TryGetValue(cell, out column);

    private ColumnProfile Classify(int x, int z, CellFacts self)
    {
        bool voidEdge = false, openEdge = false, closedEdge = false;
        foreach (var (dx, dz) in GridComponents.N8)
        {
            // Off the footprint is the void, and the void is an edge under every test — it is the only one a
            // plateau standing on other plateaus never has.
            if (!_facts.TryGetValue((x + dx, z + dz), out var n)) { voidEdge = true; openEdge = true; closedEdge = true; continue; }
            if (n.IsStructure) { closedEdge = true; continue; }
            if (n.Top < self.Top) openEdge = true;                   // a drop — base rim + wall
            if (n.Plateau != self.Plateau) closedEdge = true;         // any plateau boundary — closed rim
        }

        // Wall lower bound: the shallowest orthogonal drop's floor. Void drops to the bedrock course (Y=1);
        // a terrain drop stops at the lower neighbour's surface. Structures are never a drop (TP6).
        int voidDrop = -1, terrainDrop = -1;
        foreach (var (dx, dz) in GridComponents.N4)
        {
            if (!_facts.TryGetValue((x + dx, z + dz), out var n)) { voidDrop = 1; continue; }
            if (n.IsStructure) continue;
            if (n.Top < self.Top) terrainDrop = terrainDrop < 0 ? n.Top : Math.Min(terrainDrop, n.Top);
        }
        return new ColumnProfile(self.Top, voidEdge, openEdge, closedEdge, voidDrop, terrainDrop,
            _perimeterArc.GetValueOrDefault((x, z), -1));
    }

    // 4-connected components of equal surface top over the whole footprint (structures included, so a plateau
    // boundary is seen from the terrain side). Ids are used only for equality, so any consistent numbering does.
    private static Dictionary<(int, int), int> LabelPlateaus(IReadOnlyDictionary<(int X, int Z), int> surfaceTop)
    {
        var plateaus = new Dictionary<(int, int), int>(surfaceTop.Count);
        var components = GridComponents.Label(surfaceTop.Keys, connectivity: 4,
            canJoin: (a, b) => surfaceTop[a] == surfaceTop[b]);
        for (var id = 0; id < components.Count; id++)
            foreach (var cell in components[id]) plateaus[cell] = id;
        return plateaus;
    }

    // The outer void-facing perimeter (TP13): split the footprint into connected landmasses (4-connected, all
    // elevations — a structure is solid ground here so the outline stays whole) and Moore-trace each one's outer
    // boundary, numbering its boundary cells 0..n-1 around the loop. A wall-run reads this arc so its stripes
    // wrap the whole perimeter continuously, corners included. Interior cells and internal elevation steps (which
    // face lower terrain, not void) are on no outer boundary and keep -1.
    private void LabelPerimeter(IEnumerable<(int X, int Z)> footprint)
    {
        foreach (var landmass in GridComponents.Label(footprint, connectivity: 4))
            foreach (var (cell, arc) in GridBoundary.TracePerimeter(landmass))
                _perimeterArc[cell] = arc;
    }
}
