using PgmStudio.Minecraft.Dressing;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// The dressing pass's typed ground record: who claimed a cell of which storey, whether anything did, and
/// how near a cell stands to a claim of one kind — the primitive the road standoff and the building's
/// route-blindness are both asked through.
/// </summary>
public sealed class GroundClaimsTests
{
    [Test]
    public async Task The_first_claimant_keeps_the_cell()
    {
        // The pass places in priority order, so a later claim on a held cell must not repaint who owns it —
        // a building that stamped over pavement owns its cells as a building even though the road got there
        // first and stayed in the record everywhere else.
        var claims = new GroundClaims().On(null);
        claims.Claim(5, 5, ClaimKind.Paving, "road");
        claims.Claim(5, 5, ClaimKind.Structure, "house");

        await Assert.That(claims.Holds(5, 5)).IsTrue();
        await Assert.That(claims.HoldsOtherThan(5, 5, ClaimKind.Paving)).IsFalse();
        await Assert.That(claims.HoldsOtherThan(5, 5, ClaimKind.Structure)).IsTrue();
        // And the record names which one, so a prop refused here can say what holds the ground.
        await Assert.That(claims.At(5, 5)).IsEqualTo(((ClaimKind, string)?)(ClaimKind.Paving, "road"));
        await Assert.That(claims.At(6, 6)).IsNull();
    }

    [Test]
    public async Task A_standoff_is_kept_at_its_own_distance_and_broken_inside_it()
    {
        // "Three blocks off the road" means a cell three away stands and a cell two away does not — the
        // boundary belongs to the prop, so the query is strictly-nearer-than.
        var claims = new GroundClaims().On(null);
        claims.Claim(10, 10, ClaimKind.Paving, "road");

        await Assert.That(claims.NearerThan(12, 10, ClaimKind.Paving, 3)).IsEqualTo(((int, int)?)(10, 10));
        await Assert.That(claims.NearerThan(13, 10, ClaimKind.Paving, 3)).IsNull();
        // Chebyshev: the diagonal neighbour is as near as the straight one.
        await Assert.That(claims.NearerThan(12, 12, ClaimKind.Paving, 3)).IsNotNull();
        await Assert.That(claims.NearerThan(13, 13, ClaimKind.Paving, 3)).IsNull();
    }

    [Test]
    public async Task A_standoff_only_measures_to_its_own_kind()
    {
        // A tree's standoff names the road; a wall of water or an earlier rock nearby is occupancy's
        // question, not distance's.
        var claims = new GroundClaims().On(null);
        claims.Claim(10, 10, ClaimKind.Water, "channel");
        claims.Claim(11, 10, ClaimKind.Scatter, "rock");

        await Assert.That(claims.NearerThan(12, 10, ClaimKind.Paving, 3)).IsNull();
    }

    [Test]
    public async Task A_storey_holds_only_its_own_ground()
    {
        // A stacked board carries a surface per storey, so the same (x, z) is a different cell on each. A
        // channel carved into the ground must leave a prop resting on an island above it alone.
        var book = new GroundClaims();
        book.On("ground").Claim(7, 7, ClaimKind.Water, "river");

        await Assert.That(book.On("ground").Holds(7, 7)).IsTrue();
        await Assert.That(book.On("sky").Holds(7, 7)).IsFalse();
        await Assert.That(book.On("sky").At(7, 7)).IsNull();
        await Assert.That(book.On("sky").NearerThan(7, 7, ClaimKind.Water, 4)).IsNull();
        // And a prop that names no layer rests on the top surface, which is a storey of its own.
        await Assert.That(book.On(null).Holds(7, 7)).IsFalse();
    }
}
