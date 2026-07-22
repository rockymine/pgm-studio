using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Derive;

/// <summary>
/// The finished structural read of a composed unit — the third mirror surfaced as a browse/duel fact: the
/// sorted wool <b>approach families</b>, the hub <b>body form</b>, and the frontline <b>body form</b> (or
/// none). Derived from the generator's own labeled artifacts (each piece's <see cref="GrownPiece.Box"/> +
/// <see cref="GrownPiece.Slot"/>), never from a finished map's welded terrain, so it reads uniformly off a
/// bare unit and needs no labels stored back. It is the canonical filter fact for the browse sieve (G117) and
/// the bucket key for verdicts (G118) / duels (G120): the same read the user sees on the card.
/// <para>Reuses the two validated classifier entry points: <see cref="ShapeClassifier.Classify"/> (approach
/// family, per wool box) and <see cref="ShapeClassifier.ClassifyBody"/> (body form, per hub/frontline box).
/// The wool-family scan is the pattern promoted from the box gallery tool.</para>
/// </summary>
public sealed record StructureSummary(
    IReadOnlyList<ShapeFamily> Wools,   // sorted (deterministic canonical order)
    CompoundRead? Hub,                  // the hub body form (null only if a unit somehow has no hub box)
    CompoundRead? Frontline)            // the frontline body form, or null = none (a real, sampled outcome)
{
    public static StructureSummary Derive(GrownUnit unit) => new(
        WoolFamilies(unit).OrderBy(f => f).ToList(),
        BoxForm(unit, BoxKind.Hub),
        BoxForm(unit, BoxKind.Frontline));

    /// <summary>Classify each wool box to its emitted approach family (box scope — its own pieces + room).
    /// Public because the box gallery tool shares this exact read.</summary>
    public static List<ShapeFamily> WoolFamilies(GrownUnit unit)
    {
        var fams = new List<ShapeFamily>();
        foreach (var boxId in WoolBoxIds(unit))
        {
            var boxPieces = unit.Pieces.Where(p => p.Box?.Id == boxId).ToList();
            var room = boxPieces.FirstOrDefault(p => p.Slot == ApproachSlots.Room);
            if (room is null) continue;
            var filled = new HashSet<(int, int)>();
            var roomCells = new HashSet<(int, int)>();
            foreach (var p in boxPieces)
                foreach (var c in CellsOf(p.Rect))
                {
                    filled.Add(c);
                    if (p.Id == room.Id) roomCells.Add(c);
                }
            fams.Add(ShapeClassifier.Classify(filled, roomCells).Family);
        }
        return fams;
    }

    private static IEnumerable<string> WoolBoxIds(GrownUnit unit) =>
        unit.Pieces.Where(p => p.Box?.Kind == BoxKind.Wool).Select(p => p.Box!.Id).Distinct();

    /// <summary>The body form of the single box of <paramref name="kind"/> (hub or frontline), or null when a
    /// unit has none (a no-frontline unit).</summary>
    private static CompoundRead? BoxForm(GrownUnit unit, BoxKind kind)
    {
        var boxId = unit.Pieces.Where(p => p.Box?.Kind == kind).Select(p => p.Box!.Id).Distinct().FirstOrDefault();
        if (boxId is null) return null;
        var cells = new HashSet<(int, int)>();
        foreach (var p in unit.Pieces.Where(p => p.Box?.Id == boxId))
            foreach (var c in CellsOf(p.Rect))
                cells.Add(c);
        return cells.Count == 0 ? null : ShapeClassifier.ClassifyBody(cells);
    }

    private static IEnumerable<(int, int)> CellsOf(int[] rect)
    {
        for (var x = rect[0]; x < rect[0] + rect[2]; x++)
            for (var z = rect[1]; z < rect[1] + rect[3]; z++)
                yield return (x, z);
    }

    /// <summary>A stable, lowercase, order-independent key: <c>wools:donut,l|hub:ring|front:none</c>. Persisted
    /// on a pinned plan and used as the verdict/duel bucket key.</summary>
    public string Canonical() =>
        $"wools:{string.Join(",", Wools.Select(StructureNames.Family))}" +
        $"|hub:{StructureNames.Form(Hub)}" +
        $"|front:{StructureNames.Form(Frontline)}";
}

/// <summary>The display/filter tokens for the structural vocabulary — one mapping shared by the card badges,
/// the filter chips, the canonical bucket key, and the sieve query. Lowercase and stable.</summary>
public static class StructureNames
{
    /// <summary>A wool approach family token: <c>donut</c>, <c>l</c>, <c>i</c>, <c>u</c>, <c>h</c>, <c>clamp</c>…</summary>
    public static string Family(ShapeFamily f) => f.ToString().ToLowerInvariant();

    /// <summary>A body form token: <c>ring</c>, <c>g</c>, <c>p</c>, <c>double-hole</c>, <c>two-u</c>, <c>bar</c>
    /// (solid), <c>single</c> / <c>twin</c> (one/two spine arms), <c>arms-N</c> otherwise, or <c>none</c>.</summary>
    public static string Form(CompoundRead? r) => r is null ? "none" : r.Form switch
    {
        Compound.Rectangle => "bar",
        Compound.SpineArms => r.Arms == 1 ? "single" : r.Arms == 2 ? "twin" : $"arms-{r.Arms}",
        Compound.Ring => "ring",
        Compound.DoubleHole => "double-hole",
        Compound.P => "p",
        Compound.G => "g",
        Compound.TwoUOnI => "two-u",
        _ => r.Form.ToString().ToLowerInvariant(),
    };
}
