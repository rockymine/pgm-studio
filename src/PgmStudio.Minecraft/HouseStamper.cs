using PgmStudio.Domain;

namespace PgmStudio.Minecraft;

/// <summary>Which roof a house wears. A <see cref="Flat"/> one is the shell a wool structure and a spawn have
/// always had — a lid over the walls — and a <see cref="Gable"/> one is two slopes meeting at a ridge. They
/// are the same building underneath, which is why one stamper builds both.</summary>
public enum RoofForm { Gable, Flat }

/// <summary>
/// How a house is finished: a part or a material per piece of it, and the few numbers that decide its
/// proportions.
///
/// <para>The walls and the floor are <see cref="RoomPart"/> stacks rather than single materials. A stack is
/// not needed to band a wall — a <see cref="LayeredMaterial"/> does that, and any pattern may — but it counts
/// its courses <b>up from the floor</b>, so a stripe written at the fourth course stays at the fourth course
/// when the wall grows instead of sliding with the top. That is what it is for: pinning a band without
/// working out which layer combination lands where.</para>
/// </summary>
public sealed record HouseStyle
{
    // Wood ids and the species nibble they share. Not in Blocks because nothing else has wanted them yet.
    private const int Planks = 5, WoodSlab = 126;
    private const int Oak = 0, Spruce = 1, DarkOak = 5;

    /// <summary>The course the walls stand on, laid one block proud of them on every side, so the building
    /// meets the ground on a footing instead of stopping dead at it.</summary>
    public TerrainMaterial Sill { get; init; } = new SolidMaterial(Blocks.Cobblestone);

    /// <summary>The infill between the posts, upward from the floor. Its extent is the wall's height.</summary>
    public RoomPart Wall { get; init; } = RoomPart.Of(new SolidMaterial(Planks, Spruce), 5);

    /// <summary>The four corner columns, or null for a building whose corners are wall like the rest of it.
    /// A house reads as framed because its corners differ from the panel between them, which is what every
    /// hand-built house on the corpus does; a plain shell has no such thing and says so by leaving this
    /// unset.</summary>
    public TerrainMaterial? Post { get; init; } = new SolidMaterial(Blocks.Log, Oak);

    /// <summary>The body of each roof slope.
    ///
    /// <para><b>A slope that climbs a whole block per block must be laid in whole blocks.</b> A slab fills only
    /// the lower half of the cube it sits in, so a course of slabs stepping up a full block leaves an open half
    /// between every pair and the roof can be seen straight through. Stairs are the other material that closes
    /// a 45° step; slabs belong on a slope that rises half a block at a time, which is a different roof.</para></summary>
    public TerrainMaterial Roof { get; init; } = new SolidMaterial(Planks, Spruce);

    /// <summary>The roof's own border — its eave course and its two verges. A roof laid in one material reads
    /// flat; bordering it is what gives the slope an edge to end on.</summary>
    public TerrainMaterial Verge { get; init; } = new SolidMaterial(Planks, DarkOak);

    /// <summary>Downward from the course players stand on, so a thicker floor digs into what the house sits
    /// on rather than lifting its inside off it.</summary>
    public RoomPart Floor { get; init; } = RoomPart.Of(new SolidMaterial(Planks, Oak));

    public RoofForm Form { get; init; } = RoofForm.Gable;

    /// <summary>Whether a flat roof carries a centred hole, the way the shipped shell does — the light a
    /// windowless room otherwise has none of. A gable has its own volume and never takes one.</summary>
    public bool RoofHole { get; init; }

    /// <summary>How far the roof reaches past the walls. One block is an eave; zero ends the roof flush and
    /// leaves the wall to carry the weather.</summary>
    public int Overhang { get; init; } = 1;

    /// <summary>How steep the slope climbs: courses of rise per block travelled inward. One is the vanilla
    /// pitch; two is a steep alpine roof.</summary>
    public int Pitch { get; init; } = 1;

    public DoorMaterial Door { get; init; } = DoorMaterial.Air;

    /// <summary>The doorway, which is never smaller than two blocks wide by three tall however it is set. A
    /// single-width opening is a gap in a wall rather than a door, and a room a player carries an objective
    /// out of has to read as somewhere to walk through.</summary>
    public int DoorWidth { get; init; } = 2;

    public int DoorHeight { get; init; } = 3;
}

/// <summary>
/// Stamps a house: a sill, four corner posts with walls between them, a gabled roof over the top, and a
/// doorway through one long side.
///
/// <para><b>The roof slopes across the shorter side.</b> A gable's ridge runs the length of a building, so the
/// slope is taken across its width — pitching the long way would put the ridge on the short axis and give a
/// long building two enormous faces and no length. Each course inward from the eave rises by the pitch until
/// the two slopes meet; where the span is even they meet as a two-block ridge rather than a peak, which is
/// what a block world gives instead of a line.</para>
///
/// <para>The gable ends are filled in the wall's own material, up to the underside of the slope, so the house
/// is closed without the roof having to carry a wall's worth of blocks. Nothing is written outside the
/// footprint plus its overhang, and nothing below the sill, so a house may be stamped onto finished terrain
/// without reaching into it.</para>
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

        int maxX = minX + width - 1, maxZ = minZ + depth - 1;
        var depthOf = depth;
        var wallTop = floorY + Math.Max(1, style.Wall.Extent);     // the eave course sits on this
        var overhang = Math.Max(0, style.Overhang);
        var pitch = Math.Max(1, style.Pitch);
        var acrossZ = depth <= width;                             // ridge runs the long way

        // Air resolved out of a material is a gap left open, never a hole punched in what is already there:
        // a stack whose fourth course is air is a light slit, and skipping it keeps the pass from erasing a
        // stamp underneath. Only a doorway writes air, because that one is an opening cut on purpose.
        void Put(int x, int y, int z, TerrainMaterial material, int depth = 0)
        {
            if (y is < 1 or >= VoxelWorld.MaxHeight) return;
            // A wall is a closed ring, so a pattern reads it exactly as it reads a plateau's outer edge: the
            // arc carries a wall-run's stripes round every corner, and the turn tells a frame where a corner
            // is. Without them every cell of the wall answers as arc 0 and a striped wall comes out flat.
            var arc = PerimeterArc(minX, minZ, maxX, maxZ, x, z);
            var (id, data) = material.Resolve(new BucketContext(
                x, y, z, TerrainBucket.Fill, depth, color, arc, 0, PerimeterTurn(width, depth: depthOf, arc)));
            if (id == Blocks.Air) return;
            world.SetBlock(x, y, z, id, data);
        }

        void PutPart(int x, int y, int z, RoomPart part, int step)
        {
            var (material, depth) = part.At(step);
            Put(x, y, z, material, depth);
        }

        // Sill and floor: the sill rings the building one block proud, the floor claims downward from the
        // course players stand on.
        for (var x = minX - 1; x <= maxX + 1; x++)
            for (var z = minZ - 1; z <= maxZ + 1; z++)
            {
                if (x < minX || x > maxX || z < minZ || z > maxZ) { Put(x, floorY, z, style.Sill); continue; }
                for (var step = 0; step < Math.Max(1, style.Floor.Extent); step++)
                    PutPart(x, floorY - step, z, style.Floor, step);
            }

        // Walls, with the four corners in their own material. The course index runs up from the floor, so a
        // band written at the fourth course stays there whatever the wall's height becomes.
        for (var course = 1; course <= style.Wall.Extent; course++)
            for (var x = minX; x <= maxX; x++)
                for (var z = minZ; z <= maxZ; z++)
                {
                    var onWall = x == minX || x == maxX || z == minZ || z == maxZ;
                    if (!onWall) continue;
                    var corner = (x == minX || x == maxX) && (z == minZ || z == maxZ);
                    if (corner && style.Post is { } post) Put(x, floorY + course, z, post);
                    else PutPart(x, floorY + course, z, style.Wall, course - 1);
                }

        if (style.Form == RoofForm.Flat)
        {
            // A lid one course over the walls, reaching out by the overhang, optionally holed to light the
            // inside. The hole is measured on the walls rather than the roof plane: it lights the room, and
            // an overhang grows symmetrically so the centre is the same either way.
            var lid = wallTop + 1;
            var holeW = RoofHoleSpan(width);
            var holeD = RoofHoleSpan(depth);
            int holeMinX = minX + (width - holeW) / 2, holeMinZ = minZ + (depth - holeD) / 2;

            for (var x = minX - overhang; x <= maxX + overhang; x++)
                for (var z = minZ - overhang; z <= maxZ + overhang; z++)
                {
                    if (style.RoofHole && x >= holeMinX && x < holeMinX + holeW
                                       && z >= holeMinZ && z < holeMinZ + holeD) continue;
                    var border = x < minX || x > maxX || z < minZ || z > maxZ;
                    Put(x, lid, z, border ? style.Verge : style.Roof);
                }
            StampDoors();
            return;
        }

        // Every cell of the roof in plan, and how high the slope stands over it. A cell's height is set by
        // whichever eave it is nearer, so the two slopes meet of their own accord: at an odd span they meet on
        // one line and peak, at an even one two lines tie and the ridge is two blocks wide, which is what a
        // block world gives instead of an edge. The roof begins one course above the wall's last, or it would
        // land on the wall top and there would be no eave to see.
        int wallLow = acrossZ ? minZ : minX, wallHigh = acrossZ ? maxZ : maxX;
        int low = wallLow - overhang, high = wallHigh + overhang;
        int fromEnd = (acrossZ ? minX : minZ) - overhang, toEnd = (acrossZ ? maxX : maxZ) + overhang;

        // Measured from the wall and allowed to go past it: the course over the wall line rests on the wall,
        // and every course outward from there keeps falling at the same rate, so the overhang hangs a block
        // below its neighbour. Holding the overhang level with the wall line instead — which is the obvious
        // way to stop the roof floating a course above the wall — runs the last two blocks of the slope flat
        // and the gable stops being 45° exactly where it is most visible.
        int RoofY(int across) => wallTop + 1 + Math.Min(across - wallLow, wallHigh - across) * pitch;

        // A step of more than one course leaves the slope open between its treads, so each column carries its
        // own riser as well as its tread. The outermost course has nothing below it to close and stays one.
        int Riser(int across) => across == low || across == high ? 1 : pitch;

        for (var along = fromEnd; along <= toEnd; along++)
            for (var across = low; across <= high; across++)
            {
                // The border is the eave the slope ends on and the verge that closes each gable.
                var border = across == low || across == high || along == fromEnd || along == toEnd;
                var crown = RoofY(across);
                for (var y = crown - Riser(across) + 1; y <= crown; y++)
                    Put(acrossZ ? along : across, y, acrossZ ? across : along,
                        border ? style.Verge : style.Roof);
            }

        // The gable ends: the two walls the ridge runs into, carried up to the underside of the slope.
        foreach (var along in new[] { acrossZ ? minX : minZ, acrossZ ? maxX : maxZ })
            for (var across = acrossZ ? minZ : minX; across <= (acrossZ ? maxZ : maxX); across++)
                for (var fill = wallTop + 1; fill < RoofY(across); fill++)
                    PutPart(acrossZ ? along : across, fill, acrossZ ? across : along,
                            style.Wall, style.Wall.Extent - 1);

        StampDoors();

        // A doorway through the middle of one long side, cut after the walls so it is a hole in them, and never
        // through a corner post, which is what holds the building up. Air is written here rather than skipped:
        // this one is an opening cut on purpose, not a gap a material declined to fill.
        void StampDoors()
        {
            var choice = DoorMaterials.Of(style.Door);

            // A frame's doors are the entry contract and are taken as given, opening exactly where and how
            // wide it says. Without one the house cuts its own: centred on a long side, never smaller than
            // two by three, and never through a corner post — which is what holds the building up.
            if (doors is { Count: > 0 })
            {
                foreach (var door in doors)
                    for (var course = 1; course <= Math.Min(style.DoorHeight, style.Wall.Extent); course++)
                        for (var along = door.Lo; along < door.Lo + door.Width; along++)
                        {
                            var (x, z) = door.Edge switch
                            {
                                RoomEdge.NegZ => (along, minZ),
                                RoomEdge.PosZ => (along, maxZ),
                                RoomEdge.NegX => (minX, along),
                                _ => (maxX, along),
                            };
                            Open(x, floorY + course, z);
                        }
                return;
            }

            var doorWidth = Math.Max(2, style.DoorWidth);
            var doorHeight = Math.Max(3, style.DoorHeight);
            var centre = acrossZ ? (minX + maxX) / 2 : (minZ + maxZ) / 2;

            for (var course = 1; course <= Math.Min(doorHeight, style.Wall.Extent); course++)
                for (var step = 0; step < doorWidth; step++)
                {
                    var along = centre - (doorWidth - 1) / 2 + step;
                    if (along <= (acrossZ ? minX : minZ) || along >= (acrossZ ? maxX : maxZ)) continue;
                    var (x, z) = acrossZ ? (along, minZ) : (minX, along);
                    Open(x, floorY + course, z);
                }

            void Open(int x, int y, int z) =>
                world.SetBlock(x, y, z, choice.BlockId, choice.Coloured && color >= 0 ? color : 0);
        }
    }

    /// <summary>How far round the building's perimeter a cell sits, clockwise from its −x/−z corner, or −1 off
    /// the ring. A wall is a closed loop, so a wall-run pattern reads this exactly as it reads a plateau's
    /// outer edge — one material stripes both.</summary>
    private static int PerimeterArc(int minX, int minZ, int maxX, int maxZ, int x, int z)
    {
        int width = maxX - minX + 1, depth = maxZ - minZ + 1;
        if (z == minZ) return x - minX;                                  // −z edge, corners included
        if (x == maxX) return width - 1 + (z - minZ);                    // +x edge
        if (z == maxZ) return width + depth - 2 + (maxX - x);            // +z edge, back along x
        if (x == minX) return 2 * width + depth - 3 + (maxZ - z);        // −x edge, back along z
        return -1;
    }

    /// <summary>How sharply the ring bends where a cell sits, to the same scale a painted wall reads
    /// (<see cref="PgmStudio.Geom.Algorithms.GridBoundary.TurnAt"/>). A footprint is a rectangle, so the answer
    /// is closed-form rather than traced: a cell <c>d</c> from a corner has a window straddling it by
    /// <c>window − d</c>, and the chords make <c>atan2(window − d, d)</c> — 90° on the corner itself, then 76,
    /// 56, 34, 14 and straight, which is the ramp a traced right angle measures.</summary>
    private static int PerimeterTurn(int width, int depth, int arc)
    {
        if (arc < 0) return 0;
        var loop = 2 * (width + depth) - 4;
        if (loop <= 0) return 0;

        int[] corners = [0, width - 1, width + depth - 2, 2 * width + depth - 3];
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
