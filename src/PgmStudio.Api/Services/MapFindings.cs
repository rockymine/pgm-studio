using System.Text;
using PgmStudio.Contracts;
using PgmStudio.Domain;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Export;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Sketch;
using PgmStudio.Vocabulary;
using PgmStudio.Analysis.Playability;

namespace PgmStudio.Api.Services;

/// <summary>
/// Asking a stored map everything that can be asked of it without building anything.
///
/// <para><b>Every other gate is reached through the step it lives behind</b>, so a fault authored at one step
/// is heard at another: a plan whose objectives cannot be placed is heard at the compile, a layout that
/// rasterizes to nothing at the finish. A driver's loop is then <em>act and hope the next call mentions
/// it</em>. This is what turns that into <em>act, then ask</em>.</para>
///
/// <para><b>It calls the gates rather than restating them.</b> A summary that re-implemented one would be a
/// second copy free to disagree with the gate the route actually runs, which is the whole failure this is
/// meant to remove — so every finding here comes from the same method the step itself calls.</para>
///
/// <para><b>A read does not pay for a build.</b> The export gates need the rasterized world, which is seconds
/// of work this would spend on every call, and nothing is lost by not spending it: those gates are already
/// asked where the build is paid for. What would be lost is a caller believing a silent list means nothing is
/// wrong, so each is named in <c>unasked</c> with the route that does pay.</para>
///
/// <para><b>Which documents the map holds decides how much can be answered</b>, and the stage only follows
/// from that: the intent answers for the play pieces, and the sketch for the ground. A plan stored with a map is
/// history and is asked nothing.</para>
/// </summary>
public static class MapFindings
{
    public static async Task<MapFindingsDto> OfAsync(
        MapArtifactStore artifacts, FeatureData feature, MapReader reader, MapRow map, CancellationToken ct)
    {
        var findings = new List<Finding>();
        var layoutBytes = await artifacts.LoadLayoutAsync(map.Id, ct);
        var intent = await artifacts.LoadJsonAsync<MapIntent>(map.Id, ArtifactKind.MapIntentJson, ct);

        // The play pieces are asked of the intent where the map holds one, since that is what it builds, and the
        // board's structure and its seams of the ground its sketch builds.
        if (intent is not null)
        {
            var layoutJson = layoutBytes is { Length: > 0 } ? Encoding.UTF8.GetString(layoutBytes) : null;
            if (layoutJson is null)
            {
                findings.AddRange(IntentCheck.Check(intent));
                findings.AddRange(RoomCheck.Check(intent, RoomStyleScope.StylesOf((SketchLayout?)null)));
            }
            else
            {
                var layout = SketchLayout.Parse(layoutJson);
                // The board's play rules are the ones the export refuses on, asked here by the same call.
                findings.AddRange(SketchPlayCheck.Check(layoutJson, intent));
                var settled = WoolDoors.Settled(intent, layoutJson);
                findings.AddRange(RoomCheck.Check(settled, RoomStyleScope.StylesOf(layout)));
                findings.AddRange(SketchMeasures.Check(layoutJson, settled, layout?.Setup?.Board));
            }
        }

        if (layoutBytes is { Length: > 0 })
        {
            var layoutJson = Encoding.UTF8.GetString(layoutBytes);
            findings.AddRange(SketchMaterialGate.Check(layoutJson));
            findings.AddRange(SketchFinish.Judge(layoutJson));
        }

        return new MapFindingsDto(map.Stage, findings, Unasked);
    }

    /// <summary>The gates a read cannot reach, each with what it needs and where it is asked. Each judges the
    /// world the studio builds, so each is answered by a route that builds it.</summary>
    private static readonly UnaskedGate[] Unasked =
    [
        new("traversability",
            "EX1 and EX2 judge whether a player can walk between everything a match needs, which is a walk "
            + "over the built world rather than over any stored document",
            "GET /api/map/{slug}/export"),
        new("objective placement",
            "OB17 and OB20 judge each objective against the ground under it, which does not exist until the "
            + "layout is rasterized",
            "GET /api/map/{slug}/export"),
        new("dead ground",
            "EZ1 names standing ground nobody may edit, and what stands is the built world's — its houses and "
            + "trees are ground a player reaches",
            "GET /api/map/{slug}/editability"),
        new("dressing",
            "what the dressing pass could not seat is decided while it seats things, and is answered as "
            + "declines on the build that ran",
            "GET /api/map/{slug}/export"),
    ];
}
