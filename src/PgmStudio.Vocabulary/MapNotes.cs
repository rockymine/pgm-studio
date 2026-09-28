namespace PgmStudio.Vocabulary;

/// <summary>
/// Where a note's thread stands. A new note is <see cref="Open"/> and waits for an agent; an agent's reply
/// leaves it <see cref="Answered"/>, <see cref="NeedsInfo"/> or <see cref="WontDo"/>, each waiting for the author;
/// only the author sets <see cref="Resolved"/>. Spelled by the studio that stores it, the notes column that
/// filters on it and the agent that sets it.
/// </summary>
public static class NoteStatuses
{
    /// <summary>Waiting for an agent: a new note, a reply from the author, or a thread the author reopened.</summary>
    public const string Open = "open";

    /// <summary>An agent's change landed, and the thread waits for the author.</summary>
    public const string Answered = "answered";

    /// <summary>An agent could not act without more, asked what, and waits for the author.</summary>
    public const string NeedsInfo = "needs-info";

    /// <summary>Declined with a reason. It stays visible, and the author can reopen it.</summary>
    public const string WontDo = "wont-do";

    /// <summary>The author closed it.</summary>
    public const string Resolved = "resolved";

    public static readonly (string Id, string Name)[] All =
    [
        (Open, "Open"), (Answered, "Answered"), (NeedsInfo, "Needs info"), (WontDo, "Won't do"), (Resolved, "Resolved"),
    ];

    public static bool IsValid(string? status) => All.Any(entry => entry.Id == status);

    /// <summary>The statuses an agent's reply may leave a thread in: it answers, asks or declines.</summary>
    public static readonly string[] AgentReplies = [Answered, NeedsInfo, WontDo];

    /// <summary>Whether a thread in <paramref name="status"/> waits for its author rather than for an agent.</summary>
    public static bool WaitsOnAuthor(string status) => status is Answered or NeedsInfo or WontDo;

    /// <summary>The word's label, or the word itself where it is not one of the set.</summary>
    public static string Label(string status) => All.FirstOrDefault(entry => entry.Id == status).Name ?? status;
}

/// <summary>
/// What a note is about, when its author says. <see cref="Gameplay"/>, <see cref="Terrain"/> and
/// <see cref="Look"/> go to the map; <see cref="Studio"/> means the studio got something wrong or cannot do it and
/// becomes a backlog task; <see cref="Ruling"/> is a gameplay decision that holds on every map. A note may carry
/// none.
/// </summary>
public static class NoteTags
{
    public const string Gameplay = "gameplay";
    public const string Terrain = "terrain";
    public const string Look = "look";
    public const string Studio = "studio";
    public const string Ruling = "ruling";

    public static readonly string[] All = [Look, Terrain, Gameplay, Studio, Ruling];

    public static bool IsValid(string? tag) => tag is null || All.Contains(tag);
}

/// <summary>
/// What a note is pinned to. Every anchor but <see cref="Map"/> is a picture taken in the In game phase and keeps
/// its camera: <see cref="View"/> is the picture itself, <see cref="Point"/> one block on it, <see cref="Box"/>
/// and <see cref="Lasso"/> the ground their pixels' rays hit.
/// </summary>
public static class NoteAnchors
{
    public const string Map = "map";
    public const string View = "view";
    public const string Point = "point";
    public const string Box = "box";
    public const string Lasso = "lasso";

    public static readonly string[] All = [Map, View, Point, Box, Lasso];

    public static bool IsValid(string? kind) => kind is not null && All.Contains(kind);

    /// <summary>Whether the anchor is drawn on a picture and so carries a camera.</summary>
    public static bool OnPicture(string kind) => kind is not Map;

    /// <summary>Whether the anchor names ground — a block or an area — rather than a whole picture.</summary>
    public static bool Marks(string kind) => kind is Point or Box or Lasso;
}
