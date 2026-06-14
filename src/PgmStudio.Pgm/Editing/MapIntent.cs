namespace PgmStudio.Pgm.Editing;

/// <summary>
/// Declarative authoring intent — the source of truth for a <b>new</b> map
/// (docs/contracts/new-map-authoring.md). The author states what they want; a generator projects it
/// into the PGM document (teams/kits/regions/filters/apply-rules/spawns). Persisted as a
/// <c>map_intent_json</c> artifact, outside the entity-replace codec (like the draft sidecar).
/// <para>Teams slice: teams, per-team spawns + protection, and the observer (<c>&lt;default&gt;</c>)
/// spawn. Build/wools slices extend this record later.</para>
/// </summary>
public sealed class MapIntent
{
    /// <summary>Teams to generate. Null leaves the doc's existing teams untouched (e.g. a map that was
    /// pre-seeded with teams); a non-empty list replaces them.</summary>
    public List<TeamDef>? Teams { get; init; }

    /// <summary>Shared player cap for every generated team (symmetric map → one number).</summary>
    public int MaxPlayers { get; init; } = 12;

    public List<SpawnIntent> Spawns { get; init; } = new();

    /// <summary>The observer / <c>&lt;default&gt;</c> spawn (pre-game + spectators). Every PGM map needs one.</summary>
    public ObserverIntent? Observer { get; init; }
}

/// <summary>A team to generate. <see cref="Id"/> is the stable identifier rules/spawns reference and the
/// source of the naming slug; <see cref="Color"/> is the display colour (may be multi-word, e.g. "dark red").</summary>
public sealed class TeamDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Color { get; init; } = "";
}

/// <summary>One team's spawn: where players materialise (<see cref="Point"/>) and, optionally, the
/// anti-grief zone around it (<see cref="Protection"/>). The kit is the fixed Standard preset
/// (not author-selectable yet — see <c>TeamsGenerator</c>), so it isn't part of the intent.</summary>
public sealed class SpawnIntent
{
    public string Team { get; init; } = "";
    public Pt Point { get; init; }
    public Rect? Protection { get; init; }
    public double Yaw { get; init; }
}

/// <summary>The observer/default spawn point.</summary>
public sealed class ObserverIntent
{
    public Pt Point { get; init; }
    public double Yaw { get; init; }
}

/// <summary>A world point (spawn location).</summary>
public readonly record struct Pt(double X, double Y, double Z);

/// <summary>A footprint rectangle in world XZ.</summary>
public readonly record struct Rect(double MinX, double MinZ, double MaxX, double MaxZ);
