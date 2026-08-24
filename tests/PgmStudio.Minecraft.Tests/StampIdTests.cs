using PgmStudio.Domain;
using PgmStudio.Minecraft.Anvil;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// What a provenance claim says about the thing that made it. The record used to be a hand-built string whose
/// number meant the orbit image at one stamp site and a running index into the already-fanned list at every
/// other, so two entries of one form meant different things and nothing could pair a stamp with its own
/// mirror. What is asserted here is the property that replaced it: identity and image are separate answers.
/// </summary>
public sealed class StampIdTests
{
    [Test]
    public async Task Two_images_of_one_unit_share_an_identity_and_differ_only_in_the_image()
    {
        var authored = new StampId("house", "w1", 0);
        var mirror = authored.At(1);

        await Assert.That(mirror.Identity).IsEqualTo(authored.Identity);
        await Assert.That(mirror.Image).IsEqualTo(1);
        await Assert.That(mirror).IsNotEqualTo(authored);
    }

    [Test]
    public async Task Two_units_of_one_kind_do_not_share_an_identity()
    {
        // The half that matters as much: collapsing the images must not collapse the things.
        await Assert.That(new StampId("house", "w1", 0).Identity)
                    .IsNotEqualTo(new StampId("house", "w2", 0).Identity);
        await Assert.That(new StampId("spawn", "0", 0).Identity)
                    .IsNotEqualTo(new StampId("wool", "0", 0).Identity);
    }

    [Test]
    public async Task A_claim_answers_the_id_that_made_it()
    {
        var provenance = new WorldProvenance();
        var stamp = new StampId("spawn", "s1", 1);
        provenance.ClaimRect(0, 0, 2, 2, ProvenancePass.Structure, stamp);

        await Assert.That(provenance.OwnerAt(1, 1)).IsEqualTo(stamp);
        await Assert.That(provenance.OwnerAt(1, 1)!.Value.Image).IsEqualTo(1);
    }

    [Test]
    public async Task The_terrain_carries_a_layer_and_no_id()
    {
        // A null owner and an unclaimed column are different answers, and the layer is what tells them apart.
        var provenance = new WorldProvenance();
        provenance.Claim(0, 0, ProvenancePass.Ground);

        await Assert.That(provenance.OwnerAt(0, 0)).IsNull();
        await Assert.That(provenance.PassAt(0, 0)).IsEqualTo(ProvenancePass.Ground);
        await Assert.That(provenance.PassAt(9, 9)).IsNull();          // never claimed at all
    }

    [Test]
    public async Task A_prop_claim_is_not_a_structure_claim()
    {
        // The distinction the Prop layer exists for: a reader asking for buildings must not be handed a tree,
        // however the tree's blocks happen to read.
        var provenance = new WorldProvenance();
        provenance.Claim(0, 0, ProvenancePass.Prop, new StampId("tree", "t1", 0));
        provenance.Claim(1, 0, ProvenancePass.Structure, new StampId("house", "h1", 0));

        await Assert.That(provenance.PassAt(0, 0)).IsEqualTo(ProvenancePass.Prop);
        await Assert.That(provenance.PassAt(1, 0)).IsEqualTo(ProvenancePass.Structure);
    }

    [Test]
    public async Task An_id_survives_the_sidecar_it_is_written_to()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"stampid-{Guid.NewGuid():N}");
        try
        {
            var written = new WorldProvenance();
            written.Claim(0, 0, ProvenancePass.Prop, new StampId("tree", "t1", 0));
            written.Claim(1, 0, ProvenancePass.Prop, new StampId("tree", "t1", 1));
            written.Claim(2, 0, ProvenancePass.Structure, new StampId("house", "h1", 0));
            WorldProvenanceFile.Write(written, dir);

            var read = WorldProvenanceFile.TryRead(dir)!;

            // Three fields, three layers, both images — all of it back, or the record proves nothing to a
            // reader that comes to it off disk rather than in memory.
            await Assert.That(read.OwnerAt(0, 0)).IsEqualTo(new StampId("tree", "t1", 0));
            await Assert.That(read.OwnerAt(1, 0)).IsEqualTo(new StampId("tree", "t1", 1));
            await Assert.That(read.PassAt(1, 0)).IsEqualTo(ProvenancePass.Prop);
            await Assert.That(read.PassAt(2, 0)).IsEqualTo(ProvenancePass.Structure);
            await Assert.That(read.OwnerAt(0, 0)!.Value.Identity).IsEqualTo(read.OwnerAt(1, 0)!.Value.Identity);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
