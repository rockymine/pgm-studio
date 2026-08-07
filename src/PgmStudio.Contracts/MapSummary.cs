namespace PgmStudio.Contracts;

/// <summary>One entry in the map list (GET /api/maps).
/// <para><paramref name="Gamemodes"/> is derived from the map's objective modules, not read off its
/// <c>&lt;gamemode&gt;</c> label. It is a set because CTW/DTM/DTC coexist, and it is empty for a map
/// carrying no objective we read — which is a fact about the map, not missing data.</para>
/// <para>The three <c>Has…</c> flags are the map's <b>authoring layers</b>, and a map can hold all of them
/// at once: <paramref name="HasPlan"/> a plan document, <paramref name="HasSketch"/> a drawn sketch layout,
/// <paramref name="HasSurface"/> the rasterized world geometry the Configure wizard works on (which also
/// makes a top-down block render available, <c>GET /api/map/{slug}/layers/top-surface</c>). They are what the
/// list draws a row's layer links from, so every tool a map has been through is one click away wherever the
/// map is listed. <paramref name="Stage"/> is separate: how far the map has got, not which layers it
/// holds.</para></summary>
public sealed record MapSummary(
    string Slug,
    string Name,
    IReadOnlyList<string> Gamemodes,
    string? Version,
    string? Objective,
    string Stage,
    bool HasSurface = false,
    bool HasPlan = false,
    bool HasSketch = false);

/// <summary>Map counts for the dashboard landing cards (GET /api/maps/stage-counts). <see cref="Sketch"/>
/// counts maps that <em>hold a sketch layer</em>, matching what the Sketches list shows; the other two count
/// maps sitting at that stage, which is what those lists show.</summary>
public sealed record MapStageCounts(int Sketch, int Configure, int Edit);

/// <summary>Which authoring layers one map holds (GET /api/map/{slug}/layers) — the same four facts
/// <see cref="MapSummary"/> carries per row, asked about a single map. A tool uses it to know whether an
/// action of its own would land on work that already exists: the plan editor's build is an origination on a
/// map with no <see cref="Sketch"/> or <see cref="World"/>, and a rebuild on one that has them.</summary>
public sealed record MapLayers(bool Plan, bool Sketch, bool World, bool Intent);
