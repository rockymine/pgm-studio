using PgmStudio.Vocabulary;

namespace PgmStudio.Contracts;

/// <summary>Where an eye stood and where it looked, exactly: the camera a picture was drawn with, which
/// <c>render/eye?eye=x,y,z&amp;yaw=&amp;pitch=&amp;fov=</c> draws again.</summary>
/// <param name="X">East–west, in blocks.</param>
/// <param name="Y">The eye's height.</param>
/// <param name="Z">North–south.</param>
/// <param name="Yaw">The game's own, from −180 to 180: 0 looks south (+z), 90 west, 180 north, −90 east.</param>
/// <param name="Pitch">Degrees below the horizon, negative looking up.</param>
/// <param name="Fov">Horizontal field of view, in degrees.</param>
public sealed record EyeCameraDto(double X, double Y, double Z, double Yaw, double Pitch, double Fov);

/// <summary>One block, by its coordinates.</summary>
/// <param name="X">East–west.</param>
/// <param name="Y">Its height.</param>
/// <param name="Z">North–south.</param>
public sealed record BlockAtDto(int X, int Y, int Z);

/// <summary>One pixel of a picture, counted from its top-left corner.</summary>
/// <param name="X">Pixels from the left edge.</param>
/// <param name="Y">Pixels from the top edge.</param>
public sealed record PixelDto(int X, int Y);

/// <summary>What a mark drawn on a picture is on the ground (<c>GET …/render/eye/pick</c>).</summary>
/// <param name="Camera">The camera the picture is drawn with, whatever the query left to the eye.</param>
/// <param name="Query">The <c>render/eye</c> query words that draw that camera again, exactly.</param>
/// <param name="Hit">For a point, the block the pixel's ray hits; null for sky or for an area.</param>
/// <param name="Ground">For a point, the ground under that block — the grass under a floating tree's
/// leaves; null where the column holds none.</param>
/// <param name="Columns">For an area, every ground column its pixels' rays hit, each <c>[x, y, z]</c> with
/// <c>y</c> the height the ground was hit at, sorted by <c>x</c> then <c>z</c>. Empty for a point.</param>
/// <param name="Sky">How many of the mark's pixels hit nothing.</param>
/// <param name="OverVoid">For an area, where the pixels that hit nothing and look down meet the level of the
/// ground — the median height of <paramref name="Columns"/>, else of the board's ground — each <c>[x, y, z]</c>
/// with <c>y</c> that level, sorted by <c>x</c> then <c>z</c>: the void beside a board a mark was drawn over,
/// every one a column that holds no block. Empty for a point.</param>
/// <param name="Standing">The height of the ground under the camera — the top block a player would stand on
/// there — or null where the camera is over the void; a camera at a player's eye is 2.62 over it.</param>
/// <param name="Change">The map's latest change when the pick was cast: the board it read. A picture listed at an
/// earlier change shows a board this one may not be.</param>
public sealed record EyePickDto(
    EyeCameraDto Camera, string Query, BlockAtDto? Hit, BlockAtDto? Ground, IReadOnlyList<int[]> Columns, int Sky,
    IReadOnlyList<int[]> OverVoid, int? Standing, long Change);

/// <summary>
/// What a note is pinned to. A <c>map</c> note names nothing spatial. Every other kind was written on a picture
/// in the In game phase and keeps the view it was, the exact camera and the picture's size; a <c>point</c>,
/// <c>box</c> or <c>lasso</c> also keeps the mark in that picture's pixels and the ground it was projected onto.
/// </summary>
/// <param name="Kind">What it is pinned to.</param>
/// <param name="ViewId">The gallery view the picture was, or null for a map note.</param>
/// <param name="ViewName">What the gallery called that view when the note was written.</param>
/// <param name="Camera">The camera the picture was drawn with.</param>
/// <param name="Width">The picture's width in pixels, which <paramref name="Marks"/> are counted in.</param>
/// <param name="Height">Its height in pixels.</param>
/// <param name="Marks">The mark in pixels: one for a point, two opposite corners for a box, the outline for a
/// lasso.</param>
/// <param name="Hit">The block a point's pixel hit.</param>
/// <param name="Ground">The ground under that block.</param>
/// <param name="Columns">The ground an area's pixels hit, each <c>[x, y, z]</c>.</param>
/// <param name="OverVoid">The void an area was drawn over, each <c>[x, y, z]</c> at the level of the ground
/// beside it.</param>
public sealed record NoteAnchorDto(
    [property: WordSet(typeof(NoteAnchors))] string Kind,
    string? ViewId = null,
    string? ViewName = null,
    EyeCameraDto? Camera = null,
    int? Width = null,
    int? Height = null,
    IReadOnlyList<PixelDto>? Marks = null,
    BlockAtDto? Hit = null,
    BlockAtDto? Ground = null,
    IReadOnlyList<int[]>? Columns = null,
    IReadOnlyList<int[]>? OverVoid = null);

/// <summary>One message in a note's thread.</summary>
/// <param name="Id">Its id.</param>
/// <param name="Author">The Minecraft name of who wrote it; <c>local</c> on an open studio.</param>
/// <param name="AuthorUuid">Their uuid, or null for an open studio's local admin.</param>
/// <param name="Token">The label of the token it was written with, which is what marks an agent's message;
/// null for one written in a browser.</param>
/// <param name="Body">What it says.</param>
/// <param name="Change">The map's change it was written at: the latest change to its documents when it was
/// written, or the one its writer stated; 0 is before the map's first kept change.</param>
/// <param name="Picture">The picture it carries, by hash — the one a note was written on, or the same camera
/// after an agent's change — served at <c>GET /api/notes/pictures/{hash}</c>; null for none.</param>
/// <param name="At">When it was written, in UTC.</param>
/// <param name="Mark">A reply's own mark on a picture — a <c>point</c>, <c>box</c> or <c>lasso</c> and the ground it
/// was projected onto, kept as a note's anchor is; null for none, and always null on the note's own first message,
/// whose place is the note's anchor.</param>
public sealed record NoteMessageDto(
    long Id, string Author, string? AuthorUuid, string? Token, string Body, long Change, string? Picture, DateTime At,
    NoteAnchorDto? Mark = null);

/// <summary>A note and its thread.</summary>
/// <param name="Id">Its id.</param>
/// <param name="Map">The slug of the map it is on.</param>
/// <param name="MapName">That map's name.</param>
/// <param name="Anchor">What it is pinned to.</param>
/// <param name="Tag">What its author said it is about, or null.</param>
/// <param name="Status">Where the thread stands.</param>
/// <param name="CreatedAt">When it was written, in UTC.</param>
/// <param name="UpdatedAt">When its thread last changed, in UTC.</param>
/// <param name="Messages">The thread, oldest first; the first message is the note itself.</param>
public sealed record MapNoteDto(
    long Id, string Map, string MapName, NoteAnchorDto Anchor,
    [property: WordSet(typeof(NoteTags))] string? Tag,
    [property: WordSet(typeof(NoteStatuses))] string Status,
    DateTime CreatedAt, DateTime UpdatedAt, IReadOnlyList<NoteMessageDto> Messages);

/// <summary>A new note (<c>POST /api/map/{slug}/notes</c>).</summary>
/// <param name="Body">What it says.</param>
/// <param name="Anchor">What it is pinned to.</param>
/// <param name="Tag">What it is about, or null.</param>
/// <param name="Picture">The picture it was written on, by the hash <c>POST /api/notes/pictures</c> answered.</param>
/// <param name="Change">The map's change it was written at; absent takes the latest.</param>
public sealed record MapNoteRequest(
    string Body, NoteAnchorDto Anchor,
    [property: WordSet(typeof(NoteTags))] string? Tag = null,
    string? Picture = null,
    long? Change = null);

/// <summary>A reply in a note's thread (<c>POST /api/map/{slug}/notes/{id}/replies</c>).</summary>
/// <param name="Body">What it says.</param>
/// <param name="Status">Where the reply leaves the thread: <c>answered</c>, <c>needs-info</c> or <c>wont-do</c>
/// for an agent, <c>open</c> to hand it back to one. Absent is <c>answered</c> for a reply written with a token
/// and <c>open</c> for one written in a browser.</param>
/// <param name="Picture">The same camera after the change, by hash, or null.</param>
/// <param name="Change">The map's change the reply was written at; absent takes the latest.</param>
/// <param name="Mark">A mark on a picture the reply is about — "no, this one" — as <c>render/eye/pick</c> answered
/// it: a <c>point</c>, <c>box</c> or <c>lasso</c> anchor with its camera, the picture's size, the pixels and the
/// ground. Absent for none.</param>
public sealed record NoteReplyRequest(
    string Body,
    [property: WordSet(typeof(NoteStatuses))] string? Status = null,
    string? Picture = null,
    long? Change = null,
    NoteAnchorDto? Mark = null);

/// <summary>A change to a note (<c>PATCH /api/map/{slug}/notes/{id}</c>): its status, its tag, or both.</summary>
/// <param name="Status"><c>resolved</c>, <c>wont-do</c> or <c>open</c> to reopen it; absent leaves it.</param>
/// <param name="Tag">What it is about; an empty string clears it, absent leaves it.</param>
public sealed record NoteChangeRequest(
    [property: WordSet(typeof(NoteStatuses))] string? Status = null,
    [property: WordSet(typeof(NoteTags))] string? Tag = null);

/// <summary>Where the author's notes stand with the agent (<c>GET</c> and <c>POST /api/notes/handoff</c>).</summary>
/// <param name="Ready">Whether this studio names an agent to hand notes to.</param>
/// <param name="Waiting">How many notes on every map are open — waiting for an agent.</param>
/// <param name="Fresh">How many of those were written or answered since the last hand-off; all of them before the
/// first.</param>
/// <param name="HandedAt">When the last hand-off was taken, in UTC, or null.</param>
/// <param name="Session">The session the last hand-off started, to watch it at, or null.</param>
public sealed record NoteHandoffDto(bool Ready, int Waiting, int Fresh, DateTime? HandedAt, string? Session);

/// <summary>A hand-off (<c>POST /api/notes/handoff</c>).</summary>
/// <param name="Again">Hand the open notes over even where none was written since the last hand-off — a session
/// that stopped short, asked again.</param>
public sealed record NoteHandoffRequest(bool Again = false);

/// <summary>A picture kept for a note (<c>POST /api/notes/pictures</c>).</summary>
/// <param name="Hash">The SHA-256 of its bytes, which names it.</param>
/// <param name="Bytes">Its size.</param>
public sealed record NotePictureDto(string Hash, long Bytes);
