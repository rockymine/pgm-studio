using System.Text.Json.Nodes;

namespace PgmStudio.E2e.Tests.Harness;

/// <summary>
/// The fixture maps the specs open, one per stage the routes need.
///
/// The geometry comes from the composer rather than hand-drawn boxes: a composed board is a real layout —
/// spawns, wools, a hub, a frontline, connected build zones — so the pages under test render something
/// representative, and a descriptor composes the same plan every time, so the fixtures are stable run to run
/// without a committed fixture file.
/// </summary>
/// <param name="Descriptor">The composer descriptor the plan map was pinned from.</param>
/// <param name="PlanSlug">A composed candidate committed to authoring — <c>/maps/{slug}/plan</c>.</param>
/// <param name="SketchSlug">A draft carrying a compiled layout — <c>/maps/{slug}/sketch</c>.</param>
/// <param name="MapSlug">A composed layout finished into world geometry — <c>/maps/{slug}/configure</c>.</param>
/// <param name="PlanJson">The plan map's document, as the JSON text the API stores.</param>
public sealed record Seed(JsonNode Descriptor, string PlanSlug, string SketchSlug, string MapSlug, string PlanJson)
{
    private const string Request = "players=12&symmetry=rot_180&cell=5&count=1";

    public static async Task<Seed> CreateAsync(StudioApi api)
    {
        Console.WriteLine($"seeding against {Studio.Base}");

        var plan = await ComposedPlanMap(api);
        Console.WriteLine($"  plan       {plan.Slug}");

        // A second composed board, carried all the way to world geometry.
        var carried = await ComposedPlanMap(api);
        var compiled = (await api.Post("/plan/compile", plan.PlanJson))!;
        await api.Put($"/map/{carried.Slug}/sketch", compiled["layout"]);
        // A finished map names its author: Configure unlocks every phase past Identity only once one is
        // stated, and a name with a space in it is a pseudonym, so no Mojang lookup is asked.
        var intent = compiled["intent"]!.DeepClone();
        intent["meta"]!["authors"] = new JsonArray(new JsonObject { ["name"] = "E2E fixture" });
        await api.Put($"/map/{carried.Slug}/intent", intent);
        var finished = (await api.Post($"/map/{carried.Slug}/sketch/finish"))!;
        var mapSlug = finished["slug"]!.GetValue<string>();
        Console.WriteLine($"  configure  {mapSlug} (finished to world geometry)");

        // A sketch-stage draft that still holds its layout, so the Sketch tool opens on real content.
        var draft = (await api.Post("/sketch", new { name = "E2E sketch draft" }))!;
        var sketchSlug = draft["slug"]!.GetValue<string>();
        await api.Put($"/map/{sketchSlug}/sketch", compiled["layout"]);
        Console.WriteLine($"  sketch     {sketchSlug}");

        return new Seed(plan.Descriptor, plan.Slug, sketchSlug, mapSlug, plan.PlanJson);
    }

    /// <summary>
    /// Pins a composed board so it exists as a stored candidate, then commits it to authoring. The descriptor
    /// is taken from a browsed card, because it carries the composer's own version and schema and a hand-built
    /// one goes stale the next time either moves — which a pin refuses.
    /// </summary>
    private static async Task<(string Slug, string PlanJson, JsonNode Descriptor)> ComposedPlanMap(StudioApi api)
    {
        var cards = (await api.Get($"/compose?{Request}"))?["cards"]?.AsArray();
        if (cards is not { Count: > 0 }) throw new InvalidOperationException($"/compose?{Request} returned no cards to pin");
        var descriptor = cards[0]!["descriptor"]!;
        var pinned = (await api.Post("/compose/pin", descriptor))!;
        var authored = (await api.Post($"/plan/{pinned["id"]}/author"))!;
        return (authored["slug"]!.GetValue<string>(), pinned["planJson"]!.GetValue<string>(), descriptor.DeepClone());
    }
}
