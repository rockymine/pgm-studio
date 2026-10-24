namespace PgmStudio.Vocabulary;

/// <summary>
/// The terraform operations a sketch's <c>modifiers[]</c> may run, each one change to the ground already
/// built, inside its mask, in list order (<c>docs/tools/sketch.md</c>, <i>Terraform</i>). The word is what the
/// document stores; the screen calls each by the name beside it.
/// </summary>
public static class ModifierOps
{
    /// <summary>A Hill: the ground raised inside the mask, highest where the mask is deepest and falling to
    /// nothing at its rim.</summary>
    public const string Mound = "mound";

    /// <summary>A Hollow: a bowl sunk to a floor, flat in its middle and rounded into the ground over its
    /// melt.</summary>
    public const string Crater = "crater";

    /// <summary>A Level: the ground eased to one height inside the mask and back out over its skirt.</summary>
    public const string Level = "level";

    /// <summary>Roughness: noise in one or more sizes, added inside the mask.</summary>
    public const string Noise = "noise";

    /// <summary>A Ridge: a crest along a line or the spine of an area, its foot wandering.</summary>
    public const string Ridge = "ridge";

    /// <summary>A Butte: a flat top on an area, a cliff round it and a talus apron below.</summary>
    public const string Butte = "butte";

    /// <summary>A Spire: a needle rising to a stated top from the middle of an area.</summary>
    public const string Spire = "spire";

    /// <summary>A Scarp: one side of a line stepped up by a cliff, a talus apron on the other.</summary>
    public const string Scarp = "scarp";

    /// <summary>A Canyon: a trench along a line with a floor, walls and optional ledges.</summary>
    public const string Canyon = "canyon";

    /// <summary>A River: a bed cut along a line that never climbs, holding water.</summary>
    public const string River = "river";

    /// <summary>A Lake: a bowl cut into an area, holding water to a level.</summary>
    public const string Lake = "lake";

    /// <summary>A Road: a route graded into the ground along a line.</summary>
    public const string Road = "road";

    /// <summary>Gullies: grooves cut outward from the middle of an area.</summary>
    public const string Gullies = "gullies";

    /// <summary>A Warp: the ground inside resampled at noise-shifted positions.</summary>
    public const string Warp = "warp";

    /// <summary>A Profile: a cross-section set by distance from a line, a floor and steps of cliff and bench.</summary>
    public const string Profile = "profile";

    /// <summary>A Coast: a sea over an area, its floor falling from the shore and a beach easing the land to it.</summary>
    public const string Coast = "coast";

    /// <summary>Lumps: hummocks and fallen blocks strewn over an area.</summary>
    public const string Lumps = "lumps";

    /// <summary>A Shatter: the ground inside broken into plates, each lifted and tilted, clefts between.</summary>
    public const string Shatter = "shatter";

    public static readonly string[] All =
        [Mound, Crater, Level, Noise, Ridge, Butte, Spire, Scarp, Canyon, River, Lake, Road, Gullies, Warp,
         Profile, Coast, Lumps, Shatter];

    /// <summary>What the screen calls an operation.</summary>
    public static string Label(string? op) => op switch
    {
        Mound => "Hill",
        Crater => "Hollow",
        Level => "Level",
        Noise => "Roughness",
        Ridge => "Ridge",
        Butte => "Butte",
        Spire => "Spire",
        Scarp => "Scarp",
        Canyon => "Canyon",
        River => "River",
        Lake => "Lake",
        Road => "Road",
        Gullies => "Gullies",
        Warp => "Warp",
        Profile => "Profile",
        Coast => "Coast",
        Lumps => "Lumps",
        Shatter => "Shatter",
        _ => op ?? "",
    };
}

/// <summary>Which half of a modifier's change is kept. Absent keeps both.</summary>
public static class ModifierModes
{
    /// <summary>Only what raises the ground is kept.</summary>
    public const string Lift = "lift";

    /// <summary>Only what lowers the ground is kept: a Hill with a negative rise is then a dish of its own shape.</summary>
    public const string Cut = "cut";

    public static readonly string[] All = [Lift, Cut];
}

/// <summary>The outlines a modifier's mask is drawn as.</summary>
public static class MaskKinds
{
    /// <summary>A centre, two radii and a turn.</summary>
    public const string Ellipse = "ellipse";

    /// <summary>A closed outline of points, its depth measured from its rim inward.</summary>
    public const string Polygon = "polygon";

    /// <summary>All the ground there is, at full strength everywhere.</summary>
    public const string Board = "board";

    /// <summary>An open polyline of points and a width; its distance is measured from the line.</summary>
    public const string Line = "line";

    public static readonly string[] All = [Ellipse, Polygon, Board, Line];
}
