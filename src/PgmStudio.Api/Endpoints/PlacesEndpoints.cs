using System.Text;
using System.Text.Json;
using FastEndpoints;
using PgmStudio.Api.Access;
using PgmStudio.Api.Services;
using PgmStudio.Contracts;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Domain;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Endpoints;

/// <summary>GET /api/map/{slug}/places — the map's places as stored, or <c>{}</c> where it has none. The
/// <c>ETag</c> is the revision to state on the next write.</summary>
public sealed class PlacesGetEndpoint(MapRepository repo, MapArtifactStore artifacts) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/map/{slug}/places");
        Description(b => b.Produces<PlacesDocument>(200, "application/json").Refuses(404));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;
        var data = await artifacts.LoadAsync(map.Id, ArtifactKind.PlacesJson, ct);
        if (await artifacts.RevisionAsync(map.Id, ArtifactKind.PlacesJson, ct) is { } revision)
            Revisions.Answer(HttpContext, revision);
        await Send.OkAsync(JsonSerializer.Deserialize<JsonElement>(data ?? "{}"u8.ToArray()), ct);
    }
}

/// <summary>PUT /api/map/{slug}/places — replace the map's places, as one change in its history: an admin's, or a
/// token's carrying the notes permission, the same people the places' threads are with. Refused where a
/// place has no id or shares one, its shape is not one or carries the wrong number of points, or it holds a play
/// piece the map has none of (<see cref="PlacesCheck"/>).</summary>
public sealed class PlacesPutEndpoint(MapRepository repo, MapArtifactStore artifacts) : EndpointWithoutRequest<AppliedDto>
{
    public override void Configure()
    {
        Put("/map/{slug}/places");
        Policies(AccessPolicies.Notes);
        Description(b => b.Accepts<PlacesDocument>("application/json").Refuses(400, 404, 409).Reads(
            new QueryWord("note", "What this write is, in a sentence, kept on the change it lands as. At most 1,000 "
                + "characters.")));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;
        var bytes = await RawBody.BytesAsync(HttpContext, ct);
        var note = Query<string?>("note", isRequired: false);
        if (note is { Length: > MapChangeLog.NoteLength })
        {
            await Refusals.StopAsync(HttpContext, 400, "note too long", Findings.Of(new Finding(RequestRules.Unreadable,
                $"the note is {note.Length} characters, more than {MapChangeLog.NoteLength}", Field: "note")), ct);
            return;
        }
        var intent = await artifacts.LoadJsonOrEmptyAsync<MapIntent>(map.Id, ArtifactKind.MapIntentJson, ct);
        var (_, refused) = PlacesCheck.Check(Encoding.UTF8.GetString(bytes), intent);
        if (await Refusals.StopAsync(HttpContext, 400, "the places cannot be stored", refused, ct)) return;

        if (note is { Length: > 0 }) artifacts.Stamp = artifacts.Stamp with { Note = note };
        var written = await DocumentWrite.StoreAsync(artifacts, map.Id, ArtifactKind.PlacesJson, "places", bytes,
            Revisions.Expected(HttpContext), ct);
        if (written.Refusal is { } refusal) { await Refusals.WriteAsync(HttpContext, refusal, ct); return; }
        Revisions.Answer(HttpContext, written.Revision!.Value);
        await Send.OkAsync(new AppliedDto(), ct);
    }
}
