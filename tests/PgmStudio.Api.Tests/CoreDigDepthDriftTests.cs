using PgmStudio.Client.Components;
using PgmStudio.Domain;

namespace PgmStudio.Api.Tests;

/// <summary>
/// The one number that has to be the same on both sides of the wire: how far players must dig under a core
/// before its lava can leak.
///
/// <para><see cref="ObjectiveDefaults.DigDepth"/> is the authority — it is what the compiler resolves, what
/// the stamper builds against and what the intent reports — and <see cref="CoreDig.Depth"/> is the client's
/// copy of it, which exists because the WASM half references <c>Contracts</c> and <c>Geom</c> only and cannot
/// reach <c>Domain</c>. The plan tool's marker panel and the Configure wizard's casing step both read it live
/// as an author drags the pair, so serving it would be a round trip for an integer.</para>
///
/// <para>This is the only project that can see both, which is what makes it the gate's home: the copy was two
/// copies and both were wrong by one course for as long as they existed, and nothing failed while they were.
/// The sweep is over the whole authorable range rather than a few samples, because an off-by-one agrees with
/// the truth almost everywhere.</para>
/// </summary>
public sealed class CoreDigDepthDriftTests
{
    [Test]
    public async Task The_clients_copy_answers_what_the_generator_answers()
    {
        for (var leak = 0; leak <= 32; leak++)
        for (var floatBlocks = 0; floatBlocks <= 32; floatBlocks++)
            await Assert.That(CoreDig.Depth(leak, floatBlocks))
                .IsEqualTo(ObjectiveDefaults.DigDepth(leak, floatBlocks));
    }

    /// <summary>And the rule itself, stated once against PGM rather than derived from either copy: the leak
    /// region is tested against the lava block's <b>centre</b>, so a block resting exactly at the leak level
    /// is half a block too high and the core leaks one course lower than that level reads.</summary>
    [Test]
    [Arguments(5, 6, 0)]    // the corpus centre — no dig
    [Arguments(5, 5, 1)]    // leak == float still costs the course the block centre does
    [Arguments(8, 5, 4)]    // basalt-reach's authored pair
    [Arguments(5, 4, 2)]    // stone_fields' measured dig
    public async Task The_dig_is_leak_plus_one_less_the_float(int leak, int floatBlocks, int expected)
        => await Assert.That(ObjectiveDefaults.DigDepth(leak, floatBlocks)).IsEqualTo(expected);
}
