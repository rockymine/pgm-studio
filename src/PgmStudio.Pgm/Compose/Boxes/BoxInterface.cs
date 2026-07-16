using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Compose;

/// <summary>An interface: a shared <b>edge interval</b> — a position and a width — where a box meets its
/// neighbour. Never a point, never a node (docs/contracts/map-generation.md §1.5/§1.6). <see cref="Edge"/>
/// names the box-local edge the interval lies on; <see cref="Start"/> is the interval's offset along that
/// edge in box-local cells; <see cref="WidthCells"/> is the master variable — the touch width that sets
/// connectivity, classifies the joint, and gates the fill menu.</summary>
public sealed record BoxInterface(BoxEdge Edge, int Start, int WidthCells);
