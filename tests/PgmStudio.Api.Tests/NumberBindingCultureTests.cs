using System.Globalization;
using System.Net.Http.Json;
using PgmStudio.Contracts;

namespace PgmStudio.Api.Tests;

/// <summary>
/// The wire is dot-separated whatever culture the host runs under.
///
/// <para>Query, route and form values bind through a converter that reads the ambient culture, so on a
/// comma-decimal host "0.55" arrives as fifty-five: still a number, so nothing throws and nothing logs — the
/// endpoint simply builds a tree a hundred times bigger than the one asked for. That is the failure this
/// gates, and it is invisible on a dot-decimal developer machine, which is why the culture is made hostile
/// here rather than inherited from whatever the runner happens to hold.</para>
///
/// <para>The host is built first and the culture moved afterwards, deliberately: startup pins the process to
/// invariant, so a test that flipped the culture before booting would only be re-testing that pin. Moving it
/// after is what proves the binding is invariant on its own — the guarantee has to survive ambient state that
/// any library is free to change.</para>
///
/// <para>Asserted by comparing against the same request with the parameter omitted. The value sent is the
/// endpoint's own default for that knob, so a correctly-bound request must draw the identical picture; one
/// that mis-binds draws a different tree, and the bytes say so.</para>
/// </summary>
[NotInParallel("api-db")]
public sealed class NumberBindingCultureTests
{
    /// <summary>A comma-decimal, dot-grouping culture — the one the studio was found broken under.</summary>
    private static readonly CultureInfo CommaDecimal = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>Boot the API, then hand <paramref name="body"/> a client with the process reading numbers the
    /// German way.</summary>
    private static async Task UnderCommaDecimalCulture(Func<HttpClient, Task> body)
    {
        await using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();

        var (culture, uiCulture) = (CultureInfo.DefaultThreadCurrentCulture, CultureInfo.DefaultThreadCurrentUICulture);
        CultureInfo.DefaultThreadCurrentCulture = CommaDecimal;
        CultureInfo.DefaultThreadCurrentUICulture = CommaDecimal;
        try { await body(client); }
        finally
        {
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = uiCulture;
        }
    }

    private static async Task<List<string>> WoodCardsAsync(HttpClient client, string query)
        => [.. (await client.GetFromJsonAsync<List<PropOptionDto>>($"/api/terrain/woods?{query}"))!.Select(card => card.Svg)];

    [Test]
    public async Task The_culture_this_gates_really_would_misread_a_dot()
    {
        // Were de-DE ever to stop treating '.' as a group separator, the tests below would pass for the wrong
        // reason — so the premise is asserted rather than assumed.
        await Assert.That(double.Parse("0.55", CommaDecimal)).IsEqualTo(55d)
            .Because("the failure mode being gated is a comma-decimal culture reading the dot as grouping");
    }

    [Test]
    public async Task A_fractional_query_value_binds_as_written_under_a_comma_decimal_culture()
        => await UnderCommaDecimalCulture(async client =>
        {
            // The tree-wood picker, every knob sent explicitly at exactly the endpoint's own default.
            var sent = await WoodCardsAsync(client,
                "height=13&stems=1&leader=0.55&flow=0.45&branchAngle=0.55&levels=2&leafSize=0.6");
            var defaulted = await WoodCardsAsync(client, "height=13");

            await Assert.That(sent).IsEquivalentTo(defaulted)
                .Because("sending a knob's own default value must draw the same tree as omitting it");
        });

    [Test]
    public async Task Each_fractional_knob_binds_independently()
        => await UnderCommaDecimalCulture(async client =>
        {
            // One knob at a time: a shared parser fault shows on all of them, a per-knob one on exactly the
            // knob that misses.
            var defaulted = await WoodCardsAsync(client, "height=13");
            foreach (var knob in new[] { "leader=0.55", "flow=0.45", "branchAngle=0.55", "leafSize=0.6" })
                await Assert.That(await WoodCardsAsync(client, $"height=13&{knob}")).IsEquivalentTo(defaulted)
                    .Because($"{knob} is the endpoint's own default and must bind as written");
        });

    [Test]
    public async Task A_whole_number_query_value_binds_under_a_comma_decimal_culture()
        => await UnderCommaDecimalCulture(async client =>
        {
            // Integers travel the same converter, and a group separator changes how they read too.
            var probe = await client.GetFromJsonAsync<ShapeProbeResult>("/api/shapes/probe?family=i&w=8&h=8&cw=2");
            await Assert.That(probe!.Rejection).IsNull()
                .Because("an 8x8 box fills an I — a size knob that mis-binds would refuse it");
        });

    [Test]
    public async Task A_knob_far_outside_its_range_answers_instead_of_running_away()
        => await UnderCommaDecimalCulture(async client =>
        {
            // The shape of the original failure, kept reachable: a leader of 55 asked for a tree hundreds of
            // blocks tall and the request never returned. Bounds now hold it to a tree, so this answers — a
            // regression would hang the suite rather than fail it, which is why the timeout is explicit.
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var response = await client.GetAsync(
                "/api/terrain/woods?height=999&leader=99&flow=99&branchAngle=99&levels=99&leafSize=99&stems=99",
                timeout.Token);

            await Assert.That(response.IsSuccessStatusCode).IsTrue();
            var cards = await response.Content.ReadFromJsonAsync<List<PropOptionDto>>();
            await Assert.That(cards!.Count).IsGreaterThan(0);
        });
}
