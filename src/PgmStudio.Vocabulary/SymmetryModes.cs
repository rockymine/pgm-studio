namespace PgmStudio.Vocabulary;

/// <summary>
/// The symmetry a board is laid to: the word a layout's <c>setup.mirror_mode</c> states, the sketch tool's
/// Settings step offers and a sketch is originated with. <c>PgmStudio.Geom.Symmetry</c> is what each word
/// does; this is which words there are.
/// </summary>
public static class SymmetryModes
{
    /// <summary>No symmetry: every shape is drawn once and nothing is fanned.</summary>
    public const string None = "none";

    /// <summary>A reflection across the north–south line through the centre: left and right.</summary>
    public const string MirrorX = "mirror_x";

    /// <summary>A reflection across the east–west line through the centre: front and back.</summary>
    public const string MirrorZ = "mirror_z";

    /// <summary>A reflection across the main diagonal through the centre.</summary>
    public const string MirrorD1 = "mirror_d1";

    /// <summary>A reflection across the anti-diagonal through the centre.</summary>
    public const string MirrorD2 = "mirror_d2";

    /// <summary>A quarter-turn about the centre, four images.</summary>
    public const string Rot90 = "rot_90";

    /// <summary>A half-turn about the centre.</summary>
    public const string Rot180 = "rot_180";

    /// <summary>Every mode a layout may state.</summary>
    public static readonly string[] All = [None, MirrorX, MirrorZ, MirrorD1, MirrorD2, Rot90, Rot180];

    /// <summary>The modes the sketch tool's Settings step offers, in its order.</summary>
    public static readonly string[] Sketched = [MirrorX, MirrorZ, Rot180, Rot90, None];
}
