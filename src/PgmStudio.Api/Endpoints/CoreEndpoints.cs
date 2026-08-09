using FastEndpoints;
using PgmStudio.Data.Features;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Domain;

namespace PgmStudio.Api.Endpoints;

using Dict = Dictionary<string, object?>;

/// <summary>
/// GET /api/map/{slug}/core-suggestions[?box=x0,y0,z0,x1,y1,z1] — the cores the ingest scan proposed for this
/// map, read straight out of <c>core_candidate</c>. No world access and no scoring pass: a core's signature
/// is unambiguous enough that the gather already knew the casing, so the stored row <i>is</i> the suggestion
/// (docs/contracts/objective-suggestion.md §2). <c>box</c> is optional and narrows the list to casings
/// intersecting it, which is how the configure step scopes a detect to the area the author drew.
///
/// <para>The response also carries the generator's casing <b>defaults</b>. A core placed by hand has no
/// suggestion to read its size, shell, float and leak from, and the client cannot reach
/// <see cref="ObjectiveDefaults"/> — so the one source of truth for those numbers is served rather than
/// duplicated on the far side of the wire, where it would drift out of step with the stamper silently.</para>
/// </summary>
public sealed class CoreSuggestionsEndpoint(MapRepository repo, PgmDb db) : EndpointWithoutRequest
{
    public override void Configure() { Get("/map/{slug}/core-suggestions"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var map = await repo.GetBySlugAsync(Route<string>("slug")!, ct);
        if (map is null) { await Send.NotFoundAsync(ct); return; }

        var cores = await CoreCandidateStore.ReadAsync(db, map.Id, ct);
        if (TryParseBox(HttpContext.Request.Query["box"].ToString(), out var box))
            cores = [.. cores.Where(core => Intersects(core.Casing, box))];

        await Send.OkAsync(new Dict
        {
            ["defaults"] = new Dict
            {
                ["size"] = ObjectiveDefaults.CoreSize,
                ["height"] = ObjectiveDefaults.CoreHeight,
                ["shell"] = ObjectiveDefaults.CoreShell,
                ["float"] = ObjectiveDefaults.CoreFloat,
                ["leak"] = ObjectiveDefaults.CoreLeak,
            },
            ["cores"] = cores.Select(core => new Dict
            {
                ["box"] = new Dict
                {
                    ["minX"] = core.Casing.MinX, ["minY"] = core.Casing.MinY, ["minZ"] = core.Casing.MinZ,
                    ["maxX"] = core.Casing.MaxX, ["maxY"] = core.Casing.MaxY, ["maxZ"] = core.Casing.MaxZ,
                },
                // The casing as the author edits it: width/depth and height come off the box, so a confirmed
                // suggestion states the structure that is actually there rather than the generator's default.
                ["size"] = Math.Max(core.Casing.Width, core.Casing.Depth),
                ["height"] = core.Casing.Height,
                ["shell"] = core.Shell,
                ["float"] = core.Float,
                ["openTop"] = core.OpenTop,
                ["lava"] = core.LavaBlocks,
            }).ToList(),
        }, ct);
    }

    private static bool Intersects(BlockBox a, BlockBox b) =>
        a.MinX <= b.MaxX && a.MaxX >= b.MinX && a.MinY <= b.MaxY && a.MaxY >= b.MinY && a.MinZ <= b.MaxZ && a.MaxZ >= b.MinZ;

    private static bool TryParseBox(string s, out BlockBox box)
    {
        box = default;
        var parts = s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 6) return false;
        var n = new int[6];
        for (var i = 0; i < 6; i++) if (!int.TryParse(parts[i], out n[i])) return false;
        box = new BlockBox(
            Math.Min(n[0], n[3]), Math.Min(n[1], n[4]), Math.Min(n[2], n[5]),
            Math.Max(n[0], n[3]), Math.Max(n[1], n[4]), Math.Max(n[2], n[5]));
        return true;
    }
}
