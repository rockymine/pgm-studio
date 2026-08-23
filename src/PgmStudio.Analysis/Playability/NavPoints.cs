namespace PgmStudio.Analysis.Playability;

using PgmStudio.Analysis.Region;

using Dict = Dictionary<string, object?>;

/// <summary>One place a match starts or ends a journey at, as the map document states it: a spawn, a wool, a
/// destroyable or a core. <see cref="Owner"/> is the team it belongs to, which is what tells a journey a team
/// must make from one it never makes — a defender is not asked to reach its own wool.</summary>
/// <param name="Kind">What it is: <c>spawn</c>, <c>wool</c>, <c>destroyable</c> or <c>core</c>.</param>
/// <param name="Name">What names it — a team for a spawn, a colour for a wool, the goal's own name.</param>
/// <param name="Owner">The team it belongs to, or empty where the document names none.</param>
/// <param name="X">Where it stands, east–west.</param>
/// <param name="Z">Where it stands, north–south.</param>
public sealed record NavPoint(string Kind, string Name, string Owner, int X, int Z)
{
    public (int X, int Z) Cell => (X, Z);
}

/// <summary>
/// The places a match is played between, read off the map document once.
///
/// <para>Every playability derivation asks the same question of the document first — where are the spawns and
/// the goals — and each answer it gives has to agree, because the reads are compared against each other: the
/// export gate refuses on one, and a kit budget is judged against another. Two readers is two demand sets,
/// and the difference shows up as one read calling a board fine that the other refuses.</para>
///
/// <para>A goal states its position two ways and both are read: a <c>location</c> outright, or the centre of
/// the region it names. A region's centre is its centroid where the centroid is inside it and an interior
/// point otherwise, so a horseshoe answers with ground rather than with the gap it wraps around.</para>
/// </summary>
public static class NavPoints
{
    /// <summary>Every spawn, wool, destroyable and core the document declares, in that order.</summary>
    /// <param name="data">The map document.</param>
    /// <param name="bounds">The extent a region resolves its geometry within.</param>
    public static List<NavPoint> Of(Dict data, (double, double, double, double) bounds)
    {
        var regions = MapDoc.AsDict(data.GetValueOrDefault("regions"));
        var points = new List<NavPoint>();

        foreach (var spawn in MapDoc.AsList(data.GetValueOrDefault("spawns")).OfType<Dict>())
        {
            var team = spawn.GetValueOrDefault("team") as string ?? "";
            if (Centre(Region(spawn.GetValueOrDefault("region"), regions), regions, bounds) is { } seat)
                points.Add(new NavPoint("spawn", team, team, seat.x, seat.z));
        }

        foreach (var wool in MapDoc.AsList(data.GetValueOrDefault("wools")).OfType<Dict>())
        {
            var color = wool.GetValueOrDefault("color") as string ?? "";
            var owner = wool.GetValueOrDefault("team") as string ?? "";
            if (Stated(wool.GetValueOrDefault("location")) is { } at)
                points.Add(new NavPoint("wool", color, owner, at.x, at.z));
            else if (Centre(Region(wool.GetValueOrDefault("wool_room_region"), regions), regions, bounds) is { } room)
                points.Add(new NavPoint("wool", color, owner, room.x, room.z));
        }

        foreach (var destroyable in MapDoc.AsList(data.GetValueOrDefault("destroyables")).OfType<Dict>())
        {
            // `show: false` marks a destroyable that is decoration rather than an objective — the same
            // reading `Destroyable.IsObjective` takes.
            if (destroyable.GetValueOrDefault("show") is false) continue;
            var owner = destroyable.GetValueOrDefault("owner") as string ?? "";
            var name = destroyable.GetValueOrDefault("name") as string ?? owner;
            if (Centre(Region(destroyable.GetValueOrDefault("region"), regions), regions, bounds) is { } at)
                points.Add(new NavPoint("destroyable", name, owner, at.x, at.z));
        }

        foreach (var core in MapDoc.AsList(data.GetValueOrDefault("cores")).OfType<Dict>())
        {
            var owner = core.GetValueOrDefault("owner") as string ?? "";
            if (Centre(Region(core.GetValueOrDefault("region"), regions), regions, bounds) is { } at)
                points.Add(new NavPoint("core", owner, owner, at.x, at.z));
        }

        return points;
    }

    /// <summary>A region reference — a name into the registry, or the region written inline — as a region.</summary>
    public static Dict? Region(object? reference, Dict regions) => reference is string named
        ? regions.GetValueOrDefault(named) as Dict
        : reference as Dict;

    /// <summary>Where a region sits, as one cell: its centroid where that lies inside it, an interior point
    /// otherwise, and failing any geometry the centre of the bounding box it declares.</summary>
    public static (int x, int z)? Centre(Dict? region, Dict registry, (double, double, double, double) bounds)
    {
        if (region is null) return null;

        if (RegionGeometry2d.ToGeometry(region, bounds, registry) is { IsEmpty: false } geom)
        {
            var centroid = geom.Centroid;
            var point = geom.Contains(centroid) ? centroid : geom.InteriorPoint;
            return ((int)point.X, (int)point.Y);
        }

        var box = MapDoc.AsDict(region.GetValueOrDefault("bounds_2d"));
        if (box.Count == 0) return null;
        var min = MapDoc.AsDict(box.GetValueOrDefault("min"));
        var max = MapDoc.AsDict(box.GetValueOrDefault("max"));
        if (MapDoc.Num(min.GetValueOrDefault("x")) is not { } minX
            || MapDoc.Num(min.GetValueOrDefault("z")) is not { } minZ
            || MapDoc.Num(max.GetValueOrDefault("x")) is not { } maxX
            || MapDoc.Num(max.GetValueOrDefault("z")) is not { } maxZ) return null;
        return ((int)((minX + maxX) / 2), (int)((minZ + maxZ) / 2));
    }

    private static (int x, int z)? Stated(object? location)
    {
        var at = MapDoc.AsDict(location);
        return MapDoc.Num(at.GetValueOrDefault("x")) is { } x && MapDoc.Num(at.GetValueOrDefault("z")) is { } z
            ? ((int)x, (int)z)
            : null;
    }
}
