using PgmStudio.Domain;
using PgmStudio.Export;
using PgmStudio.Geom;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Vocabulary;

namespace PgmStudio.Export.Tests;

/// <summary>
/// <c>WX11</c> — a stamped structure the ground beside it falls away from.
///
/// <para>The fault it reports is one nothing else could see: a foundation seals the column under its whole
/// footprint and levels it at the footprint's highest, so a building whose neighbour sits well below meets
/// the world with a sheer face of its own bedrock. The export gate answers on goals, the traversability
/// check on whether the objectives connect, and both are satisfied by a board that has one.</para>
///
/// <para>Only ground the board drew is measured against. A structure at the board's rim has void beside it
/// and raises nothing: there is no ground there to fall away, nothing to bring up to the building, and no
/// drop to state — the fall would be the floor's own height above y=0 wearing the name of a step.</para>
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
    public async Task A_room_at_the_boards_rim_raises_nothing()
    {
        // The ground is exactly the room: every cell beside it is void. That is the edge of the map, not a
        // face the room presents to it — the board simply stops there, and no ground falls away from
        // anything. Measuring against it answered the plateau's own height above y=0, so a wool room at the
        // rim raised a thirty-block wall that no player could ever stand at the foot of (author).
        var surface = Plateau(0, 0, 5, 5, top: 30);
        await Assert.That(MapExportComposer.CheckStructureSites(surface, Stamped(0, 0, 5, 5))).IsEmpty();
    }

    [Test]
    public async Task A_room_whose_ground_falls_away_on_one_side_stands_on_a_wall()
    {
        // The case the rule is for, and the void has no part in it: the room is levelled at its own highest
        // and the drawn ground to its east lies eight blocks under that floor.
        var surface = Plateau(0, 0, 5, 5, top: 30);
        for (var z = 0; z <= 5; z++) surface[(6, z)] = 22;

        var finding = MapExportComposer.CheckStructureSites(surface, Stamped(0, 0, 5, 5)).Single();
        await Assert.That(finding.Rule).IsEqualTo(RoomFrameRules.StructureOnAPlinth);
        await Assert.That(finding.Severity).IsEqualTo(Severity.Complaint);
        await Assert.That(finding.Message).Contains("8 blocks above the ground beside it");
        await Assert.That(finding.Message).Contains("(6, ");
    }

    /// <summary>With the group a cell's ground is solved under, the finding states its bench: an area mark
    /// on that group's relief, at the floor, two cells past the footprint on every side.</summary>
    [Test]
    public async Task A_room_on_a_wall_states_the_bench_that_would_meet_it()
    {
        var surface = Plateau(0, 0, 5, 5, top: 30);
        for (var z = 0; z <= 5; z++) surface[(6, z)] = 22;   // drawn ground east of it, eight under the floor
        var findings = MapExportComposer.CheckStructureSites(surface, Stamped(0, 0, 5, 5), _ => "team");

        var edit = findings.Single().Edit;
        await Assert.That(edit).IsNotNull();
        await Assert.That(edit!.Path).IsEqualTo("relief.team.marks");
        await Assert.That(edit.Op).IsEqualTo("add");
        await Assert.That(edit.Value.GetProperty("kind").GetString()).IsEqualTo("area");
        await Assert.That(edit.Value.GetProperty("h").GetInt32()).IsEqualTo(30);
        var ring = edit.Value.GetProperty("ring").EnumerateArray()
            .Select(p => p.EnumerateArray().Select(v => v.GetInt32()).ToArray()).ToArray();
        await Assert.That(ring[0]).IsEquivalentTo(new[] { -2, -2 });
        await Assert.That(ring[2]).IsEquivalentTo(new[] { 7, 7 });

        var unplaced = MapExportComposer.CheckStructureSites(surface, Stamped(0, 0, 5, 5));
        await Assert.That(unplaced.Single().Edit).IsNull().Because("no group is known, so no bench can be stated");
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
        // Drawn ground beside each, well under the floor it stands on — the void raises nothing.
        for (var z = 0; z <= 5; z++) surface[(6, z)] = 22;
        for (var z = 20; z <= 25; z++) surface[(26, z)] = 22;

        var provenance = new WorldProvenance();
        provenance.ClaimRect(0, 0, 5, 5, ProvenancePass.Structure, new StampId("wool", "wool-1", 0));
        provenance.ClaimRect(20, 20, 25, 25, ProvenancePass.Structure, new StampId("spawn", "spawn-1", 0));

        var findings = MapExportComposer.CheckStructureSites(surface, provenance);
        await Assert.That(findings.Count).IsEqualTo(2);
        await Assert.That(findings.Select(f => f.SubjectIds[0])).Contains("wool:wool-1:0").And.Contains("spawn:spawn-1:0");
    }

    /// <summary>A bedrock approach wall on the seam between two pieces at different heights.
    ///
    /// <para>The wall lays no foundation — it is bedrock from <c>y 0</c> to its top by construction — so
    /// nothing levelled its footprint and the floor the rule would measure from belongs to one of its two
    /// rows only. Asked anyway, it answered the maximum of the footprint (the row on the higher piece)
    /// against a neighbour beside the *lower* row, which is a drop between two cells that never meet: the
    /// board this came from reported two blocks where every real step is one (author).</para></summary>
    [Test]
    public async Task A_wall_on_the_seam_between_two_pieces_is_not_asked()
    {
        // The geometry of `wall:0:0`: two rows across the seam, one on ground at y11 and one at y10, with
        // the ground south of the lower row at y9.
        var surface = new Dictionary<(int X, int Z), int>();
        for (var z = -65; z <= -50; z++)
        {
            for (var x = 14; x <= 19; x++) surface[(x, z)] = 10;   // the lower piece, and the wall's west row
            for (var x = 20; x <= 26; x++) surface[(x, z)] = 11;   // the higher piece, and its east row
        }
        for (var x = 14; x <= 19; x++) surface[(x, -49)] = 9;      // the third piece, south of the lower row

        var wall = new WorldProvenance();
        wall.ClaimRect(19, -65, 20, -50, ProvenancePass.Structure, new StampId("wall", "0", 0));
        await Assert.That(MapExportComposer.CheckStructureSites(surface, wall)).IsEmpty();

        // The same footprint stamped by something that *does* level answers, and the number it answers with
        // is the one the assumption earns: the footprint is levelled at 11, so the cell at 9 faces two.
        var room = new WorldProvenance();
        room.ClaimRect(19, -65, 20, -50, ProvenancePass.Structure, new StampId("wool", "wool-1", 0));
        var finding = MapExportComposer.CheckStructureSites(surface, room).Single();
        await Assert.That(finding.Rule).IsEqualTo(RoomFrameRules.StructureOnAPlinth);
        await Assert.That(finding.Message).Contains("2 blocks above the ground beside it");
    }

    /// <summary>Every kind that lays no foundation is silent, whatever the ground around it does.</summary>
    [Test]
    [Arguments("wall")]
    [Arguments("redstoneline")]
    [Arguments("destroyable")]
    [Arguments("core")]
    public async Task A_stamp_that_levels_nothing_is_silent(string kind)
    {
        var surface = Plateau(0, 0, 5, 5, top: 30);
        for (var z = 0; z <= 5; z++) surface[(6, z)] = 22;

        var provenance = new WorldProvenance();
        provenance.ClaimRect(0, 0, 5, 5, ProvenancePass.Structure, new StampId(kind, "0", 0));
        await Assert.That(MapExportComposer.CheckStructureSites(surface, provenance)).IsEmpty();
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
