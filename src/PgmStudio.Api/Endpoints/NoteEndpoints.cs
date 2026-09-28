using System.Text.Json;
using FastEndpoints;
using LinqToDB;
using LinqToDB.Async;
using PgmStudio.Api.Access;
using PgmStudio.Api.Services;
using PgmStudio.Contracts;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Domain;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Endpoints;

// ── notes on a map, and the threads under them ─────────────────────────────────────
//
// The author leaves a note pinned to the place it is about, in the Sketch tool's In game phase; an agent
// answers on the note, and the author closes the thread (docs/tools/sketch.md, Notes). Every route here states
// the notes policy, reads included: an admin in a browser, or a token an admin issued with the notes permission.

/// <summary>The one shape a note crosses the wire in, and what a note request must hold.</summary>
internal static class NoteWire
{
    public const int LongestBody = 10_000, MostLassoPoints = 2_000, MostColumns = 250_000;
    public const int WidestPicture = 1920, HighestPicture = 1080;

    public static MapNoteDto Dto(StoredNote stored, string mapName) => new(
        stored.Note.Id, stored.Note.MapSlug, mapName,
        JsonSerializer.Deserialize<NoteAnchorDto>(stored.Note.AnchorJson, MapArtifactStore.Json) ?? new NoteAnchorDto(NoteAnchors.Map),
        stored.Note.Tag, stored.Note.Status, stored.Note.CreatedAt, stored.Note.UpdatedAt,
        [.. stored.Messages.Select(message => new NoteMessageDto(
            message.Id, message.AuthorName, message.AuthorUuid, message.TokenLabel, message.Body, message.Revision,
            message.Picture, message.CreatedAt))]);

    public static string AnchorJson(NoteAnchorDto anchor) => JsonSerializer.Serialize(anchor, MapArtifactStore.Json);

    /// <summary>A message as <paramref name="caller"/> writes it.</summary>
    public static MapNoteMessageRow Message(Caller caller, string body, long revision, string? picture, DateTime at) => new()
    {
        AuthorUuid = caller.Uuid,
        AuthorName = caller.Name is { Length: > 0 } name ? name : "local",
        TokenLabel = caller.Token,
        Body = body.Trim(),
        Revision = revision,
        Picture = picture,
        CreatedAt = at,
    };

    /// <summary>What is wrong with a message's body, or null.</summary>
    public static (string Field, string Message)? BodyFault(string? body) =>
        string.IsNullOrWhiteSpace(body) ? ("body", "a note says something — `body` is empty")
        : body.Length > LongestBody ? ("body", $"`body` is {body.Length} characters, and a note holds {LongestBody}")
        : null;

    /// <summary>What is wrong with a picture a message names, or null.</summary>
    public static (string Field, string Message)? PictureFault(string? picture, NotePictures pictures) =>
        picture is null ? null
        : !pictures.Has(picture) ? ("picture", $"no picture is kept under '{picture}' — post it to /api/notes/pictures first, "
            + "and name the hash that answers")
        : null;

    /// <summary>What is wrong with an anchor, or null. A picture anchor carries the camera it was drawn with and
    /// the picture's size; a mark carries its pixels, one for a point, two corners for a box and an outline for
    /// a lasso.</summary>
    public static (string Field, string Message)? AnchorFault(NoteAnchorDto? anchor)
    {
        if (anchor is null) return ("anchor", "a note is pinned to something — `anchor` is absent");
        if (!NoteAnchors.IsValid(anchor.Kind))
            return ("anchor.kind", $"`anchor.kind` is one of {string.Join(", ", NoteAnchors.All)}");
        if (!NoteAnchors.OnPicture(anchor.Kind)) return null;
        if (anchor.Camera is not { } camera || !double.IsFinite(camera.X + camera.Y + camera.Z + camera.Yaw + camera.Pitch + camera.Fov))
            return ("anchor.camera", $"a `{anchor.Kind}` note was written on a picture, and keeps the camera it was drawn with");
        if (anchor.Width is not (> 0 and <= WidestPicture) || anchor.Height is not (> 0 and <= HighestPicture))
            return ("anchor.width", $"a picture's size is up to {WidestPicture} × {HighestPicture} pixels");
        if (!NoteAnchors.Marks(anchor.Kind)) return null;

        var marks = anchor.Marks ?? [];
        var (least, most) = anchor.Kind switch
        {
            NoteAnchors.Point => (1, 1),
            NoteAnchors.Box => (2, 2),
            _ => (3, MostLassoPoints),
        };
        if (marks.Count < least || marks.Count > most)
            return ("anchor.marks", anchor.Kind switch
            {
                NoteAnchors.Point => "a point is one pixel",
                NoteAnchors.Box => "a box is two opposite corners",
                _ => $"a lasso is an outline of 3 to {MostLassoPoints} pixels",
            });
        if (marks.Any(pixel => pixel.X < 0 || pixel.Y < 0 || pixel.X >= anchor.Width || pixel.Y >= anchor.Height))
            return ("anchor.marks", "a mark's pixels lie inside the picture");
        if ((anchor.Columns?.Count ?? 0) > MostColumns)
            return ("anchor.columns", $"an area keeps up to {MostColumns} ground columns");
        if (anchor.Columns?.Any(column => column is not { Length: 3 }) == true)
            return ("anchor.columns", "each ground column is `[x, y, z]`");
        return null;
    }

    public static async Task<Dictionary<string, string>> NamesAsync(PgmDb db, IEnumerable<string> slugs, CancellationToken ct)
    {
        var wanted = slugs.Distinct().ToList();
        return await db.Maps.Where(map => wanted.Contains(map.Slug))
            .ToDictionaryAsync(map => map.Slug, map => map.Name, ct);
    }
}

/// <summary>GET /api/notes — every note on every map, newest change first, for an agent starting on what the
/// author left. <c>status</c> takes one status or several between commas; absent is every note.</summary>
public sealed class NotesAcrossMapsEndpoint(MapNoteStore notes, PgmDb db) : EndpointWithoutRequest<List<MapNoteDto>>
{
    public override void Configure()
    {
        Get("/notes");
        Policies(AccessPolicies.Notes);
        Description(b => b.Produces<List<MapNoteDto>>(200, "application/json"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var asked = (Query<string?>("status", isRequired: false) ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (asked.FirstOrDefault(status => !NoteStatuses.IsValid(status)) is { } unknown)
        {
            await Refusals.UnreadableAsync(HttpContext, "no such status",
                $"'{unknown}' is not a status — one of {string.Join(", ", NoteStatuses.All.Select(entry => entry.Id))}", ct, "status");
            return;
        }
        var found = await notes.AcrossMapsAsync(asked, ct);
        var names = await NoteWire.NamesAsync(db, found.Select(stored => stored.Note.MapSlug), ct);
        await Send.OkAsync([.. found.Select(stored => NoteWire.Dto(stored, names.GetValueOrDefault(stored.Note.MapSlug, stored.Note.MapSlug)))], ct);
    }
}

/// <summary>GET /api/map/{slug}/notes — one map's notes with their threads, newest change first.</summary>
public sealed class MapNotesEndpoint(MapRepository repo, MapNoteStore notes) : EndpointWithoutRequest<List<MapNoteDto>>
{
    public override void Configure()
    {
        Get("/map/{slug}/notes");
        Policies(AccessPolicies.Notes);
        Description(b => b.Produces<List<MapNoteDto>>(200, "application/json").Refuses(404));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;
        await Send.OkAsync([.. (await notes.OfMapAsync(map.Slug, ct)).Select(stored => NoteWire.Dto(stored, map.Name))], ct);
    }
}

/// <summary>POST /api/map/{slug}/notes — a new note: its body, what it is pinned to, an optional tag, the
/// picture it was written on and the revision it was written against. A note an author writes waits for an
/// agent; one an agent writes is a question and waits for the author.</summary>
public sealed class MapNoteCreateEndpoint(MapRepository repo, MapNoteStore notes, NotePictures pictures, Callers callers)
    : Endpoint<MapNoteRequest, MapNoteDto>
{
    public override void Configure()
    {
        Post("/map/{slug}/notes");
        Policies(AccessPolicies.Notes);
        Description(b => b.Produces<MapNoteDto>(200, "application/json").Refuses(400, 404));
    }

    public override async Task HandleAsync(MapNoteRequest request, CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;
        if ((NoteWire.BodyFault(request.Body) ?? NoteWire.AnchorFault(request.Anchor) ?? NoteWire.PictureFault(request.Picture, pictures)
             ?? (NoteTags.IsValid(request.Tag) ? null : ("tag", $"`tag` is one of {string.Join(", ", NoteTags.All)}, or absent")))
            is { } fault)
        {
            await Refusals.UnreadableAsync(HttpContext, "not a note", fault.Message, ct, fault.Field);
            return;
        }

        var caller = await callers.OfAsync(HttpContext, ct);
        var now = DateTime.UtcNow;
        var anchor = request.Anchor;
        var stored = await notes.CreateAsync(new MapNoteRow
        {
            MapSlug = map.Slug,
            AnchorKind = anchor.Kind,
            AnchorJson = NoteWire.AnchorJson(anchor),
            ViewKey = NoteAnchors.OnPicture(anchor.Kind) ? anchor.ViewId : null,
            Tag = request.Tag,
            Status = caller.ViaToken ? NoteStatuses.NeedsInfo : NoteStatuses.Open,
            CreatedAt = now,
            UpdatedAt = now,
        }, NoteWire.Message(caller, request.Body, request.Revision ?? map.Revision, request.Picture, now), ct);
        await Send.OkAsync(NoteWire.Dto(stored, map.Name), ct);
    }
}

/// <summary>POST /api/map/{slug}/notes/{id}/replies — a reply in a thread, optionally carrying a picture. The
/// reply leaves the thread where its <c>status</c> says: an agent answers, asks or declines, and the author's
/// reply hands it back to an agent. A thread is resolved only through <c>PATCH</c>.</summary>
public sealed class NoteReplyEndpoint(MapRepository repo, MapNoteStore notes, NotePictures pictures, Callers callers)
    : Endpoint<NoteReplyRequest, MapNoteDto>
{
    private static readonly string[] Leaves = [NoteStatuses.Open, .. NoteStatuses.AgentReplies];

    public override void Configure()
    {
        Post("/map/{slug}/notes/{id}/replies");
        Policies(AccessPolicies.Notes);
        Description(b => b.Produces<MapNoteDto>(200, "application/json").Refuses(400, 404));
    }

    public override async Task HandleAsync(NoteReplyRequest request, CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;
        if (await notes.GetAsync(map.Slug, Route<long>("id"), ct) is not { } stored)
        {
            await Refusals.NotFoundAsync(HttpContext, "note", ct, Route<string>("id"));
            return;
        }
        if ((NoteWire.BodyFault(request.Body) ?? NoteWire.PictureFault(request.Picture, pictures)
             ?? (request.Status is null || Leaves.Contains(request.Status) ? null
                 : ("status", $"a reply leaves its thread {string.Join(", ", Leaves)} — resolving one is the author's PATCH")))
            is { } fault)
        {
            await Refusals.UnreadableAsync(HttpContext, "not a reply", fault.Message, ct, fault.Field);
            return;
        }

        var caller = await callers.OfAsync(HttpContext, ct);
        var status = request.Status ?? (caller.ViaToken ? NoteStatuses.Answered : NoteStatuses.Open);
        var reply = NoteWire.Message(caller, request.Body, request.Revision ?? map.Revision, request.Picture, DateTime.UtcNow);
        reply.NoteId = stored.Note.Id;
        await notes.AddAsync(reply, status, ct);
        await Send.OkAsync(NoteWire.Dto((await notes.GetAsync(map.Slug, stored.Note.Id, ct))!, map.Name), ct);
    }
}

/// <summary>PATCH /api/map/{slug}/notes/{id} — the author resolves a thread, declines it, reopens it, or
/// changes its tag. 403 to a token: an agent answers, asks or declines in a reply, and only the author closes
/// a thread.</summary>
public sealed class NoteChangeEndpoint(MapRepository repo, MapNoteStore notes, Callers callers)
    : Endpoint<NoteChangeRequest, MapNoteDto>
{
    private static readonly string[] Settable = [NoteStatuses.Resolved, NoteStatuses.WontDo, NoteStatuses.Open];

    public override void Configure()
    {
        Patch("/map/{slug}/notes/{id}");
        Policies(AccessPolicies.Notes);
        Description(b => b.Produces<MapNoteDto>(200, "application/json").Refuses(400, 403, 404));
    }

    public override async Task HandleAsync(NoteChangeRequest request, CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;
        if (await notes.GetAsync(map.Slug, Route<long>("id"), ct) is not { } stored)
        {
            await Refusals.NotFoundAsync(HttpContext, "note", ct, Route<string>("id"));
            return;
        }
        if (await callers.OfAsync(HttpContext, ct) is { ViaToken: true })
        {
            await Refusals.WriteAsync(HttpContext, 403, "not permitted",
                [new Finding(RequestRules.NotPermitted,
                    "only the author resolves, declines or reopens a thread — an agent answers, asks or declines "
                    + "with its reason in a reply", Field: "status")], ct);
            return;
        }
        if (request.Status is { } status && !Settable.Contains(status))
        {
            await Refusals.UnreadableAsync(HttpContext, "not a change",
                $"a thread is set {string.Join(", ", Settable)} — the other statuses are left by a reply", ct, "status");
            return;
        }
        if (request.Tag is { Length: > 0 } tag && !NoteTags.IsValid(tag))
        {
            await Refusals.UnreadableAsync(HttpContext, "not a change",
                $"`tag` is one of {string.Join(", ", NoteTags.All)}, or empty to clear it", ct, "tag");
            return;
        }

        var tagged = request.Tag is null ? stored.Note.Tag : request.Tag.Length == 0 ? null : request.Tag;
        await notes.ChangeAsync(stored.Note.Id, request.Status ?? stored.Note.Status, tagged, DateTime.UtcNow, ct);
        await Send.OkAsync(NoteWire.Dto((await notes.GetAsync(map.Slug, stored.Note.Id, ct))!, map.Name), ct);
    }
}

/// <summary>POST /api/notes/pictures — keep a picture for a note, answering the hash that names it. The body
/// is the picture itself, a WebP or a PNG, up to <see cref="NotePictures.Largest"/> bytes. The same bytes
/// always answer the same hash, and are kept once.</summary>
public sealed class NotePictureKeepEndpoint(NotePictures pictures) : EndpointWithoutRequest<NotePictureDto>
{
    public override void Configure()
    {
        Post("/notes/pictures");
        Policies(AccessPolicies.Notes);
        Description(b => b.Accepts<byte[]>("image/webp", "image/png", "application/octet-stream")
            .Produces<NotePictureDto>(200, "application/json").Refuses(400));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        var body = HttpContext.Request.Body;
        var chunk = new byte[81_920];
        int read;
        while ((read = await body.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > NotePictures.Largest)
            {
                await Refusals.UnreadableAsync(HttpContext, "not a picture",
                    $"a note's picture is up to {NotePictures.Largest / (1024 * 1024)} MB", ct);
                return;
            }
            buffer.Write(chunk, 0, read);
        }
        var bytes = buffer.ToArray();
        if (NotePictures.KindOf(bytes) is null)
        {
            await Refusals.UnreadableAsync(HttpContext, "not a picture",
                "a note's picture is a WebP or a PNG, sent as the body itself", ct);
            return;
        }
        await Send.OkAsync(new NotePictureDto(await pictures.SaveAsync(bytes, ct), bytes.LongLength), ct);
    }
}

/// <summary>GET /api/notes/pictures/{hash} — a note's picture. It never changes under its hash, so it is
/// answered with a year's private cache.</summary>
public sealed class NotePictureEndpoint(NotePictures pictures) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/notes/pictures/{hash}");
        Policies(AccessPolicies.Notes);
        Description(b => b.Produces(200, typeof(byte[]), "image/webp", "image/png").Refuses(404));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var hash = Route<string>("hash") ?? "";
        if (pictures.Find(hash) is not { } found)
        {
            await Refusals.NotFoundAsync(HttpContext, "picture", ct, hash);
            return;
        }
        HttpContext.Response.ContentType = found.ContentType;
        HttpContext.Response.Headers.CacheControl = "private, max-age=31536000, immutable";
        await HttpContext.Response.SendFileAsync(found.Path, ct);
    }
}
