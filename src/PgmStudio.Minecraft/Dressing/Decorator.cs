using PgmStudio.Geom;
using PgmStudio.Geom.Algorithms;

namespace PgmStudio.Minecraft.Dressing;

/// <summary>What the dressing pass is allowed to touch, and how the map is mirrored — everything the
/// <see cref="Decorator"/> needs about the world beyond the world itself.</summary>
/// <param name="SurfaceTop">The first air Y above each column, the same grid the painter and every stamper
/// read.</param>
/// <param name="RecipeAt">The dressing governing a cell — a shape override, else the map default.</param>
/// <param name="IsProtected">Cells nothing may be placed on: spawns, objectives, structures, paths. A prop
/// that landed here would break play or read wrong, so the mask is consulted before anything else.</param>
/// <param name="Symmetry">How the map is mirrored, which decides where gameplay-affecting props may be
/// generated and how each one is turned for its other images.</param>
public sealed record DressingContext(
    IReadOnlyDictionary<(int X, int Z), int> SurfaceTop,
    Func<int, int, DressingRecipe> RecipeAt,
    Func<int, int, bool> IsProtected,
    DressingSymmetry Symmetry)
{
    public DressingContext(IReadOnlyDictionary<(int X, int Z), int> surfaceTop, DressingRecipe recipe)
        : this(surfaceTop, (_, _) => recipe, (_, _) => false, DressingSymmetry.None) { }
}

/// <summary>What one pass placed, for a caller that wants to report or preview it rather than only write it.
/// Props are counted as placed, images included — two mirrored boulders are two boulders.</summary>
public readonly record struct DressingTally(int Plants, int Boulders, int Trees);

/// <summary>
/// The dressing pass: the third and last walk over a realized world (docs/world-export/decoration.md). The
/// structure stampers seat rooms and objectives on raw stone; the painter rewrites that stone's surface into
/// grass, quartz and clay; this adds the terrain's life on top — plants in the air above the soil, rock
/// half-buried in it, trees seated on it.
///
/// <para>Running <b>after</b> the painter is what makes it tractable rather than merely conventional. The one
/// fact every part of it needs is what the surface now <em>is</em> — soil takes flora, a plaza's quartz does
/// not, a monument's wool certainly does not — and the painter has just decided that per cell. So the pass
/// reads the top block and never re-derives a thing. It is also why it can place non-stone freely: the painter
/// is stone-only and has already run, so nothing written here will be overwritten.</para>
///
/// <para>The break from the painter is geometry. A material can only answer <em>which block</em> a stone cell
/// becomes; it cannot add one. Flora, boulders and trees are added cells, so this is a sibling pass that calls
/// <see cref="VoxelWorld.SetBlock"/> above the surface rather than a new kind of material.</para>
///
/// <para><b>Fairness is structural here, not a filter.</b> Anything that gives cover or breaks a sightline is
/// generated only on the authored unit and then stamped at every image of its orbit, turned by that image's
/// own transform (<see cref="DressingSymmetry"/>). Two teams therefore face the same rock from the same side.
/// Flowers go through none of that, because they decide nothing and two identical beds read as a glitch.</para>
/// </summary>
public static class Decorator
{
    /// <summary>Dress the footprint. Returns what it placed. A bare recipe everywhere is a no-op, so a map that
    /// never opened the dressing phase exports byte-for-byte as it did before.</summary>
    public static DressingTally Decorate(VoxelWorld world, DressingContext context)
    {
        // Order matters: the big props go down first and become exclusions for the small ones, so flora does
        // not sprout through a boulder and a tree does not grow out of another tree's trunk.
        var taken = new HashSet<(int X, int Z)>();
        var boulders = PlaceBoulders(world, context, taken);
        var trees = PlaceTrees(world, context, taken);
        var plants = PlaceFlora(world, context, taken);
        return new DressingTally(plants, boulders, trees);
    }

    // ── flora (DR-FL) ───────────────────────────────────────────────────────────
    // One block per cell, in the air above the surface, and only where the paint beneath accepts it. A plant
    // occupies its own cell and nothing around it, so it needs no local frame and no turning: deciding it on
    // the orbit representative is already enough to make it identical for every team.
    private static int PlaceFlora(VoxelWorld world, DressingContext context, HashSet<(int X, int Z)> taken)
    {
        var placed = 0;
        foreach (var (cell, top) in context.SurfaceTop)
        {
            if (taken.Contains(cell) || context.IsProtected(cell.X, cell.Z)) continue;
            if (context.RecipeAt(cell.X, cell.Z).Flora is not { } flora) continue;
            if (world.GetBlock(cell.X, top, cell.Z).Id != Blocks.Air) continue;   // something is already there

            var (groundId, groundData) = world.GetBlock(cell.X, top - 1, cell.Z);
            var share = DressingPalette.SoilShare(groundId, groundData);
            if (share <= 0) continue;

            if (PickPlant(flora, cell.X, cell.Z, share, context.Symmetry) is not { } plant) continue;
            world.SetBlock(cell.X, top, cell.Z, plant.Id, plant.Data);
            if (plant.Tall && top + 1 < VoxelWorld.MaxHeight)
                world.SetBlock(cell.X, top + 1, cell.Z, plant.Id, DressingPalette.DoublePlantUpper);
            placed++;
        }
        return placed;
    }

    /// <summary>What grows in one cell, or null for bare ground. Two fields decide it: a density field says
    /// whether anything grows at all — which is what turns an even speckle into meadows and clearings — and a
    /// second, coarser field paints flowers in <em>patches</em>, so a map gets fields of one colour rather than
    /// confetti.</summary>
    private static Plant? PickPlant(FloraSpec flora, int x, int z, double soilShare, DressingSymmetry symmetry)
    {
        var density = PatternNoise.Fbm(x, z, flora.Seed, flora.Scale, flora.Octaves);
        if (density < 1 - flora.Coverage * soilShare) return null;

        // Tall cover hides a crouching player, so its cell is decided on the orbit representative — the same
        // ground is tall or bare for every team. The rest of the overlay decides nothing and stays free.
        if (flora.TallShare > 0)
        {
            var (rx, rz) = symmetry.Canonical(x, z);
            if (PatternNoise.Unit(rx, rz, flora.Seed + 61) < flora.TallShare)
                return PatternNoise.Unit(rx, rz, flora.Seed + 62) < flora.FernShare
                    ? DressingPalette.LargeFern : DressingPalette.TallGrass;
        }

        var flowerField = PatternNoise.Fbm(x, z, flora.Seed + 33, flora.FlowerScale, 2);
        if (flowerField > 1 - flora.FlowerShare && PatternNoise.Unit(x, z, flora.Seed + 44) < 0.7)
        {
            var pick = PatternNoise.Unit(x, z, flora.Seed + 88);
            return DressingPalette.Flowers[(int)(pick * DressingPalette.Flowers.Length) % DressingPalette.Flowers.Length];
        }
        return PatternNoise.Unit(x, z, flora.Seed + 21) < flora.FernShare
            ? DressingPalette.Fern : DressingPalette.Grass;
    }

    // ── boulders (DR-SC) ────────────────────────────────────────────────────────
    private static int PlaceBoulders(VoxelWorld world, DressingContext context, HashSet<(int X, int Z)> taken)
    {
        var placed = 0;
        foreach (var (site, spec) in Sites(context, recipe => recipe.Boulders, spec => spec.Seed, spec => spec.Spacing))
        {
            // Shape and finish are keyed on the site, and the site is a representative — so every image of this
            // rock is the same rock, not another rock of the same size.
            var noise = spec.Seed ^ PatternNoise.Hash(site.X, site.Z, spec.Seed + 71);
            var size = 1.4 + spec.SizeSpread * 3.2 * PatternNoise.Unit(site.X, site.Z, spec.Seed + 17);
            var lobes = BoulderShapes.Of(spec.Form, size);

            var prop = BoulderCells(lobes, spec, noise);
            if (Fan(world, context, site, prop, taken) is var images && images > 0) placed += images;
        }
        return placed;
    }

    /// <summary>A boulder as offsets from its own anchor, before it knows where on the map it goes. The mossy
    /// finish is decided here too, so a rock's sky-lit faces stay its sky-lit faces when it is turned.</summary>
    private static List<PropCell> BoulderCells(IReadOnlyList<BlobLobe> lobes, BoulderSpec spec, uint noise)
    {
        var cells = new List<PropCell>();
        var (min, max) = Blob.Bounds(lobes);
        for (var y = (int)Math.Floor(min.Y); y <= (int)Math.Ceiling(max.Y); y++)
        for (var z = (int)Math.Floor(min.Z); z <= (int)Math.Ceiling(max.Z); z++)
        for (var x = (int)Math.Floor(min.X); x <= (int)Math.Ceiling(max.X); x++)
        {
            if (!Blob.Contains(lobes, new Vec3(x, y, z), noise)) continue;
            var lit = y >= 0 && !Blob.Contains(lobes, new Vec3(x, y + 1, z), noise);
            var mossy = spec.Mossy && lit && PatternNoise.Unit(x, z, noise + 3) < 0.55;
            var (id, data) = mossy ? (MossyCobblestone, 0) : (spec.BlockId, spec.BlockData);
            // A cell below the anchor is buried and may replace ground; one above it must find air.
            cells.Add(new PropCell(x, y, z, id, data, Buried: y < 0));
        }
        return cells;
    }

    // ── trees (DR-TR) ───────────────────────────────────────────────────────────
    private static int PlaceTrees(VoxelWorld world, DressingContext context, HashSet<(int X, int Z)> taken)
    {
        var placed = 0;
        foreach (var (site, spec) in Sites(context, recipe => recipe.Trees, spec => spec.Seed, spec => spec.Spacing))
        {
            // Groves, not an orchard: a coarse field gates the scatter so stands clump with clearings between.
            if (PatternNoise.Fbm(site.X, site.Z, spec.Seed + 5, spec.GroveScale, 3) < 1 - spec.GroveThreshold) continue;

            var species = PickSpecies(spec, site.X, site.Z);
            var scale = 1 - spec.SizeSpread * PatternNoise.Unit(site.X, site.Z, spec.Seed + 11);
            var shape = new TreeShape(
                Height: Math.Max(5, species.Height * scale), Levels: species.Levels,
                BranchAngle: species.BranchAngle, Leader: species.Leader);
            var treeSeed = spec.Seed ^ PatternNoise.Hash(site.X, site.Z, spec.Seed + 71);

            var prop = TreeCells(shape, species, treeSeed);
            if (Fan(world, context, site, prop, taken) is var images && images > 0) placed += images;
        }
        return placed;
    }

    /// <summary>A tree as offsets from the block it stands on. Wood is swept from the limbs and leaves are
    /// owned cluster by cluster; wood wins where they meet, so a trunk is never hollowed by its own foliage.
    /// Every leaf carries the no-decay bit — a built map has no growing tree behind its crown, and without the
    /// flag the whole thing disappears shortly after the map loads.</summary>
    private static List<PropCell> TreeCells(TreeShape shape, TreeSpecies species, uint seed)
    {
        var tree = TreeSkeleton.Grow(shape, seed);
        var wood = new HashSet<(int X, int Y, int Z)>();
        foreach (var limb in tree.Limbs)
            foreach (var cell in SweptVolume.Sweep(limb.Path, limb.StartRadius, limb.EndRadius))
                wood.Add(cell);

        var cells = new List<PropCell>(wood.Count * 3);
        foreach (var (x, y, z) in wood) cells.Add(new PropCell(x, y, z, species.LogId, species.LogData, Buried: false));

        var clusters = TreeCrown.Clusters(tree.Tips, species.LeafSize, shape.Size, seed);
        var (min, max) = TreeCrown.Bounds(clusters);
        var leafData = species.LeafData | DressingPalette.LeafNoDecay;
        for (var y = (int)Math.Floor(min.Y); y <= (int)Math.Ceiling(max.Y); y++)
        for (var z = (int)Math.Floor(min.Z); z <= (int)Math.Ceiling(max.Z); z++)
        for (var x = (int)Math.Floor(min.X); x <= (int)Math.Ceiling(max.X); x++)
        {
            if (wood.Contains((x, y, z))) continue;
            if (TreeCrown.OwnerAt(clusters, new Vec3(x, y, z), seed) is null) continue;
            cells.Add(new PropCell(x, y, z, species.LeafId, leafData, Buried: false));
        }
        return cells;
    }

    private static TreeSpecies PickSpecies(TreeSpec spec, int x, int z)
    {
        var names = spec.SpeciesNames;
        var pick = PatternNoise.Unit(x, z, spec.Seed + 3);
        return names.Count == 0
            ? DressingPalette.Species[(int)(pick * DressingPalette.Species.Count) % DressingPalette.Species.Count]
            : DressingPalette.SpeciesNamed(names[(int)(pick * names.Count) % names.Count]);
    }

    // ── the shared prop pipeline ────────────────────────────────────────────────
    /// <summary>One block of a prop, in the prop's own frame: an offset from its anchor, the block it places,
    /// and whether it sits below the surface. <see cref="Buried"/> cells may replace ground — that is what
    /// makes a boulder emerge from the earth rather than rest on it — while the rest must find air.</summary>
    private readonly record struct PropCell(int X, int Y, int Z, int Id, int Data, bool Buried);

    private const int MossyCobblestone = 48;

    /// <summary>Stamp a prop at every image of its orbit, each turned by that image's own transform, and return
    /// how many landed. All-or-nothing per image: an image whose ground is missing or protected is skipped, so
    /// a rock never appears half over a cliff — but the others still stand, because refusing the whole orbit
    /// over one bad image would silently thin a map's dressing near its edges.</summary>
    private static int Fan(VoxelWorld world, DressingContext context, (int X, int Z) site,
        List<PropCell> prop, HashSet<(int X, int Z)> taken)
    {
        if (prop.Count == 0) return 0;
        var placed = 0;
        for (var k = 0; k < context.Symmetry.Order; k++)
        {
            var anchor = context.Symmetry.ImageCell(site.X, site.Z, k);
            var turned = prop.Select(cell =>
            {
                var (tx, tz) = context.Symmetry.TurnCell(cell.X, cell.Z, k);
                return cell with { X = tx, Z = tz };
            }).ToList();

            if (!Seats(context, anchor, turned, out var baseY)) continue;
            foreach (var cell in turned)
            {
                var (wx, wy, wz) = (anchor.X + cell.X, baseY + cell.Y, anchor.Z + cell.Z);
                if (wy is < 1 or >= VoxelWorld.MaxHeight) continue;
                if (!cell.Buried && world.GetBlock(wx, wy, wz).Id != Blocks.Air) continue;
                world.SetBlock(wx, wy, wz, cell.Id, cell.Data);
                taken.Add((wx, wz));
            }
            placed++;
        }
        return placed;
    }

    /// <summary>Whether a prop can stand at an anchor, and the Y its own origin sits at. A prop seats on the
    /// <b>lowest</b> column it covers, so it never floats over a drop; ground that is missing or protected
    /// refuses it outright rather than letting it be clipped.</summary>
    private static bool Seats(DressingContext context, (int X, int Z) anchor, List<PropCell> prop, out int baseY)
    {
        baseY = int.MaxValue;
        foreach (var cell in prop)
        {
            var ground = (X: anchor.X + cell.X, Z: anchor.Z + cell.Z);
            if (!context.SurfaceTop.TryGetValue(ground, out var top)) return false;
            if (context.IsProtected(ground.X, ground.Z)) return false;
            baseY = Math.Min(baseY, top);
        }
        return baseY != int.MaxValue;
    }

    /// <summary>Every scatter site for one kind of prop, with the spec governing it. Sites are the
    /// <b>representatives</b> of their orbits and nothing else, because a prop is generated once and fanned —
    /// generating at every image would roll the shape twice and give two teams different rocks.</summary>
    private static IEnumerable<((int X, int Z) Cell, TSpec Spec)> Sites<TSpec>(
        DressingContext context, Func<DressingRecipe, TSpec?> specOf, Func<TSpec, uint> seedOf, Func<TSpec, int> spacingOf)
        where TSpec : class
    {
        // Spacing is a property of the spec and a map may carry several, so the scatter runs once per distinct
        // spec over the cells that spec governs rather than once with a spacing that fits none.
        var byCell = new Dictionary<TSpec, List<(int X, int Z)>>();
        foreach (var cell in context.SurfaceTop.Keys)
        {
            if (context.IsProtected(cell.X, cell.Z)) continue;
            if (!context.Symmetry.IsCanonical(cell.X, cell.Z)) continue;
            if (specOf(context.RecipeAt(cell.X, cell.Z)) is not { } spec) continue;
            if (!byCell.TryGetValue(spec, out var cells)) byCell[spec] = cells = [];
            cells.Add(cell);
        }

        foreach (var (spec, cells) in byCell)
        {
            cells.Sort(static (a, b) => a.Z != b.Z ? a.Z - b.Z : a.X - b.X);   // a stable walk, so a re-export matches
            foreach (var cell in BlueNoise.Sites(cells, seedOf(spec), spacingOf(spec)))
                yield return (cell, spec);
        }
    }
}
