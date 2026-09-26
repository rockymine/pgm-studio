using PgmStudio.Domain;
using PgmStudio.Pgm;

namespace PgmStudio.Pgm.Tests;

/// <summary>The derived box of a transform region is finite wherever its source's is, and unbounded exactly
/// where its source's is — the parser and the storage round-trip both take it from here, and a NaN side is
/// a document no JSON writer accepts.</summary>
public sealed class RegionBoundsDeriverTests
{
    private static Bounds2d Open(double minX, double minZ, double maxX, double maxZ) => Bounds2d.Of(minX, minZ, maxX, maxZ);

    [Test]
    public async Task A_mirror_across_z_flips_an_unbounded_side_and_keeps_the_other_axis()
    {
        var source = Open(-60, double.NegativeInfinity, -59, -38);
        var mirrored = RegionBoundsDeriver.Mirror(source, nx: 0, nz: 1, ox: 0, oz: 0);

        await Assert.That(mirrored.MinX).IsEqualTo(-60);
        await Assert.That(mirrored.MaxX).IsEqualTo(-59);
        await Assert.That(mirrored.MinZ).IsEqualTo(38);
        await Assert.That(double.IsPositiveInfinity(mirrored.MaxZ)).IsTrue();
    }

    [Test]
    public async Task A_diagonal_mirror_of_an_unbounded_box_is_unbounded_and_never_NaN()
    {
        var mirrored = RegionBoundsDeriver.Mirror(Open(0, double.NegativeInfinity, 4, 4), nx: 1, nz: 1, ox: 0, oz: 0);
        var sides = new[] { mirrored.MinX, mirrored.MinZ, mirrored.MaxX, mirrored.MaxZ };

        await Assert.That(sides.Any(double.IsNaN)).IsFalse();
        await Assert.That(sides.All(double.IsInfinity)).IsTrue();
    }

    [Test]
    public async Task A_parsed_mirror_of_an_unbounded_rectangle_serializes()
    {
        var map = MapParser.ParseXmlString(
            """<?xml version="1.0"?><map proto="1.4.0"><name>m</name><version>1</version><objective>o</objective><regions>"""
            + """<rectangle id="north" min="-60,-38" max="-59,-oo"/>"""
            + """<mirror id="south" region="north" origin="0,0,0" normal="0,0,1"/>"""
            + """<union id="both"><region id="north"/><region id="south"/></union>"""
            + "</regions></map>");

        await Assert.That(map.Regions["south"].Bounds2d!.MinZ).IsEqualTo(38);
        await Assert.That(() => System.Text.Json.JsonSerializer.Serialize(Serializer.ToDict(map))).ThrowsNothing();
    }

    [Test]
    public async Task Derive_gives_a_stored_mirror_the_same_box_the_parser_does()
    {
        var registry = new Dictionary<string, Region>
        {
            ["north"] = new() { Id = "north", Type = "rectangle", Bounds2d = Open(-60, double.NegativeInfinity, -59, -38) },
            ["south"] = new() { Id = "south", Type = "mirror", SourceId = "north", NormalX = 0, NormalZ = 1, OriginX = 0, OriginZ = 0 },
        };
        RegionBoundsDeriver.Derive(registry);

        var south = registry["south"].Bounds2d!;
        await Assert.That(south.MinZ).IsEqualTo(38);
        await Assert.That(double.IsPositiveInfinity(south.MaxZ)).IsTrue();
    }
}
