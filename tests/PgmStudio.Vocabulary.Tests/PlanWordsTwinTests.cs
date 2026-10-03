using System.Text.RegularExpressions;

namespace PgmStudio.Vocabulary.Tests;

/// <summary>
/// The plan editor's words, spelled once in <c>PgmStudio.Vocabulary</c> and once more in
/// <c>plan-doc.js</c>, which draws and edits the same document in the browser and cannot reach C#.
///
/// <para>The JS arrays are twins, not the authority: this reads them out of the module's own source and
/// holds each to the set it mirrors, in the same order. A word added to a set here and not there is a
/// role the canvas folds to a plain piece or a facing it cannot turn, which nothing else would notice until
/// an author drew it.</para>
/// </summary>
public sealed class PlanWordsTwinTests
{
    private static readonly string Source = File.ReadAllText(PlanDoc());

    [Test]
    public async Task The_roles_the_canvas_offers_are_the_roles_a_piece_may_carry()
    {
        await Assert.That(Words("ROLES")).IsEqualTo(Joined(PlanRoles.All));
        await Assert.That(Words("GENERATING_ROLES")).IsEqualTo(Joined(PlanRoles.All.Where(PlanRoles.IsGenerating)));
        await Assert.That(Words("TECHNICAL_ROLES")).IsEqualTo(Joined(PlanRoles.All.Where(PlanRoles.IsAnnotation)));
    }

    [Test]
    public async Task The_box_kinds_the_canvas_offers_are_the_kinds_a_box_may_carry()
        => await Assert.That(Words("BOX_KINDS")).IsEqualTo(Joined(PlanBoxKinds.All));

    [Test]
    public async Task The_facings_the_canvas_cycles_are_the_directions_a_spawn_may_look()
    {
        await Assert.That(Words("FACINGS")).IsEqualTo(Joined(SpawnFacings.All));

        var steps = Regex.Matches(Block("FACING_DIR"), "\"(?<word>[a-z-]+)\":\\s*\\[(?<dx>-?\\d),\\s*(?<dz>-?\\d)\\]")
            .ToDictionary(match => match.Groups["word"].Value,
                match => (int.Parse(match.Groups["dx"].Value), int.Parse(match.Groups["dz"].Value)));
        await Assert.That(steps.Count).IsEqualTo(SpawnFacings.All.Length);
        foreach (var facing in SpawnFacings.All)
            await Assert.That(steps[facing]).IsEqualTo(SpawnFacings.Direction(facing));
    }

    private static string Joined(IEnumerable<string> words) => string.Join(", ", words);

    /// <summary>The words of one exported array, in the order the module states them, comma-joined.</summary>
    private static string Words(string name)
    {
        var match = Regex.Match(Source, $@"export const {name} = \[(?<words>[^\]]*)\];");
        if (!match.Success) throw new InvalidOperationException($"plan-doc.js no longer exports an array named {name}");
        return Joined(Regex.Matches(match.Groups["words"].Value, "\"([^\"]+)\"").Select(word => word.Groups[1].Value));
    }

    /// <summary>The text of one exported object, from its declaration to its closing brace.</summary>
    private static string Block(string name)
    {
        var match = Regex.Match(Source, $@"export const {name} = \{{(?<body>.*?)\n\}};", RegexOptions.Singleline);
        if (!match.Success) throw new InvalidOperationException($"plan-doc.js no longer exports an object named {name}");
        return match.Groups["body"].Value;
    }

    private static string PlanDoc()
    {
        var relative = Path.Combine("src", "PgmStudio.Client", "wwwroot", "js", "studio", "plan", "plan-doc.js");
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, relative))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new DirectoryNotFoundException(
            $"no {relative} above the test output — the repository layout moved"), relative);
    }
}
