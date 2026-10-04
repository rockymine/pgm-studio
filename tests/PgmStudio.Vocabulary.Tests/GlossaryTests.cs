namespace PgmStudio.Vocabulary.Tests;

/// <summary>
/// The glossary is the one place a word is defined, so it has to hold together on its own: one entry per
/// word, every other name pointing at exactly one term, every word a definition leans on defined too, and
/// every definition short and plain enough to read in a tooltip.
/// </summary>
public class GlossaryTests
{
    private static readonly StringComparer Words = StringComparer.OrdinalIgnoreCase;

    [Test]
    public async Task Every_word_is_one_entry()
    {
        var doubled = Glossary.Terms.GroupBy(term => term.Term, Words).Where(group => group.Count() > 1)
            .Select(group => group.Key);

        await Assert.That(doubled).IsEmpty();
    }

    /// <summary>Another name answers one term and is never a term itself, or looking it up would answer two
    /// things.</summary>
    [Test]
    public async Task Every_other_name_points_at_one_term()
    {
        var terms = Glossary.Terms.Select(term => term.Term).ToHashSet(Words);
        var names = Glossary.Terms.SelectMany(term => term.AlsoCalled).ToList();

        await Assert.That(names.Where(terms.Contains)).IsEmpty();
        await Assert.That(names.GroupBy(name => name, Words).Where(group => group.Count() > 1).Select(group => group.Key))
            .IsEmpty();
    }

    [Test]
    public async Task Every_related_word_is_defined()
    {
        var terms = Glossary.Terms.Select(term => term.Term).ToHashSet(Words);
        var dangling = Glossary.Terms
            .SelectMany(term => term.Related.Where(related => !terms.Contains(related)).Select(related => $"{term.Term} -> {related}"));

        await Assert.That(dangling).IsEmpty();
    }

    /// <summary>A definition is read in a tooltip and by an agent before a run: at most 30 words, and none of
    /// the markup or punctuation the rule texts have dropped.</summary>
    [Test]
    public async Task Every_definition_is_short_and_plain()
    {
        var wordy = Glossary.Terms.Where(term => term.Definition.Split(' ').Length > 30).Select(term => term.Term);
        var marked = Glossary.Terms.Where(term => term.Definition.IndexOfAny(['`', '—', '≥', '→']) >= 0).Select(term => term.Term);

        await Assert.That(wordy).IsEmpty();
        await Assert.That(marked).IsEmpty();
    }

    [Test]
    public async Task A_word_is_found_by_its_own_name_or_another()
    {
        await Assert.That(Glossary.Find("Layout").Single().Term).IsEqualTo("layout");
        await Assert.That(Glossary.Find("board").Single().Term).IsEqualTo("layout");
        await Assert.That(Glossary.Find("destroyable").Single().Term).IsEqualTo("monument");
        await Assert.That(Glossary.Find("not a word")).IsEmpty();
    }
}
