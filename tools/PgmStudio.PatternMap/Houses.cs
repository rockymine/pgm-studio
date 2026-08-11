using PgmStudio.Domain;
using PgmStudio.Minecraft;

namespace PatternMap;

/// <summary>
/// The house styles the showcase stands on its own row of islands — the presets that will seed the room-style
/// library, held here first so they can be walked before they are stored.
///
/// <para>Each is described from a real building rather than invented, and the description is checked against
/// the world it came from: the corpus map is read block by block, the wall face is printed, and the style is
/// written to match what it says. A style that cannot be said in <see cref="HouseStyle"/> is the interesting
/// case — it is a gap in the model, and finding those is what the row is for.</para>
/// </summary>
public static class Houses
{
    // Ids the styles name. Literals with a comment, the way the showcase's own materials are written — Blocks
    // carries what the stamper needs and a mushroom block has never been one of them.
    private const int SprucePlanks = 1, DarkOakPlanks = 5;      // the data nibble of block 5
    private const int Spruce = 1;                               // the data nibble of block 17
    private const int Andesite = 5;                             // the data nibble of block 1
    private const int BrownMushroomBlock = 99, AllCap = 14;     // 14 is the cap texture on every face
    private const int SpruceStairs = 134, BirchStairs = 135;
    private const int Brick = 45, BirchSlab = 2;                // 2 is the birch nibble of block 126
    private const int PlainDiorite = 3, PolishedDiorite = 4;    // the nibbles of block 1
    private const int LightBlue = 3;                            // the nibble of block 159
    private const int StoneSlab = 44, BrickSlab = 4;            // 4 is the brick nibble of block 44
    private const int Oak = 0, White = 0, StoneBrick = 98;      // the nibbles of block 17/160, and a block
    private const int StainedPane = 160;

    private static readonly TerrainMaterial Spruces = new SolidMaterial(Blocks.Planks, SprucePlanks);
    private static readonly TerrainMaterial DarkOak = new SolidMaterial(Blocks.Planks, DarkOakPlanks);
    private static readonly TerrainMaterial SpruceLog = new SolidMaterial(Blocks.Log, Spruce);

    /// <summary>One house on the row: what to call it, the style, and the footprint it is drawn at.
    ///
    /// <para>The footprint is here rather than on the style because <b>a style may never carry one</b> — a
    /// room's comes from its plan piece and a prop's from the rectangle an author dragged. So the size a house
    /// was designed at travels beside it and is used when it is placed.</para></summary>
    public readonly record struct House(string Name, HouseStyle Style, int Width, int Depth);

    public static IReadOnlyList<House> All => [Alpine, Desert, Diorite, Townside, Stilts];

    /// <summary>
    /// The alpine mining house: a spruce-framed cottage on a mixed stone plinth.
    ///
    /// <para>Read off <c>alpine_mining_ii</c>. Its walls run seven courses between spruce log posts that stand
    /// the <b>full</b> height — the plinth does not turn the corner, it sits between them. The bottom two
    /// courses are cobble and andesite mixed (across the map, the block under the lowest mushroom of a column
    /// is andesite 80 times and cobble 50), and the two courses differ from each other, so the field is sampled
    /// over the volume rather than the plane. Above them the wall bands as it wraps: brown mushroom block,
    /// spruce planks, then acacia logs turned upright and on their side by turns.</para>
    /// </summary>
    public static House Alpine => new("alpine mining", new HouseStyle
    {
        Form = RoofForm.Gable,
        Pitch = 1,
        Overhang = 1,
        Roof = Spruces,
        Verge = DarkOak,

        // The face the slopes leave, in mushroom block — the one piece of this building that is a plain
        // panel, and the reason the gable is nameable at all: unbound it carries the wall's top course up,
        // and the top course here is the banded run.
        Gable = new SolidMaterial(BrownMushroomBlock, AllCap),

        Post = SpruceLog,
        Wall = new RoomPart(
        [
            // Two courses of the plinth. The scale is 2 because the real one scatters nearly block by block —
            // at 3 a whole five-block face came out one stone and the mix was invisible. Rise 2 so the second
            // course is not a copy of the first, which is what the real wall does: its two courses of cobble
            // and andesite fall differently.
            new RoomCourse(new NoiseMaterial(
                Seed: 0x5A17, Scale: 2, Octaves: 1,
                Stops: [new SolidMaterial(Blocks.Cobblestone), new SolidMaterial(Blocks.Stone, Andesite)],
                Rise: 2), 2),

            // Five of the banded run. The cycle wraps the whole perimeter rather than restarting per wall, so
            // a corner carries a band round it the way the real building does.
            new RoomCourse(new WallRunMaterial(
            [
                new WallStripe(new LogCheckerMaterial(1, Blocks.Log2), 2),
                new WallStripe(Spruces, 2),
            ]), 5),
        ], Extent: 7),

        // A stair lattice seated on the plinth: sill 3 is the first course above the two the plinth takes, so
        // the light starts where the stonework stops. Two courses tall, which is what a lattice is — four
        // stairs in a 2x2 hole with their raised halves outward.
        //
        // Cut into the spruce panels only. On a wall that bands between planks and acacia logs, a seat chosen
        // by spacing lands half in each, and an opening across that seam reads as damage — so the panel says
        // where a window goes and the band it is not part of stays whole.
        Windows = new WindowStyle
        {
            Form = WindowForm.StairLattice, Block = SpruceStairs, Sill = 3, Spacing = 1,
            HostBlock = Blocks.Planks, HostData = SprucePlanks,
        },

        Floor = RoomPart.Of(Spruces),
        Sill = new SolidMaterial(Blocks.Air),          // it meets the ground flush; there is no footing
        Door = DoorMaterial.Air,
        DoorWidth = 2,
        DoorHeight = 3,
        // Seven rather than five across: a two-wide door wants a block of wall clear of each corner post, and
        // a five-wide face has one cell left after those margins.
    }, Width: 7, Depth: 9);

    /// <summary>
    /// The warm one: end stone and sandstone under a brick roof, with no frame at all.
    ///
    /// <para>Where the alpine house is a frame with panels between it, this is a wall — two courses of end
    /// stone and sandstone above, unbroken by posts, so the corners are wall like everything else. The roof
    /// and its edge are one material, which is what a roof laid in a single thing looks like, and the gable
    /// face comes back down to the end stone the base is in so the two ends of the building answer each
    /// other.</para>
    ///
    /// <para>It is the first style to wear a <b>door head</b>: birch stairs at the two corners of the
    /// opening's top course, turned so the quarter each is missing faces inward and the doorway loses its
    /// square top.</para>
    /// </summary>
    public static House Desert => new("desert brick", new HouseStyle
    {
        Form = RoofForm.Gable,
        Pitch = 1,
        Overhang = 1,

        // One material for the roof and its edge both: a roof laid in one thing, which is what a brick roof is.
        Roof = new SolidMaterial(Brick),
        Verge = new SolidMaterial(Brick),
        Gable = new SolidMaterial(Blocks.EndStone),

        Post = null,                                            // no frame; the corners are wall
        Wall = new RoomPart(
        [
            new RoomCourse(new SolidMaterial(Blocks.EndStone), 2),
            new RoomCourse(new SolidMaterial(Blocks.Sandstone), 5),
        ], Extent: 7),

        // A course higher than the alpine house's, so the light sits above the end stone rather than on it.
        Windows = new WindowStyle
        {
            Form = WindowForm.StairLattice, Block = BirchStairs, Sill = 4, Spacing = 3,
        },

        Floor = RoomPart.Of(new SolidMaterial(Blocks.Sandstone)),
        Sill = new SolidMaterial(Blocks.Air),
        Door = DoorMaterial.Air,
        DoorWidth = 2,
        DoorHeight = 3,
        DoorHead = new DoorHeadStyle
        {
            Form = DoorHeadForm.Arched,
            Block = BirchStairs,
            Fill = DoorHeadFill.UpperSlab,
            FillBlock = Blocks.WoodenSlab,
            FillData = BirchSlab,
        },
    }, Width: 7, Depth: 9);

    /// <summary>
    /// The stone one: two storeys of five, diorite under light blue clay, beneath a brick pyramid that climbs
    /// half a block at a time.
    ///
    /// <para>The roof is the new thing. A hip over a footprint is a pyramid, and at a whole course of rise per
    /// block it comes up steep and wants stairs; laid in slabs at that pitch it would leave an open half between
    /// every pair and be seen straight through. On <b>half</b> courses it is the roof a slab is actually for —
    /// brick cubes on the even steps and brick slabs on the odd ones, climbing at forty-five degrees to the
    /// eye rather than to the block grid.</para>
    ///
    /// <para>Its windows are the other new thing, and the simplest: a hole two by two with nothing in it,
    /// taking the third and fourth courses of <em>each</em> storey — which the storey stack gives for free,
    /// since a storey seats its windows in its own frame.</para>
    /// </summary>
    public static House Diorite => new("diorite pyramid", new HouseStyle
    {
        Form = RoofForm.Hip,
        Pitch = 1,                                              // one half-course per block: the slab slope
        Overhang = 1,
        RoofSlab = StoneSlab,
        RoofSlabData = BrickSlab,
        Roof = new SolidMaterial(Brick),
        Verge = new SolidMaterial(Brick),

        Post = null,
        Storeys =
        [
            new Storey
            {
                Clear = 5,
                Wall = new RoomPart(
                [
                    new RoomCourse(new SolidMaterial(Blocks.Stone, PolishedDiorite)),
                    new RoomCourse(new SolidMaterial(Blocks.Stone, PlainDiorite), 4),
                ], Extent: 5),
            },
            new Storey
            {
                Clear = 5,
                Wall = RoomPart.Of(new SolidMaterial(Blocks.StainedClay, LightBlue), 5),
            },
        ],

        // Two by two, cut and left. Both storeys take the same, since neither names one of its own.
        Windows = new WindowStyle
        {
            Form = WindowForm.Open, Width = 2, Height = 2, Sill = 3, Spacing = 3,
        },

        Floor = RoomPart.Of(new SolidMaterial(Blocks.Stone, PlainDiorite)),
        Sill = new SolidMaterial(Blocks.Air),
        Door = DoorMaterial.Air,
        DoorWidth = 2,
        DoorHeight = 3,
    }, Width: 7, Depth: 7);

    /// <summary>
    /// The townside house: oak framing over a stone footing, spruce above, and the beam ends left long.
    ///
    /// <para>Two storeys that differ in more than their height — the ground floor is oak-framed over a course of
    /// the same cobble and andesite the alpine house stands on, with white clay in its windows; the one above is
    /// spruce with stair lattices. The <b>beams</b> are the point. Where the floors meet, the wall's own stack
    /// turns to a laid log so a course of bark runs right through the masonry, and at each corner two log ends
    /// carry on out past the building. In plan that seam is a hash: the walls are the square in the middle and
    /// eight sawn ends stand outside it, which is what a building looks like when it was made by laying logs
    /// against each other.</para>
    /// </summary>
    public static House Townside => new("townside", new HouseStyle
    {
        Form = RoofForm.Gable,
        Pitch = 1,
        Overhang = 1,
        Roof = new SolidMaterial(Blocks.Planks, Oak),
        Verge = Spruces,
        Gable = new SolidMaterial(StoneBrick),

        Post = new SolidMaterial(Blocks.Log, Oak),
        Beams = new BeamStyle { Block = Blocks.Log, Data = Oak, Reach = 1 },

        Storeys =
        [
            new Storey
            {
                Clear = 5,
                Wall = new RoomPart(
                [
                    new RoomCourse(new NoiseMaterial(
                        Seed: 0x7C0E, Scale: 2, Octaves: 1,
                        Stops: [new SolidMaterial(Blocks.Cobblestone), new SolidMaterial(Blocks.Stone, Andesite)],
                        Rise: 2)),
                    new RoomCourse(new SolidMaterial(Blocks.Planks, Oak), 4),

                    // The sixth course is the seam the storeys meet on, and it is a log laid along the wall so
                    // its bark faces out — the beam the corner ends belong to, running through the masonry.
                    new RoomCourse(new LaidLogMaterial(Blocks.Log, Oak)),
                ], Extent: 6),
                Windows = new WindowStyle
                {
                    Form = WindowForm.Pane, Block = StainedPane, Data = White,
                    Width = 2, Height = 2, Sill = 3, Spacing = 3,
                },
            },
            new Storey
            {
                Clear = 4,
                Wall = RoomPart.Of(Spruces, 4),
                Post = new SolidMaterial(Blocks.Log, Oak),
                Windows = new WindowStyle
                {
                    Form = WindowForm.StairLattice, Block = SpruceStairs, Sill = 2, Spacing = 3,
                },
            },
        ],

        // A single air hole in each gable face, two courses up so it sits in the middle of the triangle. One
        // by one because that is all a gable this size carries: the face is five cells at its base and three a
        // course up, and anything wider would run into the slope with no gable left beside it.
        GableWindows = new WindowStyle
        {
            Form = WindowForm.Open, Width = 1, Height = 1, Sill = 2,
        },

        Floor = RoomPart.Of(new SolidMaterial(Blocks.Planks, Oak)),
        Sill = new SolidMaterial(Blocks.Air),
        Door = DoorMaterial.Air,
        DoorWidth = 2,
        DoorHeight = 3,
    }, Width: 7, Depth: 9);

    /// <summary>
    /// The townside house up on stilts: the same building with its ground floor opened out.
    ///
    /// <para>Nothing new was needed for it, which is the interesting part. A storey's wall is a stack of
    /// materials and <b>air is a gap rather than a block</b> — a course that resolves to air is skipped, never
    /// written — so a wall of air is a storey with no infill, and what is left standing is the four corner
    /// posts and the floor they carry. The beams still run their ends out of the seam, and the ladder still
    /// climbs to the storey above; with no wall behind it, it climbs through open air, which is what a stilt
    /// house's ladder does.</para>
    /// </summary>
    public static House Stilts => new("townside on stilts", Townside.Style with
    {
        Storeys =
        [
            Townside.Style.Storeys[0] with
            {
                // Air below and the beam course kept: the seam is what carries the floor above, and on a
                // building with nothing under it that is the one course that has to be there.
                Wall = new RoomPart(
                [
                    new RoomCourse(new SolidMaterial(Blocks.Air), 5),
                    new RoomCourse(new LaidLogMaterial(Blocks.Log, Oak)),
                ], Extent: 6),
                Windows = new WindowStyle(),          // there is no wall left to cut one through
            },
            Townside.Style.Storeys[1],
        ],
    }, Width: 7, Depth: 9);
}
