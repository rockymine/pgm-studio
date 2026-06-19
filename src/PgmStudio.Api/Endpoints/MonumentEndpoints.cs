using System.Text.Json.Nodes;
using FastEndpoints;
using LinqToDB;
using LinqToDB.Async;
using PgmStudio.Data.Features;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Geom;
using PgmStudio.Minecraft;
using PgmStudio.Pgm;

namespace PgmStudio.Api.Endpoints;

using Dict = Dictionary<string, object?>;

/// <summary>
/// GET /api/map/{slug}/monument-suggestions?box=x0,y0,z0,x1,y1,z1[&amp;style=pedestal,label,cap] — score the
/// pre-gathered <c>monument_candidate</c> rows inside the author's box for the declared style (F9). No
/// world access: loads the candidates and runs <c>MonumentSuggester.Score</c>. <c>box</c> is required (the
/// author marks the monument area); <c>style</c> defaults to <c>Any,Any,Any</c>.
/// </summary>
public sealed class MonumentSuggestionsEndpoint(MapRepository repo, PgmDb db) : EndpointWithoutRequest
{
    public override void Configure() { Get("/map/{slug}/monument-suggestions"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var map = await repo.GetBySlugAsync(Route<string>("slug")!, ct);
        if (map is null) { await Send.NotFoundAsync(ct); return; }

        if (!TryParseBox(HttpContext.Request.Query["box"].ToString(), out var box))
        { await Send.ResponseAsync(new Dict { ["error"] = "box=x0,y0,z0,x1,y1,z1 is required" }, 400, ct); return; }
        var style = ParseStyle(HttpContext.Request.Query["style"].ToString());

        var candidates = await MonumentCandidateStore.ReadAsync(db, map.Id, ct);
        var suggestions = MonumentSuggester.Score(candidates, box, style);

        await Send.OkAsync(suggestions.Select(s => new Dict
        {
            ["x"] = s.X, ["y"] = s.Y, ["z"] = s.Z,
            ["color"] = s.Color, ["confidence"] = s.Confidence, ["source"] = s.Source,
            ["pedestal_id"] = s.PedestalId, ["pedestal_data"] = s.PedestalData,
            ["sign"] = s.SignX is null ? null : new Dict { ["x"] = s.SignX, ["y"] = s.SignY, ["z"] = s.SignZ },
            ["evidence"] = s.Evidence,
        }).ToList(), ct);
    }

    private static bool TryParseBox(string s, out ScanBox box)
    {
        box = default;
        var p = s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (p.Length != 6) return false;
        var n = new int[6];
        for (var i = 0; i < 6; i++) if (!int.TryParse(p[i], out n[i])) return false;
        box = new ScanBox(
            Math.Min(n[0], n[3]), Math.Min(n[1], n[4]), Math.Min(n[2], n[5]),
            Math.Max(n[0], n[3]), Math.Max(n[1], n[4]), Math.Max(n[2], n[5]));
        return true;
    }

    private static MonumentStyle ParseStyle(string s)
    {
        var p = s.Split(',', StringSplitOptions.TrimEntries);
        var ped = p.Length > 0 && Enum.TryParse<PedestalKind>(p[0], true, out var pk) ? pk : PedestalKind.Any;
        var lab = p.Length > 1 && Enum.TryParse<LabelKind>(p[1], true, out var lk) ? lk : LabelKind.Any;
        var cap = p.Length > 2 && Enum.TryParse<CapKind>(p[2], true, out var ck) ? ck : CapKind.Any;
        return new MonumentStyle(ped, lab, cap);
    }
}

/// <summary>
/// POST /api/map/{slug}/monument-orbit — complete the symmetry orbit of confirmed monument positions (F9
/// §5). Body: <c>{ positions: [ { x, y, z, color? } ] }</c>. Reads the confirmed <c>symmetry_json</c>
/// (mode + centre) and reflects/rotates each position onto the other teams (XZ only — Y preserved),
/// returning every position tagged with its orbit step. Pure geometry — no candidate-table or world access.
/// </summary>
public sealed class MonumentOrbitEndpoint(MapRepository repo, PgmDb db) : EndpointWithoutRequest
{
    public override void Configure() { Post("/map/{slug}/monument-orbit"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var map = await repo.GetBySlugAsync(Route<string>("slug")!, ct);
        if (map is null) { await Send.NotFoundAsync(ct); return; }

        using var reader = new StreamReader(HttpContext.Request.Body);
        var body = JsonNode.Parse(await reader.ReadToEndAsync(ct)) as JsonObject ?? new JsonObject();

        var symRow = await SymmetryStore.LoadAsync(db, map.Id, ct);
        var mode = symRow?.PrimaryType;
        var cx = symRow?.CenterX ?? 0;
        var cz = symRow?.CenterZ ?? 0;

        var outPos = new JsonArray();
        foreach (var pn in body["positions"]?.AsArray() ?? new JsonArray())
        {
            if (pn is not JsonObject p) continue;
            int x = p["x"]!.GetValue<int>(), y = p["y"]!.GetValue<int>(), z = p["z"]!.GetValue<int>();
            var color = p["color"]?.GetValue<string>();
            foreach (var (ox, oz, k) in Orbit(x, z, mode, cx, cz))
                outPos.Add(new JsonObject { ["x"] = ox, ["y"] = y, ["z"] = oz, ["color"] = color, ["orbit"] = k });
        }

        await Send.OkAsync(new Dict { ["mode"] = mode, ["positions"] = outPos }, ct);
    }

    // The orbit of (x,z) under the confirmed mode: step 0 is the source; rot_90 adds 3 turns, mirror_* /
    // rot_180 add 1. Y is untouched — symmetry is horizontal.
    private static IEnumerable<(int x, int z, int k)> Orbit(int x, int z, string? mode, double cx, double cz)
    {
        yield return (x, z, 0);
        if (mode == "rot_90")
            for (var k = 1; k < 4; k++) { var (rx, rz) = Symmetry.Point(x, z, mode, cx, cz, k); yield return (R(rx), R(rz), k); }
        else if (mode == "rot_180" || Symmetry.Normal(mode) is not null)
        { var (rx, rz) = Symmetry.Point(x, z, mode, cx, cz, 1); yield return (R(rx), R(rz), 1); }
    }

    private static int R(double v) => (int)Math.Round(v);
}
