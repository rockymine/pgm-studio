using System.Text;
using PgmStudio.Contracts;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Sketch;
using PgmStudio.Vocabulary;

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
/// from that. A map at <c>configure</c> whose plan is still stored is asked the plan's questions too — the
/// plan is still there to be wrong.</para>
/// </summary>
public static class MapFindings
{
    public static async Task<MapFindingsDto> OfAsync(
        MapArtifactStore artifacts, MapRow map, CancellationToken ct)
    {
        var findings = new List<Finding>();

        if (await artifacts.LoadAsync(map.Id, ArtifactKind.PlanJson, ct) is { Length: > 0 } planBytes
            && PlanModel.Parse(Encoding.UTF8.GetString(planBytes)) is { } plan)
        {
            // Both halves, because they answer different questions and a driver wants both: whether what the
            // plan says is coherent, and whether it yet says the things a map cannot exist without.
            findings.AddRange(PlanValidator.Check(plan));
            findings.AddRange(PlanValidator.Completeness(plan));
        }

        if (await artifacts.LoadAsync(map.Id, ArtifactKind.SketchLayoutJson, ct) is { Length: > 0 } layoutBytes)
        {
            var layoutJson = Encoding.UTF8.GetString(layoutBytes);
            findings.AddRange(SketchRoomStyleGate.Check(layoutJson));
            findings.AddRange(SketchLayoutCheck.Check(layoutJson));
        }

        return new MapFindingsDto(map.Stage, findings, Unasked);
    }

    /// <summary>The gates a read cannot reach, each with what it needs and where it is asked. All of them
    /// judge the world the export builds, so all of them are answered by asking for it.</summary>
    private static readonly UnaskedGate[] Unasked =
    [
        new("traversability",
            "EX1 and EX2 judge whether a player can walk between everything a match needs, which is a walk "
            + "over the rasterized world rather than over any stored document",
            "GET /api/map/{slug}/export"),
        new("objective placement",
            "OB17 and OB20 judge each objective against the ground under it, which does not exist until the "
            + "layout is rasterized",
            "GET /api/map/{slug}/export"),
        new("dressing",
            "what the dressing pass could not seat is decided while it seats things, and is answered as "
            + "declines on the build that ran",
            "GET /api/map/{slug}/export"),
    ];
}
