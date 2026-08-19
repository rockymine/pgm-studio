using PgmStudio.Data.Map;
using PgmStudio.Domain;
using System.Text.Json;
using FastEndpoints;
using PgmStudio.Api.Services;
using PgmStudio.Contracts;
using PgmStudio.Export;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Evaluate;
using PgmStudio.Pgm.Derive;
using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Api.Endpoints;

/// <summary>
/// POST /api/plan/inspect — the live derived-geometry feed for the plan editor's canvas overlays. The request
/// body is a plan wire document (<c>*.plan.json</c>); the response carries everything already resolved to block
/// coordinates so the canvas draws it directly: <c>interfaces</c> (land/narrow/corner contacts as segments,
/// each with the surface <c>delta</c> across it), <c>gapLinks</c> (zone-spanning connectors with the hop
/// distance), <c>frontline</c> (piece edges facing a zone), the <see cref="PieceInterfaces"/> aggregations —
/// <c>frontages</c> (per piece side, the frontline share of the exposed run), <c>frontlineRuns</c> (widths in
/// blocks) and <c>islandGaps</c> (each bridged pair's strait) — and <c>structures</c> (the boxes the world
/// build will stamp — see <see cref="PlanStructurePreview"/> —
/// which the iso view draws). Rule findings/violations are the <c>/plan/evaluate</c> endpoint's job (the
/// evaluator is the single source). This is the derivation the Blazor client can't run itself, and the feed for
/// anything needing live geometry mid-edit: unlike <c>/plan/compile</c> it never withholds its answer over
/// structural errors. A malformed body is answered 400, never 500.
/// </summary>
public sealed class PlanInspectEndpoint : EndpointWithoutRequest
{
    public override void Configure() { Post("/plan/inspect"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var body = await RawBody.ReadAsync(HttpContext, ct);

        var plan = PlanModel.Stated(body);
        if (plan is null)
        {
            await Refusals.UnreadableAsync(HttpContext, "malformed plan JSON",
                "the body is not a plan document: it is empty, or it is not JSON the plan reader accepts", ct);
            return;
        }

        ContactGraph d;
        try
        {
            d = ContactGraph.Build(plan);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NullReferenceException or IndexOutOfRangeException)
        {
            await Refusals.UnreadableAsync(HttpContext, "invalid plan structure", ex.Message, ct);
            return;
        }

        // the surface step across each pair, so the interface feed carries what the seam read knows
        var deltas = d.Contacts.ToDictionary(c => (c.A, c.B), c => c.SurfaceDelta);
        var interfaces = d.InterfaceSegments.Select(s => new
        {
            a = s.A, b = s.B, kind = s.Kind.ToString().ToLowerInvariant(),
            x1 = s.X1, z1 = s.Z1, x2 = s.X2, z2 = s.Z2, length = s.Length,
            delta = deltas.GetValueOrDefault((s.A, s.B)),
            woolRoom = s.WoolRoom, wall = s.Wall, wallChest = s.WallChestPiece,
        });

        var gapLinks = d.GapLinks.Select(g =>
        {
            var (x1, z1, x2, z2) = ContactGraph.NearestSegment(d.Piece(g.A)!.Value.Rect, d.Piece(g.B)!.Value.Rect);
            return new { a = g.A, b = g.B, zone = g.Zone, hop = g.Hop, x1, z1, x2, z2 };
        });

        var frontline = d.FrontlineEdges.Select(f => new { piece = f.Piece, x1 = f.X1, z1 = f.Z1, x2 = f.X2, z2 = f.Z2 });

        // The per-interface reads over the fanned raster board: each authored piece side's frontline share,
        // the frontline runs with their widths, and the straits between bridged islands — the aggregations
        // the interface lints (FR8, CT12) quantify over, served raw so a caller sees the numbers behind
        // them. A board the deriver cannot read degrades to empty reads, same as structures below.
        object frontages, frontlineRuns, islandGaps;
        try
        {
            var board = BoardDeriver.Derive(plan);
            frontages = PieceInterfaces.Frontages(board).Select(f => new
            {
                piece = f.Piece, side = f.Side,
                exposedBlocks = f.ExposedBlocks, frontlineBlocks = f.FrontlineBlocks, frontlineShare = f.FrontlineShare,
            }).ToList();
            frontlineRuns = PieceInterfaces.Runs(board).Select(r => new
            {
                team = r.Team, widthBlocks = r.WidthBlocks, profile = r.Profile,
                x1 = r.X1, z1 = r.Z1, x2 = r.X2, z2 = r.Z2,
            }).ToList();
            islandGaps = PieceInterfaces.IslandGaps(board).Select(g => new
            {
                piecesA = g.PiecesA, piecesB = g.PiecesB, roleA = g.RoleA, roleB = g.RoleB, blocks = g.Blocks,
            }).ToList();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NullReferenceException
                                       or IndexOutOfRangeException or KeyNotFoundException)
        {
            frontages = Array.Empty<object>(); frontlineRuns = Array.Empty<object>(); islandGaps = Array.Empty<object>();
        }

        // The boxes the world build will stamp (the iso view draws them). A plan mid-edit is routinely
        // incomplete, so a compile failure degrades to no structures rather than failing the whole feed —
        // the derived-geometry overlays above stay live either way.
        IReadOnlyList<StructureBox> structures;
        try { structures = PlanStructurePreview.Build(plan); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NullReferenceException or IndexOutOfRangeException)
        {
            structures = [];
        }

        // The destroy-goal walks: own-spawn, enemy-spawn and the ratio, in blocks over the fanned closure.
        // A measurement, not a rule — the band a rule would hold the ratio to is the author's to state.
        var goalDistances = GoalDistances.Read(plan).Select(walk => new
        {
            id = walk.Id, kind = walk.Kind,
            ownSpawnBlocks = walk.OwnSpawnBlocks, enemySpawnBlocks = walk.EnemySpawnBlocks, ratio = walk.Ratio,
        });

        await Send.OkAsync(new { interfaces, gapLinks, frontline, frontages, frontlineRuns, islandGaps, structures, goalDistances }, ct);
    }
}

/// <summary>POST /api/plan/columns — the world a plan builds, as per-column runs, which is what the plan
/// tool's 3-D preview draws (docs/tools/plan.md). The body is the plan document itself.
///
/// <para>A compiled plan carries a full intent, so unlike the sketch side this shows the wool cages, spawn
/// cubes, monuments and build region as well as the ground: the preview is the compiled map, not a schematic
/// of the pieces. The compile is <see cref="PlanWorld"/>'s, the same one the painted render goes through, so
/// the two pictures cannot disagree about what the plan means.</para>
///
/// <para>It does not gate. <c>/plan/compile</c> is where a plan is refused; a preview of an incoherent plan is
/// still a useful thing to look at, and a picture that vanishes when a validator fires teaches nothing about
/// why. 400 only when the document cannot be read or built at all.</para>
///
/// <para><b>What did not land comes back with what did</b>, under <c>warnings</c>: every prop the dressing
/// pass declined, as a <c>DR-*</c> finding naming the rule, the cell and the prop. Complaints on a success —
/// the world was built and some of what was authored is not standing in it.</para></summary>
public sealed class PlanColumnsEndpoint : EndpointWithoutRequest
{
    public override void Configure() { Post("/plan/columns"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var body = await RawBody.ReadAsync(HttpContext, ct);

        Dictionary<string, object?> payload;
        try
        {
            if (PlanWorld.Compile(body) is not { } compiled)
            {
                await Refusals.UnreadableAsync(HttpContext, "malformed plan JSON",
                    "the body is not a plan document: it is empty, or it is not JSON the plan reader accepts", ct);
                return;
            }
            var built = SketchWorldBuilder.Build(compiled.LayoutJson, compiled.Intent);
            payload = WorldColumnPayload.Of(built.World);
            // The compiled layout's own complaints ride with the dressing's: a plan that compiles to a board
            // naming something the studio does not have is still a plan whose picture is missing it. This
            // route gates nothing, so it is the one that hands them over rather than StopAsync.
            Complaints.Add(HttpContext, SketchLayoutCheck.Check(compiled.LayoutJson).Complaints);
            Complaints.Add(HttpContext, built.Declines);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NullReferenceException
                                      or IndexOutOfRangeException or JsonException)
        {
            await Refusals.UnreadableAsync(HttpContext, "invalid plan structure", ex.Message, ct);
            return;
        }

        await Send.OkAsync(payload, ct);
    }
}

/// <summary>
/// POST /api/plan/compile — compile a plan wire document one-way into the pair the draft pipeline consumes:
/// <c>{ layout: &lt;SketchLayout&gt;, intent: &lt;MapIntent&gt; }</c>. Structural validator <b>errors</b> block the
/// compile (422, carrying the error findings); lint alone never blocks. A malformed body is answered 400, never
/// 500. The two sub-objects are serialized with the exact options the downstream <c>PUT /map/{slug}/sketch</c>
/// and <c>PUT /map/{slug}/intent</c> endpoints read back, so the editor's walk-test loop can post them verbatim.
/// </summary>
public sealed class PlanCompileEndpoint : EndpointWithoutRequest
{
    public override void Configure() { Post("/plan/compile"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var body = await RawBody.ReadAsync(HttpContext, ct);

        var plan = PlanModel.Stated(body);
        if (plan is null)
        {
            await Refusals.UnreadableAsync(HttpContext, "malformed plan JSON",
                "the body is not a plan document: it is empty, or it is not JSON the plan reader accepts", ct);
            return;
        }
        // The one-way gate is where a plan stops being editable text, so a field the compiler had nowhere to
        // read is said here rather than discovered as ground the built map does not have.
        Complaints.Unread(HttpContext, body, plan);

        SketchLayout layout;
        MapIntent intent;
        Findings completeness;
        try
        {
            // Two questions, both asked here because this is the one-way gate: is the plan coherent
            // (the structural validator), and does it carry what a map cannot exist without (completeness).
            completeness = PlanValidator.Completeness(plan);
            var checked_ = PlanValidator.Check(plan).And(completeness);
            if (checked_.Refuses)
            {
                await Send.ResponseAsync(Refusals.Of("plan not compilable", checked_.Refusals), 422, ct);
                return;
            }
            (layout, intent) = PlanCompiler.Compile(plan);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NullReferenceException or IndexOutOfRangeException)
        {
            await Refusals.UnreadableAsync(HttpContext, "invalid plan structure", ex.Message, ct);
            return;
        }

        // Pre-fill team ownership once, now, on the canonical island decomposition (G157) — a spawn's team
        // owns its island, else a wool's owner, else neutral. The compiled intent carries it downstream, so
        // configure opens pre-assigned and the export paints without re-deriving.
        var ownFootprint = SketchRasterizer.RasterizeColumns(JsonSerializer.Serialize(layout, SketchLayout.Json)).Select(c => (c.X, c.Z));
        foreach (var (islandId, team) in TeamTerritory.Assign(ownFootprint, intent)) intent.IslandTeams[islandId] = team;

        // Serialize each half with its own consumer's options (snake_case shape fields for the sketch blob;
        // Web camelCase for the intent) so the editor can post the raw sub-objects straight to the pipeline.
        var layoutEl = JsonSerializer.SerializeToElement(layout, SketchLayout.Json);
        var intentEl = JsonSerializer.SerializeToElement(intent, MapArtifactStore.Json);

        // Completeness complaints that did not block (today: no objective). Carried on the success response so
        // a compile that produced a playable-but-goalless map still says so rather than passing in silence.
        // The structural gate's own complaints are the lint feed, which /plan/evaluate answers under `lint`
        // and which an author reads while writing rather than at the one-way gate — so this hands over the
        // completeness half only, and the two surfaces do not report the same list twice.
        Complaints.Add(HttpContext, completeness.Complaints);
        await Send.OkAsync(new { layout = layoutEl, intent = intentEl }, ct);
    }
}

/// <summary>
/// POST /api/plan/evaluate — the plan editor's live rule-evaluator score + lint feed (the critic that scores a
/// <c>*.plan.json</c>). The request body is a plan wire document; the response is an <see cref="EvaluationDto"/>:
/// the summed <c>score</c> (lower is better, 0 perfect), a <c>valid</c> flag (no hard term fired), and every
/// fired term ordered hard-first — each carrying its <c>layout-rules.md</c> id, the pieces/zones it indicts, and
/// the cell-space <see cref="EvidenceDto"/> the canvas overlay paints. This is where soft "feel" terms and the
/// gate terms retired from the structural validator (e.g. WL2 spawn↔wool distance) surface in the editor, so it
/// complements — not replaces — <c>/plan/inspect</c>'s derived-structure geometry. A malformed body is answered
/// 400, never 500.
/// </summary>
public sealed class PlanEvaluateEndpoint : EndpointWithoutRequest
{
    public override void Configure() { Post("/plan/evaluate"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var body = await RawBody.ReadAsync(HttpContext, ct);

        var plan = PlanModel.Stated(body);
        if (plan is null)
        {
            await Refusals.UnreadableAsync(HttpContext, "malformed plan JSON",
                "the body is not a plan document: it is empty, or it is not JSON the plan reader accepts", ct);
            return;
        }

        // A plan with nothing in it is a valid document, not a malformed one — the evaluator has no geometry
        // to score, so it is answered rather than refused: a 400 here made the editor's score panel go blank on
        // every fresh plan with no reason given. But an empty evaluation is `score 0, valid: true`, which is
        // the shape of a perfect plan, and this endpoint was saying it about the emptiest document there is
        // while /plan/compile refused the same one outright. What it answers instead is the validator's own
        // finding, cited rather than restated, so the advice surface and the gate agree (B140).
        //
        // Completeness, not Check, and only in this branch. Completeness deliberately sits outside the
        // continuous validation the editor runs — a plan under construction is legitimately missing a spawn
        // and an objective, and saying so on every keystroke is what the split exists to avoid. PL1 is the one
        // finding that is not about being unfinished: it says the document is empty, and Completeness reports
        // it alone, returning before it asks anything else.
        if (!plan.Pieces.Any(pc => PlanRoles.IsGenerating(pc.Role)))
        {
            await Send.OkAsync(Structural(PlanValidator.Completeness(plan)), ct);
            return;
        }

        Evaluation eval;
        IReadOnlyList<FindingDto> lint;
        try
        {
            var ctx = EvalContext.Build(plan, SeedEnvelopes.Default);
            eval = LayoutEvaluator.Evaluate(ctx, EvaluationProfile.Default);
            // The validator's complaints ride along: computed by the same Check the context already ran, and
            // this response is the one surface the authoring loop actually reads them from.
            lint = Refusals.Dtos(ctx.Findings.Complaints);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NullReferenceException or IndexOutOfRangeException)
        {
            await Refusals.UnreadableAsync(HttpContext, "invalid plan structure", ex.Message, ct);
            return;
        }

        await Send.OkAsync(ToDto(eval) with { Lint = lint }, ct);
    }

    /// <summary>An evaluation carrying the <b>structural</b> validator's refusals rather than the evaluator's
    /// terms — what a plan with no geometry to score is answered with. The findings are `PlanValidator`'s, so
    /// the sentence an author reads here is the one <c>/plan/compile</c> gives them; nothing is re-derived, and
    /// a rule id that moves moves in one place.</summary>
    internal static EvaluationDto Structural(Findings findings) => new(0, !findings.Refuses,
    [
        .. findings.Refusals.Select(finding =>
            new ViolationDto(finding.Rule, "hard", 0, Refusals.Dto(finding), [])),
    ]);

    /// <summary>Map the derived <see cref="Evaluation"/> onto the wire DTO: every fired term (hard-first, the
    /// registration order the evaluation already carries) with its kind, soft distance and flattened evidence.</summary>
    internal static EvaluationDto ToDto(Evaluation eval)
    {
        var violations = eval.Terms
            .Where(t => t.Violation is not null)
            .Select(t => new ViolationDto(
                t.Violation!.TermId, t.Kind == TermKind.Hard ? "hard" : "soft", t.Distance,
                Refusals.Dto(t.Violation.Finding),
                (t.Violation.Evidence ?? []).Select(MapEvidence).ToList()))
            .ToList();
        return new EvaluationDto(eval.Score, eval.IsValid, violations);
    }

    private static EvidenceDto MapEvidence(Evidence e) => e switch
    {
        EvidenceRect r => new("rect", r.Tag, Rect: r.Rect.ToArray()),
        EvidenceSegment s => new("segment", s.Tag, X1: s.X1, Z1: s.Z1, X2: s.X2, Z2: s.Z2),
        EvidenceMarker m => new("marker", m.Tag, X: m.X, Z: m.Z),
        EvidenceMeasure m => new("measure", m.Tag, X1: m.X1, Z1: m.Z1, X2: m.X2, Z2: m.Z2, Label: m.Label),
        _ => new("unknown", e.Tag),
    };
}

/// <summary>
/// POST /api/plan/feasibility — the plan editor's <b>producibility</b> read: could the composer have produced this
/// plan? The question <see cref="PlanEvaluateEndpoint"/> does not ask, and the two genuinely diverge — a plan can
/// score 0 with no rule fired and still be outside everything the emitters can build.
///
/// <para>The request body is a plan wire document; the response is a <see cref="FeasibilityDto"/>: the per-box
/// reads (each naming the parameter tuple that reproduces it, or the nearest candidate and why it misses) plus the
/// unit-level findings that belong to the arrangement. Findings cite a rule id or the id of the task that would
/// unblock them, so the report doubles as a live map of composer gaps. Boxes come from the plan's authored
/// <c>boxes</c> annotation, so a plan without them reads empty rather than erroring. A malformed body is answered
/// 400, never 500.</para>
/// </summary>
public sealed class PlanFeasibilityEndpoint : EndpointWithoutRequest
{
    public override void Configure() { Post("/plan/feasibility"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var body = await RawBody.ReadAsync(HttpContext, ct);

        var plan = PlanModel.Stated(body);
        if (plan is null)
        {
            await Refusals.UnreadableAsync(HttpContext, "malformed plan JSON",
                "the body is not a plan document: it is empty, or it is not JSON the plan reader accepts", ct);
            return;
        }

        // The same emptiest-document case /plan/evaluate answers above, and for the same reason: with no
        // geometry there are no boxes, every box-level question is vacuously satisfied, and `producible: true`
        // is what came back. A plan the structural validator refuses is not a plan the composer could have
        // produced, and it says so in the validator's own words (B140).
        if (!plan.Pieces.Any(piece => PlanRoles.IsGenerating(piece.Role))
            && PlanValidator.Completeness(plan) is { Refuses: true } structural)
        {
            await Send.OkAsync(new FeasibilityDto(false, [], Refusals.Dtos(structural.Refusals)), ct);
            return;
        }

        PlanProducibility read;
        try
        {
            read = Producibility.ReadPlan(plan);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NullReferenceException or IndexOutOfRangeException)
        {
            await Refusals.UnreadableAsync(HttpContext, "invalid plan structure", ex.Message, ct);
            return;
        }

        await Send.OkAsync(ToDto(read), ct);
    }

    /// <summary>Map the derived read onto the wire DTO — a shape-for-shape projection; the service owns every
    /// judgement, this owns only the serialization and the one severity change the wire needs. A producibility
    /// finding is a refusal of producibility inside the read, which is how the read asks its own question; it
    /// reaches a caller as a complaint, because nothing here declines to build the plan.</summary>
    internal static FeasibilityDto ToDto(PlanProducibility read) => new(
        read.IsProducible,
        read.Boxes.Select(b => new BoxFeasibilityDto(
            b.BoxId, b.Kind, b.Identity,
            b.Producible?.Label, b.Producible?.Cw,
            b.Nearest is null ? null : new NearestMissDto(
                b.Nearest.Label, b.Nearest.Cw, b.Nearest.DifferingCells,
                [.. b.Nearest.Extra.Select(r => r.ToArray())], [.. b.Nearest.Missing.Select(r => r.ToArray())]),
            Refusals.Dtos(b.Findings.AsComplaints()))).ToList(),
        Refusals.Dtos(read.Unit.AsComplaints()));

}
