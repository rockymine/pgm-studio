using PgmStudio.Domain;

namespace PgmStudio.Pgm.Authoring;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Capture-point (CP/KotH) slice of the declarative generator: one <c>&lt;hill&gt;</c> per point, the two
/// regions it is played and displayed through, and the <c>&lt;score&gt;</c> the match ends at.
///
/// <para><b>The score is written here rather than beside the boilerplate, because without it the points do
/// nothing.</b> PGM builds a score module only for a document that has a <c>&lt;score&gt;</c> element, and
/// <c>ControlPoint.tickScore</c> looks that module up every tick — so a board whose every point names a
/// <c>points</c> rate and declares no score scores nothing, silently, for the whole match. The two are one
/// decision and one generator makes it.</para>
///
/// <para><b>Most of what PGM will read off a point is not authored.</b> A hill and a control point disagree
/// about nearly every default, so the studio writes its convention out in full rather than relying on either
/// — <c>docs/pgm/control-points.md</c> §7 is the shape, and it is the corpus's. <c>required="false"</c> is
/// the one that is not a convention but a correctness rule: PGM defaults it to <b>true</b> at every proto
/// this studio writes, and a point that keeps that default ends the match for whoever captures it first.</para>
///
/// <para>Both regions are the stamper's own boxes (OB8). The point is <em>played</em> through its capture
/// region and <em>seen</em> through its progress region, and deriving either independently is how a region
/// misses the pad it belongs to. No owner-display region is written: at zero progress PGM paints the whole
/// progress region in the controller's colour, so the pad already shows who holds it, and a second region
/// over the same blocks would be subtracted away to nothing.</para>
///
/// <para>Idempotent clear-then-build, the score element included — a board that states no scoring point
/// leaves none behind, the same way a board that states no point leaves no <c>&lt;hill&gt;</c>. The intent
/// model authors new maps (<c>new-map-authoring.md</c> §7), so the slice owns its own output outright.</para>
/// </summary>
public static class ControlPointGenerator
{
    private const string Key = "control_points";

    public static void Apply(Dict doc, MapIntent intent)
    {
        var list = ObjectiveRegion.List(doc, Key);
        ObjectiveRegion.Clear(doc, list, "capture_region", "progress_region");
        doc.Remove("score");
        if (intent.ControlPoints is null) return;

        foreach (var point in intent.ControlPoints)
        {
            if (point.CaptureBox is not { } capture) continue;

            var baseId = point.Name.Length > 0 ? IntentNaming.Slug(point.Name) : "hill";
            var id = ObjectiveRegion.UniqueId(list, baseId, "hill");

            var entry = new Dict
            {
                ["id"] = id,
                // A hill, not a control point: one PGM module, and the spelling is what chooses the defaults
                // PGM applies — and what the map is tagged as.
                ["element"] = "king",
                ["capture_region"] = ObjectiveRegion.Emit(doc, id, capture, "capture"),

                // The studio's convention, written out rather than defaulted. Each of these is what the
                // corpus states on a KotH board, and every one of them differs between the two spellings.
                ["required"] = false,
                ["capture_time"] = point.CaptureTime,
                ["points"] = point.Points,
                ["neutral_state"] = true,
                ["incremental"] = true,
                ["show_progress"] = true,
                // No bonus for a crowd: 163 of 316 corpus points turn it off, which is the board playing the
                // same way whether two players or ten walk onto the pad.
                ["time_multiplier"] = 0d,
            };
            if (point.Name.Length > 0) entry["name"] = point.Name;
            // The pad is where the pie is drawn, so the display region is exactly the course the stamper
            // laid — one block thick, and every block of it colour-affected.
            if (point.PadBox is { } pad) entry["progress_region"] = ObjectiveRegion.Emit(doc, id, pad, "pad");
            list.Add(entry);
        }

        if (ScoreLimit(intent) is { } limit) doc["score"] = new Dict { ["limit"] = (long)limit };
    }

    /// <summary>
    /// The limit the match ends at: the author's where they state one, the corpus's default on a board that
    /// carries a point that pays, and none at all otherwise. Zero is an author asking for no limit — a board
    /// played to a time limit instead — and is honoured rather than replaced.
    /// </summary>
    private static int? ScoreLimit(MapIntent intent)
    {
        if (intent.ScoreLimit is { } stated) return stated > 0 ? stated : null;
        var pays = intent.ControlPoints?.Any(p => p.CaptureBox is not null && p.Points > 0) ?? false;
        return pays ? ObjectiveDefaults.ControlPointScoreLimit : null;
    }
}
