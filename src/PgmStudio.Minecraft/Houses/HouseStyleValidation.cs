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
