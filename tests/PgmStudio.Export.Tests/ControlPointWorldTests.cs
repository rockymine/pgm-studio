using System.Text.Json;
using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Palette;
using PgmStudio.Pgm;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Export.Tests;

/// <summary>
/// End-to-end CP/KotH: an intent carrying capture points → world + map.xml. The claim worth proving is the
/// one a hill is won and lost on — that the blocks the world laid are the blocks the XML's regions scope
/// (OB8), and that the volume the XML calls the capture region is a volume a player can actually stand in.
/// A region that misses its pad is a hill PGM will never recolour; a volume that misses the block a standing
/// player occupies is a hill nobody can take, and PGM reports neither.
/// </summary>
public sealed class ControlPointWorldTests
{
    // A plain two-team board. The plan supplies the ground; the points are stated on the intent, which is
    // the surface an agent drives (PUT /api/map/{slug}/intent).
    private const string Json = """
        {
          "plan": 2,
          "meta": { "name": "Hill Probe" },
          "globals": { "cell": 5, "symmetry": "rot_180", "surface": 9, "headroom": 11 },
          "pieces": [
            { "id": "field", "role": "piece", "rect": [-4, -4, 4, 4], "surface": 12 }
          ]
        }
        """;

    private static BuiltWorld Build(params ControlPointIntent[] points)
    {
        var plan = PlanModel.Parse(Json)!;
        var (layout, intent) = PlanCompiler.Compile(plan);
        return WorldBuilder.Build(
            JsonSerializer.Serialize(layout, SketchLayout.Json),
            intent with { ControlPoints = [.. points] });
    }

    private static ControlPointIntent At(double x, double z, string name = "Middle")
        => new() { Name = name, Anchor = new Pt(x, 0, z), Size = 7 };

    // ── the world ───────────────────────────────────────────────────────────────────
    [Test]
    public async Task The_pad_is_laid_in_a_material_PGM_recolours()
    {
        var built = Build(At(0, 0));
        var pad = built.ResolvedIntent.ControlPoints!.Single().PadBox!.Value;

        for (var x = pad.MinX; x <= pad.MaxX; x++)
        for (var z = pad.MinZ; z <= pad.MaxZ; z++)
            await Assert.That(built.World.GetBlock(x, pad.MinY, z).Id).IsEqualTo(Blocks.StainedClay);
    }

    // What a hill is: ground. If the pad floated the way a destroyable does, nobody could stand on it.
    [Test]
    public async Task The_block_over_the_pad_is_air_a_player_can_stand_in()
    {
        var built = Build(At(0, 0));
        var pad = built.ResolvedIntent.ControlPoints!.Single().PadBox!.Value;
        await Assert.That(built.World.GetBlock(pad.MinX + 3, pad.MinY + 1, pad.MinZ + 3).Id).IsEqualTo(Blocks.Air);
    }

    [Test]
    public async Task The_pad_is_level_across_its_whole_footprint()
    {
        var built = Build(At(0, 0));
        var pad = built.ResolvedIntent.ControlPoints!.Single().PadBox!.Value;
        await Assert.That(pad.Height).IsEqualTo(1);
    }

    // ── the map.xml the world produced ──────────────────────────────────────────────
    // The capture region has to contain the block a player standing on the pad occupies, or the point can
    // never be captured — PGM tests the block the player's feet are in.
    [Test]
    public async Task The_capture_region_holds_the_block_a_player_on_the_pad_occupies()
    {
        var built = Build(At(0, 0));
        var point = built.ResolvedIntent.ControlPoints!.Single();
        var (pad, capture) = (point.PadBox!.Value, point.CaptureBox!.Value);

        await Assert.That(capture.MinY).IsLessThanOrEqualTo(pad.MinY + 1);
        await Assert.That(capture.MaxY).IsGreaterThanOrEqualTo(pad.MinY + 1);
    }

    // The marker is the hill's one changing signal, so it has to be laid in something PGM recolours and in
    // the neutral the pad is built in. White wool: in ColorUtils's set, and white is what 263 of 359 corpus
    // pads are.
    [Test]
    public async Task The_sky_marker_is_white_wool_over_the_build_ceiling()
    {
        var built = Build(At(0, 0));
        var point = built.ResolvedIntent.ControlPoints!.Single();
        var marker = point.MarkerBox!.Value;

        await Assert.That(marker.MinY).IsGreaterThan(built.ResolvedIntent.Build!.MaxHeight!.Value);
        var (block, data) = built.World.GetBlock(marker.MinX + 1, marker.MinY + 1, marker.MinZ + 1);
        await Assert.That(block).IsEqualTo(Blocks.Wool);
        await Assert.That(data).IsEqualTo(BlockColors.BlockDamage(ObjectiveDefaults.ControlPointColor));
    }

    // The marker changes colour only if it is inside the region PGM paints in the owner's dye, and it may
    // not be inside the progress region: that display is a pie about the centre of its own bounds, so a
    // marker in it would sweep about a point in the sky instead of about the pad.
    [Test]
    public async Task The_owner_region_is_the_marker_and_does_not_touch_the_pad()
    {
        var built = Build(At(0, 0));
        var doc = new Dictionary<string, object?>();
        IntentGenerator.Apply(doc, built.ResolvedIntent);
        var map = MapParser.ParseXmlString(XmlWriter.ToXml(Deserializer.FromDict(doc)));

        var point = map.ControlPoints.Single();
        var resolved = built.ResolvedIntent.ControlPoints!.Single();
        await Assert.That(point.OwnerRegionId).IsNotEmpty();
        await Assert.That(Box(map, point.OwnerRegionId)).IsEqualTo(resolved.MarkerBox!.Value);
        await Assert.That(resolved.MarkerBox!.Value.MinY).IsGreaterThan(resolved.CaptureBox!.Value.MaxY);
    }

    // OB8: one box feeds the blocks and the region, so the region cannot miss its own pad.
    [Test]
    public async Task The_emitted_regions_are_the_boxes_the_world_laid()
    {
        var built = Build(At(0, 0));
        var doc = new Dictionary<string, object?>();
        IntentGenerator.Apply(doc, built.ResolvedIntent);
        var map = MapParser.ParseXmlString(XmlWriter.ToXml(Deserializer.FromDict(doc)));

        var point = map.ControlPoints.Single();
        var resolved = built.ResolvedIntent.ControlPoints!.Single();

        await Assert.That(Box(map, point.CaptureRegionId)).IsEqualTo(resolved.CaptureBox!.Value);
        await Assert.That(Box(map, point.ProgressRegionId)).IsEqualTo(resolved.PadBox!.Value);
    }

    // The whole board an agent asked for, read back off the exported map.
    [Test]
    public async Task A_centre_and_one_side_export_as_a_three_hill_koth_board()
    {
        var plan = PlanModel.Parse(Json)!;
        var (layout, compiled) = PlanCompiler.Compile(plan);
        // As an agent states it: the middle, and one side. The symmetry makes the third.
        var authored = compiled with
        {
            Symmetry = new SymmetryIntent { Mode = "rot_180", CenterX = 0, CenterZ = 0 },
            ControlPoints = [At(0, 0, "Middle"), At(0, 12, "Side")],
        };
        var built = WorldBuilder.Build(JsonSerializer.Serialize(layout, SketchLayout.Json), authored);

        var doc = new Dictionary<string, object?>();
        IntentGenerator.Apply(doc, built.ResolvedIntent);
        var map = MapParser.ParseXmlString(XmlWriter.ToXml(Deserializer.FromDict(doc)));

        await Assert.That(map.ControlPoints.Count).IsEqualTo(3);
        await Assert.That(map.Gamemodes).IsEquivalentTo(new[] { "koth" });
        await Assert.That(map.Score!.Limit).IsEqualTo(ObjectiveDefaults.ControlPointScoreLimit);
        // Every one of them, or the board ends on the first capture.
        foreach (var point in map.ControlPoints)
        {
            await Assert.That(point.Required).IsFalse();
            await Assert.That(point.Element).IsEqualTo(ControlPointElement.King);
        }
    }

    private static BlockBox Box(Domain.MapXml map, string regionId)
    {
        var region = map.Regions[regionId];
        // A PGM cuboid's max is one past its last block.
        return new BlockBox((int)region.MinX!, (int)region.MinY!, (int)region.MinZ!,
                            (int)region.MaxX! - 1, (int)region.MaxY! - 1, (int)region.MaxZ! - 1);
    }
}
