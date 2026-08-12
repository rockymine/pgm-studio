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
    {
        if (width < 3 || depth < 3) return;                       // no room for two walls and an inside

        var ground = new Footprint(minX, minZ, minX + width - 1, minZ + depth - 1);
        var front = style.Porch?.Edge ?? style.DoorEdge ?? FrontEdge(doors, ground);
        var (body, deck) = SplitPorch(ground, style.Porch, front);
        var doorHeight = Math.Min(
            doors is { Count: > 0 } ? style.DoorHeight : Math.Max(3, style.DoorHeight),
            Math.Max(1, style.Levels[0].Headroom));
        var openings = Doorways(doors, style, body, wallsMoved: deck is not null, front);

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
                if (!ground.Holds(x, z)) { Put(x, floorY, z, style.Sill, ground); continue; }
                for (var step = 0; step < Math.Max(1, style.Floor.Extent); step++)
                    PutPart(x, floorY - step, z, style.Floor, step, ground);
            }

        // The floor's own top course, zoned across the room it belongs to. The porch keeps the floor part
        // showing: a deck is what the building stands on, not a room to lay a border round.
        if (!style.Surface.IsPlain)
            for (var x = body.MinX; x <= body.MaxX; x++)
                for (var z = body.MinZ; z <= body.MaxZ; z++)
                    if (style.Surface.At(body.Ring(x, z)) is { } surface)
                        Put(x, floorY, z, surface, body);

        // ── the walls ─────────────────────────────────────────────────────────────────────────────────
        // Storey by storey, each counting its own courses up from its own floor — so a band written at a
        // storey's fourth course is at the fourth course of every storey, and a taller ground floor does not
        // slide the one above it. The four corners take their own material.
        for (var level = 0; level < levels.Count; level++)
        {
            var storey = levels[level];
            var wall = storey.Wall ?? style.Wall;
            var corner = storey.Post ?? style.Post;
            for (var course = 1; course <= storey.Courses(level == levels.Count - 1); course++)
                for (var x = body.MinX; x <= body.MaxX; x++)
                    for (var z = body.MinZ; z <= body.MaxZ; z++)
                    {
                        if (!body.OnPerimeter(x, z)) continue;
                        var y = floorY + bases[level] + course;
                        if (body.OnCorner(x, z) && corner is { } post) Put(x, y, z, post, body);
                        else PutPart(x, y, z, wall, course - 1, body);
                    }
        }

        // Windows, storey by storey and in each storey's own frame — a sill of two is two blocks above *this*
        // floor whichever storey it is. The seater takes a sill and a wall height and asks nothing about which
        // storey it is seating, so a storey is simply a smaller wall to it. Only the ground storey knows about
        // the doorway; there is no door to avoid on the ones above.
        for (var level = 0; level < levels.Count; level++)
        {
            var storey = levels[level];
            var windows = storey.Windows ?? style.Windows;
            var seats = HouseWindows.Seats(
                windows, body.MinX, body.MinZ, body.MaxX, body.MaxZ,
                storey.Headroom, level == 0 ? openings : null,
                Hosts(storey.Wall ?? style.Wall, windows, bases[level]));
            foreach (var seat in seats)
                HouseWindows.Cut(
                    world, seat with { Sill = seat.Sill + bases[level] }, windows, floorY,
                    body.MinX, body.MinZ, body.MaxX, body.MaxZ);
        }

        StampLevels();

        // Whether the wall at one cell of one storey resolves to the block a window wants to be cut into. The
        // wall is asked rather than inspected: it is resolved exactly as the pass that laid it resolved it,
        // same course, same arc, same run — so whatever pattern put a block there, a window finds it.
        Func<RoomEdge, int, bool>? Hosts(RoomPart wall, WindowStyle windows, int storeyBase)
        {
            if (windows.HostBlock < 0) return null;
            var course = Math.Max(1, windows.Sill);
            var (material, depth) = wall.At(course - 1);
            return (edge, along) =>
            {
                var (x, z) = edge switch
                {
                    RoomEdge.NegZ => (along, body.MinZ),
                    RoomEdge.PosZ => (along, body.MaxZ),
                    RoomEdge.NegX => (body.MinX, along),
                    _ => (body.MaxX, along),
                };
                var arc = body.Arc(x, z);
                var (id, data) = material.Resolve(new BucketContext(
                    x, floorY + storeyBase + course, z, TerrainBucket.Fill, depth, color,
                    arc, 0, body.Turn(arc), body.Run(x, z)));
                return id == windows.HostBlock && data == windows.HostData;
            };
        }

        // ── the roof ──────────────────────────────────────────────────────────────────────────────────
        var roof = new RoofField(
            style.Form, body.MinX, body.MinZ, body.MaxX, body.MaxZ, overhang, wallTop + 1, pitch, front,
            style.RoofInHalves);
        var hole = RoofHole(style, body);

        for (var x = roof.MinX; x <= roof.MaxX; x++)
            for (var z = roof.MinZ; z <= roof.MaxZ; z++)
            {
                if (hole is { } gap && gap.Holds(x, z)) continue;
                Lay(roof, x, z, body);
            }

        // The walls climb to meet it: a gable's two ends, a shed's back wall and its triangular flanks, and
        // nothing under a hip, whose slopes already come down to the wall line on every side. What they climb
        // in is the gable's own material where the style names one, else the wall's top course carried up —
        // the wall's stack has nothing left to say by then, since its courses ran out at the wall's top.
        for (var x = body.MinX; x <= body.MaxX; x++)
            for (var z = body.MinZ; z <= body.MaxZ; z++)
            {
                if (!body.OnPerimeter(x, z)) continue;
                for (var fill = wallTop + 1; fill < roof.Underside(x, z); fill++)
                    if (style.Gable is { } gable) Put(x, fill, z, gable, body);
                    else PutPart(x, fill, z, topWall, topWall.Extent - 1, body);
            }

        StampGableWindows(roof);
        StampDoors();

        if (deck is { } porchDeck && style.Porch is { } porchStyle) StampPorch(porchDeck, porchStyle);
        return;

        // ── the window in the gable ───────────────────────────────────────────────────────────────────
        // One per face and centred, where a wall takes as many as its run will hold. A gable is a triangle:
        // its height runs out toward both ends, so the middle is the one place a window certainly fits, and
        // spreading them along the run would put half of them where there is no wall to cut.
        void StampGableWindows(RoofField field)
        {
            var windows = style.GableWindows;
            if (windows.Form == WindowForm.None) return;
            var (width, height) = windows.Normalized();
            var sill = Math.Max(1, windows.Sill);

            foreach (var edge in new[] { RoomEdge.NegZ, RoomEdge.PosZ, RoomEdge.NegX, RoomEdge.PosX })
            {
                var (lo, hi) = Seat(body, edge);                     // a block clear of both corners
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
                        var (x, z) = edge switch
                        {
                            RoomEdge.NegZ => (along, body.MinZ),
                            RoomEdge.PosZ => (along, body.MaxZ),
                            RoomEdge.NegX => (body.MinX, along),
                            _ => (body.MaxX, along),
                        };
                        fits = along >= lo - 1 && along <= hi + 1
                               && field.Underside(x, z) > wallTop + course;
                    }
                if (!fits) continue;

                // Seated from the wall top, which is the course the gable starts at and the datum an author is
                // looking at when they place one — the floor is storeys away by then.
                HouseWindows.Cut(
                    world, new WindowSeat(edge, start, width, sill, height), windows, wallTop,
                    body.MinX, body.MinZ, body.MaxX, body.MaxZ);
            }
        }

        // ── the storeys above the ground ──────────────────────────────────────────────────────────────
        // Each storey but the last carries a slab over it, and each slab a way through: a hole with a ladder
        // standing in it. Without one an upper storey is a sealed volume — a building with a room nobody can
        // reach, which is a picture of a house rather than a house.
        void StampLevels()
        {
            // One cell for the whole stack, so the way up is a single shaft rather than a ladder that moves
            // from storey to storey and leaves a player to find the next one.
            var climb = LadderCell(body, front, openings);

            for (var level = 0; level < levels.Count - 1; level++)
            {
                var storey = levels[level];
                var slabY = floorY + bases[level] + storey.Courses(topmost: false);

                // The slab: the storey's ceiling across the interior only, since the perimeter is wall. Its
                // top course is zoned by the storey above it, so an upper floor takes a border and an inlay
                // exactly as the ground one does.
                var above = levels[level + 1];
                var ceiling = storey.Ceiling ?? style.Floor.At(0).Material;
                for (var x = body.MinX + 1; x < body.MaxX; x++)
                    for (var z = body.MinZ + 1; z < body.MaxZ; z++)
                    {
                        if ((x, z) == climb) continue;            // the way up
                        var surface = above.Surface?.At(body.Ring(x, z));
                        Put(x, slabY, z, surface ?? ceiling, body);
                    }

                // The ladder: it stands in the storey below the hole and reaches the floor above, so a player
                // steps off it onto the new floor rather than into its underside.
                for (var y = floorY + bases[level] + 1; y <= slabY; y++)
                    if (y is > 0 and < VoxelWorld.MaxHeight)
                        world.SetBlock(climb.X, y, climb.Z, Blocks.Ladder, LadderFacing(front));

                LayBeams(slabY);
            }
        }

        // The log ends that run out past the corners where two storeys meet. In plan the seam reads as a hash:
        // the walls are its middle and eight ends carry on outward, two from each corner. Each shows its sawn
        // end, which is the one place on a building where a cut face outward is the point — it is the end of a
        // log, and a log building leaves them long.
        void LayBeams(int y)
        {
            if (!style.Beams.Any || y is < 1 or >= VoxelWorld.MaxHeight) return;
            var reach = Math.Max(1, style.Beams.Reach);
            var wood = style.Beams.Data & 3;

            foreach (var (cornerX, cornerZ) in new[]
                     {
                         (body.MinX, body.MinZ), (body.MaxX, body.MinZ),
                         (body.MinX, body.MaxZ), (body.MaxX, body.MaxZ),
                     })
            {
                var awayX = cornerX == body.MinX ? -1 : 1;
                var awayZ = cornerZ == body.MinZ ? -1 : 1;
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
        void Lay(RoofField field, int x, int z, Footprint ring)
        {
            var crown = field.Crown(x, z);
            var material = field.OnBorder(x, z) || (style.RidgeCap && field.OnRidge(x, z))
                ? style.Verge
                : style.Roof;

            // On a half course the topmost cell is a slab rather than a cube — written straight, the way a
            // window's pieces are, because which half it fills is geometry and not something a material may
            // resolve. The cubes under it are the roof's own material like any other course.
            var slab = field.Half(x, z);
            for (var y = field.Underside(x, z); y <= (slab ? crown - 1 : crown); y++)
                Put(x, y, z, material, ring);
            if (slab && crown is > 0 and < VoxelWorld.MaxHeight)
                world.SetBlock(x, crown, z, style.RoofSlab, style.RoofSlabData & 0x7);
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
                var alongX = door.Edge is RoomEdge.NegZ or RoomEdge.PosZ;
                for (var course = 1; course <= doorHeight; course++)
                    for (var step = 0; step < door.Width; step++)
                    {
                        var along = door.Lo + step;
                        var (x, z) = door.Edge switch
                        {
                            RoomEdge.NegZ => (along, body.MinZ),
                            RoomEdge.PosZ => (along, body.MaxZ),
                            RoomEdge.NegX => (body.MinX, along),
                            _ => (body.MaxX, along),
                        };
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
                        Lay(canopy, x, z, porch);

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
                    if (door.Edge != front) continue;
                    var along = door.Edge is RoomEdge.NegZ or RoomEdge.PosZ ? x : z;
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
    private static (int X, int Z) LadderCell(Footprint body, RoomEdge front, IReadOnlyList<RoomDoor> doors)
    {
        var alongX = front is RoomEdge.NegZ or RoomEdge.PosZ;
        var (lo, hi) = alongX ? (body.MinX + 1, body.MaxX - 1) : (body.MinZ + 1, body.MaxZ - 1);
        var along = Math.Min(lo + 1, hi);
        if (doors.Any(door => door.Edge == front && along >= door.Lo - 1 && along < door.Lo + door.Width + 1))
            along = Math.Max(hi - 1, lo);

        var inward = front switch
        {
            RoomEdge.NegZ => (X: 0, Z: 1),
            RoomEdge.PosZ => (X: 0, Z: -1),
            RoomEdge.NegX => (X: 1, Z: 0),
            _ => (X: -1, Z: 0),
        };
        var (wallX, wallZ) = front switch
        {
            RoomEdge.NegZ => (along, body.MinZ),
            RoomEdge.PosZ => (along, body.MaxZ),
            RoomEdge.NegX => (body.MinX, along),
            _ => (body.MaxX, along),
        };
        return (wallX + inward.X, wallZ + inward.Z);
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
        var across = front is RoomEdge.NegZ or RoomEdge.PosZ ? ground.Depth : ground.Width;
        var depth = Math.Min(porch.Depth, across - 3);
        if (depth <= 0) return (ground, null);

        var inset = Math.Max(0, porch.Inset);
        var along = front is RoomEdge.NegZ or RoomEdge.PosZ ? ground.Width : ground.Depth;
        if (2 * inset >= along) inset = 0;

        return front switch
        {
            RoomEdge.NegZ => (ground with { MinZ = ground.MinZ + depth },
                              new Footprint(ground.MinX + inset, ground.MinZ, ground.MaxX - inset, ground.MinZ + depth - 1)),
            RoomEdge.PosZ => (ground with { MaxZ = ground.MaxZ - depth },
                              new Footprint(ground.MinX + inset, ground.MaxZ - depth + 1, ground.MaxX - inset, ground.MaxZ)),
            RoomEdge.NegX => (ground with { MinX = ground.MinX + depth },
                              new Footprint(ground.MinX, ground.MinZ + inset, ground.MinX + depth - 1, ground.MaxZ - inset)),
            _ => (ground with { MaxX = ground.MaxX - depth },
                  new Footprint(ground.MaxX - depth + 1, ground.MinZ + inset, ground.MaxX, ground.MaxZ - inset)),
        };
    }

    /// <summary>Where the doorways go. A frame's doors are the entry contract and keep the wall and the place
    /// the frame chose — but a frame knows the room and not the building, so where the style stands a
    /// <b>post</b> at the corners the opening is still fitted clear of them, narrowing rather than cutting
    /// through the frame. A porch moves the wall its doors were cut in, and they are carried onto the new line
    /// with the same fit. Without a frame the house cuts its own, centred on the front.</summary>
    private static List<RoomDoor> Doorways(
        IReadOnlyList<RoomDoor>? doors, HouseStyle style, Footprint body, bool wallsMoved, RoomEdge front)
    {
        if (doors is { Count: > 0 })
        {
            var carried = new List<RoomDoor>();
            foreach (var door in doors)
                if (Fit(body, door.Edge, door.Width, door.Lo + (door.Width - 1) / 2) is { } fitted)
                    carried.Add(door with { Lo = fitted.Lo, Width = fitted.Width });
            return carried;
        }

        var centre = front is RoomEdge.NegZ or RoomEdge.PosZ
            ? (body.MinX + body.MaxX) / 2
            : (body.MinZ + body.MaxZ) / 2;
        return Fit(body, front, Math.Max(2, style.DoorWidth), centre) is { } own
            ? [new RoomDoor(front, own.Lo, own.Width)]
            : [];
    }

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
    private static (int Lo, int Width)? Fit(Footprint body, RoomEdge edge, int width, int about)
    {
        var (runLo, runHi) = Run(body, edge);
        var (seatLo, seatHi) = Seat(body, edge);
        var (lo, hi) = seatHi >= seatLo ? (seatLo, seatHi) : (runLo, runHi);

        var fitted = Math.Min(width, hi - lo + 1);
        if (fitted < 1) return null;
        return (Math.Clamp(about - (fitted - 1) / 2, lo, hi - fitted + 1), fitted);
    }

    /// <summary>The stretch of a wall between its two corner posts.</summary>
    private static (int Lo, int Hi) Run(Footprint body, RoomEdge edge)
        => edge is RoomEdge.NegZ or RoomEdge.PosZ
            ? (body.MinX + 1, body.MaxX - 1)
            : (body.MinZ + 1, body.MaxZ - 1);

    /// <summary>Where an opening may actually be cut: the run between the corners, <b>one block further in at
    /// each end</b>.
    ///
    /// <para>Clearing the corner cell is not enough. An opening that starts in the very next cell still meets
    /// the corner post, and a door hard against the post reads as a hole knocked through the frame rather than
    /// as a way in — the post is what carries the building, and it wants a block of wall beside it before
    /// anything is taken out. The same margin is what keeps a window off the corner.</para>
    ///
    /// <para>It costs four blocks of wall, so a five-wide face has one cell left and cannot carry a two-wide
    /// door: that is a building too narrow for the door it asked for, and the answer is a wider building.</para>
    /// </summary>
    private static (int Lo, int Hi) Seat(Footprint body, RoomEdge edge)
    {
        var (lo, hi) = Run(body, edge);
        return (lo + 1, hi - 1);
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

    /// <summary>A rectangle of plan the stamper reads a ring off: which cells it holds, which of them are on
    /// its perimeter, how far in from that perimeter a cell stands, and how far round it — the arc and turn a
    /// wall-run material reads. A house has two of these once it has a porch (what the walls keep and what the
    /// deck took), which is why the ring a cell is painted against is passed rather than assumed.</summary>
    private readonly record struct Footprint(int MinX, int MinZ, int MaxX, int MaxZ)
    {
        public int Width => MaxX - MinX + 1;
        public int Depth => MaxZ - MinZ + 1;

        public bool Holds(int x, int z) => x >= MinX && x <= MaxX && z >= MinZ && z <= MaxZ;

        public bool OnPerimeter(int x, int z)
            => Holds(x, z) && (x == MinX || x == MaxX || z == MinZ || z == MaxZ);

        public bool OnCorner(int x, int z) => (x == MinX || x == MaxX) && (z == MinZ || z == MaxZ);

        /// <summary>How many blocks in from the nearest edge a cell stands — 0 on the edge itself, negative
        /// outside. What the floor's zones are cut by.</summary>
        public int Ring(int x, int z)
            => Math.Min(Math.Min(x - MinX, MaxX - x), Math.Min(z - MinZ, MaxZ - z));

        /// <summary>Which axis the wall runs along at this cell — what a block with a direction of its own is
        /// laid along, so a log on its side shows bark to the outside rather than its sawn end. A rectangle
        /// answers it exactly where a traced outline has to measure it: the ±z walls run along x and the ±x
        /// walls along z. A corner belongs to both, and takes the x wall's answer for the same reason the arc
        /// counts it there.</summary>
        public int Run(int x, int z)
            => OnCorner(x, z) ? GridBoundary.RunsBothWays
                : z == MinZ || z == MaxZ ? GridBoundary.RunAlongX
                : x == MinX || x == MaxX ? GridBoundary.RunAlongZ
                : 0;

        /// <summary>How far round the perimeter a cell sits, clockwise from the −x/−z corner, or −1 off the
        /// ring. A wall is a closed loop, so a wall-run pattern reads this exactly as it reads a plateau's
        /// outer edge — one material stripes both.</summary>
        public int Arc(int x, int z)
        {
            if (z == MinZ) return x - MinX;                                  // −z edge, corners included
            if (x == MaxX) return Width - 1 + (z - MinZ);                    // +x edge
            if (z == MaxZ) return Width + Depth - 2 + (MaxX - x);            // +z edge, back along x
            if (x == MinX) return 2 * Width + Depth - 3 + (MaxZ - z);        // −x edge, back along z
            return -1;
        }

        /// <summary>How sharply the ring bends where a cell sits, to the same scale a painted wall reads
        /// (<see cref="PgmStudio.Geom.Algorithms.GridBoundary.TurnAt"/>). A footprint is a rectangle, so the
        /// answer is closed-form rather than traced: a cell <c>d</c> from a corner has a window straddling it
        /// by <c>window − d</c>, and the chords make <c>atan2(window − d, d)</c> — 90° on the corner itself,
        /// then 76, 56, 34, 14 and straight, which is the ramp a traced right angle measures.</summary>
        public int Turn(int arc)
        {
            if (arc < 0) return 0;
            var loop = 2 * (Width + Depth) - 4;
            if (loop <= 0) return 0;

            int[] corners = [0, Width - 1, Width + Depth - 2, 2 * Width + Depth - 3];
            var nearest = int.MaxValue;
            foreach (var corner in corners)
            {
                var apart = Math.Abs(arc - corner);
                nearest = Math.Min(nearest, Math.Min(apart, loop - apart));
            }

            const int window = 5;
            if (nearest >= window) return 0;
            return (int)Math.Round(Math.Atan2(window - nearest, nearest) * 180.0 / Math.PI);
        }
    }
}
