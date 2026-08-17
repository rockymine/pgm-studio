using PgmStudio.Minecraft.Render;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// What the structure render treats as one thing seen twice. On a mirrored board the question an author
/// brings to the picture is whether both halves hold the same buildings, and a colour per finding cannot
/// answer it: the pair comes out in two hues and reads as two different things. The identity is the owner
/// with its per-image index dropped, so what is asserted here is which owners collapse together and which
/// stay apart.
/// </summary>
public sealed class StructureFinderIdentityTests
{
    [Test]
    [Arguments("house:w1:0", "house:w1:1")]      // the orbit index is the last segment
    [Arguments("spawn:0", "spawn:1")]            // a running index into the already-fanned list
    [Arguments("destroyable:0", "destroyable:1")]
    [Arguments("core:0", "core:1")]
    [Arguments("spawn:0:iron:0", "spawn:1:iron:0")]   // an index in the middle as well as at the end
    public async Task Both_images_of_one_thing_share_an_identity(string first, string second)
    {
        await Assert.That(StructureFinder.Identity(first)).IsEqualTo(StructureFinder.Identity(second));
    }

    [Test]
    public async Task Two_different_buildings_keep_two_identities()
    {
        // The half that matters as much: collapsing the images must not collapse the buildings.
        await Assert.That(StructureFinder.Identity("house:w1:0"))
                    .IsNotEqualTo(StructureFinder.Identity("house:w2:0"));
    }

    [Test]
    public async Task Two_families_keep_two_identities()
    {
        await Assert.That(StructureFinder.Identity("spawn:0"))
                    .IsNotEqualTo(StructureFinder.Identity("spawn:0:iron:0"));
        await Assert.That(StructureFinder.Identity("wool:0"))
                    .IsNotEqualTo(StructureFinder.Identity("roomfloor:0"));
    }

    [Test]
    public async Task A_claim_with_no_owner_has_no_identity()
    {
        // A world with no provenance reports every finding this way, and nothing there says which two are
        // one thing — so it must not read as "all one identity" and collapse the whole board to one colour.
        await Assert.That(StructureFinder.Identity("")).IsEqualTo("");
    }
}
