using PgmStudio.Pgm.Editing;

namespace PgmStudio.Pgm.Tests;

using Dict = Dictionary<string, object?>;

/// <summary>
/// F3 counterpart tests. Expected geometry is the reference `symmetry_authoring.create_counterpart`
/// output for the same synthetic regions about centre (0.5, 0.5) — genuine parity across the modes
/// (mirror → native PGM mirror region; rot_180 → two chained mirrors; rot_90 → baked primitive).
/// </summary>
public sealed class SymmetryAuthoringTests
{
    private const double Cx = 0.5, Cz = 0.5;

    private static Dict Doc() => new()
    {
        ["regions"] = new Dict
        {
            ["b"] = new Dict { ["id"] = "b", ["type"] = "block", ["position"] = Xyz(-24, 13, -90), ["bounds_2d"] = B(-24, -90, -23, -89) },
            ["c"] = new Dict { ["id"] = "c", ["type"] = "cuboid", ["min"] = Xyz(27, 12, -105), ["max"] = Xyz(30, 12, -108), ["bounds_2d"] = B(27, -108, 30, -105) },
            ["r"] = new Dict { ["id"] = "r", ["type"] = "rectangle", ["bounds_2d"] = B(-38, -100, -10, -80) },
            ["y"] = new Dict { ["id"] = "y", ["type"] = "cylinder", ["base"] = Xyz(-24.5, 12, -85.5), ["radius"] = 1, ["height"] = 0, ["bounds_2d"] = B(-25.5, -86.5, -23.5, -84.5) },
        },
    };

    [Test]
    public async Task Block_rot90_bakes_rotated_position_and_bounds()
    {
        var doc = Doc();
        var res = SymmetryAuthoring.CreateCounterpart(doc, "b", "rot_90", Cx, Cz);
        await Assert.That(res["counterpart"]).IsEqualTo("block_1");
        var reg = Region(doc, "block_1");
        await Assert.That(reg["type"]).IsEqualTo("block");
        await Assert.That(Pos(reg, "x")).IsEqualTo(91.0); await Assert.That(Pos(reg, "z")).IsEqualTo(-24.0);
        await Assert.That(Num(((Dict)reg["position"]!)["y"])).IsEqualTo(13.0);   // y preserved
        await Assert.That(Bd(reg)).IsEqualTo((90.0, -24.0, 91.0, -23.0));
    }

    [Test]
    public async Task Block_mirror_x_makes_native_mirror_region()
    {
        var doc = Doc();
        var res = SymmetryAuthoring.CreateCounterpart(doc, "b", "mirror_x", Cx, Cz);
        await Assert.That(res["counterpart"]).IsEqualTo("mirror_1");
        var reg = Region(doc, "mirror_1");
        await Assert.That(reg["type"]).IsEqualTo("mirror");
        await Assert.That(reg["source_id"]).IsEqualTo("b");
        await Assert.That(Num(((Dict)reg["normal"]!)["x"])).IsEqualTo(1.0);
        await Assert.That(Num(((Dict)reg["normal"]!)["z"])).IsEqualTo(0.0);
        await Assert.That(Bd(reg)).IsEqualTo((24.0, -90.0, 25.0, -89.0));
    }

    [Test]
    public async Task Cuboid_rot90_rotates_xz_and_keeps_y()
    {
        var doc = Doc();
        SymmetryAuthoring.CreateCounterpart(doc, "c", "rot_90", Cx, Cz);
        var reg = Region(doc, "cuboid_1");
        var mn = (Dict)reg["min"]!; var mx = (Dict)reg["max"]!;
        await Assert.That((Num(mn["x"]), Num(mn["z"]))).IsEqualTo((106.0, 27.0));
        await Assert.That((Num(mx["x"]), Num(mx["z"]))).IsEqualTo((109.0, 30.0));
        await Assert.That(Num(mn["y"])).IsEqualTo(12.0); await Assert.That(Num(mx["y"])).IsEqualTo(12.0);
        await Assert.That(Bd(reg)).IsEqualTo((106.0, 27.0, 109.0, 30.0));
    }

    [Test]
    public async Task Rectangle_mirror_x_reflects_bounds_only()
    {
        var doc = Doc();
        SymmetryAuthoring.CreateCounterpart(doc, "r", "mirror_x", Cx, Cz);
        var reg = Region(doc, "mirror_1");
        await Assert.That(reg["type"]).IsEqualTo("mirror");
        await Assert.That(Bd(reg)).IsEqualTo((11.0, -100.0, 39.0, -80.0));
    }

    [Test]
    public async Task Cylinder_rot90_rotates_base_keeps_radius()
    {
        var doc = Doc();
        SymmetryAuthoring.CreateCounterpart(doc, "y", "rot_90", Cx, Cz);
        var reg = Region(doc, "cylinder_1");
        await Assert.That(Pos2(reg, "base", "x")).IsEqualTo(86.5); await Assert.That(Pos2(reg, "base", "z")).IsEqualTo(-24.5);
        await Assert.That(Num(reg["radius"])).IsEqualTo(1.0);
        await Assert.That(Bd(reg)).IsEqualTo((85.5, -25.5, 87.5, -23.5));
    }

    [Test]
    public async Task Rot180_emits_two_chained_mirrors()
    {
        var doc = Doc();
        var res = SymmetryAuthoring.CreateCounterpart(doc, "b", "rot_180", Cx, Cz);
        await Assert.That(res["counterpart"]).IsEqualTo("mirror_2");
        var created = (List<object?>)res["created"]!;
        await Assert.That(string.Join(",", created)).IsEqualTo("mirror_1,mirror_2");
        var m2 = Region(doc, "mirror_2");
        await Assert.That(m2["source_id"]).IsEqualTo("mirror_1");   // chained
        await Assert.That(Num(((Dict)m2["normal"]!)["z"])).IsEqualTo(1.0);
        await Assert.That(Bd(m2)).IsEqualTo((24.0, 90.0, 25.0, 91.0));
    }

    // ── helpers ───────────────────────────────────────────────────────────────────
    [Test]
    public async Task Orbit_rot90_chains_three_quarter_turns()
    {
        var doc = Doc();
        var res = SymmetryAuthoring.CreateOrbit(doc, "b", "rot_90", Cx, Cz, "spawn");
        var created = (List<object?>)res["created"]!;
        // source + 3 counterparts = the full 4-fold orbit; baked blocks chained 90°/180°/270°.
        await Assert.That(string.Join(",", created)).IsEqualTo("block_1,block_2,block_3");
        await Assert.That(Bd(Region(doc, "b"))).IsEqualTo((-24.0, -90.0, -23.0, -89.0));         // source unchanged
        await Assert.That(Bd(Region(doc, "block_1"))).IsEqualTo((90.0, -24.0, 91.0, -23.0));      // 90°
        await Assert.That(Bd(Region(doc, "block_2"))).IsEqualTo((24.0, 90.0, 25.0, 91.0));        // 180°
        await Assert.That(Bd(Region(doc, "block_3"))).IsEqualTo((-90.0, 24.0, -89.0, 25.0));      // 270°
    }

    [Test]
    public async Task Orbit_mirror_adds_single_counterpart_in_category()
    {
        var doc = Doc();
        var res = SymmetryAuthoring.CreateOrbit(doc, "b", "mirror_x", Cx, Cz, "spawn");
        var created = (List<object?>)res["created"]!;
        await Assert.That(string.Join(",", created)).IsEqualTo("mirror_1");
        // counterpart is tracked under the source's category so it shows in the drawing activity.
        var spawn = (List<object?>)((Dict)doc["region_categories"]!)["spawn"]!;
        await Assert.That(spawn.Contains("mirror_1")).IsTrue();
    }

    private static Dict Xyz(double x, double y, double z) => new() { ["x"] = x, ["y"] = y, ["z"] = z };
    private static Dict B(double minX, double minZ, double maxX, double maxZ)
        => new() { ["min"] = new Dict { ["x"] = minX, ["z"] = minZ }, ["max"] = new Dict { ["x"] = maxX, ["z"] = maxZ } };
    private static Dict Region(Dict doc, string id) => (Dict)((Dict)doc["regions"]!)[id]!;
    private static double Pos(Dict reg, string axis) => Num(((Dict)reg["position"]!)[axis]);
    private static double Pos2(Dict reg, string key, string axis) => Num(((Dict)reg[key]!)[axis]);
    private static (double, double, double, double) Bd(Dict reg)
    {
        var bb = (Dict)reg["bounds_2d"]!; var mn = (Dict)bb["min"]!; var mx = (Dict)bb["max"]!;
        return (Num(mn["x"]), Num(mn["z"]), Num(mx["x"]), Num(mx["z"]));
    }
    private static double Num(object? v) => v switch { double d => d, long l => l, int i => i, float f => f, _ => 0 };
}
