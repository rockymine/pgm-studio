using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Compose;

/// <summary>Published negative space of a fill, in plan cell coordinates: a <see cref="ShapeVacancy"/>
/// placed into the board frame, carrying its mouth as a box-edge interval so a later box can claim it
/// (docs/contracts/map-generation.md §4.4). Unclaimed vacancies are just void (or a buffer when the void is
/// the point).</summary>
public sealed record Vacancy(string Kind, int[] Rect, BoxInterface? Mouth, IReadOnlyList<string> Walls);

/// <summary>
/// The outcome of filling a box — a data channel, not exception control flow: "no shape fits" is a signal
/// the partition answers by changing the box (resize, relax an interface, split), never a crash.
/// <see cref="Ok"/> carries the fill's pieces and its published vacancies; <see cref="TooSmall"/> carries
/// the family's minimum box so the caller can resize or downgrade directedly; <see cref="NoFamilyFits"/>
/// carries the menu that came up empty; <see cref="IllegalDock"/> names a fill the docking gate refused
/// (the mouth seals a wool, is not an entry, is the wrong span, or the family needs more hosts than a single
/// mouth offers) — a directed rejection, not a placement.
/// </summary>
public abstract record FillResult
{
    public sealed record Ok(EmittedApproach Approach, IReadOnlyList<Vacancy> Vacancies) : FillResult;

    public sealed record TooSmall(ShapeFamily Family, int MinW, int MinH) : FillResult;

    public sealed record NoFamilyFits(IReadOnlyList<ShapeFamily> Menu) : FillResult;

    public sealed record IllegalDock(DockRejection Reason, ShapeFamily Family, BoxEdge Mouth) : FillResult;
}
