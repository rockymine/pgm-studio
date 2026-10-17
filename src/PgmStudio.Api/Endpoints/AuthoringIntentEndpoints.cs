using System.Text;
using System.Text.Json;
using FastEndpoints;
using PgmStudio.Api.Services;
using PgmStudio.Contracts;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Endpoints;

using Dict = Dictionary<string, object?>;

/// <summary>GET /api/map/{slug}/intent — the map's declarative authoring intent
/// (docs/pgm/new-map-authoring.md), empty if none yet.</summary>
public sealed class IntentGetEndpoint(MapRepository repo, MapArtifactStore artifacts) : EndpointWithoutRequest<MapIntent>
{
    public override void Configure() { Get("/map/{slug}/intent"); Description(b => b.Refuses(404)); }
    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;
        if (await artifacts.RevisionAsync(map.Id, ArtifactKind.MapIntentJson, ct) is { } revision)
            Revisions.Answer(HttpContext, revision);
        await Send.OkAsync(await artifacts.LoadJsonOrEmptyAsync<MapIntent>(map.Id, ArtifactKind.MapIntentJson, ct), ct);
    }
}

/// <summary>GET /api/map/{slug}/intent/pieces — every play piece of the map's intent where the sketch draws it,
/// at the rectangle the piece route reads it as standing on.</summary>
public sealed class IntentPiecesEndpoint(MapRepository repo, MapArtifactStore artifacts)
    : EndpointWithoutRequest<List<PlayPieceDto>>
{
    public override void Configure() { Get("/map/{slug}/intent/pieces"); Description(b => b.Refuses(404)); }
    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;
        var intent = await artifacts.LoadJsonOrEmptyAsync<MapIntent>(map.Id, ArtifactKind.MapIntentJson, ct);
        await Send.OkAsync([.. PlayPieces.Of(intent).Select(piece => new PlayPieceDto(
            piece.Id, piece.Kind, piece.Color, piece.Rect.MinX, piece.Rect.MinZ, piece.Rect.MaxX, piece.Rect.MaxZ,
            piece.Carried, piece.Line))], ct);
    }
}

/// <summary>POST /api/map/{slug}/intent/pieces — add one play piece and every image the map's symmetry draws of
/// it, answering the id of its authored image. <see cref="PlayPieceAdd"/> holds what each kind is given.</summary>
public sealed class IntentPieceAddEndpoint(
    MapRepository repo, MapReader reader, MapWriter writer, MapArtifactStore artifacts, FeatureData features,
    PlayerLookup players)
    : Endpoint<PieceAddRequest>
{
    public override void Configure()
    {
        Post("/map/{slug}/intent/pieces");
        Description(b => b.Produces<PartWrittenDto>(200, "application/json").Refuses(400, 404, 409, 422));
    }

    public override async Task HandleAsync(PieceAddRequest req, CancellationToken ct)
    {
        var slug = Route<string>("slug")!;
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;
        var intent = await artifacts.LoadJsonOrEmptyAsync<MapIntent>(map.Id, ArtifactKind.MapIntentJson, ct);
        var board = await IntentPieceMoveEndpoint.BoardAsync(artifacts, map.Id, ct);
        var rect = new[] { req.MinX, req.MinZ, req.MaxX, req.MaxZ };
        var wall = req.Kind == PlayPieceKinds.Wall;
        if (wall ? req.Ends is not { Length: 4 } || rect.Any(bound => bound is not null) : rect.Any(bound => bound is null) || req.Ends is not null)
        {
            await Refusals.StopAsync(HttpContext, 400, "the piece cannot be added", Findings.Of(new Finding(
                RequestRules.Unreadable, wall
                    ? "a wall states its two `ends` as [x1, z1, x2, z2], and no rectangle"
                    : "the request states all four bounds of a placed rectangle, and no `ends`",
                Field: wall ? "ends" : "minX")), ct);
            return;
        }
        var (added, id, refused) = wall
            ? PlayPieceAdd.Wall(intent, req.Ends![0], req.Ends[1], req.Ends[2], req.Ends[3], board)
            : PlayPieceAdd.Apply(intent, req.Kind, new Rect(req.MinX!.Value, req.MinZ!.Value, req.MaxX!.Value, req.MaxZ!.Value),
                                 req.Team, board);
        if (added is null)
        {
            await Refusals.StopAsync(HttpContext, 400, "the piece cannot be added", refused, ct);
            return;
        }
        var applied = await IntentWrite.StoreAndProjectAsync(
            repo, reader, writer, artifacts, features, players, slug, map.Id,
            JsonSerializer.Serialize(added, MapArtifactStore.Json), Revisions.Expected(HttpContext), ct);
        if (applied.Refusal is not null) { await Send.ResponseAsync(applied.Body(HttpContext), applied.Status(), ct); return; }
        await Send.OkAsync(new PartWrittenDto(id), ct);
    }
}

/// <summary>PATCH /api/map/{slug}/intent/pieces/{id}/settings — set one of a play piece's settings.
/// <see cref="PlayPieceSettings"/> holds which a kind has and which every image shares.</summary>
public sealed class IntentPieceSettingEndpoint(
    MapRepository repo, MapReader reader, MapWriter writer, MapArtifactStore artifacts, FeatureData features,
    PlayerLookup players)
    : Endpoint<PieceSettingRequest>
{
    public override void Configure()
    {
        Patch("/map/{slug}/intent/pieces/{id}/settings");
        Description(b => b.Produces<AppliedDto>(200, "application/json").Refuses(400, 404, 409, 422));
    }

    public override async Task HandleAsync(PieceSettingRequest req, CancellationToken ct)
    {
        var slug = Route<string>("slug")!;
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;
        var intent = await artifacts.LoadJsonOrEmptyAsync<MapIntent>(map.Id, ArtifactKind.MapIntentJson, ct);
        var (set, refused) = PlayPieceSettings.Apply(intent, Route<string>("id")!, req.Key, new PieceValue(req.Text, req.Number, req.Flag), req.Alone,
                                                     await IntentPieceMoveEndpoint.BoardAsync(artifacts, map.Id, ct));
        if (set is null)
        {
            var missing = refused.Any(finding => finding.Rule == RequestRules.NoSuchSubject);
            await Refusals.StopAsync(HttpContext, missing ? 404 : 400,
                                     missing ? "no such piece" : "the setting cannot be made", refused, ct);
            return;
        }
        var applied = await IntentWrite.StoreAndProjectAsync(
            repo, reader, writer, artifacts, features, players, slug, map.Id,
            JsonSerializer.Serialize(set, MapArtifactStore.Json), Revisions.Expected(HttpContext), ct);
        await Send.ResponseAsync(applied.Body(HttpContext), applied.Status(), ct);
    }
}

/// <summary>PUT /api/map/{slug}/intent/pieces/{id}/house — set a room's house whole, on every image of its unit
/// unless <c>alone</c>. <see cref="PlayPieceHouse"/> holds what it refuses.</summary>
public sealed class IntentPieceHouseEndpoint(
    MapRepository repo, MapReader reader, MapWriter writer, MapArtifactStore artifacts, FeatureData features,
    PlayerLookup players)
    : Endpoint<PieceHouseRequest>
{
    public override void Configure()
    {
        Put("/map/{slug}/intent/pieces/{id}/house");
        Description(b => b.Produces<AppliedDto>(200, "application/json").Refuses(400, 404, 409, 422));
    }

    public override async Task HandleAsync(PieceHouseRequest req, CancellationToken ct)
    {
        var slug = Route<string>("slug")!;
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;
        RoomHouse? house;
        try { house = req.House.Deserialize<RoomHouse>(MapArtifactStore.Json); }
        catch (JsonException) { house = null; }
        if (house is null)
        {
            await Refusals.UnreadableAsync(HttpContext, "not a house", "the request's `house` is not a house", ct, "house");
            return;
        }
        var intent = await artifacts.LoadJsonOrEmptyAsync<MapIntent>(map.Id, ArtifactKind.MapIntentJson, ct);
        var (set, refused) = PlayPieceHouse.Apply(intent, Route<string>("id")!, house, req.Alone,
                                                  await IntentPieceMoveEndpoint.BoardAsync(artifacts, map.Id, ct));
        if (set is null)
        {
            var missing = refused.Any(finding => finding.Rule == RequestRules.NoSuchSubject);
            await Refusals.StopAsync(HttpContext, missing ? 404 : 400,
                                     missing ? "no such piece" : "the house cannot be built", refused, ct);
            return;
        }
        var applied = await IntentWrite.StoreAndProjectAsync(
            repo, reader, writer, artifacts, features, players, slug, map.Id,
            JsonSerializer.Serialize(set, MapArtifactStore.Json), Revisions.Expected(HttpContext), ct);
        await Send.ResponseAsync(applied.Body(HttpContext), applied.Status(), ct);
    }
}

/// <summary>DELETE /api/map/{slug}/intent/pieces/{id} — take one play piece out of the intent, and every image of
/// it unless <c>?alone=true</c>.</summary>
public sealed class IntentPieceDeleteEndpoint(
    MapRepository repo, MapReader reader, MapWriter writer, MapArtifactStore artifacts, FeatureData features,
    PlayerLookup players)
    : EndpointWithoutRequest
{
    public override void Configure()
    {
        Delete("/map/{slug}/intent/pieces/{id}");
        Description(b => b.Reads(new QueryWord("alone",
                "Take out the named image only, keeping the other images the symmetry draws of it.",
                Value: QueryValue.Flag))
            .Produces<AppliedDto>(200, "application/json").Refuses(400, 404, 409, 422));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var slug = Route<string>("slug")!;
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;
        var intent = await artifacts.LoadJsonOrEmptyAsync<MapIntent>(map.Id, ArtifactKind.MapIntentJson, ct);
        var alone = Query<bool>("alone", isRequired: false);
        var (kept, refused) = PlayPieceAdd.Delete(intent, Route<string>("id")!, alone,
                                                  await IntentPieceMoveEndpoint.BoardAsync(artifacts, map.Id, ct));
        if (kept is null)
        {
            var missing = refused.Any(finding => finding.Rule == RequestRules.NoSuchSubject);
            await Refusals.StopAsync(HttpContext, missing ? 404 : 400,
                                     missing ? "no such piece" : "the piece cannot be deleted", refused, ct);
            return;
        }
        var applied = await IntentWrite.StoreAndProjectAsync(
            repo, reader, writer, artifacts, features, players, slug, map.Id,
            JsonSerializer.Serialize(kept, MapArtifactStore.Json), Revisions.Expected(HttpContext), ct);
        await Send.ResponseAsync(applied.Body(HttpContext), applied.Status(), ct);
    }
}

/// <summary>PUT /api/map/{slug}/intent — store the intent the author edited and regenerate the map from
/// it. Replaces the stored intent wholesale, which is what makes a deletion in Configure stick.</summary>
public sealed class IntentPutEndpoint(MapRepository repo, MapReader reader, MapWriter writer, MapArtifactStore artifacts, FeatureData features, PlayerLookup players) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Put("/map/{slug}/intent");
        Description(b => b.Accepts<MapIntent>("application/json").Produces<AppliedDto>(200, "application/json").Refuses(404, 409, 422));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var slug = Route<string>("slug")!;
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;

        var body = await RawBody.ReadAsync(HttpContext, ct);
        Complaints.Unread(HttpContext, body, IntentWrite.Stated(body));

        var applied = await IntentWrite.StoreAndProjectAsync(repo, reader, writer, artifacts, features, players, slug,
            map.Id, body, Revisions.Expected(HttpContext), ct);
        // A wool whose teams contradict each other or the islands is stored, and said so (OB34, OB35): Configure
        // passes through such a state on its way to the right one.
        if (applied.Status() == 200 && IntentWrite.Stated(body) is { } stated)
            Complaints.Add(HttpContext, WoolTeams.Check(stated, await IslandTeamLookup.OfAsync(features, map.Id, stated, ct)));
        await Send.ResponseAsync(applied.Body(HttpContext), applied.Status(), ct);
    }
}

/// <summary>
/// PUT /api/map/{slug}/intent/from-plan — store a freshly compiled intent, carrying the map's authored
/// slices onto it first (<see cref="IntentCarry"/>), then project it exactly as a normal PUT does.
///
/// <para>The plan owns the map's structure, so a rebuild is meant to replace its teams, spawns, wools and
/// build zones. What it does not own is who wrote the map, and two separate things keep that true. The
/// projection leaves the map's people alone where the intent names none, which is the rule for every intent
/// write and not only this one. And <see cref="IntentCarry.CarryAuthored"/> copies <c>meta.authors</c> and
/// <c>meta.contributors</c> off the stored intent onto the compiled one, so the artifact this route stores
/// still says who the map is by — the projection would keep them either way, and an intent that disagreed
/// with the map about its own credits is the thing being avoided. <b>The confirmed symmetry and the island-team tags are deliberately NOT carried</b>
/// — see <see cref="IntentCarry"/> for why each would do harm rather than preserve an answer — so a rebuild
/// clears both and Configure's World and Teams phases are walked again. The layout write is the same shape for
/// a different reason (<c>…/sketch/from-plan</c>), where the finish does ride across.</para>
/// </summary>
public sealed class IntentFromPlanEndpoint(MapRepository repo, MapReader reader, MapWriter writer, MapArtifactStore artifacts, FeatureData features, PlayerLookup players) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Put("/map/{slug}/intent/from-plan");
        Description(b => b.Accepts<MapIntent>("application/json").Produces<AppliedDto>(200, "application/json").Refuses(404, 409, 422));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var slug = Route<string>("slug")!;
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;

        var compiled = await RawBody.ReadAsync(HttpContext, ct);
        // Over the posted document rather than the merged one: the carry only adds back this map's own
        // credits, so a field named here is one the caller wrote and can correct.
        Complaints.Unread(HttpContext, compiled, IntentWrite.Stated(compiled));

        var stored = await artifacts.LoadAsync(map.Id, ArtifactKind.MapIntentJson, ct);
        var merged = IntentCarry.CarryAuthored(compiled, stored is null ? null : Encoding.UTF8.GetString(stored));

        var applied = await IntentWrite.StoreAndProjectAsync(repo, reader, writer, artifacts, features, players, slug,
            map.Id, merged, Revisions.Expected(HttpContext), ct);
        await Send.ResponseAsync(applied.Body(HttpContext), applied.Status(), ct);
    }
}

/// <summary>
/// PATCH /api/map/{slug}/intent/pieces/{id} — move one play piece, and every orbit image of it, where the
/// sketch just dragged it, or turn a room or an area in place. <see cref="PlayPieceMove"/> holds what that means and what refuses it; this route
/// reads the stored intent, hands it over, and stores and projects the answer the way an ordinary intent PUT
/// does, so the map.xml the move implies is rewritten in the same call.
/// </summary>
public sealed class IntentPieceMoveEndpoint(
    MapRepository repo, MapReader reader, MapWriter writer, MapArtifactStore artifacts, FeatureData features,
    PlayerLookup players)
    : Endpoint<PieceMoveRequest>
{
    public override void Configure()
    {
        Patch("/map/{slug}/intent/pieces/{id}");
        Description(b => b.Produces<AppliedDto>(200, "application/json").Refuses(400, 404, 409, 422));
    }

    public override async Task HandleAsync(PieceMoveRequest req, CancellationToken ct)
    {
        var slug = Route<string>("slug")!;
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;

        var intent = await artifacts.LoadJsonOrEmptyAsync<MapIntent>(map.Id, ArtifactKind.MapIntentJson, ct);
        var id = Route<string>("id")!;
        var bounds = new[] { req.MinX, req.MinZ, req.MaxX, req.MaxZ };
        var placed = bounds.All(bound => bound is not null);
        if (placed == (req.Turn is not null) || (!placed && bounds.Any(bound => bound is not null)))
        {
            await Refusals.StopAsync(HttpContext, 400, "the move cannot be made", Findings.Of(new Finding(
                RequestRules.Unreadable, "the request states neither all four bounds of a placed rectangle nor a "
                + "`turn`, or states both", Field: "turn")), ct);
            return;
        }
        var board = await BoardAsync(artifacts, map.Id, ct);
        var (moved, refused) = placed
            ? PlayPieceMove.Apply(intent, id, req.Part, new Rect(req.MinX!.Value, req.MinZ!.Value, req.MaxX!.Value,
                                                                 req.MaxZ!.Value), req.Alone, board, req.Wing)
            : PlayPieceMove.Turn(intent, id, req.Part, req.Turn!.Value, req.Alone, board);

        // 404 where the id names nothing, 400 where the move itself cannot be made: one names a
        // subject the studio does not hold and the other a request it cannot act on, and only the second is
        // something the caller can fix by asking differently.
        if (moved is null)
        {
            var missing = refused.Any(finding => finding.Rule == RequestRules.NoSuchSubject);
            await Refusals.StopAsync(HttpContext, missing ? 404 : 400,
                                     missing ? "no such piece" : "the move cannot be made", refused, ct);
            return;
        }

        var applied = await IntentWrite.StoreAndProjectAsync(
            repo, reader, writer, artifacts, features, players, slug, map.Id,
            JsonSerializer.Serialize(moved, MapArtifactStore.Json), Revisions.Expected(HttpContext), ct);
        await Send.ResponseAsync(applied.Body(HttpContext), applied.Status(), ct);
    }

    /// <summary>The symmetry the map's sketch is drawn through, which a piece route takes where the intent
    /// states none.</summary>
    internal static async Task<SymmetryIntent?> BoardAsync(MapArtifactStore artifacts, long mapId, CancellationToken ct) =>
        await artifacts.LoadLayoutAsync(mapId, ct) is { Length: > 0 } stored
            ? PgmStudio.Pgm.Sketch.SketchLayout.Parse(Encoding.UTF8.GetString(stored))?.Setup?.Board
            : null;
}
