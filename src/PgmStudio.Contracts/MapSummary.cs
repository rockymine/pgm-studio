using PgmStudio.Vocabulary;

namespace PgmStudio.Contracts;

/// <summary>One entry in the map list (GET /api/maps).
/// <para><paramref name="Gamemodes"/> is derived from the map's objective modules, not read off its
/// <c>&lt;gamemode&gt;</c> label. It is a set because CTW/DTM/DTC coexist, and it is empty for a map
/// carrying no objective we read — which is a fact about the map, not missing data.</para>
/// <para>The three <c>Has…</c> flags are the map's <b>authoring layers</b>, and a map can hold all of them
/// at once: <paramref name="HasPlan"/> a plan document, <paramref name="HasSketch"/> a drawn sketch layout,
/// <paramref name="HasSurface"/> the rasterized world geometry the Configure wizard works on (which also
/// makes a top-down block render available, <c>GET /api/map/{slug}/top-surface</c>). They are what the
/// list draws a row's layer links from, so every tool a map has been through is one click away wherever the
/// map is listed. <paramref name="Stage"/> is separate: how far the map has got, not which layers it
/// holds.</para></summary>
/// <param name="Slug">What every route names the map by.</param>
/// <param name="Name">The map's own title, as its <c>map.xml</c> states it.</param>
/// <param name="Gamemodes">Which objectives it is played for — <c>ctw</c>, <c>dtm</c>, <c>dtc</c> — derived
/// from what it carries rather than read off a label.</param>
/// <param name="Version">The version its <c>map.xml</c> states, or absent where it has none.</param>
/// <param name="Objective">The one-line objective sentence a player is shown, or absent.</param>
/// <param name="Stage">How far the map has got, which is separate from which layers it holds.</param>
/// <param name="HasSurface">Whether the rasterized world geometry exists — the layer the Configure wizard
/// works on, and what makes a top-down block render available.</param>
/// <param name="HasPlan">Whether a plan document is stored.</param>
/// <param name="HasSketch">Whether a drawn sketch layout is stored.</param>
public sealed record MapSummary(
    string Slug,
    string Name,
    IReadOnlyList<string> Gamemodes,
    string? Version,
    string? Objective,
    [property: WordSet(typeof(MapStage))] string Stage,
    bool HasSurface = false,
    bool HasPlan = false,
    bool HasSketch = false);

/// <summary>Map counts for the dashboard landing cards (GET /api/maps/stage-counts). <see cref="Sketch"/>
/// counts maps that <em>hold a sketch layer</em>, matching what the Sketches list shows; the other two count
/// maps sitting at that stage, which is what those lists show.</summary>
/// <param name="Sketch">Maps holding a sketch layer, whatever stage they sit at.</param>
/// <param name="Configure">Maps sitting at the Configure stage.</param>
/// <param name="Edit">Maps sitting at the Edit stage.</param>
public sealed record MapStageCounts(int Sketch, int Configure, int Edit);

/// <summary>Which authoring artifacts one map holds (GET /api/map/{slug}/state) — the same four facts
/// <see cref="MapSummary"/> carries per row, asked about a single map. A tool uses it to know whether an
/// action of its own would land on work that already exists: the plan editor's build is an origination on a
/// map with no <see cref="Sketch"/> or <see cref="World"/>, and a rebuild on one that has them.</summary>
/// <param name="Plan">Whether a plan document is stored.</param>
/// <param name="Sketch">Whether a drawn sketch layout is stored.</param>
/// <param name="World">Whether the rasterized world geometry exists.</param>
/// <param name="Intent">Whether the map states what it is played for.</param>
public sealed record MapArtifacts(bool Plan, bool Sketch, bool World, bool Intent);

/// <summary>One thing a caller may do to a map from where it is.</summary>
/// <param name="Does">What the move accomplishes, in the words the tool documents use.</param>
/// <param name="Route">The route that does it, with <c>{slug}</c> left for the caller to fill.</param>
/// <param name="Next">Whether this is a move the map's own stage is waiting on. Several moves are open at
/// once and only some are the one the author was about to make.</param>
public sealed record MapMove(string Does, string Route, bool Next);

/// <summary>Where a map has got to, what it holds, and what may be done to it from here.
///
/// <para>The artifacts were always an affordance answer — a tool reads them to tell an origination from a
/// rebuild <em>before</em> offering the action — and they were half of one: which documents exist says what
/// a move can read, and the stage says which move is the one being waited on. Both, and the moves they
/// imply, are answered together so a driver reads its options instead of learning them.</para></summary>
/// <param name="Stage">How far the map has got. A progress marker rather than a lock: nothing refuses on
/// it, so a plan-stage map is still offered the rebuild that reads its plan.</param>
/// <param name="Artifacts">Which documents the map holds, which is what says what a move can read.</param>
/// <param name="Moves">What may be done from here, each with the route that does it.</param>
public sealed record MapState(
    [property: WordSet(typeof(MapStage))] string Stage, MapArtifacts Artifacts, IReadOnlyList<MapMove> Moves);

/// <summary>Whether the map came from a sketch — which is what drops the Monuments step.</summary>
/// <param name="Sketch">Whether the map was originated from a drawing rather than imported or planned.</param>
public sealed record MapOriginDto(bool Sketch);
