using PgmStudio.Domain;
using PgmStudio.Geom.Algorithms;

namespace PgmStudio.Minecraft;

/// <summary>
/// Stamps a house: a sill, four corner posts with walls between them, windows through those walls, a roof of
/// whichever form the style asks for, a doorway through one side, and — where the style gives a strip of the
/// footprint up for one — a porch fronting it.
///
/// <para><b>The roof is a height field, and every form is one formula over it</b> (<see cref="RoofField"/>).
/// The stamper walks the roof's plan once, asks each cell what course it tops out at and how many courses it
/// has to write to close the step down to its neighbours, and lays them. A flat lid, a gable, a hip, a gambrel,
/// a shed and a saltbox are the same loop; only the answer differs. What used to be the gable's own end-wall
/// pass generalizes with it: <b>the walls climb to meet the roof</b> wherever the roof stands above them, which
/// is the gable's two ends, the shed's back wall and triangular flanks, and nothing at all under a hip.</para>
///
/// <para>Nothing is written outside the footprint plus its overhang, and nothing below the sill, so a house may
/// be stamped onto finished terrain without reaching into it — with one exception a style has to ask for: the
/// log ends of a <see cref="BeamStyle"/> run a block past each corner, which is what makes them read as beams
/// rather than as a course of the wall.</para>
/// </summary>
public static class HouseStamper
{
    /// <summary>The full rectangle a house stamped over <paramref name="ground"/> in this <paramref name="style"/>
    /// actually writes into — never <paramref name="ground"/> itself, which is only the walls. Three things
    /// reach past it and this is their union: the roof's own <see cref="HouseStyle.Overhang"/>, the log ends a
    /// <see cref="BeamStyle"/> runs past every corner, and the one-block sill that rings every footprint
    /// regardless of either. A porch reaches no further than this — its own canopy overhangs the strip the walls
    /// gave up by the same <see cref="HouseStyle.Overhang"/>, and that strip is already inside <paramref
    /// name="ground"/>, so its reach never exceeds the main roof's.
    ///
    /// <para>This is the number a caller wanting to claim the ground a stamped house actually covers
    /// (<c>DressingScope.StructureFootprints</c>) reads, in place of the wall rectangle alone — the wall
    /// rectangle is what a plan piece or a dragged rectangle hands the stamper, not what the stamper writes.
    /// <c>HouseStamperTests</c> proves every block <see cref="Stamp(VoxelWorld,Footprint,int,HouseStyle,int,
    /// IReadOnlyList{RoomDoor})"/> actually places lands inside it, so a caller answering from the style's own
    /// fields never has to re-open the voxels to check.</para></summary>
    /// <para>This is a <b>bound</b> and not a claim. A beam runs out from a corner only, so a rectangle wide
    /// enough to hold the longest one also spans the middle of every edge, where nothing is written — and one
    /// such phantom course is enough to touch the next building and merge two findings into one. What a caller
    /// recording provenance wants is <see cref="StampedCells"/>, the union this rectangle is merely the
    /// envelope of.</para></summary>
    public static (int MinX, int MinZ, int MaxX, int MaxZ) StampedExtent(
        (int MinX, int MinZ, int MaxX, int MaxZ) ground, HouseStyle style)
    {
        var margin = Math.Max(1, Math.Max(Math.Max(0, style.Overhang), BeamReach(style)));
        return (ground.MinX - margin, ground.MinZ - margin, ground.MaxX + margin, ground.MaxZ + margin);
    }

    /// <summary>How far past a corner this style's beams run, or 0 for a building without them. Mirrors
    /// <see cref="LayBeams"/>'s own floor of one step, so the two cannot drift.</summary>
    private static int BeamReach(HouseStyle style) =>
        style.Beams.Any ? Math.Max(1, style.Beams.Reach) : 0;

    /// <summary>The cells a caller recording provenance claims for a stamp of this style over
    /// <paramref name="ground"/>: the wall plan grown by the roof's oversail, which is a true rectangle
    /// because a roof covers all four sides.
    ///
    /// <para><b>A beam's arm is deliberately outside this, and it is a claim about ground rather than about
    /// geometry.</b> <see cref="StampedExtent"/> bounds the arms because a bound must; a claim may not, for
    /// two reasons that compound. A box wide enough for the longest arm also spans the middle of every edge,
    /// where nothing is written — and one phantom course is enough to touch the next building and merge two
    /// findings into one. Claiming the arms themselves is narrower and still wrong in practice: on a village
    /// row of eleven houses, every cell an arm would have claimed held plain paving at every height, because
    /// what a style's <c>Reach</c> asks for and what <see cref="LayBeams"/> leaves standing after the roof is
    /// laid over it are not the same number. So the ring is claimed and the arms are not. The cost is a beam
    /// end reading by material — a log at a corner classed as foliage — and the cost of the alternative is two
    /// buildings losing their separate identities, which is the more expensive of the two by far.</para>
    /// </summary>
    public static IEnumerable<(int X, int Z)> StampedCells(
        (int MinX, int MinZ, int MaxX, int MaxZ) ground, HouseStyle style)
    {
        var eaves = Math.Max(1, Math.Max(0, style.Overhang));
        for (var x = ground.MinX - eaves; x <= ground.MaxX + eaves; x++)
            for (var z = ground.MinZ - eaves; z <= ground.MaxZ + eaves; z++)
                yield return (x, z);
    }

    /// <summary>Stamp a house over a resolved <see cref="RoomFrame"/> — its footprint, and its doors on the
    /// edges the frame carries. A spawn's door sits on the yaw-derived edge, which is the way players face
    /// walking out, and a wool structure takes one per entry interface; neither is something a building can
    /// work out from its own proportions, so where a frame says, the frame wins.</summary>
    public static void Stamp(VoxelWorld world, RoomFrame frame, int floorY, HouseStyle style, int color = -1)
        => Stamp(world, frame.MinX, frame.MinZ, frame.Width, frame.Depth, floorY, style, color, frame.Doors);

    /// <summary>Stamp a house whose walls occupy <paramref name="width"/> x <paramref name="depth"/> blocks
    /// with their south-west corner at <paramref name="minX"/>/<paramref name="minZ"/>, standing on
    /// <paramref name="floorY"/> — the course a player walks on inside it. <paramref name="color"/> rides the
    /// tint channel for a team-tinted material, exactly as a room's does. With no
    /// <paramref name="doors"/> the house cuts one of its own through the middle of a long side.</summary>
    public static void Stamp(VoxelWorld world, int minX, int minZ, int width, int depth, int floorY,
                             HouseStyle style, int color = -1, IReadOnlyList<RoomDoor>? doors = null)
        => Stamp(world, new Footprint(minX, minZ, minX + width - 1, minZ + depth - 1), floorY, style, color,
                 doors);

    /// <summary>Stamp a house over a <paramref name="ground"/> plan of any shape, standing on
    /// <paramref name="floorY"/> — the course a player walks on inside it. Everything below the eave reads the
    /// plan itself, so a building of more than one wing gets its sill, its floor, its walls, its windows and its
    /// doorways on its own outline.
    ///
    /// <para>The roof is <b>one field over the plan's bounding box</b> and a porch is refused on a plan of more
    /// than one wing (G172). Both want a wing at a time — a building's roof is the union of the wing volumes —
    /// so until that lands, a plan that turns a corner is roofed as though it did not.</para></summary>
    public static void Stamp(VoxelWorld world, Footprint ground, int floorY, HouseStyle style,
                             int color = -1, IReadOnlyList<RoomDoor>? doors = null)
    {
        // No room for two walls and an inside: a plan whose every cell is wall is a plan with no room in it,
        // which is the same refusal a span under three blocks used to be and holds whatever shape it is.
        if (!ground.Cells().Any(cell => ground.Ring(cell.X, cell.Z) >= 1)) return;

        var front = style.Porch?.Edge ?? style.DoorEdge ?? FrontEdge(doors, ground);
        var (body, deck) = SplitPorch(ground, style.Porch, front);
        var doorHeight = Math.Min(
            doors is { Count: > 0 } ? style.DoorHeight : Math.Max(3, style.DoorHeight),
            Math.Max(1, style.Levels[0].Headroom));
        // The run of wall the building fronts on: what its own door is cut through, and what the way up stands
        // against. A rectangle has one run per side, so this is that side; a plan that turns a corner has to
        // pick, and the rule is on the plan.
        var frontWall = body.WallFacing(front, FrontCentre(body, front));
        var openings = Doorways(doors, style, body, frontWall);

        var overhang = Math.Max(0, style.Overhang);
        var pitch = Math.Max(1, style.Pitch);
        var levels = style.Levels;
        var bases = style.LevelBases;
        var wallTop = floorY + style.WallCourses;                  // the eave course sits on this
        var topWall = levels[^1].Wall ?? style.Wall;               // what a gable end climbs in
        var groundWall = levels[0].Wall ?? style.Wall;             // what a porch post falls back to
        var groundPost = levels[0].Post ?? style.Post;             // and what it takes where there is one

        // Air resolved out of a material is a gap left open, never a hole punched in what is already there:
        // a stack whose fourth course is air is a light slit, and skipping it keeps the pass from erasing a
        // stamp underneath. Only a doorway and a window write air, because those are openings cut on purpose.
        void Put(int x, int y, int z, TerrainMaterial material, Footprint ring, int depth = 0)
        {
            if (y is < 1 or >= VoxelWorld.MaxHeight) return;
            // A wall is a closed ring, so a pattern reads it exactly as it reads a plateau's outer edge: the
            // arc carries a wall-run's stripes round every corner, and the turn tells a frame where a corner
            // is. Without them every cell of the wall answers as arc 0 and a striped wall comes out flat.
            var arc = ring.Arc(x, z);
            var (id, data) = material.Resolve(new BucketContext(
                x, y, z, TerrainBucket.Fill, depth, color, arc, 0, ring.Turn(arc), ring.Run(x, z)));
            if (id == Blocks.Air) return;
            world.SetBlock(x, y, z, id, data);
        }

        void PutPart(int x, int y, int z, RoomPart part, int step, Footprint ring)
        {
            var (material, depth) = part.At(step);
            Put(x, y, z, material, ring, depth);
        }

        // ── the foundation ────────────────────────────────────────────────────────────────────────────
        // The sill rings the whole footprint one block proud and the floor claims downward from the course
        // players stand on — both over the ground the house was given, not over what its walls kept, so a
        // porch stands on the same footing and the same floor as the room behind it.
        for (var x = ground.MinX - 1; x <= ground.MaxX + 1; x++)
            for (var z = ground.MinZ - 1; z <= ground.MaxZ + 1; z++)
            {
                if (!ground.Holds(x, z))
                {
                    // One block proud of the building rather than everything the box holds: on a plan that
                    // turns a corner the two are different, and a sill filling the notch is a doorstep across
                    // ground the house never stood on.
                    if (ground.Borders(x, z)) Put(x, floorY, z, style.Sill, ground);
                    continue;
                }
                for (var step = 0; step < Math.Max(1, style.Floor.Extent); step++)
                    PutPart(x, floorY - step, z, style.Floor, step, ground);
            }

        // The floor's own top course, zoned across the room it belongs to. The porch keeps the floor part
        // showing: a deck is what the building stands on, not a room to lay a border round.
        if (!style.Surface.IsPlain)
            foreach (var (x, z) in body.Cells())
                if (style.Surface.At(body.Ring(x, z)) is { } surface)
                    Put(x, floorY, z, surface, body);

        // ── the roof ──────────────────────────────────────────────────────────────────────────────────
        // <b>A building's roof is the union of its wings' roofs</b>, and never a max of their crowns: a max
        // blends two surfaces into one and drags roof material down the wall between wings of unequal height.
        // Each wing is extruded as the whole building it would be alone — its own rectangle, its own eave from
        // its own storey count, its own ridge axis from its own proportions — and the volumes are simply laid
        // one after another. A wing's roof reaches its own walls plus its own overhang and no further, which is
        // what keeps a stub of roof from hanging outside a wall it never touched.
        //
        // <b>A projecting wing's roof is longer than its walls.</b> Marching and projecting are the two things
        // a wing can do where it runs into a hall, and only the first stops at the seam: a projection carries
        // the wing's roof across the hall to the hall's far wall and shows a second gable over it. The walls
        // stay where the author drew them — the hall's far wall is the hall's, and the gable simply stands on
        // it — so the extension is the roof's plan alone, taken along the wing's own ridge, which is the axis a
        // gable's rise is not measured over and so lengthens the roof without raising it.
        var joints = WingJoints.Of(body);
        var roofs = body.Wings.Select((wing, index) =>
        {
            var top = WallTopOf(wing);
            var (minX, minZ, maxX, maxZ) = Roofed(wing, index);
            var roofed = new Footprint(minX, minZ, maxX, maxZ);
            var field = new RoofField(
                wing.FormOr(style.Form), minX, minZ, maxX, maxZ, overhang, top + 1,
                wing.PitchOr(pitch), front, wing.SlabOr(style.RoofSlab) >= 0, wing.RidgeAlongX);
            return (Wing: wing, Index: index, Roofed: roofed, Field: field, Top: top,
                    Slab: wing.SlabOr(style.RoofSlab));
        }).ToList();
        var hole = RoofHole(style, body);
        var marched = Marched();

        foreach (var (wing, index, _, field, _, slab) in roofs)
            for (var x = field.MinX; x <= field.MaxX; x++)
                for (var z = field.MinZ; z <= field.MaxZ; z++)
                {
                    if (hole is { } gap && gap.Holds(x, z)) continue;
                    // A marching end carries no overhang of its own: the march is what fills those columns,
                    // stepping them on into the other roof, and a course that met its own eave first would
                    // strike a block at the first step and never move.
                    if (BeyondMarch(wing, index, x, z)) continue;
                    // <b>Only the highest roof over a cell is written there.</b> The union of two wings is the
                    // outside of both volumes, so where their plans overlap the lower surface is inside the
                    // higher one — and a roof block inside a building is not a roof, it is an obstruction in
                    // the attic. This is what cuts the roof a projecting wing pushes into, across its own span:
                    // the hall's eave course stops at the wing's opening rather than running over the room
                    // behind it, and the two attics are one space. It is not a max of crowns — no surface is
                    // blended and no wing's field is touched; each still answers for itself, and this decides
                    // only which of them is the one showing.
                    //
                    // The same comparison is what keeps a gable's overhang open. A verge climbs, so the cells
                    // under it are air; an eave does not, so its overhang is a solid course. Where a wing's
                    // eave overhang runs into the column another wing's verge overhangs, the verge stands
                    // higher and the eave gives way — which it must, since an eave under a verge has no slope
                    // above it to catch anything from.
                    if (Overtopped(wing, field, x, z)) continue;
                    Lay(field, x, z, body, slab);
                }


        // <b>The march.</b> Where a wing's gable end runs up against a hall rather than into the open, its roof
        // does not simply stop at the wall: each course steps on along its own ridge into the other roof until
        // that roof already stands as tall, and stops there. The courses nearest the ridge travel furthest and
        // the ones nearest the eave stop at once, which is what draws the crossing as a diagonal valley rather
        // than as a wing abutting a wall. No overhang is carried in — an overhang is what a roof has outside a
        // wall, and inside another wing there is no outside — so the march runs across the wing's own width
        // alone.
        //
        // <b>Which ends march is the joint model's answer, not a probe's</b> (<see cref="WingJoints"/>). An end
        // marches where a hall abuts it over its whole width and the wing has not asked to project through
        // instead; an end with open ground beyond it closes the building. Asking the rectangles one cell at a
        // time cannot tell those apart from a partial touch, which answers yes and then builds a seam down half
        // of a wall.
        //
        // <b>A course also never marches further than its own distance from its own eave.</b> That bound does
        // not depend on meeting anything: a course this many blocks from its own wall is exactly as far as its
        // own roof plane would still be climbing were nothing there to meet, so it cannot need more room than
        // that to find a taller surface. Without it a course whose crown never meets one — a steeper wing over a
        // shallower hall, or any wing over a flat one, which has no rising surface to meet at all — marches
        // clean across whatever it crosses and comes out its far overhang, the shape of a wing drawn through
        // rather than one that stopped at a wall.
        foreach (var (_, index, _, field, _, slab) in roofs)
            foreach (var (x, z) in marched[index].Keys)
                Lay(field, x, z, body, slab);

        // ── the walls ─────────────────────────────────────────────────────────────────────────────────
        // Storey by storey, each counting its own courses up from its own floor — so a band written at a
        // storey's fourth course is at the fourth course of every storey, and a taller ground floor does not
        // slide the one above it. The four corners take their own material.
        for (var level = 0; level < levels.Count; level++)
        {
            if (body.At(level) is not { } plan) break;          // no wing stands this high
            var storey = levels[level];
            var wall = storey.Wall ?? style.Wall;
            var corner = storey.Post ?? style.Post;
            for (var course = 1; course <= storey.Courses(level == levels.Count - 1); course++)
                foreach (var (x, z) in plan.Cells())
                    {
                        if (!plan.OnPerimeter(x, z)) continue;
                        var y = floorY + bases[level] + course;
                        // Both kinds of turn take the post: the ones the building turns away at, and the one
                        // where two wings meet, which is a corner the walls running into it turn at even
                        // though neither runs through it.
                        var turns = plan.OnCorner(x, z) || plan.OnInnerCorner(x, z);
                        if (turns && corner is { } post) Put(x, y, z, post, plan);
                        else PutPart(x, y, z, wall, course - 1, plan);
                    }
        }

        // Windows, storey by storey and in each storey's own frame — a sill of two is two blocks above *this*
        // floor whichever storey it is. The seater takes a sill and a wall height and asks nothing about which
        // storey it is seating, so a storey is simply a smaller wall to it. Only the ground storey knows about
        // the doorway; there is no door to avoid on the ones above.
        for (var level = 0; level < levels.Count; level++)
        {
            if (body.At(level) is not { } plan) break;
            var storey = levels[level];
            var windows = storey.Windows ?? style.Windows;
            var seats = HouseWindows.Seats(
                windows, plan.Segments, storey.Headroom, level == 0 ? openings : null,
                Hosts(plan, storey.Wall ?? style.Wall, windows, bases[level]));
            foreach (var seat in seats)
                HouseWindows.Cut(world, seat with { Sill = seat.Sill + bases[level] }, windows, floorY);
        }

        StampLevels();

        // Whether the wall at one cell of one storey resolves to the block a window wants to be cut into. The
        // wall is asked rather than inspected: it is resolved exactly as the pass that laid it resolved it,
        // same course, same arc, same run — so whatever pattern put a block there, a window finds it.
        Func<WallSegment, int, bool>? Hosts(Footprint plan, RoomPart wall, WindowStyle windows, int storeyBase)
        {
            if (windows.HostBlock < 0) return null;
            var course = Math.Max(1, windows.Sill);
            var (material, depth) = wall.At(course - 1);
            return (segment, along) =>
            {
                var (x, z) = segment.Cell(along);
                var arc = plan.Arc(x, z);
                var (id, data) = material.Resolve(new BucketContext(
                    x, floorY + storeyBase + course, z, TerrainBucket.Fill, depth, color,
                    arc, 0, plan.Turn(arc), plan.Run(x, z)));
                return id == windows.HostBlock && data == windows.HostData;
            };
        }

        // The walls climb to meet the roof over them: a gable's two ends, a shed's back wall and its triangular
        // flanks, and nothing under a hip, whose slopes already come down to the wall line on every side. Each
        // wing climbs to its own roof from its own eave, so a wing that stops lower closes against its own
        // slope rather than against the tallest one in the building. What they climb in is the gable's own
        // material where the style names one, else the wall's top course carried up — the wall's stack has
        // nothing left to say by then, since its courses ran out at the wall's top.
        //
        // <b>Walls outrank roofs</b>, which is why this runs after every volume is laid: a wing standing
        // against another has the other's slope written over it otherwise.
        // <b>A face rises only where the building is outside it.</b> A wing's own rectangle has four sides, and
        // the one standing against a neighbour is not one of them — it is a doorway between two halves of the
        // same building, open at the storey and open above it. Filled anyway it walls the wing's attic off from
        // the hall's, and a building whose roof reads as one shape from outside comes out with two sealed lofts
        // inside it. So the perimeter asked here is the <b>body's</b>, not the wing's: the same outline the
        // walls themselves were built on.
        foreach (var (_, _, roofed, field, top, _) in roofs)
            foreach (var (x, z) in roofed.Cells())
            {
                if (!body.OnPerimeter(x, z)) continue;
                for (var fill = top + 1; fill < field.Underside(x, z); fill++)
                    if (style.Gable is { } gable) Put(x, fill, z, gable, body);
                    else PutPart(x, fill, z, topWall, topWall.Extent - 1, body);
            }

        foreach (var (_, _, roofed, field, top, _) in roofs) StampGableWindows(roofed, field, top);
        StampDoors();

        if (deck is { } porchDeck && style.Porch is { } porchStyle) StampPorch(porchDeck, porchStyle);
        return;

        // ── the window in the gable ───────────────────────────────────────────────────────────────────
        // One per face and centred, where a wall takes as many as its run will hold. A gable is a triangle:
        // its height runs out toward both ends, so the middle is the one place a window certainly fits, and
        // spreading them along the run would put half of them where there is no wall to cut.
        void StampGableWindows(Footprint alone, RoofField field, int top)
        {
            var windows = style.GableWindows;
            if (windows.Form == WindowForm.None) return;
            var (width, height) = windows.Normalized();
            var sill = Math.Max(1, windows.Sill);

            foreach (var wall in alone.Segments)
            {
                var (lo, hi) = wall.Seat;                            // a block clear of both corners
                var start = lo + (hi - lo + 1 - width) / 2;          // centred the way a wall's windows are
                if (start < lo || start + width - 1 > hi) continue;

                // <b>The gable has to keep material round the opening.</b> A triangle narrows as it rises, so
                // testing only the cells the window occupies lets a window that technically lands on gable run
                // hard into the slope above it and leave the face as a hole with a rim — which reads as damage
                // rather than as a window. Every course the opening takes must therefore have gable a cell
                // either side of it as well, which is what makes a small gable admit only a small window and a
                // hip, having no gable at all, admit none.
                var fits = true;
                for (var course = sill; course < sill + height && fits; course++)
                    for (var along = start - 1; along <= start + width && fits; along++)
                    {
                        var (x, z) = wall.Cell(along);
                        fits = along >= lo - 1 && along <= hi + 1
                               && field.Underside(x, z) > top + course;
                    }
                if (!fits) continue;

                // Seated from the wall top, which is the course the gable starts at and the datum an author is
                // looking at when they place one — the floor is storeys away by then.
                HouseWindows.Cut(
                    world, new WindowSeat(wall, start, width, sill, height), windows, top);
            }
        }

        // ── the storeys above the ground ──────────────────────────────────────────────────────────────
        // Each storey but the last carries a slab over it, and each slab a way through: a hole with a ladder
        // standing in it. Without one an upper storey is a sealed volume — a building with a room nobody can
        // reach, which is a picture of a house rather than a house.
        void StampLevels()
        {
            // One cell for the whole stack, so the way up is a single shaft rather than a ladder that moves
            // from storey to storey and leaves a player to find the next one. It is seated against the front
            // of the <b>topmost</b> storey's plan, because a plan only ever loses wings on the way up: a cell
            // inside the highest storey is inside every storey under it, and one chosen on the ground could be
            // under open sky two floors later.
            var top = TopPlan();
            var climb = top?.WallFacing(front, FrontCentre(top, front)) is { } wall
                ? LadderCell(wall, openings)
                : default((int X, int Z)?);

            for (var level = 0; level < levels.Count - 1; level++)
            {
                // The slab is the floor of the storey above as much as the ceiling of this one, so it is laid
                // over that storey's plan: a wing that stops here is closed by its roof, not by a floor for a
                // room nobody built.
                if (body.At(level + 1) is not { } plan) break;
                var storey = levels[level];
                var slabY = floorY + bases[level] + storey.Courses(topmost: false);

                // Across the interior only, since the perimeter is wall. Its top course is zoned by the storey
                // above it, so an upper floor takes a border and an inlay exactly as the ground one does.
                var above = levels[level + 1];
                var ceiling = storey.Ceiling ?? style.Floor.At(0).Material;
                foreach (var (x, z) in plan.Cells())
                {
                    if (plan.OnPerimeter(x, z)) continue;         // the perimeter is wall
                    if (climb == (x, z)) continue;                // the way up
                    var surface = above.Surface?.At(plan.Ring(x, z));
                    Put(x, slabY, z, surface ?? ceiling, plan);
                }

                // The ladder: it stands in the storey below the hole and reaches the floor above, so a player
                // steps off it onto the new floor rather than into its underside.
                if (climb is { } shaft)
                    for (var y = floorY + bases[level] + 1; y <= slabY; y++)
                        if (y is > 0 and < VoxelWorld.MaxHeight)
                            world.SetBlock(shaft.X, y, shaft.Z, Blocks.Ladder, LadderFacing(front));

                LayBeams(plan, slabY);
            }
        }

        // The log ends that run out past the corners where two storeys meet. In plan the seam reads as a hash:
        // the walls are its middle and two ends carry on outward from every corner the building turns away at.
        // Each shows its sawn end, which is the one place on a building where a cut face outward is the point —
        // it is the end of a log, and a log building leaves them long.
        // The highest storey any wing still stands at, which on a building of equal wings is the whole plan.
        Footprint? TopPlan()
        {
            for (var level = levels.Count - 1; level > 0; level--)
                if (body.At(level) is { } plan) return plan;
            return body;
        }

        void LayBeams(Footprint plan, int y)
        {
            if (!style.Beams.Any || y is < 1 or >= VoxelWorld.MaxHeight) return;
            var reach = Math.Max(1, style.Beams.Reach);
            var wood = style.Beams.Data & 3;

            foreach (var (cornerX, cornerZ) in plan.Cells().Where(cell => plan.OnCorner(cell.X, cell.Z)))
            {
                // Out of the building on both axes, which at a corner is the side each axis has no wall on.
                var awayX = plan.Holds(cornerX - 1, cornerZ) ? 1 : -1;
                var awayZ = plan.Holds(cornerX, cornerZ - 1) ? 1 : -1;
                for (var step = 1; step <= reach; step++)
                {
                    // Lying along the axis it runs out on, so the face pointing away from the building is the
                    // sawn one. Data 4 is the x axis and 8 the z axis, the same nibbles a laid log takes.
                    world.SetBlock(cornerX + awayX * step, y, cornerZ, style.Beams.Block, wood | 4);
                    world.SetBlock(cornerX, y, cornerZ + awayZ * step, style.Beams.Block, wood | 8);
                }
            }
        }

        // One column of a roof: its tread and whatever riser it needs under it, bordered on the roof's own
        // outermost ring and capped along the ridge when the style asks for a capped one.
        void Lay(RoofField field, int x, int z, Footprint ring, int slabBlock)
        {
            var crown = field.Crown(x, z);
            // <b>The rim is the building's, not the wing's.</b> A verge and an eave are the outer edge of a
            // roof, and a building of several wings has one outline however many rectangles drew it — so a cell
            // is rim where the roof plan stops, never where one wing's own rectangle happens to end. Asked of
            // the wing it would call a marched cell rim, because a march's first step lands exactly on the
            // wing's own overhang line, and stamp verge in the middle of the roof it just ran into: a T would
            // come out carrying four gables where it has three.
            var material = OnRoofRim(x, z) || (style.RidgeCap && field.OnRidge(x, z))
                ? style.Verge
                : style.Roof;

            // <b>No roof block below the wall top of whatever covers this cell.</b> Under a wall is inside the
            // building, and that is not where a roof goes — it is what makes a one-storey wing stop against a
            // two-storey one instead of pushing through it. A wing's own roof already starts above its own
            // wall, so on a building of one wing this decides nothing.
            var lowest = Covering(x, z) + 1;

            // On a half course the topmost cell is a slab rather than a cube — written straight, the way a
            // window's pieces are, because which half it fills is geometry and not something a material may
            // resolve. The cubes under it are the roof's own material like any other course.
            var slab = field.Half(x, z);
            for (var y = Math.Max(field.Underside(x, z), lowest); y <= (slab ? crown - 1 : crown); y++)
                Put(x, y, z, material, ring);
            if (slab && crown >= lowest && crown is > 0 and < VoxelWorld.MaxHeight)
                world.SetBlock(x, crown, z, slabBlock, style.RoofSlabData & 0x7);
        }

        /// <summary>The rectangle a wing's roof draws over: its own walls, lengthened along its own ridge to the
        /// far wall of every hall it <see cref="Wing.Projects"/> into. A wing that marches roofs its own
        /// rectangle and nothing more, because the march is what carries it any further.
        ///
        /// <para>The lengthening runs along the ridge, which is the axis a gable's rise is <em>not</em> measured
        /// over (<see cref="RoofField"/>), so a projection makes the roof longer without making it taller — the
        /// wing arrives over the hall's far wall at exactly the height it left its own.</para></summary>
        (int MinX, int MinZ, int MaxX, int MaxZ) Roofed(Wing wing, int index)
        {
            var (minX, minZ, maxX, maxZ) = (wing.MinX, wing.MinZ, wing.MaxX, wing.MaxZ);
            if (!wing.Projects) return (minX, minZ, maxX, maxZ);
            foreach (var step in (int[])[-1, 1])
            {
                if (WingJoints.Past(joints, index, step) is not { } joint) continue;
                var hall = body.Wings[joint.Hall];
                if (wing.RidgeAlongX && step < 0) minX = Math.Min(minX, hall.MinX);
                else if (wing.RidgeAlongX) maxX = Math.Max(maxX, hall.MaxX);
                else if (step < 0) minZ = Math.Min(minZ, hall.MinZ);
                else maxZ = Math.Max(maxZ, hall.MaxZ);
            }
            return (minX, minZ, maxX, maxZ);
        }

        /// <summary>Whether the cell lies past a gable end that marches — the overhang columns the march takes
        /// over from. A projecting end has no march to take them over, and keeps the overhang it would have had
        /// standing open anywhere else.</summary>
        bool BeyondMarch(Wing wing, int index, int x, int z)
        {
            if (wing.Projects) return false;
            var (low, high) = wing.GableEnds;
            var along = wing.RidgeAlongX ? x : z;
            return (along < low && WingJoints.Past(joints, index, -1) is not null)
                || (along > high && WingJoints.Past(joints, index, 1) is not null);
        }

        /// <summary>The tallest surface any other wing's own roof already reaches at this cell, or a course
        /// below every roof where none does — what the march may not draw over. Read from each wing's own field
        /// directly rather than from what the primary pass actually placed, and a field asked for a cell outside
        /// its own rectangle keeps answering the same formula it always did, which is what lets the comparison
        /// hold all the way to the far wall rather than only where the roof's own plan happens to cover.</summary>
        int CoveringCrown(Wing wing, int x, int z)
        {
            var highest = int.MinValue;
            foreach (var (other, _, otherRoofed, otherField, _, _) in roofs)
                if (!other.Equals(wing) && otherRoofed.Holds(x, z))
                    highest = Math.Max(highest, otherField.Crown(x, z));
            return highest;
        }

        /// <summary>Where every wing's roof marches, solved before any of it is laid: cell to the course the
        /// marching wing's own roof tops out at there.
        ///
        /// <para><b>The march is part of the answer to which roof is showing, so it cannot be laid last.</b> A
        /// course a wing marches into the hall is a course of the wing's roof standing over ground the hall's
        /// roof also covers — and only the highest roof over a cell is written there. Laid afterwards the wing's
        /// ridge goes in over a hall course that was already written under it, and that course is a floor across
        /// the valley: it seals the wing's loft off from the hall's, which is the one thing a junction exists to
        /// prevent. Solved first, <see cref="Overtopped"/> can see it and the hall gives way.</para></summary>
        List<Dictionary<(int X, int Z), int>> Marched()
        {
            var spans = roofs.Select(_ => new Dictionary<(int X, int Z), int>()).ToList();
            foreach (var (wing, index, _, field, _, _) in roofs)
            {
                if (wing.Projects) continue;
                var (low, high) = wing.GableEnds;
                foreach (var (end, step) in new[] { (low, -1), (high, 1) })
                {
                    if (WingJoints.Past(joints, index, step) is null) continue;
                    var (acrossLo, acrossHi) = wing.RidgeAlongX ? (wing.MinZ, wing.MaxZ) : (wing.MinX, wing.MaxX);
                    for (var across = acrossLo; across <= acrossHi; across++)
                    {
                        var reachable = Math.Min(across - acrossLo, acrossHi - across);
                        for (var reach = 1; reach <= reachable; reach++)
                        {
                            var along = end + step * reach;
                            var (x, z) = wing.RidgeAlongX ? (along, across) : (across, along);
                            if (!body.Holds(x, z)) break;               // marched clean out of the building
                            var crown = field.Crown(x, z);
                            if (CoveringCrown(wing, x, z) >= crown) break;  // the other roof already stands this tall
                            spans[index][(x, z)] = crown;
                        }
                    }
                }
            }
            return spans;
        }

        /// <summary>Whether another wing's roof stands strictly higher over this cell, so this one's surface is
        /// inside it. Asked of the other wing's <b>field</b> rather than of its walls, because an overhang is
        /// roof too: the cells a gable oversails are exactly where a neighbour's eave would otherwise fill the
        /// triangle that has to stay open under it — and of its <see cref="Marched"/> span as well, which is
        /// roof standing outside any field's rectangle.</summary>
        bool Overtopped(Wing wing, RoofField field, int x, int z)
        {
            var mine = field.Crown(x, z);
            foreach (var (other, index, _, otherField, _, _) in roofs)
            {
                if (other.Equals(wing)) continue;
                if (otherField.Covers(x, z) && otherField.Crown(x, z) > mine) return true;
                if (marched[index].TryGetValue((x, z), out var crown) && crown > mine) return true;
            }
            return false;
        }

        /// <summary>Whether any wing's roof reaches this cell at all — the building's roof plan, walls and
        /// overhang together.</summary>
        bool InRoofPlan(int x, int z)
        {
            foreach (var (_, _, _, field, _, _) in roofs)
                if (field.Covers(x, z)) return true;
            return false;
        }

        /// <summary>Whether the cell is on the outer edge of that plan: in it, with a neighbour outside it. One
        /// building, one outline — which is the whole of why this is asked of every wing's field at once rather
        /// than of the one laying the block.</summary>
        bool OnRoofRim(int x, int z) =>
            InRoofPlan(x, z) &&
            (!InRoofPlan(x - 1, z) || !InRoofPlan(x + 1, z) || !InRoofPlan(x, z - 1) || !InRoofPlan(x, z + 1));

        /// <summary>The highest wall top of any wing standing on this cell, or a course below every roof where
        /// none does — so a cell out past the walls is gated by nothing and keeps its overhang.</summary>
        int Covering(int x, int z)
        {
            var highest = int.MinValue;
            foreach (var wing in body.Wings)
                if (wing.Holds(x, z)) highest = Math.Max(highest, WallTopOf(wing));
            return highest;
        }

        /// <summary>The course a wing's own eave sits on: its floor plus the courses of every storey it
        /// reaches, the last of them counted as a top storey since a wing that stops carries no slab over it.</summary>
        int WallTopOf(Wing wing)
        {
            var reaches = wing.Storeys <= 0 ? levels.Count : Math.Clamp(wing.Storeys, 1, levels.Count);
            return floorY + bases[reaches - 1] + levels[reaches - 1].Courses(topmost: true);
        }

        // A doorway cut after the walls so it is a hole in them, and never through a corner post, which is
        // what holds the building up. Air is written here rather than skipped: this one is an opening cut on
        // purpose, not a gap a material declined to fill.
        void StampDoors()
        {
            var choice = DoorMaterials.Of(style.Door);
            foreach (var door in openings)
            {
                // The head takes the opening's <em>top</em> course where the style names one and the opening
                // is big enough to spare it, so a three-course door becomes two of clear under a beam.
                var head = style.DoorHead.Fits(door.Width, doorHeight) ? doorHeight : 0;
                var alongX = door.Wall.AlongX;
                for (var course = 1; course <= doorHeight; course++)
                    for (var step = 0; step < door.Width; step++)
                    {
                        var (x, z) = door.Wall.Cell(door.Lo + step);
                        var (id, data) = course == head
                            ? style.DoorHead.Piece(alongX, step, door.Width)
                            : (choice.BlockId, choice.Coloured && color >= 0 ? color : 0);
                        world.SetBlock(x, floorY + course, z, id, data);
                    }
            }
        }

        // ── the porch ─────────────────────────────────────────────────────────────────────────────────
        // The strip the walls gave up, roofed and posted.
        void StampPorch(Footprint porch, PorchStyle porchStyle)
        {
            // The deck's open side is the wall's own outward face: the strip was taken off that wall, so the
            // house stands behind the deck and the weather is in front of it.
            var outer = front;
            var seated = new RoofField(
                porchStyle.Roof, porch.MinX, porch.MinZ, porch.MaxX, porch.MaxZ, overhang, 0, pitch, outer,
                style.RoofInHalves);

            // The canopy is seated by its own *lowest* course rather than by its ridge: that course has to
            // clear the doorway the porch fronts, and where it lands the ridge follows by however far the form
            // happens to fall. One statement for all six, and a statement about the thing that matters, since
            // a canopy resting on the door head is a porch nobody can walk under.
            //
            // It is deliberately not seated under the house's eave. On a house the two agree — a five-course
            // wall puts its eave about where a porch wants its roof anyway — and on a tower they do not: a
            // canopy chasing the eave rides the wall up twenty courses and leaves a colonnade over a doorway
            // open to the sky.
            var canopy = seated.Raised(floorY + doorHeight + 2 - seated.Trough);

            for (var x = canopy.MinX; x <= canopy.MaxX; x++)
                for (var z = canopy.MinZ; z <= canopy.MaxZ; z++)
                    if (!body.Holds(x, z))                    // the house roofs its own footprint
                        Lay(canopy, x, z, porch, style.RoofSlab);

            // A post stands on the deck, so it takes the ground storey's wall where the style names no post —
            // the storey it is actually beside, not the one at the top of the building.
            var postMaterial = groundPost ?? groundWall.At(groundWall.Extent - 1).Material;
            foreach (var (x, z) in PorchPosts(porch, outer))
                for (var y = floorY + 1; y < canopy.Underside(x, z); y++)
                    Put(x, y, z, postMaterial, porch);

            if (porchStyle.RailBlock <= 0) return;
            var posts = PorchPosts(porch, outer).ToHashSet();
            foreach (var (x, z) in PorchRail(porch, outer))
                if (!posts.Contains((x, z)) && !FrontsDoor(x, z))
                    world.SetBlock(x, floorY + 1, z, porchStyle.RailBlock);

            // The way in. A rail that ran unbroken across the front would be a porch with a door behind it and
            // no way onto the step, so the door's own run is left open through every rail it crosses.
            bool FrontsDoor(int x, int z)
            {
                foreach (var door in openings)
                {
                    if (door.Wall.Facing != front) continue;
                    var along = door.Wall.AlongX ? x : z;
                    if (along >= door.Lo && along < door.Lo + door.Width) return true;
                }
                return false;
            }
        }
    }

    /// <summary>Where a ladder stands: against the <b>door wall</b>, one cell along from an interior corner.
    ///
    /// <para>The door wall rather than any wall, because that is the least contested cell in a room. A wool
    /// cage seats its chest stacks at the four interior corners and a spawn room seats its monuments corners
    /// first, then filling the <em>far</em> wall inward — so the door wall is untouched until a room carries
    /// more monuments than a team ever captures. One along from the corner rather than in it for the same
    /// reason: the corner itself is always taken.</para>
    ///
    /// <para>The low end of the wall unless the doorway reaches it, in which case the high end — a ladder in
    /// the doorway is a ladder in the way.</para></summary>
    private static (int X, int Z) LadderCell(WallSegment wall, IReadOnlyList<WallOpening> doors)
    {
        var (lo, hi) = wall.BetweenCorners;
        var along = Math.Min(lo + 1, hi);
        if (doors.Any(door => door.Wall == wall && along >= door.Lo - 1 && along < door.Lo + door.Width + 1))
            along = Math.Max(hi - 1, lo);

        var (wallX, wallZ) = wall.Cell(along);
        var (inX, inZ) = wall.Inward;
        return (wallX + inX, wallZ + inZ);
    }

    /// <summary>The metadata a ladder on a wall takes: it faces <em>away</em> from the wall it hangs on, since
    /// the block behind it is what holds it up.</summary>
    private static int LadderFacing(RoomEdge wall) => wall switch
    {
        RoomEdge.NegZ => 3,      // hung on the −z wall, facing +z
        RoomEdge.PosZ => 2,
        RoomEdge.NegX => 5,
        _ => 4,
    };

    /// <summary>The wall a house fronts on: the one its doors are cut through, or — with none given — the long
    /// side the building would cut its own through.</summary>
    private static RoomEdge FrontEdge(IReadOnlyList<RoomDoor>? doors, Footprint ground)
        => doors is { Count: > 0 } ? doors[0].Edge : DefaultFront(ground.Width, ground.Depth);

    /// <summary>The wall a house with no doors fronts on — the long side, which is where it cuts its own. Public
    /// because the front decides how tall a <see cref="RoofForm.Shed"/> or a <see cref="RoofForm.Saltbox"/>
    /// stands, so anything reserving headroom for one has to reach the same answer this stamper does.</summary>
    public static RoomEdge DefaultFront(int width, int depth)
        => depth <= width ? RoomEdge.NegZ : RoomEdge.NegX;

    /// <summary>The footprint split into what the walls keep and what the porch takes, or the whole of it and
    /// no deck. <b>The porch is the part that gives way</b>: it is trimmed to whatever the room can spare
    /// beyond the three blocks that hold two walls and an inside, and where the room can spare nothing there
    /// is no porch. A style asked for a building and a porch, and half a building is neither.</summary>
    private static (Footprint Body, Footprint? Deck) SplitPorch(Footprint ground, PorchStyle? porch, RoomEdge front)
    {
        if (porch is null || porch.Depth <= 0) return (ground, null);
        // A deck is a strip the walls give up, and giving one up on a plan that turns a corner means taking
        // cells out of a shape rather than moving one side of a rectangle in (G172).
        if (ground.Wings.Count > 1) return (ground, null);
        var across = front is RoomEdge.NegZ or RoomEdge.PosZ ? ground.Depth : ground.Width;
        var depth = Math.Min(porch.Depth, across - 3);
        if (depth <= 0) return (ground, null);

        var inset = Math.Max(0, porch.Inset);
        var along = front is RoomEdge.NegZ or RoomEdge.PosZ ? ground.Width : ground.Depth;
        if (2 * inset >= along) inset = 0;

        return front switch
        {
            RoomEdge.NegZ => (new Footprint(ground.MinX, ground.MinZ + depth, ground.MaxX, ground.MaxZ),
                              new Footprint(ground.MinX + inset, ground.MinZ, ground.MaxX - inset, ground.MinZ + depth - 1)),
            RoomEdge.PosZ => (new Footprint(ground.MinX, ground.MinZ, ground.MaxX, ground.MaxZ - depth),
                              new Footprint(ground.MinX + inset, ground.MaxZ - depth + 1, ground.MaxX - inset, ground.MaxZ)),
            RoomEdge.NegX => (new Footprint(ground.MinX + depth, ground.MinZ, ground.MaxX, ground.MaxZ),
                              new Footprint(ground.MinX, ground.MinZ + inset, ground.MinX + depth - 1, ground.MaxZ - inset)),
            _ => (new Footprint(ground.MinX, ground.MinZ, ground.MaxX - depth, ground.MaxZ),
                  new Footprint(ground.MaxX - depth + 1, ground.MinZ + inset, ground.MaxX, ground.MaxZ - inset)),
        };
    }

    /// <summary>Where the doorways go. A frame's doors are the entry contract and keep the wall and the place
    /// the frame chose — but a frame knows the room and not the building, so where the style stands a
    /// <b>post</b> at the corners the opening is still fitted clear of them, narrowing rather than cutting
    /// through the frame. A porch moves the wall its doors were cut in, and they are carried onto the new line
    /// with the same fit. Without a frame the house cuts its own, centred on the front.</summary>
    private static List<WallOpening> Doorways(
        IReadOnlyList<RoomDoor>? doors, HouseStyle style, Footprint body, WallSegment? frontWall)
    {
        if (doors is { Count: > 0 })
        {
            var carried = new List<WallOpening>();
            foreach (var door in doors)
            {
                var about = door.Lo + (door.Width - 1) / 2;
                if (body.WallFacing(door.Edge, about) is not { } wall) continue;
                if (Fit(wall, door.Width, about) is { } fitted)
                    carried.Add(new WallOpening(wall, fitted.Lo, fitted.Width));
            }
            return carried;
        }

        if (frontWall is not { } face) return [];
        return Fit(face, Math.Max(2, style.DoorWidth), FrontCentre(body, face.Facing)) is { } own
            ? [new WallOpening(face, own.Lo, own.Width)]
            : [];
    }

    /// <summary>The middle of the side a house fronts on, which is where it cuts its own door.</summary>
    private static int FrontCentre(Footprint body, RoomEdge front)
        => front is RoomEdge.NegZ or RoomEdge.PosZ
            ? (body.MinX + body.MaxX) / 2
            : (body.MinZ + body.MaxZ) / 2;

    /// <summary>An opening of at most <paramref name="width"/> on one wall, as near <paramref name="about"/> as
    /// the wall allows, or null where the wall cannot carry one at all.
    ///
    /// <para>Every opening keeps the same margin off both corners, and it is kept <b>whatever stands in the
    /// corner</b>. A post makes the reason easy to see — a column wants a block of wall beside it before
    /// anything is taken out — but the rule is not about the post: a corner is where two walls meet and turn,
    /// and an opening hard against that turn reads as a wall that failed rather than as a way through, in a
    /// plain shell exactly as in a framed house. Making it conditional would also mean the same building gained
    /// and lost the margin as its corners were bound and unbound, which is a style deciding where a door goes.
    /// It is the same margin <see cref="HouseWindows.Seats"/> keeps, for the same reason.</para>
    ///
    /// <para>The door <b>narrows</b> rather than the margin giving way: a face too tight for the width asked
    /// for gets a narrower opening, not one that meets the corner. Two blocks is what a door wants and one is
    /// what a narrow shed can have, which is the building saying it is narrow — so a five-wide face carries a
    /// centred single opening rather than a two-wide one against the turn. Only a face with no seat at all
    /// falls back to the run between the corners, because a building nobody can walk into is worse than one
    /// with a tight door.</para></summary>
    private static (int Lo, int Width)? Fit(WallSegment wall, int width, int about)
    {
        var (runLo, runHi) = wall.BetweenCorners;
        var (seatLo, seatHi) = wall.Seat;
        var (lo, hi) = seatHi >= seatLo ? (seatLo, seatHi) : (runLo, runHi);

        var fitted = Math.Min(width, hi - lo + 1);
        if (fitted < 1) return null;
        return (Math.Clamp(about - (fitted - 1) / 2, lo, hi - fitted + 1), fitted);
    }

    /// <summary>The deck's posts: one at each outer corner, and enough between them that no span of the eave
    /// runs more than five blocks unsupported.</summary>
    private static List<(int X, int Z)> PorchPosts(Footprint porch, RoomEdge outer)
    {
        var alongX = outer is RoomEdge.NegZ or RoomEdge.PosZ;
        var (lo, hi) = alongX ? (porch.MinX, porch.MaxX) : (porch.MinZ, porch.MaxZ);
        var fixedAt = outer switch
        {
            RoomEdge.NegZ => porch.MinZ,
            RoomEdge.PosZ => porch.MaxZ,
            RoomEdge.NegX => porch.MinX,
            _ => porch.MaxX,
        };

        const int longestSpan = 5;
        var run = hi - lo;
        var spans = Math.Max(1, (run + longestSpan - 1) / longestSpan);
        var posts = new List<(int X, int Z)>();
        for (var index = 0; index <= spans; index++)
        {
            var along = lo + (int)Math.Round((double)index * run / spans);
            posts.Add(alongX ? (along, fixedAt) : (fixedAt, along));
        }
        return posts;
    }

    /// <summary>The deck's open edges — the one facing away from the house and the two flanks running back to
    /// it. The edge against the wall is not one of them; the wall is already there.</summary>
    private static IEnumerable<(int X, int Z)> PorchRail(Footprint porch, RoomEdge outer)
    {
        for (var x = porch.MinX; x <= porch.MaxX; x++)
            for (var z = porch.MinZ; z <= porch.MaxZ; z++)
            {
                if (!porch.OnPerimeter(x, z)) continue;
                var against = outer switch
                {
                    RoomEdge.NegZ => z == porch.MaxZ,
                    RoomEdge.PosZ => z == porch.MinZ,
                    RoomEdge.NegX => x == porch.MaxX,
                    _ => x == porch.MinX,
                };
                if (!against) yield return (x, z);
            }
    }

    /// <summary>The gap a flat lid carries, or null for a roof that has none. Sloped forms never take one:
    /// they have a volume of their own and a hole in a slope is a leak rather than a light.</summary>
    private static Footprint? RoofHole(HouseStyle style, Footprint body)
    {
        if (!style.RoofHole || style.Form != RoofForm.Flat) return null;
        int spanX = RoofHoleSpan(body.Width), spanZ = RoofHoleSpan(body.Depth);
        // Measured on the walls rather than on the roof plane: it lights the room, and an overhang grows
        // symmetrically so the centre is the same either way.
        var minX = body.MinX + (body.Width - spanX) / 2;
        var minZ = body.MinZ + (body.Depth - spanZ) / 2;
        return new Footprint(minX, minZ, minX + spanX - 1, minZ + spanZ - 1);
    }

    /// <summary>The hole a flat roof carries over a shell <paramref name="span"/> wide: proportional
    /// (span − 4, so the rim keeps two courses of roof) but capped at 4 — 3 on an odd span, so it stays
    /// centred. The shipped 8-wide shell keeps its 4×4 hole.</summary>
    public static int RoofHoleSpan(int span)
    {
        var cap = span % 2 == 0 ? 4 : 3;
        var floor = span % 2 == 0 ? 2 : 1;
        return Math.Min(cap, Math.Max(span - 4, floor));
    }
}
