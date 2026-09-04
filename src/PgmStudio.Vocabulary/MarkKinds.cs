namespace PgmStudio.Vocabulary;

/// <summary>
/// What a relief mark pins. A mark states a height somewhere and the solver interpolates the surface between
/// them; the word decides which of a mark's fields the reader takes.
///
/// <para>Two parties spell them: the reader that turns a stated mark into one the solver takes, and the
/// canvas that places and edits them. A kind neither knows is dropped without a word, so a third spelling is
/// terrain the author drew and the board does not have.</para>
/// </summary>
public static class MarkKinds
{
    /// <summary>A height at one place, falling off over its own radius.</summary>
    public const string Point = "point";

    /// <summary>A ridgeline: heights along a run of points, with a reach either side.</summary>
    public const string Line = "line";

    /// <summary>A ring held at one height.</summary>
    public const string Area = "area";

    /// <summary>A cliff: a run of points with a high side and a low one, and a band each holds across.</summary>
    public const string Scarp = "scarp";

    /// <summary>The group's own edge, held at a height and eased inward.</summary>
    public const string Rim = "rim";

    /// <summary>A push is <b>not</b> a mark — it lifts the solved surface rather than pinning it — but it is
    /// placed, selected and edited through the same phase, so the canvas needs one word for it alongside the
    /// rest. The stored form carries no kind, the array a push sits in already saying what it is, which is why
    /// this word is the canvas's alone and is not in <see cref="All"/>.</summary>
    public const string Push = "push";

    /// <summary>The five kinds a stated mark may name.</summary>
    public static readonly string[] All = [Point, Line, Area, Scarp, Rim];
}
