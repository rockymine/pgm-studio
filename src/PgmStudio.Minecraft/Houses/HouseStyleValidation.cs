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
    /// <summary>A house names a block that is not the stair, single slab or log its field takes.</summary>
    /// <remarks>Set the field the finding names to a block of the kind it takes: a stair, a single slab or a
    /// log.</remarks>
    [Rule(RuleCategory.Malformed, RuleConcern.Style, RuleConcern.Material)]
    public const string BlockKind = "HS1";

    /// <summary>A doorway clears less than 2.5 blocks once its door head is written into its top course.</summary>
    /// <remarks>Change the <c>doorway.height</c> of the house until the doorway clears at least 2.5 blocks once its
    /// door head is written in.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Style, RuleConcern.Structure)]
    public const string DoorClearance = "HS2";

    /// <summary>A roof or its verge is a pattern, a log not laid, or one of grass, dirt, sand, gravel, farmland and
    /// mycelium, or its slab or stair is not cut from the roof's material.</summary>
    /// <remarks>Set the <c>roof.body</c> and the <c>roof.verge</c> of the house each to one block or a
    /// <c>laidLog</c>, then change the <c>roof.slab</c> or the <c>roof.stair</c> to a block cut from the body's
    /// material.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Style, RuleConcern.Material)]
    public const string RoofMaterial = "HS3";

    /// <summary>A door head, a window with its host block, or a timber frame is cut from more than one
    /// material.</summary>
    /// <remarks>Either change the block the finding names to one cut from the material of the block it pairs with,
    /// or change the paired block to match it.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Style, RuleConcern.Material)]
    public const string PartMaterial = "HS4";

    /// <summary>A house is built of an ore.</summary>
    /// <remarks>Change the material the finding names to a block that is not an ore, such as stone, stained clay or
    /// wool.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Style, RuleConcern.Material)]
    public const string OreMaterial = "HS5";

    /// <summary>The ground storey of a house with a door head has no wall across the doorway's courses.</summary>
    /// <remarks>Either set the <c>doorway.head.form</c> of the house to <c>none</c>, or change the <c>wall</c> of
    /// the ground storey in <c>storeys</c> until it is not air across the doorway's height.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Style, RuleConcern.World)]
    public const string DoorWithoutWall = "HS6";

    /// <summary>A house has a footing.</summary>
    /// <remarks>Set the <c>foundation.footing</c> of the house to <c>null</c>.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Style, RuleConcern.World)]
    public const string Footing = "HS7";

    /// <summary>The wall of a house is less than its doorway height plus 2 plus the rise of its porch canopy, in
    /// blocks.</summary>
    /// <remarks>Either set the <c>porch</c> of the house to <c>null</c>, or change the <c>clear</c> of a storey in
    /// <c>storeys</c> until the wall has at least as many courses as the porch needs.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Style, RuleConcern.Structure)]
    public const string PorchHeadroom = "HS8";

    /// <summary>A house has beam ends, and the top course of a storey under them is not a laid log.</summary>
    /// <remarks>Either set the top course of the <c>wall</c> of the storey to a <c>laidLog</c> cut from the beams'
    /// wood, or set the <c>beams.block</c> of the house to <c>-1</c>.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Style, RuleConcern.Structure, RuleConcern.Material)]
    public const string BeamsWithoutTimber = "HS9";

    /// <summary>A house with a stilt storey stands on a plate that is not air.</summary>
    /// <remarks>Either set the <c>foundation.plate</c> of the house to air, or change the <c>wall</c> of the ground
    /// storey in <c>storeys</c> until it is not air across the doorway's height.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Style, RuleConcern.World, RuleConcern.Terrain)]
    public const string StiltFloor = "HS10";

    /// <summary>A house has beam ends, and the corners of a storey under them are not log posts.</summary>
    /// <remarks>Either set the <c>post</c> of the storey to a log of the beams' wood, or set the <c>beams.block</c>
    /// of the house to <c>-1</c>.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Style, RuleConcern.Structure, RuleConcern.Material)]
    public const string BeamsWithoutPosts = "HS11";

    /// <summary>A gable is laid in one block, and the verge of its roof is laid in the same block.</summary>
    /// <remarks>Either set the <c>roof.gable</c> of the house to a block other than the <c>roof.verge</c>, or set
    /// the <c>roof.verge</c> to a block other than the gable.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Style, RuleConcern.Material)]
    public const string GableAsVerge = "HS12";

    /// <summary>The ground storey of a house has a laid log as its first course.</summary>
    /// <remarks>Set the first course of the <c>wall</c> of the ground storey to a block that is not a laid
    /// log.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Style, RuleConcern.Structure, RuleConcern.Material)]
    public const string LogAtTheFoot = "HS13";

    /// <summary>The roof form of a house, a wing or a porch canopy is not one of flat, gable, hip, gambrel or
    /// saltbox.</summary>
    /// <remarks>Change the <c>roof.form</c> of the house, the <c>porch.roof</c> or the <c>spec.form</c> of a wing
    /// to one of flat, gable, hip, gambrel or saltbox.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Style, RuleConcern.Structure)]
    public const string ShedRoof = "HS14";

    /// <summary>A storey's wall is checkered in one log, and its corner posts are cut from the same log.</summary>
    /// <remarks>Either change the checkerboard in the <c>wall</c> of the storey to another log, or change the
    /// <c>post</c> of the storey to a log of another wood.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Style, RuleConcern.Material)]
    public const string CheckerInPostWood = "HS15";

    /// <summary>A wall or gable of a house is built of a surfacing block.</summary>
    /// <remarks>Change the material the finding names to a block that is not a surfacing block.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Style, RuleConcern.Material)]
    public const string SurfacingWall = "HS16";

    /// <summary>A wall, gable or roof of a house is built of snow or ice.</summary>
    /// <remarks>Change the material the finding names to a block that is not snow or ice.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Style, RuleConcern.Material)]
    public const string SnowAndIce = "HS17";

    /// <summary>A storey above the ground has no deck other than air and no field laid over the deck.</summary>
    /// <remarks>Either set the <c>deck</c> of the storey to a material other than air, or set the
    /// <c>surface.field</c> of the storey to a material.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Style, RuleConcern.Structure)]
    public const string FloorlessStorey = "HS18";

    /// <summary>The name of a library house is not describing words joined by hyphens and ending on a building
    /// word.</summary>
    /// <remarks>Change the <c>name</c> of the house until every word but the last is a describing word and the last
    /// is a building word.</remarks>
    [Rule(RuleCategory.Unknown, RuleConcern.Style)]
    public const string LibraryName = "HS19";
}

/// <summary>
/// The gate a <see cref="HouseStyle"/> is checked against before it is stored — beside the style rather than in
/// a driver. A style that cannot be built as stated is refused when it is posted; one that builds against the
/// author's verdicts on how a house looks (<c>HS7</c>–<c>HS19</c>) is stored with complaints naming them.
///
/// <para><see cref="Check"/> is safe to run on any house, decorative or objective, whatever it is built for:
/// every fault it names is a property of the style's own geometry or materials, never of what the building is
/// <em>for</em>. No style the library is seeded with trips it. A stair-lattice or a slab-banded window is
/// allowed on any house, spawns included, so long as its block is the kind the form needs
/// (<see cref="HouseStyleRules.BlockKind"/>); neither form is a defect to be refused on its own.</para>
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
        if (style.Porch is { } porch)
        {
            findings.AddRange(CheckRoofForm(porch.Roof, "porch.roof"));
            if (porch.Canopy is { } canopy) CheckRoofMaterial("porch.canopy", canopy, findings);
        }
        CheckPorchHeadroom(style, findings);
        CheckBeamsHaveTimber(style, findings);
        CheckFrameTimber(style, findings);
        CheckBeamsHavePosts(style, findings);
        CheckGableAgainstVerge(style, findings);
        CheckLogAtTheFoot(style, findings);
        CheckStiltFloor(style, findings);
        CheckCheckerAgainstPosts(style, findings);
        CheckFaces(style, findings);
        CheckFloors(style, findings);
        return findings;
    }

    /// <summary>HS8 — a porch whose canopy climbs past the wall it is attached to. The canopy is seated by its
    /// own lowest course clearing the doorway (which is what keeps the way out of the door walkable), and its
    /// ridge then follows by the form's own rise; a wall shorter than the doorway plus that clearance plus
    /// that rise is a wall the canopy tops out above.</summary>
    private static void CheckPorchHeadroom(HouseStyle style, List<Finding> findings)
    {
        if (style.Porch is not { Depth: > 0 } porch) return;
        var rise = style.PorchCanopyRise();
        var needed = style.Doorway.Height + PorchClearance + rise;
        if (style.WallCourses >= needed) return;

        findings.Add(new Finding(HouseStyleRules.PorchHeadroom,
            $"the porch fronts a {style.Doorway.Height}-course doorway and its {porch.Depth}-deep canopy "
            + $"climbs {rise} course(s) over it, so it tops out {needed - style.WallCourses} course(s) above a "
            + $"wall of {style.WallCourses}. Shorten the porch, flatten its roof, lower the door, or raise the "
            + "wall.",
            Severity.Complaint, Field: "porch"));
    }

    /// <summary>The air between a doorway's top course and the canopy over it, in courses — what the stamper
    /// seats a canopy by, and therefore what a wall has to reach for the canopy to stand under its eave.</summary>
    private const int PorchClearance = 2;

    // ── a block named for a geometric role, never checked to be that kind of block ────────────────────────

    private static void CheckDoorHead(DoorHeadStyle head, List<Finding> findings)
    {
        if (head.Form == DoorHeadForm.None) return;
        Refuse(HouseBlockKinds.DoorHeadBlock, head.Block, findings);
        if (head.Fill == DoorHeadFill.UpperSlab) Refuse(HouseBlockKinds.DoorHeadFill, head.FillBlock, findings);
    }

    /// <summary>HS1 for one field of the catalogue, at the path it is stated at. The field, the kind it takes
    /// and the sentence saying why are the catalogue's, so <c>GET /api/room-styles/block-kinds</c> answers
    /// with the same words the refusal does.</summary>
    private static void Refuse(HouseBlockField field, int blockId, List<Finding> findings, string? at = null)
    {
        if (HouseBlockKinds.Accepts(field.Kind, blockId)) return;
        var path = at ?? field.Field;
        findings.Add(new Finding(HouseStyleRules.BlockKind,
            $"{path} ({blockId}) is not {HouseBlockKinds.Spoken(field.Kind)}. {field.Means}",
            Field: path));
    }

    // ── a beam is a log, and a part built of two blocks is built of one material ──────────────────────────

    /// <summary>The beams that run past a building's corners are the ends of its floor timbers, which is what
    /// <see cref="BeamStyle.Block"/> has always been — the docstring says "the log the ends are cut from".
    /// Anything else is a beam that is not one.</summary>
    private static void CheckBeams(BeamStyle beams, List<Finding> findings)
    {
        if (beams.Block >= 0) Refuse(HouseBlockKinds.Beams, beams.Block, findings);
    }

    /// <summary>HS9 — beam ends coming out of a course that is not a laid log. The ends are laid at each seam,
    /// in the top course of the storey under it, so that course is the floor timber they are the ends of; a laid
    /// log on any other course carries nothing they belong to. A building of one storey has no seam and lays no
    /// ends, so the word on it is inert rather than wrong.</summary>
    private static void CheckBeamsHaveTimber(HouseStyle style, List<Finding> findings)
    {
        if (!style.Beams.Any) return;
        var levels = style.Levels;
        for (var at = 0; at < levels.Count - 1; at++)
        {
            var wall = levels[at].Wall ?? style.Wall;
            var seam = levels[at].Courses(topmost: false) - 1;
            var course = wall.At(seam).Material;
            if (course is LaidLogMaterial) continue;

            findings.Add(new Finding(HouseStyleRules.BeamsWithoutTimber,
                $"the beams of {BlockMaterials.Of(style.Beams.Block, style.Beams.Data)} come out of storey {at}'s "
                + $"top course, and that course is {Describe(course)} rather than a laid log, so the ends run out "
                + "of the wall with no timber behind them. Lay that course in a laidLog of the beams' wood, or "
                + "drop the beams.",
                Severity.Complaint, Field: $"storeys[{at}].wall"));
        }
    }

    /// <summary>HS11 — beam ends at a seam whose storey stands on corners that are not log.</summary>
    private static void CheckBeamsHavePosts(HouseStyle style, List<Finding> findings)
    {
        if (!style.Beams.Any) return;
        var levels = style.Levels;
        for (var at = 0; at < levels.Count - 1; at++)
        {
            if (levels[at].Post is SolidMaterial post && BlockFamilies.IsLog(post.Id)) continue;

            findings.Add(new Finding(HouseStyleRules.BeamsWithoutPosts,
                $"the beams of {BlockMaterials.Of(style.Beams.Block, style.Beams.Data)} come out at storey {at}'s "
                + $"corners, and those corners are {Describe(levels[at].Post)} rather than log posts, so the ends "
                + "have no upright to dock against. Stand the storey on log posts of the beams' wood, or drop the "
                + "beams.",
                Severity.Complaint, Field: $"storeys[{at}].post"));
        }
    }

    /// <summary>Every wall a style states: the building's own and each storey's, since a storey with no wall
    /// of its own falls back to the building's.</summary>
    private static IEnumerable<RoomPart> WallParts(HouseStyle style)
    {
        yield return style.Wall;
        foreach (var storey in style.Storeys)
            if (storey.Wall is { } wall) yield return wall;
    }

    /// <summary>A material as a finding names it: its block where it lays one, else its kind.</summary>
    private static string Describe(TerrainMaterial? material) => material switch
    {
        null => "the wall itself",
        _ when SingleBlock(material) is { } block => BlockMaterials.Of(block.Id, block.Data),
        _ => $"a {material.GetType().Name.Replace("Material", "").ToLowerInvariant()} pattern",
    };

    /// <summary>The timbers of one frame: the corner post, the beam ends that dock against it and the laid-log
    /// course they are the ends of. A frame in two woods is a frame nobody cut.</summary>
    private static void CheckFrameTimber(HouseStyle style, List<Finding> findings)
    {
        if (!style.Beams.Any || style.Levels.Count < 2) return;
        var beam = BlockMaterials.Of(style.Beams.Block, style.Beams.Data);

        foreach (var (where, id, data) in Timbers(style))
            if (BlockMaterials.Of(id, data) != beam)
                findings.Add(new Finding(HouseStyleRules.PartMaterial,
                    $"beams are {beam} and {where} is {BlockMaterials.Of(id, data)}. A post, the beam ends "
                    + "docking against it and the course they are the ends of are one frame, so they are cut "
                    + "from one wood.",
                    Field: where));
    }

    /// <summary>HS12 — a gable face in the verge's block. The face is the style's own gable where it names one
    /// and the top storey's last wall course carried up where it does not, which is what the stamp lays.</summary>
    private static void CheckGableAgainstVerge(HouseStyle style, List<Finding> findings)
    {
        if (style.Roof.Form is RoofForm.Hip or RoofForm.Flat) return;
        if (style.Roof.Verge.IsAir() || SingleBlock(style.Roof.Verge) is not { } verge) return;
        var topWall = style.Levels[^1].Wall ?? style.Wall;
        var named = style.Roof.Gable is not null;
        if (SingleBlock(style.Roof.Gable ?? topWall.At(topWall.Extent - 1).Material) is not { } gable) return;
        if (gable != verge) return;

        findings.Add(new Finding(HouseStyleRules.GableAsVerge,
            $"the gable is {BlockMaterials.Of(gable.Id, gable.Data)} and so is the verge, so the roof's border runs "
            + "down the gable in the gable's own block and the end of the roof has no edge. Lay the gable in "
            + "another block than the verge.",
            Severity.Complaint, Field: named ? "roof.gable" : "roof.verge"));
    }

    /// <summary>HS13 — the ground storey's first course laid in log.</summary>
    private static void CheckLogAtTheFoot(HouseStyle style, List<Finding> findings)
    {
        var ground = style.Levels[0].Wall ?? style.Wall;
        if (ground.At(0).Material is not LaidLogMaterial laid) return;

        findings.Add(new Finding(HouseStyleRules.LogAtTheFoot,
            $"the ground storey's first course is a laid log of {BlockMaterials.Of(laid.Id, laid.Data)}, so the "
            + "building stands on a log lying round its footprint. Start the wall on masonry or planks and lay the "
            + "log as the storey's top course.",
            Severity.Complaint, Field: style.Storeys.Count > 0 ? "storeys[0].wall" : "wall"));
    }

    /// <summary>The one block a material lays wherever it is put, or null for a pattern of several. A laid log
    /// is its log: the axis it turns to is the stamp's, not a different block.</summary>
    private static (int Id, int Data)? SingleBlock(TerrainMaterial material) => material switch
    {
        SolidMaterial solid => (solid.Id, solid.Data),
        LaidLogMaterial laid => (laid.Id, laid.Data),
        _ => null,
    };

    /// <summary>The blocks a frame is made of besides the beams: every post the style names, and every
    /// laid-log course a wall carries. Only logs are asked — a post of stone is a pier, not a timber, and the
    /// question of what a frame is cut from does not arise.</summary>
    private static IEnumerable<(string Where, int Id, int Data)> Timbers(HouseStyle style)
    {
        if (style.Post is SolidMaterial post && BlockFamilies.IsLog(post.Id))
            yield return ("post", post.Id, post.Data);
        for (var at = 0; at < style.Storeys.Count; at++)
            if (style.Storeys[at].Post is SolidMaterial storeyPost && BlockFamilies.IsLog(storeyPost.Id))
                yield return ($"storeys[{at}].post", storeyPost.Id, storeyPost.Data);
        foreach (var part in WallParts(style))
            foreach (var band in part.Stack.Bands)
                if (band.Material is LaidLogMaterial laid) yield return ("the wall's laid log", laid.Id, laid.Data);
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
        foreach (var named in NamedMaterials(style).GroupBy(entry => entry.Field))
            if (named.SelectMany(entry => Materials.Laid(entry.Material))
                    .FirstOrDefault(block => BlockFamilies.IsOre(block.Id)) is { Id: > 0 } ore)
                findings.Add(new Finding(HouseStyleRules.OreMaterial,
                    $"{named.Key} names {BlockPalette.Name(ore.Id, 0)}, which is an ore. An ore is stone with "
                    + "something in it and is not a building material.",
                    Field: named.Key));
    }

    /// <summary>A door <b>head</b> written over a storey whose wall is air across the doorway's own courses.
    /// The head is what the rule is about and not the doorway: an opening cut in an open storey is nothing at
    /// all, while an arch and its lintel stand in mid-air over the stilts. Asked of the ground storey, which
    /// is the one a door is cut through, and only where the house states storeys of its own — a house whose
    /// wall is the fallback has a wall.</summary>
    private static void CheckDoorHasWall(HouseStyle style, List<Finding> findings)
    {
        if (style.Doorway.Head.Form == DoorHeadForm.None) return;
        if (!IsOnStilts(style)) return;

        findings.Add(new Finding(HouseStyleRules.DoorWithoutWall,
            $"the ground storey's wall is air over all {Math.Max(1, style.Doorway.Height)} of the doorway's "
            + "courses, so there is no wall to carry a door head — the arch and its lintel stand in mid-air.",
            Field: "doorHead.form"));
    }

    /// <summary>HS10 — a stilt storey standing on a floor. Reads a stilt storey the way <see cref="HouseStyleRules.DoorWithoutWall"/> does,
    /// so the two cannot disagree about what one is: a ground storey whose wall is air for the whole of the
    /// doorway's courses. Where that storey stands on a plate of anything but air, the ground the stilts were
    /// raised over is covered by it.</summary>
    private static void CheckStiltFloor(HouseStyle style, List<Finding> findings)
    {
        if (!IsOnStilts(style)) return;
        if (style.Foundation.Plate.Stack.Bands.All(band => band.Material.IsAir())) return;

        findings.Add(new Finding(HouseStyleRules.StiltFloor,
            $"the ground storey is open for all {Math.Max(1, style.Doorway.Height)} of the doorway's courses "
            + "— a house on stilts — and it stands on a plate that is not air, so the ground it was raised "
            + "over is floored across the whole footprint. State the plate as air to let the terrain run on "
            + "under it, and give the storey above it a deck of its own.",
            Severity.Complaint, Field: "foundation.plate"));
    }

    /// <summary>Whether the ground storey is open for the whole of the doorway's courses — a house on stilts,
    /// an open undercroft. One reading, so <see cref="HouseStyleRules.DoorWithoutWall"/> and
    /// <see cref="HouseStyleRules.StiltFloor"/> mean the same thing by it.</summary>
    private static bool IsOnStilts(HouseStyle style)
    {
        if (style.Storeys.Count == 0 || style.Storeys[0].Wall is not { } wall) return false;
        for (var course = 0; course < Math.Max(1, style.Doorway.Height); course++)
            if (!wall.At(course).Material.IsAir()) return false;
        return true;
    }

    /// <summary>HS7 — a footing, of any block round a plate of any depth.</summary>
    private static void CheckFooting(Foundation foundation, List<Finding> findings)
    {
        if (foundation.Footing is not { } footing) return;
        findings.Add(new Finding(HouseStyleRules.Footing,
            $"the foundation is ringed by a footing of {Describe(footing)}, which reads as a rim round the "
            + "building rather than as anything it stands on. Leave the footing unset; lay the ground storey's "
            + "first course in that block instead where the stone was the point.",
            Severity.Complaint, Field: "foundation.footing"));
    }

    /// <summary>HS14 for one roof form, at the path it was stated at — a building's roof, a porch's canopy, or a
    /// wing's own roof where it names one. One answer for all three, so a lean-to is named wherever it is
    /// stated.</summary>
    public static Findings CheckRoofForm(RoofForm form, string field) => form != RoofForm.Shed
        ? Findings.None
        : Findings.Of(new Finding(HouseStyleRules.ShedRoof,
            $"{field} is a shed — one plane climbing from the front wall to the back — and a lean-to is not a "
            + "roof a building on a map wears. Give it a gable, a hip, a gambrel or a saltbox.",
            Severity.Complaint, Field: field));

    /// <summary>HS15 — a storey's wall checkered in the log its own corner posts are cut from. Asked storey by
    /// storey, since the post that stands beside a checker is that storey's.</summary>
    private static void CheckCheckerAgainstPosts(HouseStyle style, List<Finding> findings)
    {
        var levels = style.Levels;
        for (var at = 0; at < levels.Count; at++)
        {
            if (levels[at].Post is not SolidMaterial post || !BlockFamilies.IsLog(post.Id)) continue;
            var wall = levels[at].Wall ?? style.Wall;
            if (!wall.Stack.Bands.Any(band => CheckersIn(band.Material, post))) continue;

            findings.Add(new Finding(HouseStyleRules.CheckerInPostWood,
                $"storey {at}'s wall is checkered in {BlockMaterials.Of(post.Id, post.Data)} log, the log its "
                + "corner posts are cut from, so the squares run into the posts and the two read as one mass. "
                + "Checker the wall in another log, or stand the storey on posts of another wood.",
                Severity.Complaint, Field: style.Storeys.Count > 0 ? $"storeys[{at}].wall" : "wall"));
        }
    }

    /// <summary>Whether a material checkers the wall in <paramref name="post"/>'s own log: a log checker cut from
    /// it, or a checker one of whose squares is that log, standing or laid — looked for through the patterns a
    /// checker can sit inside.</summary>
    private static bool CheckersIn(TerrainMaterial material, SolidMaterial post)
    {
        bool IsPostLog(TerrainMaterial square) => SingleBlock(square) is { } block
            && block.Id == post.Id && BlockVariants.Normalize(block.Id, block.Data) == BlockVariants.Normalize(post.Id, post.Data);

        return material switch
        {
            LogCheckerMaterial logs => IsPostLog(new SolidMaterial(logs.Id, logs.Data)),
            CheckerMaterial checker => IsPostLog(checker.Even) || IsPostLog(checker.Odd),
            LayeredMaterial layered => (layered.Stack?.Bands ?? []).Any(band => CheckersIn(band.Material, post)),
            WallRunMaterial run => (run.Runs ?? []).Any(stripe => CheckersIn(stripe.Material, post)),
            WallDiagonalMaterial diagonal => (diagonal.Runs ?? []).Any(stripe => CheckersIn(stripe.Material, post)),
            WallFrameMaterial frame => CheckersIn(frame.Edge, post) || CheckersIn(frame.Fill, post),
            _ => false,
        };
    }

    /// <summary>HS16 and HS17 — what the building shows outside, block by block: no wall or gable in a block that
    /// surfaces ground, and no wall, gable or roof in snow or ice. Each face is reported once per rule, however
    /// many of its blocks are at fault.</summary>
    private static void CheckFaces(HouseStyle style, List<Finding> findings)
    {
        foreach (var face in Faces(style).GroupBy(entry => entry.Field))
        {
            var field = face.Key;
            var blocks = face.SelectMany(entry => Materials.Laid(entry.Material)).ToList();
            var roof = field is "roof" or "verge" or "porch.canopy";
            if (!roof && blocks.FirstOrDefault(block => BlockRoles.IsSurfacing(block.Id, block.Data)) is
                { Id: > 0 } turf)
                findings.Add(new Finding(HouseStyleRules.SurfacingWall,
                    $"{field} lays {BlockPalette.Name(turf.Id, turf.Data)}, which is the skin over soil rather than a "
                    + "material, so the wall reads as a turf bank. Lay it in something built.",
                    Severity.Complaint, Field: field));
            if (blocks.FirstOrDefault(block => BlockFamilies.IsFrozen(block.Id)) is { Id: > 0 } frozen)
                findings.Add(new Finding(HouseStyleRules.SnowAndIce,
                    $"{field} lays {BlockPalette.Name(frozen.Id, frozen.Data)}. Snow and ice lie on a building rather "
                    + "than being what it is built of — leave them to the ground's theme.",
                    Severity.Complaint, Field: field));
        }
    }

    /// <summary>HS18 — a storey over the ground whose floor is air across the room: its deck resolves to air and
    /// its surface lays no field over it.</summary>
    private static void CheckFloors(HouseStyle style, List<Finding> findings)
    {
        var levels = style.Levels;
        for (var at = 1; at < levels.Count; at++)
        {
            if (!levels[at].Deck.IsAir() || !(levels[at].Surface?.Field).IsAir()) continue;
            findings.Add(new Finding(HouseStyleRules.FloorlessStorey,
                $"storey {at} stands on air: it names no deck, and the plate it falls back to is air, so the room is "
                + "a ring of wall over a hole. Give the storey a deck.",
                Severity.Complaint, Field: $"storeys[{at}].deck"));
        }
    }

    /// <summary>Every surface a building shows outside, with the field it was named in: each band of the
    /// building's wall and of every storey's own, the gable where one is named, the roof, its verge and a
    /// porch canopy laid in its own material.</summary>
    private static IEnumerable<(string Field, TerrainMaterial Material)> Faces(HouseStyle style)
    {
        foreach (var band in style.Wall.Stack.Bands) yield return ("wall", band.Material);
        for (var at = 0; at < style.Storeys.Count; at++)
            if (style.Storeys[at].Wall is { } wall)
                foreach (var band in wall.Stack.Bands) yield return ($"storeys[{at}].wall", band.Material);
        if (style.Roof.Gable is { } gable) yield return ("gable", gable);
        yield return ("roof", style.Roof.Body);
        yield return ("verge", style.Roof.Verge);
        if (style.Porch?.Canopy is { } canopy) yield return ("porch.canopy", canopy);
    }

    /// <summary>Every material a style names, with the field it was named in — its <see cref="Faces"/> and what
    /// it stands on and is framed in besides. What a check about the material rather than the role reads.</summary>
    private static IEnumerable<(string Field, TerrainMaterial Material)> NamedMaterials(HouseStyle style)
    {
        foreach (var face in Faces(style)) yield return face;
        foreach (var band in style.Foundation.Plate.Stack.Bands) yield return ("foundation.plate", band.Material);
        if (style.Foundation.Footing is { } footing) yield return ("foundation.footing", footing);
        if (style.Post is { } post) yield return ("post", post);
        for (var at = 0; at < style.Storeys.Count; at++)
        {
            if (style.Storeys[at].Post is { } storeyPost) yield return ($"storeys[{at}].post", storeyPost);
            if (style.Storeys[at].Deck is { } deck) yield return ($"storeys[{at}].deck", deck);
        }
        if (style.Beams.Block >= 0) yield return ("beams.block", new SolidMaterial(style.Beams.Block, style.Beams.Data));
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
            case WindowForm.StairLattice or WindowForm.Arched:
                Refuse(HouseBlockKinds.WindowStair, windows.Block, findings, at: $"{field}.block");
                break;
            case WindowForm.SlabBanded:
                Refuse(HouseBlockKinds.WindowSlab, windows.Block, findings, at: $"{field}.block");
                break;
        }
    }

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
        var findings = new List<Finding>(CheckRoofForm(roof.Form, "roofForm"));
        if (roof.Slab >= 0) Refuse(HouseBlockKinds.RoofSlab, roof.Slab, findings);

        // A slab belongs in a roof only on a half-course rise (RoofSlab set). Naming one in Roof itself while
        // RoofSlab is unset asks for a whole block of rise in a material that only fills half its cube, which
        // is the see-through roof HouseStyle.Roof's own docstring warns about.
        if (roof.Slab < 0 && SolidId(roof.Body) is { } roofId && BlockFamilies.IsSlab(roofId))
            findings.Add(new Finding(HouseStyleRules.RoofMaterial,
                $"roof ({roofId}) is a slab and roofSlab is unset (-1). A course of slabs at a whole block of " +
                "rise leaves an open half between every pair and the roof reads see-through — set roofSlab to " +
                "a real slab and let roof carry the whole-block half, or choose a whole block for roof.",
                Field: "roof"));

        CheckRoofMaterial("roof", roof.Body, findings);
        CheckRoofMaterial("verge", roof.Verge, findings);

        // A laid log has no slab to continue it in, so a half-course rise over one is a course of logs and a
        // course of something else alternating up the slope.
        if (roof.Slab >= 0 && roof.Body is LaidLogMaterial)
            findings.Add(new Finding(HouseStyleRules.RoofMaterial,
                "roofSlab is set over a roof laid in logs, and no slab is cut from a log. A laid log carries " +
                "its own whole-course rise — leave roofSlab unset.",
                Field: "roofSlab"));

        // The half-course slab is the body continuing by halves, so it is the body's own material. A slab of
        // something else makes the roof two materials in alternating courses, which reads as neither.
        if (roof.Slab >= 0 && roof.Body is SolidMaterial body
            && !BlockMaterials.Same(body.Id, body.Data, roof.Slab, roof.SlabData))
            findings.Add(new Finding(HouseStyleRules.RoofMaterial,
                $"roofSlab is {BlockMaterials.Of(roof.Slab, roof.SlabData)} and the roof it steps in halves " +
                $"is {BlockMaterials.Of(body.Id, body.Data)}. The half-course slab continues the body, so it " +
                "is the body's own material.",
                Field: "roofSlab"));

        // The stair is the body climbing in steps, so it is the body's own material, it is never cut from a
        // log, and a roof climbs in halves or in stairs and not both.
        if (roof.Stair >= 0)
        {
            Refuse(HouseBlockKinds.RoofStair, roof.Stair, findings);
            if (roof.Slab >= 0)
                findings.Add(new Finding(HouseStyleRules.RoofMaterial,
                    "roofStair and roofSlab are both set. A roof climbs half a block at a time in slabs or a whole " +
                    "block at a time in stairs — name one of them.", Field: "roofStair"));
            if (roof.Body is LaidLogMaterial)
                findings.Add(new Finding(HouseStyleRules.RoofMaterial,
                    "roofStair is set over a roof laid in logs, and no stair is cut from a log — leave roofStair " +
                    "unset.", Field: "roofStair"));
            if (roof.Body is SolidMaterial solid && !BlockMaterials.Same(solid.Id, solid.Data, roof.Stair, 0))
                findings.Add(new Finding(HouseStyleRules.RoofMaterial,
                    $"roofStair is {BlockMaterials.Of(roof.Stair, 0)} and the roof it steps in is " +
                    $"{BlockMaterials.Of(solid.Id, solid.Data)}. The stair continues the body, so it is the body's " +
                    "own material.", Field: "roofStair"));
        }
        return findings;
    }

    /// <summary>HS3 for one plane of a roof — its body, its verge, or a porch canopy laid in its own material.
    /// Each is one block: never a pattern, never a bare log, never a ground material.</summary>
    private static void CheckRoofMaterial(string field, TerrainMaterial material, List<Finding> findings)
    {
        // A laid log is one material, not several: it is one block that takes its axis from the surface it
        // is on, which is the one thing a solid cannot say and the reason it is its own kind. On a roof
        // that axis is the ridge, so the logs lie along the slope and show bark rather than sawn ends.
        if (material is LaidLogMaterial laid)
        {
            if (!BlockFamilies.IsLog(laid.Id))
                findings.Add(new Finding(HouseStyleRules.RoofMaterial,
                    $"{field} ({laid.Id}) is laid as a log and is not one. Only a log carries its axis in " +
                    "its data; anything else laid comes out turned at random.", Field: field));
            return;
        }

        // A roof is read from below and from a distance, and each plane of it is one surface: a pattern
        // there is several blocks in one surface, which is the fault rather than a style.
        if (material is not SolidMaterial solid)
        {
            findings.Add(new Finding(HouseStyleRules.RoofMaterial,
                $"{field} is a {Patterned(material)} rather than one block. A roof's body, its verge and a " +
                "canopy are each a single material — name the block itself.", Field: field));
            return;
        }
        var id = solid.Id;
        if (BlockFamilies.IsLog(id))
            findings.Add(new Finding(HouseStyleRules.RoofMaterial,
                $"{field} ({id}) is a bare log, which has no axis and stands every one of them on end — a " +
                "sawn face out at whoever looks at the slope. Lay it instead: a laid log takes the axis " +
                "the ridge is going.", Field: field));
        else if (BlockFamilies.IsSoil(id))
            findings.Add(new Finding(HouseStyleRules.RoofMaterial,
                $"{field} ({id}) is a ground material. A ground material — what a building stands on — is " +
                "never a roof or a verge material.", Field: field));
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
