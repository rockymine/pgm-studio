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

    /// <summary>Buildable space (docs/contracts/new-map-authoring.md §5): the build height cap and the
    /// areas/bridges where building is allowed. The generator unions them and wires the void boundary.</summary>
    public BuildIntent? Build { get; init; }

    /// <summary>The objective wools. One per defending team on a symmetric map; each is captured by the
    /// other N−1 teams (one monument each). Null/empty leaves objectives untouched.</summary>
    public List<WoolIntent>? Wools { get; init; }

    /// <summary>Map identity: name + authors/contributors. Version (1.0.0), proto (1.5.0), gamemode (ctw)
    /// and the objective text are auto-derived by the generator, not authored.</summary>
    public MetaIntent? Meta { get; init; }
}

/// <summary>Authored map identity. Authors/contributors are Minecraft <b>usernames</b>; the endpoint
/// resolves each to a uuid via <c>MojangClient</c> before saving (the contribution attribute is unused).</summary>
public sealed class MetaIntent
{
    public string Name { get; init; } = "";
    public List<string> Authors { get; init; } = new();
    public List<string> Contributors { get; init; } = new();
}

/// <summary>Where players may build. <see cref="Areas"/> are the buildable rectangles (main footprints
/// and any gap-crossing bridges alike); they're unioned into one build region and the void boundary
/// (no bridging over the void outside that union) is wired automatically.</summary>
public sealed class BuildIntent
{
    /// <summary>Y cap above which no block placement is allowed (null = no ceiling).</summary>
    public int? MaxHeight { get; init; }
    public List<Rect> Areas { get; init; } = new();
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

/// <summary>One objective wool: defended by <see cref="Owner"/> in its <see cref="Room"/>, dispensed at
/// <see cref="Spawn"/> (a point — the wool's <c>location</c> is the int-floored version), and captured by
/// the teams in <see cref="Monuments"/> (one each, the non-owners).</summary>
public sealed class WoolIntent
{
    public string Owner { get; init; } = "";
    /// <summary>Dye colour (slug, e.g. <c>light_blue</c>). Empty → defaults to the owner team's colour.</summary>
    public string Color { get; init; } = "";
    public Rect Room { get; init; }
    public Pt Spawn { get; init; }
    public List<MonumentIntent> Monuments { get; init; } = new();
}

/// <summary>A capture point: the team that captures this wool, and where they place it.</summary>
public sealed class MonumentIntent
{
    public string Team { get; init; } = "";
    public Pt Location { get; init; }
}

/// <summary>A world point (spawn location).</summary>
public readonly record struct Pt(double X, double Y, double Z);

/// <summary>A footprint rectangle in world XZ.</summary>
public readonly record struct Rect(double MinX, double MinZ, double MaxX, double MaxZ);
