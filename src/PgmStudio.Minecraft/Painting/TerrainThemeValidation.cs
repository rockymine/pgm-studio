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

    /// <summary>A sampled pattern's brush is finer than the blocks it paints. A cell size or a field scale is
    /// the period a pattern varies over, in blocks, so below two it changes faster than the ground it is laid
    /// on can show: every block is its own feature, the pattern resolves to noise at any distance, and no
    /// palette rescues it. A guard against a pathological number rather than a judgement about taste — how
    /// coarse a brush should be is the author's, and only a brush finer than one block is nobody's.</summary>
    /// <remarks>Give the pattern a period of at least two blocks. A `cell` and a `voronoi` state theirs as `cellSize`, a `noise`, `turbulence` or `electric` field as `scale`; the committed themes sit around six to eight, which is what a pattern read as a ground looks like. To mix two blocks with no feature size at all, a `cell` at a coarse size with a high `jitter` is the pattern that means it.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Theme, RuleConcern.Terrain)]
    public const string BrushTooFine = "PT3";

    /// <summary>A sampled field paints a bucket a player reads from the side and states no vertical period,
    /// so every block in a column resolves the same and the face comes out in vertical stripes. A field of the
    /// plane is a fabric for ground seen from above; a wall or a fill is seen edge-on, and the one thing that
    /// gives a face its grain there is the field varying with height.</summary>
    /// <remarks>Give the pattern a `rise` — the vertical period of its field, in blocks, which samples the volume instead of the plane. Around the pattern's own `cellSize` or `scale` is what reads as one fabric rather than two; the wall-run and diagonal patterns draw their stripes deliberately and are not asked. A field whose stripes are the intent belongs in `surface` or `rim`, which are the buckets read from above.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Theme, RuleConcern.Terrain)]
    public const string FlatFieldOnAFace = "PT4";

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

        // A fill under a surface is ground, and ground is met from above wherever it is not cut. A fill with
        // neither a surface nor a rim over it is the whole of a thing that is *made of* its material (TP22) —
        // a stilt, a kerb, a tunnel wall — and every side of that is a face.
        if (!theme.Surface.Enabled && !theme.Rim.Enabled) CheckRise("fill", theme.Fill, findings);
        return findings;
    }

    /// <summary>Every sampled field in a bucket read from the side that states no vertical period. A face is
    /// met edge-on, so a field of the plane gives each of its columns one answer and it comes out striped.
    ///
    /// <para>Its own walk rather than <see cref="Nodes"/>'s, because one band of a depth stack is the one
    /// place the question does not apply: a band a single course thick has no height for a field to vary
    /// over, which is what the course of turf over a body of stone is. A stack read any other way — by ring,
    /// by world height, by inclination — gives each band the whole span, so each is asked as the bucket
    /// was.</para></summary>
    private static void CheckRise(string bucket, TerrainMaterial? material, List<Finding> findings)
    {
        if (material is null) return;

        if (material is LayeredMaterial layered)
        {
            var bands = layered.Stack?.Bands ?? [];
            for (var at = 0; at < bands.Count; at++)
            {
                if (layered.Axis == BandAxis.Depth && bands[at].Thickness <= 1) continue;
                CheckRise($"{bucket}.stack[{at}]", bands[at].Material, findings);
            }
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
    /// material that is not a sampled field. A checker's square, a wall run's stripe and a stack's bands are
    /// drawn rather than sampled and each already says what it does with height.</summary>
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
