using PgmStudio.Domain;
namespace PgmStudio.Pgm.Plan;

/// <summary>A goal and the ground it covers, in the units the placement rule compares — the footprint the
/// stamper will build, never the marker it is anchored on. <see cref="On"/> names what it stands on when the
/// caller knows (a plan piece), and is absent when the goal is read back off a built world.</summary>
public readonly record struct PlacedGoal(string Kind, string Id, BlockRect Footprint, string? On = null);

/// <summary>Ground a goal may not reach into: a stamped room's own frame, and the name of whatever carries
/// it — a plan piece at compile, a team or a wool colour at export.</summary>
public readonly record struct GoalKeepOut(string Kind, string Name, BlockRect Frame);

/// <summary>
/// OB17 — the three places a destroyable or a core may not stand. Unlike a wool they have no room, no
/// per-team monument and nothing binding them to a piece, so what bounds them is only where the map's own
/// rules would leave them unbreakable: over the void, inside a spawn, or inside a wool room.
///
/// <para>The rule is stated once and asked twice, because the two gates know different things. At
/// <b>compile</b> the land is the plan's pieces and the rooms are the frames the compiler will stamp, which
/// catches the fault before a build is spent. At <b>export</b> the land is the ground the rasterizer actually
/// produced, which is the only place a subtract, a relief or a sketch edited after its compile can be seen —
/// a plan that passed can still export a goal over a hole somebody carved afterwards. Neither gate is
/// redundant and neither may be the only one.</para>
///
/// <para>Every decision reads the <b>footprint</b>. A marker legally inside its piece can still put the
/// structure over the void, and a square grown from the marker is not the structure's ground: a cube-4 leans
/// one block further along +X/+Z than it does −X/−Z.</para>
/// </summary>
public static class ObjectivePlacement
{
    /// <summary>The rule these refusals answer, carried on every finding so a caller can act on the id rather
    /// than parse the sentence.</summary>
    public const string Rule = ObjectiveRules.Placement;

    /// <summary>Every refusal the placement of <paramref name="goals"/> earns. Empty is the map that ships.
    /// <paramref name="isLand"/> answers whether one block column is ground — pieces at compile, rasterized
    /// columns at export — and <paramref name="keepOuts"/> is the stamped rooms, as frames rather than as the
    /// pieces holding them, since a spawn piece is often far larger than its room and refusing a goal at its
    /// far corner would be a refusal with no cause.</summary>
    public static Findings Check(
        IEnumerable<PlacedGoal> goals, Func<int, int, bool> isLand, IReadOnlyCollection<GoalKeepOut> keepOuts)
    {
        var findings = new List<Finding>();

        foreach (var goal in goals)
        {
            var where = goal.On is { } on ? $" on '{on}'" : "";
            var size = $"{goal.Footprint.Width}×{goal.Footprint.Depth}";

            if (!Grounded(goal.Footprint, isLand))
                findings.Add(Refuse(
                    $"{goal.Kind} '{goal.Id}'{where} is {size} and overhangs the void — the build slice denies "
                    + "breaking blocks out there, so the goal could never be completed",
                    goal.Id, goal.On));

            // One room is enough to refuse; naming every frame a large goal happens to touch would repeat the
            // same fault with different scenery.
            foreach (var room in keepOuts)
                if (Overlaps(goal.Footprint, room.Frame))
                {
                    findings.Add(Refuse(
                        $"{goal.Kind} '{goal.Id}'{where} is {size} and reaches into the {room.Kind} on "
                        + $"'{room.Name}' — {(room.Kind == "spawn"
                            ? "spawn protection denies breaking blocks to every team, so the goal could never be broken"
                            : "the room's own rules would cover the goal")}",
                        goal.Id, goal.On, room.Name));
                    break;
                }
        }

        return findings;
    }

    /// <summary>Whether every block of a footprint stands on ground. Tested per block rather than by rect
    /// subtraction because a footprint may be carried by several abutting pieces at once, which a
    /// single-rect test would read as a hole.</summary>
    private static bool Grounded(BlockRect footprint, Func<int, int, bool> isLand)
    {
        for (var x = footprint.MinX; x < footprint.MaxX; x++)
        for (var z = footprint.MinZ; z < footprint.MaxZ; z++)
            if (!isLand(x, z))
                return false;
        return true;
    }

    private static bool Overlaps(BlockRect a, BlockRect b) =>
        Math.Min(a.MaxX, b.MaxX) > Math.Max(a.MinX, b.MinX)
        && Math.Min(a.MaxZ, b.MaxZ) > Math.Max(a.MinZ, b.MinZ);

    /// <summary>The finding names the marker's own id ahead of the ground it stands on: a refusal that named
    /// only the piece is ambiguous the moment two goals share one, and the id is what makes the answer
    /// actionable to a caller that must then move one specific marker.</summary>
    private static Finding Refuse(string message, params string?[] subjects) =>
        new(Rule, message, Subjects: [.. subjects.OfType<string>()]);
}
