using FastEndpoints;
using PgmStudio.Api.Services;
using PgmStudio.Contracts;
using PgmStudio.Data.Features;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;

namespace PgmStudio.Api.Endpoints;

/// <summary>
/// POST /api/map/from-documents — load a whole map from the three documents it is made of: the plan it was
/// drawn from, the layout that was built, and the intent it is played for.
///
/// <para>The way back in for a map authored elsewhere. Neither world import can take one: <c>import-folder</c>
/// refuses a folder carrying a <c>map.xml</c> outright, <c>import-url</c> reads only <c>region/*.mca</c>, and
/// nothing in the API reads a <c>map.xml</c> at all — so a map built against one studio could only be moved
/// to another as a world, arriving without its plan, its drawing or its intent. This takes the documents
/// instead, and what it stores is a map Configure opens and the plan tool can rebuild.</para>
///
/// <para>A map already stored under the slug is replaced. The work itself is
/// <see cref="MapFromDocuments"/> — the order the steps run in is the operation's, not this route's.</para>
/// </summary>
public sealed class MapFromDocumentsEndpoint(
    MapRepository repo, MapReader reader, MapWriter writer, MapArtifactStore artifacts,
    WorldFeatureWriter features, PgmDb db, MojangClient mojang)
    : Endpoint<MapFromDocumentsRequest, MapLoadedDto>
{
    public override void Configure() { Post("/map/from-documents"); AllowAnonymous(); }

    public override async Task HandleAsync(MapFromDocumentsRequest request, CancellationToken ct)
    {
        var loaded = await MapFromDocuments.LoadAsync(
            HttpContext, request, repo, reader, writer, artifacts, features, db, mojang, ct);

        if (loaded.IsError)
        {
            await Refusals.WriteAsync(HttpContext, loaded.ErrorStatus!.Value, loaded.Error!, loaded.Findings!, ct);
            return;
        }

        // What the board names and does not have rides on the success, the same as it does on a finish.
        Complaints.Add(HttpContext, loaded.Complaints?.Complaints ?? []);
        await Send.OkAsync(new MapLoadedDto(loaded.Slug!, loaded.Replaced, loaded.Cells, loaded.Islands,
            $"/maps/{loaded.Slug}/configure"), ct);
    }
}
