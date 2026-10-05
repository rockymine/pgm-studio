using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Pgm.Derive;
using PgmStudio.Pgm.Shapes;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Evaluate.Terms;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Plan;

/// <summary>
/// The plan rule ids a finding cites — the structural ones the validator owns itself. Rules it merely
/// <em>enforces</em> for another document keep that document's own id instead: a core, a two-team goal and a
/// goal footprint cite <see cref="ObjectiveRules"/>, a room frame cites <see cref="RoomFrameRules"/>, and the
/// lint table cites <see cref="LayoutRules"/>. Stable names, kept apart from any task-tracking id.
/// </summary>
public static class PlanRules
{
    /// <summary>A plan has no piece that makes ground.</summary>
    /// <remarks>Add a piece to <c>pieces</c> with <c>role</c> set to <c>piece</c>, <c>spawn</c> or
    /// <c>wool-room</c>.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Plan)]
    public const string NoLand = "PL1";

    /// <summary>A plan has no spawn.</summary>
    /// <remarks>Add a spawn to <c>placements.spawns</c> with <c>piece</c> set to a piece that makes ground and
    /// <c>at</c> set to an offset in blocks inside it.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan, RuleConcern.Spawn)]
    public const string NoSpawn = "PL2";

    /// <summary>A plan has no objective.</summary>
    /// <remarks>Either add a wool to <c>placements.wools</c>, or add a monument to <c>placements.destroyables</c>,
    /// or add a core to <c>placements.cores</c>, or set the <c>placements.controlPoints</c> of the plan to
    /// 1.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan, RuleConcern.Objective)]
    public const string NoObjective = "PL3";

    /// <summary>A piece overlaps another piece that stands at a different surface.</summary>
    /// <remarks>Either move the <c>rect</c> of one of the two pieces in <c>pieces</c> until they no longer overlap,
    /// or set the <c>surface</c> of the two pieces to the same value.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Plan)]
    public const string SurfaceClash = "PL4";

    /// <summary>A marker names a piece that the plan does not have.</summary>
    /// <remarks>Either change the <c>piece</c> of the marker to the <c>id</c> of a piece in <c>pieces</c>, or add a
    /// piece with the <c>id</c> to <c>pieces</c>.</remarks>
    [Rule(RuleCategory.Unknown, RuleConcern.Plan)]
    public const string UnknownPiece = "PL5";

    /// <summary>A marker overlaps a buffer.</summary>
    /// <remarks>Either change the <c>piece</c> of the marker to a piece that makes ground, or set the <c>role</c>
    /// of the buffer in <c>pieces</c> to <c>piece</c>.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Plan)]
    public const string PlacementOnBuffer = "PL6";

    /// <summary>The offset of a marker is not between 0 and its piece's size in blocks, along x and along
    /// z.</summary>
    /// <remarks>Set the <c>at</c> of the marker to an offset between 0 and its piece's size in blocks, along x and
    /// along z.</remarks>
    [Rule(RuleCategory.Malformed, RuleConcern.Plan)]
    public const string PlacementOutside = "PL7";

    /// <summary>A spawn room holds fewer than one wool monument for each wool its team captures.</summary>
    /// <remarks>Either widen the <c>rect</c> of the spawn's room piece in <c>pieces</c>, or delete a wool from
    /// <c>placements.wools</c>.</remarks>
    [Rule(RuleCategory.Unsatisfiable,
        RuleConcern.Plan, RuleConcern.Spawn, RuleConcern.Objective, RuleConcern.Structure)]
    public const string MonumentSeats = "PL8";

    /// <summary>A wool has no route from a capturing team's spawn.</summary>
    /// <remarks>Either add a piece to <c>pieces</c> that has a shared edge with each of the pieces it joins, or add
    /// a build region to <c>zones</c> that touches both.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan, RuleConcern.Objective, RuleConcern.Spawn)]
    public const string WoolUnreachable = "PL9";

    /// <summary>A monument's form is not one of the six monument forms.</summary>
    /// <remarks>Change the <c>style</c> of the monument in <c>placements.destroyables</c> to <c>pillar-1</c>,
    /// <c>pillar-2</c>, <c>pillar-3</c>, <c>cube-3</c>, <c>cube-4</c> or <c>column-plus</c>.</remarks>
    [Rule(RuleCategory.Unknown, RuleConcern.Plan, RuleConcern.Objective, RuleConcern.Style)]
    public const string UnknownStyle = "PL10";

    /// <summary>A wool's colour is not one of the sixteen dye names.</summary>
    /// <remarks>Either change the <c>color</c> of the wool in <c>placements.wools</c> to one of the sixteen dye
    /// names, or set the <c>color</c> of the wool to <c>null</c>.</remarks>
    [Rule(RuleCategory.Unknown, RuleConcern.Plan, RuleConcern.Objective)]
    public const string UnknownColor = "PL14";

    /// <summary>A wall names a pair of pieces that has no shared edge.</summary>
    /// <remarks>Either move the <c>rect</c> of one of the two pieces in <c>pieces</c> until the pair has a shared
    /// edge, or delete the wall from <c>walls</c>.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Plan, RuleConcern.Structure)]
    public const string WallWithoutInterface = "PL11";

    /// <summary>A piece runs past either end of a wall by 1 block or more.</summary>
    /// <remarks>Either shrink the <c>rect</c> of the piece that runs past the wall in <c>pieces</c> until it ends
    /// where the wall ends, or delete the wall from <c>walls</c>.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Plan, RuleConcern.Structure)]
    public const string WallAtJunction = "PL17";

    /// <summary>A wall touches a wool's room piece.</summary>
    /// <remarks>Either change the pair in <c>walls</c> to one whose shared edge is between 10 and 20 blocks in
    /// front of the wool room's entrance, or delete the wall from <c>walls</c>.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Plan, RuleConcern.Structure, RuleConcern.Objective)]
    public const string WallOnWoolRoom = "PL13";

    /// <summary>An island holds a piece that the symmetry copies, and a piece that it does not copy.</summary>
    /// <remarks>Either set the <c>mirrors</c> of every piece of the island in <c>pieces</c> to the same value, or
    /// move the <c>rect</c> of a piece in <c>pieces</c> until it no longer touches the island.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Plan)]
    public const string MixedMirrors = "PL12";

    /// <summary>The plan version of a plan is not 2.</summary>
    /// <remarks>Change each <c>at</c> in <c>placements</c> from cells to blocks, then set the <c>plan</c> of the
    /// document to 2.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Plan)]
    public const string StaleVersion = "PL15";

    /// <summary>The number of capture points a plan states is not 1, the number of its teams, or the number of its
    /// teams plus 1.</summary>
    /// <remarks>Set the <c>placements.controlPoints</c> of the plan to 1, to the number of teams, or to the number
    /// of teams plus 1.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Plan, RuleConcern.Objective)]
    public const string ControlPointCount = "PL16";

    /// <summary>Two pieces of different islands touch at a single point.</summary>
    /// <remarks>Either add a piece to <c>pieces</c> that has a shared edge with each of the two, or move the
    /// <c>rect</c> of one of the two pieces in <c>pieces</c> until it no longer touches the other.</remarks>
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
        // four are authorable here, and one can still be set downstream when the map is configured.
        var p = plan.Placements;
        if (p.Wools.Count == 0 && p.Destroyables.Count == 0 && p.Cores.Count == 0
            && (p.ControlPoints ?? 0) <= 0)
            findings.Add(new Finding(PlanRules.NoObjective,
                "this plan states no objective — no wool, destroyable, core or capture point — so nothing in "
                + "it wins the match. A board stating its goals on the intent instead is answered there",
                Severity.Complaint));

        // A count the board's symmetry cannot lay out. The compiler places none rather than rounding to a
        // number it can, so the plan would compile to a capture board with no points on it.
        if (p.ControlPoints is { } count && count > 0
            && !ControlPointLayout.Fans(count, Symmetry.Order(plan.Globals.Symmetry)))
            findings.Add(new Finding(PlanRules.ControlPointCount,
                $"this plan states {count} capture point(s), which a board of "
                + $"{Symmetry.Order(plan.Globals.Symmetry)} team(s) cannot lay out — a point is the centre of "
                + "symmetry or one of a ring the orbit fans, so the counts that work are 1, one per team, or "
                + "one per team plus a centre",
                Severity.Complaint));

        return findings;
    }

    // ── errors (block the compile) ──────────────────────────────────────────────────────────────────────

    private static IEnumerable<Finding> Errors(PlanModel plan, ContactGraph d)
    {
        var findings = new List<Finding>();
        void Error(string rule, string message, params string[] subjects) =>
            findings.Add(new Finding(rule, message, Subjects: subjects.Length > 0 ? subjects : null));
        // Said rather than refused: a board whose wall can be walked round still builds and still plays, and
        // where the line should sit instead is the author's call.
        void Complain(string rule, string message, params string[] subjects) =>
            findings.Add(new Finding(rule, message, Severity.Complaint,
                                     Subjects: subjects.Length > 0 ? subjects : null));

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
        // A destroyable/core is the one marker kind that may name no piece at all: an empty piece reads
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
        // map.
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

        // and only where no land runs on past either of its ends. A wall spans the interval two pieces share;
        // ground one block beyond an end, on either face, is ground a player beside the wall steps round it on.
        // That is the fault `docs/gameplay/approaches.md` states as "ground pulled out past the wall's ends is
        // what breaks it", and it is a relation between rectangles — the two the wall names, or a third it
        // stands against at a T — which no render of a built world can show, because by then they are terrain.
        foreach (var c in d.WallInterfaces)
        {
            if (d.Piece(c.A) is not { } pa || d.Piece(c.B) is not { } pb) continue;
            if (FlankOf(d.Pieces, ContactGraph.WallFootprint(pa, pb)) is not { } flank) continue;
            Complain(PlanRules.WallAtJunction,
                $"wall '{c.A}'–'{c.B}' is not flanked: '{flank.Id}' runs past the wall's end, so a player on it "
                + "beside the wall rounds it with one step off the corner rather than crossing it. A wall sits "
                + "between two pieces of the same width with nothing beyond its ends — put one the lane's own "
                + "width between the two and wall that seam instead",
                c.A, c.B, flank.Id);
        }

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
            var frame = ResolveFrame(plan, d, "wool", w.Piece, PlanRoles.WoolRoom, w.At, w.Footprint, [],
                out var findings);
            foreach (var finding in findings) yield return finding;
            _ = frame;
        }
        foreach (var s in plan.Placements.Spawns)
        {
            var room = ResolveFrame(plan, d, "spawn", s.Piece, PlanRoles.Spawn, s.At, s.Footprint,
                PieceDoors.ForSpawn(d, s.Piece, s.Facing), out var findings);
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
        double[]? footprint, IReadOnlyList<RoomEdge> spawnDoorEdges, out List<Finding> findings)
    {
        findings = [];
        var piece = d.Piece(pieceId);
        if (piece is null || piece.Value.Role != role) return null;

        var rect = piece.Value.Rect;
        var (markerX, markerZ) = PlanMarkers.Block(rect, at);
        var isSpawn = role == PlanRoles.Spawn;
        List<(double MinX, double MinZ, double MaxX, double MaxZ)> entries = isSpawn
            ? []
            : [.. PlanCompiler.WoolEntrySegments(d, pieceId)
                .Select(seg => ((double)seg.MinX, (double)seg.MinZ, (double)seg.MaxX, (double)seg.MaxZ))];
        List<(double X, double Z)> ironMarkers = isSpawn
            ? [.. plan.Placements.Iron.Where(ir => ir.Piece == pieceId)
                .Select(ir => PlanMarkers.Block(rect, ir.At))]
            : [];
        // No shell, deliberately: a plan carries no room-style binding (structures.md §9), so the frame this
        // checks doors and entries against is the widest one any binding could leave — an interior with no
        // walls inset into it. A footprint that holds a room but not a shell is not refused anywhere; the
        // building simply does not stand on it.
        var room = RoomFrames.ResolveRoom(rect, PlanMarkers.Footprint(rect, footprint), shellBound: false,
            markerX, markerZ, entries, spawnDoorEdges, ironMarkers, out var refusal);
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
            findings.Add(new Finding(LayoutRules.NoBuildRegion,
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
                    findings.Add(new Finding(LayoutRules.SpawnOnWoolRoute,
                        $"wool on '{wp}' (team {owner}) is only reachable through a spawn piece", Subjects: [wp]));
            }
        return findings;
    }

    // ── lint (never blocks; each cites a rule id) ───────────────────────────────────────────────────────

    /// <summary>The lint table — one entry per checked rule; add a rule by appending a delegate.</summary>
    public static readonly IReadOnlyList<Func<PlanModel, ContactGraph, IEnumerable<Finding>>> LintRules =
    [
        LintPcC, LintG2, LintG5, LintSp2, LintBz5, LintEl1, LintSt2, LintWx4, LintWx8, LintBz12,
        LintSp8, LintSp9, LintBz121, LintSt8, LintSt9, LintSt10, LintBz11, LintBoardEdges,
        LintZoneReach,
    ];

    /// <summary>Ids as a finding names them: "'a'", "'a' and 'b'", "'a', 'b' and 'c'".</summary>
    internal static string Quoted(IEnumerable<string> ids)
    {
        var quoted = ids.Select(id => $"'{id}'").ToList();
        return quoted.Count <= 1 ? string.Concat(quoted) : $"{string.Join(", ", quoted[..^1])} and {quoted[^1]}";
    }

    /// <summary>A piece by its id, or the edge of the layout where a run reaches none.</summary>
    private static string Named(string pieceId) => pieceId.Length > 0 ? $"'{pieceId}'" : "the edge of the layout";

    /// <summary>The edge of a band a measured number broke: "less than 10 blocks", "more than 20 blocks".</summary>
    private static string Beyond(int value, int low, int high) =>
        value < low ? $"less than {low} blocks" : $"more than {high} blocks";

    private static Finding Lint(string rule, string msg, params string[] subjects) =>
        new(rule, msg, Severity.Complaint, Subjects: subjects.Length > 0 ? subjects : null);

    private static Finding Lint(string rule, string msg, DocumentEdit? edit, params string[] subjects) =>
        new(rule, msg, Severity.Complaint, Subjects: subjects.Length > 0 ? subjects : null, Edit: edit);

    /// <summary>The change that grades a seam two pieces step across: a <c>line</c> relief mark six wide,
    /// running from a point inside the higher piece to a point inside the lower, stating the two surfaces
    /// at its ends. The run is the step's own size each side of the seam, so the mark solves to a slope a
    /// player walks. It lands on the group the compile fuses the two pieces into — <c>team</c> for pieces
    /// that mirror, <c>neutral</c> otherwise.</summary>
    private static DocumentEdit? RampEdit(PieceInterfaces.Seam seam, DerivedPiece a, DerivedPiece b)
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
        return DocumentEdit.Of(MapDocuments.Layout, $"relief.{group}.marks", DocumentEdit.Add,
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
                    $"pieces '{c.A}' and '{c.B}' touch at a single point and share no edge", c.A, c.B);
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
                yield return Lint(LayoutRules.CorridorWidth, $"build region '{z.Id}' is {min} blocks across its shorter side, less than {ContactGraph.CorridorMin} blocks", z.Id);
        }
    }

    // G5 — void gaps between individual landmasses are 10–20 per hop.
    private static IEnumerable<Finding> LintG5(PlanModel plan, ContactGraph d)
    {
        foreach (var g in d.GapLinks)
        {
            if (g.Hop == 0) continue;   // abutting inside the zone — not a hop
            if (g.Hop < GapHopBand.MinHop)
                yield return Lint(LayoutRules.VoidHop, $"pieces '{g.A}' and '{g.B}' have a gap of {g.Hop} blocks between them, less than {GapHopBand.MinHop} blocks", g.A, g.B);
            else if (g.Hop > GapHopBand.MaxHop)
                yield return Lint(LayoutRules.VoidHop, $"pieces '{g.A}' and '{g.B}' have a gap of {g.Hop} blocks between them, more than {GapHopBand.MaxHop} blocks", g.A, g.B);
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
            if (!inBack) yield return Lint(LayoutRules.SpawnAtBack, $"spawn '{s.Id}' at ({bx:0.#}, {bz:0.#}) stands in the half of room piece '{s.Piece}' closer to the symmetry centre", s.Piece);
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
            var what = z.IsWaterLane ? "water lane" : "build region";
            foreach (var p in d.Pieces)
                if (spawnPieces.Contains(p.Id) && Touches(p.Rect, zr))
                    yield return Lint(LayoutRules.ZoneTouchesSpawn, $"{what} '{z.Id}' touches room piece '{p.Id}' of a spawn", z.Id, p.Id);
        }
    }

    // BZ12 — a water lane covers void, never terrain. A water lane is a build zone that opens later: water at
    // y=0 stops its columns reading as void. Over a piece the columns already hold terrain, so that part of the
    // lane changes nothing and the drawn rect overstates the route it adds.
    private static IEnumerable<Finding> LintBz12(PlanModel plan, ContactGraph d)
    {
        foreach (var z in plan.WaterLanes)
        {
            var zr = ContactGraph.ToBlock(z.Rect, d.Cell);
            foreach (var p in d.Pieces)
                if (PlanRoles.IsGenerating(p.Role) && Overlaps(p.Rect, zr))
                    yield return Lint(LayoutRules.WaterLaneOverGround, $"water lane '{z.Id}' overlaps piece '{p.Id}' that makes ground", z.Id, p.Id);
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
            yield return Lint(LayoutRules.UnwalkableStep,
                $"pieces '{seam.A}' and '{seam.B}' differ by {delta} blocks in height where they share an edge, "
                + "more than 1 block",
                RampEdit(seam, a.Value, b.Value), seam.A, seam.B);
        }
    }

    /// <summary>The seams <see cref="LintSp8"/> and <see cref="LintBz121"/> already report, so <c>EL1</c> does
    /// not name them a second time in less detail.</summary>
    private static HashSet<(string A, string B)> SeamsSp8AndWl11Own(PlanModel plan, ContactGraph d)
    {
        var spoken = new HashSet<(string, string)>();
        foreach (var finding in LintSp8(plan, d).Concat(LintBz121(plan, d)))
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
                yield return Lint(LayoutRules.IronInSpawn,
                    $"iron '{ir.Id}' at ({markerX:0}, {markerZ:0}) has its {RoomFrames.IronSpan} by "
                    + $"{RoomFrames.IronSpan} cube outside the room piece of every spawn", ir.Piece);
        }
    }

    // WX4 — a pad shifted off its marker to keep the wall clearance. The export follows the pad (the
    // emitted spawn/wool point moves with it), so the author is told rather than surprised.
    private static IEnumerable<Finding> LintWx4(PlanModel plan, ContactGraph d)
    {
        foreach (var w in plan.Placements.Wools)
            if (ResolveFrame(plan, d, "wool", w.Piece, PlanRoles.WoolRoom, w.At, w.Footprint, [], out _)
                is { Frame.Pad.Shifted: true })
                yield return Lint(RoomFrameRules.PadClearance, $"wool '{w.Id}' on piece '{w.Piece}' has its pad moved inward to keep 1 block from the walls of its room", w.Piece);
        foreach (var s in plan.Placements.Spawns)
            if (ResolveFrame(plan, d, "spawn", s.Piece, PlanRoles.Spawn, s.At, s.Footprint,
                PieceDoors.ForSpawn(d, s.Piece, s.Facing), out _) is { Frame.Pad.Shifted: true })
                yield return Lint(RoomFrameRules.PadClearance, $"spawn '{s.Id}' on piece '{s.Piece}' has its pad moved inward to keep 1 block from the walls of its room", s.Piece);
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
                PieceDoors.ForSpawn(d, s.Piece, s.Facing), out _);
            if (room is null) continue;
            foreach (var iron in room.Iron.Where(i => !i.Placeable))
                yield return Lint(RoomFrameRules.IronFit,
                    $"iron at ({iron.MarkerX}, {iron.MarkerZ}) in the room of spawn '{s.Id}' has no place for its "
                    + $"{RoomFrames.IronSpan} by {RoomFrames.IronSpan} cube inside piece '{s.Piece}' and at least "
                    + $"{RoomFrames.IronGap} blocks from the room", s.Piece);
        }

        foreach (var ir in plan.Placements.Iron)
        {
            if (framedSpawnPieces.Contains(ir.Piece)) continue;
            if (d.Piece(ir.Piece) is not { } piece) continue;   // unknown piece is a structural error
            var (markerX, markerZ) = PlanMarkers.Block(piece.Rect, ir.At);
            if (RoomFrames.PlaceIron(markerX, markerZ, piece.Rect).Placeable) continue;
            yield return Lint(RoomFrameRules.IronFit,
                $"iron '{ir.Id}' at ({markerX}, {markerZ}) has its {RoomFrames.IronSpan} by "
                + $"{RoomFrames.IronSpan} cube reaching outside piece '{ir.Piece}'", ir.Piece);
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
                    yield return Lint(LayoutRules.SpawnExitStep,
                        $"spawn '{s.Id}' has its room piece '{s.Piece}' {delta} blocks above or below piece "
                        + $"'{(seam.A == s.Piece ? seam.B : seam.A)}' ahead of its door, more than 1 block",
                        RampEdit(seam, piece.Value, other.Value), seam.A, seam.B);
            }
        }
    }

    // WL11 — a wool room's approach steps two or more blocks and nothing bridges it. SP8's reading, asked of
    // a room that has no facing: a room has no front, so every entry interface is a door and all of them are
    // measured. The player who crosses one is the attacker — a team is kept out of its own wool — so the step
    // is met at the end of the run that decides the map, as a wall to build up or a drop with no way back.
    private static IEnumerable<Finding> LintBz121(PlanModel plan, ContactGraph d)
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
                yield return Lint(LayoutRules.WoolEntryStep,
                    $"room piece '{roomId}' of a wool {arrival} {delta} blocks to piece "
                    + $"'{(seam.A == roomId ? seam.B : seam.A)}' where they share an edge, more than 1 block", seam.A, seam.B);
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
                yield return Lint(LayoutRules.SpawnDoorGround,
                    $"spawn '{s.Id}' on piece '{s.Piece}' has {ahead} blocks of ground ahead of its door, "
                    + $"less than {minAhead} blocks", s.Piece);
        }
    }

    /// <summary>The lane mouth an approach wall bars, in blocks (<c>ST8</c>): narrower is a doorway, wider bars
    /// a room rather than a lane.</summary>
    public const int WallMouthMinBlocks = 10, WallMouthMaxBlocks = 20;

    /// <summary>How far an approach wall stands in front of the wool room's entrance, in blocks (<c>ST8</c>):
    /// about 15 is the seat.</summary>
    public const int WallStandoffMinBlocks = 10, WallStandoffMaxBlocks = 20;

    // ST8 — an approach wall's geometry: the interface it bars is a lane mouth (WallMouth*), and it stands in
    // front of the wool room's entrance (WallStandoff*). The full-span clause needs no check: the compiler
    // builds the wall across the whole interface.
    private static IEnumerable<Finding> LintSt8(PlanModel plan, ContactGraph d)
    {
        var seams = PieceInterfaces.Seams(d);
        foreach (var wall in seams.Where(seam => seam.Wall))
        {
            // a wall pair that includes the wool room itself is PL13's refusal — piling this lint on top of
            // that error would say one fault twice in two vocabularies
            if (wall.RoleA == PlanRoles.WoolRoom || wall.RoleB == PlanRoles.WoolRoom) continue;

            if (wall.Length < WallMouthMinBlocks || wall.Length > WallMouthMaxBlocks)
                yield return Lint(LayoutRules.ApproachWallEdgeLength,
                    $"approach wall between pieces '{wall.A}' and '{wall.B}' bars a shared edge {wall.Length} "
                    + "blocks long, " + Beyond(wall.Length, WallMouthMinBlocks, WallMouthMaxBlocks), wall.A, wall.B);

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
            if (nearest is { } gap && (gap < WallStandoffMinBlocks || gap > WallStandoffMaxBlocks))
                yield return Lint(LayoutRules.ApproachWallStandoff,
                    $"approach wall between pieces '{wall.A}' and '{wall.B}' stands {gap} blocks from the wool "
                    + "room's entrance, " + Beyond(gap, WallStandoffMinBlocks, WallStandoffMaxBlocks), wall.A, wall.B);
        }
    }

    /// <summary>The caps a role piece is read against, from the project that resolves the frame. Stated once
    /// there: this lint reads a plan and <c>WX13</c> reads the room the intent actually builds, and two
    /// numbers for one cap is two rules.</summary>
    public const int FootprintCap = RoomFrames.FootprintCap;

    /// <inheritdoc cref="FootprintCap"/>
    public const int RegionCapAcross = RoomFrames.RegionCapAcross, RegionCapAlong = RoomFrames.RegionCapAlong;

    // ST9 — the building a role piece raises is at most 20×20 blocks. The footprint is what a player walks
    // into, so the cap is on the rectangle the shell actually stands on: the one the placement states, or the
    // one WX1 defaults from the piece where it states none.
    private static IEnumerable<Finding> LintSt9(PlanModel plan, ContactGraph d)
    {
        foreach (var (kind, pieceId, at, footprint, doors) in RoleRooms(plan, d))
        {
            if (ResolveFrame(plan, d, kind, pieceId, RoleOf(kind), at, footprint, doors, out _)
                is not { Frame: var frame }) continue;
            if (frame.Width <= FootprintCap && frame.Depth <= FootprintCap) continue;
            yield return Lint(LayoutRules.BuildingFootprint,
                $"{kind} room on piece '{pieceId}' is {frame.Width} by {frame.Depth} blocks, more than "
                + $"{FootprintCap} by {FootprintCap} blocks", pieceId);
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
            yield return Lint(LayoutRules.RoomRegionSize,
                $"room piece '{piece.Id}' is {piece.Rect.Width} by {piece.Rect.Depth} blocks, more than "
                + $"{RegionCapAcross} by {RegionCapAlong} blocks", piece.Id);
        }
    }

    // The role-piece rooms a plan states, as the arguments ResolveFrame takes.
    private static IEnumerable<(string Kind, string PieceId, double[] At, double[]? Footprint,
        IReadOnlyList<RoomEdge> Doors)> RoleRooms(PlanModel plan, ContactGraph d)
    {
        foreach (var w in plan.Placements.Wools) yield return ("wool", w.Piece, w.At, w.Footprint, []);
        foreach (var s in plan.Placements.Spawns)
            yield return ("spawn", s.Piece, s.At, s.Footprint, PieceDoors.ForSpawn(d, s.Piece, s.Facing));
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
                yield return Lint(LayoutRules.StitchedZones,
                    $"build regions {Quoted(region.ZoneIds)} touch and together fill one rectangle",
                    [.. region.ZoneIds]);
        }
    }

    /// <summary>The first piece holding land one block beyond either end of a wall
    /// <paramref name="footprint"/>, across both of its faces — the ground a player rounds the wall on — or
    /// null where both ends stand over void. The footprint is max-exclusive, as a piece's rect is.</summary>
    internal static DerivedPiece? FlankOf(
        IEnumerable<DerivedPiece> pieces, (int MinX, int MinZ, int MaxX, int MaxZ) footprint)
    {
        var (minX, minZ, maxX, maxZ) = footprint;
        var alongX = maxX - minX > maxZ - minZ;
        foreach (var piece in pieces)
        {
            var r = piece.Rect;
            var flanks = alongX
                ? r.MinZ < maxZ && r.MaxZ > minZ && (Covers(r.MinX, r.MaxX, minX - 1) || Covers(r.MinX, r.MaxX, maxX))
                : r.MinX < maxX && r.MaxX > minX && (Covers(r.MinZ, r.MaxZ, minZ - 1) || Covers(r.MinZ, r.MaxZ, maxZ));
            if (flanks) return piece;
        }
        return null;

        static bool Covers(int lo, int hi, int at) => lo <= at && at < hi;
    }

    /// <summary>The narrowest frontline a crossing may be, in blocks (the author's number). Under it a front
    /// reads as a funnel whatever share of its face it takes, which is the half <c>FR8</c>'s share cannot
    /// see.</summary>
    public const int MinFrontlineBlocks = 15;

    /// <summary>How wide a negative space between a wool room or a spawn and the frontline — or another goal —
    /// must be, in blocks, the floor <c>WL12</c> measures against. A gap is crossed by jumping long before it is
    /// crossed by building, and one the attack reaches from the front deletes the approach the board was drawn
    /// around.</summary>
    public const int MinGoalSpaceBlocks = 16;

    /// <summary>The same floor between a wool room or a spawn and the team's own ground away from the front —
    /// its hub, an approach — in blocks. The author's number: 12 is on the low end and not a fault, since
    /// the side that jumps it is the one already standing there.</summary>
    public const int MinGoalHomeSpaceBlocks = 12;

    /// <summary>The same floor for a space touching neither, in blocks: a hole in a team's own ground is
    /// crossed on purpose and may be tighter than one beside a goal.</summary>
    public const int MinPlainSpaceBlocks = 12;

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
                yield return Lint(LayoutRules.FrontlineShare,
                    $"piece '{face.Piece}' has front line along {face.FrontlineBlocks} of the {face.ExposedBlocks} "
                    + $"blocks of its {face.Side} side facing void, less than a third",
                    face.Piece);

            // FR9 is the absolute floor FR8's share cannot see: a crossing narrow in blocks reads as a
            // funnel however much of its face it takes, and a 10-block front on a 10-block face passes the
            // share at 1.00. Only a face that has a frontline at all is asked.
            if (face.FrontlineBlocks > 0 && face.FrontlineBlocks < MinFrontlineBlocks)
                yield return Lint(LayoutRules.MinimumFrontline,
                    $"piece '{face.Piece}' has {face.FrontlineBlocks} blocks of front line along its {face.Side} "
                    + $"side, less than {MinFrontlineBlocks} blocks",
                    face.Piece);
        }

        // WL12 — how narrow a gap beside a goal is. The space reader measures every straight run the terrain
        // closes at both ends, which is the line a player jumps, and names the piece at each end. A run any
        // build zone covers is not asked: building over it is what the zone states. Three floors, all in
        // blocks so they hold at any grid scale — a goal across from the frontline or another goal, a goal
        // across from its team's own ground, and the narrowest crossing of a hole touching no goal.
        var goalPieces = new HashSet<string>(
            plan.Pieces.Where(piece => piece.Role is PlanRoles.WoolRoom or PlanRoles.Spawn).Select(piece => piece.Id),
            StringComparer.Ordinal);
        var frontPieces = PieceInterfaces.Frontages(board).Where(face => face.FrontlineBlocks > 0)
            .Select(face => face.Piece).ToHashSet(StringComparer.Ordinal);
        var reported = new HashSet<(string, string, int)>();
        foreach (var space in board.Spaces)
        {
            var hole = space.Kind == NegativeSpaceKinds.Hole;
            var narrowest = space.Crossings.Count > 0 ? space.Crossings.Min(run => run.Cells) : 0;
            foreach (var run in space.Crossings)
            {
                var beside = new[] { run.From, run.To }.Where(goalPieces.Contains).Distinct().ToList();
                var across = new[] { run.From, run.To }.Where(end => !goalPieces.Contains(end)).ToList();
                var exposed = beside.Count > 1 || across.Any(end => end.Length == 0 || frontPieces.Contains(end));
                var floor = beside.Count > 0 ? (exposed ? MinGoalSpaceBlocks : MinGoalHomeSpaceBlocks)
                    : hole && run.Cells == narrowest ? MinPlainSpaceBlocks
                    : 0;
                if (floor == 0) continue;
                var crossing = run.Cells * board.Cell;
                if (crossing >= floor) continue;
                // a run any build zone reaches is a crossing the board states, and building over it is the point
                if (Enumerable.Range(0, run.Cells).Any(step => board.Build.Contains(
                        (run.X + (run.AlongX ? step : 0), run.Z + (run.AlongX ? 0 : step))))) continue;
                var pair = string.CompareOrdinal(run.From, run.To) <= 0 ? (run.From, run.To) : (run.To, run.From);
                if (!reported.Add((pair.Item1, pair.Item2, crossing))) continue;

                yield return Lint(LayoutRules.RoomGapWidth,
                    $"pieces {Named(run.From)} and {Named(run.To)} have a {(beside.Count > 0 ? "gap" : space.Kind)} "
                    + $"{crossing} blocks across between them at cell ({run.X}, {run.Z}), less than {floor} blocks",
                    [.. new[] { run.From, run.To }.Where(name => name.Length > 0).Distinct()]);
            }
        }

        // CT12 judges the CTW strait: the direct crossing between the two team islands of a two-team wool
        // board. Chains over stepping stones are not straits (each hop is G5's), and a mid island between
        // the teams makes the crossing indirect by construction.
        if (plan.Placements.Wools.Count > 0 && Symmetry.Order(plan.Globals.Symmetry) == 2)
            foreach (var gap in PieceInterfaces.IslandGaps(board))
            {
                if (!gap.Direct || gap.RoleA != "team" || gap.RoleB != "team") continue;
                if (gap.Blocks is >= 15 and <= 40) continue;
                yield return Lint(LayoutRules.TeamGapWidth,
                    $"team sides of pieces {Quoted(gap.PiecesA)} and of pieces {Quoted(gap.PiecesB)} have a gap of "
                    + $"{gap.Blocks} blocks between them, " + Beyond(gap.Blocks, 15, 40),
                    [.. gap.PiecesA.Concat(gap.PiecesB)]);
            }
    }

    /// <summary>BZ9's void half: a stretch of a build zone reaching past everything it docks. A crossing is
    /// bounded by the ground it connects rather than by a width — it may cover only part of a face, it may be
    /// wider than any one face where it docks the mirrored images at their corners (the shifted
    /// <c>rot_180</c> form), and the span between two docked ends is the crossing itself and touches nothing
    /// by definition. What it may not do is <b>overhang</b>: carry a stretch beyond the last ground it meets,
    /// which connects nothing and reads as no man's land a player walks into and stops.
    ///
    /// <para>So the measure is not whether a line of the zone meets ground but whether it lies outside the
    /// span of the lines that do: between two docked ends is the gap the zone exists to carry, and a zone
    /// aimed across a hop it does not reach is <c>G5</c>'s question rather than this one. Measured across the
    /// bridging axis — the one the zone's own contacts lie on — since that is where growing out means growing
    /// sideways into nothing. Read over the <b>fanned</b> board, because half of what a zone docks is often an
    /// image of what the plan states, and over the zone cells as connected regions, so an L- or T-shaped union
    /// is judged as the one crossing it is.</para></summary>
    private static IEnumerable<Finding> LintZoneReach(PlanModel plan, ContactGraph d)
    {
        BoardStructure board;
        try { board = BoardDeriver.Derive(plan); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException
                                       or NullReferenceException or IndexOutOfRangeException or KeyNotFoundException)
        { yield break; }

        var zoneOf = new Dictionary<(int X, int Z), string>();
        foreach (var zone in plan.BuildZones)
            for (var cx = zone.Rect.X; cx < zone.Rect.X + zone.Rect.Width; cx++)
                for (var cz = zone.Rect.Z; cz < zone.Rect.Z + zone.Rect.Height; cz++)
                    zoneOf.TryAdd((cx, cz), zone.Id);

        foreach (var region in Regions(zoneOf.Keys))
        {
            // A zone docked left and right bridges in x, so what may overhang is its extent in z, and the
            // other way about. With no contact at all either answers the same thing — the whole region stands
            // beyond ground — and x is taken so the report has an axis to name.
            var (alongX, alongZ) = Contacts(region, board.Filled);
            var acrossX = alongZ > alongX;
            var overhang = Overhang(region, board.Filled, acrossX);
            if (overhang.Count == 0) continue;
            var axis = acrossX ? "x" : "z";

            var zones = region.Select(cell => zoneOf[cell]).Distinct().OrderBy(id => id, StringComparer.Ordinal).ToList();
            yield return Lint(LayoutRules.ZoneOverhang,
                $"build region{(zones.Count > 1 ? "s" : "")} {Quoted(zones)} {(zones.Count > 1 ? "run" : "runs")} "
                + $"{overhang.Count * plan.Globals.Cell} blocks past the last piece {(zones.Count > 1 ? "they touch" : "it touches")}, "
                + $"at {axis} {Runs(overhang, plan.Globals.Cell)}",
                [.. zones]);
        }
    }

    /// <summary>How much of a region's contact with the board lies on each axis — the read that says which way
    /// the crossing bridges, and so which way it can overhang.</summary>
    private static (int AlongX, int AlongZ) Contacts(
        IEnumerable<(int X, int Z)> region, IReadOnlyDictionary<(int, int), (string PieceId, int K)> filled)
    {
        int alongX = 0, alongZ = 0;
        foreach (var (x, z) in region)
        {
            if (filled.ContainsKey((x - 1, z))) alongX++;
            if (filled.ContainsKey((x + 1, z))) alongX++;
            if (filled.ContainsKey((x, z - 1))) alongZ++;
            if (filled.ContainsKey((x, z + 1))) alongZ++;
        }
        return (alongX, alongZ);
    }

    /// <summary>The region's lines that stand outside the span of the lines meeting ground, along one axis.
    /// A line between two that meet ground is the crossing itself; one beyond them all is the overhang.</summary>
    private static List<int> Overhang(
        IReadOnlyList<(int X, int Z)> region,
        IReadOnlyDictionary<(int, int), (string PieceId, int K)> filled, bool alongX)
    {
        var lines = region.GroupBy(cell => alongX ? cell.X : cell.Z)
                          .ToDictionary(line => line.Key, line => line.Any(cell => Touches(cell, filled)));
        var docked = lines.Where(line => line.Value).Select(line => line.Key).ToList();
        // Nothing docked at all: the whole region stands beyond ground, which is the same fault at its limit.
        int from = docked.Count > 0 ? docked.Min() : int.MaxValue, to = docked.Count > 0 ? docked.Max() : int.MinValue;
        return [.. lines.Keys.Where(line => line < from || line > to).OrderBy(line => line)];
    }

    /// <summary>The zone cells as connected regions, four-connected — one crossing per region, so an L or a T
    /// drawn as several rectangles is judged as the shape it makes rather than rectangle by rectangle.</summary>
    private static List<List<(int X, int Z)>> Regions(IEnumerable<(int X, int Z)> cells)
    {
        var left = new HashSet<(int X, int Z)>(cells);
        var regions = new List<List<(int X, int Z)>>();
        while (left.Count > 0)
        {
            var seed = left.First();
            left.Remove(seed);
            var region = new List<(int X, int Z)> { seed };
            var queue = new Queue<(int X, int Z)>();
            queue.Enqueue(seed);
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                foreach (var side in Sides(cell))
                    if (left.Remove(side)) { region.Add(side); queue.Enqueue(side); }
            }
            regions.Add(region);
        }
        return regions;
    }

    /// <summary>Whether a cell has ground on any side of it, or under it — a zone lapping a piece is docked as
    /// surely as one flush against it (FR1+FR2: overlapping terrain is allowed).</summary>
    private static bool Touches((int X, int Z) cell, IReadOnlyDictionary<(int, int), (string PieceId, int K)> filled)
        => filled.ContainsKey(cell) || Sides(cell).Any(filled.ContainsKey);

    private static IEnumerable<(int X, int Z)> Sides((int X, int Z) cell)
    {
        yield return (cell.X - 1, cell.Z);
        yield return (cell.X + 1, cell.Z);
        yield return (cell.X, cell.Z - 1);
        yield return (cell.X, cell.Z + 1);
    }

    /// <summary>Consecutive line indices as block extents — <c>-45..-20 and 15..45</c> — so a reader flies to
    /// the stretch rather than counting cells off a rectangle.</summary>
    private static string Runs(IReadOnlyList<int> lines, int cell)
    {
        var runs = new List<string>();
        for (var i = 0; i < lines.Count; i++)
        {
            var from = lines[i];
            while (i + 1 < lines.Count && lines[i + 1] == lines[i] + 1) i++;
            runs.Add($"{from * cell}..{(lines[i] + 1) * cell}");
        }
        return runs.Count == 1 ? runs[0] : string.Join(" and ", runs);
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
    /// only crowd the drawer), or, for a destroyable/core, a deliberate absolute placement: the plan has
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
                PieceDoors.ForSpawn(d, s.Piece, s.Facing), out _) is { } room)
                yield return ("spawn", s.Piece, Frame(room));
        foreach (var w in plan.Placements.Wools)
            if (ResolveFrame(plan, d, "wool", w.Piece, PlanRoles.WoolRoom, w.At, w.Footprint, [], out _)
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
