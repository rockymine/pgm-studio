using PgmStudio.Api.Services;
using PgmStudio.Data.Map;
using PgmStudio.Domain;
using PgmStudio.Export;
using PgmStudio.Minecraft.Palette;
using PgmStudio.Minecraft.Render;

namespace PgmStudio.Api.Endpoints;

/// <summary>GET /api/map/{slug}/render/picture — the picture a server shows the map by (<see cref="MapPicture"/>):
/// the kept view marked as the picture, else the whole board from above its long side, drawn at
/// <c>width</c> by <c>height</c>. 422 where the board has no ground to frame; 503 (<c>RQ10</c>) without block
/// textures. Kept with the world like every eye picture.</summary>
[Queued]
internal sealed class PictureReadEndpoint(MapRepository repo, MapReader reader, MapArtifactStore artifacts,
                                          BlockTextureStore textures)
    : WorldRenderEndpoint(repo, reader, artifacts)
{
    private readonly MapRepository _repo = repo;
    private readonly MapArtifactStore _artifacts = artifacts;
    private BlockTextureSet? _textures;
    private IReadOnlyList<WorldView> _kept = [];

    public override void Configure()
    {
        Get("/map/{slug}/render/picture");
        Summary(s => s.Summary = WorldReadCatalog.Sentence("render/picture"));
        Description(b => b.Png().Refuses(404, 422, 503).Reads(
            new QueryWord("width", "Pixels across, 160 to 1920. Absent is 960.", Min: 160, Max: 1920),
            new QueryWord("height", "Pixels down, 90 to 1080. Absent is 540.", Min: 90, Max: 1080)));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var (set, reason) = await textures.GetAsync(ct);
        if (set is null)
        {
            await Refusals.WriteAsync(HttpContext, 503, "no block textures",
                [new Vocabulary.Finding(RequestRules.TexturesUnavailable, reason ?? "the studio has no block textures")], ct);
            return;
        }
        if (await _repo.OfRouteAsync(HttpContext, ct) is not { } map) return;
        _textures = set;
        _kept = await KeptViews.LoadAsync(_artifacts, map.Id, ct);
        await base.HandleAsync(ct);
    }

    protected override string DrawnWith =>
        $"{textures.Identity}|{string.Join(';', _kept.Select(view => $"{view.Id}:{view.Query}:{view.Picture}"))}";

    protected override async Task<IDisposable?> TurnAsync(CancellationToken ct) => await EyeRenders.TurnAsync(ct);

    protected override string Empty => "the board has no ground to frame, or its picture finds no place to stand";

    protected override byte[]? Draw(BuiltRead read) =>
        MapPicture.Draw(read.Built, _kept, _textures!,
            Math.Clamp(OptionalInt("width") ?? 960, 160, 1920), Math.Clamp(OptionalInt("height") ?? 540, 90, 1080));
}
