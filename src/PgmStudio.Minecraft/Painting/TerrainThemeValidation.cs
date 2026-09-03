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
    /// <summary>A block that surfaces ground is painted below the course it surfaces. Grass, podzol, mycelium
    /// and farmland are each exactly one course thick — what is under them is soil — so a bucket deeper than
    /// one course filled with one writes it into every course of its depth, and the ground comes out made of
    /// its own skin.</summary>
    /// <remarks>Put the surfacing block in a `layered` material as the top band at thickness 1, with the soil under it — grass over two dirt is the standard stack. A `cell` or a `voronoi` is a pick and not a stack: whichever block it picks fills the whole depth, so a surfacing block cannot go in one at any depth over one.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Theme, RuleConcern.Terrain)]
    public const string SurfaceBlockBuried = "PT1";

    /// <summary>A pattern states a band, a stop or a side and carries no material in it. The member reads as
    /// present and holds nothing, so the painter meets it with no block to write — and it meets it while the
    /// world is being built, long after the document was stored.</summary>
    /// <remarks>Give the member its material. A `voronoi`'s `bands` and a `layered`'s `stack` each take a pair — `{"material": …, "depth": N}` and `{"material": …, "thickness": N}` — where a `noise`'s `stops` takes bare materials, so a list of materials handed to `bands` binds a band per entry with the material left empty.</remarks>
    [Rule(RuleCategory.Malformed, RuleConcern.Theme, RuleConcern.Terrain)]
    public const string MaterialMissing = "PT2";
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
        return findings;
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

        IEnumerable<(TerrainMaterial? Material, string Path)> members = material switch
        {
            LayeredMaterial layered => layered.Stack.Bands
                .Select((band, at) => ((TerrainMaterial?)band.Material, $"{path}.stack[{at}]")),
            VoronoiMaterial voronoi => voronoi.Bands
                .Select((band, at) => ((TerrainMaterial?)band.Material, $"{path}.bands[{at}]")),
            CellMaterial cell => cell.Palette.Select((entry, at) => (entry, $"{path}.palette[{at}]")),
            NoiseMaterial noise => noise.Stops.Select((stop, at) => (stop, $"{path}.stops[{at}]")),
            TurbulenceMaterial turbulence => turbulence.Stops.Select((stop, at) => (stop, $"{path}.stops[{at}]")),
            ElectricMaterial electric => electric.Stops.Select((stop, at) => (stop, $"{path}.stops[{at}]")),
            WallRunMaterial run => run.Runs
                .Select((stripe, at) => ((TerrainMaterial?)stripe.Material, $"{path}.runs[{at}]")),
            WallDiagonalMaterial diagonal => diagonal.Runs
                .Select((stripe, at) => ((TerrainMaterial?)stripe.Material, $"{path}.runs[{at}]")),
            WallFrameMaterial frame => [(frame.Edge, $"{path}.edge"), (frame.Fill, $"{path}.fill")],
            CheckerMaterial checker => [(checker.Even, $"{path}.even"), (checker.Odd, $"{path}.odd")],
            _ => [],
        };

        foreach (var (member, memberPath) in members)
            foreach (var gap in Uncarried(member, memberPath))
                yield return gap;
    }

    /// <summary>Whether <paramref name="material"/> may fill <paramref name="depth"/> courses. A stack is read
    /// band by band, since a stack is what a depth is <em>for</em>; anything else is a pick, and a pick over
    /// more than one course writes one block into all of them.</summary>
    private static void CheckDepth(string bucket, TerrainMaterial material, int depth, List<Finding> findings)
    {
        if (material is LayeredMaterial layered)
        {
            var course = 0;
            foreach (var band in layered.Stack.Bands)
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

    /// <summary>Every surfacing block a material can resolve to, patterns walked to their leaves. The data
    /// travels with the id because podzol is a variant of dirt and nothing else tells the two apart.</summary>
    private static IEnumerable<(int Id, int Data)> Surfacing(TerrainMaterial material) =>
        Blocks(material).Where(block => BlockRoles.IsSurfacing(block.Id, block.Data));

    private static IEnumerable<(int Id, int Data)> Blocks(TerrainMaterial material) => material switch
    {
        SolidMaterial solid => [(solid.Id, solid.Data)],
        LayeredMaterial layered => layered.Stack.Bands.SelectMany(band => Blocks(band.Material)),
        VoronoiMaterial voronoi => voronoi.Bands.SelectMany(band => Blocks(band.Material)),
        CellMaterial cell => cell.Palette.SelectMany(Blocks),
        NoiseMaterial noise => noise.Stops.SelectMany(Blocks),
        TurbulenceMaterial turbulence => turbulence.Stops.SelectMany(Blocks),
        ElectricMaterial electric => electric.Stops.SelectMany(Blocks),
        CheckerMaterial checker => Blocks(checker.Even).Concat(Blocks(checker.Odd)),
        _ => [],
    };
}
