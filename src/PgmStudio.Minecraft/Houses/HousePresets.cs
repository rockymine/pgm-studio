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
    private const int StoneSlab = 44;
    private const int Oak = 0, Jungle = 3, White = 0;           // the nibbles of block 17 and of block 160
    private const int StoneBrick = 98, StainedPane = 160;

    private static readonly TerrainMaterial Spruces = new SolidMaterial(Blocks.Planks, SprucePlanks);
    private static readonly TerrainMaterial DarkOak = new SolidMaterial(Blocks.Planks, DarkOakPlanks);
    private static readonly TerrainMaterial SpruceLog = new SolidMaterial(Blocks.Log, Spruce);

    // ── the stone-and-spruce houses' own vocabulary ───────────────────────────────────────────────────
    private const int StoneBrickBlock = 98;
    private const int SmoothSandstone = 2;                      // the nibble of block 24
    private const int DoubleSlab = 43, DoubleSandstone = 1;     // 1 is the sandstone nibble of block 43
    private const int StoneBrickStairs = 109;
    private const int StoneBrickSlab = 5;                       // 5 is the stone-brick nibble of block 44

    /// <summary>
    /// The masonry the stone-and-spruce houses are built out of: stone and <b>polished andesite</b>, alternating
    /// a block at a time.
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
    /// wall and this is the course under it, which is the order a real building puts them in.</summary>
    private static readonly TerrainMaterial Rubble = new NoiseMaterial(
        Seed: 0x3C1D, Scale: 2, Octaves: 1,
        Stops: [new SolidMaterial(Blocks.Cobblestone), new SolidMaterial(Blocks.Stone, Andesite)],
        Rise: 2);

    /// <summary>One preset: what the library calls it, the style, and the footprint it is drawn at.
    ///
    /// <para>The footprint is here rather than on the style because <b>a style may never carry one</b> — a
    /// room's comes from its plan piece and a prop's from the rectangle an author dragged. So the size a house
    /// was designed at travels beside it and is used when it is placed.</para></summary>
    public readonly record struct House(string Name, HouseStyle Style, int Width, int Depth);

    // ── the one an author stated ──────────────────────────────────────────────────────────────────────
    private const int PolishedAndesiteNibble = 6;
    private const int NetherBrickStairs = 114, NetherBrickSlab = 6;
    private const int DarkOakPlank = 5, BlackClay = 15, DarkOakLog = 1;    // nibbles of 5 / 159 / 162

    /// <summary>Dark oak logs and planks over a black clay base, a cobble gable, and a roof <b>laid</b> in
    /// spruce logs that run the length of the ridge — no verge of its own, so the log reaches the eave. Nether
    /// brick frames the door and the windows, and the gable's window is stone brick, the cobble's own
    /// colour.</summary>
    public static House Darkwood => new("black-clay-and-dark-oak-house", new HouseStyle
    {
        Roof = new RoofStyle
        {
            Form = RoofForm.Gable, Pitch = 1, Overhang = 1, RidgeCap = false,
            Body = new LaidLogMaterial(Blocks.Log, Spruce),
            Verge = new LaidLogMaterial(Blocks.Log, Spruce),
            Gable = new SolidMaterial(Blocks.Cobblestone),
            GableWindows = new WindowStyle
            {
                Form = WindowForm.StairLattice, Block = StoneBrickStairs, Width = 2, Height = 2, Sill = 1,
            },
        },
        Post = null,
        Foundation = new Foundation { Plate = RoomPart.Of(new SolidMaterial(Blocks.Cobblestone), 1) },
        Storeys =
        [
            new Storey
            {
                Clear = 5,
                Post = new SolidMaterial(Blocks.Log2, DarkOakLog),
                Surface = new FloorSurface { BorderWidth = 1, InlayInset = 2 },
                Wall = new RoomPart(new BandStack(
                [
                    new Band(new SolidMaterial(Blocks.StainedClay, BlackClay), 2),
                    new Band(new SolidMaterial(Blocks.Planks, DarkOakPlank), 4),
                ]), Extent: 5),
                Windows = new WindowStyle
                {
                    Form = WindowForm.StairLattice, Block = NetherBrickStairs, Width = 2, Height = 2, Sill = 2,
                    Spacing = 3,
                },
            },
        ],
        Doorway = new Doorway
        {
            Door = DoorMaterial.Air, Width = 2, Height = 3,
            Head = new DoorHeadStyle
            {
                Form = DoorHeadForm.Arched, Block = NetherBrickStairs,
                Fill = DoorHeadFill.UpperSlab, FillBlock = StoneSlab, FillData = NetherBrickSlab,
            },
        },
    }, 11, 9);

    public static IReadOnlyList<House> All =>
        [Alpine, Desert, Townside, Stilts, Cottage, Longhouse, Counting, Darkwood];

    /// <summary>
    /// The house styles boards are built with, each kept as the stamper's own JSON in a file of its name under
    /// <c>Houses/Kept</c> and seeded into the room-style library beside the presets, so a board names one as
    /// <c>{"library": "banded-stone-house"}</c> and every studio holds it from its first start. They stand on no row
    /// of the showcase: a style carries no footprint, and these were drawn for the boards that stamp them.
    /// </summary>
    public static IReadOnlyList<(string Name, HouseStyle Style)> Kept => kept.Value;

    private const string KeptResource = "kept-house/";

    private static readonly Lazy<IReadOnlyList<(string Name, HouseStyle Style)>> kept = new(() =>
    {
        var assembly = typeof(HousePresets).Assembly;
        return [.. assembly.GetManifestResourceNames()
            .Where(resource => resource.StartsWith(KeptResource, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(resource =>
            {
                using var stream = assembly.GetManifestResourceStream(resource)!;
                using var reader = new StreamReader(stream);
                return (resource[KeptResource.Length..^".json".Length], HouseStyleJson.Deserialize(reader.ReadToEnd()));
            })];
    });

    /// <summary>The preset an author stated in words rather than in code — the dark timber house. It is kept for
    /// that reason: nothing can re-derive a choice.</summary>
    public static IReadOnlyList<House> Authored => [Darkwood];

    /// <summary>
    /// A spruce-framed house of acacia logs on a mixed stone plinth.
    ///
    /// <para>Read off <c>alpine_mining_ii</c>. Its walls run seven courses between spruce log posts that stand
    /// the <b>full</b> height — the plinth does not turn the corner, it sits between them. The bottom two
    /// courses are cobble and andesite mixed (across the map, the block under the lowest mushroom of a column
    /// is andesite 80 times and cobble 50), and the two courses differ from each other, so the field is sampled
    /// over the volume rather than the plane. Above them the wall is acacia logs turned upright and on their
    /// side by turns, a log checker of one wood that is not the posts' own.</para>
    /// </summary>
    public static House Alpine => new("acacia-log-checkered-house", new HouseStyle
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

            // Five of the acacia log checker, the whole wall above the plinth.
            new Band(new LogCheckerMaterial(1, Blocks.Log2), 5),
        ]), Extent: 7),

        // A stair lattice seated on the plinth: sill 3 is the first course above the two the plinth takes, so
        // the light starts where the stonework stops. Two courses tall, which is what a lattice is — four
        // stairs in a 2x2 hole with their raised halves outward, in spruce against the acacia.
        Windows = new WindowStyle
        {
            Form = WindowForm.StairLattice, Block = SpruceStairs, Sill = 3, Spacing = 1,
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
    /// The warm one: end stone and sandstone under a brick roof, between pillars of smooth sandstone.
    ///
    /// <para>Two courses of end stone and sandstone above, and at each corner a smooth sandstone pillar, the
    /// same stone dressed. The roof and its edge are one material, which is what a roof laid in a single thing
    /// looks like, and the gable face comes back down to the end stone the base is in so the two ends of the
    /// building answer each other.</para>
    ///
    /// <para>It is the first style to wear a <b>door head</b>: birch stairs at the two corners of the
    /// opening's top course, turned so the quarter each is missing faces inward and the doorway loses its
    /// square top.</para>
    /// </summary>
    public static House Desert => new("brick-roofed-sandstone-house", new HouseStyle
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

        Post = new SolidMaterial(Blocks.Sandstone, SmoothSandstone),
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
    /// The townside house: oak framing over a stone plinth, spruce above, and the beam ends left long.
    ///
    /// <para>Two storeys that differ in more than their height — the ground floor is oak-framed over a course of
    /// the same cobble and andesite the alpine house stands on, with white clay in its windows; the one above is
    /// spruce with stair lattices. The <b>beams</b> are the point. Where the floors meet, the wall's own stack
    /// turns to a laid log so a course of bark runs right through the masonry, and at each corner two log ends
    /// carry on out past the building. In plan that seam is a hash: the walls are the square in the middle and
    /// eight sawn ends stand outside it, which is what a building looks like when it was made by laying logs
    /// against each other.</para>
    /// </summary>
    public static House Townside => new("oak-and-spruce-timbered-house", new HouseStyle
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
    /// The timbered house up on stilts: the same building with its ground floor opened out, under a jungle
    /// verge.
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
    public static House Stilts => new("jungle-trimmed-stilt-house", Townside.Style with
    {
        Roof = Townside.Style.Roof with { Verge = new SolidMaterial(Blocks.Planks, Jungle) },
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
            // The room over the stilts keeps the townside's floor: a storey naming no deck stands on the plate's
            // top course, and the plate here is air.
            Townside.Style.Storeys[1] with { Deck = Townside.Style.Foundation.Deck },
        ],
    }, Width: 7, Depth: 9);

    // ══ the stone-and-spruce houses ═══════════════════════════════════════════════════════════════════
    // Buildings meant to stand together. The cottage and the longhouse share one masonry, one timber and one
    // roof line, so what separates them is their proportion — the cottage is small and steep, the longhouse long
    // and low — and the townhouse stands on the same masonry and rises in sandstone.

    /// <summary>
    /// The cottage: one room of checkered masonry under a steep gable, with a timbered face at each end.
    ///
    /// <para>It is the smallest of them and the one that states the palette plainly — a rubble plinth
    /// two courses high, <see cref="Masonry"/> above it, and a spruce roof and gable bordered in dark oak. The
    /// gable is spruce rather than more stone because that is the one move nearly every hand-built house makes:
    /// the wall is what holds the building up and the gable is what closes it, and saying so in a second material
    /// is what stops a small building reading as a box with a lid.</para>
    ///
    /// <para>Its windows are <see cref="WindowForm.Arched"/> — two upside-down stairs rounding the top corners
    /// of a 2×2 hole. On a nine-wide face the seater lays one per side clear of both posts.</para>
    /// </summary>
    public static House Cottage => new("spruce-roofed-stone-cottage", new HouseStyle
    {
        Roof = new RoofStyle
        {
            Form = RoofForm.Gable,
            Pitch = 1,
            Overhang = 1,
            RidgeCap = true,
            Body = Spruces,
            Verge = DarkOak,
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

        Foundation = new Foundation { Plate = RoomPart.Of(Spruces, 2) },
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
    public static House Longhouse => new("spruce-roofed-stone-longhouse", new HouseStyle
    {
        Roof = new RoofStyle
        {
            Form = RoofForm.Gable,
            Pitch = 1,                                              // long and low; a hall is not a steeple
            Overhang = 1,
            RidgeCap = true,
            Body = Spruces,
            Verge = DarkOak,
            Gable = Spruces,
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

        Foundation = new Foundation { Plate = RoomPart.Of(Spruces, 2) },
        Beams = new BeamStyle { Block = Blocks.Log, Data = Spruce, Reach = 1 },
        Doorway = new Doorway
        {
            Door = DoorMaterial.Air,
            Width = 3,                                          // a hall's door is wide
            Height = 3,
            Head = new DoorHeadStyle
            {
                Form = DoorHeadForm.Arched, Block = StoneBrickStairs,
                Fill = DoorHeadFill.UpperSlab, FillBlock = StoneSlab, FillData = StoneBrickSlab,
            },
        },
    }, Width: 21, Depth: 9);

    /// <summary>
    /// A townhouse: three storeys on a nine-by-nine footprint under a hip, a stone ground floor and two of
    /// sandstone over it.
    ///
    /// <para>A hip is the roof for it because a hip comes down to the wall line on all four sides and so has no
    /// gable face to fill, which is what a building seen from every side at once wants; over a square footprint
    /// it is a pyramid, since the ridge is whatever run the longer side has left over and a square leaves
    /// none.</para>
    ///
    /// <para>The ground floor is the stone-and-spruce houses' own: a rubble course, <see cref="Masonry"/>, and a
    /// laid spruce course on top between spruce log corners, which is the frame the floor above stands on. The
    /// two storeys over it are sandstone, a plain course under double sandstone slabs, between smooth sandstone
    /// pillars, and their windows are birch: a stair lattice on the first, arched on the second.</para>
    /// </summary>
    public static House Counting => new("stone-and-sandstone-townhouse", new HouseStyle
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

        Post = null,
        Storeys =
        [
            new Storey
            {
                Clear = 4,
                Post = SpruceLog,
                Wall = new RoomPart(new BandStack(
                    [new Band(Rubble, 1), new Band(Masonry, 3), new Band(new LaidLogMaterial(Blocks.Log, Spruce), 1)]),
                    Extent: 5),
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
                Post = new SolidMaterial(Blocks.Sandstone, SmoothSandstone),
                Wall = Sandstone(courses: 4),
                Windows = new WindowStyle
                {
                    Form = WindowForm.StairLattice, Block = BirchStairs,
                    Width = 2, Height = 2, Sill = 1, Spacing = 3,
                },
            },
            new Storey
            {
                Deck = new SolidMaterial(Blocks.Planks, SprucePlanks),
                Clear = 3,
                Post = new SolidMaterial(Blocks.Sandstone, SmoothSandstone),
                Wall = Sandstone(courses: 3),
                Windows = new WindowStyle
                {
                    Form = WindowForm.Arched, Block = BirchStairs,
                    Width = 2, Height = 2, Sill = 1, Spacing = 3,
                },
            },
        ],

        Foundation = new Foundation { Plate = RoomPart.Of(new SolidMaterial(StoneBrickBlock), 2) },
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

    /// <summary>A sandstone storey's wall: one course of plain sandstone under double sandstone slabs to the
    /// top.</summary>
    private static RoomPart Sandstone(int courses) => new(new BandStack(
    [
        new Band(new SolidMaterial(Blocks.Sandstone), 1),
        new Band(new SolidMaterial(DoubleSlab, DoubleSandstone), courses - 1),
    ]), Extent: courses);
}
