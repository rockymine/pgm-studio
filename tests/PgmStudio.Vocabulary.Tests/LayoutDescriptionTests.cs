namespace PgmStudio.Vocabulary.Tests;

/// <summary>A layout's written description names what its descriptor says, in glossary words, and every word
/// it links is one the glossary defines.</summary>
public class LayoutDescriptionTests
{
    private static IReadOnlyList<DescriptionPart> Describe(
        int players = 10, int teams = 2, string symmetry = SymmetryModes.Rot180, int wools = 1,
        string[]? forms = null, string hub = "ring", string frontline = "twin") =>
        LayoutDescription.Of(players, teams, symmetry, wools, forms ?? ["l"], hub, frontline);

    [Test]
    public async Task A_one_wool_layout_reads_as_one_sentence_per_idea()
    {
        var text = LayoutDescription.Plain(Describe());

        await Assert.That(text).IsEqualTo(
            "A two-team layout for 6–13 players a team, copied by a half turn about the centre. "
            + "Each team has one wool, kept in a wool room and reached by an L-shaped approach. "
            + "The team's side is built around a ring-shaped hub, with a twin front line facing the mid.");
    }

    [Test]
    public async Task Several_wools_name_every_distinct_approach_shape_once()
    {
        var text = LayoutDescription.Plain(Describe(wools: 3, forms: ["i", "l", "l"]));

        await Assert.That(text).Contains("Each team has three wools, each kept in its own wool room");
        await Assert.That(text).Contains("reached by I-shaped and L-shaped approaches");
    }

    [Test]
    public async Task A_side_without_a_front_line_piece_names_the_hub_edge_as_its_front_line()
    {
        var text = LayoutDescription.Plain(Describe(frontline: "none"));

        await Assert.That(text).Contains("hub, whose own edge facing the mid is the side's front line");
    }

    [Test]
    [Arguments(10, "6–13")]
    [Arguments(18, "14–21")]
    [Arguments(40, "32+")]
    public async Task The_players_are_the_size_bands_range(int players, string range)
    {
        await Assert.That(LayoutDescription.Plain(Describe(players: players))).Contains($"{range} players a team");
    }

    [Test]
    public async Task Every_linked_word_is_a_glossary_term()
    {
        var terms = Glossary.Terms.Select(term => term.Term).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var linked = new[] { Describe(), Describe(frontline: "none"), Describe(wools: 2, forms: ["u", "donut"]) }
            .SelectMany(parts => parts).Where(part => part.Term is not null).Select(part => part.Term!).Distinct();

        await Assert.That(linked.Where(term => !terms.Contains(term))).IsEmpty();
    }
}
