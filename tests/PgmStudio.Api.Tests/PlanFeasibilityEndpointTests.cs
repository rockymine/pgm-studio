using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace PgmStudio.Api.Tests;

/// <summary>
/// POST /api/plan/feasibility — "could the composer have produced this?", the read the evaluator does not give.
/// The endpoint is a thin projection over the Pgm-side service (which owns every judgement and is tested there),
/// so these cover the wire contract: the per-box and unit-level halves both present, the nearest-miss cells the
/// canvas paints carried through, a box-less plan answered empty rather than as an error, and a malformed body
/// answered 400 never 500. DB-free, so a bare host suffices.
/// </summary>
[NotInParallel("api-db")]
public sealed class PlanFeasibilityEndpointTests
{
    [Test]
    public async Task An_unproducible_exemplar_reports_both_halves_with_citations()
    {
        using var client = ApiTestFactory.Shared.CreateClient();

        var resp = await client.PostAsync("/api/plan/feasibility",
            new StringContent(ReadSeed("shifted-u-frontline-attach-hole-hub.plan.json"), Encoding.UTF8, "application/json"));
        await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(body.GetProperty("producible").GetBoolean()).IsFalse();

        // the per-box half: the donut wool reads as one, is not producible, and names the nearest candidate
        var wool = body.GetProperty("boxes").EnumerateArray().First(b => b.GetProperty("boxId").GetString() == "wool-a");
        await Assert.That(wool.GetProperty("producible").GetBoolean()).IsFalse();
        await Assert.That(wool.GetProperty("identity").GetString()).Contains("Donut");
        var nearest = wool.GetProperty("nearest");
        await Assert.That(nearest.GetProperty("label").GetString()).Contains("Donut");
        await Assert.That(nearest.GetProperty("differingCells").GetInt32()).IsGreaterThan(0);

        // and the other side of the same wire: this plan's hub is the unequal-walled ring, which does reproduce —
        // a producible box carries its tuple and no nearest miss, in the same response as an unproducible one
        var hub = body.GetProperty("boxes").EnumerateArray().First(b => b.GetProperty("kind").GetString() == "hub");
        await Assert.That(hub.GetProperty("producible").GetBoolean()).IsTrue();
        await Assert.That(hub.GetProperty("producibleAs").GetString()).Contains("walls");

        // the unit-level half: the arrangement findings, each citing the rule or task behind it. (The frontline's
        // own two blockers used to be here and cite G123; that landed, so what remains is the seat gap.)
        var unit = body.GetProperty("unit").EnumerateArray().ToList();
        await Assert.That(unit).IsNotEmpty();
        await Assert.That(unit.All(f => !string.IsNullOrEmpty(f.GetProperty("cites").GetString()))).IsTrue();
        await Assert.That(unit.Any(f => f.GetProperty("rule").GetString() == "seats-within-separation-gap")).IsTrue();
    }

    /// <summary>The nearest miss carries the differing cells as rects, which is what lets the canvas paint the
    /// discrepancy instead of only reporting a count.</summary>
    [Test]
    public async Task The_nearest_miss_carries_the_cells_the_canvas_paints()
    {
        using var client = ApiTestFactory.Shared.CreateClient();

        var resp = await client.PostAsync("/api/plan/feasibility",
            new StringContent(ReadSeed("shifted-u-frontline-attach-hole-hub.plan.json"), Encoding.UTF8, "application/json"));
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();

        var wool = body.GetProperty("boxes").EnumerateArray().First(b => b.GetProperty("boxId").GetString() == "wool-a");
        var nearest = wool.GetProperty("nearest");
        var extra = nearest.GetProperty("extra").EnumerateArray().ToList();
        var missing = nearest.GetProperty("missing").EnumerateArray().ToList();
        await Assert.That(extra.Count + missing.Count).IsEqualTo(nearest.GetProperty("differingCells").GetInt32());
        // every cell is a [x, z, w, h] rect in the plan's cell frame, ready for the overlay
        foreach (var cellRect in extra.Concat(missing))
            await Assert.That(cellRect.EnumerateArray().Count()).IsEqualTo(4);
    }

    [Test]
    public async Task A_plan_without_boxes_reads_empty_rather_than_erroring()
    {
        using var client = ApiTestFactory.Shared.CreateClient();

        var resp = await client.PostAsync("/api/plan/feasibility",
            new StringContent(ReadSeed("base-2wool.plan.json"), Encoding.UTF8, "application/json"));
        await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(body.GetProperty("boxes").GetArrayLength()).IsEqualTo(0);
        await Assert.That(body.GetProperty("unit").GetArrayLength()).IsEqualTo(0);
    }

    [Test]
    public async Task Malformed_body_is_a_400_not_a_500()
    {
        using var client = ApiTestFactory.Shared.CreateClient();

        var resp = await client.PostAsync("/api/plan/feasibility",
            new StringContent("not a plan", Encoding.UTF8, "application/json"));
        await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    private static string ReadSeed(string file)
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            var candidate = Path.Combine(dir, "tools", "seeds", file);
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            dir = Directory.GetParent(dir)?.FullName;
        }
        throw new FileNotFoundException($"seed {file} not found above the test binary");
    }
}
