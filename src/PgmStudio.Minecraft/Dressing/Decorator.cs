using PgmStudio.Geom;
using PgmStudio.Geom.Algorithms;
// Named one by one rather than by namespace: Domain and Geom both carry a Vec3, and this file already means
// Geom's everywhere it says one.
using RoomDoor = PgmStudio.Domain.RoomDoor;
using RoomEdge = PgmStudio.Domain.RoomEdge;
using static PgmStudio.Domain.RoomEdges;   // an alias does not carry extension methods; this does
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Houses;
using PgmStudio.Minecraft.Stamping;
using PgmStudio.Minecraft.Painting;
using PgmStudio.Minecraft.Palette;

namespace PgmStudio.Minecraft.Dressing;

/// <summary>What the dressing pass places, what it may not touch, and how the map is mirrored — everything the
/// <see cref="Decorator"/> needs about the world beyond the world itself.</summary>
/// <param name="SurfaceTop">The first air Y above each column, the same grid the painter and every stamper
/// read.</param>
/// <param name="Props">What the author placed, in the order they were placed.</param>
/// <param name="IsProtected">Cells nothing may be placed on: spawns, rooms, structures. A prop that landed
/// here would break play or read wrong, so the mask is consulted before anything else.</param>
/// <param name="Symmetry">How the map is mirrored, which decides where every prop's other images go and how
/// each one is turned to get there.</param>
/// <param name="IsGoalGround">The ground a destroyable or a core stands on, grown by its clearance — a
/// narrower mask than <paramref name="IsProtected"/> and a different question. A goal's ground is not
/// forbidden, it is <b>kept open</b>: grass, fern and flowers grow across it and under a floating monument
/// the way they grow anywhere, because none of them changes what a player can see or reach. What may not
/// stand there is <b>cover</b> — the two-block grass that hides a footstep among it, since an objective is
/// the one thing on a map that wants its approach legible. A tree, a boulder or a building is refused
/// earlier still (OB17 at export), because those are authored and dropping one silently would discard a
/// placement the author can see.</param>
public sealed record DressingContext(
    IReadOnlyDictionary<(int X, int Z), int> SurfaceTop,
    IReadOnlyList<PlacedProp> Props,
    Func<int, int, bool> IsProtected,
    DressingSymmetry Symmetry,
    Func<int, int, bool>? IsGoalGround = null)
{
    public DressingContext(IReadOnlyDictionary<(int X, int Z), int> surfaceTop, IReadOnlyList<PlacedProp> props)
        : this(surfaceTop, props, (_, _) => false, DressingSymmetry.None) { }

    /// <summary>Whether cover may stand on a cell — everything except the ground a goal is read against.</summary>
    public bool AllowsCover(int x, int z) => IsGoalGround is null || !IsGoalGround(x, z);
}

/// <summary>
/// What one pass placed, for a caller that wants to report, claim or preview it rather than only write it.
/// Counted as placed, images included — two mirrored boulders are two boulders.
///
/// <para><b>The buildings are reported as extents and everything else as a count</b>, because a building is
/// the only prop whose columns something downstream has to own: provenance records which stamped thing claimed
/// each column, and a tree or a boulder already separates from built ground by material
/// (<see cref="BlockRoles"/>). Reporting them here is what makes the claim a product of the placement rather
/// than a second derivation from the author's intent — see <see cref="StructureClaim"/> for why that
/// direction is the whole point.</para>
/// </summary>
/// <param name="Claimed">The buildings raised, or null where none was. Read <see cref="Structures"/> instead,
/// which never answers null — the same shape <see cref="Domain.Finding.Subjects"/> carries, so a caller never
/// has to tell an absent list from an empty one.</param>
/// <param name="Dropped">Every whole-prop decline this pass made, or null where nothing was declined — the
/// same null-when-empty convention <paramref name="Claimed"/> carries, for the same reason: a pass that
/// dropped nothing compares equal to one that placed a board with nothing to drop. A path's own per-cell
/// skips are not here — those are the ordinary shape of a route crossing protected ground, not a decision an
/// author needs restated one cell at a time — only the whole-prop causes that used to be silent: a house
/// whose wings make no building, a house that collides with something already standing, a house with no
/// ground under any of its cells, and a tree or a boulder whose site finds no ground, lands on a protected
/// or already-claimed column, or stands nearer to the road than its own kind's standoff allows.</param>
public readonly record struct DressingPlacement(
    int Plants = 0, int Boulders = 0, int Trees = 0, int PathCells = 0, int WaterCells = 0, int Houses = 0,
    IReadOnlyList<StructureClaim>? Claimed = null, IReadOnlyList<DroppedProp>? Dropped = null)
{
    /// <summary>The buildings this pass actually raised, each with the columns it owns. Never null.</summary>
    public IReadOnlyList<StructureClaim> Structures => Claimed ?? [];
}

/// <summary>One whole prop the pass declined to place, and why — the report <see cref="DressingPlacement.Dropped"/>
/// carries. <see cref="Id"/> and <see cref="Kind"/> are the prop's own, so a caller can point back at the
/// document; <see cref="Reason"/> is one factual sentence naming the cause and, where the cause is a place, the
/// cell it happened at, so the drop can be checked against the canvas rather than taken on faith.</summary>
public sealed record DroppedProp(string Id, string Kind, string Reason);

/// <summary>
/// The dressing pass: the third and last walk over a realized world (docs/world-export/decoration.md). The
/// structure stampers seat rooms and objectives on raw stone; the painter rewrites that stone's surface into
/// grass, quartz and clay; this adds the terrain's life on top — a route worn across it, rock half-buried in
/// it, trees standing on it, cover growing over it.
///
/// <para>Running <b>after</b> the painter is what makes it tractable rather than merely conventional. The one
/// fact every part of it needs is what the surface now <em>is</em> — soil takes flora, a plaza's quartz does
/// not, a monument's wool certainly does not — and the painter has just decided that per cell. So the pass
/// reads the top block and never re-derives a thing. It is also why it can place non-stone freely: the painter
/// is stone-only and has already run, so nothing written here will be overwritten.</para>
///
/// <para><b>Everything here was placed by hand.</b> A tree is cover and a boulder is a wall, so where each one
/// stands decides how the map plays, and that decision belongs to the author rather than to a density field.
/// The fields that remain are the ones inside a drawn area — which blade of grass, which cobble — where
/// nobody would want to place them one at a time.</para>
///
/// <para><b>Fairness is structural, not a filter.</b> Every prop is generated once, in its own local frame,
/// and then stamped at each image of its orbit, turned by that image's own transform
/// (<see cref="DressingSymmetry"/>). Two teams therefore face the same rock from the same side. Fanning only
/// the position would not do it: mirroring the anchor of a boulder whose lobe points east leaves the lobe
/// pointing east on both halves of the map.</para>
/// </summary>
public static class Decorator
{
    /// <summary>Place everything the author placed, and report what landed — <b>what landed</b>, not what was
    /// asked for: a prop this pass declines is absent from the report, which is what lets a caller claim its
    /// provenance from the return rather than re-deriving it from the document. No props is a no-op, so a map
    /// that never opened the dressing phase exports byte-for-byte as it did before.</summary>
    public static DressingPlacement Decorate(VoxelWorld world, DressingContext context)
    {
        // Order is what keeps the parts from growing through each other. Water goes first because it is the one
        // prop that carves the ground — everything after it seats on what it leaves. Then paths, whose paved
        // cells become bare ground for the props above them (a route with a tree in the middle of it is not a
        // route); then buildings, which check every claim but the road's — paths are laid first and a road is
        // meant to run to a porch or a door, so a house standing over pavement wins the ground and the path
        // simply ends at its wall (the author's ruling). Then the big props, each an exclusion for the small
        // ones and each keeping its own stated standoff from the road, and cover last, into whatever is left.
        var claims = new GroundClaims();
        var placed = new DressingPlacement();
        var structures = new List<StructureClaim>();
        var dropped = new List<DroppedProp>();

        foreach (var prop in context.Props.OfType<WaterProp>())
            placed = placed with { WaterCells = placed.WaterCells + PlaceWater(world, context, prop, claims) };
        foreach (var prop in context.Props.OfType<PathProp>())
            placed = placed with { PathCells = placed.PathCells + PlacePath(world, context, prop, claims) };
        foreach (var prop in context.Props.OfType<HouseProp>())
        {
            var raised = PlaceHouse(world, context, prop, claims, dropped);
            structures.AddRange(raised);
            placed = placed with { Houses = placed.Houses + raised.Count };
        }
        foreach (var prop in context.Props.OfType<BoulderProp>())
            placed = placed with { Boulders = placed.Boulders + PlaceBoulder(world, context, prop, claims, dropped) };
        foreach (var prop in context.Props.OfType<TreeProp>())
            placed = placed with { Trees = placed.Trees + PlaceTree(world, context, prop, claims, dropped) };
        foreach (var prop in context.Props.OfType<FloraProp>())
            placed = placed with { Plants = placed.Plants + PlaceFlora(world, context, prop, claims) };

        // Null rather than an empty list when nothing was raised or dropped, so a pass that placed a board
        // with nothing to report compares equal to a pass that was never asked to (the Finding.Subjects rule,
        // for the same reason).
        return placed with
        {
            Claimed = structures.Count > 0 ? structures : null,
            Dropped = dropped.Count > 0 ? dropped : null,
        };
    }

    // ── paths (DR-PA) ───────────────────────────────────────────────────────────
    /// <summary>Repaint the ground a stroke covers. A path adds no cell: it swaps the top block of each column
    /// it crosses, which is why it can run over a slope without becoming a ramp and why a bridge is still the
    /// draw phase's job. Its cells become bare ground, so nothing grows through the road.</summary>
    private static int PlacePath(VoxelWorld world, DressingContext context, PathProp path, GroundClaims claims)
    {
        if (path.Points.Count < 2) return 0;
        var placed = 0;
        var stroke = PathStroke.Cells(path.Points, path.Radius, path.Style, path.Coverage, path.Seed).ToList();

        for (var k = 0; k < context.Symmetry.Order; k++)
        foreach (var cell in stroke)
        {
            var (x, z) = context.Symmetry.ImageCell(cell.X, cell.Z, k);
            if (context.IsProtected(x, z)) continue;
            if (!context.SurfaceTop.TryGetValue((x, z), out var top) || top < 1) continue;

            // A stamp's own block is not a road's to take: the painter only writes terrain, so anything else
            // on top of a column belongs to something the map is played through.
            if (DressingPalette.IsStamp(world.GetBlock(x, top - 1, z).Id)) continue;

            // The pave is a full terrain material, resolved at the cell the way the painter resolves a surface,
            // so a cobbled road is a cell pattern at a small patch size rather than a mode of the stroke.
            var (id, data) = path.Pave.Resolve(new BucketContext(x, top - 1, z, TerrainBucket.Surface, 0));
            world.SetBlock(x, top - 1, z, id, data);
            claims.Claim(x, z, ClaimKind.Route);
            placed++;
        }
        return placed;
    }

    // ── water (DR-WA) ───────────────────────────────────────────────────────────
    /// <summary>Cut a channel and fill it. Water is the one prop that changes the ground rather than standing on
    /// it: laid flat it reads as blue paint, so it has to sit in a carved bed and fill to a level plane. The bed
    /// is a bowl deepest on the centerline; the fill is one water line across the whole run.
    ///
    /// <para><b>Only existing terrain is ever touched.</b> The carve runs from just above the bed floor up to the
    /// column's old surface and no higher, so nothing is written into what was already air — a channel dug across
    /// a hollow keeps the hollow. The water line is the lowest surface the channel crosses, which is what keeps
    /// the fill from floating above ground it did not cut: every column's surface is at or above the line, so
    /// every block written sits at or below terrain that was there before.</para></summary>
    private static int PlaceWater(VoxelWorld world, DressingContext context, WaterProp water, GroundClaims claims)
    {
        if (water.Points.Count < 2 || water.Radius <= 0 || water.Depth <= 0) return 0;
        var bed = WaterBed.Cells(water.Points, water.Radius, water.Depth, water.Form, water.Edge, water.Seed).ToList();
        if (bed.Count == 0) return 0;
        var shore = WaterBed.ShoreCells(water.Points, water.Radius, water.Form, water.Shore, water.Edge, water.ShoreWander, water.Seed).ToList();

        // The bank is a full terrain material, so the bed floor and the beach are a voronoi patchwork or any
        // pattern the painter offers, resolved cell by cell exactly as the painter resolves a surface.
        (int Id, int Data) Bank(int x, int y, int z) => water.Bank.Resolve(new BucketContext(x, y, z, TerrainBucket.Surface, 0));

        // The water first, every image: carve each bed and fill it to that image's own level line. The columns
        // it wets are remembered so the beach, which comes after, never lays sand over open water where the two
        // overlap across the symmetry fan.
        var placed = 0;
        for (var image = 0; image < context.Symmetry.Order; image++)
        {
            var cells = new List<(int X, int Z, int SurfaceSolid, int Depth)>(bed.Count);
            var waterLevel = int.MaxValue;
            foreach (var cell in bed)
            {
                var (x, z) = context.Symmetry.ImageCell(cell.X, cell.Z, image);
                if (context.IsProtected(x, z)) continue;
                if (!context.SurfaceTop.TryGetValue((x, z), out var top) || top < 2) continue;
                var surfaceSolid = top - 1;
                // Water no more takes a stamp's own block than a path does: the painter writes only terrain, so
                // anything else on a surface belongs to something the map is played through.
                if (DressingPalette.IsStamp(world.GetBlock(x, surfaceSolid, z).Id)) continue;

                cells.Add((x, z, surfaceSolid, cell.Depth));
                waterLevel = Math.Min(waterLevel, surfaceSolid);
            }
            if (cells.Count == 0) continue;

            foreach (var (x, z, surfaceSolid, depth) in cells)
            {
                var bedFloor = Math.Max(0, waterLevel - depth);
                // Take the material out and fill it: water up to the level line, air above it (a bank cut higher
                // than the water stands open, not roofed over). The loop never rises past the old surface, so it
                // only ever replaces terrain that was already there.
                for (var y = bedFloor + 1; y <= surfaceSolid; y++)
                    world.SetBlock(x, y, z, y <= waterLevel ? Blocks.StationaryWater : Blocks.Air);
                // The bank floor the shallows show through, laid where terrain already stood.
                if (bedFloor >= 1) { var (id, data) = Bank(x, bedFloor, z); world.SetBlock(x, bedFloor, z, id, data); }
                claims.Claim(x, z, ClaimKind.Water);
                placed++;
            }
        }

        // Then the beach, every image: the bank material on the surface just past the water, wherever the water
        // met carvable land. A shore column that a channel elsewhere already filled with water is left as water.
        for (var image = 0; image < context.Symmetry.Order; image++)
        foreach (var cell in shore)
        {
            var (x, z) = context.Symmetry.ImageCell(cell.X, cell.Z, image);
            if (claims.Holds(x, z) || context.IsProtected(x, z)) continue;
            if (!context.SurfaceTop.TryGetValue((x, z), out var top) || top < 1) continue;
            var surfaceSolid = top - 1;
            if (DressingPalette.IsStamp(world.GetBlock(x, surfaceSolid, z).Id)) continue;

            var (id, data) = Bank(x, surfaceSolid, z);
            world.SetBlock(x, surfaceSolid, z, id, data);
            claims.Claim(x, z, ClaimKind.Water);
        }
        return placed;
    }

    // ── flora (DR-FL) ───────────────────────────────────────────────────────────
    /// <summary>Grow cover inside a drawn area. One block per cell, in the air above the surface, and only
    /// where the paint beneath accepts it — a plant occupies its own cell and nothing around it, so it needs no
    /// local frame and no turning.</summary>
    private static int PlaceFlora(VoxelWorld world, DressingContext context, FloraProp area, GroundClaims claims)
    {
        if (area.Points.Count < 3) return 0;
        var ring = area.Points.Select(point => new[] { point[0], point[1] }).ToList();
        var placed = 0;

        for (var k = 0; k < context.Symmetry.Order; k++)
        foreach (var (x, z) in Inside(ring, context.Symmetry, k))
        {
            if (claims.Holds(x, z) || context.IsProtected(x, z)) continue;
            if (!context.SurfaceTop.TryGetValue((x, z), out var top)) continue;
            if (world.GetBlock(x, top, z).Id != Blocks.Air) continue;   // something is already there

            var (groundId, groundData) = world.GetBlock(x, top - 1, z);
            var share = DressingPalette.SoilShare(groundId, groundData);
            if (share <= 0) continue;

            if (PickPlant(area.Spec, area.Seed, x, z, share, context.Symmetry) is not { } plant) continue;
            // Tall grass is the one plant that is cover rather than colour, so it is the one the goal's own
            // ground turns away — the field simply grows its short cover there instead of skipping the cell,
            // which keeps the meadow continuous across a monument instead of ringing it with bare dirt.
            if (plant.Tall && !context.AllowsCover(x, z)) continue;
            world.SetBlock(x, top, z, plant.Id, plant.Data);
            if (plant.Tall && top + 1 < VoxelWorld.MaxHeight)
                world.SetBlock(x, top + 1, z, plant.Id, DressingPalette.DoublePlantUpper);
            placed++;
        }
        return placed;
    }

    // The cells of one image of a drawn ring. The ring is mirrored point by point rather than the cells being
    // mirrored one at a time, so an area's image is the same area seen from the other side.
    private static IEnumerable<(int X, int Z)> Inside(List<double[]> ring, DressingSymmetry symmetry, int image)
    {
        var turned = symmetry.ImageRing(ring, image);
        double minX = double.MaxValue, maxX = double.MinValue, minZ = double.MaxValue, maxZ = double.MinValue;
        foreach (var point in turned)
        {
            minX = Math.Min(minX, point[0]); maxX = Math.Max(maxX, point[0]);
            minZ = Math.Min(minZ, point[1]); maxZ = Math.Max(maxZ, point[1]);
        }
        for (var z = (int)Math.Floor(minZ); z <= (int)Math.Ceiling(maxZ); z++)
        for (var x = (int)Math.Floor(minX); x <= (int)Math.Ceiling(maxX); x++)
            if (Polygon.PointInRing(x + 0.5, z + 0.5, turned)) yield return (x, z);
    }

    /// <summary>What grows in one cell, or null for bare ground. Two fields decide it: a density field says
    /// whether anything grows at all — which is what turns an even speckle into meadows and clearings — and a
    /// second, coarser field paints flowers in <em>patches</em>, so an area gets fields of one colour rather
    /// than confetti.</summary>
    private static Plant? PickPlant(FloraSpec flora, uint seed, int x, int z, double soilShare, DressingSymmetry symmetry)
    {
        var density = PatternNoise.Fbm(x, z, seed, flora.Scale, flora.Octaves);
        if (density < 1 - flora.Coverage * soilShare) return null;

        // Tall cover hides a crouching player, so its cell is decided on the orbit representative — the same
        // ground is tall or bare for every team. The rest of the overlay decides nothing and stays free.
        if (flora.TallShare > 0)
        {
            var (rx, rz) = symmetry.Canonical(x, z);
            if (PatternNoise.Unit(rx, rz, seed + 61) < flora.TallShare)
                return PatternNoise.Unit(rx, rz, seed + 62) < flora.FernShare
                    ? DressingPalette.LargeFern : DressingPalette.TallGrass;
        }

        var flowerField = PatternNoise.Fbm(x, z, seed + 33, flora.FlowerScale, 2);
        if (flowerField > 1 - flora.FlowerShare && PatternNoise.Unit(x, z, seed + 44) < 0.7)
        {
            var pick = PatternNoise.Unit(x, z, seed + 88);
            return DressingPalette.Flowers[(int)(pick * DressingPalette.Flowers.Length) % DressingPalette.Flowers.Length];
        }
        return PatternNoise.Unit(x, z, seed + 21) < flora.FernShare
            ? DressingPalette.Fern : DressingPalette.Grass;
    }

    // ── boulders (DR-SC) ────────────────────────────────────────────────────────
    // ── buildings (DR-HO) ───────────────────────────────────────────────────────
    /// <summary>
    /// Raise a building on the rectangles its author dragged, at every image of its orbit — one or more touching
    /// wings composed into the one <see cref="BuildingPlan"/> <see cref="HouseStamper"/> takes, so an L or a T is
    /// stamped once as one house rather than once per rectangle (G177).
    ///
    /// <para><b>It is not gated on the protected mask, and it never joins it.</b> That mask exists to keep a
    /// scatter off the cells the map is played through — a flower field is generated, so it has to be told
    /// where not to grow. A building is not generated: someone drew this rectangle here, on purpose, and a
    /// refusal would silently drop a placement the author can see on the canvas.</para>
    ///
    /// <para><b>It is gated on what is already standing — except the road.</b> Its cells join the pass's
    /// running claims as <see cref="ClaimKind.Structure"/>, and a footprint that lands on a cell water or an
    /// earlier building holds is refused rather than raised through whatever got there first: two authored
    /// rectangles that overlap are two buildings colliding, and a building is no more owed the ground under
    /// an earlier one than a tree is owed the ground under a building. The one claim it does not check is
    /// <see cref="ClaimKind.Route"/> — paths are laid first and a road is meant to run to a porch, so a house
    /// over pavement stands and the road ends at its wall (the author's ruling).</para>
    ///
    /// <para>What it does need is ground, and that is physics rather than policy: it seats on the
    /// <b>lowest</b> column of its own footprint, so it settles into a slope rather than standing on stilts
    /// over the low side, and one course down, so the floor sinks into what it stands on the way a room's does.
    /// The rest of that sinking is the excavation: any terrain column rising above the floor inside the
    /// footprint is carved to air before the stamp, so a house on a hillside cuts into the slope with its
    /// rooms intact instead of the hill standing through them.</para>
    ///
    /// <para><b>Decided once for the whole orbit</b>, the same rule every other prop follows: every image has
    /// to have ground under it and no overlap with anything already standing before any of them is raised, so
    /// a building does not stand on one side of a mirrored map and leave a hole where the other team's copy
    /// should be.</para>
    /// </summary>
    private static List<StructureClaim> PlaceHouse(
        VoxelWorld world, DressingContext context, HouseProp house, GroundClaims claims,
        List<DroppedProp> dropped)
    {
        if (house.Plan() is not { } plan)
        {
            // Check() never disagrees with Plan()'s own refusal — Plan() is Check().Refuses ? null : Read() —
            // so the first finding here is always the reason this prop just failed to read.
            var findings = house.Check();
            dropped.Add(new DroppedProp(house.Id, "house", $"its wings make no building: {findings[0].Message}"));
            return [];
        }

        // A footprint below the floor is a wall with a roof, not a building — declined before any ground
        // is asked about, because no seat makes a 4-deep box a house.
        var footprintX = plan.MaxX - plan.MinX + 1;
        var footprintZ = plan.MaxZ - plan.MinZ + 1;
        if (Math.Min(footprintX, footprintZ) < DressingRules.FootprintMin)
        {
            dropped.Add(new DroppedProp(house.Id, "house",
                $"footprint {footprintX}×{footprintZ} is under {DressingRules.FootprintMin}×"
                + $"{DressingRules.FootprintMin} ({DressingRules.FootprintFloor})"));
            return [];
        }

        var images = new List<(BuildingPlan Plan, RoomEdge? Front, int FloorY)>(context.Symmetry.Order);
        for (var k = 0; k < context.Symmetry.Order; k++)
        {
            if (TurnedFootprint(plan, context.Symmetry, k) is not { } image) return [];
            // Two authored rectangles that overlap are two buildings colliding (still MG7's drop); a wing that
            // shares ground with its own prop's other wings is not a second building, and never reaches this
            // set at all — the whole point of the plan being one BuildingPlan rather than one FirstOverlap call
            // per rectangle. Reported once for the whole prop, at whichever image and cell collided first — an
            // author redrawing a second orbit image's clash does not need it named twice.
            if (FirstOverlap(image, claims) is { } collision)
            {
                dropped.Add(new DroppedProp(house.Id, "house",
                    $"ground already claimed at ({collision.X}, {collision.Z})"));
                return [];
            }
            var floorY = Ground(context, image);
            if (floorY is null)
            {
                dropped.Add(new DroppedProp(house.Id, "house", "no ground under any of its cells"));
                return [];
            }
            if (!HasPassage(context, claims, image))
            {
                dropped.Add(new DroppedProp(house.Id, "house",
                    $"no way past it: fewer than {DressingRules.PassAroundWidth} blocks of passable ground "
                    + $"beside every side ({DressingRules.PassAround})"));
                return [];
            }

            var front = house.Front is { } edge ? context.Symmetry.TurnEdge(edge, k) : (RoomEdge?)null;
            images.Add((image, front, floorY.Value));
        }

        var raised = new List<StructureClaim>(images.Count);
        for (var k = 0; k < images.Count; k++)
        {
            var (image, front, floorY) = images[k];
            Excavate(world, context, image, floorY);
            HouseStamper.Stamp(
                world, image, floorY, house.Style,
                doors: front is { } side ? Doorway(house.Style, image, side) : null);

            foreach (var (x, z) in image.Cells()) claims.Claim(x, z, ClaimKind.Structure);

            // The claim is made here, after the stamp and inside the same loop, so it cannot outlive a drop:
            // every early return above leaves this list empty and the caller claims nothing. The cells are the
            // stamped extent rather than the wall rectangle — a roof's overhang and verge reach past the walls
            // and are as much the building as they are — and they come from HouseStamper's own function, so
            // the claim and the stamp read one derivation instead of two that agree today.
            raised.Add(new StructureClaim($"house:{house.Id}:{k}", ClaimedCells(image, house.Style)));
        }
        return raised;
    }

    /// <summary>Every column one raised image of a building covers: each wing's own rectangle grown by what
    /// the style's roof reaches past its walls, unioned. The union of the wings rather than one box round the
    /// whole plan, because an L or a T has ground in its notch no eave reaches and claiming it would merge two
    /// buildings with clear ground between them into one structure.</summary>
    private static IReadOnlyList<(int X, int Z)> ClaimedCells(BuildingPlan image, HouseStyle style) =>
        [.. image.Wings
            .SelectMany(wing => HouseStamper.StampedCells((wing.MinX, wing.MinZ, wing.MaxX, wing.MaxZ), style))
            .Distinct()];

    /// <summary>The <paramref name="image"/>-th image of a plan: every wing's own two corners turned round the
    /// orbit and rebuilt as a <see cref="Wing"/>, so a quarter turn swaps each wing's width and depth exactly as
    /// a single-rectangle building's always did — the shape is turned one wing at a time, not merely relocated.
    /// Public so <c>DressingScope</c> reads the same turned plan a build actually stamps rather than re-deriving
    /// it from the corners a second way.
    ///
    /// <para><b>Everything the wing states about itself is carried, and its ridge is turned with it.</b> A wing
    /// keeps its storeys, its roof form, its pitch, its slab and its choice to project, because those are the
    /// building rather than its placement. The ridge axis is the one of them that a quarter turn <em>changes</em>
    /// — a ridge along x comes out along z — and dropping it is what would turn a T into two ranges side by
    /// side, since it is the crossing of two ridges that makes a junction at all.</para></summary>
    public static BuildingPlan TurnedFootprint(BuildingPlan plan, DressingSymmetry symmetry, int image)
    {
        var wings = new List<Wing>(plan.Wings.Count);
        foreach (var wing in plan.Wings)
        {
            var corners = symmetry.ImageRing(
                [[wing.MinX, wing.MinZ], [wing.MaxX, wing.MaxZ]], image);
            int minX = (int)Math.Floor(Math.Min(corners[0][0], corners[1][0]));
            int minZ = (int)Math.Floor(Math.Min(corners[0][1], corners[1][1]));
            int maxX = (int)Math.Floor(Math.Max(corners[0][0], corners[1][0]));
            int maxZ = (int)Math.Floor(Math.Max(corners[0][1], corners[1][1]));
            wings.Add(wing with
            {
                MinX = minX, MinZ = minZ, MaxX = maxX, MaxZ = maxZ,
                Spec = wing.Spec with { Ridge = TurnedRidge(wing.Ridge, symmetry, image) },
            });
        }
        return new BuildingPlan(wings);
    }

    /// <summary>A stated ridge axis under the same turn its wing takes, or null where the wing left it to its
    /// own proportions — which turn with it and so need no correction.</summary>
    private static RidgeAxis? TurnedRidge(RidgeAxis? ridge, DressingSymmetry symmetry, int image)
    {
        if (ridge is not { } axis) return null;
        var along = symmetry.TurnEdge(axis == RidgeAxis.AlongX ? RoomEdge.PosX : RoomEdge.PosZ, image);
        return along is RoomEdge.PosX or RoomEdge.NegX ? RidgeAxis.AlongX : RidgeAxis.AlongZ;
    }

    /// <summary>The first cell of a plan already claimed by something else, or null where nothing collides —
    /// the building half of MG7's overlap rule, tested over the whole union of its wings rather than a resting
    /// subset, because a building has no other level: the floor it stamps covers every column of it.</summary>
    private static (int X, int Z)? FirstOverlap(BuildingPlan plan, GroundClaims claims)
    {
        foreach (var (x, z) in plan.Cells())
            if (claims.HoldsOtherThan(x, z, ClaimKind.Route)) return (x, z);
        return null;
    }

    /// <summary>Whether a building leaves a way past itself: at least one of its four sides carries a band of
    /// passable ground <see cref="DressingRules.PassAroundWidth"/> blocks deep along its whole run — extended
    /// one step past each corner, because that step is where the passage turns in from, and it is exactly the
    /// cell that separates a flank a player can enter from a flank walled off at both ends. A house corking a
    /// leg fails all four: its flanks are void, and the ground beyond its gable ends fails the corner step.
    /// Passable is terrain with nothing <em>built</em> on it — a road or a channel alongside the wall is
    /// still a way past, an earlier building is not. Measured over the plan's bounding box: the notch of an L
    /// is the building's own ground, not a public route through it.</summary>
    private static bool HasPassage(DressingContext context, GroundClaims claims, BuildingPlan plan)
    {
        int minX = plan.MinX, minZ = plan.MinZ, maxX = plan.MaxX, maxZ = plan.MaxZ;
        var depth = DressingRules.PassAroundWidth;

        return Band(maxX + 1, maxX + depth, minZ - 1, maxZ + 1)      // east flank
            || Band(minX - depth, minX - 1, minZ - 1, maxZ + 1)      // west flank
            || Band(minX - 1, maxX + 1, maxZ + 1, maxZ + depth)      // south flank
            || Band(minX - 1, maxX + 1, minZ - depth, minZ - 1);     // north flank

        bool Band(int fromX, int toX, int fromZ, int toZ)
        {
            for (var z = fromZ; z <= toZ; z++)
            for (var x = fromX; x <= toX; x++)
            {
                if (!context.SurfaceTop.ContainsKey((x, z))) return false;
                if (claims.HoldsKind(x, z, ClaimKind.Structure)) return false;
            }
            return true;
        }
    }

    /// <summary>The course a building's floor sits at: one below the lowest ground its plan covers, or null
    /// where that plan has no ground at all.</summary>
    private static int? Ground(DressingContext context, BuildingPlan plan)
    {
        var lowest = int.MaxValue;
        foreach (var (x, z) in plan.Cells())
            if (context.SurfaceTop.TryGetValue((x, z), out var top)) lowest = Math.Min(lowest, top);
        return lowest == int.MaxValue || lowest < 2 ? null : lowest - 1;
    }

    /// <summary>Carve the terrain standing above a building's floor out of its footprint, before the stamp.
    /// A building seats on the lowest column of its plan, so on a hillside or a relief mark the higher ground
    /// runs straight through where its rooms will be — and the stamper deliberately never cuts terrain (air
    /// out of a material is a gap left open, not a hole punched), so without this the relief stands inside
    /// the house. The building wins the ground it was drawn on (the author's ruling, the same rule that keeps
    /// a tree from rooting in one): every footprint column is cleared from the floor's own course up to its
    /// old surface, so the house sinks into the slope with its interior intact. Only the wall plan is carved —
    /// the ground under the eaves is outside the building — and a column whose surface carries a stamp is left
    /// whole, the rule every pass keeps.</summary>
    private static void Excavate(VoxelWorld world, DressingContext context, BuildingPlan plan, int floorY)
    {
        foreach (var (x, z) in plan.Cells())
        {
            if (!context.SurfaceTop.TryGetValue((x, z), out var top)) continue;
            if (DressingPalette.IsStamp(world.GetBlock(x, top - 1, z).Id)) continue;
            for (var y = floorY + 1; y < top; y++)
            {
                if (y is < 1 or >= VoxelWorld.MaxHeight) continue;
                world.SetBlock(x, y, z, Blocks.Air);
            }
        }
    }

    /// <summary>The one doorway a chosen wall asks for: centred on the run of wall the plan actually has facing
    /// that way and clear of both corner posts, which is what the building would cut for itself on a long side.
    /// Stated as a door rather than left to the stamper because the stamper picks the <em>side</em> as well, and
    /// here the side is the author's. Reads the plan's own <see cref="WallSegment"/> rather than a width and a
    /// depth, since on a plan of more than one wing the chosen wall is not the whole of that side.</summary>
    private static IReadOnlyList<RoomDoor> Doorway(HouseStyle style, BuildingPlan plan, RoomEdge front)
    {
        var about = front.AlongX() ? (plan.MinX + plan.MaxX) / 2 : (plan.MinZ + plan.MaxZ) / 2;
        if (plan.WallFacing(front, about) is not { } wall) return [];

        // A block of wall clear of each corner post, the rule the stamper's own doors keep: an opening in the
        // very next cell still meets the post, and a door against the post reads as a hole knocked through the
        // frame. Only a run too tight to spare the margin falls back to the bare run between its corners.
        var (seatLo, seatHi) = wall.Seat;
        var (runLo, runHi) = wall.BetweenCorners;
        var (lo, hi) = seatHi >= seatLo ? (seatLo, seatHi) : (runLo, runHi);

        var width = Math.Clamp(style.Doorway.CutWidth, 1, hi - lo + 1);
        return [new RoomDoor(front, Math.Clamp(about - (width - 1) / 2, lo, hi - width + 1), width)];
    }

    private static int PlaceBoulder(
        VoxelWorld world, DressingContext context, BoulderProp boulder, GroundClaims claims,
        List<DroppedProp> dropped)
    {
        var lobes = BoulderShapes.Of(boulder.Form, boulder.Reach);
        return Fan(world, context, (boulder.X, boulder.Z), BoulderCells(lobes, boulder), claims, boulder.RouteStandoff, boulder.Id, "boulder", dropped);
    }

    /// <summary>A boulder as offsets from its own anchor, before it knows where on the map it goes. The rock's
    /// material and its mossy finish are both decided here, in the boulder's own frame, so a patterned or
    /// weathered rock stays the same rock at every image of its orbit — resolving either against map
    /// coordinates would give two teams the same shape in different colours.</summary>
    private static List<PropCell> BoulderCells(IReadOnlyList<BlobLobe> lobes, BoulderProp boulder)
    {
        var cells = new List<PropCell>();
        var (min, max) = Blob.Bounds(lobes);
        int top = (int)Math.Ceiling(max.Y);
        for (var y = (int)Math.Floor(min.Y); y <= top; y++)
        for (var z = (int)Math.Floor(min.Z); z <= (int)Math.Ceiling(max.Z); z++)
        for (var x = (int)Math.Floor(min.X); x <= (int)Math.Ceiling(max.X); x++)
        {
            if (!Blob.Contains(lobes, new Vec3(x, y, z), boulder.Seed)) continue;
            var lit = y >= 0 && !Blob.Contains(lobes, new Vec3(x, y + 1, z), boulder.Seed);
            var mossy = boulder.Mossy && lit && PatternNoise.Unit(x, z, boulder.Seed + 3) < 0.55;
            // Depth is measured down from the rock's own crust, not the map's surface, so a layer stack reads
            // as a weathered skin over a core rather than as the terrain bands it would name anywhere else.
            var depth = Crust(lobes, boulder, x, y, z, top);
            var (id, data) = mossy
                ? (MossyCobblestone, 0)
                : boulder.Rock.Resolve(new BucketContext(x, y, z, TerrainBucket.Surface, depth));
            // A cell below the anchor is buried and may replace ground; one above it must find air.
            cells.Add(new PropCell(x, y, z, id, data, Buried: y < 0));
        }
        return cells;
    }

    /// <summary>How many rock cells stand directly above this one — the depth a layered material reads.</summary>
    private static int Crust(IReadOnlyList<BlobLobe> lobes, BoulderProp boulder, int x, int y, int z, int top)
    {
        var depth = 0;
        while (y + depth + 1 <= top && Blob.Contains(lobes, new Vec3(x, y + depth + 1, z), boulder.Seed)) depth++;
        return depth;
    }

    // ── trees (DR-TR) ───────────────────────────────────────────────────────────
    private static int PlaceTree(
        VoxelWorld world, DressingContext context, TreeProp tree, GroundClaims claims,
        List<DroppedProp> dropped)
        => Fan(world, context, (tree.X, tree.Z), TreeCells(tree), claims, tree.RouteStandoff, tree.Id, "tree", dropped);

    /// <summary>How far this tree's crown actually reaches from its own trunk — the farthest a leaf cell of
    /// <see cref="TemplateTree"/> or <see cref="GrownTree"/> stands from the anchor, horizontally. This is the
    /// same deterministic build the stamp writes with (species/shape, seed, form), read for its geometry rather
    /// than its blocks, so a caller wanting the crown's true reach without a world to measure it — the
    /// point-and-radius foliage render (<c>docs/world-export/decoration.md</c> §6) — gets the honest figure
    /// rather than a species-nominal guess, at the cost of nothing more than the same generation the stamp was
    /// always going to run.</summary>
    public static double CanopyRadius(TreeProp tree)
    {
        var (_, leaves) = tree.Form == TreeForm.Template ? TemplateTree(tree) : GrownTree(tree);
        var farthest = 0.0;
        foreach (var (x, _, z) in leaves)
        {
            var reach = Math.Sqrt((double)x * x + (double)z * z);
            if (reach > farthest) farthest = reach;
        }
        return farthest;
    }

    /// <summary>A tree as offsets from the block it stands on. Both forms end here: each produces the cells
    /// that are wood and the cells that are leaf, and this turns those into blocks. Wood wins where they
    /// overlap, so a trunk is never hollowed by its own foliage, and every leaf carries the no-decay bit — a
    /// built map has no growing tree behind its crown, and without the flag the whole thing disappears shortly
    /// after the map loads.</summary>
    private static List<PropCell> TreeCells(TreeProp tree)
    {
        var (wood, leaves) = tree.Form == TreeForm.Template ? TemplateTree(tree) : GrownTree(tree);
        var timber = tree.Timber;
        var leafData = timber.LeafData | DressingPalette.LeafNoDecay;
        var logData = timber.LogData | DressingPalette.LogAllBark;

        var cells = new List<PropCell>(wood.Count + leaves.Count);
        foreach (var (x, y, z) in wood) cells.Add(new PropCell(x, y, z, timber.LogId, logData, Buried: false));
        foreach (var (x, y, z) in leaves)
        {
            if (wood.Contains((x, y, z))) continue;
            cells.Add(new PropCell(x, y, z, timber.LeafId, leafData, Buried: false));
        }
        return cells;
    }

    /// <summary>The vanilla tree: its species' proportions scaled to the prop's height.</summary>
    private static (HashSet<(int X, int Y, int Z)> Wood, IReadOnlyList<(int X, int Y, int Z)> Leaves) TemplateTree(TreeProp tree)
    {
        var built = TreeTemplate.Build(DressingPalette.SpeciesNamed(tree.Species).ShapeAt(tree.Reach), tree.Seed);
        return ([.. built.Wood], built.Leaves);
    }

    /// <summary>The grown tree: wood swept from the limb splines, leaves owned cluster by cluster.</summary>
    private static (HashSet<(int X, int Y, int Z)> Wood, IReadOnlyList<(int X, int Y, int Z)> Leaves) GrownTree(TreeProp tree)
    {
        var shape = tree.Shape;
        var grown = TreeSkeleton.Grow(shape, tree.Seed);
        var wood = new HashSet<(int X, int Y, int Z)>();
        foreach (var limb in grown.Limbs)
            foreach (var cell in SweptVolume.Sweep(limb.Path, limb.StartRadius, limb.EndRadius))
                wood.Add(cell);

        var clusters = TreeCrown.Clusters(grown.Tips, tree.LeafCluster, shape.Size, tree.Seed);
        var (min, max) = TreeCrown.Bounds(clusters);
        var leaves = new List<(int X, int Y, int Z)>();
        for (var y = (int)Math.Floor(min.Y); y <= (int)Math.Ceiling(max.Y); y++)
        for (var z = (int)Math.Floor(min.Z); z <= (int)Math.Ceiling(max.Z); z++)
        for (var x = (int)Math.Floor(min.X); x <= (int)Math.Ceiling(max.X); x++)
            if (TreeCrown.OwnerAt(clusters, new Vec3(x, y, z), tree.Seed) is not null) leaves.Add((x, y, z));
        // Nothing is placed that the tree does not hold: a leaf out of reach of the wood is a block floating
        // in the air, and the crown is emitted through the same reachability the corpus is gated on.
        return (wood, [.. TreeCrown.Rooted(leaves, wood)]);
    }

    // ── the shared prop pipeline ────────────────────────────────────────────────
    /// <summary>One block of a prop, in the prop's own frame: an offset from its anchor, the block it places,
    /// and whether it sits below the surface. <see cref="Buried"/> cells may replace ground — that is what
    /// makes a boulder emerge from the earth rather than rest on it — while the rest must find air.</summary>
    private readonly record struct PropCell(int X, int Y, int Z, int Id, int Data, bool Buried);

    private const int MossyCobblestone = 48;

    /// <summary>Stamp a prop at every image of its orbit, each turned by that image's own transform, and return
    /// how many landed. <b>Decided once for the whole orbit</b>, not once per image: every image has to seat
    /// before any of them is written, so the prop stands at all of them or none. A rock whose mirror lands a
    /// block nearer a protected column, or on ground the relief left slightly steeper, does not get to stand on
    /// one side of a mirrored board and vanish from the other — the difference a player would actually notice
    /// is not "the edges are a little thinner", it is "the two halves disagree".</summary>
    private static int Fan(VoxelWorld world, DressingContext context, (int X, int Z) site,
        List<PropCell> prop, GroundClaims claims, int routeStandoff, string id, string kind, List<DroppedProp> dropped)
    {
        if (prop.Count == 0) return 0;

        var images = new List<((int X, int Z) Anchor, List<PropCell> Turned, int BaseY)>(context.Symmetry.Order);
        for (var k = 0; k < context.Symmetry.Order; k++)
        {
            var anchor = context.Symmetry.ImageCell(site.X, site.Z, k);
            var turned = prop.Select(cell =>
            {
                var (tx, tz) = context.Symmetry.TurnCell(cell.X, cell.Z, k);
                return cell with { X = tx, Z = tz };
            }).ToList();

            // Decided once for the whole orbit, so the report is too: whichever image seats first refuses the
            // whole prop, and that is the one image and cell named — a second orbit image failing the same
            // way is not a second entry.
            if (!Seats(context, anchor, turned, claims, routeStandoff, out var baseY, out var declineReason))
            {
                dropped.Add(new DroppedProp(id, kind, declineReason));
                return 0;
            }
            images.Add((anchor, turned, baseY));
        }

        foreach (var (anchor, turned, baseY) in images)
            foreach (var cell in turned)
            {
                var (wx, wy, wz) = (anchor.X + cell.X, baseY + cell.Y, anchor.Z + cell.Z);
                if (wy is < 1 or >= VoxelWorld.MaxHeight) continue;
                if (!cell.Buried && world.GetBlock(wx, wy, wz).Id != Blocks.Air) continue;
                world.SetBlock(wx, wy, wz, cell.Id, cell.Data);
                claims.Claim(wx, wz, ClaimKind.Scatter);
            }
        return images.Count;
    }

    /// <summary>Whether a prop can stand at an anchor, and the Y its own origin sits at — plus, where it
    /// cannot, one sentence naming which resting cell failed and why, for <see cref="Fan"/> to report against
    /// the prop that asked.
    ///
    /// <para>One footprint decides all of it. Ground, occupancy and protection are every one of them asked
    /// only where the prop <b>rests</b> — the cells at its lowest level, a trunk's few blocks or a boulder's
    /// buried underside — and it seats on the lowest column among those, so it sits into a slope rather than
    /// floating over its low side. A resting cell that is already claimed by whatever stood here first — a
    /// building, an earlier boulder, another tree's own trunk — refuses the whole image, which is what stops a
    /// trunk growing through a wall rather than merely losing the handful of blocks the wall happens to cover.
    /// The road reaches one step further than occupancy: a prop whose kind states a
    /// <see cref="PlacedProp.RouteStandoff"/> also refuses when a resting cell stands nearer than that many
    /// blocks to the nearest paved cell, so a trunk keeps off the kerb and not merely off the pavement.
    /// A resting cell over something the map is played through refuses it the same way: a trunk on a spawn or
    /// a monument is the fault a wall clipping through the room is.</para>
    ///
    /// <para>What is above may overhang nothing at all — neither ground, nor an earlier prop, nor protection.
    /// Demanding a clear column under a whole volume would ban every tree from a shoreline or an island edge,
    /// would ban two canopies from ever touching (which real cover does constantly), and would make a tree
    /// taller by refusing it: a grown crown widens with height, so testing the whole volume against protection
    /// empties a forest exactly where the objectives it should be covering stand. A canopy overhanging a
    /// monument at y+15 is not a trunk grown through it — a hand-built map's trees overhang its structures
    /// too — so the crown is free to reach wherever it would over open ground.</para></summary>
    private static bool Seats(
        DressingContext context, (int X, int Z) anchor, List<PropCell> prop, GroundClaims claims,
        int routeStandoff, out int baseY, out string declineReason)
    {
        baseY = int.MaxValue;
        declineReason = "";
        var restsAt = prop.Count == 0 ? 0 : prop.Min(cell => cell.Y);
        foreach (var cell in prop)
        {
            if (cell.Y != restsAt) continue;
            var ground = (X: anchor.X + cell.X, Z: anchor.Z + cell.Z);
            if (context.IsProtected(ground.X, ground.Z))
            {
                declineReason = $"the column at ({ground.X}, {ground.Z}) is protected";
                return false;
            }
            if (claims.Holds(ground.X, ground.Z))
            {
                declineReason = $"ground already claimed at ({ground.X}, {ground.Z})";
                return false;
            }
            if (routeStandoff > 0 && claims.NearerThan(ground.X, ground.Z, ClaimKind.Route, routeStandoff) is { } road)
            {
                declineReason = $"({ground.X}, {ground.Z}) is nearer than {routeStandoff} blocks to the road "
                    + $"at ({road.X}, {road.Z}) ({DressingRules.RoadStandoff})";
                return false;
            }
            if (!context.SurfaceTop.TryGetValue(ground, out var top))
            {
                declineReason = $"no ground at ({ground.X}, {ground.Z})";
                return false;
            }
            baseY = Math.Min(baseY, top);
        }
        return baseY != int.MaxValue;
    }
}
