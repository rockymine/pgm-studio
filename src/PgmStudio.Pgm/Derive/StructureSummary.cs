using PgmStudio.Geom;
using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Derive;

/// <summary>
/// The finished structural read of a composed unit — the third mirror surfaced as a browse/duel fact: the
/// sorted wool <b>approach families</b>, the hub <b>compound form</b>, and the frontline <b>compound form</b> (or
/// none). Derived from the generator's own labeled artifacts (each piece's <see cref="UnitPiece.Box"/> +
/// <see cref="UnitPiece.Part"/>), never from a finished map's welded terrain, so it reads uniformly off a
/// bare unit and needs no labels stored back. It is the canonical filter fact for the browse sieve and
/// the bucket key for verdicts / duels: the same read the user sees on the card.
/// <para>Reuses the two validated classifier entry points: <c>ShapeClassifier.Classify</c> (approach
/// family, per wool box) and <see cref="ShapeClassifier.ClassifyCompound"/> (compound form, per hub/frontline box).
/// The wool-family scan is the pattern promoted from the box gallery tool.</para>
/// </summary>
public sealed record StructureSummary(
    IReadOnlyList<CompoundForm> Wools,   // sorted (deterministic canonical order)
    CompoundForm? Hub,                  // the hub compound form (null only if a unit somehow has no hub box)
    CompoundForm? Frontline)            // the frontline compound form, or null = none (a real, sampled outcome)
{
    public static StructureSummary Derive(TeamUnit unit) => new(
        WoolFamilies(unit).OrderBy(f => f).ToList(),
        BoxForm(unit, BoxKind.Hub),
        BoxForm(unit, BoxKind.Frontline));

    /// <summary>Classify each wool box to its emitted approach family (box scope — its own pieces + room).
    /// Public because the box gallery tool shares this exact read.</summary>
    public static List<CompoundForm> WoolFamilies(TeamUnit unit)
    {
        var fams = new List<CompoundForm>();
        foreach (var boxId in WoolBoxIds(unit))
        {
            var boxPieces = unit.Pieces.Where(p => p.Box?.Id == boxId).ToList();
            var room = boxPieces.FirstOrDefault(p => p.Part == CompoundParts.Room);
            if (room is null) continue;
            var filled = new HashSet<(int, int)>();
            var roomCells = new HashSet<(int, int)>();
            foreach (var p in boxPieces)
                foreach (var c in CellsOf(p.Rect))
                {
                    filled.Add(c);
                    if (p.Id == room.Id) roomCells.Add(c);
                }
            if (ShapeClassifier.Classify(filled, roomCells).Family is { } family) fams.Add(family);
        }
        return fams;
    }

    private static IEnumerable<string> WoolBoxIds(TeamUnit unit) =>
        unit.Pieces.Where(p => p.Box?.Kind == BoxKind.Wool).Select(p => p.Box!.Id).Distinct();

    /// <summary>The compound form of the single box of <paramref name="kind"/> (hub or frontline), or null when a
    /// unit has none (a no-frontline unit).</summary>
    private static CompoundForm? BoxForm(TeamUnit unit, BoxKind kind)
    {
        var boxId = unit.Pieces.Where(p => p.Box?.Kind == kind).Select(p => p.Box!.Id).Distinct().FirstOrDefault();
        if (boxId is null) return null;
        var cells = new HashSet<(int, int)>();
        foreach (var p in unit.Pieces.Where(p => p.Box?.Id == boxId))
            foreach (var c in CellsOf(p.Rect))
                cells.Add(c);
        return cells.Count == 0 ? null : ShapeClassifier.ClassifyCompound(cells);
    }

    private static IEnumerable<(int, int)> CellsOf(CellRect rect)
    {
        for (var x = rect.X; x < rect.X + rect.Width; x++)
            for (var z = rect.Z; z < rect.Z + rect.Height; z++)
                yield return (x, z);
    }

    /// <summary>A stable, lowercase, order-independent key: <c>wools:donut,l|hub:donut|front:none</c>. Persisted
    /// on a pinned plan and used as the verdict/duel bucket key.</summary>
    public string Canonical() =>
        CanonicalOf(Wools.Select(StructureNames.Family), StructureNames.Form(Hub), StructureNames.Form(Frontline));

    /// <summary>The canonical key of a structure already read into its tokens.</summary>
    public static string CanonicalOf(IEnumerable<string> wools, string hub, string frontline) =>
        $"wools:{string.Join(",", wools)}|hub:{hub}|front:{frontline}";
}

/// <summary>The display/filter tokens for the structural vocabulary — one mapping shared by the card badges,
/// the filter chips, the canonical bucket key, and the sieve query. Lowercase and stable.</summary>
public static class StructureNames
{
    /// <summary>A wool shape token: <c>donut</c>, <c>l</c>, <c>i</c>, <c>u</c>, <c>h</c>, <c>clamp</c>…</summary>
    public static string Family(CompoundForm f) => f.Word();

    /// <summary>A hub or front-line form token: <c>i</c>, <c>l</c>, <c>u</c>, <c>e</c>, <c>donut</c>, <c>g</c>,
    /// <c>p</c>, <c>double-hole</c>, <c>two-u</c>, or <c>none</c> where the unit has no such box.</summary>
    public static string Form(CompoundForm? f) => f?.Word() ?? "none";
}
