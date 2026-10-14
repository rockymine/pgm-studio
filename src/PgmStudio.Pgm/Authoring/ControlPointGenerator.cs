using PgmStudio.Domain;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Authoring;

using PgmStudio.Pgm.Editing;
using Dict = Dictionary<string, object?>;

/// <summary>
/// Capture-point (CP/KotH) slice of the declarative generator: one <c>&lt;hill&gt;</c> per point, the two
/// regions it is played and displayed through, and the <c>&lt;score&gt;</c> the match ends at.
///
/// <para>The <c>&lt;score&gt;</c> its points pay into is <see cref="ScoreGenerator"/>'s, which a flag pays into
/// too.</para>
///
/// <para><b>Most of what PGM will read off a point is not authored.</b> A hill and a control point disagree
/// about nearly every default, so the studio writes its convention out in full rather than relying on either
/// — <c>docs/pgm/control-points.md</c> §7 is the shape, and it is the corpus's. <c>required="false"</c> is
/// the one that is not a convention but a correctness rule: PGM defaults it to <b>true</b> at every proto
/// this studio writes, and a point that keeps that default ends the match for whoever captures it first.</para>
///
/// <para>All three regions are the stamper's own boxes (OB8). The point is <em>played</em> through its
/// capture region, <em>seen up close</em> through the progress region that scopes its pad, and <em>seen from
/// across the board</em> through the owner region that scopes its sky marker — and deriving any of them
/// independently is how a region misses the blocks it belongs to. The pad and the marker are separate
/// regions rather than one: the progress display is a pie swept about the centre of its own bounds, so a
/// marker inside it would move that centre off the pad, while the owner display is flat and does not
/// care where its blocks are. They do not overlap, which is what keeps PGM's
/// <c>InverseFilter(progress)</c> over the owner region from eating the marker.</para>
///
/// <para>Idempotent clear-then-build — a board that states no point leaves no <c>&lt;hill&gt;</c>. The intent
/// model authors new maps (<c>new-map-authoring.md</c> §7), so the slice owns its own output outright.</para>
/// </summary>
public static class ControlPointGenerator
{
    private const string Key = "control_points";
    private const string HeldPrefix = "__held-";
    private const string TouchSuffix = "-touchers";

    public static void Apply(Dict doc, MapIntent intent)
    {
        var list = ObjectiveRegion.List(doc, Key);
        var filters = DocAccess.Filters(doc);
        foreach (var id in list.OfType<Dict>().Select(point => point.GetValueOrDefault("player_filter") as string).OfType<string>())
            if (id.EndsWith(TouchSuffix, StringComparison.Ordinal))
            {
                filters.Remove(id);
                filters.Remove(HeldPrefix + id[..^TouchSuffix.Length]);
            }
        ObjectiveRegion.Clear(doc, list, "capture_region", "progress_region", "owner_region");
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
            if (point.CaptureMode == CaptureModes.Touch) Touch(doc, entry, id);
            if (point.Name.Length > 0) entry["name"] = point.Name;
            // The pad is where the pie is drawn, so the display region is exactly the course the stamper
            // laid — one block thick, and every block of it colour-affected.
            if (point.PadBox is { } pad) entry["progress_region"] = ObjectiveRegion.Emit(doc, id, pad, "pad");
            // The sky marker, which is white until somebody owns the point and the holder's dye afterwards.
            // Flat display, so the box is emitted as it stands and PGM picks the wool cross out of the air
            // around it through the default visual-materials filter.
            if (point.MarkerBox is { } marker)
                entry["owner_region"] = ObjectiveRegion.Emit(doc, id, marker, "marker");
            list.Add(entry);
        }
    }

    // A touch point (pgmvox's TouchPoint): taken in the shortest capture there is by whoever leads on it, with no
    // neutral state between owners, and played only by the players whose team does not hold it — so a step onto
    // it takes it however many of the holders stand there, and it stays taken until another team steps on.
    private static void Touch(Dict doc, Dict entry, string id)
    {
        var held = HeldPrefix + id;
        FilterEditor.CreateFilter(doc, new Dict { ["id"] = held, ["type"] = "objective", ["objective"] = id });
        var touchers = id + TouchSuffix;
        FilterEditor.CreateFilter(doc, new Dict { ["id"] = touchers, ["type"] = "not", ["child"] = held });
        entry["capture_time"] = ObjectiveDefaults.TouchCaptureTime;
        entry["capture_rule"] = "lead";
        entry["neutral_state"] = false;
        entry["incremental"] = false;
        entry["show_progress"] = false;
        entry["player_filter"] = touchers;
    }
}
