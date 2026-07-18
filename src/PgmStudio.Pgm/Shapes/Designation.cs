namespace PgmStudio.Pgm.Shapes;

/// <summary>Which box kind's <b>designation</b> finishes a terminal-free <see cref="ShapeBody"/> into a placed
/// box (docs/contracts/map-generation.md §1.12). <see cref="Approach"/> stamps an <c>entry</c> and a
/// <c>room</c> terminal — the wool/spawn approach. <see cref="Hub"/> stamps per-edge <c>interface</c> marks
/// carrying widths, <b>no terminal</b> — the constraint source. <see cref="Frontline"/> stamps a <c>face</c>
/// mark, <b>no terminal</b> — the edge where the fanned images meet.
///
/// <para>The body layer is shared across every box kind; the designation is per-kind. It is the
/// <b>designation, not the box kind</b>, that decides a mark's dock role (<see cref="DesignationMarks"/>,
/// <c>DockingGate.Role</c>): wool and spawn are both the <see cref="Approach"/> designation, so both read the
/// same docking law.</para></summary>
public enum Designation { Approach, Hub, Frontline }

/// <summary>The <b>designation marks</b> the non-approach designations stamp onto a body's edges — the siblings
/// of the approach's <see cref="ApproachSlots.Entry"/> / <see cref="ApproachSlots.Room"/> marks
/// (map-generation.md §1.12, §5.3). <see cref="Interface"/> is a hub edge that sources a per-edge width a
/// neighbour docks; <see cref="Face"/> is the frontline edge the mid meets. A designation stamps its mark and
/// the docking gate maps the mark to a dock role per designation — the approach keeps <c>entry</c>/<c>room</c>,
/// stamped as <see cref="ApproachSlots"/>.</summary>
public static class DesignationMarks
{
    public const string Interface = "interface";
    public const string Face = "face";
}
