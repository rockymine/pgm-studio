using PgmStudio.Client.Components;
using PgmStudio.Domain;

namespace PgmStudio.Api.Tests;

/// <summary>
/// The casing a stated lava interior implies has to be the same on both sides of the wire.
///
/// <para><see cref="ObjectiveDefaults.CoreCasing"/> is the authority — it is what the compiler resolves and
/// the stamper builds — and <see cref="CoreCasing.Of"/> is the client's copy of it, which exists because the
/// WASM half cannot reach <c>Domain</c>. This is the only project that can see both. The sweep covers every
/// footprint and height an author can state with room either side, open and capped, because an off-by-one
/// agrees with the truth almost everywhere.</para>
/// </summary>
public sealed class CoreCasingDriftTests
{
    [Test]
    public async Task The_clients_copy_answers_what_the_generator_answers()
    {
        for (var lava = 0; lava <= 12; lava++)
        for (var lavaHeight = 0; lavaHeight <= 12; lavaHeight++)
        foreach (var openTop in new[] { false, true })
            await Assert.That(CoreCasing.Of(lava, lavaHeight, openTop))
                .IsEqualTo(ObjectiveDefaults.CoreCasing(lava, lavaHeight, openTop));
    }
}
