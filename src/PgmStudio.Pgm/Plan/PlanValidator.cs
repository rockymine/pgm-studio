using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Pgm.Derive;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Plan;

/// <summary>
/// The plan rule ids a finding cites — the structural ones the validator owns itself. Rules it merely
/// <em>enforces</em> for another document keep that document's own id instead: a core, a two-team goal and a
/// goal footprint cite <see cref="ObjectiveRules"/>, a room frame cites <see cref="RoomFrameRules"/>, and the
/// lint table cites the layout-rules checklist. <see cref="CornerContact"/> is the one of that checklist
/// declared here rather than stated in <c>rules.md</c>: this validator is the only thing that fires it, and a
/// rule a gate raises has to resolve in the catalogue. Stable names, kept apart from any task-tracking id.
/// </summary>
public static class PlanRules
{
    /// <summary>No generating piece: there is no land, so there is nothing to build.</summary>
    /// <remarks>Give the plan at least one piece whose role generates terrain — <c>piece</c>, <c>spawn</c> or <c>wool-room</c>. A <c>buffer</c> reserves space and produces none.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Plan)]
    public const string NoLand = "PL1";

    /// <summary>No spawn: PGM has nowhere to put a player and the map cannot be entered.</summary>
    /// <remarks>Add a spawn placement for each team: an entry in <c>placements.spawns</c> naming a piece and a fractional offset into it.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan, RuleConcern.Spawn)]
    public const string NoSpawn = "PL2";

    /// <summary>No objective of any kind — a complaint, since which goal a map carries is the author's.</summary>
    /// <remarks>Add a wool, destroyable or core placement. Nothing is blocked without one — the map compiles, builds and loads; it just cannot be won, so this is only worth acting on when the board is meant to be finished.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan, RuleConcern.Objective)]
    public const string NoObjective = "PL3";

    /// <summary>Two pieces claim the same ground at incompatible heights, so there is no coherent surface.</summary>
    /// <remarks>The two pieces named in the finding overlap and give their shared cells different surface heights. Move one off the other, or set both to the same <c>surface</c> — a step between them wants two pieces that meet at an edge, not two that overlap.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Plan)]
    public const string SurfaceClash = "PL4";

    /// <summary>A placement names a piece the plan does not have.</summary>
    /// <remarks>The placement's <c>piece</c> is a piece id the plan does not declare. Fix the spelling, or add the piece.</remarks>
    [Rule(RuleCategory.Unknown, RuleConcern.Plan)]
    public const string UnknownPiece = "PL5";

    /// <summary>A placement stands on a buffer, which is reserved empty space and produces no terrain.</summary>
    /// <remarks>Point the placement at a generating piece. A buffer is reserved empty space, so anything standing on one stands on nothing.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Plan)]
    public const string PlacementOnBuffer = "PL6";

    /// <summary>A placement falls outside the piece it names.</summary>
    /// <remarks>The placement's <c>at</c> is an offset in blocks from the named piece's minimum corner, so both components belong in 0..the piece's block span on that axis — its cell width times <c>globals.cell</c>. A value outside that lands the marker off its own piece.</remarks>
    [Rule(RuleCategory.Malformed, RuleConcern.Plan)]
    public const string PlacementOutside = "PL7";

    /// <summary>A spawn room cannot seat every monument its team will capture.</summary>
    /// <remarks>Enlarge the spawn piece, or reduce how many wools this team captures. A spawn room seats one monument per wool its team will take, and the wall it seats them on is the room's interior span.</remarks>
    [Rule(RuleCategory.Unsatisfiable,
        RuleConcern.Plan, RuleConcern.Spawn, RuleConcern.Objective, RuleConcern.Structure)]
    public const string MonumentSeats = "PL8";

    /// <summary>A wool cannot be reached from a capturing team's spawn at all.</summary>
    /// <remarks>Nothing walkable connects the capturing team's spawn to this wool: add a piece bridging the gap, or widen a border narrower than a corridor. Distance here is the walk over the surface, not the straight line.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan, RuleConcern.Objective, RuleConcern.Spawn)]
    public const string WoolUnreachable = "PL9";

    /// <summary>A destroyable style names something that is not a style.</summary>
    /// <remarks>Use one of the destroyable style ids the studio ships — <c>GET /api/destroyable-styles</c> lists them.</remarks>
    [Rule(RuleCategory.Unknown, RuleConcern.Plan, RuleConcern.Objective, RuleConcern.Style)]
    public const string UnknownStyle = "PL10";

    /// <summary>A wool colour names something that is not a dye.</summary>
    /// <remarks>Use one of the sixteen dye names — <c>GET /api/objectives/vocabulary</c> lists them under <c>wool.colors</c>. Leave the colour out entirely to have one picked: a team's first wool takes the team colour and later wools take distinct dyes.</remarks>
    [Rule(RuleCategory.Unknown, RuleConcern.Plan, RuleConcern.Objective)]
    public const string UnknownColor = "PL14";

    /// <summary>A wall is drawn on a pair of pieces that share no land interface.</summary>
    /// <remarks>A wall is drawn between two pieces that share no walkable border, so there is nothing for it to divide. Move the pieces until they touch along an edge, or drop the wall.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Plan, RuleConcern.Structure)]
    public const string WallWithoutInterface = "PL11";

    /// <summary>A bedrock wall is drawn on the wool room's own interface, so the wall and the room stand
    /// through each other and the room can barely be entered. The wool's own edge is never a wall seat.</summary>
    /// <remarks>Bedrock wall may not interface with the wool room piece: place down the bedrock wall around 15 blocks away from the room — on the approach piece's outer interface, where the approach meets the board.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Plan, RuleConcern.Structure, RuleConcern.Objective)]
    public const string WallOnWoolRoom = "PL13";

    /// <summary>A connected landmass mixes fanned and non-fanned pieces, so it has no coherent orbit image.</summary>
    /// <remarks>The symmetry fan copies whole islands, so every piece of one connected landmass must agree about <c>mirrors</c>. A non-fanned piece is for an isolated on-axis island; a mid that touches team land is authored as half its ground and completed by the fan.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Plan)]
    public const string MixedMirrors = "PL12";

    /// <summary>The document states a shape version this build does not read. A marker's <c>at</c> is an
    /// offset in blocks from its piece's minimum corner, and an earlier version stated the same field in
    /// cells — the same numbers, a different distance — so a document that names another version is refused
    /// rather than read under the wrong unit.</summary>
    /// <remarks>Convert the document to the current version. Nothing about a stale plan is recoverable by
    /// inspection: the unit a coordinate is in is not visible in the coordinate.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Plan)]
    public const string StaleVersion = "PL15";

    /// <summary>Two pieces meet at a single point and along no edge, and nothing else joins them. A corner is
    /// never a connection — a point has no walkable corridor mouth — so the board reads as one area where
    /// players find two, and the diagonal is the sneaky crossing that is not there. Suppressed where the pair
    /// already reaches the same land component through real interfaces, since the corner is then redundant
    /// rather than misleading.</summary>
    /// <remarks>Bridge the two with a piece sharing a real border with each — one block of shared edge is
    /// enough, and makes all three one land component — or move them apart so the board stops suggesting a
    /// crossing. Widening the corner is not one of the options: no amount of point contact connects.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Plan)]
    public const string CornerContact = "PC-C";
}

/// <summary>
/// The plan validator: structural <b>errors</b> that block a compile (unreachable wool, a wool path only
/// through a spawn piece, a placement outside its piece, different-surface piece overlaps) and non-blocking
/// <b>lint</b> that cites a provisional layout rule by id — including corner contacts (PC-C), which never form a
/// land interface but are author judgment, not blockers. (Narrow seams are legal connecting geometry, so there
/// is no per-seam width lint — corridor quality of the assembled footprint is a later concern.) Pure — a plan
/// validates the same on the server and in the editor. Lint rules are a small extensible table (see
/// <see cref="LintRules"/>).
/// </summary>
public static class PlanValidator
{
    /// <summary>
    /// <b>Whether what the plan says is coherent</b> — the structural errors that block a compile and the lint
    /// that rides along with it, in one answer. The only verb for that question: a caller asking whether the
    /// plan is refused reads <see cref="Findings.Refuses"/> rather than counting, and a caller wanting only the
    /// blocking half reads <see cref="Findings.Refusals"/>. One entry point rather than several is the point: a
    /// rule added to this class is reached by every caller, instead of by whichever door its author found.
    /// </summary>
    public static Findings Check(PlanModel plan)
    {
        var d = ContactGraph.Build(plan);
        var findings = new List<Finding>();
        findings.AddRange(Errors(plan, d));
        foreach (var rule in LintRules) findings.AddRange(rule(plan, d));
        return findings;
    }

    /// <summary>
    /// Whether the plan carries the things a map cannot exist without — a separate question from
    /// <see cref="Check"/>, which asks only whether what the plan *says* is coherent and therefore passes a
    /// document that says nothing. Kept apart on purpose: a plan under construction is legitimately incomplete
    /// (the composer scores candidates before it has placed anything), so these belong to the one-way gate that
    /// turns a plan into a map, not to the continuous validation the editor and the evaluator run.
    /// <para>Errors here block that gate; the lint is a complaint the author may ignore.</para>
    /// </summary>
    public static Findings Completeness(PlanModel plan)
    {
        var findings = new List<Finding>();

        // No generating piece: there is no land, so there is nothing to build. Reported alone because every
        // other complaint about a blank document is downstream of this one.
        if (!plan.Pieces.Any(pc => PlanRoles.IsGenerating(pc.Role)))
        {
            findings.Add(new Finding(PlanRules.NoLand, "this plan has no pieces — there is no land to build"));
            return findings;
        }

        // No spawn: PGM has nowhere to put a player, so the finished map cannot be entered at all. The hard one.
        if (plan.Placements.Spawns.Count == 0)
            findings.Add(new Finding(PlanRules.NoSpawn,
                "this plan has no spawn — a map with nowhere to put a player cannot be loaded"));

        // No objective of any kind. A complaint, not a block: which goal a map carries is the author's, all
        // three are authorable here, and one can still be set downstream when the map is configured.
        var p = plan.Placements;
        if (p.Wools.Count == 0 && p.Destroyables.Count == 0 && p.Cores.Count == 0)
            findings.Add(new Finding(PlanRules.NoObjective,
                "this plan has no objective — no wool, destroyable or core, so nothing wins the match",
                Severity.Complaint));

        return findings;
    }

    // ── errors (block the compile) ──────────────────────────────────────────────────────────────────────

    private static IEnumerable<Finding> Errors(PlanModel plan, ContactGraph d)
    {
        var findings = new List<Finding>();
        void Error(string rule, string message, params string[] subjects) =>
            findings.Add(new Finding(rule, message, Subjects: subjects.Length > 0 ? subjects : null));

        // PL15 — the shape version, first and alone: every coordinate below is read under the units this
        // version states, so a document from another one is refused rather than measured wrongly.
        if (plan.Version != PlanModel.CurrentVersion)
        {
            Error(PlanRules.StaleVersion,
                $"this plan states version {plan.Version}; this build reads version "
                + $"{PlanModel.CurrentVersion} — marker offsets are blocks from the piece corner, and version 1 "
                + "stated them in cells");
            return findings;
        }

        // different-surface overlaps: two pieces claim the same ground at incompatible heights — no coherent
        // surface, a genuine structural error. (Narrow seams connect and are legal; corner contacts are author
        // judgment and lint, not errors — see PC-C.)
        foreach (var c in d.Contacts)
            if (c.Kind == ContactKind.Overlap && c.SurfaceDelta != 0)
                Error(PlanRules.SurfaceClash,
                    $"overlapping pieces '{c.A}' and '{c.B}' have different surfaces (delta {c.SurfaceDelta})",
                    c.A, c.B);

        // a connected landmass must agree about mirroring: the fan copies whole islands, so a component that
        // is half fanned and half not has no coherent orbit image. The compiler throws on the same condition;
        // refusing it here is what lets the gate name the pieces instead of answering an anonymous 400.
        foreach (var component in d.Components)
        {
            var members = component.Select(id => d.Piece(id)!.Value).ToList();
            if (members.Select(p => p.Mirrors).Distinct().Count() > 1)
                Error(PlanRules.MixedMirrors,
                    $"landmass [{string.Join(", ", component)}] mixes mirrored and non-mirrored pieces — " +
                    "a non-fanned piece must form its own island",
                    [.. component]);
        }

        // placements must reference a real piece and sit inside it (a wool's flat area is its piece footprint).
        // A destroyable/core is the one marker kind that may name no piece at all (B128): an empty piece reads
        // `at` as an absolute board position, so `allowAbsolute` skips the reference check rather than flagging
        // a dangling one.
        foreach (var s in plan.Placements.Spawns) CheckInside(d, "spawn", s.Piece, s.At, findings);
        foreach (var w in plan.Placements.Wools) CheckInside(d, "wool", w.Piece, w.At, findings);
        foreach (var ir in plan.Placements.Iron) CheckInside(d, "iron", ir.Piece, ir.At, findings);
        foreach (var b in plan.Placements.Destroyables) CheckInside(d, "destroyable", b.Piece, b.At, findings, allowAbsolute: true);
        foreach (var c in plan.Placements.Cores) CheckInside(d, "core", c.Piece, c.At, findings, allowAbsolute: true);

        // OB22 — how far a goal may float. Both defaults are floors — enough that a goal reads as a monument
        // rather than as terrain — and a stated float had no ceiling at all, so one number put a goal wherever
        // an author typed. Asked of the stated value, which is what a plan knows: the derived question, whether
        // the structure's own box clears the map's build ceiling, needs terrain the plan has not solved yet and
        // is answered at the build (OB23).
        foreach (var b in plan.Placements.Destroyables)
            if (b.Float is { } floated && floated > ObjectiveDefaults.MaxFloat)
                Error(ObjectiveRules.FloatCap,
                    $"destroyable float {floated} is over the {ObjectiveDefaults.MaxFloat} a goal may float — "
                    + "a goal that high is reached by building a tower to it", b.Piece);
        foreach (var c in plan.Placements.Cores)
            if (c.Float is { } floated && floated > ObjectiveDefaults.MaxFloat)
                Error(ObjectiveRules.FloatCap,
                    $"core float {floated} is over the {ObjectiveDefaults.MaxFloat} a goal may float — "
                    + "a goal that high is reached by building a tower to it", c.Piece);

        // DC2 — float and leak are one knob: together they say how far players must dig under the core
        // (max(0, leak + 1 − float)). Authoring one alone silently pairs it with the other's default, which is
        // a dig depth nobody chose — so ask for both or neither.
        foreach (var c in plan.Placements.Cores)
            if (c.Float is null != c.Leak is null)
                Error(ObjectiveRules.PairedKnobs,
                    $"core '{(c.Float is null ? "leak" : "float")}' was set without its pair — "
                    + "float and leak only mean anything together (they set the dig depth)", c.Piece);

        // A core is stated by its interior and chosen from a closed range, so a casing with no lava in it is
        // not a thing that can be written down. What is left to check is the range itself: a number outside
        // it is an authoring error to name rather than a value to quietly clamp.
        foreach (var c in plan.Placements.Cores)
        {
            if (c.Lava is { } lava && (lava < ObjectiveDefaults.MinCoreLava || lava > ObjectiveDefaults.MaxCoreLava))
                Error(ObjectiveRules.Casing,
                    $"core lava footprint {lava} is outside {ObjectiveDefaults.MinCoreLava}–"
                    + $"{ObjectiveDefaults.MaxCoreLava} — a core is chosen from those, not sized freely", c.Piece);
            if (c.LavaHeight is { } height
                && (height < ObjectiveDefaults.MinCoreLavaHeight || height > ObjectiveDefaults.MaxCoreLavaHeight))
                Error(ObjectiveRules.Casing,
                    $"core lava height {height} is outside {ObjectiveDefaults.MinCoreLavaHeight}–"
                    + $"{ObjectiveDefaults.MaxCoreLavaHeight}", c.Piece);
        }

        // OB17 — where a goal may not stand. A destroyable and a core go almost anywhere; the exceptions are
        // the three places the map stops working, and all three are decided by the structure's FOOTPRINT
        // rather than by its marker, which is why a marker legally inside its piece can still be wrong.
        //
        //   void   — a goal hanging off the land is under the build slice's `block_place=deny(void)` rule,
        //            so the blocks that make it up cannot be broken and the objective cannot be completed.
        //   spawn  — spawn protection emits `block="never"` over the spawns union, which denies EVERYONE,
        //            the attacking team included. A goal inside it is a map that cannot be won, and nothing
        //            downstream reports that: PGM loads it and the round simply never ends.
        //   wool   — a wool room carries its own enter/block rules for its owner; a second objective sharing
        //            that ground inherits them and reads as part of the room besides.
        //
        // Reported per structure, naming the marker before the ground it stands on, so the reader is pointed
        // at the one goal that is wrong rather than at everything sharing its piece. An agent driving the
        // compile endpoint is refused for every one of the three rather than silently building an unwinnable
        // map (B21).
        // The land is the plan's pieces and the rooms are the frames the compiler will stamp; the rule itself
        // is ObjectivePlacement's, which the export gate asks again over the ground the rasterizer actually
        // produced. Stating it once is what keeps the two answers the same sentence.
        findings.AddRange(ObjectivePlacement.Check(
            PlacedGoals(plan, d),
            (x, z) => d.Pieces.Any(piece => x >= piece.Rect.MinX && x < piece.Rect.MaxX
                                         && z >= piece.Rect.MinZ && z < piece.Rect.MaxZ),
            [.. ObjectiveRooms(plan, d).Select(room => new GoalKeepOut(room.Kind, room.Piece, room.Frame))]));

        // An unknown style names no structure, so the compiler would have to invent one — and silently
        // stamping a pillar where the author asked for a cube is worse than saying the word is not a style.
        foreach (var b in plan.Placements.Destroyables)
            if (!string.IsNullOrEmpty(b.Style) && !DestroyableStyles.IsKnown(b.Style))
                Error(PlanRules.UnknownStyle,
                    $"destroyable style '{b.Style}' is not one of [{string.Join(", ", DestroyableStyles.All)}]",
                    b.Piece);

        // A colour PGM cannot resolve makes the wool unplaceable rather than mis-coloured, and the compiler
        // has no honest fallback: the auto-assignment it would otherwise use is what an absent colour asks for,
        // so silently substituting it would answer a different question from the one the plan asked.
        foreach (var w in plan.Placements.Wools)
            if (!string.IsNullOrEmpty(w.Color) && !WoolColors.IsColor(w.Color))
                Error(PlanRules.UnknownColor,
                    $"wool color '{w.Color}' is not one of [{string.Join(", ", WoolColors.All)}]",
                    w.Piece);

        // OB14 — a destroyable is one team's to defend and every other team's to break, which only means
        // something at two teams: PGM marks a goal shared exactly when the count is not 2, and what a shared
        // DTM goal should play like is undecided. The editor hides the tool outside order 2, but a
        // hand-written plan can still ask; compiling it would invent an answer to an open design question.
        if (Symmetry.Order(plan.Globals.Symmetry) != 2)
            foreach (var kind in new[]
                     {
                         plan.Placements.Destroyables.Count > 0 ? "destroyables" : null,
                         plan.Placements.Cores.Count > 0 ? "cores" : null,
                     }.Where(k => k is not null))
                Error(ObjectiveRules.TwoTeamOnly,
                    $"{kind} need a two-team symmetry; '{plan.Globals.Symmetry}' has "
                    + $"{Symmetry.Order(plan.Globals.Symmetry)} team(s)");

        // a wall mark must land on a real shared land interface (else there is no lane seam to build across)
        var landPairs = new HashSet<(string, string)>();
        foreach (var c in d.LandInterfaces) { landPairs.Add((c.A, c.B)); landPairs.Add((c.B, c.A)); }
        foreach (var w in plan.Walls)
            if (!landPairs.Contains((w.A, w.B)))
                Error(PlanRules.WallWithoutInterface,
                    $"wall '{w.A}'–'{w.B}' is not a shared land interface", w.A, w.B);

        // and never on the wool room's own edge: the wall and the room stamp through each other there, and
        // the device belongs an approach out, not against the room it defends
        var roleOf = plan.Pieces.ToDictionary(piece => piece.Id, piece => piece.Role);
        foreach (var w in plan.Walls)
            if (roleOf.GetValueOrDefault(w.A) == PlanRoles.WoolRoom || roleOf.GetValueOrDefault(w.B) == PlanRoles.WoolRoom)
                Error(PlanRules.WallOnWoolRoom,
                    $"bedrock wall '{w.A}'–'{w.B}' may not interface with the wool room piece — place it "
                    + "around 15 blocks away from the room", w.A, w.B);

        // WX2/WX3/WX6 + capacity — the stamped-room rules (docs/world-export/structures.md): a role piece
        // must be big enough for its shell, the marker's pad must be square, a wool room must have an entry
        // interface, and a spawn room must seat every monument it will host.
        findings.AddRange(RoomFrameErrors(plan, d));

        // reachability over the fanned board: every wool reachable from each capturing team's spawn, and not
        // only via a spawn piece
        findings.AddRange(ReachabilityErrors(plan, d));
        return findings;
    }

    // The stamped-room refusals (WX2/WX3/WX6 + monument capacity). Only role pieces are checked: a marker
    // on a plain piece keeps the legacy marker-anchored default room, which cannot refuse.
    private static IEnumerable<Finding> RoomFrameErrors(PlanModel plan, ContactGraph d)
    {
        foreach (var w in plan.Placements.Wools)
        {
            var frame = ResolveFrame(plan, d, "wool", w.Piece, PlanRoles.WoolRoom, w.At, w.Footprint, null,
                out var findings);
            foreach (var finding in findings) yield return finding;
            _ = frame;
        }
        foreach (var s in plan.Placements.Spawns)
        {
            var room = ResolveFrame(plan, d, "spawn", s.Piece, PlanRoles.Spawn, s.At, s.Footprint,
                RoomEdges.OfFacing(s.Facing), out var findings);
            foreach (var finding in findings) yield return finding;
            if (room is null) continue;

            // Capacity: this spawn will host a monument for every wool its team captures — on a symmetric
            // board, every authored wool per opposing team. Truncating at stamp time silently drops goals.
            var captured = plan.Placements.Wools.Count * Math.Max(1, d.Order - 1);
            var seats = RoomFrames.MonumentSlots(room.Frame, room.Frame.Doors[0]).Count;
            if (captured > seats)
                yield return new Finding(PlanRules.MonumentSeats,
                    $"spawn room on '{s.Piece}' seats {seats} monuments, {captured} captured wools need placing",
                    Subjects: [s.Piece]);
        }
    }

    // Resolve the room the export would stamp for a role-piece marker — including the piece's iron for a
    // spawn — surfacing each WX refusal as an error finding. Null (with findings) on refusal, null
    // (without) when the piece is plain or missing.
    private static ResolvedRoom? ResolveFrame(
        PlanModel plan, ContactGraph d, string kind, string pieceId, string role, double[] at,
        double[]? footprint, RoomEdge? spawnDoorEdge, out List<Finding> findings)
    {
        findings = [];
        var piece = d.Piece(pieceId);
        if (piece is null || piece.Value.Role != role) return null;

        var rect = piece.Value.Rect;
        var (markerX, markerZ) = PlanMarkers.Block(rect, at);
        List<(double MinX, double MinZ, double MaxX, double MaxZ)> entries = spawnDoorEdge is null
            ? [.. PlanCompiler.WoolEntrySegments(d, pieceId)
                .Select(seg => ((double)seg.MinX, (double)seg.MinZ, (double)seg.MaxX, (double)seg.MaxZ))]
            : [];
        List<(double X, double Z)> ironMarkers = spawnDoorEdge is null
            ? []
            : [.. plan.Placements.Iron.Where(ir => ir.Piece == pieceId)
                .Select(ir => PlanMarkers.Block(rect, ir.At))];
        // No shell, deliberately: a plan carries no room-style binding (structures.md §9), so the frame this
        // checks doors and entries against is the widest one any binding could leave — an interior with no
        // walls inset into it. A footprint that holds a room but not a shell is not refused anywhere; the
        // building simply does not stand on it.
        var room = RoomFrames.ResolveRoom(rect, PlanMarkers.Footprint(rect, footprint), shellBound: false,
            markerX, markerZ, entries, spawnDoorEdge, ironMarkers, out var refusal);
        if (refusal is not null)
            findings.Add(refusal with
            {
                Message = $"{kind} on '{pieceId}': {refusal.Message}",
                Subjects = [pieceId],
            });
        return room;
    }

    // The spawn door's wall from the marker facing (front = −z, the board reading the editor renders).

    private static void CheckInside(
        ContactGraph d, string kind, string pieceId, double[] at, List<Finding> findings, bool allowAbsolute = false)
    {
        // No piece named, and this kind allows it: `at` is an absolute board position, resolved for real
        // against solved terrain at export (PlanCompiler.ResolveGoalAnchor). There is no piece footprint to
        // bound it against here — the placement's own OB17 gate is where a goal like this gets checked, and
        // only once the ground it needs actually exists.
        if (allowAbsolute && pieceId.Length == 0) return;
        var piece = d.Plan.Pieces.FirstOrDefault(p => p.Id == pieceId);
        if (piece is null)
        {
            findings.Add(new Finding(PlanRules.UnknownPiece, $"{kind} references unknown piece '{pieceId}'",
                Subjects: [pieceId]));
            return;
        }
        // A buffer is reserved empty space — it produces no terrain, so nothing may be placed on it.
        if (PlanRoles.IsAnnotation(piece.Role))
        {
            findings.Add(new Finding(PlanRules.PlacementOnBuffer,
                $"{kind} references non-generating buffer '{pieceId}'", Subjects: [pieceId]));
            return;
        }
        // The offset is in blocks and the piece's rect in cells, so the bound is the piece's block span.
        double x = at[0], z = at[1];
        int w = piece.Rect.Width * d.Cell, h = piece.Rect.Height * d.Cell;
        if (x < 0 || z < 0 || x > w || z > h)
            findings.Add(new Finding(PlanRules.PlacementOutside,
                $"{kind} at [{x},{z}] falls outside piece '{pieceId}' (0..{w}, 0..{h} blocks)", Subjects: [pieceId]));
    }

    // Build the fanned piece graph (land + gap edges), then check each wool node is reachable from a capturing
    // team's spawn, and that some frontline path reaches it without passing through a spawn piece.
    private static IEnumerable<Finding> ReachabilityErrors(PlanModel plan, ContactGraph d)
    {
        var findings = new List<Finding>();
        var spawnPieces = plan.Placements.Spawns.Select(s => s.Piece).ToList();
        var woolPieces = plan.Placements.Wools.Select(w => w.Piece).ToList();
        if (spawnPieces.Count == 0 || woolPieces.Count == 0) return findings;

        var graph = FannedGraph.Build(d);
        var spawnNodes = graph.Nodes.Where(n => spawnPieces.Contains(n.PieceId)).Select(n => n.Key).ToHashSet();

        // SP1 measures a walk from the frontline, and the frontline is derived from the declared build zones.
        // Where none is declared the rule has nothing to answer about, so it says so once rather than
        // reporting every wool unreachable — which reads as a geometry fault and sends an author redrawing a
        // board whose shape was never the problem.
        var hasBuildZone = plan.BuildZones.Any();
        if (!hasBuildZone)
            findings.Add(new Finding("SP1",
                "this plan declares no build zone, so there is no frontline to walk from and no wool's "
                + "approach can be judged — add a `zones` entry marking where players may build",
                Severity.Complaint));

        foreach (var wp in woolPieces)
            for (var owner = 0; owner < d.Order; owner++)
            {
                var woolNode = (owner, wp);
                if (!graph.Nodes.Any(n => n.Key == woolNode)) continue;

                // every other team captures this wool: each capturing spawn must reach the wool node
                for (var captor = 0; captor < d.Order; captor++)
                {
                    if (captor == owner) continue;
                    var from = graph.Nodes.Where(n => n.Team == captor && spawnPieces.Contains(n.PieceId)).Select(n => n.Key);
                    if (!graph.Reachable(from, woolNode))
                        findings.Add(new Finding(PlanRules.WoolUnreachable,
                            $"wool on '{wp}' (team {owner}) is unreachable from team {captor}'s spawn",
                            Subjects: [wp]));
                }

                // SP1: the wool must be reachable from a frontline piece without crossing a spawn piece.
                // Asked only where the plan declares a build zone: the frontline is the set of pieces one
                // touches, so a zone-less plan has no piece to start the walk from and every wool would
                // answer unreachable on a board whose geometry is fine.
                if (!hasBuildZone) continue;
                var frontStarts = graph.Nodes.Where(n => graph.Frontline.Contains(n.Key) && !spawnNodes.Contains(n.Key)).Select(n => n.Key);
                if (!graph.ReachableAvoiding(frontStarts, woolNode, spawnNodes))
                    findings.Add(new Finding("SP1",
                        $"wool on '{wp}' (team {owner}) is only reachable through a spawn piece", Subjects: [wp]));
            }
        return findings;
    }

    // ── lint (never blocks; each cites a rule id) ───────────────────────────────────────────────────────

    /// <summary>The lint table — one entry per checked rule; add a rule by appending a delegate.</summary>
    public static readonly IReadOnlyList<Func<PlanModel, ContactGraph, IEnumerable<Finding>>> LintRules =
    [
        LintPcC, LintG2, LintG5, LintSp2, LintBz5, LintEl1, LintSt2, LintWx4, LintWx8, LintWl1,
        LintSp8, LintSp9, LintWl11, LintSt8, LintSt9, LintSt10, LintBz11, LintBoardEdges,
    ];

    private static Finding Lint(string rule, string msg, params string[] subjects) =>
        new(rule, msg, Severity.Complaint, Subjects: subjects.Length > 0 ? subjects : null);

    private static Finding Lint(string rule, string msg, FindingEdit? edit, params string[] subjects) =>
        new(rule, msg, Severity.Complaint, Subjects: subjects.Length > 0 ? subjects : null, Edit: edit);

    /// <summary>The change that grades a seam two pieces step across: a <c>line</c> relief mark six wide,
    /// running from a point inside the higher piece to a point inside the lower, stating the two surfaces
    /// at its ends. The run is the step's own size each side of the seam, so the mark solves to a slope a
    /// player walks. It lands on the group the compile fuses the two pieces into — <c>team</c> for pieces
    /// that mirror, <c>neutral</c> otherwise.</summary>
    private static FindingEdit? RampEdit(PieceInterfaces.Seam seam, DerivedPiece a, DerivedPiece b)
    {
        var (high, low) = a.Surface >= b.Surface ? (a, b) : (b, a);
        var delta = high.Surface - low.Surface;
        if (delta < 2) return null;
        var midX = (seam.X1 + seam.X2) / 2.0;
        var midZ = (seam.Z1 + seam.Z2) / 2.0;
        // The seam is axis-aligned; the ramp runs across it, from the high piece's side to the low piece's.
        var acrossX = seam.X1 == seam.X2;
        var towardLow = acrossX
            ? Math.Sign(low.Rect.CenterX - high.Rect.CenterX)
            : Math.Sign(low.Rect.CenterZ - high.Rect.CenterZ);
        if (towardLow == 0) towardLow = 1;
        var run = delta + 1;
        var start = acrossX ? new[] { midX - towardLow * run, midZ } : new[] { midX, midZ - towardLow * run };
        var end = acrossX ? new[] { midX + towardLow * run, midZ } : new[] { midX, midZ + towardLow * run };
        var group = high.Mirrors ? "team" : "neutral";
        return FindingEdit.Of(FindingEdit.Layout, $"relief.{group}.marks", FindingEdit.Add,
            new
            {
                id = $"ramp-{high.Id}-{low.Id}", kind = "line", width = 6,
                points = new[] { new[] { Math.Round(start[0], 1), Math.Round(start[1], 1) },
                                 new[] { Math.Round(end[0], 1), Math.Round(end[1], 1) } },
                h = new[] { high.Surface, low.Surface },
            },
            $"a line mark six wide from ({start[0]:0.#}, {start[1]:0.#}) at {high.Surface} to "
            + $"({end[0]:0.#}, {end[1]:0.#}) at {low.Surface}, so the seam grades over {2 * run} blocks");
    }

    // PC-C — a corner contact: two pieces meet at a single point. Per the Definitions a corner touch is never a
    // connection (no walkable corridor mouth). A corner as the pair's only relationship is a sneaky diagonal
    // between otherwise-separate areas; when the pieces already join the same land component through real
    // interfaces the corner is harmless, so it is suppressed.
    private static IEnumerable<Finding> LintPcC(PlanModel plan, ContactGraph d)
    {
        var comp = ComponentIndex(d);
        foreach (var c in d.Contacts)
            if (c.Kind == ContactKind.Corner && !SameComponent(comp, c.A, c.B))
                yield return Lint(PlanRules.CornerContact,
                    $"corner contact between separate areas: '{c.A}' and '{c.B}' touch at a point, not a "
                    + "corridor (no land interface)", c.A, c.B);
    }

    // Map each piece id to its land component index (components join pieces via real land interfaces and
    // same-surface overlaps). Two pieces in the same component are already walkably connected.
    private static Dictionary<string, int> ComponentIndex(ContactGraph d)
    {
        var comp = new Dictionary<string, int>();
        for (var i = 0; i < d.Components.Count; i++)
            foreach (var id in d.Components[i]) comp[id] = i;
        return comp;
    }

    private static bool SameComponent(Dictionary<string, int> comp, string a, string b) =>
        comp.TryGetValue(a, out var ca) && comp.TryGetValue(b, out var cb) && ca == cb;

    // G2 — minimum corridor width 10: a build zone narrower than the corridor minimum in either dimension.
    private static IEnumerable<Finding> LintG2(PlanModel plan, ContactGraph d)
    {
        foreach (var z in plan.Zones)
        {
            var r = ContactGraph.ToBlock(z.Rect, d.Cell);
            var min = Math.Min(r.Width, r.Depth);
            if (min < ContactGraph.CorridorMin)
                yield return Lint("G2", $"zone '{z.Id}' corridor width {min} < {ContactGraph.CorridorMin}", z.Id);
        }
    }

    // G5 — void gaps between individual landmasses are 10–20 per hop.
    private static IEnumerable<Finding> LintG5(PlanModel plan, ContactGraph d)
    {
        foreach (var g in d.GapLinks)
        {
            if (g.Hop == 0) continue;   // abutting inside the zone — not a hop
            if (g.Hop < 10) yield return Lint("G5", $"gap hop {g.Hop} < 10 between '{g.A}' and '{g.B}'", g.A, g.B);
            else if (g.Hop > 20) yield return Lint("G5", $"gap hop {g.Hop} > 20 between '{g.A}' and '{g.B}'", g.A, g.B);
        }
    }

    // SP2 — spawn near the back of its lane (toward the map edge), not the front half.
    private static IEnumerable<Finding> LintSp2(PlanModel plan, ContactGraph d)
    {
        foreach (var s in plan.Placements.Spawns)
        {
            var piece = d.Piece(s.Piece);
            if (piece is null) continue;
            var (bx, bz) = PlanMarkers.Block(piece.Value.Rect, s.At);
            // back = the piece half farther from the centre along its dominant axis
            var r = piece.Value.Rect;
            bool zAxis = r.Depth >= r.Width;
            double pos = zAxis ? bz : bx, mid = zAxis ? r.CenterZ : r.CenterX, center = 0;
            bool inBack = Math.Abs(pos - center) >= Math.Abs(mid - center);
            if (!inBack) yield return Lint("SP2", $"spawn on '{s.Piece}' not near the back of its lane", s.Piece);
        }
    }

    // BZ5 — zones never touch a spawn piece. Both kinds: a water lane reaching a spawn is the same fault
    // arriving later, and later is worse, because the defenders have already committed to the map they read
    // at the first tick.
    private static IEnumerable<Finding> LintBz5(PlanModel plan, ContactGraph d)
    {
        var spawnPieces = plan.Placements.Spawns.Select(s => s.Piece).ToHashSet();
        foreach (var z in plan.Zones)
        {
            var zr = ContactGraph.ToBlock(z.Rect, d.Cell);
            var what = z.IsWaterLane ? "water lane" : "build zone";
            foreach (var p in d.Pieces)
                if (spawnPieces.Contains(p.Id) && Touches(p.Rect, zr))
                    yield return Lint("BZ5", $"{what} '{z.Id}' touches spawn piece '{p.Id}'", z.Id, p.Id);
        }
    }

    // WL1 — a water lane covers void, never terrain. The lane opens because water at y=0 stops the columns
    // reading as void; over a piece the columns already hold terrain, so that part of the lane changes nothing
    // and the drawn rect overstates the route it adds.
    private static IEnumerable<Finding> LintWl1(PlanModel plan, ContactGraph d)
    {
        foreach (var z in plan.WaterLanes)
        {
            var zr = ContactGraph.ToBlock(z.Rect, d.Cell);
            foreach (var p in d.Pieces)
                if (PlanRoles.IsGenerating(p.Role) && Overlaps(p.Rect, zr))
                    yield return Lint("WL1", $"water lane '{z.Id}' covers terrain piece '{p.Id}' — a lane opens void, and this part of it is already land", z.Id, p.Id);
        }
    }

    // EL1 — a land seam a player cannot walk. Two pieces meeting at a surface delta of 2 or more leave a step
    // nobody walks up bare, so the seam is a place the relief has to smooth into a ramp or a flight before the
    // board is walkable there. The palette steps by 2 (rules.md EL1), so on an ordinary board every non-flush
    // seam is one of these: the list is the seams to grade, which is what makes it a hint rather than a fault.
    //
    // SP8 and WL11 are this rule asked at the two seams where the step decides a match — a spawn's egress and
    // a wool room's entry — and they say more about what crossing it costs. A seam either of them speaks for
    // is left to them, so one seam is named once by the most specific rule that owns it.
    private static IEnumerable<Finding> LintEl1(PlanModel plan, ContactGraph d)
    {
        var spoken = SeamsSp8AndWl11Own(plan, d);
        foreach (var seam in PieceInterfaces.Seams(d))
        {
            if (spoken.Contains((seam.A, seam.B))) continue;
            var a = d.Piece(seam.A);
            var b = d.Piece(seam.B);
            if (a is null || b is null) continue;
            if (PlanRoles.IsAnnotation(a.Value.Role) || PlanRoles.IsAnnotation(b.Value.Role)) continue;

            var delta = Math.Abs(a.Value.Surface - b.Value.Surface);
            if (delta < 2) continue;
            yield return Lint("EL1",
                $"'{seam.A}'–'{seam.B}' steps {delta} blocks — a player does not walk up more than one, so "
                + "this seam wants a ramp or a flight in the relief",
                RampEdit(seam, a.Value, b.Value), seam.A, seam.B);
        }
    }

    /// <summary>The seams <see cref="LintSp8"/> and <see cref="LintWl11"/> already report, so <c>EL1</c> does
    /// not name them a second time in less detail.</summary>
    private static HashSet<(string A, string B)> SeamsSp8AndWl11Own(PlanModel plan, ContactGraph d)
    {
        var spoken = new HashSet<(string, string)>();
        foreach (var finding in LintSp8(plan, d).Concat(LintWl11(plan, d)))
            if (finding.SubjectIds is [var a, var b]) spoken.Add((a, b));
        return spoken;
    }

    // ST2 — every iron marker belongs inside a spawn piece: iron there auto-renews in the export, and iron
    // anywhere else is a one-off block a team mines once. The whole cube must be inside, not just the marker
    // it centres on — a cube half in the spawn region renews half of itself.
    private static IEnumerable<Finding> LintSt2(PlanModel plan, ContactGraph d)
    {
        var spawnPieces = d.Pieces.Where(p => p.Role == PlanRoles.Spawn).ToList();
        foreach (var ir in plan.Placements.Iron)
        {
            var piece = d.Piece(ir.Piece);
            if (piece is null) continue;                       // unknown-piece handled as a structural error
            var (markerX, markerZ) = PlanMarkers.Block(piece.Value.Rect, ir.At);
            var inside = spawnPieces.Any(spawn =>
                RoomFrames.PlaceIron(markerX, markerZ, spawn.Rect).Placeable);
            if (!inside)
                yield return Lint("ST2",
                    $"the iron cube at ({markerX:0},{markerZ:0}) does not stand inside a spawn piece, so it "
                    + "is mined once rather than renewed", ir.Piece);
        }
    }

    // WX4 — a pad shifted off its marker to keep the wall clearance. The export follows the pad (the
    // emitted spawn/wool point moves with it), so the author is told rather than surprised.
    private static IEnumerable<Finding> LintWx4(PlanModel plan, ContactGraph d)
    {
        foreach (var w in plan.Placements.Wools)
            if (ResolveFrame(plan, d, "wool", w.Piece, PlanRoles.WoolRoom, w.At, w.Footprint, null, out _)
                is { Frame.Pad.Shifted: true })
                yield return Lint(RoomFrameRules.PadClearance, $"wool pad on '{w.Piece}' shifted inward to keep wall clearance — the exported wool point moves with it", w.Piece);
        foreach (var s in plan.Placements.Spawns)
            if (ResolveFrame(plan, d, "spawn", s.Piece, PlanRoles.Spawn, s.At, s.Footprint,
                RoomEdges.OfFacing(s.Facing), out _) is { Frame.Pad.Shifted: true })
                yield return Lint(RoomFrameRules.PadClearance, $"spawn pad on '{s.Piece}' shifted inward to keep wall clearance — the exported spawn point moves with it", s.Piece);
    }

    // WX8/WX9 — an iron marker that resolves unplaceable. Every marker on the board is checked, whether it
    // rides a framed spawn piece or a piece carrying no room at all: the export stamps nothing for one, the
    // marker stays on the board where the author put it, and this is what says so.
    private static IEnumerable<Finding> LintWx8(PlanModel plan, ContactGraph d)
    {
        var framedSpawnPieces = plan.Placements.Spawns
            .Select(s => s.Piece).Where(id => d.Piece(id)?.Role == PlanRoles.Spawn).ToHashSet();

        foreach (var s in plan.Placements.Spawns)
        {
            var room = ResolveFrame(plan, d, "spawn", s.Piece, PlanRoles.Spawn, s.At, s.Footprint,
                RoomEdges.OfFacing(s.Facing), out _);
            if (room is null) continue;
            foreach (var iron in room.Iron.Where(i => !i.Placeable))
                yield return Lint(RoomFrameRules.IronFit,
                    $"iron at ({iron.MarkerX}, {iron.MarkerZ}) on '{s.Piece}' cannot be placed: the cube needs "
                    + $"its {RoomFrames.IronSpan}×{RoomFrames.IronSpan} footprint inside the piece and "
                    + $"{RoomFrames.IronGap} blocks of clear air to the shell, and the room keeps the "
                    + "footprint it was given", s.Piece);
        }

        foreach (var ir in plan.Placements.Iron)
        {
            if (framedSpawnPieces.Contains(ir.Piece)) continue;
            if (d.Piece(ir.Piece) is not { } piece) continue;   // unknown piece is a structural error
            var (markerX, markerZ) = PlanMarkers.Block(piece.Rect, ir.At);
            if (RoomFrames.PlaceIron(markerX, markerZ, piece.Rect).Placeable) continue;
            yield return Lint(RoomFrameRules.IronFit,
                $"iron at ({markerX}, {markerZ}) on '{ir.Piece}' cannot be placed: its "
                + $"{RoomFrames.IronSpan}×{RoomFrames.IronSpan} cube reaches outside the piece it stands on, "
                + "so the export stamps nothing for it", ir.Piece);
        }
    }

    // SP8 — a spawn's egress steps two or more blocks and nothing bridges it: a Δ≥2 seam cannot be walked
    // up, so a player leaving the door cannot come back (and at Δ≥2 down, may not get out at all). Only the
    // seams ahead of the door are the egress; a cliff at the spawn's back is a legitimate wall.
    private static IEnumerable<Finding> LintSp8(PlanModel plan, ContactGraph d)
    {
        var seams = PieceInterfaces.Seams(d);
        foreach (var s in plan.Placements.Spawns)
        {
            var piece = d.Piece(s.Piece);
            if (piece is null) continue;
            var (dirX, dirZ) = DoorDirection(s.Facing);
            foreach (var seam in seams)
            {
                if (seam.A != s.Piece && seam.B != s.Piece) continue;
                var other = d.Piece(seam.A == s.Piece ? seam.B : seam.A);
                if (other is null) continue;
                var forward = (other.Value.Rect.CenterX - piece.Value.Rect.CenterX) * dirX
                            + (other.Value.Rect.CenterZ - piece.Value.Rect.CenterZ) * dirZ;
                if (forward <= 0) continue;
                var delta = Math.Abs(other.Value.Surface - piece.Value.Surface);
                if (delta >= 2)
                    yield return Lint("SP8",
                        $"spawn egress steps {delta} blocks at '{seam.A}'–'{seam.B}' — use 1-level steps or "
                        + "a ramp against the spawn",
                        RampEdit(seam, piece.Value, other.Value), seam.A, seam.B);
            }
        }
    }

    // WL11 — a wool room's approach steps two or more blocks and nothing bridges it. SP8's reading, asked of
    // a room that has no facing: a room has no front, so every entry interface is a door and all of them are
    // measured. The player who crosses one is the attacker — a team is kept out of its own wool — so the step
    // is met at the end of the run that decides the map, as a wall to build up or a drop with no way back.
    private static IEnumerable<Finding> LintWl11(PlanModel plan, ContactGraph d)
    {
        var seams = PieceInterfaces.Seams(d);
        var rooms = plan.Placements.Wools.Select(wool => wool.Piece)
            .Where(id => d.Piece(id) is { Role: PlanRoles.WoolRoom })
            .Distinct(StringComparer.Ordinal);

        foreach (var roomId in rooms)
        {
            var room = d.Piece(roomId)!.Value;
            // The entry set the cage cuts its doors on, so a lint and a stamper cannot disagree about which
            // seam is a way in. A room reachable only over a build zone declares no land seam here, and that
            // is BZ5's business rather than this rule's.
            var entries = PlanCompiler.WoolEntrySegments(d, roomId);
            if (entries.Count == 0) continue;

            foreach (var seam in seams)
            {
                if (seam.A != roomId && seam.B != roomId) continue;
                var other = d.Piece(seam.A == roomId ? seam.B : seam.A);
                if (other is null) continue;
                var delta = Math.Abs(other.Value.Surface - room.Surface);
                if (delta < 2) continue;
                var arrival = other.Value.Surface > room.Surface ? "drops" : "climbs";
                yield return Lint("WL11",
                    $"wool room approach {arrival} {delta} blocks at '{seam.A}'–'{seam.B}' — an attacker "
                    + "arrives across it, so use 1-level steps or a ramp against the room", seam.A, seam.B);
            }
        }
    }

    // SP9 — the ground a spawn door opens onto: at least 15 blocks before bare void, measured along the
    // door's own line, because that is where every player leaving the spawn walks first. A build zone
    // counts as ground here — a gap-only spawn whose door opens onto its egress bridge is an authored
    // motif — while a buffer is exactly the declared emptiness this rule exists to keep off the doorstep.
    private static IEnumerable<Finding> LintSp9(PlanModel plan, ContactGraph d)
    {
        const int minAhead = 15;
        var zoneRects = plan.BuildZones.Select(z => ContactGraph.ToBlock(z.Rect, d.Cell)).ToList();
        foreach (var s in plan.Placements.Spawns)
        {
            var piece = d.Piece(s.Piece);
            if (piece is null) continue;
            var (dirX, dirZ) = DoorDirection(s.Facing);
            var (markerX, markerZ) = PlanMarkers.Block(piece.Value.Rect, s.At);

            // walk the ray out of the spawn piece, then count crossable ground until the first bare block
            double x = markerX, z = markerZ;
            var rect = piece.Value.Rect;
            while (x >= rect.MinX && x < rect.MaxX && z >= rect.MinZ && z < rect.MaxZ) { x += dirX; z += dirZ; }
            var ahead = 0;
            bool Covers(BlockRect r) => x >= r.MinX && x < r.MaxX && z >= r.MinZ && z < r.MaxZ;
            while (ahead < minAhead && (d.Pieces.Any(p => Covers(p.Rect)) || zoneRects.Any(Covers)))
            { ahead++; x += dirX; z += dirZ; }
            if (ahead < minAhead)
                yield return Lint("SP9",
                    $"spawn door on '{s.Piece}' faces void {ahead} blocks out — a door wants at least "
                    + $"{minAhead} blocks of ground or bridgeable zone ahead", s.Piece);
        }
    }

    // ST8 — an approach wall's geometry: the interface it bars is a 10–20 block lane mouth (a wall across a
    // 30-block face bars a room, not a lane), and it stands about 15 blocks in front of the wool room's
    // entrance. The full-span clause needs no check: the compiler builds the wall across the whole interface.
    private static IEnumerable<Finding> LintSt8(PlanModel plan, ContactGraph d)
    {
        var seams = PieceInterfaces.Seams(d);
        foreach (var wall in seams.Where(seam => seam.Wall))
        {
            // a wall pair that includes the wool room itself is PL13's refusal — piling this lint on top of
            // that error would say one fault twice in two vocabularies
            if (wall.RoleA == PlanRoles.WoolRoom || wall.RoleB == PlanRoles.WoolRoom) continue;

            if (wall.Length < 10 || wall.Length > 20)
                yield return Lint("ST8",
                    $"approach wall '{wall.A}'–'{wall.B}' bars a {wall.Length}-block interface — "
                    + "a wall wants a 10–20 block lane mouth", wall.A, wall.B);

            // the entrance it defends: the nearest wool-room seam of either walled piece (an approach
            // touching two rooms defends the near one; the far room's distance means nothing). Only a wall
            // parallel to the entry stands "in front of" it — a wall barring a side interface of the same
            // approach defends a flank.
            int? nearest = null;
            foreach (var entry in seams)
            {
                if (entry.RoleA != PlanRoles.WoolRoom && entry.RoleB != PlanRoles.WoolRoom) continue;
                var approach = entry.RoleA == PlanRoles.WoolRoom ? entry.B : entry.A;
                if (approach != wall.A && approach != wall.B) continue;
                if ((wall.X1 == wall.X2) != (entry.X1 == entry.X2)) continue;   // perpendicular: a flank wall
                var standoff = SegmentGap(wall.X1, wall.Z1, wall.X2, wall.Z2, entry.X1, entry.Z1, entry.X2, entry.Z2);
                if (nearest is null || standoff < nearest) nearest = standoff;
            }
            if (nearest is { } gap && (gap < 10 || gap > 20))
                yield return Lint("ST8",
                    $"approach wall '{wall.A}'–'{wall.B}' stands {gap} blocks from the wool room's "
                    + "entrance — about 15 in front is the seat", wall.A, wall.B);
        }
    }

    /// <summary>The largest building a role piece may raise, in blocks square (the author's number). It is a
    /// hall a player crosses; past it the room is a field with a roof.</summary>
    public const int FootprintCap = 20;

    /// <summary>The largest protection region a role piece may be, in blocks (the author's numbers), across
    /// its short axis and along its long one. A region is the ground and the immunity together, so the long
    /// axis affords a room its approach without handing a team a field it cannot be fought in.</summary>
    public const int RegionCapAcross = 20, RegionCapAlong = 30;

    // ST9 — the building a role piece raises is at most 20×20 blocks. The footprint is what a player walks
    // into, so the cap is on the rectangle the shell actually stands on: the one the placement states, or the
    // one WX1 defaults from the piece where it states none.
    private static IEnumerable<Finding> LintSt9(PlanModel plan, ContactGraph d)
    {
        foreach (var (kind, pieceId, at, footprint, door) in RoleRooms(plan))
        {
            if (ResolveFrame(plan, d, kind, pieceId, RoleOf(kind), at, footprint, door, out _)
                is not { Frame: var frame }) continue;
            if (frame.Width <= FootprintCap && frame.Depth <= FootprintCap) continue;
            yield return Lint("ST9",
                $"the {kind} building on '{pieceId}' is {frame.Width}×{frame.Depth} blocks — a footprint is at "
                + $"most {FootprintCap}×{FootprintCap}, which is a hall a player crosses "
                + "rather than a field. State a smaller footprint on the placement", pieceId);
        }
    }

    // ST10 — a role piece is at most 20×30 blocks. The piece is the protection region and the ground the
    // building stands on, so an oversized one hands a team a field of immunity rather than a room; the
    // building it carries is capped separately (ST9).
    private static IEnumerable<Finding> LintSt10(PlanModel plan, ContactGraph d)
    {
        foreach (var piece in d.Pieces)
        {
            if (piece.Role is not (PlanRoles.WoolRoom or PlanRoles.Spawn)) continue;
            var (across, along) = (Math.Min(piece.Rect.Width, piece.Rect.Depth),
                                   Math.Max(piece.Rect.Width, piece.Rect.Depth));
            if (across <= RegionCapAcross && along <= RegionCapAlong) continue;
            yield return Lint("ST10",
                $"{piece.Role} piece '{piece.Id}' is {piece.Rect.Width}×{piece.Rect.Depth} blocks — a "
                + $"protection region is at most {RegionCapAcross}×{RegionCapAlong}, in "
                + "either orientation", piece.Id);
        }
    }

    // The role-piece rooms a plan states, as the arguments ResolveFrame takes.
    private static IEnumerable<(string Kind, string PieceId, double[] At, double[]? Footprint, RoomEdge? Door)>
        RoleRooms(PlanModel plan)
    {
        foreach (var w in plan.Placements.Wools) yield return ("wool", w.Piece, w.At, w.Footprint, null);
        foreach (var s in plan.Placements.Spawns)
            yield return ("spawn", s.Piece, s.At, s.Footprint, RoomEdges.OfFacing(s.Facing));
    }

    private static string RoleOf(string kind) => kind == "spawn" ? PlanRoles.Spawn : PlanRoles.WoolRoom;

    // BZ11 — one zone for a compact middle: several zones merging into one region whose union is itself a
    // plain rectangle is a stitched funnel one zone would have drawn — the author reads one crossing, the
    // player reads a patchwork. An L- or T-shaped union is a different thing: rectangles are the only shape
    // a zone comes in, so a non-rectangular region NEEDS several, and that decomposition is not stitching.
    private static IEnumerable<Finding> LintBz11(PlanModel plan, ContactGraph d)
    {
        foreach (var region in d.BuildRegions)
        {
            if (region.ZoneIds.Count < 2) continue;
            var zones = plan.BuildZones.Where(z => region.ZoneIds.Contains(z.Id)).ToList();
            var covered = new HashSet<(int X, int Z)>();
            foreach (var zone in zones)
                for (var cx = zone.Rect.X; cx < zone.Rect.X + zone.Rect.Width; cx++)
                    for (var cz = zone.Rect.Z; cz < zone.Rect.Z + zone.Rect.Height; cz++)
                        covered.Add((cx, cz));
            var bboxArea = (zones.Max(z => z.Rect.X + z.Rect.Width) - zones.Min(z => z.Rect.X))
                         * (zones.Max(z => z.Rect.Z + z.Rect.Height) - zones.Min(z => z.Rect.Z));
            if (covered.Count == bboxArea)
                yield return Lint("BZ11",
                    $"zones [{string.Join(", ", region.ZoneIds)}] stitch into one rectangular region — one "
                    + "zone draws this crossing; several belong only where the region genuinely turns a "
                    + "corner, or as separate regions (one per frontline leg, one flush zone per island)",
                    [.. region.ZoneIds]);
        }
    }

    /// <summary>The narrowest frontline a crossing may be, in blocks (the author's number). Under it a front
    /// reads as a funnel whatever share of its face it takes, which is the half <c>FR8</c>'s share cannot
    /// see.</summary>
    public const int MinFrontlineBlocks = 15;

    // FR8, FR9 and CT12 — the three reads that need the fanned raster board: a crossing spanning the face it
    // docks against, a frontline at least MinFrontlineBlocks wide, and every bridged pair of islands 15–40
    // blocks apart on a wool board. One delegate so the board is derived once; a plan the deriver cannot
    // read simply yields no board lint — the structural errors already name what is wrong with it.
    private static IEnumerable<Finding> LintBoardEdges(PlanModel plan, ContactGraph d)
    {
        BoardStructure board;
        try { board = BoardDeriver.Derive(plan); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException
                                       or NullReferenceException or IndexOutOfRangeException or KeyNotFoundException)
        { yield break; }

        // A crossing spans the face it docks against on every authored board (shares read 1.00, worst
        // incidental partial 0.40); the funnel fault reads 0.25 — a 10-block zone on an 80-block face. The
        // floor sits between the two populations.
        const int realCrossing = 10;
        const double shareFloor = 1.0 / 3;
        foreach (var face in PieceInterfaces.Frontages(board))
        {
            if (face.FrontlineBlocks >= realCrossing && face.FrontlineShare < shareFloor)
                yield return Lint("FR8",
                    $"piece '{face.Piece}' side {face.Side}: the zones turn {face.FrontlineBlocks} of its "
                    + $"{face.ExposedBlocks} exposed blocks into frontline ({face.FrontlineShare:0.00}) — a "
                    + "crossing wants to span the face it docks against, not funnel through a slice of it",
                    face.Piece);

            // FR9 is the absolute floor FR8's share cannot see: a crossing narrow in blocks reads as a
            // funnel however much of its face it takes, and a 10-block front on a 10-block face passes the
            // share at 1.00. Only a face that has a frontline at all is asked.
            if (face.FrontlineBlocks > 0 && face.FrontlineBlocks < MinFrontlineBlocks)
                yield return Lint("FR9",
                    $"piece '{face.Piece}' side {face.Side}: a {face.FrontlineBlocks}-block frontline is "
                    + $"under the {MinFrontlineBlocks} a crossing wants — players read a front that narrow "
                    + "as a funnel rather than as somewhere to cross",
                    face.Piece);
        }

        // CT12 judges the CTW strait: the direct crossing between the two team islands of a two-team wool
        // board. Chains over stepping stones are not straits (each hop is G5's), and a mid island between
        // the teams makes the crossing indirect by construction.
        if (plan.Placements.Wools.Count > 0 && Symmetry.Order(plan.Globals.Symmetry) == 2)
            foreach (var gap in PieceInterfaces.IslandGaps(board))
            {
                if (!gap.Direct || gap.RoleA != "team" || gap.RoleB != "team") continue;
                if (gap.Blocks is >= 15 and <= 40) continue;
                yield return Lint("CT12",
                    $"team islands [{string.Join(", ", gap.PiecesA)}] and [{string.Join(", ", gap.PiecesB)}] "
                    + $"stand {gap.Blocks} blocks apart — the CTW strait wants 15–40",
                    [.. gap.PiecesA.Concat(gap.PiecesB)]);
            }
    }

    // The board direction a door's facing opens toward (front = −z, the reading the editor renders).
    private static (int X, int Z) DoorDirection(string facing) => facing switch
    {
        "back" => (0, 1),
        "left" => (-1, 0),
        "right" => (1, 0),
        _ => (0, -1),
    };

    // The rectilinear gap between two axis-aligned segments: the interval gap per axis, largest axis wins —
    // for parallel segments that overlap laterally this is exactly the perpendicular standoff.
    private static int SegmentGap(int ax1, int az1, int ax2, int az2, int bx1, int bz1, int bx2, int bz2)
    {
        var gapX = Math.Max(0, Math.Max(Math.Min(bx1, bx2) - Math.Max(ax1, ax2), Math.Min(ax1, ax2) - Math.Max(bx1, bx2)));
        var gapZ = Math.Max(0, Math.Max(Math.Min(bz1, bz2) - Math.Max(az1, az2), Math.Min(az1, az2) - Math.Max(bz1, bz2)));
        return Math.Max(gapX, gapZ);
    }

    // ── objective footprints (OB17) ─────────────────────────────────────────────────────────────────────

    /// <summary>Every objective as ground rather than as a marker: the block rect the structure will cover,
    /// resolved from the marker's piece and offset. Both kinds resolve their unset fields to the same defaults
    /// the compiler would, so the rule judges the structure that gets built.</summary>
    private static IEnumerable<PlacedGoal> PlacedGoals(PlanModel plan, ContactGraph d)
    {
        foreach (var b in plan.Placements.Destroyables)
        {
            // An unknown style is its own error; size it as the default rather than reporting twice.
            DestroyableStyles.TryParse(string.IsNullOrEmpty(b.Style) ? null : b.Style, out var style);
            var (width, _, depth) = ObjectiveFootprint.Destroyable(style);
            if (Footprint(d, b.Piece, b.At, width, depth) is { } rect)
                yield return new PlacedGoal("destroyable", b.Id, rect, b.Piece);
        }
        foreach (var c in plan.Placements.Cores)
        {
            var (width, depth) = ObjectiveFootprint.Core(
                ObjectiveDefaults.CoreCasing(c.Lava ?? ObjectiveDefaults.CoreLava,
                    c.LavaHeight ?? ObjectiveDefaults.CoreLavaHeight, c.OpenTop ?? false).Size);
            if (Footprint(d, c.Piece, c.At, width, depth) is { } rect)
                yield return new PlacedGoal("core", c.Id, rect, c.Piece);
        }
    }

    /// <summary>The block rect a marker's structure covers, or null when the marker names no piece — either a
    /// dangling reference <see cref="CheckInside"/> already reported (a second finding for the same typo would
    /// only crowd the drawer), or, for a destroyable/core, a deliberate absolute placement (B128): the plan has
    /// no ground truth for it yet, so the compile-time OB17 gate is silent and the export-time gate, which
    /// reads the ground actually built, is the one that answers.</summary>
    private static BlockRect? Footprint(ContactGraph d, string pieceId, double[] at, int width, int depth)
    {
        var piece = d.Piece(pieceId);
        if (piece is null) return null;
        var (markerX, markerZ) = PlanMarkers.Block(piece.Value.Rect, at);
        // ObjectiveFootprint speaks the stamper's inclusive block box; BlockRect's max is exclusive.
        var box = ObjectiveFootprint.Centred(markerX, markerZ, width, depth);
        return new BlockRect(box.MinX, box.MinZ, box.MaxX + 1, box.MaxZ + 1);
    }

    /// <summary>The stamped rooms a goal may not reach into: every spawn's and every wool's resolved frame.
    /// These are the frames themselves rather than the pieces holding them — a piece is often much larger
    /// than the room it carries, and flagging a goal at its far corner would be a refusal with no cause.</summary>
    private static IEnumerable<(string Kind, string Piece, BlockRect Frame)> ObjectiveRooms(PlanModel plan, ContactGraph d)
    {
        foreach (var s in plan.Placements.Spawns)
            if (ResolveFrame(plan, d, "spawn", s.Piece, PlanRoles.Spawn, s.At, s.Footprint,
                RoomEdges.OfFacing(s.Facing), out _) is { } room)
                yield return ("spawn", s.Piece, Frame(room));
        foreach (var w in plan.Placements.Wools)
            if (ResolveFrame(plan, d, "wool", w.Piece, PlanRoles.WoolRoom, w.At, w.Footprint, null, out _)
                is { } room)
                yield return ("wool room", w.Piece, Frame(room));
    }

    // RoomFrame counts its extent the same way BlockRect does (Width = MaxX − MinX), so this is a re-label,
    // not a conversion — rounding here would widen every room by a block and refuse goals standing clear of it.
    private static BlockRect Frame(ResolvedRoom room) =>
        new(room.Frame.MinX, room.Frame.MinZ, room.Frame.MaxX, room.Frame.MaxZ);

    // ── shared helpers ──────────────────────────────────────────────────────────────────────────────────


    private static bool Touches(BlockRect a, BlockRect b)
    {
        int ix = Math.Min(a.MaxX, b.MaxX) - Math.Max(a.MinX, b.MinX);
        int iz = Math.Min(a.MaxZ, b.MaxZ) - Math.Max(a.MinZ, b.MinZ);
        return ix >= 0 && iz >= 0 && !(ix == 0 && iz == 0);
    }

    // Shared area, not a shared edge: a zone abutting a piece is the normal case (that is how a route meets
    // land), and only a genuine overlap covers ground.
    private static bool Overlaps(BlockRect a, BlockRect b) =>
        Math.Min(a.MaxX, b.MaxX) > Math.Max(a.MinX, b.MinX)
        && Math.Min(a.MaxZ, b.MaxZ) > Math.Max(a.MinZ, b.MinZ);
}
