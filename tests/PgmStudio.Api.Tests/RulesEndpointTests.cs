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

    private sealed record Row(
        string Rule, string Family, string Owner, string Means, string? Fix, string? Evidence,
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
    /// what to do. <c>WL2</c> because it is a layout rule — the half a reader has no other way to look
    /// up.</summary>
    [Test]
    public async Task One_rule_can_be_asked_about_by_id()
    {
        var rules = await RulesAsync("?rule=WL2");

        var wl2 = rules.Single();
        await Assert.That(wl2.Family).IsEqualTo("WL");
        await Assert.That(wl2.Owner).Contains("rules.md");
        await Assert.That(wl2.Means).Contains("spawn");
        // A layout rule carries no fix — it is a claim about how a map plays, which is the author's to state —
        // and carries how well the corpus backs it instead.
        await Assert.That(wl2.Fix).IsNull();
        await Assert.That(wl2.Evidence).IsEqualTo("corpus");
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

    /// <summary>Every gate rule says what it is about. A rule added without its <c>[Rule]</c> attribute
    /// arrives with no concerns and no category, which nothing else would catch — the catalogue lists it, it
    /// carries a sentence, and only the two machine-legible fields a caller branches on are missing.</summary>
    [Test]
    public async Task Every_gate_rule_says_what_it_is_about()
    {
        var silent = (await RulesAsync())
            .Where(rule => !rule.Owner.Contains("rules.md"))
            .Where(rule => rule.Concerns is not { Count: > 0 })
            .ToList();

        await Assert.That(silent.Select(rule => $"{rule.Rule} ({rule.Owner})")).IsEmpty();
    }

    /// <summary>And every gate rule a finding can cite says what to do about it. The four that do not are
    /// named, because a category is what a caller branches on and a rule quietly missing one reads as a rule
    /// nothing raises.</summary>
    [Test]
    public async Task Only_the_rules_nothing_raises_carry_no_category()
    {
        var uncategorized = (await RulesAsync())
            .Where(rule => !rule.Owner.Contains("rules.md") && rule.Category is null)
            .Select(rule => rule.Rule)
            .ToList();

        await Assert.That(uncategorized).IsEquivalentTo(NothingRaises);
    }

    /// <summary>
    /// <b>No id is answered twice.</b> The catalogue reads two sources — the constants and the bullets in
    /// <c>rules.md</c> — and nothing stops one id being in both: a rule declared in code and left stated as a
    /// bullet answers two rows, and a caller reading the first gets whichever the ordering happened to put
    /// there. <c>PC-C</c> is the live case, declared in <c>PlanRules</c> and cited by <c>rules.md</c> rather
    /// than stated in it; re-adding the bullet is the mistake this catches.
    /// </summary>
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

    /// <summary>A layout rule carries neither, and that is not an omission: it is stated as a markdown bullet,
    /// which has nowhere to write one.</summary>
    [Test]
    public async Task A_layout_rule_carries_neither()
    {
        var wl2 = (await RulesAsync("?rule=WL2")).Single();

        await Assert.That(wl2.Category).IsNull();
        await Assert.That(wl2.Concerns).IsNull();
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
    /// <b>Every layout rule answered can be met.</b> The catalogue answers the layout rules the studio can
    /// cite rather than all 92 <c>rules.md</c> states, because the question it exists for is <i>what is this
    /// finding</i>, and <c>RuleCatalog.Raised</c> is where that subset is stated. Nothing in the compiler
    /// connects that set to the three kinds of site that name one — a plan validator lint, an evaluator
    /// term's <c>RuleId</c>, a producibility finding's <c>Cites</c> — so this does, in both directions: a row
    /// answered that no source names is a rule with no finding to explain, and an id a source names that the
    /// catalogue does not answer is a finding a reader cannot look up.
    ///
    /// <para>The sweep is over <c>src/</c> as text rather than by reflection, because two of the three kinds
    /// of site are string literals inside method bodies and nothing can reach them any other way. An id
    /// spelled in a comment counts as a citation, which is the loose direction: it over-approximates what a
    /// caller can meet, and over-approximating is the harmless half.</para>
    /// </summary>
    [Test]
    public async Task Every_layout_rule_answered_is_one_a_source_can_cite()
    {
        var answered = (await RulesAsync())
            .Where(rule => rule.Owner.Contains("rules.md"))
            .Select(rule => rule.Rule)
            .ToHashSet(StringComparer.Ordinal);

        var stated = Stated();
        var cited = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(Source(), "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")) continue;
            foreach (Match match in Citation.Matches(File.ReadAllText(file)))
                if (stated.Contains(match.Groups[1].Value)) cited.Add(match.Groups[1].Value);
        }

        // A sweep that finds nothing would pass one direction vacuously.
        await Assert.That(cited).IsNotEmpty();
        await Assert.That(answered.Except(cited).Order(StringComparer.Ordinal)).IsEmpty();
        await Assert.That(cited.Except(answered).Order(StringComparer.Ordinal)).IsEmpty();
    }

    /// <summary>An id as a source names one: a bare string literal, which is how all three kinds of site
    /// spell it.</summary>
    private static readonly Regex Citation = new(@"""([A-Z]{1,3}(?:-[A-Z]+|[0-9]+))""");

    /// <summary>Every id <c>rules.md</c> states, so a literal that merely looks like one — a task id, a
    /// block name — is not mistaken for a citation.</summary>
    private static HashSet<string> Stated()
    {
        var bullet = new Regex(@"^- \*\*(?<id>[A-Z]{1,3}(?:-[A-Z]+|[0-9]+))\b");
        var path = Path.Combine(Source(), "..", "docs", "generator", "rules.md");
        return [.. File.ReadLines(path)
            .Select(line => bullet.Match(line))
            .Where(match => match.Success)
            .Select(match => match.Groups["id"].Value)];
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
                    declared[id] = $"{type.FullName}.{field.Name}";
                }
        }

        // A sweep that finds nothing would pass this vacuously, which is the one way it could be useless.
        await Assert.That(declared).IsNotEmpty();
        await Assert.That(declared.Where(rule => !answered.Contains(rule.Key))
            .Select(rule => $"{rule.Key} declared at {rule.Value}")).IsEmpty();
    }
}
