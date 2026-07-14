namespace PgmStudio.Pgm.Shapes;

/// <summary>The base shape families — one taxonomy for both the emit side (fill a box with a family) and the
/// derive side (classify a box back to a family), so the mirror closes as <c>derived == requested</c> on a
/// single enum. The shape is kind-agnostic: a wool box, a spawn box, or any terminal-capped approach reads the
/// same family. <see cref="I"/>/<see cref="L"/>/<see cref="Z"/>/<see cref="Scythe"/> are the open path by turn
/// count (an S with a fold wraps a bay = scythe); <see cref="U"/> and <see cref="H"/> are the two-leg branch,
/// split by how the terminal docks the crossbar — <see cref="U"/> flush against a bar wider than itself,
/// <see cref="H"/> capping its own room-run stub; <see cref="Clamp"/> catches the terminal between two bars (it
/// bridges them); <see cref="Donut"/> encloses a void; <see cref="Isolated"/> has no terrain approach (a
/// derive-only reading — the emitter refuses it). See the catalog in <c>docs/contracts/map-generation.md</c> §5.</summary>
public enum ShapeFamily { Isolated, I, L, Z, Scythe, Clamp, U, H, Donut }
