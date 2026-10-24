namespace PgmStudio.Vocabulary.Tests;

/// <summary>
/// The words a band stack's ending is offered and described in. The set is closed: the editor offers each word,
/// describes each differently, and a word outside it reads as the ending that holds the last band.
/// </summary>
public sealed class BandEndingsTests
{
    [Test]
    public async Task The_endings_offered_are_hold_hand_over_and_cycle()
    {
        await Assert.That(BandEndings.All).IsEquivalentTo(new[] { "hold", "handOver", "cycle" });
    }

    [Test]
    [Arguments("hold")]
    [Arguments("handOver")]
    [Arguments("cycle")]
    public async Task A_word_in_the_set_is_its_own_canonical_form(string word)
    {
        await Assert.That(BandEndings.Canonical(word)).IsEqualTo(word);
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("repeat")]
    [Arguments("Cycle")]
    [Arguments("loop")]
    public async Task A_word_outside_the_set_reads_as_hold(string? word)
    {
        await Assert.That(BandEndings.Canonical(word)).IsEqualTo(BandEndings.Hold);
    }

    [Test]
    public async Task Each_ending_is_described_in_words_of_its_own()
    {
        var descriptions = BandEndings.All.Select(BandEndings.Describe).ToList();

        await Assert.That(descriptions.Distinct().Count()).IsEqualTo(BandEndings.All.Length);
        await Assert.That(BandEndings.Describe(BandEndings.Cycle)).Contains("cycle");
        await Assert.That(BandEndings.Describe(BandEndings.Hold)).Contains("holds");
    }
}
