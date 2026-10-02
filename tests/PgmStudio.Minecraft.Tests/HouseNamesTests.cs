using PgmStudio.Minecraft.Houses;
using PgmStudio.Vocabulary;
using PgmStudio.Minecraft.Library;
namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// What a library house style may be called (<see cref="HouseNames"/>, <c>HS19</c>): describing words, then the
/// kind of building, every word from one of the two lists. Proved against the names the author called
/// misleading, and against every name the library is seeded with.
/// </summary>
public sealed class HouseNamesTests
{
    [Test]
    [Arguments("brick-roofed-stone-cottage")]
    [Arguments("oak-stilt-house")]
    [Arguments("hay-gambrel-barn")]
    [Arguments("brick-roofed-stone-and-dark-oak-house")]
    public async Task Describing_words_and_a_building_make_a_name(string name)
        => await Assert.That(HouseNames.Check(name)).IsEmpty();

    /// <summary>The names the author called misleading, and the shapes a name may not take: a board's prefix, a
    /// role a room plays on a map, a place, an occupation, a bare building, and anything not lowercase words
    /// joined by hyphens.</summary>
    [Test]
    [Arguments("17h-croft")]
    [Arguments("sb-spawn")]
    [Arguments("showcase-hall")]
    [Arguments("talltimber-cottage-jungle")]
    [Arguments("townside")]
    [Arguments("stonemason")]
    [Arguments("cottage")]
    [Arguments("stone-workshop")]
    [Arguments("Oak-House")]
    [Arguments("oak house")]
    [Arguments("oak--house")]
    [Arguments("")]
    public async Task A_name_for_where_a_style_was_used_is_HS19(string name)
    {
        var finding = HouseNames.Check(name).Single();
        await Assert.That(finding.Rule).IsEqualTo(HouseStyleRules.LibraryName);
        await Assert.That(finding.Severity).IsEqualTo(Severity.Complaint);
        await Assert.That(finding.Field).IsEqualTo("name");
    }

    /// <summary>The finding names the word at fault, so a rename starts from the word and not from the list.</summary>
    [Test]
    public async Task The_finding_names_the_words_the_lists_do_not_hold()
    {
        var finding = HouseNames.Check("showcase-stone-workshop").Single();
        await Assert.That(finding.Message).Contains("'showcase'");
        await Assert.That(finding.Message).Contains("'workshop'");
    }

    /// <summary>Every style the library is seeded with is named by the rule it is saved under.</summary>
    [Test]
    public async Task Every_seeded_style_is_named_by_the_rule()
    {
        var names = SeedFolder.Houses.Select(house => house.Name);
        var misnamed = names.Where(name => HouseNames.Check(name) != Findings.None).ToList();
        await Assert.That(misnamed).IsEmpty();
    }

    /// <summary>A word is in one list or the other for what it says, and a list that grew a duplicate in
    /// another case would accept a name the endpoint never offered.</summary>
    [Test]
    public async Task Every_word_is_lowercase_letters()
    {
        var words = HouseNames.Describing.Concat(HouseNames.Buildings).ToList();
        await Assert.That(words.Where(word => !word.All(char.IsAsciiLetterLower)).ToList()).IsEmpty();
    }
}
