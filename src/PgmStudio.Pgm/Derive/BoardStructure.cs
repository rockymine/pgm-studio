namespace PgmStudio.Pgm.Derive;

/// <summary>The structure a plan's fanned board implies — the raster-layer derivation: islands + anchor
/// roles, stepping-stone kinds, build-zone kinds/widths/interfaces, per-wool approaches + lane shapes, the
/// frontline/intra/self edges, wool-lane cells, the mid form, and enclosed voids classified by boundary.
/// Computed by <see cref="BoardDeriver.Derive"/>; consumed by the deriver gallery, the evaluator, and the
/// conformance sweep. All cell coordinates (×Cell = blocks).</summary>
public sealed record BoardStructure(
    int Cell,
    Dictionary<(int, int), (string PieceId, int K)> Filled,
    HashSet<(int, int)> Build,
    Dictionary<(int, int), string> BuildKindOf,
    List<(string Kind, int Neutrals, int Width, int IfaceMin, int IfaceMax)> Zones,
    string MidForm,
    List<HashSet<(int, int)>> Islands,
    Dictionary<(int, int), int> IslandOf,
    string[] Roles,
    string[] SteppingKind,
    List<(int Island, double Bx, double Bz, int Count)> Approaches,
    List<(string Shape, int Width)> WoolShapes,
    List<(int X1, int Z1, int X2, int Z2)> FrontEdges,
    List<(int X1, int Z1, int X2, int Z2)> IntraEdges,
    List<(int X1, int Z1, int X2, int Z2)> SelfEdges,
    HashSet<(int, int)> LaneCells,
    List<(int X1, int Z1, int X2, int Z2)> RedstoneEdges,
    List<(HashSet<(int, int)> Cells, bool Declared, string Class, int CrossRoutes)> Voids);
