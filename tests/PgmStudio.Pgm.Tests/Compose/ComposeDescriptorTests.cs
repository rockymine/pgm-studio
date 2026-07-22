using PgmStudio.Pgm.Compose;

namespace PgmStudio.Pgm.Tests.Compose;

/// <summary>The canonical versioned request descriptor for a generated plan: <see cref="ComposeDescriptor.For"/>
/// stamps the current composer + schema version and carries every request field (including a full-range seed),
/// and the JSON round-trips exactly.</summary>
public sealed class ComposeDescriptorTests
{
    [Test]
    public async Task For_stamps_versions_and_carries_every_request_field()
    {
        var request = new ComposeRequest(12, 2, "mirror_z", seed: 12345678901234567890UL, cell: 6);
        var d = ComposeDescriptor.For(request);

        await Assert.That(d.Schema).IsEqualTo(ComposeDescriptor.CurrentSchema);
        await Assert.That(d.ComposerVersion).IsEqualTo(ComposerVersion.Current);
        await Assert.That(d.PlayersPerTeam).IsEqualTo(12);
        await Assert.That(d.Teams).IsEqualTo(2);
        await Assert.That(d.Symmetry).IsEqualTo("mirror_z");
        await Assert.That(d.Cell).IsEqualTo(6);
        await Assert.That(d.Seed).IsEqualTo(12345678901234567890UL);
    }

    [Test]
    public async Task Json_round_trips_including_a_full_range_seed()
    {
        var d = ComposeDescriptor.For(new ComposeRequest(30, 4, "rot_90", seed: ulong.MaxValue, cell: 5));
        var back = ComposeDescriptor.Parse(d.ToJson());

        await Assert.That(back).IsEqualTo(d);
        await Assert.That(back!.Seed).IsEqualTo(ulong.MaxValue);
    }
}
