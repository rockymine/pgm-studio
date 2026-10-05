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
///
/// <para>A layout rule is a constant like any other, in <c>LayoutRules</c>.</para>
/// </summary>
[NotInParallel("api-db")]
public sealed class RulesEndpointTests
{
    /// <summary>A rule id: one to three letters, then a number or a letter suffix (<c>PC-C</c>). A one-letter
    /// id counts only on a field marked <c>[Rule]</c>, as the catalogue reads it.</summary>
    private static readonly Regex IdShape = new(@"^[A-Z]{1,3}(?:-[A-Z]+|[0-9]+)$");

    /// <summary>The class every layout rule is declared in.</summary>
    private static readonly Type Layout = typeof(PgmStudio.Domain.LayoutRules);

    private sealed record Row(
        string Rule, string Family, string Owner, string Means, string? Fix,
        string? Category, List<string>? Concerns);

    /// <summary>The gate rules that carry no category, named rather than counted. Each states how a room
    /// frame is derived and no finding cites one, so there is no caller to branch and nothing to do about it
    /// — they are constants because a rule may not live only in a markdown file. Any other rule arriving
    /// without one is a rule added without its <c>[Rule]</c> attribute.</summary>
    private static readonly string[] NothingRaises = ["WX1", "WX5", "WX7", "WX9"];

    private static async Task<List<Row>> RulesAsync(string query = "")
    {
        using var client = ApiTestFactory.Shared.CreateClient();
        var resp = await client.GetAsync($"/api/rules{query}");
        await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.OK);
        return (await resp.Content.ReadFromJsonAsync<List<Row>>(
            new JsonSerializerOptions(JsonSerializerDefaults.Web)))!;
    }

    /// <summary>The whole point in one call: an id a reader met in a finding, answered with what it means and
    /// what to do.</summary>
    [Test]
    public async Task One_rule_can_be_asked_about_by_id()
    {
        var rules = await RulesAsync("?rule=WL2");

        var wl2 = rules.Single();
        await Assert.That(wl2.Family).IsEqualTo("WL");
        await Assert.That(wl2.Owner).StartsWith($"{Layout.FullName}.");
        await Assert.That(wl2.Means).Contains("spawn");
        await Assert.That(wl2.Fix).Contains("`pieces`");
    }

    /// <summary>A document field is marked in a fix and nowhere else: a meaning says what is wrong, and only
    /// what to do names a field to change.</summary>
    [Test]
    public async Task Only_a_fix_marks_a_field()
    {
        var marked = (await RulesAsync()).Where(rule => rule.Means.Contains('`')).Select(rule => rule.Rule).ToList();

        await Assert.That(marked).IsEmpty();
    }

    /// <summary>A gate rule is the other shape: mechanical, so what to do about one is derivable and is
    /// stated.</summary>
    [Test]
    public async Task A_gate_rule_says_what_to_do_about_it()
    {
        var pl1 = (await RulesAsync("?rule=PL1")).Single();

        await Assert.That(pl1.Family).IsEqualTo("PL");
        await Assert.That(pl1.Owner).IsEqualTo("PgmStudio.Pgm.Plan.PlanRules.NoGroundPiece");
        await Assert.That(pl1.Means).Contains("no piece that makes ground");
        await Assert.That(pl1.Fix).IsNotNull();
    }

    /// <summary>A number a rule's text states is the number the code checks: HP2 tells an author the least
    /// span a wing may have, and that span is a constant the text cannot name.</summary>
    [Test]
    public async Task HP2_states_the_span_the_room_frame_checks()
    {
        var hp2 = (await RulesAsync("?rule=HP2")).Single();
        var span = PgmStudio.Domain.RoomFrames.MinFootprintSpan;

        await Assert.That(hp2.Means).Contains($"less than {span} blocks across its shorter side");
        await Assert.That(hp2.Fix).Contains($"at least {span} blocks across its shorter side");
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

    /// <summary>And every rule says what to do about it, layout rules included. The fix is the
    /// <c>&lt;remarks&gt;</c> of the same docstring, so this fails on a rule added with a summary and nothing
    /// else.</summary>
    [Test]
    public async Task No_rule_is_listed_without_a_fix()
    {
        var unhelpable = (await RulesAsync()).Where(rule => string.IsNullOrWhiteSpace(rule.Fix)).ToList();

        await Assert.That(unhelpable.Select(rule => $"{rule.Rule} ({rule.Owner})")).IsEmpty();
    }

    /// <summary>Every rule says what it is about. A rule added without its <c>[Rule]</c> attribute arrives
    /// with no concerns and no category, which nothing else would catch — the catalogue lists it, it carries
    /// a sentence, and only the two machine-legible fields a caller branches on are missing.</summary>
    [Test]
    public async Task Every_rule_says_what_it_is_about()
    {
        var silent = (await RulesAsync())
            .Where(rule => rule.Concerns is not { Count: > 0 })
            .ToList();

        await Assert.That(silent.Select(rule => $"{rule.Rule} ({rule.Owner})")).IsEmpty();
    }

    /// <summary>And every rule a finding can cite carries a category. The four that do not are named, because
    /// a category is what a caller branches on and a rule quietly missing one reads as a rule nothing
    /// raises.</summary>
    [Test]
    public async Task Only_the_rules_nothing_raises_carry_no_category()
    {
        var uncategorized = (await RulesAsync())
            .Where(rule => rule.Category is null)
            .Select(rule => rule.Rule)
            .ToList();

        await Assert.That(uncategorized).IsEquivalentTo(NothingRaises);
    }

    /// <summary><b>No id is answered twice.</b> Nothing stops two classes declaring the same id, and a caller
    /// reading the first of two rows gets whichever the ordering happened to put there.</summary>
    [Test]
    public async Task No_rule_is_answered_twice()
    {
        var doubled = (await RulesAsync())
            .GroupBy(rule => rule.Rule)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key} from {string.Join(" and ", group.Select(rule => rule.Owner))}")
            .ToList();

        await Assert.That(doubled).IsEmpty();
    }

    /// <summary>The category is the axis a caller branches on: one word answers every rule they would act on
    /// the same way, whichever gate asks it.</summary>
    [Test]
    public async Task A_category_can_be_asked_for_on_its_own()
    {
        var internals = await RulesAsync("?category=INTERNAL");

        await Assert.That(internals.Select(rule => rule.Rule)).IsEquivalentTo(new[] { "EX3", "RQ2", "RQ6" });
        await Assert.That(internals.All(rule => rule.Category == "internal")).IsTrue();
    }

    /// <summary>
    /// <b>The escalation as a query.</b> <c>refusals.md</c> § <i>One question, asked at every grain</i> says
    /// reachability is asked at five grains by four gates and that nothing but that paragraph says so. Asking
    /// for the two concerns answers the plan half of it — <c>WX6</c> over one piece and <c>PL9</c> over the
    /// whole board — and leaves out the two that ask it of built ground.
    /// </summary>
    [Test]
    public async Task Concerns_narrow_rather_than_widen()
    {
        var both = (await RulesAsync("?concerns=objective&concerns=plan")).Select(rule => rule.Rule).ToList();

        await Assert.That(both).Contains("WX6");
        await Assert.That(both).Contains("PL9");
        await Assert.That(both).DoesNotContain("EX1");        // the built world, not the plan
        await Assert.That(both).DoesNotContain("PL5");        // the plan, but not an objective
    }

    /// <summary>A word outside the closed set is refused rather than answered with nothing. An empty list is
    /// the honest answer to "is there a rule called that"; it is the wrong answer to a mistyped category,
    /// which would read as "no rules do that".</summary>
    [Test]
    public async Task A_category_that_is_not_a_word_is_refused()
    {
        using var client = ApiTestFactory.Shared.CreateClient();
        var resp = await client.GetAsync("/api/rules?category=unpossible");

        await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        var refusal = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var finding = refusal.GetProperty("findings")[0];
        await Assert.That(finding.GetProperty("rule").GetString()).IsEqualTo("RQ1");
        await Assert.That(finding.GetProperty("field").GetString()).IsEqualTo("category");
        await Assert.That(finding.GetProperty("message").GetString()).Contains("unplayable");
    }

    /// <summary>
    /// <b>Every layout rule is raised by name.</b> A layout rule exists because a finding cites it, and the
    /// three kinds of site that cite one — a plan validator lint, an evaluator term's <c>RuleId</c>, a
    /// producibility finding's <c>Cites</c> — reach it as <c>LayoutRules.Name</c>. So every constant is
    /// named somewhere in <c>src/</c> outside its own file, and no layout id is spelled as a bare string
    /// literal, which is a citation the compiler cannot follow.
    /// </summary>
    [Test]
    public async Task Every_layout_rule_is_raised_by_name()
    {
        var constants = Layout.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral)
            .ToDictionary(field => field.Name, field => (string)field.GetRawConstantValue()!);
        var text = Directory.EnumerateFiles(Source(), "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                           && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                           && Path.GetFileName(file) != "LayoutRules.cs")
            .Select(File.ReadAllText)
            .ToList();

        await Assert.That(constants).IsNotEmpty();
        await Assert.That(constants.Keys
            .Where(name => !text.Any(source => Regex.IsMatch(source, $@"\bLayoutRules\.{name}\b")))
            .Order(StringComparer.Ordinal)).IsEmpty();
        await Assert.That(constants.Values
            .Where(id => text.Any(source => source.Contains($"\"{id}\"", StringComparison.Ordinal)))
            .Order(StringComparer.Ordinal)).IsEmpty();
    }

    /// <summary>The <c>src/</c> tree above the test output.</summary>
    private static string Source()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new DirectoryNotFoundException(
            "no src/ above the test output — the repository layout moved"), "src");
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
                    if (char.IsDigit(id[1]) && field.GetCustomAttribute<PgmStudio.Vocabulary.RuleAttribute>() is null)
                        continue;
                    declared[id] = $"{type.FullName}.{field.Name}";
                }
        }

        // A sweep that finds nothing would pass this vacuously, which is the one way it could be useless.
        await Assert.That(declared).IsNotEmpty();
        await Assert.That(declared.Where(rule => !answered.Contains(rule.Key))
            .Select(rule => $"{rule.Key} declared at {rule.Value}")).IsEmpty();
    }
}
