namespace PgmStudio.Pgm.Shapes;

/// <summary>Which box kind's <b>designation</b> finishes a terminal-free <see cref="ShapeBody"/> into a placed
/// box (docs/generator/model.md §3). <see cref="Approach"/> stamps an <c>entry</c> and a
/// <c>room</c> terminal — the wool/spawn approach. <see cref="Hub"/> carries a width per free run,
/// <b>no terminal</b> — the constraint source. <see cref="Frontline"/> designates one edge the face,
/// <b>no terminal</b> — where the fanned images meet.
///
/// <para>The body layer is shared across every box kind; the designation is per-kind. It is the
/// <b>designation, not the box kind</b>, that decides a mark's dock role (<see cref="DesignationMarks"/>,
/// <c>DockingGate.Role</c>): wool and spawn are both the <see cref="Approach"/> designation, so both read the
/// same docking law.</para></summary>
public enum Designation { Approach, Hub, Frontline }

/// <summary>The <b>designation marks</b> the non-approach designations stamp onto a body's edges — the siblings
/// of the approach's <see cref="ApproachSlots.Entry"/> / <see cref="ApproachSlots.Room"/> marks
/// (model.md §4 — the shape model). <see cref="Interface"/> is a hub edge that sources a per-edge width a
/// neighbour docks; <see cref="Face"/> is the frontline edge the mid meets. <b>Nothing stamps these yet</b> —
/// the hub publishes <c>EdgeOffer</c>s and the frontline returns its face edge directly; these constants exist
/// so <c>DockingGate.Role</c> can already map them to a dock role per designation. The approach keeps
/// <c>entry</c>/<c>room</c>, stamped as <see cref="ApproachSlots"/>.</summary>
public static class DesignationMarks
{
    public const string Interface = "interface";
    public const string Face = "face";
}
