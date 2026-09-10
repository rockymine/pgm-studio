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
/// <para>Asserted on the contour interval, the studio's one fractional query knob. The reply echoes the
/// spacing it bound, so the bound value is read directly rather than inferred from a picture; a second case
/// holds the echo to the value actually in play, since an interval that is reported and not used would pass
/// the first on its own.</para>
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
        using var client = ApiTestFactory.Shared.CreateClient();

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

    /// <summary>A board carrying one group with a relief field on it — the layout the contour preview reads,
    /// posted whole because the overlay tracks unsaved edits rather than the stored document.</summary>
    private const string Relieved = """
        {"setup":{"mirror_mode":"rot_180","center":{"cx":0,"cz":0}},
         "layers":[{"base_y":0,"layout":{
           "shapes":[{"id":"s1","type":"rectangle","operation":"add",
                      "min_x":-20,"max_x":20,"min_z":-20,"max_z":20,"floor":8,"base_height":12}],
           "groups":[{"id":"i","name":"I","shapeIds":["s1"]}]}}],
         "relief":{"i":{"base":6,"reach":40,"step":1,
                        "marks":[{"kind":"point","at":[0,0],"h":40,"r":8}]}}}
        """;

    /// <summary>The board's contours at <paramref name="interval"/> blocks of spacing: the spacing the
    /// endpoint bound, echoed back, and how many lines it drew at it.</summary>
    private static async Task<(double Interval, int Lines)> ContoursAsync(HttpClient client, string interval)
    {
        var body = new StringContent(Relieved, System.Text.Encoding.UTF8, "application/json");
        var response = await client.PostAsync(
            $"/api/map/{SketchBoard.Slug}/sketch/relief?interval={interval}", body);
        response.EnsureSuccessStatusCode();
        var contours = await response.Content.ReadFromJsonAsync<ReliefContoursDto>();
        return (contours!.Interval, contours.Groups.Sum(group => group.Lines.Count));
    }

    [Test]
    public async Task The_culture_this_gates_really_would_misread_a_dot()
    {
        // Were de-DE ever to stop treating '.' as a group separator, the tests below would pass for the wrong
        // reason — so the premise is asserted rather than assumed.
        await Assert.That(double.Parse("0.55", CommaDecimal)).IsEqualTo(55d)
            .Because("the failure mode being gated is a comma-decimal culture reading the dot as grouping");
    }

    [Test]
    [Arguments("0.5", 0.5)]
    [Arguments("1.5", 1.5)]
    [Arguments("0.25", 0.25)]
    [Arguments("0.001", 0.001)]
    public async Task A_fractional_query_value_binds_as_written_under_a_comma_decimal_culture(
        string sent, double bound)
        => await UnderCommaDecimalCulture(async client =>
        {
            using var board = await SketchBoard.FreshAsync();

            // The dot read as a group separator turns each of these into the digits run together — 0.5 into
            // five, 0.001 into one — so the spacing the endpoint reports is the whole assertion.
            var (interval, _) = await ContoursAsync(client, sent);
            await Assert.That(interval).IsEqualTo(bound)
                .Because($"'{sent}' must bind as written, whatever the ambient culture reads a dot as");
        });

    [Test]
    public async Task The_bound_interval_is_the_one_the_contours_are_traced_at()
        => await UnderCommaDecimalCulture(async client =>
        {
            using var board = await SketchBoard.FreshAsync();

            // A value reported and not used would pass the case above on its own, so the spacing is held to
            // what it draws: half-block levels are ten times as dense as five-block ones over the same field.
            var (_, fine) = await ContoursAsync(client, "0.5");
            var (_, coarse) = await ContoursAsync(client, "5");

            await Assert.That(fine).IsGreaterThan(coarse)
                .Because("a half-block contour interval traces more lines than a five-block one");
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
            // The shape of the original failure, kept reachable: a mis-bound fractional value asks for work
            // hundreds of times the size intended and the request never returns. A regression would hang the
            // suite rather than fail it, which is why the timeout is explicit.
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            using var board = await SketchBoard.FreshAsync();

            var body = new StringContent(Relieved, System.Text.Encoding.UTF8, "application/json");
            var response = await client.PostAsync(
                $"/api/map/{SketchBoard.Slug}/sketch/relief?interval=0.001", body, timeout.Token);

            await Assert.That(response.IsSuccessStatusCode).IsTrue();
        });
}
