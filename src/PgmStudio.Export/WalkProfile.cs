using System.Text;
using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Minecraft.Anvil;

namespace PgmStudio.Export;

/// <summary>
/// A walked route's own profile, read off nothing but the route itself and the provenance the world was
/// built with — the half `WalkRender` draws as a picture, answered instead as facts a caller subtracts: the
/// steps that are not a plain walk, and what stands beside the route.
/// </summary>
public static class WalkProfile
{
    /// <summary>One step of a route that is not a plain walk — a scramble, a barrier or a drop: the cell it
    /// lands on, the signed rise from the place before it, and the word <see cref="Walk.StepWord"/> gives
    /// that rise.</summary>
    public readonly record struct Event(int X, int Z, int Rise, string Word);

    /// <summary>The steps of a route that are not a plain walk, and their totals: how many climbed, how
    /// many fell, and the largest in either direction — zero where the route never left a walk.</summary>
    public sealed record Profile(IReadOnlyList<Event> Events, int Rises, int Falls, int WorstStep);

    /// <summary>The profile of <paramref name="path"/>: its events and their totals.</summary>
    public static Profile Of(WalkPath path)
    {
        var events = Events(path);
        return new Profile(events, events.Count(step => step.Rise > 0), events.Count(step => step.Rise < 0),
            events.Count == 0 ? 0 : events.Max(step => Math.Abs(step.Rise)));
    }

    /// <summary>Every step of <paramref name="path"/> that is not a plain walk, in route order.</summary>
    public static IReadOnlyList<Event> Events(WalkPath path)
    {
        var events = new List<Event>();
        for (var i = 1; i < path.Places.Count; i++)
        {
            var rise = path.Places[i].Y - path.Places[i - 1].Y;
            var word = Walk.StepWord(rise);
            if (word != "walk") events.Add(new Event(path.Places[i].X, path.Places[i].Z, rise, word));
        }
        return events;
    }

    /// <summary>One thing the provenance record names within a stated distance of a route: the claim, the
    /// first cell it was met at, and that cell's distance to the nearest cell the route passes
    /// through.</summary>
    public readonly record struct Neighbour(StampId Owner, int X, int Z, int Distance);

    /// <summary>What a player actually meets standing beside a route — everything but the ambient cover
    /// (<c>flora</c>) and the paint (<c>stroke</c>).</summary>
    private static readonly HashSet<string> StandingKinds =
        ["tree", "boulder", "house", "water", "spawn", "destroyable", "core", "wool", "ironcube"];

    private static readonly (int Dx, int Dz)[] EightNeighbours =
        [(1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1)];

    /// <summary>Every distinct thing the provenance record names within <paramref name="radius"/> cells
    /// (Chebyshev) of any cell <paramref name="path"/> passes through — the nearest such cell for each one,
    /// and its distance. Empty at a radius of zero or less.</summary>
    public static IReadOnlyList<Neighbour> Beside(WalkPath path, WorldProvenance provenance, int radius)
    {
        if (radius <= 0) return [];

        // A multi-source flood over the eight-neighbourhood, one ring at a time: a cell reached for the
        // first time at ring d is exactly its Chebyshev distance to the nearest route cell.
        var distance = new Dictionary<(int X, int Z), int>();
        var frontier = new Queue<(int X, int Z)>();
        foreach (var cell in path.Cells)
            if (distance.TryAdd(cell, 0)) frontier.Enqueue(cell);
        while (frontier.Count > 0)
        {
            var cell = frontier.Dequeue();
            var here = distance[cell];
            if (here >= radius) continue;
            foreach (var (dx, dz) in EightNeighbours)
            {
                var next = (cell.X + dx, cell.Z + dz);
                if (distance.ContainsKey(next)) continue;
                distance[next] = here + 1;
                frontier.Enqueue(next);
            }
        }

        var found = new Dictionary<StampId, Neighbour>();
        foreach (var (cell, near) in distance.OrderBy(entry => entry.Value)
                                              .ThenBy(entry => entry.Key.X).ThenBy(entry => entry.Key.Z))
        {
            if (provenance.OwnerAt(cell.X, cell.Z) is not { } owner || !StandingKinds.Contains(owner.Kind)) continue;
            if (found.ContainsKey(owner)) continue;
            found[owner] = new Neighbour(owner, cell.X, cell.Z, near);
        }
        return [.. found.Values.OrderBy(n => n.Distance).ThenBy(n => n.Owner.Kind).ThenBy(n => n.Owner.Unit)];
    }

    /// <summary>The route as characters: its own numbers, a station at every place it stood with the word and
    /// the signed step where it left a walk, the totals, and what stands beside it.</summary>
    public static string Render(WalkPlace from, WalkPlace to, string aim, WalkPath path, Profile profile,
        IReadOnlyList<Neighbour> beside)
    {
        var text = new StringBuilder();
        text.Append($"ROUTE ({from.X}, {from.Z}) -> ({to.X}, {to.Z}) aim {aim}: {path.Cost.Distance} blocks, "
            + $"{path.Cost.Blocks} placed, {path.Cost.Drops} drop(s), worst drop {path.Cost.WorstDrop}\n");

        text.Append("  place            y   step\n");
        for (var i = 0; i < path.Places.Count; i++)
        {
            var place = path.Places[i];
            text.Append("  ").Append($"({place.X}, {place.Z})".PadRight(16)).Append(place.Y.ToString().PadLeft(4));
            if (i > 0)
            {
                var rise = place.Y - path.Places[i - 1].Y;
                var word = Walk.StepWord(rise);
                if (word != "walk") text.Append("   ").Append(word).Append(' ').Append(Signed(rise));
            }
            text.Append('\n');
        }

        text.Append($"rises {profile.Rises}, falls {profile.Falls}, worst step {profile.WorstStep}: ");
        text.Append(profile.Events.Count == 0
            ? "walked end to end"
            : string.Join("; ", profile.Events.Select(step => $"{step.Word} {Signed(step.Rise)} at ({step.X}, {step.Z})")));
        text.Append('\n');

        if (beside.Count > 0)
            text.Append("beside: ").Append(string.Join(", ",
                beside.Select(near => $"{Named(near.Owner)} at ({near.X}, {near.Z}) d{near.Distance}")))
                .Append('\n');

        return text.ToString();
    }

    /// <summary>The unreached answer, for a route the ground does not join.</summary>
    public static string RenderUnreachable(WalkPlace from, WalkPlace to, string aim) =>
        $"ROUTE ({from.X}, {from.Z}) -> ({to.X}, {to.Z}) aim {aim}: unreachable\n";

    private static string Signed(int value) => value >= 0 ? $"+{value}" : value.ToString();

    /// <summary>A claim as a reader names it — <c>kind unit</c>, with <c>#image</c> where it is an orbit image
    /// rather than the authored unit itself.</summary>
    private static string Named(StampId owner) =>
        owner.Image == 0 ? $"{owner.Kind} {owner.Unit}" : $"{owner.Kind} {owner.Unit}#{owner.Image}";
}
