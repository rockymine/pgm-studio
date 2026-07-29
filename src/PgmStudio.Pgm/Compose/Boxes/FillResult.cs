using PgmStudio.Geom;
using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Compose;

/// <summary>Published negative space of a fill, in plan cell coordinates: a <see cref="ShapeVacancy"/>
/// placed into the board frame, carrying its mouth as a box-edge interval so a later box can claim it
/// (docs/generator/model.md §4 — what a body leaves empty). Unclaimed vacancies are just void (or a buffer when the void is
/// the point).</summary>
public sealed record Vacancy(string Kind, CellRect Rect, BoxAbutment? Mouth, IReadOnlyList<string> Walls);

/// <summary>
/// The outcome of filling a box — a data channel, not exception control flow: "no shape fits" is a signal
/// the partition answers by changing the box (resize, relax an interface, split), never a crash.
/// <see cref="Ok"/> carries the fill's pieces and its published vacancies; <see cref="TooSmall"/> carries
/// the family's minimum box so the caller can resize or downgrade directedly; <see cref="NoFamilyFits"/>
/// carries the menu that came up empty; <see cref="IllegalDock"/> names a fill the docking gate refused
/// (the mouth seals a wool, is not an entry, is the wrong span, or the family needs more hosts than a single
/// mouth offers); <see cref="UnsupportedKnobs"/> names a knob combination the emitter does not build — a
/// directed rejection, not a placement.
///
/// <para><see cref="Rejection"/> projects every non-<see cref="Ok"/> case onto the shared
/// <see cref="FillRejection"/> vocabulary the body kinds also report through, so a caller reasoning about
/// producibility reads one taxonomy across all four box kinds. The projection is the only mapping between the
/// two — these cases stay the approach channel's own surface.</para>
/// </summary>
public abstract record FillResult
{
    public sealed record Ok(EmittedApproach Approach, IReadOnlyList<Vacancy> Vacancies) : FillResult;

    public sealed record TooSmall(ShapeFamily Family, int MinW, int MinH) : FillResult;

    public sealed record NoFamilyFits(IReadOnlyList<ShapeFamily> Menu) : FillResult;

    public sealed record IllegalDock(DockRejection Reason, ShapeFamily Family, BoxEdge Mouth) : FillResult;

    /// <summary>The knob combination is not one the emitter builds (its own message in
    /// <see cref="Detail"/>) — reported as data rather than thrown, per this type's contract.</summary>
    public sealed record UnsupportedKnobs(ShapeFamily Family, string Detail) : FillResult;

    /// <summary>This result's directed reason in the shared vocabulary, or <c>null</c> when it is
    /// <see cref="Ok"/>.</summary>
    public FillRejection? Rejection => this switch
    {
        Ok => null,
        TooSmall t => new FillRejection.TooSmall(t.MinW, t.MinH),
        NoFamilyFits n => new FillRejection.NotOnMenu(n.Menu.Select(f => f.ToString()).ToList()),
        IllegalDock d => new FillRejection.IllegalDock(d.Reason, d.Mouth),
        UnsupportedKnobs u => new FillRejection.UnsupportedKnobs(u.Detail),
        _ => null,
    };
}
