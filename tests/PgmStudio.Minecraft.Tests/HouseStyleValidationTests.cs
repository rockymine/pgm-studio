using PgmStudio.Domain;
using PgmStudio.Minecraft.Houses;
using PgmStudio.Minecraft.Painting;
using PgmStudio.Minecraft.Palette;
using PgmStudio.Vocabulary;
using PgmStudio.Minecraft.Library;
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

    // The kept pyramid house: a hip in stone brick climbing on stone brick half slabs at pitch 1, and a doorway
    // with no head.
    private static HouseStyle Pyramid =>
        SeedFolder.House("diorite-blue-clay-pyramid-house");

    // A house whose windows are slab-banded in spruce slabs and seated in its spruce boarding — the form built
    // right, on the preset whose upper wall is that boarding.
    private static HouseStyle Banded => SeededHouses.Longhouse with
    {
        Windows = new WindowStyle
        {
            Form = WindowForm.SlabBanded, Block = Blocks.WoodenSlab, Data = 1,
            Width = 3, Sill = 4, Spacing = 3, HostBlock = Blocks.Planks, HostData = 1,
        },
    };

    // ── the shipped presets are clean ──────────────────────────────────────────────────────────────────

    /// <summary>Every house the library is seeded with is clean: a board names one by its library name, and a
    /// style the gate refuses could never be stored on the board that names it.</summary>
    [Test]
    [MethodDataSource(nameof(SeededNames))]
    public async Task Every_seeded_house_passes_the_gate(string name)
        => await Assert.That(HouseStyleValidation.Check(SeedFolder.House(name))).IsEmpty();

    public static IEnumerable<string> SeededNames() => SeedFolder.Houses.Select(house => house.Name);

    /// <summary>A house built on <see cref="WindowForm.StairLattice"/> and one on <see cref="WindowForm.SlabBanded"/>
    /// both pass clean — pinned on its own so the pattern is not lost inside the loop over every preset. Neither form is a defect: the author has ruled both
    /// allowed, on any house, and the corpus complaint was always the block handed to the form (a fence where a
    /// stair goes, a pane where a slab goes — <see cref="HouseStyleRules.BlockKind"/>), never the pattern
    /// itself.</summary>
    [Test]
    public async Task A_stair_lattice_and_a_slab_band_pass_clean_with_the_right_block()
    {
        await Assert.That(HouseStyleValidation.Check(SeededHouses.Alpine)).IsEmpty();
        await Assert.That(HouseStyleValidation.Check(Banded)).IsEmpty();
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
        var style = Headed(SeededHouses.Desert,
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
        var style = SeededHouses.Desert;
        await Assert.That(HouseStyleValidation.Check(style).Any(f => f.Field?.StartsWith("doorHead") == true)).IsFalse();
    }

    [Test]
    [Arguments(WindowForm.StairLattice)]
    [Arguments(WindowForm.Arched)]
    public async Task A_stair_form_window_named_a_fence_is_refused(WindowForm form)
    {
        // ashfall-scar: nine houses' stairLattice windows given Oak Fence (85).
        var style = SeededHouses.Alpine with { Windows = SeededHouses.Alpine.Windows with { Form = form, Block = Blocks.OakFence } };
        var findings = HouseStyleValidation.Check(style);
        await Assert.That(findings.Any(f => f.Rule == HouseStyleRules.BlockKind && f.Field == "windows.block"))
            .IsTrue();
    }

    [Test]
    public async Task A_slab_banded_window_named_a_glass_pane_is_refused()
    {
        // sable-marsh's spawn windows: slabBanded given Glass Pane (102).
        var style = Banded with { Windows = Banded.Windows with { Block = Blocks.GlassPane } };
        var findings = HouseStyleValidation.Check(style);
        await Assert.That(findings.Any(f => f.Rule == HouseStyleRules.BlockKind && f.Field == "windows.block"))
            .IsTrue();
    }

    [Test]
    public async Task A_double_slab_does_not_pass_as_a_slab()
    {
        // 43 is the double stone slab: a full cube regardless of data, so it is the same fault as any other
        // whole-block id in a slab-banded window.
        var style = Banded with { Windows = Banded.Windows with { Block = 43 } };
        await Assert.That(HouseStyleValidation.Check(style)).IsNotEmpty();
    }

    [Test]
    public async Task An_open_or_a_pane_window_is_never_checked_for_kind()
    {
        // Open and Pane windows take whatever block is named — there is no geometric role to be the wrong
        // kind of, unlike a stair lattice or a slab band.
        var desert = SeededHouses.Desert;
        var open = desert with { Windows = desert.Windows with { Form = WindowForm.Open, Block = Blocks.OakFence } };
        var pane = SeededHouses.Townside.Storeys[0].Windows! with { Form = WindowForm.Pane, Block = Blocks.OakFence };
        await Assert.That(HouseStyleValidation.Check(open)).IsEmpty();
        await Assert.That(HouseStyleValidation.CheckWindow("windows", pane)).IsEmpty();
    }

    [Test]
    public async Task A_gable_window_and_a_storey_window_are_checked_the_same_way_the_house_window_is()
    {
        var gableWrong = SeededHouses.Alpine with
        {
            Roof = SeededHouses.Alpine.Roof with
            {
                GableWindows = new WindowStyle
                {
                    Form = WindowForm.Arched, Block = Blocks.OakFence, Width = 2, Height = 2,
                },
            },
        };
        await Assert.That(HouseStyleValidation.Check(gableWrong).Single().Field).IsEqualTo("gableWindows.block");

        var storeyWrong = SeededHouses.Townside with
        {
            Storeys =
            [
                SeededHouses.Townside.Storeys[0],
                SeededHouses.Townside.Storeys[1] with
                {
                    Windows = SeededHouses.Townside.Storeys[1].Windows! with { Block = Blocks.OakFence },
                },
            ],
        };
        await Assert.That(HouseStyleValidation.Check(storeyWrong).Single().Field).IsEqualTo("storeys[1].windows.block");
    }

    // ── HS2 — a door's clear height ───────────────────────────────────────────────────────────────────

    [Test]
    public async Task A_three_course_door_with_a_genuine_upper_slab_head_clears_two_point_five()
        => await Assert.That(SeededHouses.Desert.Doorway.Clearance).IsEqualTo(2.5m);

    [Test]
    public async Task The_same_door_with_a_solid_cube_where_the_fill_should_be_clears_only_two()
    {
        // The exact sable-marsh/corvid-hollow measurement: a genuine head with the fill block wrong drops the
        // reported clearance from 2.5 to a flat 2.0.
        var style = Headed(SeededHouses.Desert, head => head with { FillBlock = Blocks.Cobblestone });
        await Assert.That(style.Doorway.Clearance).IsEqualTo(2.0m);
        await Assert.That(HouseStyleValidation.Check(style).Select(f => f.Rule)).Contains(HouseStyleRules.DoorClearance);
    }

    [Test]
    public async Task A_door_with_no_head_clears_its_own_door_height()
    {
        await Assert.That(Pyramid.Doorway.Clearance).IsEqualTo(3m);
        await Assert.That(HouseStyleValidation.Check(Pyramid)).IsEmpty();
    }

    [Test]
    public async Task A_door_head_solid_fill_by_design_still_has_to_clear_two_point_five()
    {
        // Fill = Solid never gives back the half-block, whatever the block is, so a three-course door with a
        // solid-filled head cannot clear the line — this is not a defect in Solid, it is a door too short for
        // the head it wears.
        var style = Headed(SeededHouses.Desert, head => head with { Fill = DoorHeadFill.Solid });
        await Assert.That(style.Doorway.Clearance).IsEqualTo(2.0m);
    }

    // ── HS3 — a roof's own materials ──────────────────────────────────────────────────────────────────

    [Test]
    public async Task The_pyramid_houses_own_construction_is_the_clean_reference()
        // Roof = whole block, RoofSlab = a real slab of it, at pitch 1: the roof a slab is actually for.
        => await Assert.That(HouseStyleValidation.Check(Pyramid)).IsEmpty();

    [Test]
    public async Task A_slab_named_as_the_whole_block_roof_with_no_roof_slab_set_is_refused()
    {
        // The Weirgate shed fault, inverted from Diorite's: roof is itself a slab id and roofSlab is -1, so the
        // roof is a course of slabs at a whole block of rise — see-through.
        var style = Roofed(SeededHouses.Desert, new SolidMaterial(Blocks.WoodenSlab));
        var findings = HouseStyleValidation.Check(style);
        await Assert.That(findings.Any(f => f.Rule == HouseStyleRules.RoofMaterial && f.Field == "roof")).IsTrue();
    }

    /// <summary>A <b>bare</b> log has no axis, so every one of them stands upright and shows a sawn face out
    /// at the slope. That is the fault, and it is the log with no axis rather than the log.</summary>
    [Test]
    public async Task A_roof_or_a_verge_named_a_bare_log_is_refused()
    {
        var roofLog = Roofed(SeededHouses.Desert, new SolidMaterial(Blocks.Log2, 0));
        var vergeLog = Roofed(SeededHouses.Desert, verge: new SolidMaterial(Blocks.Log2, 0));
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
        await Assert.That(HouseStyleValidation.Check(Roofed(SeededHouses.Desert, laid, laid))).IsEmpty();
        await Assert.That(HouseStyleValidation.Check(Roofed(SeededHouses.Desert, laid))).IsEmpty();
    }

    /// <summary>Laying something that is not a log is the fault the laid kind can still commit: only a log
    /// carries its axis in its data, so anything else laid comes out turned at random.</summary>
    [Test]
    public async Task A_roof_laid_in_something_that_is_not_a_log_is_refused()
    {
        var style = Roofed(SeededHouses.Desert, new LaidLogMaterial(Blocks.Stone));
        var findings = HouseStyleValidation.Check(style);
        await Assert.That((findings.Single().Rule, findings.Single().Field))
            .IsEqualTo((HouseStyleRules.RoofMaterial, "roof"));
    }

    /// <summary>No slab is cut from a log, so a half-course rise over a laid-log roof alternates logs with
    /// something that is not one, all the way up the slope.</summary>
    [Test]
    public async Task A_half_course_rise_over_a_laid_log_roof_is_refused()
    {
        var style = Slabbed(Roofed(SeededHouses.Desert, new LaidLogMaterial(Blocks.Log, 1)), Blocks.StoneSlab);
        var findings = HouseStyleValidation.Check(style);
        await Assert.That(findings.Any(f => f.Rule == HouseStyleRules.RoofMaterial && f.Field == "roofSlab")).IsTrue();
    }

    [Test]
    public async Task A_roof_or_a_verge_named_a_ground_material_is_refused()
    {
        // quillon-barrow: three houses roofed in Grass Block (2:0) over a Podzol (3:2) verge.
        var style = Roofed(SeededHouses.Desert, new SolidMaterial(2, 0), new SolidMaterial(3, 2));
        var findings = HouseStyleValidation.Check(style);
        await Assert.That(findings.Count(f => f.Rule == HouseStyleRules.RoofMaterial)).IsEqualTo(2);
    }

    /// <summary>An oak-planked roof stepping in the stair named.</summary>
    private static HouseStyle Staired(int stair, int slab = -1) => new()
    {
        Roof = new RoofStyle
        {
            Body = new SolidMaterial(Blocks.Planks, 0), Verge = new SolidMaterial(Blocks.Planks, 5),
            Stair = stair, Slab = slab,
        },
    };

    private static IEnumerable<Finding> AtRoofStair(HouseStyle style) =>
        HouseStyleValidation.Check(style).Where(f => f.Field == "roofStair");

    /// <summary><b>A roof stair is a stair of the body's own material, and a roof climbs in stairs or in
    /// slabs.</b> Oak stairs over oak planks pass; a plank is not a stair (<c>HS1</c>); a spruce stair over oak
    /// is two materials, and a stair beside a slab is two rises (<c>HS3</c>).</summary>
    [Test]
    public async Task A_roof_stair_is_a_stair_of_the_bodys_own_material_and_never_with_a_slab()
    {
        await Assert.That(AtRoofStair(Staired(Blocks.OakStairs))).IsEmpty();
        await Assert.That(AtRoofStair(Staired(Blocks.Planks)).Select(f => f.Rule)).Contains(HouseStyleRules.BlockKind);
        await Assert.That(AtRoofStair(Staired(134)).Select(f => f.Rule)).Contains(HouseStyleRules.RoofMaterial);
        await Assert.That(AtRoofStair(Staired(Blocks.OakStairs, slab: Blocks.WoodenSlab)).Select(f => f.Rule))
            .Contains(HouseStyleRules.RoofMaterial);
    }

    [Test]
    public async Task RoofSlab_itself_has_to_be_a_single_slab_when_set()
    {
        // Two faults in one field, and both are true: a cobblestone block is not a slab at all (HS1), and it
        // is not the stone brick the body is laid in either (HS3).
        var style = Slabbed(Pyramid, Blocks.Cobblestone);
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
        var body = HouseStyleValidation.Check(Roofed(SeededHouses.Desert, voronoi));
        await Assert.That(body.Any(f => f.Rule == HouseStyleRules.RoofMaterial && f.Field == "roof")).IsTrue();
        var verge = HouseStyleValidation.Check(Roofed(SeededHouses.Desert, verge: voronoi));
        await Assert.That(verge.Any(f => f.Rule == HouseStyleRules.RoofMaterial && f.Field == "verge")).IsTrue();
    }

    /// <summary>The half-course slab continues the body by halves, so it is the body's own material. Kiln
    /// Row's four styles put a sandstone slab (44:1) under a brick roof (45); Rimegarth's four put a spruce
    /// slab under a snow one.</summary>
    [Test]
    public async Task A_roof_slab_of_another_material_than_the_body_is_refused()
    {
        var kilnRow = Roofed(Pyramid, new SolidMaterial(45)) with { };
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
        var brick = Roofed(Pyramid, new SolidMaterial(45)) with { };
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
        var style = SeededHouses.Alpine with { Beams = new BeamStyle { Block = 15 } };
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
        var style = Headed(SeededHouses.Alpine,
            head => head with { Form = DoorHeadForm.Arched, Block = 135, Fill = DoorHeadFill.UpperSlab,
                                FillBlock = Blocks.StoneSlab, FillData = 1 });
        var findings = HouseStyleValidation.Check(style);
        await Assert.That(findings.Any(f => f.Rule == HouseStyleRules.PartMaterial
                                         && f.Field == "doorHead.fillBlock")).IsTrue();

        // the same head with a birch slab under the birch stair passes
        var birch = Headed(SeededHouses.Alpine,
            head => head with { Form = DoorHeadForm.Arched, Block = 135, Fill = DoorHeadFill.UpperSlab,
                                FillBlock = Blocks.WoodenSlab, FillData = 2 });
        await Assert.That(HouseStyleValidation.Check(birch)
            .Any(f => f.Rule == HouseStyleRules.PartMaterial)).IsFalse();
    }

    /// <summary>A window and the block it is seated in are one opening.</summary>
    [Test]
    public async Task A_window_seated_in_another_material_is_refused()
    {
        var style = Banded with
        {
            Windows = Banded.Windows with { HostBlock = 24, HostData = 0 },
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
        var style = SeededHouses.Alpine with { Post = new SolidMaterial(15) };
        var findings = HouseStyleValidation.Check(style);
        await Assert.That(findings.Any(f => f.Rule == HouseStyleRules.OreMaterial && f.Field == "post"))
            .IsTrue();
    }

    /// <summary>A house on stilts has no wall on its ground storey, so an arch and its lintel over the
    /// doorway stand in mid-air — `ow-stilt`, on Overwall. The doorway itself is not the fault: an opening cut
    /// in an open storey is nothing at all, which is why <see cref="SeededHouses.Stilts"/> passes.</summary>
    [Test]
    public async Task A_door_head_over_an_open_storey_is_refused_and_a_bare_doorway_is_not()
    {
        await Assert.That(HouseStyleValidation.Check(SeededHouses.Stilts)).IsEmpty();

        var headed = Headed(SeededHouses.Stilts,
            head => head with { Form = DoorHeadForm.Arched, Block = 109,
                                Fill = DoorHeadFill.UpperSlab, FillBlock = Blocks.StoneSlab, FillData = 5 });
        var findings = HouseStyleValidation.Check(headed);
        await Assert.That(findings.Any(f => f.Rule == HouseStyleRules.DoorWithoutWall)).IsTrue();
    }

    // ── the footing has a legible off switch ───────────────────────────────────────────────────────────

    /// <summary><b>No footing is a state, not a block that happens to be air</b>, and it is the state every
    /// shipped style is in: "does this building have a footing" is a question the style answers with null.</summary>
    [Test]
    public async Task No_shipped_style_has_a_footing()
    {
        foreach (var (name, style) in SeedFolder.Houses)
            await Assert.That((name, style.Foundation.Footing)).IsEqualTo((name, (TerrainMaterial?)null));
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
        Roof = new RoofStyle { Form = RoofForm.Gable, Pitch = 1, Overhang = 1 },
        Porch = new PorchStyle { Depth = porchDepth, Roof = RoofForm.Gable },
        Doorway = new Doorway { Width = 2, Height = doorHeight },
    };

    /// <summary>A three-course wall, a three-course door and a three-deep porch. The canopy is seated clear of
    /// the door and its ridge follows the form up, so it tops out above the eave of the house it is attached to
    /// and reads as a second building.</summary>
    [Test]
    public async Task A_canopy_that_climbs_past_its_own_wall_is_HS8()
    {
        var findings = HouseStyleValidation.Check(Porched(wallCourses: 3, porchDepth: 3, doorHeight: 3));

        var porch = findings.Single(finding => finding.Rule == HouseStyleRules.PorchHeadroom);
        await Assert.That(porch.Severity).IsEqualTo(Severity.Complaint);   // the porch is built either way
        await Assert.That(porch.Field).IsEqualTo("porch");
        await Assert.That(porch.Message).Contains("4 course(s) above");    // 3 + 2 + 2 wanted against 3
    }

    /// <summary>And a wall with the courses for it says nothing. The three numbers that buy them are all the
    /// author's, so each is proved to buy what the rule claims.</summary>
    [Test]
    [Arguments(7, 3, 3)]     // the wall raised to what the canopy wants
    [Arguments(6, 2, 3)]     // a shallower porch: one course of rise instead of two, so one course less wall
    [Arguments(5, 3, 1)]     // a lower door: two courses off the door is two courses off the wall
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

    // ── a beam with nothing behind it, and a frame in two woods (HS9, HS4) ────────────────────────────

    private static readonly TerrainMaterial Masonry = new SolidMaterial(98, 0);

    // Two storeys of three clear: the lower lays four courses, the fourth being the seam the beam ends come out of.
    private static HouseStyle Framed(TerrainMaterial wall, int postWood, int beamWood, int? laidWood = null) => new()
    {
        Wall = new RoomPart(new BandStack(laidWood is { } laid
            ? [new Band(wall, 3), new Band(new LaidLogMaterial(Blocks.Log, laid), 1)]
            : [new Band(wall, 4)]), 4),
        Storeys = [new Storey { Clear = 3 }, new Storey { Clear = 3 }],
        Post = new SolidMaterial(Blocks.Log, postWood),
        Beams = new BeamStyle { Block = Blocks.Log, Data = beamWood, Reach = 1 },
    };

    /// <summary>The corpus fault: `opus5-mootgate`'s townhouse lays spruce beam ends at each storey seam over
    /// a wall of cobble and stone brick. A beam end is the end of a floor timber; over masonry it is eight
    /// logs sticking out of a wall that is carrying nothing.</summary>
    [Test]
    public async Task Beams_over_a_wall_with_no_laid_log_are_HS9()
    {
        var findings = HouseStyleValidation.Check(Framed(Masonry, postWood: 1, beamWood: 1));

        var beams = findings.Single(finding => finding.Rule == HouseStyleRules.BeamsWithoutTimber);
        await Assert.That(beams.Severity).IsEqualTo(Severity.Complaint);
        await Assert.That(beams.Field).IsEqualTo("storeys[0].wall");
        await Assert.That(beams.Message).Contains("laid log");
    }

    /// <summary>The author's ruling: the laid log is at the level of the corner beams. A course of it anywhere else
    /// in the wall is not the timber the ends come out of.</summary>
    [Test]
    public async Task A_laid_log_on_another_course_than_the_beams_is_HS9()
    {
        var lower = Framed(Masonry, postWood: 1, beamWood: 1) with
        {
            Wall = new RoomPart(new BandStack(
                [new Band(Masonry, 1), new Band(new LaidLogMaterial(Blocks.Log, 1), 1), new Band(Masonry, 2)]), 4),
        };
        var findings = HouseStyleValidation.Check(lower);
        await Assert.That(findings.Any(finding => finding.Rule == HouseStyleRules.BeamsWithoutTimber)).IsTrue();
    }

    /// <summary>A wall carrying one says nothing — `opus5-scarrow-delph`'s stilt houses are that building, and
    /// they are what the detail looks like done right.</summary>
    [Test]
    public async Task Beams_over_a_wall_that_carries_a_laid_log_say_nothing()
    {
        var findings = HouseStyleValidation.Check(Framed(Masonry, postWood: 1, beamWood: 1, laidWood: 1));
        await Assert.That(findings.Any(finding => finding.Rule == HouseStyleRules.BeamsWithoutTimber)).IsFalse();
    }

    /// <summary>And a building of one storey never lays a beam, so the word on it is inert rather than
    /// wrong.</summary>
    [Test]
    public async Task Beams_on_a_single_storey_building_are_not_asked()
    {
        var single = Framed(Masonry, postWood: 1, beamWood: 1) with { Storeys = [new Storey { Clear = 3 }] };
        var findings = HouseStyleValidation.Check(single);
        await Assert.That(findings.Any(finding => finding.Rule == HouseStyleRules.BeamsWithoutTimber)).IsFalse();
    }

    /// <summary>The other half, and the other corpus fault: `opus5-scarrow-delph`'s stilt houses stand spruce
    /// posts under oak beams over an oak laid-log course. A post, the ends docking against it and the course
    /// they are the ends of are one frame, so they are cut from one wood — the same rule a door head's stair
    /// and its slab fill answer to.</summary>
    [Test]
    public async Task A_frame_cut_from_two_woods_is_HS4()
    {
        // spruce post (17:1), oak beams and oak laid log (17:0) — exactly the corpus pair.
        var findings = HouseStyleValidation.Check(Framed(Masonry, postWood: 1, beamWood: 0, laidWood: 0));

        var frame = findings.Single(finding => finding.Rule == HouseStyleRules.PartMaterial
                                            && finding.Field == "post");
        await Assert.That(frame.Message).Contains("oak");
        await Assert.That(frame.Message).Contains("spruce");
    }

    /// <summary>One wood throughout says nothing.</summary>
    [Test]
    public async Task A_frame_cut_from_one_wood_says_nothing()
    {
        var findings = HouseStyleValidation.Check(Framed(Masonry, postWood: 0, beamWood: 0, laidWood: 0));
        await Assert.That(findings.Any(finding => finding.Rule == HouseStyleRules.PartMaterial)).IsFalse();
    }

    // ── a house on stilts standing on a floor (HS10) ─────────────────────────────────────────────────

    private static HouseStyle OnStilts(TerrainMaterial plate) => new()
    {
        Foundation = new Foundation { Plate = RoomPart.Of(plate) },
        Wall = RoomPart.Of(new SolidMaterial(Blocks.Planks, 1), 5),
        Doorway = new Doorway { Width = 2, Height = 3, Head = new DoorHeadStyle { Form = DoorHeadForm.None } },
        Storeys =
        [
            // The open storey: air for the whole of the doorway's courses, closed by a laid log.
            new Storey { Clear = 5, Wall = new RoomPart(new BandStack(
                [new Band(new SolidMaterial(Blocks.Air), 5), new Band(new LaidLogMaterial(Blocks.Log, 0), 1)]), 5) },
            new Storey { Clear = 4, Wall = RoomPart.Of(new SolidMaterial(Blocks.Planks, 1), 4) },
        ],
    };

    /// <summary>The corpus fault: `opus5-scarrow-delph`'s smithy stands its open storey on a course of oak
    /// planks, so the mire it was raised over is floored across the whole footprint.</summary>
    [Test]
    public async Task A_stilt_house_standing_on_a_plate_is_HS10()
    {
        var findings = HouseStyleValidation.Check(OnStilts(new SolidMaterial(Blocks.Planks)));

        var stilt = findings.Single(finding => finding.Rule == HouseStyleRules.StiltFloor);
        await Assert.That(stilt.Severity).IsEqualTo(Severity.Complaint);
        await Assert.That(stilt.Field).IsEqualTo("foundation.plate");
    }

    /// <summary>And a plate of air says nothing, which is the answer the finding points at: the terrain runs
    /// on under the building.</summary>
    [Test]
    public async Task A_stilt_house_on_a_plate_of_air_says_nothing()
    {
        var findings = HouseStyleValidation.Check(OnStilts(new SolidMaterial(Blocks.Air)));
        await Assert.That(findings.Any(finding => finding.Rule == HouseStyleRules.StiltFloor)).IsFalse();
    }

    /// <summary>A plate of air is the stilt house's ground, and a storey that names no deck of its own stands on
    /// the plate's top course — so every shipped style raised on air gives each storey above it a deck, or the room
    /// over the stilts has no floor.</summary>
    [Test]
    public async Task Every_shipped_style_on_a_plate_of_air_floors_the_storeys_above_it()
    {
        var shipped = SeedFolder.Houses;
        var floorless = shipped
            .Where(entry => entry.Style.Foundation.Deck.IsAir())
            .SelectMany(entry => entry.Style.Levels.Skip(1)
                .Select((level, at) => (entry.Name, Storey: at + 1, level.Deck)))
            .Where(entry => entry.Deck.IsAir())
            .Select(entry => $"{entry.Name} storey {entry.Storey}")
            .ToList();
        await Assert.That(floorless).IsEmpty();
    }

    /// <summary>A building with walls on the ground is not on stilts, so its floor is a floor.</summary>
    [Test]
    public async Task A_walled_ground_storey_on_a_plate_is_not_HS10()
    {
        var walled = OnStilts(new SolidMaterial(Blocks.Planks)) with
        {
            Storeys = [new Storey { Clear = 5, Wall = RoomPart.Of(new SolidMaterial(Blocks.Planks, 1), 5) },
                       new Storey { Clear = 4, Wall = RoomPart.Of(new SolidMaterial(Blocks.Planks, 1), 4) }],
        };
        var findings = HouseStyleValidation.Check(walled);
        await Assert.That(findings.Any(finding => finding.Rule == HouseStyleRules.StiltFloor)).IsFalse();
    }

    // ── beam ends and the posts beside them (HS11) ────────────────────────────────────────────────

    private static readonly TerrainMaterial SpruceLog = new SolidMaterial(Blocks.Log, 1);
    private static readonly TerrainMaterial LaidSpruce = new LaidLogMaterial(Blocks.Log, 1);

    /// <summary>The author's ruling: corner beams require log pillars and a laid log. Beam ends over a laid seam
    /// whose corners are stone brick have no upright to dock against.</summary>
    [Test]
    public async Task Beam_ends_beside_corners_that_are_not_log_are_HS11()
    {
        var stone = Framed(Masonry, postWood: 1, beamWood: 1, laidWood: 1) with { Post = Masonry };

        var posts = HouseStyleValidation.Check(stone).Single(finding => finding.Rule == HouseStyleRules.BeamsWithoutPosts);
        await Assert.That(posts.Severity).IsEqualTo(Severity.Complaint);
        await Assert.That(posts.Field).IsEqualTo("storeys[0].post");
        await Assert.That(posts.Message).Contains("stone brick");
    }

    /// <summary>Corners that are wall like the rest of it are no posts at all, and are asked the same.</summary>
    [Test]
    public async Task Beam_ends_on_a_building_with_no_posts_are_HS11()
    {
        var bare = Framed(Masonry, postWood: 1, beamWood: 1, laidWood: 1) with { Post = null };
        var findings = HouseStyleValidation.Check(bare);
        await Assert.That(findings.Any(finding => finding.Rule == HouseStyleRules.BeamsWithoutPosts)).IsTrue();
    }

    /// <summary>The author's croft: log pillars in a stone wall with no beams and no laid log anywhere is fine.
    /// </summary>
    [Test]
    public async Task Log_posts_without_beam_ends_are_not_asked()
    {
        var croft = Framed(Masonry, postWood: 1, beamWood: 1) with { Beams = new BeamStyle() };
        var findings = HouseStyleValidation.Check(croft);
        await Assert.That(findings.Any(finding => finding.Rule == HouseStyleRules.BeamsWithoutPosts
                                               || finding.Rule == HouseStyleRules.BeamsWithoutTimber)).IsFalse();
    }

    /// <summary>The whole frame says nothing: log posts, beam ends of the same wood, and a laid log at their seam.
    /// </summary>
    [Test]
    public async Task Beam_ends_with_log_posts_and_a_laid_seam_say_nothing()
    {
        var findings = HouseStyleValidation.Check(Framed(Masonry, postWood: 1, beamWood: 1, laidWood: 1));
        await Assert.That(findings.Any(finding => finding.Rule == HouseStyleRules.BeamsWithoutPosts)).IsFalse();
    }

    // ── a gable in the verge's own block (HS12) ────────────────────────────────────────────────────

    private static readonly TerrainMaterial DarkOakPlanks = new SolidMaterial(Blocks.Planks, 5);
    private static readonly TerrainMaterial SprucePlanks = new SolidMaterial(Blocks.Planks, 1);

    private static HouseStyle Gabled(RoofForm form, TerrainMaterial verge, TerrainMaterial? gable) => new()
    {
        Wall = RoomPart.Of(Masonry, 5),
        Post = null,
        Roof = new RoofStyle { Form = form, Body = SprucePlanks, Verge = verge, Gable = gable },
    };

    /// <summary>A gable laid in dark oak under a dark oak overhang, so the face and the roof's edge are one
    /// block.</summary>
    [Test]
    public async Task A_gable_in_the_verges_own_block_is_HS12()
    {
        var findings = HouseStyleValidation.Check(Gabled(RoofForm.Gable, DarkOakPlanks, DarkOakPlanks));

        var gable = findings.Single(finding => finding.Rule == HouseStyleRules.GableAsVerge);
        await Assert.That(gable.Severity).IsEqualTo(Severity.Complaint);
        await Assert.That(gable.Field).IsEqualTo("roof.gable");
        await Assert.That(gable.Message).Contains("dark oak");
    }

    /// <summary>The fix the author gave: spruce planks under the dark oak overhang.</summary>
    [Test]
    public async Task A_spruce_gable_under_a_dark_oak_verge_says_nothing()
    {
        var findings = HouseStyleValidation.Check(Gabled(RoofForm.Gable, DarkOakPlanks, SprucePlanks));
        await Assert.That(findings.Any(finding => finding.Rule == HouseStyleRules.GableAsVerge)).IsFalse();
    }

    /// <summary>With no gable named the face is the wall's top course carried up, and that is the block the
    /// verge is compared with.</summary>
    [Test]
    public async Task An_unnamed_gable_is_the_walls_top_course()
    {
        var style = Gabled(RoofForm.Saltbox, DarkOakPlanks, gable: null) with { Wall = RoomPart.Of(DarkOakPlanks, 5) };

        var gable = HouseStyleValidation.Check(style).Single(finding => finding.Rule == HouseStyleRules.GableAsVerge);
        await Assert.That(gable.Field).IsEqualTo("roof.verge");
    }

    /// <summary>A hip and a flat lid leave no triangle of wall, so the same pair says nothing there.</summary>
    [Test]
    [Arguments(RoofForm.Hip)]
    [Arguments(RoofForm.Flat)]
    public async Task A_roof_that_leaves_no_gable_is_not_asked(RoofForm form)
    {
        var findings = HouseStyleValidation.Check(Gabled(form, DarkOakPlanks, DarkOakPlanks));
        await Assert.That(findings.Any(finding => finding.Rule == HouseStyleRules.GableAsVerge)).IsFalse();
    }

    // ── a laid log at the foot of the wall (HS13) ──────────────────────────────────────────────────

    private static HouseStyle Footed(params Band[] bands) => new()
    {
        Wall = new RoomPart(new BandStack(bands), 5),
        Post = SpruceLog,
    };

    /// <summary>A wall started on a course of laid spruce, so a log is the building's foot.</summary>
    [Test]
    public async Task A_laid_log_as_the_bottom_course_is_HS13()
    {
        var findings = HouseStyleValidation.Check(Footed(new Band(LaidSpruce, 1), new Band(SprucePlanks, 4)));

        var foot = findings.Single(finding => finding.Rule == HouseStyleRules.LogAtTheFoot);
        await Assert.That(foot.Severity).IsEqualTo(Severity.Complaint);
        await Assert.That(foot.Field).IsEqualTo("wall");
        await Assert.That(foot.Message).Contains("spruce");
    }

    /// <summary>The same log as the storey's top course is the plate its posts carry.</summary>
    [Test]
    public async Task A_laid_log_at_the_top_of_the_wall_is_not_HS13()
    {
        var findings = HouseStyleValidation.Check(Footed(new Band(SprucePlanks, 4), new Band(LaidSpruce, 1)));
        await Assert.That(findings.Any(finding => finding.Rule == HouseStyleRules.LogAtTheFoot)).IsFalse();
    }

    /// <summary>Only the building's foot is asked: a laid course opening an upper storey sits on the storey
    /// below rather than on the ground.</summary>
    [Test]
    public async Task A_laid_log_opening_an_upper_storey_is_not_HS13()
    {
        var style = new HouseStyle
        {
            Post = SpruceLog,
            Storeys =
            [
                new Storey { Clear = 4, Wall = new RoomPart(new BandStack(
                    [new Band(Masonry, 4), new Band(LaidSpruce, 1)]), 5) },
                new Storey { Clear = 3, Wall = new RoomPart(new BandStack(
                    [new Band(LaidSpruce, 1), new Band(SprucePlanks, 2)]), 3) },
            ],
        };
        var findings = HouseStyleValidation.Check(style);
        await Assert.That(findings.Any(finding => finding.Rule == HouseStyleRules.LogAtTheFoot)).IsFalse();
    }

    // ── a footing (HS7) ──────────────────────────────────────────────────────────────────────────────

    private static HouseStyle Founded(int plateDepth, TerrainMaterial? footing) => SeededHouses.Alpine with
    {
        Foundation = new Foundation { Plate = RoomPart.Of(SprucePlanks, plateDepth), Footing = footing },
    };

    /// <summary>The author's ruling, on every house: a footing reads as a rim round the building whatever depth
    /// of plate it rings, so a deep plate does not earn one.</summary>
    [Test]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(3)]
    public async Task A_footing_round_a_plate_of_any_depth_is_HS7(int plateDepth)
    {
        var footing = HouseStyleValidation.Check(Founded(plateDepth, new SolidMaterial(Blocks.Cobblestone)))
            .Single(finding => finding.Rule == HouseStyleRules.Footing);
        await Assert.That(footing.Severity).IsEqualTo(Severity.Complaint);
        await Assert.That(footing.Field).IsEqualTo("foundation.footing");
    }

    [Test]
    public async Task A_plate_with_no_footing_is_not_HS7()
    {
        var findings = HouseStyleValidation.Check(Founded(2, footing: null));
        await Assert.That(findings.Any(finding => finding.Rule == HouseStyleRules.Footing)).IsFalse();
    }

    // ── a shed, on the house, its porch or one wing (HS14) ─────────────────────────────────────────

    [Test]
    public async Task A_shed_roof_is_HS14()
    {
        var style = SeededHouses.Alpine with
        {
            Roof = SeededHouses.Alpine.Roof with { Form = RoofForm.Shed },
        };
        var shed = HouseStyleValidation.Check(style).Single(finding => finding.Rule == HouseStyleRules.ShedRoof);
        await Assert.That(shed.Severity).IsEqualTo(Severity.Complaint);
        await Assert.That(shed.Field).IsEqualTo("roofForm");
    }

    /// <summary>A porch that names no roof wears a gable, so asking for a porch and saying nothing more is
    /// never the lean-to the rule refuses; a canopy that names a shed is refused under the porch.</summary>
    [Test]
    public async Task A_porch_naming_no_roof_wears_a_gable_and_a_shed_canopy_is_HS14()
    {
        var porched = SeededHouses.Alpine with { Porch = new PorchStyle { Depth = 2 } };
        await Assert.That(porched.Porch!.Roof).IsEqualTo(RoofForm.Gable);
        await Assert.That(HouseStyleValidation.Check(porched).Any(finding => finding.Rule == HouseStyleRules.ShedRoof))
            .IsFalse();

        var leaning = porched with { Porch = porched.Porch with { Roof = RoofForm.Shed } };
        var shed = HouseStyleValidation.Check(leaning).Single(finding => finding.Rule == HouseStyleRules.ShedRoof);
        await Assert.That(shed.Field).IsEqualTo("porch.roof");
    }

    [Test]
    [Arguments(RoofForm.Gable)]
    [Arguments(RoofForm.Flat)]
    [Arguments(RoofForm.Hip)]
    [Arguments(RoofForm.Gambrel)]
    [Arguments(RoofForm.Saltbox)]
    public async Task Every_form_but_the_shed_is_a_roof_a_style_may_wear(RoofForm form)
        => await Assert.That(HouseStyleValidation.CheckRoofForm(form, "roofForm")).IsEmpty();

    // ── a checker in the posts' own log (HS15) ─────────────────────────────────────────────────────

    /// <summary>A wall checkered in spruce log beside spruce log posts, so the corner and the panel read as one
    /// mass.</summary>
    [Test]
    public async Task A_wall_checkered_in_its_posts_own_log_is_HS15()
    {
        var style = new HouseStyle
        {
            Post = SpruceLog,
            Wall = RoomPart.Of(new CheckerMaterial(1, SpruceLog, SprucePlanks), 5),
        };
        var checker = HouseStyleValidation.Check(style)
            .Single(finding => finding.Rule == HouseStyleRules.CheckerInPostWood);
        await Assert.That(checker.Severity).IsEqualTo(Severity.Complaint);
        await Assert.That(checker.Field).IsEqualTo("wall");
        await Assert.That(checker.Message).Contains("spruce");
    }

    /// <summary>A log checker is one log in two orientations, so it is the posts' own wood or another one; a
    /// different log is what the author asks the panel to be.</summary>
    [Test]
    [Arguments(3, true)]     // jungle squares beside jungle posts
    [Arguments(1, false)]    // spruce squares beside jungle posts
    public async Task A_log_checker_is_HS15_only_in_the_posts_own_wood(int squareWood, bool refused)
    {
        var style = new HouseStyle
        {
            Post = new SolidMaterial(Blocks.Log, 3),
            Storeys = [new Storey { Clear = 4, Wall = new RoomPart(new BandStack(
                [new Band(Masonry, 2), new Band(new LogCheckerMaterial(1, Blocks.Log, squareWood), 3)]), 5) }],
        };
        var findings = HouseStyleValidation.Check(style);
        await Assert.That(findings.Any(finding => finding.Rule == HouseStyleRules.CheckerInPostWood)).IsEqualTo(refused);
    }

    /// <summary>A course laid in the posts' own log is the frame, not a checker, and is what HS9 asks for.</summary>
    [Test]
    public async Task A_laid_course_of_the_posts_wood_is_not_HS15()
    {
        var findings = HouseStyleValidation.Check(Footed(new Band(SprucePlanks, 4), new Band(LaidSpruce, 1)));
        await Assert.That(findings.Any(finding => finding.Rule == HouseStyleRules.CheckerInPostWood)).IsFalse();
    }

    // ── a wall in a block that surfaces ground (HS16) ──────────────────────────────────────────────

    [Test]
    [Arguments(2, 0)]       // grass
    [Arguments(3, 2)]       // podzol
    [Arguments(110, 0)]     // mycelium
    [Arguments(60, 0)]      // farmland
    public async Task A_wall_laid_in_a_block_that_surfaces_ground_is_HS16(int id, int data)
    {
        var findings = HouseStyleValidation.Check(Footed(new Band(Masonry, 2), new Band(new SolidMaterial(id, data), 3)));
        var turf = findings.Single(finding => finding.Rule == HouseStyleRules.SurfacingWall);
        await Assert.That(turf.Severity).IsEqualTo(Severity.Complaint);
        await Assert.That(turf.Field).IsEqualTo("wall");
    }

    /// <summary>The author's narrowing: sand, gravel and dirt can each work in a wall, so only the four skins
    /// are refused.</summary>
    [Test]
    [Arguments(12, 0)]      // sand
    [Arguments(13, 0)]      // gravel
    [Arguments(3, 0)]       // dirt
    [Arguments(3, 1)]       // coarse dirt
    public async Task Sand_gravel_and_dirt_are_a_walls_to_use(int id, int data)
    {
        var findings = HouseStyleValidation.Check(Footed(new Band(Masonry, 2), new Band(new SolidMaterial(id, data), 3)));
        await Assert.That(findings.Any(finding => finding.Rule == HouseStyleRules.SurfacingWall)).IsFalse();
    }

    /// <summary>The walk reaches every block a wall lays, a drawn pattern's stripes included, and the gable.</summary>
    [Test]
    public async Task A_stripe_of_mycelium_or_a_grass_gable_is_HS16()
    {
        var striped = Footed(new Band(Masonry, 2), new Band(new WallRunMaterial(
            [new WallStripe(SprucePlanks, 2), new WallStripe(new SolidMaterial(Blocks.Mycelium), 1)]), 3));
        await Assert.That(HouseStyleValidation.Check(striped).Single(finding => finding.Rule == HouseStyleRules.SurfacingWall).Field)
            .IsEqualTo("wall");

        var gabled = Footed(new Band(Masonry, 5)) with
        {
            Roof = new RoofStyle { Gable = new SolidMaterial(Blocks.Grass) },
        };
        await Assert.That(HouseStyleValidation.Check(gabled).Single(finding => finding.Rule == HouseStyleRules.SurfacingWall).Field)
            .IsEqualTo("gable");
    }

    // ── snow and ice (HS17) ────────────────────────────────────────────────────────────────────────

    [Test]
    [Arguments(78)]         // snow layer
    [Arguments(79)]         // ice
    [Arguments(80)]         // snow block
    [Arguments(174)]        // packed ice
    public async Task Snow_or_ice_in_a_wall_is_HS17(int id)
    {
        var findings = HouseStyleValidation.Check(Footed(new Band(Masonry, 2), new Band(new SolidMaterial(id), 3)));
        var frozen = findings.Single(finding => finding.Rule == HouseStyleRules.SnowAndIce);
        await Assert.That(frozen.Severity).IsEqualTo(Severity.Complaint);
        await Assert.That(frozen.Field).IsEqualTo("wall");
    }

    /// <summary>A roof laid in snow, where the white is all anybody sees.</summary>
    [Test]
    public async Task A_snow_roof_is_HS17()
    {
        var style = Footed(new Band(Masonry, 5)) with { Roof = new RoofStyle { Body = new SolidMaterial(80) } };
        await Assert.That(HouseStyleValidation.Check(style).Single(finding => finding.Rule == HouseStyleRules.SnowAndIce).Field)
            .IsEqualTo("roof");
    }

    /// <summary>And white is not the fault: the author's narrowing keeps white clay, white wool and quartz.</summary>
    [Test]
    [Arguments(159, 0)]
    [Arguments(35, 0)]
    [Arguments(155, 0)]
    public async Task A_white_wall_is_not_HS17(int id, int data)
    {
        var findings = HouseStyleValidation.Check(Footed(new Band(Masonry, 2), new Band(new SolidMaterial(id, data), 3)));
        await Assert.That(findings.Any(finding => finding.Rule == HouseStyleRules.SnowAndIce)).IsFalse();
    }

    // ── a storey standing on air (HS18) ────────────────────────────────────────────────────────────

    /// <summary>The case found in review: a stilt house's plate is air, the storey over the stilts names no
    /// deck and falls back to it, and the room has no floor.</summary>
    [Test]
    public async Task A_storey_over_a_plate_of_air_with_no_deck_is_HS18()
    {
        var floorless = HouseStyleValidation.Check(OnStilts(new SolidMaterial(Blocks.Air)))
            .Single(finding => finding.Rule == HouseStyleRules.FloorlessStorey);
        await Assert.That(floorless.Severity).IsEqualTo(Severity.Complaint);
        await Assert.That(floorless.Field).IsEqualTo("storeys[1].deck");
    }

    /// <summary>A deck of its own floors the storey, and so does a surface whose field covers the room.</summary>
    [Test]
    public async Task A_deck_or_a_floor_field_floors_the_storey_over_the_stilts()
    {
        var stilts = OnStilts(new SolidMaterial(Blocks.Air));
        var decked = stilts with { Storeys = [stilts.Storeys[0], stilts.Storeys[1] with { Deck = SprucePlanks }] };
        var fielded = stilts with
        {
            Storeys = [stilts.Storeys[0], stilts.Storeys[1] with { Surface = new FloorSurface { Field = SprucePlanks } }],
        };
        foreach (var style in new[] { decked, fielded })
            await Assert.That(HouseStyleValidation.Check(style).Any(finding => finding.Rule == HouseStyleRules.FloorlessStorey))
                .IsFalse();
    }

    // ── a porch canopy in its own material ─────────────────────────────────────────────────────────

    /// <summary>A canopy laid in its own material is a roof plane like the body and the verge: one block, never
    /// a pattern or a ground material (HS3), and never snow or ice (HS17).</summary>
    [Test]
    public async Task A_canopy_is_held_to_what_a_roof_is_held_to()
    {
        HouseStyle Canopied(TerrainMaterial canopy) => SeededHouses.Alpine with
        {
            Porch = new PorchStyle { Depth = 2, Canopy = canopy },
        };
        await Assert.That(HouseStyleValidation.Check(Canopied(new SolidMaterial(98))).Any(finding => finding.Field == "porch.canopy"))
            .IsFalse();

        var patterned = HouseStyleValidation.Check(Canopied(new CheckerMaterial(1, Masonry, SprucePlanks)));
        await Assert.That(patterned.Single(finding => finding.Field == "porch.canopy").Rule).IsEqualTo(HouseStyleRules.RoofMaterial);

        var snowed = HouseStyleValidation.Check(Canopied(new SolidMaterial(80)));
        await Assert.That(snowed.Single(finding => finding.Field == "porch.canopy").Rule).IsEqualTo(HouseStyleRules.SnowAndIce);
    }
}
