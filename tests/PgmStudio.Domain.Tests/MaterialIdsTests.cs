using PgmStudio.Domain;

namespace PgmStudio.Domain.Tests;

/// <summary>
/// PGM material matches → block ids (<see cref="MaterialIds"/>). The table is partial by design, so the
/// behaviour that matters most is what it does with a name it does not hold.
/// </summary>
public sealed class MaterialIdsTests
{
    [Test]
    public async Task A_plain_name_resolves()
        => await Assert.That(MaterialIds.Resolve("stained glass")).IsEquivalentTo(new[] { 95 });

    [Test]
    public async Task Separators_and_case_do_not_matter()
    {
        // PGM's own lookup treats these as one name, so a map may write any of them.
        await Assert.That(MaterialIds.Resolve("STAINED_GLASS")).IsEquivalentTo(new[] { 95 });
        await Assert.That(MaterialIds.Resolve("stained-glass")).IsEquivalentTo(new[] { 95 });
        await Assert.That(MaterialIds.Resolve("  stained   glass ")).IsEquivalentTo(new[] { 95 });
    }

    [Test]
    public async Task A_data_value_selects_the_same_block()
    {
        // The nibble is a colour or variant; a rule naming the block names every one of them.
        await Assert.That(MaterialIds.Resolve("stained glass:5")).IsEquivalentTo(new[] { 95 });
    }

    [Test]
    public async Task A_multi_pattern_match_resolves_each_pattern()
        => await Assert.That(MaterialIds.Resolve("air;water")).IsEquivalentTo(new[] { 0, 8 });

    [Test]
    public async Task An_unknown_name_resolves_to_nothing()
    {
        await Assert.That(MaterialIds.Resolve("some future block")).IsEmpty();
        await Assert.That(MaterialIds.Resolve("")).IsEmpty();
    }

    [Test]
    public async Task A_partly_unknown_match_is_reported_as_not_fully_resolved()
    {
        // "some ids" and "every id" are different answers, and a caller deleting blocks needs the second.
        await Assert.That(MaterialIds.Resolve("stained glass;some future block")).IsEquivalentTo(new[] { 95 });
        await Assert.That(MaterialIds.ResolvesFully("stained glass;some future block")).IsFalse();
        await Assert.That(MaterialIds.ResolvesFully("stained glass;obsidian")).IsTrue();
        await Assert.That(MaterialIds.ResolvesFully("")).IsFalse();
    }
}
