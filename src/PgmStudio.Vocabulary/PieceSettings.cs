namespace PgmStudio.Vocabulary;

/// <summary>
/// The settings a play piece states beside where it stands, each set on its own: a goal's name and structure, a
/// wool's colour and a spawn's facing. The plan editor's marker fields are spelled the same, and the sketch
/// writes them through <c>PATCH …/intent/pieces/{id}/settings</c>.
/// </summary>
public static class PieceSettings
{
    /// <summary>A destroyable's or a core's name.</summary>
    public const string Name = "name";

    /// <summary>A wool's dye.</summary>
    public const string Color = "color";

    /// <summary>A destroyable's design.</summary>
    public const string Style = "style";

    /// <summary>A destroyable's material.</summary>
    public const string Materials = "materials";

    /// <summary>The air under a destroyable or a core.</summary>
    public const string Float = "float";

    /// <summary>A core's lava footprint, width and depth.</summary>
    public const string Lava = "lava";

    /// <summary>A core's lava height, in layers.</summary>
    public const string LavaHeight = "lavaHeight";

    /// <summary>Whether a core's lava is flush with its rim.</summary>
    public const string OpenTop = "openTop";

    /// <summary>How far a core's lava falls before it counts.</summary>
    public const string Leak = "leak";

    /// <summary>The way a spawn's players face on arriving, turned a quarter at a time.</summary>
    public const string Facing = "facing";

    /// <summary>Every setting.</summary>
    public static readonly string[] All = [Name, Color, Style, Materials, Float, Lava, LavaHeight, OpenTop, Leak, Facing];
}
