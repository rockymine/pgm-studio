using PgmStudio.Domain;
using PgmStudio.Minecraft.Painting;
using PgmStudio.Minecraft.Palette;

namespace PgmStudio.Minecraft.Houses;

/// <summary>
/// The built-in house styles: the presets the room-style library is seeded with, and what the showcase stands
/// on its own row of islands.
///
/// <para>Here rather than beside the showcase because two things need them — the world that is walked and the
/// seeder that stores them — and this is the lowest project both can reach.</para>
///
/// <para>Each is described from a real building rather than invented, and the description is checked against
/// the world it came from: the corpus map is read block by block, the wall face is printed, and the style is
/// written to match what it says. A style that cannot be said in <see cref="HouseStyle"/> is the interesting
/// case — it is a gap in the model, and finding those is what the row is for.</para>
/// </summary>
public static class HousePresets
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

    // ── the terrace row's own vocabulary ──────────────────────────────────────────────────────────────
    private const int PolishedAndesite = 6;                     // the nibble of block 1
    private const int CobblestoneWall = 139, StoneBrickBlock = 98;
    private const int SmoothSandstone = 2;                      // the nibble of block 24
    private const int StoneBrickStairs = 109, SandstoneStairs = 128;
    private const int SpruceFence = 189, SpruceSlab = 1;        // 1 is the spruce nibble of block 126
    private const int DarkOakSlab = 5;                          // 5 is the dark-oak nibble of block 126
    private const int StoneBrickSlab = 5;                       // 5 is the stone-brick nibble of block 44

    /// <summary>
    /// The masonry the whole row is built out of: stone and <b>polished andesite</b>, alternating a block at a
    /// time.
    ///
    /// <para>They are the pair to reach for because they differ in <em>texture and not in hue</em> — both are
    /// the same grey, one speckled and one flat — so the board reads as coursed stonework rather than as a
    /// chequerboard. That is the whole rule for checkering a wall: a checker states the grid it is laid on, so
    /// the two squares have to be near enough in value that the grid is a texture. Two blocks a player can name
    /// apart across a courtyard — cobble against dark oak, white against black wool — make a draughtboard, and a
    /// draughtboard is the one pattern that can never be mistaken for a building.</para>
    ///
    /// <para>Laid at <b>size 1</b>. The board is read off the wall's own perimeter arc and its height, so a
    /// one-block square carries the alternation round the corners without a seam, and anything larger starts to
    /// read as panels.</para>
    /// </summary>
    private static readonly TerrainMaterial Masonry = new CheckerMaterial(1,
        new SolidMaterial(Blocks.Stone),
        new SolidMaterial(Blocks.Stone, PolishedAndesiteNibble));

    /// <summary>The same two stones scattered rather than boarded — a rubble plinth. The checker is the dressed
    /// wall and this is the footing under it, which is the order a real building puts them in.</summary>
    private static readonly TerrainMaterial Rubble = new NoiseMaterial(
        Seed: 0x3C1D, Scale: 2, Octaves: 1,
        Stops: [new SolidMaterial(Blocks.Cobblestone), new SolidMaterial(Blocks.Stone, Andesite)],
        Rise: 2);

    /// <summary>One house on the row: what to call it, the style, and the footprint it is drawn at.
    ///
    /// <para>The footprint is here rather than on the style because <b>a style may never carry one</b> — a
    /// room's comes from its plan piece and a prop's from the rectangle an author dragged. So the size a house
    /// was designed at travels beside it and is used when it is placed.</para></summary>
    public readonly record struct House(string Name, HouseStyle Style, int Width, int Depth);

    // ── the three an author stated ────────────────────────────────────────────────────────────────────
    // Ids these three name and the ten above do not. Spelled here for the reason every nibble in this file is:
    // a bare number in a palette says nothing about which face of a block it is.
    private const int PolishedAndesiteNibble = 6, ClayBlock = 82;
    private const int SandstoneSlab = 1;                                    // 1 is the sandstone nibble of 44
    private const int NetherBrickStairs = 114, NetherBrickSlab = 6;
    private const int RedMushroomBlock = 100, Pores = 0;                    // 0 is the pore texture on every face
    private const int SmoothSandstoneBlock = 2, BirchPlanks = 2, DarkOakPlank = 5;
    private const int BlackClay = 15, RedWool = 14, DarkOakLog = 1;         // nibbles of 159 / 35 / 162

    /// <summary>Andesite posts, a wall of cobble giving way to stone, a clay gable and a stone-brick roof
    /// trimmed in polished andesite — a mason's own house, grey on grey with one spruce window in it.</summary>
    public static House Stonemason => new("stonemason", new HouseStyle
    {
        Roof = new RoofStyle
        {
            Form = RoofForm.Gable, Pitch = 1, Overhang = 1, RidgeCap = true,
            Body = new SolidMaterial(StoneBrickBlock),
            Verge = new SolidMaterial(Blocks.Stone, PolishedAndesite),
            Gable = new SolidMaterial(ClayBlock),
            GableWindows = new WindowStyle
            {
                Form = WindowForm.StairLattice, Block = SpruceStairs, Width = 2, Height = 2, Sill = 1,
            },
        },
        // The post is the storey's, not the building's — a house whose corners are stated once
        // per level, which is what a stack of storeys each with their own is for.
        Post = null,
        Foundation = new Foundation { Plate = RoomPart.Of(new SolidMaterial(StoneBrickBlock), 1) },
        Storeys = [Ground(
            new SolidMaterial(Blocks.Cobblestone), new SolidMaterial(Blocks.Stone),
            new SolidMaterial(Blocks.Stone, Andesite), SpruceStairs)],
        Doorway = Arched(StoneBrickStairs, StoneSlab, StoneBrickSlab),
    }, 11, 9);

    /// <summary>Sandstone posts under smooth sandstone and birch, a red mushroom cap for a verge and its
    /// pores for the gable, all under a red wool roof — sandy, and deliberately cheerful.</summary>
    public static House SandyMushroom => new("sandy mushroom", new HouseStyle
    {
        Roof = new RoofStyle
        {
            Form = RoofForm.Gable, Pitch = 1, Overhang = 1, RidgeCap = true,
            Body = new SolidMaterial(Blocks.Wool, RedWool),
            Verge = new SolidMaterial(RedMushroomBlock, AllCap),
            Gable = new SolidMaterial(RedMushroomBlock, Pores),
            GableWindows = new WindowStyle
            {
                Form = WindowForm.StairLattice, Block = SpruceStairs, Width = 2, Height = 2, Sill = 1,
            },
        },
        // The post is the storey's, not the building's — a house whose corners are stated once
        // per level, which is what a stack of storeys each with their own is for.
        Post = null,
        Foundation = new Foundation { Plate = RoomPart.Of(new SolidMaterial(Blocks.Sandstone), 1) },
        Storeys = [Ground(
            new SolidMaterial(Blocks.Sandstone, SmoothSandstoneBlock),
            new SolidMaterial(Blocks.Planks, BirchPlanks),
            new SolidMaterial(Blocks.Sandstone), SpruceStairs)],
        Doorway = Arched(SandstoneStairs, StoneSlab, SandstoneSlab),
    }, 11, 9);

    /// <summary>Dark oak logs and planks over a black clay base, a cobble gable, and a roof <b>laid</b> in
    /// spruce logs that run the length of the ridge — no verge of its own, so the log reaches the eave.</summary>
    public static House Darkwood => new("darkwood", new HouseStyle
    {
        Roof = new RoofStyle
        {
            Form = RoofForm.Gable, Pitch = 1, Overhang = 1, RidgeCap = false,
            Body = new LaidLogMaterial(Blocks.Log, Spruce),
            Verge = new LaidLogMaterial(Blocks.Log, Spruce),
            Gable = new SolidMaterial(Blocks.Cobblestone),
            GableWindows = new WindowStyle
            {
                Form = WindowForm.StairLattice, Block = NetherBrickStairs, Width = 2, Height = 2, Sill = 1,
            },
        },
        // The post is the storey's, not the building's — a house whose corners are stated once
        // per level, which is what a stack of storeys each with their own is for.
        Post = null,
        Foundation = new Foundation { Plate = RoomPart.Of(new SolidMaterial(Blocks.Cobblestone), 1) },
        Storeys = [Ground(
            new SolidMaterial(Blocks.StainedClay, BlackClay),
            new SolidMaterial(Blocks.Planks, DarkOakPlank),
            new SolidMaterial(Blocks.Log2, DarkOakLog), NetherBrickStairs)],
        Doorway = Arched(NetherBrickStairs, StoneSlab, NetherBrickSlab),
    }, 11, 9);

    /// <summary>The one storey all three are: two courses of <paramref name="base"/> under a run of
    /// <paramref name="body"/>, a post of its own, and a stair lattice seated where the two meet.</summary>
    private static Storey Ground(TerrainMaterial @base, TerrainMaterial body, TerrainMaterial post, int window)
        => new()
        {
            Clear = 5,
            Post = post,
            // A plain floor: no border ring and no inlay, so what a player walks on is the floor part's own
            // top course. Stated rather than left null because a storey with no surface at all is a storey
            // that has said nothing about its floor, which is a different thing from one that has said plain.
            Surface = new FloorSurface { BorderWidth = 1, InlayInset = 2 },
            Wall = new RoomPart(new BandStack([new Band(@base, 2), new Band(body, 4)]), Extent: 5),
            Windows = new WindowStyle
            {
                Form = WindowForm.StairLattice, Block = window, Width = 2, Height = 2, Sill = 2, Spacing = 3,
            },
        };

    /// <summary>An open doorway three courses tall under an arched head — stairs in the corners and a slab
    /// spanning the middle, both cut from one material, which is what HS4 asks of the pair.</summary>
    private static Doorway Arched(int stairs, int slab, int slabData) => new()
    {
        Door = DoorMaterial.Air, Width = 2, Height = 3,
        Head = new DoorHeadStyle
        {
            Form = DoorHeadForm.Arched, Block = stairs,
            Fill = DoorHeadFill.UpperSlab, FillBlock = slab, FillData = slabData,
        },
    };



    public static IReadOnlyList<House> All =>
    [
        Alpine, Desert, Diorite, Townside, Stilts, Cottage, Longhouse, Terrace, Counting, Workshop,
        Stonemason, SandyMushroom, Darkwood,
    ];

    /// <summary>The three an author stated in words rather than in code — a mason's house, a mushroom cottage
    /// and a dark timber one. They are the first buildings here that came from a person, and they are kept as
    /// presets for that reason: nothing can re-derive a choice.
    ///
    /// <para>All three are one storey under a gable and share a shape the ten above do not: the wall is two
    /// courses of one material under a run of another, stated as a stack rather than as a frame, and the
    /// gable is a third material that answers neither.</para></summary>
    public static IReadOnlyList<House> Authored => [Stonemason, SandyMushroom, Darkwood];

    /// <summary>The five that are one settlement rather than five samples: a cottage, a hall, a terrace, a
    /// counting house and a workshop, all cut from the same <see cref="Masonry"/> and the same spruce, so a
    /// village built out of them reads as one place. Each is under the 192 blocks of footprint a dressing
    /// building is allowed, so every one of them can be dragged onto a map.</summary>
    public static IReadOnlyList<House> Village => [Cottage, Longhouse, Terrace, Counting, Workshop];

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
        Roof = new RoofStyle
        {
            Form = RoofForm.Gable,
            Pitch = 1,
            Overhang = 1,
            Body = Spruces,
            Verge = DarkOak,
            // The face the slopes leave, in mushroom block — the one piece of this building that is a plain
            // panel, and the reason the gable is nameable at all: unbound it carries the wall's top course up,
            // and the top course here is the banded run.
            Gable = new SolidMaterial(BrownMushroomBlock, AllCap),
        },

        Post = SpruceLog,
        Wall = new RoomPart(new BandStack(
        [
            // Two courses of the plinth. The scale is 2 because the real one scatters nearly block by block —
            // at 3 a whole five-block face came out one stone and the mix was invisible. Rise 2 so the second
            // course is not a copy of the first, which is what the real wall does: its two courses of cobble
            // and andesite fall differently.
            new Band(new NoiseMaterial(
                Seed: 0x5A17, Scale: 2, Octaves: 1,
                Stops: [new SolidMaterial(Blocks.Cobblestone), new SolidMaterial(Blocks.Stone, Andesite)],
                Rise: 2), 2),

            // Five of the banded run. The cycle wraps the whole perimeter rather than restarting per wall, so
            // a corner carries a band round it the way the real building does.
            new Band(new WallRunMaterial(
            [
                new WallStripe(new LogCheckerMaterial(1, Blocks.Log2), 2),
                new WallStripe(Spruces, 2),
            ]), 5),
        ]), Extent: 7),

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

        Foundation = new Foundation
        {
            Plate = RoomPart.Of(Spruces),
            Footing = null,   // it meets the ground flush; there is no footing
        },
        Doorway = new Doorway { Door = DoorMaterial.Air, Width = 2, Height = 3 },
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
        Roof = new RoofStyle
        {
            Form = RoofForm.Gable,
            Pitch = 1,
            Overhang = 1,
            // One material for the roof and its edge both: a roof laid in one thing, which is what a brick roof is.
            Body = new SolidMaterial(Brick),
            Verge = new SolidMaterial(Brick),
            Gable = new SolidMaterial(Blocks.EndStone),
        },

        Post = null,                                            // no frame; the corners are wall
        Wall = new RoomPart(new BandStack(
        [
            new Band(new SolidMaterial(Blocks.EndStone), 2),
            new Band(new SolidMaterial(Blocks.Sandstone), 5),
        ]), Extent: 7),

        // A course higher than the alpine house's, so the light sits above the end stone rather than on it.
        Windows = new WindowStyle
        {
            Form = WindowForm.StairLattice, Block = BirchStairs, Sill = 4, Spacing = 3,
        },

        Foundation = new Foundation
        {
            Plate = RoomPart.Of(new SolidMaterial(Blocks.Sandstone)),
            Footing = null,
        },
        Doorway = new Doorway
        {
            Door = DoorMaterial.Air,
            Width = 2,
            Height = 3,
            Head = new DoorHeadStyle
            {
                Form = DoorHeadForm.Arched,
                Block = BirchStairs,
                Fill = DoorHeadFill.UpperSlab,
                FillBlock = Blocks.WoodenSlab,
                FillData = BirchSlab,
            },
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
        Roof = new RoofStyle
        {
            Form = RoofForm.Hip,
            Pitch = 1,                                              // one half-course per block: the slab slope
            Overhang = 1,
            Slab = StoneSlab,
            SlabData = BrickSlab,
            Body = new SolidMaterial(Brick),
            Verge = new SolidMaterial(Brick),
        },

        Post = null,
        Storeys =
        [
            new Storey
            {
                Clear = 5,
                Wall = new RoomPart(new BandStack(
                [
                    new Band(new SolidMaterial(Blocks.Stone, PolishedDiorite)),
                    new Band(new SolidMaterial(Blocks.Stone, PlainDiorite), 4),
                ]), Extent: 5),
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

        Foundation = new Foundation
        {
            Plate = RoomPart.Of(new SolidMaterial(Blocks.Stone, PlainDiorite)),
            Footing = null,
        },
        Doorway = new Doorway { Door = DoorMaterial.Air, Width = 2, Height = 3 },
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
        Roof = new RoofStyle
        {
            Form = RoofForm.Gable,
            Pitch = 1,
            Overhang = 1,
            Body = new SolidMaterial(Blocks.Planks, Oak),
            Verge = Spruces,
            Gable = new SolidMaterial(StoneBrick),
            // A single air hole in each gable face, two courses up so it sits in the middle of the triangle. One
            // by one because that is all a gable this size carries: the face is five cells at its base and three a
            // course up, and anything wider would run into the slope with no gable left beside it.
            GableWindows = new WindowStyle
            {
                Form = WindowForm.Open, Width = 1, Height = 1, Sill = 2,
            },
        },

        Post = new SolidMaterial(Blocks.Log, Oak),
        Beams = new BeamStyle { Block = Blocks.Log, Data = Oak, Reach = 1 },

        Storeys =
        [
            new Storey
            {
                Clear = 5,
                Wall = new RoomPart(new BandStack(
                [
                    new Band(new NoiseMaterial(
                        Seed: 0x7C0E, Scale: 2, Octaves: 1,
                        Stops: [new SolidMaterial(Blocks.Cobblestone), new SolidMaterial(Blocks.Stone, Andesite)],
                        Rise: 2)),
                    new Band(new SolidMaterial(Blocks.Planks, Oak), 4),

                    // The sixth course is the seam the storeys meet on, and it is a log laid along the wall so
                    // its bark faces out — the beam the corner ends belong to, running through the masonry.
                    new Band(new LaidLogMaterial(Blocks.Log, Oak)),
                ]), Extent: 6),
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

        Foundation = new Foundation
        {
            Plate = RoomPart.Of(new SolidMaterial(Blocks.Planks, Oak)),
            Footing = null,
        },
        Doorway = new Doorway { Door = DoorMaterial.Air, Width = 2, Height = 3 },
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
    ///
    /// <para><b>And the plate goes the same way.</b> A stilt house is raised so the ground can run on
    /// underneath it — a bank, a mire, a shore — so a plate laid across the footprint would put a plank
    /// rectangle on that ground and stop it (<c>HS10</c>). Air there is the same word in the same place doing
    /// the same thing: the terrain the building stands over is what shows between the posts.</para>
    /// </summary>
    public static House Stilts => new("townside on stilts", Townside.Style with
    {
        Foundation = Townside.Style.Foundation with { Plate = RoomPart.Of(new SolidMaterial(Blocks.Air)) },
        Storeys =
        [
            Townside.Style.Storeys[0] with
            {
                // Air below and the beam course kept: the seam is what carries the floor above, and on a
                // building with nothing under it that is the one course that has to be there.
                Wall = new RoomPart(new BandStack(
                [
                    new Band(new SolidMaterial(Blocks.Air), 5),
                    new Band(new LaidLogMaterial(Blocks.Log, Oak)),
                ]), Extent: 6),
                Windows = new WindowStyle(),          // there is no wall left to cut one through
            },
            Townside.Style.Storeys[1],
        ],
    }, Width: 7, Depth: 9);

    // ══ the village ═══════════════════════════════════════════════════════════════════════════════════
    // Five buildings meant to stand together. They share one masonry, one timber and one roof line, so what
    // separates them is what each is *for* — a cottage is small and steep, a hall is long and low, a counting
    // house is tall and narrow — rather than what each is made of. A settlement whose buildings differ in
    // material reads as five settlements; one whose buildings differ in proportion reads as a village.

    /// <summary>
    /// The cottage: one room of checkered masonry under a steep gable, with a timbered face at each end.
    ///
    /// <para>It is the smallest of the five and the one that states the row's palette plainly — a rubble plinth
    /// two courses high, <see cref="Masonry"/> above it, and spruce for everything the roof touches. The gable
    /// is spruce rather than more stone because that is the one move nearly every hand-built house makes: the
    /// wall is what holds the building up and the gable is what closes it, and saying so in a second material
    /// is what stops a small building reading as a box with a lid.</para>
    ///
    /// <para>Its windows are <see cref="WindowForm.Arched"/> — two upside-down stairs rounding the top corners
    /// of a 2×2 hole. On a nine-wide face the seater lays one per side clear of both posts.</para>
    /// </summary>
    public static House Cottage => new("cottage", new HouseStyle
    {
        Roof = new RoofStyle
        {
            Form = RoofForm.Gable,
            Pitch = 1,
            Overhang = 1,
            RidgeCap = true,
            Body = DarkOak,
            Verge = Spruces,
            Gable = Spruces,
            GableWindows = new WindowStyle { Form = WindowForm.Open, Width = 1, Height = 1, Sill = 2 },
        },

        Post = SpruceLog,
        Wall = new RoomPart(new BandStack(
        [
            new Band(Rubble, 2),
            new Band(Masonry, 3),
        ]), Extent: 5),

        Windows = new WindowStyle
        {
            Form = WindowForm.Arched, Block = StoneBrickStairs, Width = 2, Height = 2, Sill = 3, Spacing = 3,
        },

        Foundation = new Foundation
        {
            Plate = RoomPart.Of(Spruces, 2),                    // two courses: the plinth a footing is the foot of
            Footing = new SolidMaterial(Blocks.Cobblestone),
        },
        Doorway = new Doorway
        {
            Door = DoorMaterial.Air,
            Width = 2,
            Height = 3,
            Head = new DoorHeadStyle
            {
                Form = DoorHeadForm.Arched, Block = StoneBrickStairs,
                Fill = DoorHeadFill.UpperSlab, FillBlock = StoneSlab, FillData = StoneBrickSlab,
            },
        },
    }, Width: 9, Depth: 11);

    /// <summary>
    /// The longhouse: a hall for the workforce, twenty-one blocks along and nine across, with a <b>row of
    /// arched windows</b> down each long wall.
    ///
    /// <para>The row is the point, and it is what a long building is for. The seater divides the run between
    /// the two corner posts and spreads as many windows as fit at the spacing asked for, centred on that run —
    /// so the row is symmetric about the middle of the wall rather than starting at one end and stopping when
    /// it runs out, and lengthening the building adds windows instead of stretching the gaps: four a side at
    /// twenty-one wide, five at twenty-five, six at twenty-nine.</para>
    ///
    /// <para><b>It is entered at the gable end, and the row is why.</b> Windows are spread and centred on a
    /// wall's run, and a doorway is centred on the same run, so on a long wall the two land on each other — and
    /// a seat a door meets is dropped rather than shifted. Entered in the middle of its long side this building
    /// loses the two windows either side of its door and reads as a row with a hole punched in it; entered at
    /// the end it keeps all four a side, and a hall is entered at the end anyway.</para>
    ///
    /// <para>Its proportions are the other half. The roof is measured across the building's <em>shorter</em>
    /// side whatever its length, so a hall gets a long ridge and not a tall one, and the pitch is left at one:
    /// a steep roof on a building this long would be all roof.</para>
    /// </summary>
    public static House Longhouse => new("longhouse", new HouseStyle
    {
        Roof = new RoofStyle
        {
            Form = RoofForm.Gable,
            Pitch = 1,                                              // long and low; a hall is not a steeple
            Overhang = 1,
            RidgeCap = true,
            Body = Spruces,
            Verge = DarkOak,
            Gable = DarkOak,
        },
        Front = RoomEdge.NegX,                               // the gable end: the long walls keep their rows

        Post = SpruceLog,
        Wall = new RoomPart(new BandStack(
        [
            new Band(Rubble, 1),
            new Band(Masonry, 2),
            new Band(Spruces, 3),
        ]), Extent: 6),

        // Cut into the spruce only, so the row sits in the boarding above the stonework rather than across the
        // line where the two meet — the seam is where an opening reads as damage.
        Windows = new WindowStyle
        {
            Form = WindowForm.Arched, Block = SpruceStairs, Width = 2, Height = 2, Sill = 4, Spacing = 2,
            HostBlock = Blocks.Planks, HostData = SprucePlanks,
        },

        Foundation = new Foundation
        {
            Plate = RoomPart.Of(Spruces, 2),                    // two courses: the plinth a footing is the foot of
            Footing = new SolidMaterial(Blocks.Cobblestone),
        },
        Beams = new BeamStyle { Block = Blocks.Log, Data = Spruce, Reach = 1 },
        Doorway = new Doorway
        {
            Door = DoorMaterial.Air,
            Width = 3,
            Height = 4,                                         // a hall takes a cart, not a person
            Head = new DoorHeadStyle
            {
                Form = DoorHeadForm.Arched, Block = SpruceStairs,
                Fill = DoorHeadFill.UpperSlab, FillBlock = Blocks.WoodenSlab, FillData = SpruceSlab,
            },
        },
    }, Width: 21, Depth: 9);

    /// <summary>
    /// The terrace house: a room with an open deck on top of it, walled by a parapet and roofed by nothing.
    ///
    /// <para><b>It needed no new machinery, and that is the interesting part.</b> Air is a gap rather than a
    /// block everywhere in a style, so every piece of this is something taken away. The upper storey's wall is
    /// one course of cobblestone wall over two courses of air, which is a parapet. Its post is air, or four
    /// columns would stand at the corners of the deck. And the lid is a <see cref="RoofForm.Flat"/> roof laid
    /// in air, which writes nothing at all — so the top of the stack is open to the sky.</para>
    ///
    /// <para>What holds it up is the storey below's <c>Ceiling</c>: the slab across the interior
    /// is the deck a player stands on, and the ladder that any stack of storeys carries is the way up onto it.
    /// The one cost is that the parapet storey still states the three blocks of clear a storey may not go under,
    /// so the building reserves two courses it never writes (G171).</para>
    /// </summary>
    public static House Terrace => new("terrace", new HouseStyle
    {
        Roof = new RoofStyle
        {
            Form = RoofForm.Flat,
            Overhang = 0,
            Body = new SolidMaterial(Blocks.Air),                   // no lid: the top of the stack is the sky
            Verge = new SolidMaterial(Blocks.Air),
        },

        Post = new SolidMaterial(StoneBrickBlock),
        Storeys =
        [
            new Storey
            {
                Clear = 5,
                // The last course is the cornice: the deck's own line, said on the outside of the building.
                // Without it a terrace is a grey box with a lip, and nothing tells a player where the floor is.
                Wall = new RoomPart(new BandStack(
                [
                    new Band(Rubble, 1),
                    new Band(Masonry, 4),
                    new Band(new SolidMaterial(StoneBrickBlock), 1),
                ]), Extent: 6),
                Windows = new WindowStyle
                {
                    Form = WindowForm.Arched, Block = StoneBrickStairs,
                    Width = 2, Height = 3, Sill = 2, Spacing = 3,
                },
            },
            new Storey
            {
                Deck = new SolidMaterial(StoneSlab, StoneBrickSlab),   // the deck underfoot
                Clear = 3,                                      // the least a storey may state
                Wall = new RoomPart(new BandStack(
                [
                    new Band(new SolidMaterial(CobblestoneWall), 1),
                    new Band(new SolidMaterial(Blocks.Air), 2),
                ]), Extent: 3),
                Post = new SolidMaterial(Blocks.Air),           // nothing standing on the deck
                Windows = new WindowStyle(),                    // no wall left to cut one through
            },
        ],

        Foundation = new Foundation
        {
            Plate = RoomPart.Of(new SolidMaterial(StoneBrickBlock), 2),
            Footing = new SolidMaterial(Blocks.Cobblestone),
        },
        Doorway = new Doorway
        {
            Door = DoorMaterial.Air,
            Width = 2,
            Height = 3,
            Head = new DoorHeadStyle
            {
                Form = DoorHeadForm.Arched, Block = StoneBrickStairs,
                Fill = DoorHeadFill.UpperSlab, FillBlock = StoneSlab, FillData = StoneBrickSlab,
            },
        },
    }, Width: 11, Depth: 9);

    /// <summary>
    /// The counting house: three storeys on a nine-by-nine footprint under a hip, the settlement's tall one.
    ///
    /// <para>It differs from its neighbours in <b>proportion rather than palette</b> — the same masonry, the
    /// same spruce, stacked instead of spread. A hip is the roof for it because a hip comes down to the wall
    /// line on all four sides and so has no gable face to fill, which is what a building seen from every side at
    /// once wants; over a square footprint it is a pyramid, since the ridge is whatever run the longer side has
    /// left over and a square leaves none.</para>
    ///
    /// <para>The storeys narrow as they rise, in weight rather than in plan: stone at the bottom, boarding above
    /// it, and smooth sandstone at the top where the wall carries least. It shows its floors from outside by
    /// changing material at each seam and not by beam ends: a beam end is the end of a floor timber and none of
    /// these walls is carrying one, so eight log ends running out of masonry would be a detail about a way of
    /// building this house is not built (<c>HS9</c>).</para>
    /// </summary>
    public static House Counting => new("counting house", new HouseStyle
    {
        Roof = new RoofStyle
        {
            Form = RoofForm.Hip,
            Pitch = 1,
            Overhang = 1,
            Slab = StoneSlab,                                   // half courses: the slope a slab is for
            SlabData = StoneBrickSlab,
            Body = new SolidMaterial(StoneBrickBlock),
            Verge = new SolidMaterial(StoneBrickBlock),
        },

        Post = SpruceLog,
        Storeys =
        [
            new Storey
            {
                Clear = 4,
                Wall = new RoomPart(new BandStack([new Band(Rubble, 1), new Band(Masonry, 3)]), Extent: 4),
                Windows = new WindowStyle
                {
                    Form = WindowForm.Arched, Block = StoneBrickStairs,
                    Width = 2, Height = 2, Sill = 2, Spacing = 3,
                },
            },
            new Storey
            {
                Deck = new SolidMaterial(Blocks.Planks, SprucePlanks),
                Clear = 3,
                Wall = RoomPart.Of(Spruces, 3),
                Windows = new WindowStyle
                {
                    Form = WindowForm.Pane, Block = StainedPane, Data = White,
                    Width = 2, Height = 2, Sill = 1, Spacing = 3,
                },
            },
            new Storey
            {
                Deck = new SolidMaterial(Blocks.Planks, SprucePlanks),
                Clear = 3,
                Wall = RoomPart.Of(new SolidMaterial(Blocks.Sandstone, SmoothSandstone), 3),
                Windows = new WindowStyle
                {
                    Form = WindowForm.Arched, Block = SandstoneStairs,
                    Width = 2, Height = 2, Sill = 1, Spacing = 3,
                },
            },
        ],

        Foundation = new Foundation
        {
            Plate = RoomPart.Of(new SolidMaterial(StoneBrickBlock), 2),
            Footing = new SolidMaterial(Blocks.Cobblestone),
        },
        Doorway = new Doorway
        {
            Door = DoorMaterial.Air,
            Width = 2,
            Height = 3,
            Head = new DoorHeadStyle
            {
                Form = DoorHeadForm.Arched, Block = StoneBrickStairs,
                Fill = DoorHeadFill.UpperSlab, FillBlock = StoneSlab, FillData = StoneBrickSlab,
            },
        },
    }, Width: 9, Depth: 9);

    /// <summary>
    /// The workshop: a shed-roofed shop with its front given up to a porch.
    ///
    /// <para>A shed is one plane falling toward the door, which is what an outbuilding wears and what makes it
    /// read as secondary to the hall beside it without being smaller than one. The porch is a strip of the
    /// building's own footprint the walls stand back from — taken <em>out</em> of the house rather than added to
    /// it — with the deck, the posts and the rail on the ground the walls gave up, and its own little canopy
    /// seated to clear the doorway it fronts.</para>
    ///
    /// <para>Its floor is the one in the row that is divided in plan: a border of stone brick hugging the walls
    /// and spruce across the rest, which is the zoning a workshop floor actually has.</para>
    /// </summary>
    public static House Workshop => new("workshop", new HouseStyle
    {
        Roof = new RoofStyle
        {
            Form = RoofForm.Shed,
            Pitch = 1,
            Overhang = 1,
            // A shed climbs its whole shorter span where a gable climbs half of one, so on anything but a shallow
            // building it comes out all roof — eleven courses over a six-course wall. Half courses are the lever:
            // the slope travels one half per block instead of two, so the same lean-to costs half the height.
            Slab = Blocks.WoodenSlab,
            SlabData = DarkOakSlab,                             // the slab continues the body, so it is its material
            Body = DarkOak,
            Verge = Spruces,
            Gable = Spruces,
        },

        Post = SpruceLog,
        Wall = new RoomPart(new BandStack(
        [
            new Band(Rubble, 1),
            new Band(Masonry, 2),
            new Band(Spruces, 3),
        ]), Extent: 6),

        Windows = new WindowStyle
        {
            Form = WindowForm.SlabBanded, Block = Blocks.WoodenSlab, Data = SpruceSlab,
            Width = 3, Sill = 4, Spacing = 3,
            HostBlock = Blocks.Planks, HostData = SprucePlanks,
        },

        Porch = new PorchStyle
        {
            Depth = 2,
            Inset = 1,                                          // a feature of the front, not the front itself
            Roof = RoofForm.Shed,
            RailBlock = SpruceFence,
        },

        Foundation = new Foundation
        {
            Plate = RoomPart.Of(Spruces, 2),                    // two courses: the plinth a footing is the foot of
            Surface = new FloorSurface
            {
                Field = Spruces,
                Border = new SolidMaterial(StoneBrickBlock),
                BorderWidth = 1,
            },
            Footing = new SolidMaterial(Blocks.Cobblestone),
        },
        Doorway = new Doorway
        {
            Door = DoorMaterial.Air,
            Width = 3,
            Height = 3,
            Head = new DoorHeadStyle
            {
                Form = DoorHeadForm.Arched, Block = SpruceStairs,
                Fill = DoorHeadFill.UpperSlab, FillBlock = Blocks.WoodenSlab, FillData = SpruceSlab,
            },
        },
    }, Width: 13, Depth: 11);
}
