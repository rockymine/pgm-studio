namespace PgmStudio.Analysis.Playability;

using PgmStudio.Analysis.Region;
using PgmStudio.Geom;

using Dict = Dictionary<string, object?>;

/// <summary>One place a match starts or ends a journey at, as the map document states it: a spawn, a wool, a
/// destroyable or a core. <see cref="Owner"/> is the team that defends it, which is what tells the journey a
/// team must make to a goal from the one it makes to its own — an attacker has to stand on the goal, and a
/// defender only has to reach the border of the ground its own goal's protection bars it from.</summary>
/// <param name="Kind">What it is: <c>spawn</c>, <c>wool</c>, <c>destroyable</c> or <c>core</c>.</param>
/// <param name="Name">What names it — a team for a spawn, a colour for a wool, the goal's own name.</param>
/// <param name="Owner">The team that defends it — a spawn's own team, a destroyable's or core's stated owner,
/// and for a wool the team its room belongs to — <c>wool.team</c>, which the document carries because the
/// codec infers it from the teams the monuments do not name. Empty where the document names none.</param>
/// <param name="X">Where it stands, east–west.</param>
/// <param name="Z">Where it stands, north–south.</param>
/// <param name="Y">Which storey of that cell it is on, where the document states one — the floor of a spawn's
/// box, a wool's own location, a goal region's underside. Null where the region carries no height, and then
/// the cell's lowest place is what is meant.</param>
public sealed record NavPoint(string Kind, string Name, string Owner, int X, int Z, int? Y = null)
{
    public (int X, int Z) Cell => (X, Z);

    /// <summary>Which place of <paramref name="ground"/> this point stands on: the storey nearest its stated
    /// height, or the cell's lowest where it states none. Null where the cell holds no place at all.</summary>
    public WalkPlace? Seat(WalkGround ground)
        => Y is { } height ? ground.Nearest(Cell, height) : ground.Stand(Cell);
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
    /// <summary>Every spawn, wool, destroyable and core a match is played between, in that order.</summary>
    /// <param name="data">The map document.</param>
    /// <param name="bounds">The extent a region resolves its geometry within.</param>
    /// <param name="declared">Goals stated somewhere the document cannot carry them. A destroyable's region is
    /// the box the stamper built its blocks from, so a goal whose box is not cast yet is left out of the
    /// document rather than given a guessed one — correct for the contract, and it would leave every read
    /// before a build blind to that goal. A caller holding the authored intent passes its goals here; one the
    /// document already carries wins, since that one has been placed.</param>
    public static List<NavPoint> Of(Dict data, (double, double, double, double) bounds,
        IReadOnlyList<NavPoint>? declared = null)
    {
        var regions = MapDoc.AsDict(data.GetValueOrDefault("regions"));
        var points = new List<NavPoint>();

        foreach (var spawn in MapDoc.AsList(data.GetValueOrDefault("spawns")).OfType<Dict>())
        {
            var team = spawn.GetValueOrDefault("team") as string ?? "";
            var box = Region(spawn.GetValueOrDefault("region"), regions);
            if (Centre(box, regions, bounds) is { } seat)
                points.Add(new NavPoint("spawn", team, team, seat.x, seat.z, Height(box)));
        }

        foreach (var wool in MapDoc.AsList(data.GetValueOrDefault("wools")).OfType<Dict>())
        {
            var color = wool.GetValueOrDefault("color") as string ?? "";
            var owner = wool.GetValueOrDefault("team") as string ?? "";
            if (Stated(wool.GetValueOrDefault("location")) is { } at)
                points.Add(new NavPoint("wool", color, owner, at.x, at.z, at.y));
            else if (Region(wool.GetValueOrDefault("wool_room_region"), regions) is { } room
                     && Centre(room, regions, bounds) is { } inside)
                points.Add(new NavPoint("wool", color, owner, inside.x, inside.z, Height(room)));
        }

        foreach (var destroyable in MapDoc.AsList(data.GetValueOrDefault("destroyables")).OfType<Dict>())
        {
            // `show: false` marks a destroyable that is decoration rather than an objective — the same
            // reading `Destroyable.IsObjective` takes.
            if (destroyable.GetValueOrDefault("show") is false) continue;
            var owner = destroyable.GetValueOrDefault("owner") as string ?? "";
            var name = destroyable.GetValueOrDefault("name") as string ?? owner;
            var box = Region(destroyable.GetValueOrDefault("region"), regions);
            if (Centre(box, regions, bounds) is { } at)
                points.Add(new NavPoint("destroyable", name, owner, at.x, at.z, Height(box)));
        }

        foreach (var core in MapDoc.AsList(data.GetValueOrDefault("cores")).OfType<Dict>())
        {
            var owner = core.GetValueOrDefault("owner") as string ?? "";
            var box = Region(core.GetValueOrDefault("region"), regions);
            if (Centre(box, regions, bounds) is { } at)
                points.Add(new NavPoint("core", owner, owner, at.x, at.z, Height(box)));
        }

        if (declared is { Count: > 0 })
        {
            var carried = points.Select(point => (point.Kind, point.Name, point.Owner)).ToHashSet();
            points.AddRange(declared.Where(goal => !carried.Contains((goal.Kind, goal.Name, goal.Owner))));
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

    /// <summary>How high a region sits, as the storey a player stands on to reach it: the underside of the
    /// shape, which for a spawn box is its floor and for a goal is the ground its blocks were stamped from.
    /// A region stating no height — a rectangle, a circle — answers null, and the cell's lowest place is what
    /// its point means.</summary>
    public static int? Height(Dict? region)
    {
        if (region is null) return null;
        var at = (region.GetValueOrDefault("type") as string) switch
        {
            "cuboid" => MapDoc.AsDict(region.GetValueOrDefault("min")),
            "cylinder" => MapDoc.AsDict(region.GetValueOrDefault("base")),
            "sphere" or "half" or "mirror" => MapDoc.AsDict(region.GetValueOrDefault("origin")),
            "block" or "point" => MapDoc.AsDict(region.GetValueOrDefault("position")),
            _ => [],
        };
        return MapDoc.Num(at.GetValueOrDefault("y")) is { } y ? (int)y : null;
    }

    private static (int x, int z, int? y)? Stated(object? location)
    {
        var at = MapDoc.AsDict(location);
        return MapDoc.Num(at.GetValueOrDefault("x")) is { } x && MapDoc.Num(at.GetValueOrDefault("z")) is { } z
            ? ((int)x, (int)z, MapDoc.Num(at.GetValueOrDefault("y")) is { } y ? (int)y : null)
            : null;
    }
}
