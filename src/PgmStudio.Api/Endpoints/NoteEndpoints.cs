using System.Globalization;
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
// The author leaves a note pinned to the place it is about, in the Sketch tool's Review phase; an agent
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
        stored.Note.Status, stored.Note.CreatedAt, stored.Note.UpdatedAt,
        [.. stored.Messages.Select(message => new NoteMessageDto(
            message.Id, message.AuthorName, message.AuthorUuid, message.TokenLabel, message.Body, message.Change,
            message.Picture, message.CreatedAt,
            message.MarkJson is { } mark ? JsonSerializer.Deserialize<NoteAnchorDto>(mark, MapArtifactStore.Json) : null))]);

    public static string AnchorJson(NoteAnchorDto anchor) => JsonSerializer.Serialize(anchor, MapArtifactStore.Json);

    /// <summary>A message as <paramref name="caller"/> writes it, with the mark a reply carries.</summary>
    public static MapNoteMessageRow Message(Caller caller, string body, long change, string? picture, DateTime at,
                                            NoteAnchorDto? mark = null) => new()
    {
        AuthorUuid = caller.Uuid,
        AuthorName = caller.Name is { Length: > 0 } name ? name : "local",
        TokenLabel = caller.Token,
        Body = body.Trim(),
        Change = change,
        Picture = picture,
        MarkJson = mark is null ? null : AnchorJson(mark),
        CreatedAt = at,
    };

    /// <summary>What is wrong with a message's body, or null.</summary>
    public static (string Field, string Message)? BodyFault(string? body) =>
        string.IsNullOrWhiteSpace(body) ? ("body", "a note says something — `body` is empty")
        : body.Length > LongestBody ? ("body", $"`body` is {body.Length} characters, and a note holds {LongestBody}")
        : null;

    /// <summary>What is wrong with the change a message states it was written at, or null: one past the map's
    /// latest has not landed.</summary>
    public static (string Field, string Message)? ChangeFault(long? change, long latest) =>
        change is < 0 ? ("change", $"`change` is {change}, and changes are numbered from 1")
        : change > latest ? ("change", $"change {change} has not landed — the latest change to this map is {latest}")
        : null;

    /// <summary>What is wrong with a picture a message names, or null.</summary>
    public static (string Field, string Message)? PictureFault(string? picture, NotePictures pictures) =>
        picture is null ? null
        : !pictures.Has(picture) ? ("picture", $"no picture is kept under '{picture}' — post it to /api/notes/pictures first, "
            + "and name the hash that answers")
        : null;

    /// <summary>What is wrong with a reply's mark, or null: it is a mark on a picture — a point, a box or a lasso —
    /// held to everything a note's anchor of that kind is.</summary>
    public static (string Field, string Message)? MarkFault(NoteAnchorDto? mark) =>
        mark is null ? null
        : !NoteAnchors.Marks(mark.Kind) ? ("mark.kind", $"a reply's mark is a {NoteAnchors.Point}, a {NoteAnchors.Box} or a {NoteAnchors.Lasso}")
        : AnchorFault(mark) is { } fault ? ("mark" + fault.Field["anchor".Length..], fault.Message)
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
        if ((anchor.OverVoid?.Count ?? 0) > MostColumns)
            return ("anchor.overVoid", $"an area keeps up to {MostColumns} columns over the void");
        if (anchor.OverVoid?.Any(column => column is not { Length: 3 }) == true)
            return ("anchor.overVoid", "each column over the void is `[x, y, z]`");
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
/// author left. <c>status</c> takes one status or several between commas; absent is every note. <c>since</c>
/// keeps the threads that moved at or after an instant, which is how a scheduled check asks what is new.</summary>
public sealed class NotesAcrossMapsEndpoint(MapNoteStore notes, PgmDb db) : EndpointWithoutRequest<List<MapNoteDto>>
{
    public override void Configure()
    {
        Get("/notes");
        Policies(AccessPolicies.Notes);
        Description(b => b.Produces<List<MapNoteDto>>(200, "application/json").Reads(
            new QueryWord("status", "One status, or several between commas: "
                + $"{string.Join(", ", NoteStatuses.All.Select(entry => $"`{entry.Id}`"))}. Absent is every note, and "
                + "a word that is not a status is refused."),
            new QueryWord("since", "An instant in ISO 8601, `2026-10-02T08:00:00Z`: only the threads whose last message "
                + "or status change is at or after it. Absent is every thread; an instant that does not read is refused.")));
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
        DateTime? since = null;
        if (Query<string?>("since", isRequired: false) is { Length: > 0 } stated)
        {
            if (!DateTimeOffset.TryParse(stated, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var instant))
            {
                await Refusals.UnreadableAsync(HttpContext, "no such instant",
                    $"'{stated}' is not an instant — ISO 8601, such as 2026-10-02T08:00:00Z", ct, "since");
                return;
            }
            since = instant.UtcDateTime;
        }
        var found = await notes.AcrossMapsAsync(asked, since, ct);
        var names = await NoteWire.NamesAsync(db, found.Select(stored => stored.Note.MapSlug), ct);
        await Send.OkAsync([.. found.Select(stored => NoteWire.Dto(stored, names.GetValueOrDefault(stored.Note.MapSlug, stored.Note.MapSlug)))], ct);
    }
}

/// <summary>Where the author's open notes stand with the agent: how many wait, how many since the last hand-off.</summary>
internal static class NoteHandoffs
{
    public static async Task<(NoteHandoffDto Standing, List<StoredNote> Open)> StandingAsync(
        MapNoteStore notes, AgentHandoff agent, CancellationToken ct)
    {
        var open = await notes.AcrossMapsAsync([NoteStatuses.Open], ct: ct);
        var last = agent.Last;
        var fresh = open.Count(stored => last is null || stored.Note.UpdatedAt > last.At);
        return (new NoteHandoffDto(agent.Ready, open.Count, fresh, last?.At, last?.Session), open);
    }
}

/// <summary>GET /api/notes/handoff — whether this studio names an agent, how many notes wait for one, and the last
/// hand-off.</summary>
public sealed class NoteHandoffStandingEndpoint(MapNoteStore notes, AgentHandoff agent) : EndpointWithoutRequest<NoteHandoffDto>
{
    public override void Configure()
    {
        Get("/notes/handoff");
        Policies(AccessPolicies.Notes);
        Description(b => b.Produces<NoteHandoffDto>(200, "application/json"));
    }

    public override async Task HandleAsync(CancellationToken ct) =>
        await Send.OkAsync((await NoteHandoffs.StandingAsync(notes, agent, ct)).Standing, ct);
}

/// <summary>POST /api/notes/handoff — the author hands the open notes to the agent, which starts one session that
/// answers them. Refused where nothing was written since the last hand-off unless <c>again</c> asks to repeat it,
/// 403 to a token — the author hands notes over and an agent answers them — and 503 where this studio names no
/// agent or the service that starts one refused.</summary>
public sealed class NoteHandoffEndpoint(MapNoteStore notes, AgentHandoff agent, Callers callers, PgmDb db)
    : Endpoint<NoteHandoffRequest, NoteHandoffDto>
{
    public override void Configure()
    {
        Post("/notes/handoff");
        Policies(AccessPolicies.Notes);
        Description(b => b.Produces<NoteHandoffDto>(200, "application/json").Refuses(403, 409, 503));
    }

    public override async Task HandleAsync(NoteHandoffRequest request, CancellationToken ct)
    {
        if (await callers.OfAsync(HttpContext, ct) is { ViaToken: true })
        {
            await Refusals.WriteAsync(HttpContext, 403, "not permitted",
                [new Finding(RequestRules.NotPermitted, "the author hands notes to an agent, and an agent answers them")], ct);
            return;
        }
        if (!agent.Ready)
        {
            await Refusals.WriteAsync(HttpContext, 503, "no agent",
                [new Finding(RequestRules.AgentUnavailable,
                    "this studio names no agent to hand notes to — set Notes:Agent:Fire and Notes:Agent:Token")], ct);
            return;
        }
        var (standing, open) = await NoteHandoffs.StandingAsync(notes, agent, ct);
        if (standing.Waiting == 0 || (standing.Fresh == 0 && !request.Again))
        {
            await Refusals.WriteAsync(HttpContext, 409, "nothing to hand over",
                [new Finding(RequestRules.Conflict, standing.Waiting == 0
                    ? "no note is waiting for an agent"
                    : $"every open note was handed over at {standing.HandedAt:HH:mm} UTC and none was written since — "
                      + "`again` hands them over once more")], ct);
            return;
        }

        var names = await NoteWire.NamesAsync(db, open.Select(stored => stored.Note.MapSlug), ct);
        var maps = open.GroupBy(stored => stored.Note.MapSlug)
            .Select(map => $"{names.GetValueOrDefault(map.Key, map.Key)} ({map.Key}): {map.Count()}");
        var text = $"{standing.Waiting} open note(s), {standing.Fresh} written or answered since the last hand-off, "
            + $"on {string.Join("; ", maps)}. Read them with GET /api/notes?status=open.";
        var (_, why) = await agent.HandAsync(text, ct);
        if (why is not null)
        {
            await Refusals.WriteAsync(HttpContext, 503, "no agent", [new Finding(RequestRules.AgentUnavailable, why)], ct);
            return;
        }
        await Send.OkAsync((await NoteHandoffs.StandingAsync(notes, agent, ct)).Standing, ct);
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

/// <summary>POST /api/map/{slug}/notes — a new note: its body, what it is pinned to, the
/// picture it was written on and the change it was written at. A note an author writes waits for an agent; one
/// an agent writes is a question and waits for the author.</summary>
public sealed class MapNoteCreateEndpoint(
    MapRepository repo, MapNoteStore notes, NotePictures pictures, Callers callers, MapChangeLog log)
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
        var latest = await log.LatestAsync(map.Slug, ct);
        if ((NoteWire.BodyFault(request.Body) ?? NoteWire.AnchorFault(request.Anchor) ?? NoteWire.PictureFault(request.Picture, pictures)
             ?? NoteWire.ChangeFault(request.Change, latest))
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
            Status = caller.ViaToken ? NoteStatuses.NeedsInfo : NoteStatuses.Open,
            CreatedAt = now,
            UpdatedAt = now,
        }, NoteWire.Message(caller, request.Body, request.Change ?? latest, request.Picture, now), ct);
        await Send.OkAsync(NoteWire.Dto(stored, map.Name), ct);
    }
}

/// <summary>POST /api/map/{slug}/notes/{id}/replies — a reply in a thread, optionally carrying a picture and a
/// mark of its own on a picture, held to what a note's point, box or lasso anchor is. The
/// reply leaves the thread where its <c>status</c> says: an agent answers, asks or declines, and the author's
/// reply hands it back to an agent. A thread is resolved only through <c>PATCH</c>. An agent's answer to a note
/// written on a picture, at the board's latest change and stating no picture of its own, carries the note's
/// camera drawn over the board as stored: the after to the note's before.</summary>
public sealed class NoteReplyEndpoint(
    MapRepository repo, MapNoteStore notes, NotePictures pictures, Callers callers, MapChangeLog log,
    MapReader reader, MapArtifactStore artifacts, BlockTextureStore textures, BuildQueue queue)
    : Endpoint<NoteReplyRequest, MapNoteDto>
{
    private static readonly string[] Leaves = [NoteStatuses.Open, .. NoteStatuses.AgentReplies];

    public override void Configure()
    {
        Post("/map/{slug}/notes/{id}/replies");
        Policies(AccessPolicies.Notes);
        Description(b => b.Produces<MapNoteDto>(200, "application/json").Refuses(400, 404, 429));
    }

    public override async Task HandleAsync(NoteReplyRequest request, CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;
        if (await notes.GetAsync(map.Slug, Route<long>("id"), ct) is not { } stored)
        {
            await Refusals.NotFoundAsync(HttpContext, "note", ct, Route<string>("id"));
            return;
        }
        var latest = await log.LatestAsync(map.Slug, ct);
        if ((NoteWire.BodyFault(request.Body) ?? NoteWire.PictureFault(request.Picture, pictures)
             ?? NoteWire.MarkFault(request.Mark)
             ?? NoteWire.ChangeFault(request.Change, latest)
             ?? (request.Status is null || Leaves.Contains(request.Status) ? null
                 : ("status", $"a reply leaves its thread {string.Join(", ", Leaves)} — resolving one is the author's PATCH")))
            is { } fault)
        {
            await Refusals.UnreadableAsync(HttpContext, "not a reply", fault.Message, ct, fault.Field);
            return;
        }

        var caller = await callers.OfAsync(HttpContext, ct);
        var status = request.Status ?? (caller.ViaToken ? NoteStatuses.Answered : NoteStatuses.Open);
        var picture = request.Picture;
        var anchor = JsonSerializer.Deserialize<NoteAnchorDto>(stored.Note.AnchorJson, MapArtifactStore.Json);
        if (caller.ViaToken && status == NoteStatuses.Answered && picture is null
            && (request.Change ?? latest) == latest && anchor is { Camera: not null })
        {
            using var turn = await queue.TurnOfAsync(HttpContext);
            if (turn is null)
            {
                await BuildQueue.RefuseBusyAsync(HttpContext);
                return;
            }
            picture = await AfterAsync(map, anchor, ct);
        }
        var reply = NoteWire.Message(caller, request.Body, request.Change ?? latest, picture, DateTime.UtcNow, request.Mark);
        reply.NoteId = stored.Note.Id;
        await notes.AddAsync(reply, status, ct);
        await Send.OkAsync(NoteWire.Dto((await notes.GetAsync(map.Slug, stored.Note.Id, ct))!, map.Name), ct);
    }

    /// <summary>The note's own camera drawn over the board as stored and kept, answering the picture's hash; null
    /// where none can be drawn here — no block textures, no world, or a camera that finds nothing.</summary>
    private async Task<string?> AfterAsync(MapRow map, NoteAnchorDto anchor, CancellationToken ct)
    {
        if (anchor.Camera is not { } camera) return null;
        var (set, _) = await textures.GetAsync(ct);
        if (set is null || await WorldReads.LoadAsync(map, reader, artifacts, ct) is not { } read) return null;
        var words = new Dictionary<string, string>
        {
            ["eye"] = string.Create(CultureInfo.InvariantCulture, $"{camera.X},{camera.Y},{camera.Z}"),
            ["yaw"] = camera.Yaw.ToString(CultureInfo.InvariantCulture),
            ["pitch"] = camera.Pitch.ToString(CultureInfo.InvariantCulture),
            ["fov"] = camera.Fov.ToString(CultureInfo.InvariantCulture),
            ["width"] = (anchor.Width ?? 1280).ToString(CultureInfo.InvariantCulture),
            ["height"] = (anchor.Height ?? 720).ToString(CultureInfo.InvariantCulture),
        };
        EyeShot? shot;
        using (await EyeRenders.TurnAsync(ct))
            shot = EyeReadEndpoint.Shot(read.Built, set, EyeAim.Read(word => words.GetValueOrDefault(word)));
        return shot is null ? null : await pictures.SaveAsync(shot.Png, ct);
    }
}

/// <summary>PATCH /api/map/{slug}/notes/{id} — the author resolves a thread, declines it or reopens it. 403 to a
/// token: an agent answers, asks or declines in a reply, and only the author closes
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
        if (!Settable.Contains(request.Status))
        {
            await Refusals.UnreadableAsync(HttpContext, "not a change",
                $"a thread is set {string.Join(", ", Settable)} — the other statuses are left by a reply", ct, "status");
            return;
        }
        await notes.ChangeAsync(stored.Note.Id, request.Status, DateTime.UtcNow, ct);
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
