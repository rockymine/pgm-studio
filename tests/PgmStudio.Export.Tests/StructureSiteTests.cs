using PgmStudio.Domain;
using PgmStudio.Export;
using PgmStudio.Geom;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Vocabulary;

namespace PgmStudio.Export.Tests;

/// <summary>
/// <c>WX11</c> — a stamped structure whose neighbours have no ground to meet it on.
///
/// <para>The fault it reports is one nothing else could see: a foundation seals the column under its whole
/// footprint and levels it at the footprint's highest, so a building whose neighbour is void or well below
/// meets the world with a sheer face of its own bedrock. The export gate answers on goals, the traversability
/// check on whether the objectives connect, and both are satisfied by a board that has one.</para>
/// </summary>
public sealed class StructureSiteTests
{
    /// <summary>A plateau at <paramref name="top"/> over a rectangle, as the terrain surface the check
    /// reads, and nothing anywhere else.</summary>
    private static Dictionary<(int X, int Z), int> Plateau(int minX, int minZ, int maxX, int maxZ, int top)
    {
        var surface = new Dictionary<(int X, int Z), int>();
        for (var x = minX; x <= maxX; x++)
        for (var z = minZ; z <= maxZ; z++)
            surface[(x, z)] = top;
        return surface;
    }

    private static WorldProvenance Stamped(int minX, int minZ, int maxX, int maxZ, string unit = "wool-1")
    {
        var provenance = new WorldProvenance();
        provenance.ClaimRect(minX, minZ, maxX, maxZ, ProvenancePass.Structure, new StampId("wool", unit, 0));
        return provenance;
    }

    [Test]
    public async Task A_room_whose_footprint_is_the_whole_plateau_stands_on_a_wall()
    {
        // The ground is exactly the room: every cell beside it is void, so the foundation's own face is the
        // whole height of the plateau.
        var surface = Plateau(0, 0, 5, 5, top: 30);
        var findings = MapExportComposer.CheckStructureSites(surface, Stamped(0, 0, 5, 5));

        var finding = findings.Single();
        await Assert.That(finding.Rule).IsEqualTo(RoomFrameRules.StructureOnAPlinth);
        await Assert.That(finding.Severity).IsEqualTo(Severity.Complaint);
        await Assert.That(finding.Message).Contains("over the void");
        await Assert.That(finding.Message).Contains("30 blocks above");
    }

    [Test]
    public async Task A_room_on_ground_that_runs_past_it_says_nothing()
    {
        // The same room with the plateau reaching four cells further on every side.
        var surface = Plateau(-4, -4, 9, 9, top: 30);
        await Assert.That(MapExportComposer.CheckStructureSites(surface, Stamped(0, 0, 5, 5))).IsEmpty();
    }

    /// <summary>A step of one is a doorstep. The rule is about a face nobody drew, and terrain that falls away
    /// by a block under a building is terrain.</summary>
    [Test]
    public async Task A_single_course_of_fall_beside_a_room_is_not_a_wall()
    {
        // Ground all round the room, and the strip east of it one course down.
        Dictionary<(int X, int Z), int> Surrounded(int eastTop)
        {
            var surface = Plateau(-1, -1, 5, 6, top: 30);
            foreach (var (cell, top) in Plateau(6, -1, 6, 6, top: eastTop)) surface[cell] = top;
            return surface;
        }

        await Assert.That(MapExportComposer.CheckStructureSites(Surrounded(29), Stamped(0, 0, 5, 5))).IsEmpty();

        // Four courses down is a wall, and the finding says where.
        var finding = MapExportComposer.CheckStructureSites(Surrounded(26), Stamped(0, 0, 5, 5)).Single();
        await Assert.That(finding.Message).Contains("4 blocks above");
        await Assert.That(finding.Message).Contains("(6, ");
    }

    /// <summary>Two buildings on one board are two findings, told apart by the identity each stamp already
    /// recorded rather than by flooding across the columns they share a border with.</summary>
    [Test]
    public async Task Each_stamped_thing_is_answered_for_on_its_own()
    {
        var surface = Plateau(0, 0, 5, 5, top: 30);
        foreach (var (cell, top) in Plateau(20, 20, 25, 25, top: 30)) surface[cell] = top;

        var provenance = new WorldProvenance();
        provenance.ClaimRect(0, 0, 5, 5, ProvenancePass.Structure, new StampId("wool", "wool-1", 0));
        provenance.ClaimRect(20, 20, 25, 25, ProvenancePass.Structure, new StampId("spawn", "spawn-1", 0));

        var findings = MapExportComposer.CheckStructureSites(surface, provenance);
        await Assert.That(findings.Count).IsEqualTo(2);
        await Assert.That(findings.Select(f => f.SubjectIds[0])).Contains("wool:wool-1:0").And.Contains("spawn:spawn-1:0");
    }

    /// <summary>The terrain claims every column it lays, and none of it is a structure. A board with no
    /// building on it has nothing here to answer for.</summary>
    [Test]
    public async Task Ground_is_not_a_structure()
    {
        var surface = Plateau(0, 0, 5, 5, top: 30);
        var provenance = new WorldProvenance();
        provenance.ClaimRect(0, 0, 5, 5, ProvenancePass.Ground);
        await Assert.That(MapExportComposer.CheckStructureSites(surface, provenance)).IsEmpty();
    }
}
