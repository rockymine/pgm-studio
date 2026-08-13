namespace PgmStudio.Pgm.Authoring;

using PgmStudio.Domain;
using PgmStudio.Pgm.Editing;
using Dict = Dictionary<string, object?>;

/// <summary>
/// Water-lane slice of the declarative generator (docs/pgm/water-lanes.md): the authored lane
/// footprints become a <c>&lt;union id="water-lanes"&gt;</c> of <c>y=0</c> cuboids, and the map pulls in
/// <c>&lt;include id="water-lanes"/&gt;</c>.
///
/// <para>That pair is the whole export, because it is the whole contract. The shared fragment — resolved by
/// the server out of its includes directory, never shipped with a map — is what fills the lanes with water on
/// a timer; the map states only where they are, under the id the fragment keys on. So this generator writes
/// no fill, no trigger, no mode and no hidden destroyable, and it deliberately writes no <c>&lt;apply&gt;</c>
/// either: not one corpus map that authors a lane this way applies anything to its lane region.</para>
///
/// <para>The lane must stay <b>outside</b> the buildable region <see cref="BuildGenerator"/> emits, and does,
/// because the two come from different zone kinds. That is what makes the mechanism work: the void rule keeps
/// players out of the lane's columns, and it stops keeping them out the moment water lands at <c>y=0</c> and
/// the columns no longer read as void. Adding a lane to the build area would open it from the first tick and
/// destroy the point of it.</para>
///
/// <para>Idempotent clear-then-build, like the other slices.</para>
/// </summary>
public static class WaterLaneGenerator
{
    /// <summary>The id shared by the fragment and the region that keys it. One string is the entire interface
    /// between the map and the behaviour it pulls in, so it is written once and used for both.</summary>
    public const string LaneId = "water-lanes";

    // A lane is the single block layer the void filter reads: `min y = 0`, `max y = 1`. PGM cuboid bounds are
    // half-open, so that pair is exactly y=0 and nothing above it.
    private const int LaneMinY = 0;
    private const int LaneMaxY = 1;

    public static void Apply(Dict doc, MapIntent intent)
    {
        Clear(doc);
        if (intent.WaterLanes is not { Rects.Count: > 0 } lanes) return;

        // The fragment resolves the lanes by id, and PGM resolves an id to a region of any type — so a single
        // lane is the cuboid itself under that id, and several are cuboids unioned under it. Same shape the
        // build slice takes for one area versus many.
        var single = lanes.Rects.Count == 1;
        var laneIds = new List<object?>();
        var laneNumber = 1;
        foreach (var rect in lanes.Rects)
        {
            var id = single ? LaneId : $"{LaneId}-{laneNumber++}";
            RegionEditor.CreateRegion(doc, new Dict
            {
                ["type"] = "cuboid", ["id"] = id, ["category"] = "build",
                ["min_x"] = rect.MinX, ["min_y"] = LaneMinY, ["min_z"] = rect.MinZ,
                ["max_x"] = rect.MaxX, ["max_y"] = LaneMaxY, ["max_z"] = rect.MaxZ,
            });
            laneIds.Add(id);
        }
        if (!single)
            RegionEditor.GroupRegions(doc, new Dict { ["type"] = "union", ["id"] = LaneId, ["child_ids"] = laneIds });
    }

    /// <summary>
    /// Pair the lane region with the include that gives it behaviour, at export. A map has the include iff it
    /// has the region, so the two cannot drift apart: a region with no include is inert geometry the server
    /// never fills, and an include with no region resolves a fragment that matches nothing.
    /// <para>Separate from <see cref="Apply"/> because the two halves live at different seams — the region is
    /// document state that persists, and the include list is assembled onto the <see cref="MapXml"/> at export
    /// beside the rest of the CTW boilerplate (<see cref="CtwStandards"/>).</para>
    /// </summary>
    public static void EnsureInclude(MapXml map)
    {
        var hasLanes = map.Regions.ContainsKey(LaneId);
        if (hasLanes && !map.Includes.Contains(LaneId)) map.Includes.Add(LaneId);
        else if (!hasLanes) map.Includes.Remove(LaneId);
    }

    private static void Clear(Dict doc)
    {
        var regions = DocAccess.Regions(doc);
        foreach (var key in regions.Keys.Where(IsGenerated).ToList()) regions.Remove(key);
    }

    private static bool IsGenerated(string key) =>
        key == LaneId || (key.StartsWith($"{LaneId}-", StringComparison.Ordinal)
                          && int.TryParse(key[(LaneId.Length + 1)..], out _));
}
