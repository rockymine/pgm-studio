namespace PgmStudio.Contracts;

/// <summary>One directed finding about producibility. <see cref="Code"/> is the stable slug (the machine-legible
/// half — a panel styles off it and an agent branches on it); <see cref="Cites"/> is a <c>layout-rules.md</c> rule
/// id or the id of the task that would unblock it (<c>"G123"</c>) when one applies, else null; <see cref="Detail"/>
/// is the human half, carrying the measured numbers.</summary>
public sealed record FeasibilityFindingDto(string Code, string? Cites, string Detail);

/// <summary>The closest the emitters got when nothing reproduced a box: the candidate whose terrain differs in
/// the fewest cells, and the cells that differ. <see cref="Extra"/> are cells the candidate emits that the box
/// does not have; <see cref="Missing"/> are cells the box has that the candidate does not. Both are
/// <c>[x, z, w, h]</c> cell rects in the same frame a piece rect uses, so the canvas paints them as evidence —
/// which is what turns "8 cells differ" into a visible answer.</summary>
public sealed record NearestMissDto(
    string Label, int Cw, int DifferingCells,
    IReadOnlyList<int[]> Extra, IReadOnlyList<int[]> Missing);

/// <summary>
/// One box's producibility. <see cref="Identity"/> is what the derivers read the geometry as — a <b>hint, never a
/// verdict</b>, since the classifiers read topology and a shape can be recognisably a ring while no ring is
/// emittable at its wall widths. <see cref="ProducibleAs"/> names the parameter tuple that reproduces it exactly
/// (null when none does), <see cref="Nearest"/> the closest candidate otherwise, and <see cref="Findings"/> the
/// directed reasons.
/// </summary>
public sealed record BoxFeasibilityDto(
    string BoxId, string Kind, string Identity,
    string? ProducibleAs, int? Cw,
    NearestMissDto? Nearest,
    IReadOnlyList<FeasibilityFindingDto> Findings)
{
    public bool Producible => ProducibleAs is not null;
}

/// <summary>
/// POST /api/plan/feasibility — "could the composer have produced this plan?", the question
/// <c>/api/plan/evaluate</c> does not ask: a plan can score 0 with no rule fired and still be unbuildable by the
/// machine.
///
/// <para><see cref="Boxes"/> is the per-box read (empty when the plan has no authored boxes — the annotation this
/// hangs off). <see cref="Unit"/> is the findings that belong to the <b>arrangement</b> rather than to any one box
/// (the parallel-fronts guard, the frontline's face demand, the seat-separation law). Both are reported together:
/// a box can be unbuildable on its own geometry <i>and</i> the unit unbuildable in how it is arranged, and an
/// author wants to see all of it rather than the first failure.</para>
/// </summary>
public sealed record FeasibilityDto(
    bool Producible, IReadOnlyList<BoxFeasibilityDto> Boxes, IReadOnlyList<FeasibilityFindingDto> Unit);
