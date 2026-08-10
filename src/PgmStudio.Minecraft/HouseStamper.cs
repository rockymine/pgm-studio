using PgmStudio.Domain;

namespace PgmStudio.Minecraft;

/// <summary>
/// How a house is finished: a material per part, and the few numbers that decide its proportions. Separate
/// from <see cref="RoomStyle"/> because a room and a house are different buildings — a room is a cube with a
/// lid, sized by the piece it holds, and a house is a shell with a pitched roof, sized to be looked at.
/// </summary>
public sealed record HouseStyle
{
    // Wood ids and the species nibble they share. Not in Blocks because nothing else has wanted them yet.
    private const int Planks = 5, WoodSlab = 126;
    private const int Oak = 0, Spruce = 1, DarkOak = 5;

    /// <summary>The course the walls stand on, laid one block proud of them on every side, so the building
    /// meets the ground on a footing instead of stopping dead at it.</summary>
    public TerrainMaterial Sill { get; init; } = new SolidMaterial(Blocks.Cobblestone);

    /// <summary>The infill between the posts.</summary>
    public TerrainMaterial Wall { get; init; } = new SolidMaterial(Planks, Spruce);

    /// <summary>The four corner columns. A house reads as framed because its corners are a different material
    /// from the panel between them — the thing every hand-built house on the corpus does.</summary>
    public TerrainMaterial Post { get; init; } = new SolidMaterial(Blocks.Log, Oak);

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

    public TerrainMaterial Floor { get; init; } = new SolidMaterial(Planks, Oak);

    /// <summary>Courses of wall between the sill and the eave.</summary>
    public int WallHeight { get; init; } = 4;

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
    /// <summary>Stamp a house whose walls occupy <paramref name="width"/> x <paramref name="depth"/> blocks
    /// with their south-west corner at <paramref name="minX"/>/<paramref name="minZ"/>, standing on
    /// <paramref name="floorY"/> — the course a player walks on inside it. <paramref name="color"/> rides the
    /// tint channel for a team-tinted material, exactly as a room's does.</summary>
    public static void Stamp(VoxelWorld world, int minX, int minZ, int width, int depth, int floorY,
                             HouseStyle style, int color = -1)
    {
        if (width < 3 || depth < 3) return;                       // no room for two walls and an inside

        int maxX = minX + width - 1, maxZ = minZ + depth - 1;
        var wallTop = floorY + Math.Max(1, style.WallHeight);     // the eave course sits on this
        var overhang = Math.Max(0, style.Overhang);
        var pitch = Math.Max(1, style.Pitch);
        var acrossZ = depth <= width;                             // ridge runs the long way

        void Put(int x, int y, int z, TerrainMaterial material)
        {
            if (y is < 1 or >= VoxelWorld.MaxHeight) return;
            var (id, data) = material.Resolve(new BucketContext(x, y, z, TerrainBucket.Fill, 0, color));
            if (id == Blocks.Air) return;
            world.SetBlock(x, y, z, id, data);
        }

        // Sill and floor: the sill rings the building one block proud, the floor fills what it encloses.
        for (var x = minX - 1; x <= maxX + 1; x++)
            for (var z = minZ - 1; z <= maxZ + 1; z++)
                Put(x, floorY, z, x < minX || x > maxX || z < minZ || z > maxZ ? style.Sill : style.Floor);

        // Walls, with the four corners in their own material.
        for (var course = 1; course <= style.WallHeight; course++)
            for (var x = minX; x <= maxX; x++)
                for (var z = minZ; z <= maxZ; z++)
                {
                    var onWall = x == minX || x == maxX || z == minZ || z == maxZ;
                    if (!onWall) continue;
                    var corner = (x == minX || x == maxX) && (z == minZ || z == maxZ);
                    Put(x, floorY + course, z, corner ? style.Post : style.Wall);
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
                    Put(acrossZ ? along : across, fill, acrossZ ? across : along, style.Wall);

        // A doorway through the middle of one long side, cut after the walls so it is a hole in them. It never
        // eats a corner post, which is what holds the gable up.
        var doorWidth = Math.Max(2, style.DoorWidth);
        var doorHeight = Math.Max(3, style.DoorHeight);
        var centre = acrossZ ? (minX + maxX) / 2 : (minZ + maxZ) / 2;
        var door = DoorMaterials.All.First(choice => choice.Material == style.Door);

        for (var course = 1; course <= Math.Min(doorHeight, style.WallHeight); course++)
            for (var step = 0; step < doorWidth; step++)
            {
                var along = centre - (doorWidth - 1) / 2 + step;
                if (along <= (acrossZ ? minX : minZ) || along >= (acrossZ ? maxX : maxZ)) continue;
                if (acrossZ) Cut(along, floorY + course, minZ);
                else Cut(minX, floorY + course, along);
            }

        // An opening is a hole, anything else fills it — a coloured door takes the building's own colour.
        void Cut(int x, int y, int z) => world.SetBlock(x, y, z, door.BlockId,
            door.BlockId == Blocks.Air ? 0 : door.Coloured && color >= 0 ? color : 0);
    }
}
