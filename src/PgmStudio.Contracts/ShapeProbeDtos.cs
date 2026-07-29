namespace PgmStudio.Contracts;

/// <summary>What the emitter said about one knob combination. Exactly one of <paramref name="Svg"/> and
/// <paramref name="Rejection"/> is set: a probe either drew a shape or was refused, and a refusal is the
/// interesting half — it is the only place the studio surfaces the emitter's own guards.
/// <paramref name="LandCells"/> is the walkable terrain the fill spends (the box's land currency);
/// <paramref name="Slots"/> names each emitted piece's template slot, in emit order.</summary>
public sealed record ShapeProbeResult(
    string? Svg,
    ShapeRejectionDto? Rejection,
    int LandCells,
    IReadOnlyList<string> Slots);

/// <summary>A refusal in the shared directed-reason vocabulary, flattened for the wire.
/// <paramref name="Code"/> is the case (<c>too-small</c>, <c>not-on-menu</c>, <c>illegal-dock</c>,
/// <c>unsupported-knobs</c>, <c>form-does-not-fit</c>); <paramref name="Detail"/> is the emitter's own message
/// where it has one, passed through rather than reworded. <paramref name="MinW"/>/<paramref name="MinH"/> carry
/// the family's minimum box on a size refusal, so the panel can offer the exact resize.</summary>
public sealed record ShapeRejectionDto(string Code, string Detail, int? MinW, int? MinH);

/// <summary>The knob surface the probe panel drives, and what each family admits — served rather than
/// restated in the client, so a family gaining a knob does not need a matching edit in the page.</summary>
public sealed record ShapeProbeSchema(
    IReadOnlyList<ShapeFamilyDto> Families,
    IReadOnlyList<string> Mouths,
    int MinCorridorCells,
    int MaxCorridorCells);

/// <summary>One family's probe surface: its token, the minimum box it needs at the default corridor width,
/// which optional knobs it accepts, and whether the production menu admits it at all.
///
/// <para><paramref name="MinW"/>/<paramref name="MinH"/> are in the <b>dock frame</b> — the box a fill through a
/// top or bottom mouth needs — which is the frame a size refusal reports and therefore the only one that can
/// agree with it. A lateral-mouth family (the donut and clamp open sideways) transposes between this and the
/// emitter's canonical frame, so the two differ for those and picking the wrong one puts a chip and a refusal
/// visibly at odds. A left or right mouth swaps the pair.</para></summary>
public sealed record ShapeFamilyDto(
    string Token,
    string Label,
    int MinW,
    int MinH,
    bool OnProductionMenu,
    IReadOnlyList<string> Knobs);
