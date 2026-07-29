using FastEndpoints;
using PgmStudio.Contracts;
using PgmStudio.Geom;
using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Render;
using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Api.Endpoints;

/// <summary>
/// GET /api/shapes/probe — emit one knob combination and report what the emitter said. This is the half of the
/// catalog that a gallery cannot be: it goes through <see cref="BoxFiller"/>, so the profile check and the
/// <see cref="DockingGate"/> run exactly as they do in composition, and a <b>refusal is a first-class answer</b>
/// rather than an error. <see cref="FillResult"/> already carries every reason as data; this only flattens it
/// for the wire, passing the emitter's own message through rather than rewording it.
/// </summary>
public sealed class ShapeProbeEndpoint : EndpointWithoutRequest
{
    /// <summary>The probe render scale — larger than a catalog card, since one shape has the panel to itself.</summary>
    private const int ProbeScale = 12;

    public override void Configure() { Get("/shapes/probe"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var familyToken = Query<string?>("family", isRequired: false) ?? "i";
        if (!Enum.TryParse<ShapeFamily>(familyToken, ignoreCase: true, out var family) ||
            family == ShapeFamily.Isolated)
        {
            await Send.ResponseAsync(new { error = $"unknown family '{familyToken}'" }, 400, ct);
            return;
        }

        var boxW = Math.Clamp(Query<int?>("w", isRequired: false) ?? 8, 1, 64);
        var boxH = Math.Clamp(Query<int?>("h", isRequired: false) ?? 8, 1, 64);
        var cw = Math.Clamp(Query<int?>("cw", isRequired: false) ?? 2, 1, 8);
        var flip = Query<bool?>("flip", isRequired: false) ?? false;
        var woolAtEnd = Query<bool?>("woolAtEnd", isRequired: false) ?? false;
        var sideTuck = Query<bool?>("sideTuck", isRequired: false) ?? false;
        var attachW = Math.Clamp(Query<int?>("attachW", isRequired: false) ?? 0, 0, 8);
        if (!Enum.TryParse<BoxEdge>(Query<string?>("mouth", isRequired: false) ?? "Top", true, out var mouth))
            mouth = BoxEdge.Top;

        var placement = sideTuck ? RoomPlacement.SideTuck : RoomPlacement.Inline;
        var box = new Box("wool", BoxKind.Wool, new CellRect(0, 0, boxW, boxH), boxW * boxH);
        var result = BoxFiller.Fill(box, mouth, cw, family, flip, "wool-room", placement, woolAtEnd, attachW);

        if (result is not FillResult.Ok ok)
        {
            await Send.ResponseAsync(
                new ShapeProbeResult(null, Flatten(result), 0, []), cancellation: ct);
            return;
        }

        var svg = PlanBoardSvg.Render(WoolBoxEmitter.AsPlan(ok.Approach, "probe"), ProbeScale);
        var slots = ok.Approach.Terrain.Select(p => p.Slot ?? "?")
            .Append(ApproachSlots.Room).ToList();
        await Send.ResponseAsync(
            new ShapeProbeResult(svg, null, BoxFiller.Land(ok.Approach), slots), cancellation: ct);
    }

    /// <summary>Flatten a refusal onto the wire. The detail is the emitter's or the gate's own words — a
    /// reworded guard is a second copy free to disagree with the one that fired.</summary>
    private static ShapeRejectionDto Flatten(FillResult result) => result switch
    {
        FillResult.TooSmall t => new("too-small",
            $"Below {t.Family}'s minimum box of {t.MinW}×{t.MinH} cells at this corridor width.", t.MinW, t.MinH),
        FillResult.NoFamilyFits n => new("not-on-menu",
            $"Not on this box kind's fill menu — it offers {string.Join(", ", n.Menu)}.", null, null),
        FillResult.IllegalDock d => new("illegal-dock",
            $"The docking gate refused a {d.Mouth} mouth for the {d.Family}: {d.Reason}.", null, null),
        FillResult.UnsupportedKnobs u => new("unsupported-knobs", u.Detail, null, null),
        _ => new("refused", "The fill was refused with no stated reason.", null, null),
    };
}

/// <summary>
/// GET /api/shapes/probe/schema — the knob surface the panel drives: every family the emitter builds, its
/// minimum box, which optional knobs it accepts, and whether the production menu admits it. Served rather than
/// restated client-side so a family gaining a knob does not need a matching edit in the page.
/// </summary>
public sealed class ShapeProbeSchemaEndpoint : EndpointWithoutRequest
{
    public override void Configure() { Get("/shapes/probe/schema"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        const int cw = 2;
        var families = Enum.GetValues<ShapeFamily>()
            .Where(f => f != ShapeFamily.Isolated)
            .Select(f =>
            {
                // the DOCK frame (along × depth), not the emitter's canonical frame: the panel fills through a
                // mouth, and a lateral-mouth family (donut, clamp) transposes between the two. Reporting
                // ShapeEmitter.MinBox here would put 10×5 in the chip while a refusal said 5×10.
                var (along, depth) = WoolBoxEmitter.MouthBox(f, cw);
                return new ShapeFamilyDto(
                    f.ToString().ToLowerInvariant(), f.ToString(), along, depth,
                    FillMenu.ProductionFamilies.Contains(f), KnobsOf(f));
            })
            .ToList();

        await Send.ResponseAsync(
            new ShapeProbeSchema(families, Enum.GetNames<BoxEdge>(), 2, 5), cancellation: ct);
    }

    /// <summary>The optional knobs a family accepts, from the emitter's own guards: the side-tuck room is
    /// I/Z/scythe only, the wool-at-an-end is the U/H/clamp/donut variant, and the attachment width is the
    /// donut's hub-entry. A knob absent here is one the emitter rejects for that family outright.</summary>
    private static IReadOnlyList<string> KnobsOf(ShapeFamily family)
    {
        var knobs = new List<string> { "flip" };
        if (family is ShapeFamily.I or ShapeFamily.Z or ShapeFamily.Scythe) knobs.Add("sideTuck");
        if (family is ShapeFamily.U or ShapeFamily.H or ShapeFamily.Clamp or ShapeFamily.Donut) knobs.Add("woolAtEnd");
        if (family is ShapeFamily.Donut or ShapeFamily.Scythe) knobs.Add("attachW");
        return knobs;
    }
}
