using System.Net;
using System.Net.Http.Json;
using PgmStudio.Contracts;
using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Api.Tests;

/// <summary>
/// The knob panel's HTTP surface (G144): GET /api/shapes/probe emits one knob combination through
/// <see cref="BoxFiller"/> and answers with a shape <b>or</b> a refusal, and GET /api/shapes/probe/schema
/// serves the knob surface the panel drives.
///
/// <para>What is really gated here is that the two <b>agree</b>. The schema's minimum box and a size
/// refusal's minimum are computed by different code on different sides of a frame change — the emitter's
/// canonical frame versus the dock frame a mouth imposes — and a lateral-mouth family transposes between
/// them. When they disagree the panel shows a chip saying 10×5 beside a refusal saying 5×10, which is both
/// wrong and the exact failure this endpoint exists to prevent elsewhere. Read-only, so no schema reset.</para>
/// </summary>
[NotInParallel("api-db")]
public sealed class ShapeProbeEndpointTests
{
    [Test]
    public async Task Schema_covers_every_emittable_family_and_flags_the_menu()
    {
        await using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();

        var schema = await client.GetFromJsonAsync<ShapeProbeSchema>("/api/shapes/probe/schema");
        var tokens = schema!.Families.Select(f => f.Token).ToList();

        // every family the emitter builds, and only those — Isolated is a derive-only reading
        foreach (var family in Enum.GetValues<ShapeFamily>().Where(f => f != ShapeFamily.Isolated))
            await Assert.That(tokens).Contains(family.ToString().ToLowerInvariant());
        await Assert.That(tokens).DoesNotContain("isolated");

        // the menu flag is the page's honesty: Z is advertised, the scythe is not (G146)
        await Assert.That(schema.Families.Single(f => f.Token == "z").OnProductionMenu).IsTrue();
        await Assert.That(schema.Families.Single(f => f.Token == "scythe").OnProductionMenu).IsFalse();

        await Assert.That(schema.Mouths).IsEquivalentTo(new[] { "Top", "Bottom", "Left", "Right" });
    }

    [Test]
    public async Task Schema_knobs_match_what_the_emitter_accepts_per_family()
    {
        await using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();
        var schema = await client.GetFromJsonAsync<ShapeProbeSchema>("/api/shapes/probe/schema");

        // side-tuck is I/Z/scythe only — the guard the emitter states in its own message
        await Assert.That(schema!.Families.Single(f => f.Token == "i").Knobs).Contains("sideTuck");
        await Assert.That(schema.Families.Single(f => f.Token == "l").Knobs).DoesNotContain("sideTuck");
        // the wool-at-an-end variant belongs to the two-leg and enclosing families
        await Assert.That(schema.Families.Single(f => f.Token == "u").Knobs).Contains("woolAtEnd");
        await Assert.That(schema.Families.Single(f => f.Token == "i").Knobs).DoesNotContain("woolAtEnd");
        // the attachment width is the donut's hub entry
        await Assert.That(schema.Families.Single(f => f.Token == "donut").Knobs).Contains("attachW");
    }

    [Test]
    public async Task A_probe_that_fills_carries_the_shape_its_land_and_its_slots()
    {
        await using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();

        var probe = await client.GetFromJsonAsync<ShapeProbeResult>(
            "/api/shapes/probe?family=donut&w=6&h=13&cw=2&mouth=Top");

        await Assert.That(probe!.Rejection).IsNull();
        await Assert.That(probe.Svg).IsNotNull();
        await Assert.That(probe.Svg!).Contains("<svg");
        await Assert.That(probe.LandCells).IsGreaterThan(0);
        // the slot template is the family's, terminal last — the panel prints it as the shape's anatomy
        await Assert.That(probe.Slots).Contains(ApproachSlots.Room);
        await Assert.That(probe.Slots).Contains(ApproachSlots.EntryBar);
        await Assert.That(probe.Slots[^1]).IsEqualTo(ApproachSlots.Room);
    }

    [Test]
    public async Task A_box_below_the_minimum_is_a_refusal_that_states_the_minimum()
    {
        await using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();

        var probe = await client.GetFromJsonAsync<ShapeProbeResult>("/api/shapes/probe?family=donut&w=4&h=4&cw=2");

        await Assert.That(probe!.Svg).IsNull();
        await Assert.That(probe.Rejection).IsNotNull();
        await Assert.That(probe.Rejection!.Code).IsEqualTo("too-small");
        await Assert.That(probe.Rejection.MinW).IsNotNull();
        await Assert.That(probe.Rejection.MinH).IsNotNull();
        await Assert.That(probe.LandCells).IsEqualTo(0);
    }

    [Test]
    public async Task The_offered_resize_actually_fills()
    {
        // the panel turns MinW/MinH into a one-click resize, so the offer has to be a box that works —
        // an off-by-one or a transposed pair would hand the author a button that refuses again
        await using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();

        foreach (var family in new[] { "donut", "u", "h", "clamp", "l", "z" })
        {
            var refused = await client.GetFromJsonAsync<ShapeProbeResult>(
                $"/api/shapes/probe?family={family}&w=2&h=2&cw=2");
            await Assert.That(refused!.Rejection?.Code).IsEqualTo("too-small")
                .Because($"a 2x2 box is below every family's minimum ({family})");

            var retry = await client.GetFromJsonAsync<ShapeProbeResult>(
                $"/api/shapes/probe?family={family}&w={refused.Rejection!.MinW}&h={refused.Rejection.MinH}&cw=2");
            await Assert.That(retry!.Rejection).IsNull()
                .Because($"the minimum box the refusal offered must fill for {family}");
        }
    }

    [Test]
    public async Task The_schemas_minimum_and_the_refusals_minimum_are_the_same_frame()
    {
        // the frame bug this endpoint shipped with: the schema reported the emitter's canonical frame while
        // a refusal reports the dock frame, so the donut's chip said 10x5 next to a refusal saying 5x10.
        await using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();
        var schema = await client.GetFromJsonAsync<ShapeProbeSchema>("/api/shapes/probe/schema");

        foreach (var family in schema!.Families)
        {
            var refused = await client.GetFromJsonAsync<ShapeProbeResult>(
                $"/api/shapes/probe?family={family.Token}&w=1&h=1&cw=2");
            if (refused!.Rejection is not { Code: "too-small" } size) continue;   // off-menu answers first
            await Assert.That(size.MinW).IsEqualTo(family.MinW)
                .Because($"{family.Token}'s schema minimum must be the frame a refusal speaks");
            await Assert.That(size.MinH).IsEqualTo(family.MinH);
        }
    }

    [Test]
    public async Task An_unsupported_knob_passes_the_emitters_own_words_through()
    {
        // a reworded guard is a second copy free to disagree with the one that fired
        await using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();

        var probe = await client.GetFromJsonAsync<ShapeProbeResult>(
            "/api/shapes/probe?family=l&w=12&h=12&cw=2&sideTuck=true");

        await Assert.That(probe!.Svg).IsNull();
        await Assert.That(probe.Rejection!.Code).IsEqualTo("unsupported-knobs");
        await Assert.That(probe.Rejection.Detail).Contains("side-tuck");
        await Assert.That(probe.Rejection.Detail).Contains("L");
    }

    [Test]
    public async Task A_family_off_the_fill_menu_is_refused_with_the_menu_that_was_offered()
    {
        await using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();

        var probe = await client.GetFromJsonAsync<ShapeProbeResult>("/api/shapes/probe?family=scythe&w=16&h=14&cw=2");

        await Assert.That(probe!.Svg).IsNull();
        await Assert.That(probe.Rejection!.Code).IsEqualTo("not-on-menu");
        await Assert.That(probe.Rejection.Detail).Contains("Donut")
            .Because("the refusal names what the menu did offer, so the author can pick from it");
    }

    [Test]
    public async Task An_unknown_family_is_400_rather_than_a_refusal()
    {
        // a bad enum is a caller error, not a modelling answer — it must not masquerade as a refusal
        await using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();

        var resp = await client.GetAsync("/api/shapes/probe?family=triangle&w=8&h=8&cw=2");
        await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);

        var isolated = await client.GetAsync("/api/shapes/probe?family=isolated&w=8&h=8&cw=2");
        await Assert.That(isolated.StatusCode).IsEqualTo(HttpStatusCode.BadRequest)
            .Because("Isolated is a derive-only reading the emitter refuses to build");
    }

    [Test]
    public async Task Every_mouth_is_drivable()
    {
        // the panel offers all four edges; a fill that only works mouth-up would make three chips dead
        await using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();

        foreach (var mouth in new[] { "Top", "Bottom", "Left", "Right" })
        {
            var probe = await client.GetFromJsonAsync<ShapeProbeResult>(
                $"/api/shapes/probe?family=i&w=8&h=8&cw=2&mouth={mouth}");
            await Assert.That(probe!.Rejection).IsNull().Because($"an I in an 8x8 box fills a {mouth} mouth");
            await Assert.That(probe.Svg!).Contains("<svg");
        }
    }
}
