using PgmStudio.Domain;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Vocabulary;
namespace PgmStudio.Minecraft.Tests;

/// <summary>The build-pass provenance record: which layer claimed each column, last, so a renderer
/// can answer Ground/Structure from a recorded fact rather than from the block's own material.</summary>
public sealed class WorldProvenanceTests
{
    [Test]
    public async Task An_unclaimed_column_answers_null()
    {
        var provenance = new WorldProvenance();
        await Assert.That(provenance.LayerAt(5, 5)).IsNull();
    }

    [Test]
    public async Task A_claimed_column_answers_its_layer()
    {
        var provenance = new WorldProvenance();
        provenance.Claim(5, 5, ProvenanceLayer.Structure);
        await Assert.That(provenance.LayerAt(5, 5)).IsEqualTo(ProvenanceLayer.Structure);
    }

    [Test]
    public async Task A_later_claim_overwrites_an_earlier_one_at_the_same_column()
    {
        // The whole of "placement order": the rasterizer claims Ground first, a stamp claims Structure over
        // it later, and the later claim is what a render must read.
        var provenance = new WorldProvenance();
        provenance.Claim(5, 5, ProvenanceLayer.Ground);
        provenance.Claim(5, 5, ProvenanceLayer.Structure);
        await Assert.That(provenance.LayerAt(5, 5)).IsEqualTo(ProvenanceLayer.Structure);
    }

    [Test]
    public async Task An_earlier_claim_after_a_later_one_still_wins_because_it_was_called_last()
    {
        // The record has no notion of "pass order" beyond call order — a caller claiming Ground after
        // Structure genuinely means the column reverted, which is not a case any pass produces today but is
        // worth the record being honest about rather than pinning Structure as sticky.
        var provenance = new WorldProvenance();
        provenance.Claim(5, 5, ProvenanceLayer.Structure);
        provenance.Claim(5, 5, ProvenanceLayer.Ground);
        await Assert.That(provenance.LayerAt(5, 5)).IsEqualTo(ProvenanceLayer.Ground);
    }

    [Test]
    public async Task ClaimRect_covers_every_inclusive_column_and_nothing_outside_it()
    {
        var provenance = new WorldProvenance();
        provenance.ClaimRect(0, 0, 2, 2, ProvenanceLayer.Structure);

        await Assert.That(provenance.LayerAt(0, 0)).IsEqualTo(ProvenanceLayer.Structure);
        await Assert.That(provenance.LayerAt(2, 2)).IsEqualTo(ProvenanceLayer.Structure);   // inclusive corner
        await Assert.That(provenance.LayerAt(1, 1)).IsEqualTo(ProvenanceLayer.Structure);
        await Assert.That(provenance.LayerAt(3, 0)).IsNull();                               // one past the edge
        await Assert.That(provenance.Count).IsEqualTo(9);
    }

    [Test]
    public async Task Claim_over_a_cell_set_writes_every_cell_once()
    {
        var provenance = new WorldProvenance();
        provenance.Claim([(0, 0), (1, 0), (1, 1)], ProvenanceLayer.Ground);
        await Assert.That(provenance.Count).IsEqualTo(3);
        await Assert.That(provenance.LayerAt(1, 1)).IsEqualTo(ProvenanceLayer.Ground);
    }

    [Test]
    public async Task An_unclaimed_column_answers_no_owner_as_well_as_no_layer()
    {
        var provenance = new WorldProvenance();
        await Assert.That(provenance.OwnerAt(5, 5)).IsNull();
    }

    [Test]
    public async Task A_claim_with_no_owner_answers_null_while_its_layer_still_answers()
    {
        // NoOwner is a real, recorded answer — the column was claimed, by nothing identified — and has to
        // read differently from a column nothing ever claimed at all.
        var provenance = new WorldProvenance();
        provenance.Claim(5, 5, ProvenanceLayer.Ground);
        await Assert.That(provenance.OwnerAt(5, 5)).IsEqualTo(null);
    }

    [Test]
    public async Task A_claim_carries_the_owner_it_was_given()
    {
        var provenance = new WorldProvenance();
        provenance.Claim(5, 5, ProvenanceLayer.Structure, new StampId("house", "d-h1", 0));
        await Assert.That(provenance.OwnerAt(5, 5)).IsEqualTo(new StampId("house", "d-h1", 0));
    }

    [Test]
    public async Task A_later_claim_overwrites_an_earlier_claims_owner_too()
    {
        var provenance = new WorldProvenance();
        provenance.Claim(5, 5, ProvenanceLayer.Structure, new StampId("house", "d-h1", 0));
        provenance.Claim(5, 5, ProvenanceLayer.Structure, new StampId("house", "d-h2", 0));
        await Assert.That(provenance.OwnerAt(5, 5)).IsEqualTo(new StampId("house", "d-h2", 0));
    }

    [Test]
    public async Task ClaimRect_gives_every_column_in_it_the_same_owner()
    {
        var provenance = new WorldProvenance();
        provenance.ClaimRect(0, 0, 2, 2, ProvenanceLayer.Structure, new StampId("wool", "0", 0));
        await Assert.That(provenance.OwnerAt(0, 0)).IsEqualTo(new StampId("wool", "0", 0));
        await Assert.That(provenance.OwnerAt(2, 2)).IsEqualTo(new StampId("wool", "0", 0));
    }

    [Test]
    public async Task Claim_over_a_cell_set_gives_every_cell_the_same_owner()
    {
        var provenance = new WorldProvenance();
        provenance.Claim([(0, 0), (1, 0)], ProvenanceLayer.Structure, new StampId("house", "d-h1", 0));
        await Assert.That(provenance.OwnerAt(0, 0)).IsEqualTo(new StampId("house", "d-h1", 0));
        await Assert.That(provenance.OwnerAt(1, 0)).IsEqualTo(new StampId("house", "d-h1", 0));
    }
}
