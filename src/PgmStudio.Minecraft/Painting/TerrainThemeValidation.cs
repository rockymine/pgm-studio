using PgmStudio.Domain;
using PgmStudio.Minecraft.Palette;
using PgmStudio.Vocabulary;

namespace PgmStudio.Minecraft.Painting;

/// <summary>The terrain-paint rule ids <see cref="TerrainThemeValidation"/> cites — stable names for what a
/// finding about a theme is about, the way <c>HS*</c> names a house-style fault. Distinct from the
/// <c>TP1</c>–<c>TP16</c> of <c>docs/world-export/terrain-painting.md</c>, which are the model's own laws and
/// not findings anything answers with.</summary>
public static class TerrainThemeRules
{
    /// <summary>A surfacing block in a palette part is more than 1 block thick, or lies below the first band of its
    /// band stack.</summary>
    /// <remarks>Either change the pattern to a <c>layered</c> one whose first band is the surfacing block at a
    /// <c>thickness</c> of 1, or delete the surfacing block from the <c>bands</c>, <c>palette</c> or <c>stops</c>
    /// of the pattern.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Theme, RuleConcern.Terrain)]
    public const string SurfaceBlockBuried = "PT1";

    /// <summary>A band, stop or side of a pattern has no material.</summary>
    /// <remarks>Add a <c>material</c> to the <c>bands</c> entry or the <c>stack</c> entry the finding names. Set
    /// the <c>stops</c> entry the finding names to a block or a pattern.</remarks>
    [Rule(RuleCategory.Malformed, RuleConcern.Theme, RuleConcern.Terrain)]
    public const string MaterialMissing = "PT2";

    /// <summary>A sampled pattern's period is less than 2 blocks.</summary>
    /// <remarks>Set the <c>cellSize</c> of a <c>cell</c> or <c>voronoi</c> pattern to at least 2 blocks. Set the
    /// <c>scale</c> of a <c>noise</c>, <c>turbulence</c> or <c>electric</c> pattern to at least 2 blocks.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Theme, RuleConcern.Terrain)]
    public const string BrushTooFine = "PT3";

    /// <summary>The rise of a sampled pattern in the wall or fill of a palette is less than 1 block.</summary>
    /// <remarks>Either set the <c>rise</c> of the pattern to at least 1 block, or change the <c>kind</c> of the
    /// pattern to <c>solid</c> or <c>checker</c>.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Theme, RuleConcern.Terrain)]
    public const string FlatFieldOnAFace = "PT4";

    /// <summary>An island painted with a team colour pattern has the spawns of more than 1 team.</summary>
    /// <remarks>Either split the island with a subtract in <c>shapes</c>, or change the <c>theme</c> of the shape
    /// that paints it to a palette with no team colour pattern.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Theme, RuleConcern.Terrain)]
    public const string TintOverSharedGround = "PT5";

    /// <summary>The finest period a sampled pattern may vary over, in blocks — <see cref="BrushTooFine"/>'s
    /// one number (author).</summary>
    public const int BrushFloor = 2;
}

/// <summary>
/// What a terrain theme's own materials cannot do, whatever ground they are painted on.
///
/// <para>The one rule here is about <b>depth</b>, which is where a theme's materials and its buckets meet: a
/// bucket carries a depth and a material carries no notion of one, so a material that is a <em>pick</em> —
/// a cell, a voronoi, a noise field — writes whichever block it picked into every course the bucket claims.
/// That is the whole of the fault: a block whose meaning is "this is the surface" repeated three courses down
/// is a ground made of its own skin, and it is the single most repeated authoring mistake in this repository
/// (author).</para>
///
/// <para>A <see cref="LayeredMaterial"/> is the exception and the answer both: it is a stack rather than a
/// pick, so a surfacing block is legal as its <b>top band at one course</b>, which is what the standard
/// grass-over-two-dirt surface already is.</para>
/// </summary>
public static class TerrainThemeValidation
{
    /// <summary>Every fault a theme's own materials can name. Buckets that carry a depth are the ones asked:
    /// the rim and the surface state their own, and the fill claims everything under them.</summary>
    public static Findings Check(TerrainTheme theme)
    {
        var findings = new List<Finding>();
        CheckCarried("rim", theme.Rim.Material, findings);
        CheckCarried("surface", theme.Surface.Material, findings);
        CheckCarried("fill", theme.Fill, findings);
        CheckDepth("rim", theme.Rim.Material, theme.Rim.Depth, findings);
        CheckDepth("surface", theme.Surface.Material, theme.Surface.Depth, findings);
        CheckDepth("fill", theme.Fill, int.MaxValue, findings);
        CheckBrush("rim", theme.Rim.Material, findings);
        CheckBrush("surface", theme.Surface.Material, findings);
        CheckBrush("fill", theme.Fill, findings);

        // The wall is asked only where it paints. A theme built from one material binds that material to
        // every bucket and disables the ones it does not want (TerrainTheme.OfMaterial), so a wall nothing
        // writes would answer a second time for the fill's own fault.
        if (theme.WallEnabled)
        {
            CheckCarried("wall", theme.Wall, findings);
            CheckBrush("wall", theme.Wall, findings);
            CheckRise("wall", theme.Wall, findings);
        }

        // The fill is asked as the wall is. Ground is cut wherever a board has a cliff, a tunnel or a void
        // edge, and the face that cut leaves is the fill — so a field flat through it stripes exactly where a
        // player is closest to it (author).
        CheckRise("fill", theme.Fill, findings);
        return findings;
    }

    /// <summary>Every sampled field in a bucket read from the side that states no vertical period. A face is
    /// met edge-on, so a field of the plane gives each of its columns one answer and it comes out striped.
    ///
    /// <para>Every field in the tree is asked, a band of a stack included and whatever that band's thickness:
    /// a course sits against the courses around it, so a field flat through one still meets its neighbours in
    /// a straight seam. A rise is never nought on a face (author).</para></summary>
    private static void CheckRise(string bucket, TerrainMaterial? material, List<Finding> findings)
    {
        if (material is null) return;

        if (material is LayeredMaterial layered)
        {
            var bands = layered.Stack?.Bands ?? [];
            for (var at = 0; at < bands.Count; at++)
                CheckRise($"{bucket}.stack[{at}]", bands[at].Material, findings);
            if (layered.Beyond is { } beyond) CheckRise($"{bucket}.beyond", beyond, findings);
            return;
        }

        if (Rise(material) is { Blocks: <= 0 } field)
            findings.Add(new Finding(TerrainThemeRules.FlatFieldOnAFace,
                $"{bucket} samples its field in the plane only, so every block of a column resolves alike and "
                + "it reads as vertical stripes. A rise is the vertical period that gives a face its grain.",
                Field: $"{bucket}.{field.Field}"));

        foreach (var (child, childPath) in Children(material, bucket))
            CheckRise(childPath, child, findings);
    }

    /// <summary>The vertical period a sampled field varies over and the field that states it, or null for a
    /// material that is not a sampled field.
    ///
    /// <para>The five here are the five that <em>have</em> a rise, so the question is unaskable of anything
    /// else by construction. A checker's square, a log checker's, a wall run's stripe, a diagonal's, a frame's
    /// edge and a laid log's axis are all <b>drawn</b> — geometry placed where the author placed it, each
    /// already stating what it does with height — and a solid, a stack and a team tint vary with something
    /// that is not position at all.</para></summary>
    private static (int Blocks, string Field)? Rise(TerrainMaterial? material) => material switch
    {
        VoronoiMaterial voronoi => (voronoi.Rise, "rise"),
        CellMaterial cell => (cell.Rise, "rise"),
        NoiseMaterial noise => (noise.Rise, "rise"),
        TurbulenceMaterial turbulence => (turbulence.Rise, "rise"),
        ElectricMaterial electric => (electric.Rise, "rise"),
        _ => null,
    };

    /// <summary>Every sampled pattern in a bucket whose period is under
    /// <see cref="TerrainThemeRules.BrushFloor"/>, nested ones included — a field inside a voronoi's band
    /// paints at its own scale and is as fine as it says it is.</summary>
    private static void CheckBrush(string bucket, TerrainMaterial? material, List<Finding> findings)
    {
        foreach (var (node, path) in Nodes(material, bucket))
        {
            if (Brush(node) is not { } brush || brush.Period >= TerrainThemeRules.BrushFloor) continue;
            findings.Add(new Finding(TerrainThemeRules.BrushTooFine,
                $"{path} varies over {brush.Period} block(s), which is finer than the blocks it paints — "
                + "every block is its own feature and the pattern reads as noise at any distance. A period "
                + $"of at least {TerrainThemeRules.BrushFloor} is what makes a pattern a ground.",
                Field: $"{path}.{brush.Field}"));
        }
    }

    /// <summary>The period a pattern samples over and the field that states it, or null for a pattern that is
    /// drawn rather than sampled. A checker's square and a wall run's stripe are geometry an author placed at
    /// the width they meant, so neither is asked: what this measures is a <em>sampling</em> period, the one
    /// number below which a field stops having features at all.</summary>
    private static (int Period, string Field)? Brush(TerrainMaterial? material) => material switch
    {
        VoronoiMaterial voronoi => (voronoi.CellSize, "cellSize"),
        CellMaterial cell => (cell.CellSize, "cellSize"),
        NoiseMaterial noise => (noise.Scale, "scale"),
        TurbulenceMaterial turbulence => (turbulence.Scale, "scale"),
        ElectricMaterial electric => (electric.Scale, "scale"),
        _ => null,
    };

    /// <summary>Every material in a tree with the path it sits at, the root included. The walk both the
    /// empty-member read and the brush read run over, so a pattern reachable by one is reachable by
    /// both.</summary>
    private static IEnumerable<(TerrainMaterial? Material, string Path)> Nodes(TerrainMaterial? material, string path)
    {
        yield return (material, path);
        if (material is null) yield break;
        foreach (var (child, childPath) in Children(material, path))
            foreach (var node in Nodes(child, childPath))
                yield return node;
    }

    /// <summary>Every member of a bucket's material that binds with nothing in it. A pattern's bands, stops
    /// and sides are each a place a material goes, and a document naming the member without the material
    /// binds it to an empty one rather than failing — so the fault has to be looked for rather than
    /// caught.</summary>
    private static void CheckCarried(string bucket, TerrainMaterial? material, List<Finding> findings)
    {
        foreach (var where in Uncarried(material, bucket))
            findings.Add(new Finding(TerrainThemeRules.MaterialMissing,
                $"{where} states no material, so nothing can be painted where it is picked",
                Field: where));
    }

    /// <summary>The path to every member a pattern states and left empty, the material tree walked to its
    /// leaves. The three pair-carrying members — a stack's band, a voronoi's band, a wall run's stripe — are
    /// each a value type, so an entry naming only its width or its depth carries an empty material rather
    /// than refusing to bind at all.</summary>
    private static IEnumerable<string> Uncarried(TerrainMaterial? material, string path)
    {
        if (material is null) { yield return path; yield break; }

        foreach (var (member, memberPath) in Children(material, path))
            foreach (var gap in Uncarried(member, memberPath))
                yield return gap;
    }

    /// <summary>One material's own members, each with the path it sits at. Every list is read through
    /// <see cref="Members{T}"/>, which answers a single empty member for a pattern that states no list at
    /// all: a document naming a member set under a name the model does not have — a cell's <c>materials</c>
    /// where it wants <c>palette</c> — deserializes the pattern with a null list, which is the very fault
    /// the empty-member walk exists to name, and walking it directly threw instead.</summary>
    private static IEnumerable<(TerrainMaterial? Material, string Path)> Children(
        TerrainMaterial material, string path)
        => material switch
        {
            LayeredMaterial layered => Members(layered.Stack?.Bands, $"{path}.stack",
                                               band => (TerrainMaterial?)band.Material),
            VoronoiMaterial voronoi => Members(voronoi.Bands, $"{path}.bands",
                                               band => (TerrainMaterial?)band.Material),
            CellMaterial cell => Members(cell.Palette, $"{path}.palette", entry => (TerrainMaterial?)entry),
            NoiseMaterial noise => Members(noise.Stops, $"{path}.stops", stop => (TerrainMaterial?)stop),
            TurbulenceMaterial turbulence => Members(turbulence.Stops, $"{path}.stops",
                                                     stop => (TerrainMaterial?)stop),
            ElectricMaterial electric => Members(electric.Stops, $"{path}.stops",
                                                 stop => (TerrainMaterial?)stop),
            WallRunMaterial run => Members(run.Runs, $"{path}.runs",
                                           stripe => (TerrainMaterial?)stripe.Material),
            WallDiagonalMaterial diagonal => Members(diagonal.Runs, $"{path}.runs",
                                                     stripe => (TerrainMaterial?)stripe.Material),
            WallFrameMaterial frame => [(frame.Edge, $"{path}.edge"), (frame.Fill, $"{path}.fill")],
            CheckerMaterial checker => [(checker.Even, $"{path}.even"), (checker.Odd, $"{path}.odd")],
            _ => [],
        };

    /// <summary>A pattern's members with their paths, or the pattern's own path once where it states no
    /// member list at all — a pattern that picks from nothing paints nothing, which is the same finding one
    /// naming an empty member is.</summary>
    private static IEnumerable<(TerrainMaterial? Material, string Path)> Members<T>(
        IReadOnlyList<T>? list, string path, Func<T, TerrainMaterial?> of)
    {
        if (list is null) return [(null, path)];
        return list.Select((entry, at) => (of(entry), $"{path}[{at}]"));
    }

    /// <summary>Whether <paramref name="material"/> may fill <paramref name="depth"/> courses. A stack is read
    /// band by band, since a stack is what a depth is <em>for</em>; anything else is a pick, and a pick over
    /// more than one course writes one block into all of them.</summary>
    private static void CheckDepth(string bucket, TerrainMaterial material, int depth, List<Finding> findings)
    {
        // Only the depth axis measures courses. A stack read inward, by world height or by inclination states
        // its thicknesses in rings, in world Y or in degrees, so each of its bands covers the bucket's whole
        // depth and is asked the same question the bucket was.
        if (material is LayeredMaterial { Axis: not BandAxis.Depth } across)
        {
            foreach (var band in across.Stack?.Bands ?? []) CheckDepth(bucket, band.Material, depth, findings);
            if (across.Beyond is { } beyond) CheckDepth(bucket, beyond, depth, findings);
            return;
        }

        if (material is LayeredMaterial layered)
        {
            var course = 0;
            foreach (var band in layered.Stack?.Bands ?? [])
            {
                // The top band at one course is the surface itself, which is the whole point of the stack.
                var surfacing = Surfacing(band.Material).ToList();
                if (surfacing.Count > 0 && (course > 0 || band.Thickness > 1))
                    findings.Add(Buried(bucket, surfacing[0], band.Thickness,
                        course > 0
                            ? $"stands {course} course(s) below the top of the {bucket}"
                            : $"is {band.Thickness} courses thick at the top of the {bucket}"));
                course += Math.Max(1, band.Thickness);
            }
            return;
        }

        if (depth <= 1) return;
        foreach (var block in Surfacing(material))
        {
            findings.Add(Buried(bucket, block, depth,
                $"fills all {(depth == int.MaxValue ? "of" : depth.ToString())} the {bucket}'s courses, "
                + "because the material is a pick rather than a stack"));
            break;
        }
    }

    private static Finding Buried(string bucket, (int Id, int Data) block, int thickness, string how) =>
        new(TerrainThemeRules.SurfaceBlockBuried,
            $"{BlockPalette.Name(block.Id, block.Data)} surfaces ground and {how}. A surfacing block is exactly one "
            + "course thick and what is under it is soil — put it at the top of a layered stack instead.",
            Field: bucket);

    /// <summary>Every surfacing block a material can resolve to, patterns walked to their leaves by
    /// <see cref="Materials.BlocksOf"/>.</summary>
    private static IEnumerable<(int Id, int Data)> Surfacing(TerrainMaterial material) =>
        Materials.BlocksOf(material).Where(block => BlockRoles.IsSurfacing(block.Id, block.Data));
}
