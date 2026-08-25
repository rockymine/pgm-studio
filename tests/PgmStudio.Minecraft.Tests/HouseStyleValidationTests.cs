using PgmStudio.Domain;
using PgmStudio.Minecraft.Houses;
using PgmStudio.Minecraft.Painting;
using PgmStudio.Minecraft.Palette;
using PgmStudio.Vocabulary;
namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// The house-style gate (<see cref="HouseStyleRules"/>): a block named for a geometric role has to be that
/// kind of block, a doorway has to clear the least height a door may, and a roof's own materials have to fit
/// its pitch and its family. Every check is proved against the exact corpus fault it was written for — a
/// fixture that reproduces the wrong block, not a synthetic one — and against the shipped presets, which must
/// all read as clean.
/// </summary>
public sealed class HouseStyleValidationTests
{
    // A preset with one thing about its roof changed. The fixtures below are corpus faults reproduced on a
    // shipped preset, so each names only the material or the number that is wrong and keeps the rest.
    private static HouseStyle Roofed(HouseStyle style, TerrainMaterial? body = null, TerrainMaterial? verge = null)
        => style with { Roof = style.Roof with { Body = body ?? style.Roof.Body, Verge = verge ?? style.Roof.Verge } };

    private static HouseStyle Slabbed(HouseStyle style, int slab)
        => style with { Roof = style.Roof with { Slab = slab } };

    // A preset with one thing about the beam over its doorway changed.
    private static HouseStyle Headed(HouseStyle style, Func<DoorHeadStyle, DoorHeadStyle> change)
        => style with { Doorway = style.Doorway with { Head = change(style.Doorway.Head) } };

    // ── the shipped presets are clean ──────────────────────────────────────────────────────────────────

    [Test]
    [MethodDataSource(nameof(Presets))]
    public async Task Every_shipped_preset_passes_the_gate(HousePresets.House house)
        => await Assert.That(HouseStyleValidation.Check(house.Style)).IsEmpty();

    public static IEnumerable<HousePresets.House> Presets() => HousePresets.All;

    /// <summary>Alpine and Workshop are the two presets built on <see cref="WindowForm.StairLattice"/> and
    /// <see cref="WindowForm.SlabBanded"/> respectively, and both pass clean — pinned on its own so the pattern
    /// is not lost inside the loop over every preset. Neither form is a defect: the author has ruled both
    /// allowed, on any house, and the corpus complaint was always the block handed to the form (a fence where a
    /// stair goes, a pane where a slab goes — <see cref="HouseStyleRules.BlockKind"/>), never the pattern
    /// itself.</summary>
    [Test]
    public async Task A_stair_lattice_and_a_slab_band_pass_clean_with_the_right_block()
    {
        await Assert.That(HouseStyleValidation.Check(HousePresets.Alpine.Style)).IsEmpty();
        await Assert.That(HouseStyleValidation.Check(HousePresets.Workshop.Style)).IsEmpty();
    }

    /// <summary>The author's ruling, pinned on the one house type it was ever in question for: a spawn shell
    /// built with a stair-lattice or a slab-banded window, real blocks throughout, passes clean. Nothing —
    /// not <see cref="HouseStyleValidation.Check"/>, not any other gate — singles a spawn's window out.</summary>
    [Test]
    public async Task A_spawn_built_with_a_stair_lattice_or_a_slab_band_window_is_allowed()
    {
        var lattice = HouseStyle.Spawn with { Windows = WindowStyle.Lattice };
        var band = HouseStyle.Spawn with { Windows = WindowStyle.Band };
        await Assert.That(HouseStyleValidation.Check(lattice)).IsEmpty();
        await Assert.That(HouseStyleValidation.Check(band)).IsEmpty();
    }

    // ── HS1 — a block named for a geometric role, never checked to be that kind of block ────────────────

    [Test]
    public async Task A_door_head_named_a_cobblestone_stair_is_refused()
    {
        // sable-marsh's spawn room, block for block: doorHead.block and fillBlock both Cobblestone (4).
        var style = Headed(HousePresets.Desert.Style,
            head => head with { Block = Blocks.Cobblestone, FillBlock = Blocks.Cobblestone });
        // Wrecking both fields at once also drops the door's clear height below the line (HS2) — the same
        // fault sable-marsh's own spawn room carries, not a second problem this fixture introduced.
        var findings = HouseStyleValidation.Check(style);
        await Assert.That(findings.Single(f => f.Field == "doorHead.block").Rule).IsEqualTo(HouseStyleRules.BlockKind);
        await Assert.That(findings.Single(f => f.Field == "doorHead.fillBlock").Rule).IsEqualTo(HouseStyleRules.BlockKind);
        await Assert.That(findings.Select(f => f.Rule)).Contains(HouseStyleRules.DoorClearance);
    }

    [Test]
    public async Task The_same_door_head_with_the_real_stair_and_slab_restored_is_clean()
    {
        // The fix is naming the right kind, not dropping the head: Desert's own blocks are a real stair and a
        // real slab, so restoring them clears the finding without touching the form.
        var style = HousePresets.Desert.Style;
        await Assert.That(HouseStyleValidation.Check(style).Any(f => f.Field.StartsWith("doorHead"))).IsFalse();
    }

    [Test]
    [Arguments(WindowForm.StairLattice)]
    [Arguments(WindowForm.Arched)]
    public async Task A_stair_form_window_named_a_fence_is_refused(WindowForm form)
    {
        // ashfall-scar: nine houses' stairLattice windows given Oak Fence (85).
        var style = HousePresets.Alpine.Style with { Windows = HousePresets.Alpine.Style.Windows with { Form = form, Block = Blocks.OakFence } };
        var findings = HouseStyleValidation.Check(style);
        await Assert.That(findings.Single().Rule).IsEqualTo(HouseStyleRules.BlockKind);
        await Assert.That(findings.Single().Field).IsEqualTo("windows.block");
    }

    [Test]
    public async Task A_slab_banded_window_named_a_glass_pane_is_refused()
    {
        // sable-marsh's spawn windows: slabBanded given Glass Pane (102).
        var style = HousePresets.Workshop.Style with { Windows = HousePresets.Workshop.Style.Windows with { Block = Blocks.GlassPane } };
        var findings = HouseStyleValidation.Check(style);
        await Assert.That(findings.Single().Rule).IsEqualTo(HouseStyleRules.BlockKind);
        await Assert.That(findings.Single().Field).IsEqualTo("windows.block");
    }

    [Test]
    public async Task A_double_slab_does_not_pass_as_a_slab()
    {
        // 43 is the double stone slab: a full cube regardless of data, so it is the same fault as any other
        // whole-block id in a slab-banded window.
        var style = HousePresets.Workshop.Style with { Windows = HousePresets.Workshop.Style.Windows with { Block = 43 } };
        await Assert.That(HouseStyleValidation.Check(style)).IsNotEmpty();
    }

    [Test]
    public async Task An_open_or_a_pane_window_is_never_checked_for_kind()
    {
        // Open and Pane windows take whatever block is named — there is no geometric role to be the wrong
        // kind of, unlike a stair lattice or a slab band.
        var open = HousePresets.Diorite.Style with { Windows = HousePresets.Diorite.Style.Windows with { Block = Blocks.OakFence } };
        var pane = HousePresets.Townside.Style.Storeys[0].Windows! with { Form = WindowForm.Pane, Block = Blocks.OakFence };
        await Assert.That(HouseStyleValidation.Check(open)).IsEmpty();
        await Assert.That(HouseStyleValidation.CheckWindow("windows", pane)).IsEmpty();
    }

    [Test]
    public async Task A_gable_window_and_a_storey_window_are_checked_the_same_way_the_house_window_is()
    {
        var gableWrong = HousePresets.Alpine.Style with
        {
            Roof = HousePresets.Alpine.Style.Roof with
            {
                GableWindows = new WindowStyle
                {
                    Form = WindowForm.Arched, Block = Blocks.OakFence, Width = 2, Height = 2,
                },
            },
        };
        await Assert.That(HouseStyleValidation.Check(gableWrong).Single().Field).IsEqualTo("gableWindows.block");

        var storeyWrong = HousePresets.Townside.Style with
        {
            Storeys =
            [
                HousePresets.Townside.Style.Storeys[0],
                HousePresets.Townside.Style.Storeys[1] with
                {
                    Windows = HousePresets.Townside.Style.Storeys[1].Windows! with { Block = Blocks.OakFence },
                },
            ],
        };
        await Assert.That(HouseStyleValidation.Check(storeyWrong).Single().Field).IsEqualTo("storeys[1].windows.block");
    }

    // ── HS2 — a door's clear height ───────────────────────────────────────────────────────────────────

    [Test]
    public async Task A_three_course_door_with_a_genuine_upper_slab_head_clears_two_point_five()
        => await Assert.That(HousePresets.Desert.Style.Doorway.Clearance).IsEqualTo(2.5m);

    [Test]
    public async Task The_same_door_with_a_solid_cube_where_the_fill_should_be_clears_only_two()
    {
        // The exact sable-marsh/corvid-hollow measurement: a genuine head with the fill block wrong drops the
        // reported clearance from 2.5 to a flat 2.0.
        var style = Headed(HousePresets.Desert.Style, head => head with { FillBlock = Blocks.Cobblestone });
        await Assert.That(style.Doorway.Clearance).IsEqualTo(2.0m);
        await Assert.That(HouseStyleValidation.Check(style).Select(f => f.Rule)).Contains(HouseStyleRules.DoorClearance);
    }

    [Test]
    public async Task A_door_with_no_head_clears_its_own_door_height()
    {
        await Assert.That(HousePresets.Diorite.Style.Doorway.Clearance).IsEqualTo(3m);
        await Assert.That(HouseStyleValidation.Check(HousePresets.Diorite.Style)).IsEmpty();
    }

    [Test]
    public async Task A_door_head_solid_fill_by_design_still_has_to_clear_two_point_five()
    {
        // Fill = Solid never gives back the half-block, whatever the block is, so a three-course door with a
        // solid-filled head cannot clear the line — this is not a defect in Solid, it is a door too short for
        // the head it wears.
        var style = Headed(HousePresets.Desert.Style, head => head with { Fill = DoorHeadFill.Solid });
        await Assert.That(style.Doorway.Clearance).IsEqualTo(2.0m);
    }

    // ── HS3 — a roof's own materials ──────────────────────────────────────────────────────────────────

    [Test]
    public async Task Diorites_own_construction_is_the_clean_reference()
        // Roof = whole block, RoofSlab = a real slab, at pitch 1: the shape HousePresets.Diorite documents as
        // "the roof a slab is actually for".
        => await Assert.That(HouseStyleValidation.Check(HousePresets.Diorite.Style)).IsEmpty();

    [Test]
    public async Task A_slab_named_as_the_whole_block_roof_with_no_roof_slab_set_is_refused()
    {
        // The Weirgate shed fault, inverted from Diorite's: roof is itself a slab id and roofSlab is -1, so the
        // roof is a course of slabs at a whole block of rise — see-through.
        var style = Roofed(HousePresets.Desert.Style, new SolidMaterial(Blocks.WoodenSlab));
        var findings = HouseStyleValidation.Check(style);
        await Assert.That(findings.Any(f => f.Rule == HouseStyleRules.RoofMaterial && f.Field == "roof")).IsTrue();
    }

    [Test]
    public async Task A_roof_or_a_verge_named_a_log_is_refused()
    {
        var roofLog = Roofed(HousePresets.Desert.Style, new SolidMaterial(Blocks.Log2, 0));
        var vergeLog = Roofed(HousePresets.Desert.Style, verge: new SolidMaterial(Blocks.Log2, 0));
        var roofFindings = HouseStyleValidation.Check(roofLog);
        await Assert.That((roofFindings.Single().Rule, roofFindings.Single().Field))
            .IsEqualTo((HouseStyleRules.RoofMaterial, "roof"));
        await Assert.That(HouseStyleValidation.Check(vergeLog).Single().Field).IsEqualTo("verge");
    }

    [Test]
    public async Task A_roof_or_a_verge_named_a_ground_material_is_refused()
    {
        // quillon-barrow: three houses roofed in Grass Block (2:0) over a Podzol (3:2) verge.
        var style = Roofed(HousePresets.Desert.Style, new SolidMaterial(2, 0), new SolidMaterial(3, 2));
        var findings = HouseStyleValidation.Check(style);
        await Assert.That(findings.Count(f => f.Rule == HouseStyleRules.RoofMaterial)).IsEqualTo(2);
    }

    [Test]
    public async Task RoofSlab_itself_has_to_be_a_single_slab_when_set()
    {
        // Two faults in one field, and both are true: a cobblestone block is not a slab at all (HS1), and it
        // is not the brick the body is laid in either (HS3).
        var style = Slabbed(HousePresets.Diorite.Style, Blocks.Cobblestone);
        var findings = HouseStyleValidation.Check(style);
        await Assert.That(findings.All(f => f.Field == "roofSlab")).IsTrue();
        await Assert.That(findings.Select(f => f.Rule))
            .Contains(HouseStyleRules.BlockKind).And.Contains(HouseStyleRules.RoofMaterial);
    }

    /// <summary>A roof is read as one plane from below and from a distance, so a pattern in it is several
    /// blocks in one surface. Both halves are held to a single block.</summary>
    [Test]
    public async Task A_patterned_roof_or_verge_is_refused()
    {
        var voronoi = new VoronoiMaterial(1, 5,
            [new VoronoiBand(new SolidMaterial(98), 1), new VoronoiBand(new SolidMaterial(Blocks.Planks, 1), 1)]);
        var body = HouseStyleValidation.Check(Roofed(HousePresets.Desert.Style, voronoi));
        await Assert.That(body.Any(f => f.Rule == HouseStyleRules.RoofMaterial && f.Field == "roof")).IsTrue();
        var verge = HouseStyleValidation.Check(Roofed(HousePresets.Desert.Style, verge: voronoi));
        await Assert.That(verge.Any(f => f.Rule == HouseStyleRules.RoofMaterial && f.Field == "verge")).IsTrue();
    }

    /// <summary>The half-course slab continues the body by halves, so it is the body's own material. Kiln
    /// Row's four styles put a sandstone slab (44:1) under a brick roof (45); Rimegarth's four put a spruce
    /// slab under a snow one.</summary>
    [Test]
    public async Task A_roof_slab_of_another_material_than_the_body_is_refused()
    {
        var kilnRow = Roofed(HousePresets.Diorite.Style, new SolidMaterial(45)) with { };
        kilnRow = kilnRow with { Roof = kilnRow.Roof with { Slab = Blocks.StoneSlab, SlabData = 1 } };
        var findings = HouseStyleValidation.Check(kilnRow);
        await Assert.That(findings.Any(f => f.Rule == HouseStyleRules.RoofMaterial && f.Field == "roofSlab"))
            .IsTrue();
    }

    /// <summary>The same roof in one material passes — a brick body over the brick slab (44:4), which is the
    /// whole brick roof the rule exists to allow.</summary>
    [Test]
    public async Task A_roof_and_its_slab_in_one_material_pass()
    {
        var brick = Roofed(HousePresets.Diorite.Style, new SolidMaterial(45)) with { };
        brick = brick with { Roof = brick.Roof with { Slab = Blocks.StoneSlab, SlabData = 4 } };
        await Assert.That(HouseStyleValidation.Check(brick).Any(f => f.Field == "roofSlab")).IsFalse();
    }

    // ── the footing has a legible off switch ───────────────────────────────────────────────────────────

    /// <summary><b>No footing is a state, not a block that happens to be air.</b> It was a bare
    /// <c>SolidMaterial(Air)</c> standing in for one, so "does this building have a footing" was a comparison
    /// against a sentinel rather than a question the style could answer.</summary>
    [Test]
    public async Task A_building_seated_into_terrain_has_no_footing_at_all()
    {
        foreach (var house in new[] { HousePresets.Alpine, HousePresets.Desert, HousePresets.Diorite, HousePresets.Townside, HousePresets.Stilts })
            await Assert.That((house.Name, house.Style.Foundation.Footing)).IsEqualTo((house.Name, (TerrainMaterial?)null));
    }

    [Test]
    public async Task The_village_row_keeps_its_plinth()
    {
        foreach (var house in HousePresets.Village)
            await Assert.That(house.Style.Foundation.Footing).IsNotNull();
    }

    // ── BlockFamilies ──────────────────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task Cobblestone_is_neither_a_stair_nor_a_slab()
    {
        await Assert.That(BlockFamilies.IsStair(Blocks.Cobblestone)).IsFalse();
        await Assert.That(BlockFamilies.IsSlab(Blocks.Cobblestone)).IsFalse();
    }

    [Test]
    public async Task Stone_brick_is_not_its_own_stair_id()
        // corvid-hollow's fault exactly: 98 is Stone Brick, 109 is Stone Brick Stairs.
        => await Assert.That(BlockFamilies.IsStair(98)).IsFalse();

    [Test]
    [Arguments(44)]
    [Arguments(126)]
    [Arguments(182)]
    public async Task Single_slabs_are_slabs(int blockId)
        => await Assert.That(BlockFamilies.IsSlab(blockId)).IsTrue();

    [Test]
    [Arguments(43)]
    [Arguments(125)]
    [Arguments(181)]
    public async Task Double_slabs_are_not_slabs(int blockId)
    {
        await Assert.That(BlockFamilies.IsSlab(blockId)).IsFalse();
        await Assert.That(BlockFamilies.IsDoubleSlab(blockId)).IsTrue();
    }

    [Test]
    public async Task Logs_and_ground_are_named()
    {
        await Assert.That(BlockFamilies.IsLog(Blocks.Log)).IsTrue();
        await Assert.That(BlockFamilies.IsLog(Blocks.Log2)).IsTrue();
        await Assert.That(BlockFamilies.IsSoil(2)).IsTrue();     // Grass Block
        await Assert.That(BlockFamilies.IsSoil(3)).IsTrue();     // Dirt / Podzol
        await Assert.That(BlockFamilies.IsSoil(Blocks.Cobblestone)).IsFalse();
    }
}
