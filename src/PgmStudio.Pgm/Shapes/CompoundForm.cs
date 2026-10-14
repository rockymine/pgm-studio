namespace PgmStudio.Pgm.Shapes;

/// <summary>
/// The one list of forms a <see cref="Compound"/> takes (docs/generator/model.md §4): what its pieces make, read
/// by topology alone and never by width. A form says nothing about whether the compound ends in a room — that is
/// the box kind's business, and each kind's menu (<see cref="Compose.FillMenu"/>) lists the forms it fills with.
/// <c>ShapeClassifier.Classify</c> reads a compound ending in a room back to its form,
/// <see cref="ShapeClassifier.ClassifyCompound"/> one without; the two close the mirror with
/// <see cref="ShapeEmitter"/> and <see cref="CompoundEmitter"/>.
///
/// <list type="bullet">
/// <item><see cref="I"/> — a straight bar: one rectangle, or a lane straight to its room.</item>
/// <item><see cref="L"/> — one turn: a spine with one arm, or a lane with one bend.</item>
/// <item><see cref="Z"/> — two turns the same way round, a staircase; two notches.</item>
/// <item><see cref="Scythe"/> — a path folded back on itself round a bay, entering and ending on one edge.</item>
/// <item><see cref="Clamp"/> — two bars bridged by the room between them: remove it and the ground falls in two.</item>
/// <item><see cref="U"/> — a spine with two arms and the bay between them; a room docks flush on the spine.</item>
/// <item><see cref="H"/> — the U whose room caps its own short stub, lifted off the spine.</item>
/// <item><see cref="Donut"/> — four bars round one enclosed hole.</item>
/// <item><see cref="E"/> — a spine with three or more arms, a comb.</item>
/// <item><see cref="P"/> — a ring whose bottom bar runs longer than the loop, so the loop slides along it.</item>
/// <item><see cref="G"/> — a ring with an L docked on its edge: one hole plus an open bay a docking front line
/// seals into a taller one.</item>
/// <item><see cref="DoubleHole"/> — a ring with a U docked on its edge, its bay the second hole, kept apart by a
/// solid wall.</item>
/// <item><see cref="TwoU"/> — two loops on one baseline, their holes kept apart by an open channel.</item>
/// </list>
/// </summary>
public enum CompoundForm { I, L, Z, Scythe, Clamp, U, H, Donut, E, P, G, DoubleHole, TwoU }

/// <summary>The forms by their arms, and each form's word on the wire.</summary>
public static class CompoundForms
{
    /// <summary>The form of a spine carrying <paramref name="arms"/> perpendicular arms, at least one: one is an L,
    /// two a U, three or more an E.</summary>
    public static CompoundForm SpineWith(int arms) => arms switch
    {
        < 1 => throw new ArgumentOutOfRangeException(nameof(arms), arms, "a spine form carries at least one arm."),
        1 => CompoundForm.L,
        2 => CompoundForm.U,
        _ => CompoundForm.E,
    };

    /// <summary>How many arms a spine form carries — 1 for an L, 2 for a U, 3 for an E — or 0 for a form that is
    /// not a spine with arms.</summary>
    public static int Arms(this CompoundForm form) => form switch
    {
        CompoundForm.L => 1,
        CompoundForm.U => 2,
        CompoundForm.E => 3,
        _ => 0,
    };

    /// <summary>The form's lowercase word — what the browse filters, the catalog and the stored board rows
    /// carry: <c>i</c>, <c>l</c>, <c>donut</c>, <c>double-hole</c>, <c>two-u</c>.</summary>
    public static string Word(this CompoundForm form) => form switch
    {
        CompoundForm.DoubleHole => "double-hole",
        CompoundForm.TwoU => "two-u",
        _ => form.ToString().ToLowerInvariant(),
    };
}
