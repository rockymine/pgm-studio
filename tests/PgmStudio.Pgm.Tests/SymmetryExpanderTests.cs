using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Pgm.Tests;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Orbit-fill (declarative authoring §4): the author states one orbit unit + a symmetry, and
/// <see cref="SymmetryExpander"/> rotates/reflects it onto the other teams (in <see cref="MapIntent.Teams"/>
/// list order). Asserts the geometry, the team remapping, author-override precedence, and that the filled
/// units still satisfy the generator's mirror property end-to-end.
/// </summary>
public sealed class SymmetryExpanderTests
{
    private static List<TeamDef> FourTeams() =>
    [
        new() { Id = "red-team", Color = "red", Name = "Red" },
        new() { Id = "blue-team", Color = "blue", Name = "Blue" },
        new() { Id = "green-team", Color = "green", Name = "Green" },
        new() { Id = "yellow-team", Color = "yellow", Name = "Yellow" },
    ];

    private static List<TeamDef> TwoTeams() =>
    [
        new() { Id = "red-team", Color = "red", Name = "Red" },
        new() { Id = "blue-team", Color = "blue", Name = "Blue" },
    ];

    [Test]
    public async Task No_symmetry_is_a_passthrough()
    {
        var intent = new MapIntent { Teams = TwoTeams(), Spawns = [new SpawnIntent { Team = "red-team", Point = new(1, 2, 3) }] };
        var outp = SymmetryExpander.Expand(intent);
        await Assert.That(outp.Spawns.Count).IsEqualTo(1);
        await Assert.That(ReferenceEquals(outp, intent)).IsTrue();
    }

    [Test]
    public async Task Rot90_fills_four_spawns_in_orbit_order()
    {
        var intent = new MapIntent
        {
            Teams = FourTeams(),
            Symmetry = new SymmetryIntent { Mode = "rot_90", CenterX = 0, CenterZ = 0 },
            Spawns = [new SpawnIntent { Team = "red-team", Point = new(10, 12, 10), Protection = [new(0, 0, 20, 20)], Yaw = 0 }],
        };
        var outp = SymmetryExpander.Expand(intent);

        await Assert.That(outp.Spawns.Count).IsEqualTo(4);
        // CCW rotation about origin: (10,10) → (-10,10) → (-10,-10) → (10,-10)
        var blue = outp.Spawns.First(s => s.Team == "blue-team").Point;
        await Assert.That(blue.X).IsEqualTo(-10.0); await Assert.That(blue.Z).IsEqualTo(10.0); await Assert.That(blue.Y).IsEqualTo(12.0);
        var green = outp.Spawns.First(s => s.Team == "green-team").Point;
        await Assert.That(green.X).IsEqualTo(-10.0); await Assert.That(green.Z).IsEqualTo(-10.0);
        var yellow = outp.Spawns.First(s => s.Team == "yellow-team").Point;
        await Assert.That(yellow.X).IsEqualTo(10.0); await Assert.That(yellow.Z).IsEqualTo(-10.0);
        // yaw rotates with the unit (0 → 90 at the first quarter turn)
        await Assert.That(outp.Spawns.First(s => s.Team == "blue-team").Yaw).IsEqualTo(90.0);
        // protection rect is carried + transformed
        await Assert.That(outp.Spawns.First(s => s.Team == "blue-team").Protection.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Mirror_x_reflects_point_and_yaw()
    {
        var intent = new MapIntent
        {
            Teams = TwoTeams(),
            Symmetry = new SymmetryIntent { Mode = "mirror_x", CenterX = 0, CenterZ = 0 },
            Spawns = [new SpawnIntent { Team = "red-team", Point = new(10, 8, 4), Yaw = 90 }],
        };
        var outp = SymmetryExpander.Expand(intent);

        await Assert.That(outp.Spawns.Count).IsEqualTo(2);
        var blue = outp.Spawns.First(s => s.Team == "blue-team").Point;
        await Assert.That(blue.X).IsEqualTo(-10.0); await Assert.That(blue.Z).IsEqualTo(4.0); await Assert.That(blue.Y).IsEqualTo(8.0);
        // facing -X (yaw 90) mirrored across the X-normal plane → facing +X (yaw 270)
        await Assert.That(outp.Spawns.First(s => s.Team == "blue-team").Yaw).IsEqualTo(270.0);
    }

    [Test]
    public async Task Region_orbit_keeps_block_centre_points_and_integer_grid_rects()
    {
        // Corpus convention (verified against PGM RectangleRegion/PointRegion + the 350-map corpus):
        // rectangle bounds live on the 1×1 block grid (integers); a spawn POINT sits at the block centre
        // (x.5). The orbit must keep each — the point keeps its half (rounding it to an integer would shift
        // it off its block), and the rectangle stays integer and covers the same 20×50 extent, mirrored.
        var intent = new MapIntent
        {
            Teams = TwoTeams(),
            Symmetry = new SymmetryIntent { Mode = "mirror_x", CenterX = 0, CenterZ = 0 },
            Spawns = [new SpawnIntent { Team = "red-team", Point = new(10.5, 8, 4.5), Protection = [new(2, 5, 22, 55)] }],
        };
        var outp = SymmetryExpander.Expand(intent);

        var blue = outp.Spawns.First(s => s.Team == "blue-team");
        await Assert.That(blue.Point.X).IsEqualTo(-10.5); await Assert.That(blue.Point.Z).IsEqualTo(4.5);
        var p = blue.Protection[0];
        await Assert.That(p.MinX).IsEqualTo(-22.0); await Assert.That(p.MaxX).IsEqualTo(-2.0);   // mirrored, integer
        await Assert.That(p.MinZ).IsEqualTo(5.0); await Assert.That(p.MaxZ).IsEqualTo(55.0);
        await Assert.That(p.MaxX - p.MinX).IsEqualTo(20.0);                                        // 20×50 preserved
        await Assert.That(p.MaxZ - p.MinZ).IsEqualTo(50.0);
    }

    [Test]
    public async Task Multi_rect_protection_orbits_every_rect()
    {
        // a two-rect protection footprint under mirror_x: both rects must reflect onto the partner team
        var intent = new MapIntent
        {
            Teams = TwoTeams(),
            Symmetry = new SymmetryIntent { Mode = "mirror_x", CenterX = 0, CenterZ = 0 },
            Spawns = [new SpawnIntent { Team = "red-team", Point = new(10, 8, 4), Protection = [new(2, 0, 12, 10), new(12, 0, 18, 6)] }],
        };
        var outp = SymmetryExpander.Expand(intent);

        var blue = outp.Spawns.First(s => s.Team == "blue-team");
        await Assert.That(blue.Protection.Count).IsEqualTo(2);
        // each rect reflected across the X-normal (x→-x), order preserved, extent kept
        await Assert.That(blue.Protection[0].MinX).IsEqualTo(-12.0); await Assert.That(blue.Protection[0].MaxX).IsEqualTo(-2.0);
        await Assert.That(blue.Protection[1].MinX).IsEqualTo(-18.0); await Assert.That(blue.Protection[1].MaxX).IsEqualTo(-12.0);
    }

    [Test]
    public async Task Multi_rect_room_orbits_every_rect()
    {
        var intent = new MapIntent
        {
            Teams = TwoTeams(),
            Symmetry = new SymmetryIntent { Mode = "mirror_x", CenterX = 0, CenterZ = 0 },
            Wools =
            [
                new WoolIntent { Owner = "red-team", Color = "red", Room = [new(0, 0, 10, 10), new(10, 0, 16, 6)], Spawn = new(5, 5, 5),
                    Monuments = [new MonumentIntent { Team = "blue-team", Location = new(5, 1, 5) }] },
            ],
        };
        var outp = SymmetryExpander.Expand(intent);

        var blueWool = outp.Wools!.First(w => w.Owner == "blue-team");
        await Assert.That(blueWool.Room.Count).IsEqualTo(2);
        await Assert.That(blueWool.Room[0].MinX).IsEqualTo(-10.0); await Assert.That(blueWool.Room[0].MaxX).IsEqualTo(0.0);
        await Assert.That(blueWool.Room[1].MinX).IsEqualTo(-16.0); await Assert.That(blueWool.Room[1].MaxX).IsEqualTo(-10.0);
    }

    [Test]
    public async Task Author_override_is_not_overwritten()
    {
        var intent = new MapIntent
        {
            Teams = TwoTeams(),
            Symmetry = new SymmetryIntent { Mode = "mirror_x" },
            Spawns =
            [
                new SpawnIntent { Team = "red-team", Point = new(10, 8, 4) },
                new SpawnIntent { Team = "blue-team", Point = new(-3, 9, -7) },   // hand-placed, not the mirror
            ],
        };
        var outp = SymmetryExpander.Expand(intent);

        await Assert.That(outp.Spawns.Count).IsEqualTo(2);
        var blue = outp.Spawns.First(s => s.Team == "blue-team").Point;
        await Assert.That(blue.X).IsEqualTo(-3.0); await Assert.That(blue.Y).IsEqualTo(9.0); await Assert.That(blue.Z).IsEqualTo(-7.0);
    }

    [Test]
    public async Task Wool_orbit_remaps_owner_and_capturing_team()
    {
        var intent = new MapIntent
        {
            Teams = TwoTeams(),
            Symmetry = new SymmetryIntent { Mode = "mirror_x", CenterX = 0, CenterZ = 0 },
            Wools =
            [
                new WoolIntent
                {
                    Owner = "red-team", Color = "red",
                    Room = [new(0, 0, 10, 10)], Spawn = new(5, 5, 5),
                    Monuments = [new MonumentIntent { Team = "blue-team", Location = new(5, 1, 5) }],
                },
            ],
        };
        var outp = SymmetryExpander.Expand(intent);

        await Assert.That(outp.Wools!.Count).IsEqualTo(2);
        var blueWool = outp.Wools!.First(w => w.Owner == "blue-team");
        // room reflected across the X-normal: x→-x
        await Assert.That(blueWool.Room[0].MinX).IsEqualTo(-10.0); await Assert.That(blueWool.Room[0].MaxX).IsEqualTo(0.0);
        await Assert.That(blueWool.Spawn.X).IsEqualTo(-5.0);
        // the capturer shifts by the same orbit step: blue captured red's wool, so red captures blue's
        await Assert.That(blueWool.Monuments.Count).IsEqualTo(1);
        await Assert.That(blueWool.Monuments[0].Team).IsEqualTo("red-team");
        await Assert.That(blueWool.Monuments[0].Location.X).IsEqualTo(-5.0);
        // orbit copy defaults its colour to the new owner team (empty slug → owner colour)
        await Assert.That(blueWool.Color).IsEqualTo("");
    }

    [Test]
    public async Task Filled_spawns_satisfy_the_mirror_property_end_to_end()   // generate → categorize recovers it
    {
        var doc = new Dict
        {
            ["teams"] = new List<object?>(), ["regions"] = new Dict(), ["filters"] = new Dict(),
            ["spawns"] = new List<object?>(), ["apply_rules"] = new List<object?>(),
        };
        var intent = new MapIntent
        {
            Teams = FourTeams(), MaxPlayers = 12,
            Symmetry = new SymmetryIntent { Mode = "rot_90", CenterX = 0, CenterZ = 0 },
            Spawns = [new SpawnIntent { Team = "red-team", Point = new(10, 12, 10), Protection = [new(0, 0, 20, 20)] }],
        };
        IntentGenerator.Apply(doc, intent);

        var facets = RegionCategorizer.DeriveFacets(doc);
        foreach (var slug in new[] { "red", "blue", "green", "yellow" })
        {
            await Assert.That(facets[$"{slug}-spawn-point"].Category).IsEqualTo("spawn");
            await Assert.That(facets[$"{slug}-spawn"].Subtype).IsEqualTo("protection");
        }
        await Assert.That(((List<object?>)doc["spawns"]!).Count).IsEqualTo(4);
    }

    [Test]
    public async Task Synthesizes_teams_from_symmetry_when_none_listed()
    {
        // author states only symmetry + one spawn, no teams — rot_90 → 4 palette teams, anchored to "red"
        var intent = new MapIntent
        {
            Symmetry = new SymmetryIntent { Mode = "rot_90", CenterX = 0, CenterZ = 0 },
            Spawns = [new SpawnIntent { Team = "red", Point = new(10, 12, 10) }],
        };
        var outp = SymmetryExpander.Expand(intent);

        await Assert.That(outp.Teams!.Count).IsEqualTo(4);
        await Assert.That(outp.Teams!.Select(t => t.Color)).IsEquivalentTo(new[] { "red", "blue", "green", "yellow" });
        await Assert.That(outp.Teams![0].Id).IsEqualTo("red");          // anchored to the authored spawn's id
        await Assert.That(outp.Spawns.Count).IsEqualTo(4);               // orbit-filled onto the synthesized teams
        await Assert.That(outp.Spawns.Select(s => s.Team)).Contains("blue-team");   // synthesized teams get the -team suffix (B10)
    }

    [Test]
    public async Task Mirror_synthesizes_two_teams()
    {
        var intent = new MapIntent
        {
            Symmetry = new SymmetryIntent { Mode = "mirror_x" },
            Spawns = [new SpawnIntent { Team = "red", Point = new(8, 9, 8) }],
        };
        var outp = SymmetryExpander.Expand(intent);
        await Assert.That(outp.Teams!.Count).IsEqualTo(2);
        await Assert.That(outp.Teams!.Select(t => t.Color)).IsEquivalentTo(new[] { "red", "blue" });
    }

    [Test]
    public async Task Build_areas_orbit_and_dedup()
    {
        // one off-centre rect under mirror_x → its mirror is added (2 total)
        var intent = new MapIntent
        {
            Teams = TwoTeams(),
            Symmetry = new SymmetryIntent { Mode = "mirror_x", CenterX = 0, CenterZ = 0 },
            Build = new BuildIntent { MaxHeight = 64, Areas = [new Rect(2, 0, 12, 10)] },
        };
        var outp = SymmetryExpander.Expand(intent);
        await Assert.That(outp.Build!.Areas.Count).IsEqualTo(2);
        await Assert.That(outp.Build!.MaxHeight).IsEqualTo(64);
        await Assert.That(outp.Build!.Areas.Any(r => r.MinX == -12.0 && r.MaxX == -2.0)).IsTrue();
    }

    [Test]
    public async Task Build_holes_orbit_with_the_areas()
    {
        var intent = new MapIntent
        {
            Teams = TwoTeams(),
            Symmetry = new SymmetryIntent { Mode = "mirror_x", CenterX = 0, CenterZ = 0 },
            Build = new BuildIntent { Areas = [new Rect(2, 0, 12, 10)], Holes = [new Rect(4, 2, 6, 4)] },
        };
        var outp = SymmetryExpander.Expand(intent);
        await Assert.That(outp.Build!.Areas.Count).IsEqualTo(2);
        await Assert.That(outp.Build!.Holes.Count).IsEqualTo(2);
        await Assert.That(outp.Build!.Holes.Any(r => r.MinX == -6.0 && r.MaxX == -4.0)).IsTrue();
    }

    [Test]
    public async Task Centre_symmetric_build_area_dedups_to_one()
    {
        // a rect centred on the mirror axis maps onto itself → no duplicate
        var intent = new MapIntent
        {
            Teams = TwoTeams(),
            Symmetry = new SymmetryIntent { Mode = "mirror_x", CenterX = 0, CenterZ = 0 },
            Build = new BuildIntent { Areas = [new Rect(-10, 0, 10, 20)] },
        };
        var outp = SymmetryExpander.Expand(intent);
        await Assert.That(outp.Build!.Areas.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Suggested_team_count_tracks_symmetry_order()
    {
        await Assert.That(SymmetryExpander.SuggestedTeamCount("rot_90")).IsEqualTo(4);
        await Assert.That(SymmetryExpander.SuggestedTeamCount("rot_180")).IsEqualTo(2);
        await Assert.That(SymmetryExpander.SuggestedTeamCount("mirror_x")).IsEqualTo(2);
        await Assert.That(SymmetryExpander.SuggestedTeamCount("mirror_d2")).IsEqualTo(2);
    }
}
