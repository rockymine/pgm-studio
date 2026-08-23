using System.Text;
using PgmStudio.Analysis.Layer;
using PgmStudio.Analysis.Playability;
using PgmStudio.Domain;
using PgmStudio.Minecraft.Dressing;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Sketch;
using PgmStudio.Vocabulary;

namespace PgmStudio.Export;

using Dict = Dictionary<string, object?>;
using PgmStudio.Geom;

/// <summary>The outcome of composing a map for export: either a refusal — the status it is answered under,
/// the gate's short label and the findings themselves — or the composed <c>map.xml</c>. For a
/// sketch-originated map <see cref="World"/> also carries the synthesised voxel world so the caller can bundle
/// its region files.
/// <para>A refusal is handed over as findings rather than as a rendered body: this composer sits below
/// <c>Api</c>, and the layer that answers HTTP is the one that renders the envelope, so a caller cannot tell
/// how deep the refusal was raised.</para></summary>
/// <param name="Doc">The composed map document, on success — what the XML was written from, and what a
/// post-export read (the coverage measure, a headless driver's own analysis) consumes without re-parsing
/// the XML it just wrote.</param>
public sealed record ExportComposition(
    Refusal? Refusal, string? Xml, BuiltWorld? World, Dict? Doc = null);

/// <summary>
/// The shared pipeline behind <c>GET /map/{slug}/xml</c> and <c>GET /map/{slug}/export</c>: the
/// traversability gate, sketch-intent resolution, and XML composition — so the two routes can't drift, and
/// the reviewed XML is exactly what ships. For a sketch map it builds the world and re-projects the resolved
/// intent (snapped spawns + auto-derived monument locations) so the XML agrees with the world; the build +
/// compose run under one guard, surfacing any failure as a structured error.
///
/// <para>Pure — no DB, no IO. Everything the store would otherwise supply (whether the map is intent-authored,
/// the traversability segments, the stored authoring intent for a sketch map, the cached surface palette and
/// spawn-ore renewables for everything else) is loaded by the caller and handed in, which is what lets a
/// headless driver reach this without reaching a database.</para>
/// </summary>
public static class MapExportComposer
{
    /// <param name="isIntent">Whether the map was authored through the declarative intent model — gates the
    /// traversability check below (corpus maps have none and export unconditionally).</param>
    /// <param name="segments">The map's scanned layer segments, for the traversability check. Only read when
    /// <paramref name="isIntent"/> is true; null when the map was never scanned.</param>
    /// <param name="intent">The stored authoring intent. Required (and always non-null in practice) when
    /// <paramref name="layoutBytes"/> is not null — a sketch-originated map resolves it against the world it
    /// builds; ignored otherwise.</param>
    /// <param name="surfacePalette">The cached scanned surface palette, for a non-sketch intent map's CTW
    /// boilerplate. Ignored for a sketch map, which has no scanned surface.</param>
    /// <param name="resources">The cached resource blocks, for a non-sketch intent map's renewables. Ignored
    /// for a sketch map, which derives its own renewable cubes from the world it just built.</param>
    public static ExportComposition Compose(
        Dict doc, byte[]? layoutBytes, bool isIntent, SegmentIndex? segments, MapIntent? intent,
        IReadOnlySet<int>? surfacePalette, IReadOnlyList<(string Type, int X, int Y, int Z)> resources)
    {
        try
        {
            // Sketch-originated: synthesise the world, re-project the resolved intent so the XML agrees with
            // it, then compose (no scanned surface/resources for a sketch map). Every gate a sketch map is
            // held to is inside that call rather than in front of it, so a driver reaching it directly is
            // judged by the same chain this route is.
            if (layoutBytes is not null)
                return BuildAndCompose(doc, Encoding.UTF8.GetString(layoutBytes), intent!);

            // OB20 — every declared <gamemode> against PGM's own closed enum. Checked first on this leg, and
            // against every map regardless of origin or world state: it needs no ground and no built intent,
            // and an id outside PGM's enum fails the whole map to load (MapInfoImpl.parseGamemodes) however
            // clean everything else is.
            if (RefuseUnknownGamemode(doc) is { } gamemodeRefusal) return gamemodeRefusal;

            // EX1 — an intent-authored map must be traversable before it can export (§9), judged over the
            // world scanned into the store. A map that ships its own world has no authored ground to be held
            // to, so it is exempt.
            if (isIntent
                && RefuseUntraversable(doc, segments?.StandingColumns(), segments?.Y0Columns()) is { } cutOff)
                return cutOff;

            // EX2 — asked of an intent-authored map that is not sketch-originated, where there is a document
            // to read and no resolved intent to compare it against. A corpus map is exempt for the reason the
            // traversability gate exempts it: 281 of the 1,616 maps in the two corpora declare no team at all,
            // and an FFA map with none is not a broken map.
            if (isIntent && Playable(null, doc) is { Refuses: true } unenterable)
                return Refuse("not a playable map", [.. unenterable.Refusals]);

            // Other maps get plain XML (they already ship a world). Intent maps additionally get the cached
            // surface palette + spawn-ore renewables — cache-only, never triggering a world scan on export.
            var xml = MapXmlComposer.Compose(doc, isIntent, surfacePalette, resources);
            return new(null, xml, null);
        }
        catch (DressingParseException ex)
        {
            // A malformed dressing document is an authoring error, not a reason to export the map bare —
            // refused by name rather than silently read as though nothing had been placed.
            return Refuse("dressing document invalid", [ex.Finding], 422);
        }
        // Anything else is not this gate's to name, so it propagates: the API's middleware logs the trace and
        // answers RQ2, and the headless driver gets the exception it can debug. Catching it here would answer
        // the studio's own fault as the caller's, and this composer has no logger to keep the trace in.
    }

    /// <summary>The sketch leg of the export, and the one chain every caller runs so none can drift past
    /// another's gates: refuse an unloadable gamemode, build the world,
    /// gate the goals against the ground the rasterizer actually produced, project the resolved intent, judge
    /// what the document says and whether it can be walked, compose the XML.
    /// <paramref name="decorate"/> runs after the intent is projected and before the document is judged —
    /// where a driver states identity the intent does not carry (name, version, objective, authors).
    /// Exceptions propagate; <see cref="Compose"/> is the caller that turns them into structured errors, and
    /// a headless driver keeps its own crash semantics.
    /// <para><b>Every gate a sketch map is held to is in this chain</b>, so which door a caller came through
    /// cannot change what it is judged by.</para></summary>
    public static ExportComposition BuildAndCompose(
        Dict doc, string layoutJson, MapIntent intent, Action<Dict>? decorate = null)
    {
        // OB20 — every declared <gamemode> against PGM's own closed enum. First, because it needs no ground
        // and no built intent, and an id outside PGM's enum fails the whole map to load
        // (MapInfoImpl.parseGamemodes) however clean everything else is.
        if (RefuseUnknownGamemode(doc) is { } gamemodeRefusal) return gamemodeRefusal;

        // SK2 — a board whose extent is past what the studio will realize is refused before the build, not
        // during it: the cost is paid per column of the extent whether or not ground is drawn there. Asked in
        // the shared leg, so a headless driver is refused by the same measure the HTTP export is.
        if (SketchLayoutCheck.Check(layoutJson) is { Refuses: true } oversized)
            return Refuse("board too large", [.. oversized.Refusals], 422);

        var built = WorldBuilder.Build(layoutJson, intent);
        var goals = built.ResolvedIntent;

        // The ground this export ships, as the build itself read it. A sketch map's stored segments are
        // whatever the last `sketch/finish` rasterized, and a subtract cut, a relief solve or an edit after
        // the finish each move where ground is without re-entering that stage — so the layout in hand is the
        // only reading that describes the world about to be written, and it is the build's reading rather
        // than a second one taken beside it.
        var columns = built.Columns!;

        // OB17 — asked against the ground the rasterizer actually produced, not the plan's rectangles. A map
        // begun in Sketch never passes the compile gate at all, so this is the only place every export is
        // checked.
        if (RefuseObjectivePlacement(columns, goals) is { } placementRefusal) return placementRefusal;

        IntentGenerator.Apply(doc, goals);
        decorate?.Invoke(doc);

        // EX1 — the map must be walkable from its spawns to everything a match needs (§9), over the ground
        // above and the document the projection has just written. It reads the spawns, wools and goals the
        // slices emit, so it cannot be asked before them.
        if (RefuseUntraversable(doc, Surface(columns), AtY0(columns)) is { } cutOff) return cutOff;

        // EX2/EX3 — last, because it reads the document the slices have just written and compares it against
        // the intent they were written from. Every gate above it quantifies over a collection and so passes a
        // board with nothing on it.
        if (Playable(goals, doc) is { Refuses: true } unplayable)
            return Refuse("not a playable map", [.. unplayable.Refusals]);

        var renewCubes = WorldBuilder.RenewableCubeFootprints(goals);
        var sketchXml = MapXmlComposer.Compose(doc, isIntent: true, surfaceBlockIds: null, resources: [], renewCubes);
        return new(null, sketchXml, built, doc);
    }

    // ── OB20 — every declared <gamemode> must resolve against PGM's own closed enum ────────────────────────

    /// <summary>The rule ids the export gate owns itself. The rest it enforces for another document and cites
    /// that document's own id — <see cref="ObjectiveRules"/> for a goal, a gamemode or a prop in a clearance,
    /// and <see cref="DressingParseException.Rule"/> for a dressing document that will not parse.</summary>
    private static class ExportRules
    {
        /// <summary>Some part of the map cannot be walked to from the rest of it.</summary>
        /// <remarks>The finding names what is cut off. Bridge it: add ground, widen a border, or move the isolated spawn or objective onto the reachable part of the map.</remarks>
        [Rule(RuleCategory.Unplayable, RuleConcern.World, RuleConcern.Terrain)]
        public const string NotTraversable = "EX1";

        /// <summary>Nobody can enter the map: it declares no spawn of any kind.</summary>
        /// <remarks>Give the intent at least one spawn. An observer spawn is synthesised from the team spawns, so a map with no spawns at all has neither, and nobody can join it.</remarks>
        [Rule(RuleCategory.Unplayable, RuleConcern.Intent, RuleConcern.Spawn)]
        public const string NoSpawn = "EX2";

        /// <summary>What the intent declared is not in the document about to be written.</summary>
        /// <remarks>Nothing an author can do directly: the intent states this and the document does not carry it, which is a fault between the two. Check that the intent stored for the map is the one that was authored, and report it if it is.</remarks>
        [Rule(RuleCategory.Internal, RuleConcern.Intent, RuleConcern.World, RuleConcern.Studio)]
        public const string NotCarried = "EX3";

        /// <summary>A map with something to win has nobody to win it: it declares an objective and no team.</summary>
        /// <remarks>Add the teams. A wool, a destroyable and a core are each one team's to defend and the others' to take, so a map carrying one and no team has an objective nobody owns.</remarks>
        [Rule(RuleCategory.Unplayable, RuleConcern.Intent, RuleConcern.Objective)]
        public const string NoTeam = "EX4";
    }

    // ── EX2 · EX3 · EX4 — is this a map, and is it the map its author stated? ──────────────────────────────

    /// <summary>
    /// Whether the document about to be written is a map at all, and whether it is the map the intent stated.
    ///
    /// <para><b>Every other gate here quantifies over a collection, so a board with nothing on it satisfies all
    /// of them.</b> <c>OB17</c> asks whether a goal stands in void, <c>OB19</c> whether a prop crowds one, and
    /// the traversability check whether spawn and wool points reach each other — over empty lists, each is
    /// vacuously true. Two authoring-trial boards exported clean on exactly that: a ten-line <c>map.xml</c> with
    /// a name, a gamemode, an empty objective and a hunger rule, and every stage answered 200.</para>
    ///
    /// <para><b>The second question is the one those boards actually needed.</b> Their plan carried two spawns
    /// and two destroyables. The author did state them; they were lost somewhere between the plan and the
    /// export, which is a harder failure than an author writing nothing and is invisible to any gate reading
    /// one side alone. This is the last point that sees both, so it is where the two are compared.</para>
    ///
    /// <para><b>Almost nothing here is a judgement about how a map plays.</b> A map with no spawn cannot be
    /// entered, which is mechanical; a count that went in and did not come out is arithmetic. The one ruling
    /// is the author's and is stated as such: the three gamemodes the studio authors — CTW, DTM, DTC — are
    /// played by teams, so a map that states an objective must state who contests it.</para>
    ///
    /// <para>Whether a map needs an objective <em>at all</em> is still not asked here. <c>PL3</c> already says
    /// a plan with no goal has nothing to win, as a complaint because which goal a map carries is the
    /// author's, and a second copy of one rule under an export id is the duplication the shared vocabulary
    /// exists to prevent. It is also the half that could not be reported — this gate answers into a response
    /// whose body is XML or a zip, which has nowhere for a complaint to ride.</para>
    /// </summary>
    /// <param name="intent">The resolved intent, where one is in hand. Null on a path that has only the
    /// document, which leaves the carried-through comparison unasked rather than guessed at.</param>
    public static Findings Playable(MapIntent? intent, Dict doc)
    {
        var findings = new List<Finding>();

        if (Entries(doc, "spawns").Count == 0)
            findings.Add(new Finding(ExportRules.NoSpawn,
                "the map declares no spawn, so no player and no observer can enter it", Field: "spawns"));

        // EX4 — the author's rule: the three gamemodes the studio authors are played by teams, so a map that
        // states something to win must state who is contesting it. Asked of the objectives rather than of the
        // <gamemode> element, because that element is derived from exactly these three lists and PGM does not
        // read it to decide which modules run (OB7) — the objectives are what make it a CTW, DTM or DTC map.
        // A board with no objective at all is not asked: it is unfinished rather than wrong, which is PL3's to
        // say, and an FFA map is a shape the studio does not author.
        var goals = Entries(doc, "wools").Count + Entries(doc, "destroyables").Count + Entries(doc, "cores").Count;
        if (goals > 0 && Entries(doc, "teams").Count == 0)
            findings.Add(new Finding(ExportRules.NoTeam,
                $"the map declares {goals} objective(s) and no team, so there is nobody to defend or take them",
                Field: "teams"));

        if (intent is not null)
            foreach (var (field, stated, written) in new[]
                     {
                         ("spawns", intent.Spawns.Count, Entries(doc, "spawns").Count),
                         ("teams", intent.Teams?.Count ?? 0, Entries(doc, "teams").Count),
                         ("wools", intent.Wools?.Count ?? 0, Entries(doc, "wools").Count),
                         ("destroyables", intent.Destroyables?.Count ?? 0, Entries(doc, "destroyables").Count),
                         ("cores", intent.Cores?.Count ?? 0, Entries(doc, "cores").Count),
                     })
            {
                if (stated > 0 && written == 0)
                    findings.Add(new Finding(ExportRules.NotCarried,
                        $"the intent states {stated} {field} and the document carries none — they were lost "
                        + "between what was authored and what is about to be written",
                        Field: field));
            }

        return findings;
    }

    /// <summary>One of the document's top-level lists, or an empty one. A key that is absent and a key holding
    /// an empty list are the same answer to every question above, so they are not distinguished.</summary>
    private static List<object?> Entries(Dict doc, string key) =>
        doc.GetValueOrDefault(key) as List<object?> ?? [];

    /// <summary>One refusal, for the layer above to answer in the one envelope every gate in the studio
    /// refuses in. 409 is this composer's usual status — a document that is well-formed and conflicts with
    /// what the map is — and the two that are not say so.</summary>
    private static ExportComposition Refuse(string error, IReadOnlyList<Finding> findings, int status = 409) =>
        new(new Refusal(status, error, findings), null, null);

    private static ExportComposition? RefuseUnknownGamemode(Dict doc)
    {
        var declared = (doc.GetValueOrDefault("gamemode") as List<object?> ?? [])
            .Select(g => g as string ?? "").Where(g => g.Length > 0).ToList();
        var unknown = declared.Where(id => !Gamemodes.IsKnownId(id)).ToList();
        if (unknown.Count == 0) return null;

        return Refuse("unknown gamemode",
        [
            new Finding(ObjectiveRules.UnknownGamemode,
                $"declares <gamemode> {string.Join(", ", unknown)}, which PGM's Gamemode enum does not "
                + "recognize — the map would fail to load",
                Field: "gamemode", Subjects: unknown),
        ]);
    }

    // ── EX1 — can the map be walked? ──────────────────────────────────────────────────────────────────────

    /// <summary>The traversability judgement, in the one shape both legs answer it in: the columns a player
    /// can stand on and the columns that reach <c>y=0</c>, against the spawns, wools and goals the document
    /// declares. Null when everything a match needs can reach everything else.</summary>
    private static ExportComposition? RefuseUntraversable(
        Dict doc, HashSet<(int, int)>? surfaceColumns, HashSet<(int, int)>? y0Columns)
    {
        var walk = Traversability.Check(doc, surfaceColumns, y0Columns);
        if (walk.Connected) return null;

        return Refuse("not traversable",
        [
            new Finding(ExportRules.NotTraversable, walk.Message,
                Subjects: [.. walk.Isolated.Select(isolated => isolated.For is { } team
                    ? $"{isolated.Kind} {isolated.Name} (for {team})"
                    : $"{isolated.Kind} {isolated.Name}")]),
        ]);
    }

    /// <summary>Every column the board draws — a walkable surface, the same reading a scanned map's
    /// <c>layer_segment</c> rows give.</summary>
    private static HashSet<(int, int)> Surface(IReadOnlyList<(int X, int Z, int YFloor, int YTop)> columns)
        => [.. columns.Select(column => (column.X, column.Z))];

    /// <summary>The columns whose span reaches the world floor, which is what tells ground apart from a
    /// bridge or a platform standing over void.</summary>
    private static HashSet<(int, int)> AtY0(IReadOnlyList<(int X, int Z, int YFloor, int YTop)> columns)
        => [.. columns.Where(column => column.YFloor <= 0 && 0 <= column.YTop)
                      .Select(column => (column.X, column.Z))];

    // ── OB17 — objective placement, over the ground the rasterizer actually produced ──────────────────────

    private static ExportComposition? RefuseObjectivePlacement(
        IReadOnlyList<(int X, int Z, int YFloor, int YTop)> columns, MapIntent goals)
    {
        var findings = CheckGoalPlacement(columns, goals);
        return findings.Count == 0 ? null : Refuse("objective placement", [.. findings]);
    }

    /// <summary>
    /// <b><c>OB17</c> — every goal that stands where a goal may not: over the void, inside a spawn room, or
    /// inside a wool room.</b> Asked against the ground the rasterizer actually produced rather than the
    /// plan's rectangles, so it reads the world about to be written; a map begun in Sketch never passes the
    /// compile gate at all, which is what makes this the only reading every map gets.
    ///
    /// <para>Public because the export is not the only place worth asking it. It refuses here, where a map in
    /// that state cannot be shipped; <c>POST …/sketch/columns</c> asks the same question of the same build and
    /// carries the answer as complaints, so an author hears it while drawing rather than at the door.</para>
    /// </summary>
    public static Findings CheckGoalPlacement(
        IReadOnlyList<(int X, int Z, int YFloor, int YTop)> columns, MapIntent goals)
    {
        var groundColumns = Surface(columns);
        bool IsLand(int x, int z) => groundColumns.Contains((x, z));
        var findings = ObjectivePlacement.Check(PlacedGoals(goals), IsLand, KeepOuts(goals)).ToList();

        // A wool monument over the void the same way: it is not a PlacedGoal — its room is its own keep-out,
        // which the footprint check would misread as the goal reaching into it — but a monument with no
        // column under it can be neither reached nor won, and only this gate sees the resolved location.
        foreach (var wool in goals.Wools ?? [])
            foreach (var monument in wool.Monuments)
                if (!IsLand((int)Math.Floor(monument.Location.X), (int)Math.Floor(monument.Location.Z)))
                    findings.Add(new Finding(ObjectiveRules.Placement,
                        $"{monument.Team}'s monument on {wool.Owner}'s wool stands over the void — a goal "
                        + "with nothing under it cannot be won",
                        Subjects: [wool.Owner]));

        findings.AddRange(GoalsSharingGround(goals));
        return findings;
    }

    /// <summary><b><c>OB24</c> — two goals built into the same blocks.</b> Asked over the resolved boxes,
    /// which is the only reading that knows how big each structure came out: a plan states a marker and a
    /// style, and the volume around it is settled by the stamper.
    ///
    /// <para>The cause is nearly always the orbit rather than the author's hand. A goal occupies its own
    /// position <em>and every image of it</em>, so a second goal drawn where the symmetry maps the first
    /// lands inside it — and the plan, which reads the two as distinct placements at distinct coordinates,
    /// has nothing to object to. The boxes are what disagree, and they only exist here.</para></summary>
    private static IEnumerable<Finding> GoalsSharingGround(MapIntent goals)
    {
        var placed = PlacedBoxes(goals).ToList();
        for (var i = 0; i < placed.Count; i++)
            for (var j = i + 1; j < placed.Count; j++)
            {
                if (!placed[i].Box.Intersects(placed[j].Box)) continue;
                yield return new Finding(ObjectiveRules.GoalsShareGround,
                    $"the {placed[i].Kind} '{placed[i].Name}' and the {placed[j].Kind} '{placed[j].Name}' are "
                    + $"built into the same blocks — {Span(placed[i].Box)} and {Span(placed[j].Box)} overlap, "
                    + "so breaking one is breaking the other",
                    Subjects: [placed[i].Name, placed[j].Name]);
            }
    }

    /// <summary>A box as a reader checks it in-game: the two corners, nothing else. The record's own
    /// <c>ToString</c> prints its derived widths too, which is three numbers of noise per corner in a
    /// sentence whose whole job is a coordinate.</summary>
    private static string Span(BlockBox box) =>
        $"({box.MinX}, {box.MinY}, {box.MinZ})–({box.MaxX}, {box.MaxY}, {box.MaxZ})";

    /// <summary>Every destroyable and core as the volume it was stamped into, named as a player meets it.</summary>
    private static IEnumerable<(string Kind, string Name, BlockBox Box)> PlacedBoxes(MapIntent goals)
    {
        foreach (var destroyable in goals.Destroyables ?? [])
            if (destroyable.Box is { } box)
                yield return ("destroyable", GoalName(destroyable.Name, destroyable.Owner), box);
        foreach (var core in goals.Cores ?? [])
            if (core.Box is { } box)
                yield return ("core", GoalName(core.Name, core.Owner), box);
    }

    // Every destroyable/core as ground rather than as a marker, from the resolved intent's own stamped box —
    // the ground kept open is then the ground the structure occupies by construction (OB8), the same reason
    // the compile-time reading resolves the footprint from the piece rather than trusting the marker.
    private static IEnumerable<PlacedGoal> PlacedGoals(MapIntent goals)
    {
        foreach (var destroyable in goals.Destroyables ?? [])
            if (destroyable.Box is { } box)
                yield return new PlacedGoal("destroyable", GoalName(destroyable.Name, destroyable.Owner), Rect(box));
        foreach (var core in goals.Cores ?? [])
            if (core.Box is { } box)
                yield return new PlacedGoal("core", GoalName(core.Name, core.Owner), Rect(box));
    }

    // The stamped rooms a goal may not reach into: every spawn's and every wool's resolved frame, read
    // through the same public frame resolution WorldBuilder itself stamps by, so the export gate can
    // never disagree with the world it just built.
    private static List<GoalKeepOut> KeepOuts(MapIntent goals)
    {
        var keepOuts = new List<GoalKeepOut>();
        foreach (var spawn in goals.Spawns)
        {
            var frame = WorldBuilder.SpawnRoom(spawn).Frame;
            keepOuts.Add(new GoalKeepOut("spawn", spawn.Team, new BlockRect(frame.MinX, frame.MinZ, frame.MaxX, frame.MaxZ)));
        }
        foreach (var wool in goals.Wools ?? [])
        {
            var frame = WorldBuilder.WoolFrame(wool);
            keepOuts.Add(new GoalKeepOut("wool room", wool.Owner, new BlockRect(frame.MinX, frame.MinZ, frame.MaxX, frame.MaxZ)));
        }
        return keepOuts;
    }

    private static string GoalName(string name, string owner) => name.Length > 0 ? name : owner;

    // ObjectiveFootprint/ObjectivePlacement speak the stamper's inclusive block box; BlockRect's max is
    // exclusive (the same conversion PlanValidator's compile-time reading makes).
    private static BlockRect Rect(BlockBox box) => new(box.MinX, box.MinZ, box.MaxX + 1, box.MaxZ + 1);

}
