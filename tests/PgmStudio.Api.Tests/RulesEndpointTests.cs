using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PgmStudio.Api.Tests;

/// <summary>
/// <c>GET /api/rules</c> — and, more than the endpoint, the two ways its answer can quietly stop being true.
///
/// <para>The catalogue reads each rule out of its own source rather than out of a table, so nothing here can
/// disagree with a docstring. What it <em>can</em> do is miss a rule entirely: the endpoint names the
/// assemblies it reads, and a new <c>*Rules</c> class in a project not on that list is invisible with no error
/// anywhere. So the check is a sweep of every assembly that actually shipped, compared against what the
/// endpoint answered.</para>
///
/// <para>The other way is a rule added with no sentence beside it. An id with an empty description is worse
/// than an absent one, because it looks answered.</para>
/// </summary>
[NotInParallel("api-db")]
public sealed class RulesEndpointTests
{
    /// <summary>A rule id: two or three letters, then a number or a letter suffix (<c>PC-C</c>).</summary>
    private static readonly Regex IdShape = new(@"^[A-Z]{2,3}(?:-[A-Z]+|[0-9]+)$");

    private sealed record Row(string Rule, string Family, string Owner, string Means, string? Fix, string? Evidence);

    private static async Task<List<Row>> RulesAsync(string query = "")
    {
        using var client = ApiTestFactory.Shared.CreateClient();
        var resp = await client.GetAsync($"/api/rules{query}");
        await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.OK);
        return (await resp.Content.ReadFromJsonAsync<List<Row>>(
            new JsonSerializerOptions(JsonSerializerDefaults.Web)))!;
    }

    /// <summary>The whole point in one call: an id a reader met in a refusal, answered with what it means and
    /// what to do. <c>SP7</c> because it is the one that prompted this — a layout rule nobody could look up.
    /// </summary>
    [Test]
    public async Task One_rule_can_be_asked_about_by_id()
    {
        var rules = await RulesAsync("?rule=SP7");

        var sp7 = rules.Single();
        await Assert.That(sp7.Family).IsEqualTo("SP");
        await Assert.That(sp7.Owner).Contains("rules.md");
        await Assert.That(sp7.Means).Contains("iron");
        // A layout rule carries no fix — it is a claim about how a map plays, which is the author's to state —
        // and carries how well the corpus backs it instead.
        await Assert.That(sp7.Fix).IsNull();
        await Assert.That(sp7.Evidence).IsEqualTo("corpus");
    }

    /// <summary>A gate rule is the other shape: mechanical, so what to do about one is derivable and is
    /// stated.</summary>
    [Test]
    public async Task A_gate_rule_says_what_to_do_about_it()
    {
        var pl1 = (await RulesAsync("?rule=PL1")).Single();

        await Assert.That(pl1.Family).IsEqualTo("PL");
        await Assert.That(pl1.Owner).IsEqualTo("PgmStudio.Pgm.Plan.PlanRules.NoLand");
        await Assert.That(pl1.Means).Contains("no land");
        await Assert.That(pl1.Fix).IsNotNull();
        await Assert.That(pl1.Evidence).IsNull();
    }

    /// <summary>An id nobody has is an empty list, not a 404 — a caller asking "is there a rule called that"
    /// should not have to tell an absent rule from a typo in the route by the status code.</summary>
    [Test]
    public async Task An_id_that_is_not_a_rule_is_an_empty_list()
    {
        await Assert.That(await RulesAsync("?rule=ZZ99")).IsEmpty();
    }

    [Test]
    public async Task A_family_can_be_asked_for_on_its_own()
    {
        var wingJoints = await RulesAsync("?family=hj");

        await Assert.That(wingJoints.Select(rule => rule.Rule))
            .IsEquivalentTo(new[] { "HJ1", "HJ2", "HJ3", "HJ4", "HJ5" });
    }

    /// <summary>Every rule says something. An id listed with an empty description is worse than one that is
    /// missing, because it looks answered — and it is exactly what happens when a rule is added without the
    /// docstring the catalogue reads.</summary>
    [Test]
    public async Task No_rule_is_listed_without_a_description()
    {
        var silent = (await RulesAsync()).Where(rule => string.IsNullOrWhiteSpace(rule.Means)).ToList();

        await Assert.That(silent.Select(rule => $"{rule.Rule} ({rule.Owner})")).IsEmpty();
    }

    /// <summary>And every gate rule says what to do about it. The fix is the <c>&lt;remarks&gt;</c> of the same
    /// docstring, so this fails on a rule added with a summary and nothing else.</summary>
    [Test]
    public async Task No_gate_rule_is_listed_without_a_fix()
    {
        var unhelpable = (await RulesAsync())
            .Where(rule => !rule.Owner.Contains("rules.md") && string.IsNullOrWhiteSpace(rule.Fix)).ToList();

        await Assert.That(unhelpable.Select(rule => $"{rule.Rule} ({rule.Owner})")).IsEmpty();
    }

    /// <summary>
    /// <b>The check that stops the catalogue going quietly incomplete.</b> The endpoint names the assemblies it
    /// reads, which is deliberate — an assembly nothing has touched is not loaded, so sweeping the app domain
    /// would drop a family depending on what the process happened to do first. The cost of naming them is that
    /// a new <c>*Rules</c> class in a project not on the list is invisible and nothing fails.
    ///
    /// <para>So this sweeps every <c>PgmStudio</c> assembly that actually shipped beside the test binary, finds
    /// every constant shaped like a rule id, and asserts the endpoint answered all of them. It fails with the
    /// ids and their declaring types, which is the list of what to add.</para>
    /// </summary>
    [Test]
    public async Task Every_rule_id_declared_anywhere_is_in_the_catalogue()
    {
        var answered = (await RulesAsync()).Select(rule => rule.Rule).ToHashSet();

        var declared = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var dll in Directory.GetFiles(AppContext.BaseDirectory, "PgmStudio.*.dll"))
        {
            System.Reflection.Assembly assembly;
            try { assembly = System.Reflection.Assembly.LoadFrom(dll); }
            catch (BadImageFormatException) { continue; }

            Type[] types;
            try { types = assembly.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = [.. ex.Types.OfType<Type>()]; }

            foreach (var type in types)
                foreach (var field in type.GetFields(
                             BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                {
                    if (!field.IsLiteral || field.FieldType != typeof(string)) continue;
                    if (field.GetRawConstantValue() is not string id || !IdShape.IsMatch(id)) continue;
                    declared[id] = $"{type.FullName}.{field.Name}";
                }
        }

        // A sweep that finds nothing would pass this vacuously, which is the one way it could be useless.
        await Assert.That(declared).IsNotEmpty();
        await Assert.That(declared.Where(rule => !answered.Contains(rule.Key))
            .Select(rule => $"{rule.Key} declared at {rule.Value}")).IsEmpty();
    }
}
