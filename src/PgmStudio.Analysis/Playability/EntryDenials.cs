namespace PgmStudio.Analysis.Playability;

using PgmStudio.Geom;

using PgmStudio.Analysis.Region;
using Dict = Dictionary<string, object?>;

/// <summary>
/// Which ground a team may not set foot on — the map's <c>enter</c> apply rules read per team, so a spawn's
/// own protection reads as the wall it is.
///
/// <para>One reader, because two would disagree. The traversability verdict subtracts a team's barred places
/// before asking whether it can reach the goals it must contest, and a walk measured for that team has to
/// subtract the same ones or it prices a route through ground the player is thrown out of. What they share is
/// the answer — <see cref="Barred"/> — rather than what either does with it.</para>
///
/// <para>A denial has to be <b>provable</b> to count: a rule with no region, geometry that will not resolve,
/// or a filter this reader cannot follow denies nobody. So an exotic wiring can only ever fail to subtract
/// ground, never invent a barred region that is not there.</para>
/// </summary>
public static class EntryDenials
{
    /// <summary>The teams a map spawns, in declaration order and without the observer.</summary>
    public static List<string> Teams(Dict data)
    {
        var teams = new List<string>();
        foreach (var spawn in MapDoc.AsList(data.GetValueOrDefault("spawns")).OfType<Dict>())
        {
            if (MapDoc.Truthy(spawn.GetValueOrDefault("observer"))) continue;
            if (spawn.GetValueOrDefault("team") is not string team || team.Length == 0) continue;
            if (!teams.Contains(team)) teams.Add(team);
        }
        return teams;
    }

    /// <summary>Where one team may not stand: each barred cell with the heights barred in it. A rule's region
    /// bars its footprint over the heights it covers (<see cref="RegionGeometry2d.Heights"/>), so a wool room
    /// drawn as a cuboid above a spawn bars the room's storey and not the spawn under it; a rectangle bars
    /// every height of its footprint.</summary>
    public sealed class Barred
    {
        private readonly Dictionary<(int X, int Z), List<(double Low, double High)>> _spans = [];

        /// <summary>Every cell barred at any height.</summary>
        public IReadOnlySet<(int X, int Z)> Cells => _cells;
        private readonly HashSet<(int X, int Z)> _cells = [];

        internal void Add((int X, int Z) cell, (double Low, double High) span)
        {
            if (!_spans.TryGetValue(cell, out var spans)) _spans[cell] = spans = [];
            spans.Add(span);
            _cells.Add(cell);
        }

        /// <summary>Whether a player standing at this place is inside the barred region: its feet are.</summary>
        public bool Bars(WalkPlace place)
            => _spans.TryGetValue(place.Cell, out var spans) && spans.Any(span => span.Low <= place.Y && place.Y <= span.High);
    }

    /// <summary>One team's barrier over the grid given, or null where nothing bars it.</summary>
    public static Barred? For(Dict data, string team, CellRect over)
    {
        var regions = MapDoc.AsDict(data.GetValueOrDefault("regions"));
        var filters = MapDoc.AsDict(data.GetValueOrDefault("filters"));
        var bounds = ((double)over.X, (double)over.Z, (double)(over.X + over.Width), (double)(over.Z + over.Height));
        Barred? barred = null;

        foreach (var rule in MapDoc.AsList(data.GetValueOrDefault("apply_rules")).OfType<Dict>())
        {
            if (rule.GetValueOrDefault("enter") is not string enter || enter.Length == 0) continue;
            if (rule.GetValueOrDefault("region") is not { } regionRef || Allows(enter, filters, team)) continue;
            if (Editability.RegionMask(regionRef, regions, bounds, over.X, over.Z, over.Width, over.Height) is not { } mask) continue;

            var heights = RegionGeometry2d.Heights(regionRef, regions);
            barred ??= new Barred();
            for (var i = 0; i < mask.Length; i++)
                if (mask[i]) barred.Add((over.X + i % over.Width, over.Z + i / over.Width), heights);
        }
        return barred is { Cells.Count: > 0 } ? barred : null;
    }

    /// <summary>The cells of the protection a goal stands in, for a team barred from it: the innermost union the
    /// author named, inside a rule's region that bars the team, that still holds the goal. A wool room drawn as
    /// a union of a room and the lane into it is one protection even where a row between them was missed, so
    /// its cells are the room's and the lane's together. Empty where no rule barring the team covers the goal.
    /// </summary>
    public static HashSet<(int X, int Z)> Protection(Dict data, string team, (int X, int Z) goal, CellRect over)
    {
        var regions = MapDoc.AsDict(data.GetValueOrDefault("regions"));
        var filters = MapDoc.AsDict(data.GetValueOrDefault("filters"));
        var bounds = ((double)over.X, (double)over.Z, (double)(over.X + over.Width), (double)(over.Z + over.Height));
        bool[]? Mask(object reference) => Editability.RegionMask(reference, regions, bounds, over.X, over.Z, over.Width, over.Height);
        var at = (goal.Z - over.Z) * over.Width + (goal.X - over.X);
        if (goal.X < over.X || goal.Z < over.Z || goal.X >= over.X + over.Width || goal.Z >= over.Z + over.Height) return [];

        foreach (var rule in MapDoc.AsList(data.GetValueOrDefault("apply_rules")).OfType<Dict>())
        {
            if (rule.GetValueOrDefault("enter") is not string enter || enter.Length == 0 || Allows(enter, filters, team)) continue;
            if (rule.GetValueOrDefault("region") is not { } reference || Mask(reference) is not { } mask || !mask[at]) continue;

            // Descend through named unions while one still holds the goal.
            for (var depth = 0; depth < 16; depth++)
            {
                var region = RegionGeometry2d.Resolve(reference, regions);
                if ((region?.GetValueOrDefault("type") as string) != "union") break;
                var inner = MapDoc.AsList(region.GetValueOrDefault("children")).OfType<string>()
                    .Where(child => !child.Contains("__")
                                    && RegionGeometry2d.Resolve(child, regions)?.GetValueOrDefault("type") as string == "union")
                    .Select(child => (child, mask: Mask(child)))
                    .FirstOrDefault(candidate => candidate.mask is { } m && m[at]);
                if (inner.mask is null) break;
                (reference, mask) = (inner.child, inner.mask);
            }
            var cells = new HashSet<(int X, int Z)>();
            for (var i = 0; i < mask.Length; i++)
                if (mask[i]) cells.Add((over.X + i % over.Width, over.Z + i / over.Width));
            return cells;
        }
        return [];
    }

    /// <summary>Whether an <c>enter</c> filter lets <paramref name="team"/> in. Deliberately permissive: a
    /// team filter answers by its team, the boolean wrappers compose, and anything unresolvable answers yes.
    /// </summary>
    public static bool Allows(string value, Dict filters, string team, HashSet<string>? seen = null)
    {
        seen ??= [];
        if (value.Length == 0 || !seen.Add(value)) return true;
        if (value is "always" or "allow") return true;
        if (value == "never") return false;
        if (filters.GetValueOrDefault(value) is not Dict filter) return true;

        return (filter.GetValueOrDefault("type") as string) switch
        {
            "team" => filter.GetValueOrDefault("team") as string == team,
            "not" => !Allows(filter.GetValueOrDefault("child") as string ?? "", filters, team, seen),
            "allow" => Allows(filter.GetValueOrDefault("child") as string ?? "", filters, team, seen),
            "deny" => !Allows(filter.GetValueOrDefault("child") as string ?? "", filters, team, seen),
            "any" => MapDoc.AsList(filter.GetValueOrDefault("children"))
                .Any(child => Allows(child as string ?? "", filters, team, seen)),
            "all" => MapDoc.AsList(filter.GetValueOrDefault("children"))
                .All(child => Allows(child as string ?? "", filters, team, seen)),
            "always" => true,
            "never" => false,
            _ => true,
        };
    }
}
