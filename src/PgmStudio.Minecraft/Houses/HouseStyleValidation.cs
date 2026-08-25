using PgmStudio.Domain;
using PgmStudio.Minecraft.Painting;
using PgmStudio.Minecraft.Palette;
using PgmStudio.Vocabulary;

namespace PgmStudio.Minecraft.Houses;

/// <summary>The house-style rule ids <see cref="HouseStyleValidation"/> cites — stable names for what a finding
/// is about, the way <c>WX*</c> names a stamping rule and <c>PC-*</c> names a plan lint. Kept apart from any
/// task-tracking id: those are swept once shipped, and a rule an author or another tool reads back off a
/// refusal has to keep meaning the same thing after the task that added it is long gone from the board.</summary>
public static class HouseStyleRules
{
    /// <summary>A block named for a geometric role — which way a stair climbs, which half a slab fills — is not
    /// that kind of block: <c>doorHead.block</c>, its <c>fillBlock</c> under <c>upperSlab</c>, a window's
    /// <c>block</c> under <c>stairLattice</c>, <c>arched</c> or <c>slabBanded</c>, or <c>roofSlab</c>
    /// itself.</summary>
    /// <remarks>Name a block of the kind the field means: a stair id where a stair is asked for, a slab where a slab is. The finding names the field; <c>GET /api/house-parts</c> lists what each one accepts.</remarks>
    [Rule(RuleCategory.Malformed, RuleConcern.Style, RuleConcern.Material)]
    public const string BlockKind = "HS1";

    /// <summary>A doorway does not clear the least height a door may, once its head is written into the top
    /// course.</summary>
    /// <remarks>Raise the storey, or use a door head that does not eat a course. A doorway needs 3 clear blocks over 2 wide once its head is written into the top course.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Style, RuleConcern.Structure)]
    public const string DoorClearance = "HS2";

    /// <summary>A roof is not one material. Its body and its verge are each a single block — never a
    /// pattern, so nothing spreads a voronoi across a roof — and the half-course slab continues the body in
    /// the body's own material. A log or a ground material is not a roof material at all, and a slab named as
    /// the whole-block body with no half-course companion builds a roof with a gap in every course.</summary>
    /// <remarks>Give the roof one material and the verge one material. They may be the same — a brick body with a brick verge is a whole brick roof — or they may differ, which is how a dark oak verge trims a brick roof; what they may not be is a pattern, several blocks, or a log or a ground material. Set `roofSlab` to a slab of the body's own material, or leave it unset and let the body carry the whole rise. The gable is the end wall and follows the wall, not this rule.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Style, RuleConcern.Material)]
    public const string RoofMaterial = "HS3";

    /// <summary>A part built of two blocks is built of two materials — a door head whose stair and whose slab
    /// fill are cut from different stone, a window whose block and whose host disagree. The two blocks are
    /// one line of the building and read as one thing or as a mistake.</summary>
    /// <remarks>Cut both blocks from the same material: a sandstone stair takes a sandstone slab, a birch stair a birch one. It is the material that has to match and not the shape — a stair over a slab is the point of the pair.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Style, RuleConcern.Material)]
    public const string PartMaterial = "HS4";

    /// <summary>An ore is used as a building material. An ore is stone with something in it — it belongs in
    /// the ground a map is dug out of, and in a wall, a post or a beam it reads as a mistake rather than as a
    /// material.</summary>
    /// <remarks>Choose a building material. If the intent was the colour, the block it is embedded in is the one to name — stone for iron and coal, and the stained clay or wool nearest the tint for anything else.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Style, RuleConcern.Material)]
    public const string OreMaterial = "HS5";

    /// <summary>A door is cut through a wall that is not there. A storey whose wall is air over the doorway's
    /// own courses — a house on stilts, an open undercroft — has nothing to cut, so a doorway and its head are
    /// a lintel standing in mid-air.</summary>
    /// <remarks>Take the doorway off the storey, or give the storey a wall to cut it through. A stilt house is entered from the storey above it, so the door belongs there.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Style, RuleConcern.World)]
    public const string DoorWithoutWall = "HS6";

    /// <summary>A footing round a plate one course deep. The footing is what a foundation stands on, so over
    /// a plate that is a single course it is a one-block rim round a building with no foundation under it —
    /// noise at every wall, on every house, and the commonest thing wrong with how a village reads.</summary>
    /// <remarks>Drop the footing, or give the plate the depth that earns one: two or three courses, which is the foundation a footing is the foot of. It is a complaint rather than a refusal because the building stands either way — what it costs is how the building looks.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Style, RuleConcern.World)]
    public const string ShallowFooting = "HS7";
}

/// <summary>
/// The gate a <see cref="HouseStyle"/> is checked against before it is stored — beside the style rather than in
/// a driver, so a bad one is refused when it is posted instead of silently built.
///
/// <para><see cref="Check"/> is safe to run on any house, decorative or objective, whatever it is built for:
/// every fault it names is a property of the style's own geometry, never of what the building is <em>for</em>.
/// None of the ten shipped presets in <see cref="HousePresets"/> trips it, including
/// <see cref="HousePresets.Alpine"/> and <see cref="HousePresets.Workshop"/>, whose stair-lattice and
/// slab-banded windows are built correctly — a window built from either form is allowed on any house, spawns
/// included, so long as its block is the kind the form needs (<see cref="HouseStyleRules.BlockKind"/>); neither
/// form is a defect to be refused on its own.</para>
/// </summary>
public static class HouseStyleValidation
{
    private const decimal LeastDoorClearance = 2.5m;

    /// <summary>Every fault in <paramref name="style"/> its own geometry can name: a block used for a
    /// geometric role that is not the kind that role needs, a doorway that does not clear the least height a
    /// door may, and a roof whose own materials are wrong for its pitch or its family.</summary>
    public static Findings Check(HouseStyle style)
    {
        var findings = new List<Finding>();
        CheckDoorHead(style.Doorway.Head, findings);
        CheckWindow("windows", style.Windows, findings);
        CheckWindow("gableWindows", style.Roof.GableWindows, findings);
        for (var at = 0; at < style.Storeys.Count; at++)
            if (style.Storeys[at].Windows is { } storeyWindows)
                CheckWindow($"storeys[{at}].windows", storeyWindows, findings);
        findings.AddRange(CheckRoof(style.Roof));
        CheckDoorClearance(style.Doorway, findings);
        CheckBeams(style.Beams, findings);
        CheckPartMaterials(style, findings);
        CheckOres(style, findings);
        CheckDoorHasWall(style, findings);
        CheckFooting(style.Foundation, findings);
        return findings;
    }

    // ── a block named for a geometric role, never checked to be that kind of block ────────────────────────

    private static void CheckDoorHead(DoorHeadStyle head, List<Finding> findings)
    {
        if (head.Form == DoorHeadForm.None) return;

        if (!BlockFamilies.IsStair(head.Block))
            findings.Add(new Finding(HouseStyleRules.BlockKind,
                $"doorHead.block ({head.Block}) is not a stair. An arched head turns its two corners by a " +
                "stair's own facing; anything else lays a solid lintel across the doorway instead of an arch.",
                Field: "doorHead.block"));

        if (head.Fill == DoorHeadFill.UpperSlab && !BlockFamilies.IsSlab(head.FillBlock))
            findings.Add(new Finding(HouseStyleRules.BlockKind,
                $"doorHead.fillBlock ({head.FillBlock}) under upperSlab is not a single slab. The fill raises " +
                "half of its own cube to read as one line with the corners; a block without a half — a double " +
                "slab included — reads as a full cube instead.",
                Field: "doorHead.fillBlock"));
    }

    // ── a beam is a log, and a part built of two blocks is built of one material ──────────────────────────

    /// <summary>The beams that run past a building's corners are the ends of its floor timbers, which is what
    /// <see cref="BeamStyle.Block"/> has always been — the docstring says "the log the ends are cut from".
    /// Anything else is a beam that is not one.</summary>
    private static void CheckBeams(BeamStyle beams, List<Finding> findings)
    {
        if (beams.Block >= 0 && !BlockFamilies.IsLog(beams.Block))
            findings.Add(new Finding(HouseStyleRules.BlockKind,
                $"beams.block ({beams.Block}) is not a log. A beam is the end of a floor timber and docks "
                + "against the posts; a log is what one is cut from.",
                Field: "beams.block"));
    }

    /// <summary>Every pair of blocks that has to read as one line: a door head's stair and the slab that fills
    /// it, a window's block and the block it is seated in. Each pair is checked only where both halves are
    /// actually named — a window with no host names none.</summary>
    private static void CheckPartMaterials(HouseStyle style, List<Finding> findings)
    {
        var head = style.Doorway.Head;
        if (head.Form != DoorHeadForm.None && head.Fill == DoorHeadFill.UpperSlab
            && !BlockMaterials.Same(head.Block, 0, head.FillBlock, head.FillData))
            findings.Add(new Finding(HouseStyleRules.PartMaterial,
                $"doorHead.block is {BlockMaterials.Of(head.Block, 0)} and its fill is "
                + $"{BlockMaterials.Of(head.FillBlock, head.FillData)}. The two corners and the line between "
                + "them are one head, so they are cut from one material.",
                Field: "doorHead.fillBlock"));

        void Window(string where, WindowStyle? window)
        {
            if (window is not { } win || win.Form == WindowForm.None || win.HostBlock < 0) return;
            if (!BlockMaterials.Same(win.Block, win.Data, win.HostBlock, win.HostData))
                findings.Add(new Finding(HouseStyleRules.PartMaterial,
                    $"{where}.block is {BlockMaterials.Of(win.Block, win.Data)} and the host it is seated in "
                    + $"is {BlockMaterials.Of(win.HostBlock, win.HostData)}. A window and its host are one "
                    + "opening, so they are cut from one material.",
                    Field: $"{where}.hostBlock"));
        }
        Window("windows", style.Windows);
        Window("gableWindows", style.Roof.GableWindows);
        for (var at = 0; at < style.Storeys.Count; at++)
            Window($"storeys[{at}].windows", style.Storeys[at].Windows);
    }

    /// <summary>Every ore named anywhere in the style. Walked over the materials rather than checked field by
    /// field, because an ore is wrong in all of them and a list of the places it has been found is a list that
    /// grows.</summary>
    private static void CheckOres(HouseStyle style, List<Finding> findings)
    {
        foreach (var (field, material) in Materials(style))
            foreach (var id in Blocks(material))
                if (BlockFamilies.IsOre(id))
                {
                    findings.Add(new Finding(HouseStyleRules.OreMaterial,
                        $"{field} names {BlockPalette.Name(id, 0)}, which is an ore. An ore is stone with "
                        + "something in it and is not a building material.",
                        Field: field));
                    break;
                }
    }

    /// <summary>A door <b>head</b> written over a storey whose wall is air across the doorway's own courses.
    /// The head is what the rule is about and not the doorway: an opening cut in an open storey is nothing at
    /// all, while an arch and its lintel stand in mid-air over the stilts. Asked of the ground storey, which
    /// is the one a door is cut through, and only where the house states storeys of its own — a house whose
    /// wall is the fallback has a wall.</summary>
    private static void CheckDoorHasWall(HouseStyle style, List<Finding> findings)
    {
        if (style.Doorway.Head.Form == DoorHeadForm.None) return;
        if (style.Storeys.Count == 0 || style.Storeys[0].Wall is not { } wall) return;

        var courses = Math.Max(1, style.Doorway.Height);
        for (var course = 0; course < courses; course++)
            if (wall.At(course).Material is not SolidMaterial { Id: 0 }) return;

        findings.Add(new Finding(HouseStyleRules.DoorWithoutWall,
            $"the ground storey's wall is air over all {courses} of the doorway's courses, so there is no "
            + "wall to carry a door head — the arch and its lintel stand in mid-air.",
            Field: "doorHead.form"));
    }

    /// <summary>A footing on a plate too shallow to have a foot. A complaint: the building stands either
    /// way, and what a one-block rim costs is how it reads.</summary>
    private static void CheckFooting(Foundation foundation, List<Finding> findings)
    {
        if (foundation.Footing is null || foundation.Depth >= 2) return;
        findings.Add(new Finding(HouseStyleRules.ShallowFooting,
            "the foundation rings a plate one course deep with a footing, which is a one-block rim round a "
            + "building with no foundation under it. Drop the footing, or give the plate two or three courses.",
            Severity.Complaint, Field: "foundation.footing"));
    }

    /// <summary>Every material a style names, with the field it was named in — what a check that is about the
    /// material rather than about the role reads.</summary>
    private static IEnumerable<(string Field, TerrainMaterial Material)> Materials(HouseStyle style)
    {
        yield return ("roof", style.Roof.Body);
        yield return ("verge", style.Roof.Verge);
        yield return ("gable", style.Roof.Gable);
        yield return ("wall", style.Wall.At(0).Material);
        yield return ("foundation.plate", style.Foundation.Plate.At(0).Material);
        if (style.Foundation.Footing is { } footing) yield return ("foundation.footing", footing);
        if (style.Post is { } post) yield return ("post", post);
        for (var at = 0; at < style.Storeys.Count; at++)
        {
            if (style.Storeys[at].Wall is { } wall) yield return ($"storeys[{at}].wall", wall.At(0).Material);
            if (style.Storeys[at].Post is { } storeyPost) yield return ($"storeys[{at}].post", storeyPost);
        }
        if (style.Beams.Block >= 0) yield return ("beams.block", new SolidMaterial(style.Beams.Block, style.Beams.Data));
    }

    /// <summary>Every block id a material can resolve to, a pattern walked to its leaves.</summary>
    private static IEnumerable<int> Blocks(TerrainMaterial material) => material switch
    {
        SolidMaterial solid => [solid.Id],
        LayeredMaterial layered => layered.Stack.Bands.SelectMany(band => Blocks(band.Material)),
        VoronoiMaterial voronoi => voronoi.Bands.SelectMany(band => Blocks(band.Material)),
        _ => [],
    };

    /// <summary>Whether <paramref name="windows"/>'s <see cref="WindowStyle.Block"/> is the kind its
    /// <see cref="WindowStyle.Form"/> needs, standalone — what a storey style checks itself with, off the same
    /// <see cref="WindowStyle"/> a house window is, before the storey has a house around it to compose
    /// into.</summary>
    public static Findings CheckWindow(string field, WindowStyle windows)
    {
        var findings = new List<Finding>();
        CheckWindow(field, windows, findings);
        return findings;
    }

    private static void CheckWindow(string field, WindowStyle windows, List<Finding> findings)
    {
        switch (windows.Form)
        {
            case WindowForm.StairLattice or WindowForm.Arched when !BlockFamilies.IsStair(windows.Block):
                findings.Add(new Finding(HouseStyleRules.BlockKind,
                    $"{field}.block ({windows.Block}) is not a stair. {FormName(windows.Form)} turns its " +
                    "corners by a stair's own facing; anything else builds without the diamond or the rounded " +
                    "corners the form is named for.",
                Field: $"{field}.block"));
                break;
            case WindowForm.SlabBanded when !BlockFamilies.IsSlab(windows.Block):
                findings.Add(new Finding(HouseStyleRules.BlockKind,
                    $"{field}.block ({windows.Block}) is not a single slab. A slab band raises half a cube for " +
                    "the sill and lowers half for the lintel; anything else — a double slab included — leaves " +
                    "no half-block of clear air above the sill or below the lintel.",
                Field: $"{field}.block"));
                break;
        }
    }

    private static string FormName(WindowForm form) => form switch
    {
        WindowForm.StairLattice => "a stair lattice",
        WindowForm.Arched => "an arched window",
        _ => form.ToString(),
    };

    // ── a roof's own materials ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Every fault a roof's own materials can name: a slab block that is not a slab, a slab named as
    /// a whole-block roof, and a log or a ground material standing in for the roof or the verge.
    ///
    /// <para>Asked of the <see cref="RoofStyle"/> rather than of a whole house, because a roof <em>part</em>
    /// in the library is one of these and nothing else — it carries its own form, its pitch and its own
    /// half-course slab, which is what lets the slab/pitch pairing run there as well as on a house. The whole
    /// gate rather than half of it, since a roof saved on its own is stamped exactly as a roof bound onto a
    /// house is.</para></summary>
    public static Findings CheckRoof(RoofStyle roof)
    {
        var findings = new List<Finding>();
        if (roof.Slab >= 0 && !BlockFamilies.IsSlab(roof.Slab))
            findings.Add(new Finding(HouseStyleRules.BlockKind,
                $"roofSlab ({roof.Slab}) is not a single slab. A half-course roof steps in the slab's own " +
                "half on every odd course; anything else — a double slab included — comes out a full cube and " +
                "the slope stops climbing by halves.",
                Field: "roofSlab"));

        // A slab belongs in a roof only on a half-course rise (RoofSlab set). Naming one in Roof itself while
        // RoofSlab is unset asks for a whole block of rise in a material that only fills half its cube, which
        // is the see-through roof HouseStyle.Roof's own docstring warns about.
        if (roof.Slab < 0 && SolidId(roof.Body) is { } roofId && BlockFamilies.IsSlab(roofId))
            findings.Add(new Finding(HouseStyleRules.RoofMaterial,
                $"roof ({roofId}) is a slab and roofSlab is unset (-1). A course of slabs at a whole block of " +
                "rise leaves an open half between every pair and the roof reads see-through — set roofSlab to " +
                "a real slab and let roof carry the whole-block half, or choose a whole block for roof.",
                Field: "roof"));

        foreach (var (field, material) in new[] { ("roof", roof.Body), ("verge", roof.Verge) })
        {
            // A roof is read from below and from a distance, and both halves of it are one plane each: a
            // pattern there is several blocks in one surface, which is the fault rather than a style.
            if (material is not SolidMaterial solid)
            {
                findings.Add(new Finding(HouseStyleRules.RoofMaterial,
                    $"{field} is a {Patterned(material)} rather than one block. A roof's body and its verge " +
                    "are each a single material — name the block itself.", Field: field));
                continue;
            }
            var id = solid.Id;
            if (BlockFamilies.IsLog(id))
                findings.Add(new Finding(HouseStyleRules.RoofMaterial,
                    $"{field} ({id}) is a log. A log is never a roof or a verge material.", Field: field));
            else if (BlockFamilies.IsSoil(id))
                findings.Add(new Finding(HouseStyleRules.RoofMaterial,
                    $"{field} ({id}) is a ground material. A ground material — what a building stands on — is " +
                    "never a roof or a verge material.", Field: field));
        }

        // The half-course slab is the body continuing by halves, so it is the body's own material. A slab of
        // something else makes the roof two materials in alternating courses, which reads as neither.
        if (roof.Slab >= 0 && roof.Body is SolidMaterial body
            && !BlockMaterials.Same(body.Id, body.Data, roof.Slab, roof.SlabData))
            findings.Add(new Finding(HouseStyleRules.RoofMaterial,
                $"roofSlab is {BlockMaterials.Of(roof.Slab, roof.SlabData)} and the roof it steps in halves " +
                $"is {BlockMaterials.Of(body.Id, body.Data)}. The half-course slab continues the body, so it " +
                "is the body's own material.",
                Field: "roofSlab"));
        return findings;
    }

    /// <summary>The block id a material resolves to when it is a bare <see cref="SolidMaterial"/>, or null
    /// for anything patterned.</summary>
    private static int? SolidId(TerrainMaterial material) => material is SolidMaterial solid ? solid.Id : null;

    /// <summary>What a non-solid material is, in the word its own <c>kind</c> uses — so a finding can say
    /// which pattern was found rather than only that one was.</summary>
    private static string Patterned(TerrainMaterial material) =>
        material.GetType().Name.Replace("Material", "").ToLowerInvariant() + " pattern";

    // ── a door's clear height ──────────────────────────────────────────────────────────────────────────────

    private static void CheckDoorClearance(Doorway doorway, List<Finding> findings)
    {
        var clear = doorway.Clearance;
        if (clear < LeastDoorClearance)
            findings.Add(new Finding(HouseStyleRules.DoorClearance,
                $"the doorway clears {clear:0.0} blocks once its head is written in; a door must clear at " +
                $"least {LeastDoorClearance:0.0}.",
                Field: "doorHeight"));
    }
}
