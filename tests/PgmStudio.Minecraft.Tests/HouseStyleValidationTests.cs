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
        await Assert.That(findings.Any(f => f.Rule == HouseStyleRules.BlockKind && f.Field == "windows.block"))
            .IsTrue();
    }

    [Test]
    public async Task A_slab_banded_window_named_a_glass_pane_is_refused()
    {
        // sable-marsh's spawn windows: slabBanded given Glass Pane (102).
        var style = HousePresets.Workshop.Style with { Windows = HousePresets.Workshop.Style.Windows with { Block = Blocks.GlassPane } };
        var findings = HouseStyleValidation.Check(style);
        await Assert.That(findings.Any(f => f.Rule == HouseStyleRules.BlockKind && f.Field == "windows.block"))
            .IsTrue();
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

    /// <summary>A <b>bare</b> log has no axis, so every one of them stands upright and shows a sawn face out
    /// at the slope. That is the fault, and it is the log with no axis rather than the log.</summary>
    [Test]
    public async Task A_roof_or_a_verge_named_a_bare_log_is_refused()
    {
        var roofLog = Roofed(HousePresets.Desert.Style, new SolidMaterial(Blocks.Log2, 0));
        var vergeLog = Roofed(HousePresets.Desert.Style, verge: new SolidMaterial(Blocks.Log2, 0));
        var roofFindings = HouseStyleValidation.Check(roofLog);
        await Assert.That((roofFindings.Single().Rule, roofFindings.Single().Field))
            .IsEqualTo((HouseStyleRules.RoofMaterial, "roof"));
        await Assert.That(HouseStyleValidation.Check(vergeLog).Single().Field).IsEqualTo("verge");
    }

    /// <summary>A <b>laid</b> log is a roof material, on the body and on the verge alike — the verge because an
    /// unbound one is the body reaching the edge, which is what a roof laid in one thing looks like. It is one
    /// block that takes its axis from the surface, so it is not the "several blocks in one surface" a pattern
    /// on a roof is refused for either.</summary>
    [Test]
    public async Task A_roof_laid_in_logs_is_allowed_on_the_body_and_on_the_verge()
    {
        var laid = new LaidLogMaterial(Blocks.Log, 1);   // spruce
        await Assert.That(HouseStyleValidation.Check(Roofed(HousePresets.Desert.Style, laid, laid))).IsEmpty();
        await Assert.That(HouseStyleValidation.Check(Roofed(HousePresets.Desert.Style, laid))).IsEmpty();
    }

    /// <summary>Laying something that is not a log is the fault the laid kind can still commit: only a log
    /// carries its axis in its data, so anything else laid comes out turned at random.</summary>
    [Test]
    public async Task A_roof_laid_in_something_that_is_not_a_log_is_refused()
    {
        var style = Roofed(HousePresets.Desert.Style, new LaidLogMaterial(Blocks.Stone));
        var findings = HouseStyleValidation.Check(style);
        await Assert.That((findings.Single().Rule, findings.Single().Field))
            .IsEqualTo((HouseStyleRules.RoofMaterial, "roof"));
    }

    /// <summary>No slab is cut from a log, so a half-course rise over a laid-log roof alternates logs with
    /// something that is not one, all the way up the slope.</summary>
    [Test]
    public async Task A_half_course_rise_over_a_laid_log_roof_is_refused()
    {
        var style = Slabbed(Roofed(HousePresets.Desert.Style, new LaidLogMaterial(Blocks.Log, 1)), Blocks.StoneSlab);
        var findings = HouseStyleValidation.Check(style);
        await Assert.That(findings.Any(f => f.Rule == HouseStyleRules.RoofMaterial && f.Field == "roofSlab")).IsTrue();
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

    // ── HS1 · HS4 · HS5 · HS6 — the beams, the pairs, the ores, and a door with no wall ────────────────

    /// <summary>The beams that run past a building's corners are the ends of its floor timbers, and a log is
    /// what one is cut from — which is what <see cref="BeamStyle.Block"/>'s own docstring has always said.
    /// `sn-compass-keep` gave them iron ore.</summary>
    [Test]
    public async Task A_beam_that_is_not_a_log_is_refused()
    {
        var style = HousePresets.Alpine.Style with { Beams = new BeamStyle { Block = 15 } };
        var findings = HouseStyleValidation.Check(style);
        await Assert.That(findings.Any(f => f.Rule == HouseStyleRules.BlockKind && f.Field == "beams.block"))
            .IsTrue();
        // and the same block is an ore wherever it stands
        await Assert.That(findings.Any(f => f.Rule == HouseStyleRules.OreMaterial)).IsTrue();
    }

    /// <summary>A door head's two corners and the line between them are one head. `kr-block` and its three
    /// siblings put a birch stair over a sandstone slab.</summary>
    [Test]
    public async Task A_door_head_of_two_materials_is_refused()
    {
        var style = Headed(HousePresets.Alpine.Style,
            head => head with { Form = DoorHeadForm.Arched, Block = 135, Fill = DoorHeadFill.UpperSlab,
                                FillBlock = Blocks.StoneSlab, FillData = 1 });
        var findings = HouseStyleValidation.Check(style);
        await Assert.That(findings.Any(f => f.Rule == HouseStyleRules.PartMaterial
                                         && f.Field == "doorHead.fillBlock")).IsTrue();

        // the same head with a birch slab under the birch stair passes
        var birch = Headed(HousePresets.Alpine.Style,
            head => head with { Form = DoorHeadForm.Arched, Block = 135, Fill = DoorHeadFill.UpperSlab,
                                FillBlock = Blocks.WoodenSlab, FillData = 2 });
        await Assert.That(HouseStyleValidation.Check(birch)
            .Any(f => f.Rule == HouseStyleRules.PartMaterial)).IsFalse();
    }

    /// <summary>A window and the block it is seated in are one opening.</summary>
    [Test]
    public async Task A_window_seated_in_another_material_is_refused()
    {
        var style = HousePresets.Workshop.Style with
        {
            Windows = HousePresets.Workshop.Style.Windows with { HostBlock = 24, HostData = 0 },
        };
        var findings = HouseStyleValidation.Check(style);
        await Assert.That(findings.Any(f => f.Rule == HouseStyleRules.PartMaterial
                                         && f.Field == "windows.hostBlock")).IsTrue();
    }

    /// <summary>An ore is stone with something in it. `sb-assay` and `sn-compass-keep` built walls, posts and
    /// beams out of iron ore.</summary>
    [Test]
    public async Task An_ore_named_anywhere_in_a_style_is_refused()
    {
        var style = HousePresets.Alpine.Style with { Post = new SolidMaterial(15) };
        var findings = HouseStyleValidation.Check(style);
        await Assert.That(findings.Any(f => f.Rule == HouseStyleRules.OreMaterial && f.Field == "post"))
            .IsTrue();
    }

    /// <summary>A house on stilts has no wall on its ground storey, so an arch and its lintel over the
    /// doorway stand in mid-air — `ow-stilt`, on Overwall. The doorway itself is not the fault: an opening cut
    /// in an open storey is nothing at all, which is why <see cref="HousePresets.Stilts"/> passes.</summary>
    [Test]
    public async Task A_door_head_over_an_open_storey_is_refused_and_a_bare_doorway_is_not()
    {
        await Assert.That(HouseStyleValidation.Check(HousePresets.Stilts.Style)).IsEmpty();

        var headed = Headed(HousePresets.Stilts.Style,
            head => head with { Form = DoorHeadForm.Arched, Block = 109,
                                Fill = DoorHeadFill.UpperSlab, FillBlock = Blocks.StoneSlab, FillData = 5 });
        var findings = HouseStyleValidation.Check(headed);
        await Assert.That(findings.Any(f => f.Rule == HouseStyleRules.DoorWithoutWall)).IsTrue();
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

    // ── a porch the wall it is attached to cannot carry (HS8) ─────────────────────────────────────────

    private static HouseStyle Porched(int wallCourses, int porchDepth, int doorHeight) => new()
    {
        Wall = RoomPart.Of(new SolidMaterial(Blocks.Cobblestone), wallCourses),
        Storeys = [new Storey { Clear = wallCourses }],
        Roof = new RoofStyle { Form = RoofForm.Shed, Pitch = 1, Overhang = 1 },
        Porch = new PorchStyle { Depth = porchDepth, Roof = RoofForm.Shed },
        Doorway = new Doorway { Width = 2, Height = doorHeight },
    };

    /// <summary>The corpus fault: `opus5-mootgate`'s market stall — a three-course wall, a three-course door
    /// and a two-deep porch. The canopy is seated clear of the door and its ridge follows the form up, so it
    /// tops out above the eave of the house it is attached to and reads as a second building.</summary>
    [Test]
    public async Task A_canopy_that_climbs_past_its_own_wall_is_HS8()
    {
        var findings = HouseStyleValidation.Check(Porched(wallCourses: 3, porchDepth: 2, doorHeight: 3));

        var porch = findings.Single(finding => finding.Rule == HouseStyleRules.PorchHeadroom);
        await Assert.That(porch.Severity).IsEqualTo(Severity.Complaint);   // the porch is built either way
        await Assert.That(porch.Field).IsEqualTo("porch");
        await Assert.That(porch.Message).Contains("4 course(s) above");    // 3 + 2 + 2 wanted against 3
    }

    /// <summary>And a wall with the courses for it says nothing. The three numbers that buy them are all the
    /// author's, so each is proved to buy what the rule claims.</summary>
    [Test]
    [Arguments(7, 2, 3)]     // the wall raised to what the canopy wants
    [Arguments(6, 1, 3)]     // a shallower porch: one course of rise instead of two, so one course less wall
    [Arguments(5, 2, 1)]     // a lower door: two courses off the door is two courses off the wall
    public async Task A_wall_with_the_courses_its_porch_needs_says_nothing(int wall, int depth, int door)
    {
        var findings = HouseStyleValidation.Check(Porched(wall, depth, door));
        await Assert.That(findings.Any(finding => finding.Rule == HouseStyleRules.PorchHeadroom)).IsFalse();
    }

    /// <summary>A style with no porch is never asked.</summary>
    [Test]
    public async Task A_style_with_no_porch_is_not_HS8()
    {
        var findings = HouseStyleValidation.Check(Porched(3, 2, 3) with { Porch = null });
        await Assert.That(findings.Any(finding => finding.Rule == HouseStyleRules.PorchHeadroom)).IsFalse();
    }
}
