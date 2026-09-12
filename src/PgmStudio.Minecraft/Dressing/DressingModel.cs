using System.Text.Json.Serialization;
using PgmStudio.Geom.Algorithms;
using PgmStudio.Minecraft.Painting;

namespace PgmStudio.Minecraft.Dressing;

/// <summary>The ground cover a flora overlay scatters, and how thickly. Everything about it is a noise field
/// evaluated per cell, so it adds no state and re-exports identically.</summary>
/// <param name="Coverage">0–1; how much of the eligible ground carries anything at all.</param>
/// <param name="Scale">The density field's feature size in blocks — small clumps into speckle, large into
/// meadows and clearings.</param>
/// <param name="Octaves">Octaves of the density field; more is cloudier and finer-grained.</param>
/// <param name="FernShare">0–1; how much of the plain cover is fern rather than grass.</param>
/// <param name="FlowerShare">0–1; how much of the ground the flower field claims. Flowers cluster into
/// <em>fields</em> rather than confetti, which is why they have a field of their own.</param>
/// <param name="FlowerScale">The flower field's feature size — how big a patch of one colour gets.</param>
/// <param name="TallShare">0–1; how much of the plain cover is tall (two-block) grass, which is the part of
/// the overlay that hides a player and so stays off a goal's own ground.</param>
public sealed record FloraSpec(
    double Coverage = 0.45,
    int Scale = 12,
    int Octaves = 3,
    double FernShare = 0.25,
    double FlowerShare = 0.18,
    int FlowerScale = 18,
    double TallShare = 0.0);

/// <summary>The shape family a boulder takes. Each is a list of lobes, not a code path — see
/// <see cref="BoulderShapes"/>.</summary>
public enum BoulderForm
{
    /// <summary>An erratic: one rounded mass standing on the ground, weathered into broad facets.</summary>
    Round,
    /// <summary>The same mass broken up — angular and heavily weathered.</summary>
    Angular,
    /// <summary>Wide, flat lobes with their middle at the surface: a low outcrop rather than a rock.</summary>
    Outcrop,
    /// <summary>Three shrinking lobes stacked up — a cairn.</summary>
    Cairn,
}

/// <summary>
/// Which of the two trees a <see cref="TreeProp"/> is. They are different things, not settings of one thing.
///
/// <para>A <see cref="Template"/> is the vanilla tree: a trunk of a known height under a canopy of a known
/// profile, per species. It is what a player reads as "an oak", and it is what most maps want. A
/// <see cref="Copied"/> tree is whatever an author built, so the studio describes it with no numbers at
/// all.</para>
/// </summary>
public enum TreeForm
{
    /// <summary>The vanilla tree of a named species.</summary>
    Template,
    /// <summary>A tree an author built by hand, cut out of a world and carried block by block as the recipe's
    /// own <see cref="TreeStyle.Body"/>. It has no species, no wood and no knob: what it looks like is what
    /// was built, and the studio's part is only to seat it, turn it round the symmetry and keep its leaves.</summary>
    Copied,
}

/// <summary>What a tree is made of: the log and leaf blocks, named once. It is a species' own row rather than
/// a field on it because a wood is a pair of blocks and a species is a silhouette, and the two are asked for
/// separately — the crown is cut from the profile, the blocks it is written in from here.</summary>
public sealed record TreeWood(string Name, int LogId, int LogData, int LeafId, int LeafData);

/// <summary>A vanilla tree species as data: its wood and its proportions. A species is a row, not a class — the
/// profile is a radius table (<see cref="Geom.Algorithms.CanopyProfiles"/>), so adding one adds no code.
/// <para><b>Height</b> — The species' natural height in blocks: trunk plus canopy. A prop scales from
/// it.</para></summary>
public sealed record TreeSpecies(
    string Name,
    TreeWood Wood,
    Geom.Algorithms.CanopyProfile Profile,
    double Height,
    double CanopyRadius,
    double Lean = 0,
    bool WideTrunk = false)
{
    /// <summary>This species' proportions at <paramref name="height"/> blocks tall. Height scales the canopy
    /// with the trunk, so a small oak is a small oak rather than a full canopy on a stump — and the trunk is
    /// then whatever is left under a canopy of that size, so the tree really is the height it was asked for.</summary>
    public Geom.Algorithms.TemplateShape ShapeAt(double height)
    {
        var wanted = Math.Clamp(height, 4, 60);
        var radius = CanopyRadius * (wanted / Height);
        var trunk = Math.Max(1, wanted - Geom.Algorithms.CanopyProfiles.Rise(Profile, radius));
        return new Geom.Algorithms.TemplateShape(trunk, radius, Profile, Lean * (wanted / Height), WideTrunk);
    }
}

/// <summary>The lobe lists behind <see cref="BoulderForm"/> — style-as-data, the same seam the structure
/// presets use. A form is a shape, so it is a table rather than a branch.</summary>
public static class BoulderShapes
{
    /// <summary>The lobes of a boulder of the given form at <paramref name="size"/> blocks of reach, in the
    /// rock's own frame with the ground at <c>y = 0</c>.
    ///
    /// <para><b>A boulder is an erratic: it stands on the ground and is bedded into it rather than emerging
    /// from it.</b> A rock a glacier left is a mass dropped on a surface, so its bulk is over the ground and
    /// only its foot is under — <see cref="Bed"/> of its height, enough that no course shows daylight beneath
    /// it and enough to seat it on a slope. The one form that genuinely emerges is
    /// <see cref="BoulderForm.Outcrop"/>, and it is the one whose middle stays at the surface.</para>
    ///
    /// <para>The silhouette is <paramref name="seed"/>ed rather than fixed, because two rocks of one form
    /// standing near each other are one rock stamped twice while their lobes are identical. An erratic is a
    /// main mass with a haunch at its foot and a shoulder over it, both thrown out on hashed bearings, so the
    /// plan outline is a rounded irregular blob and the elevation leans. The proportions and what they were
    /// chosen against are <c>docs/world-export/decoration.md</c> §5.</para></summary>
    public static IReadOnlyList<Geom.Algorithms.BlobLobe> Of(BoulderForm form, double size, uint seed) => form switch
    {
        BoulderForm.Angular => Erratic(size, seed, erosion: 0.45),
        BoulderForm.Outcrop =>
        [
            Lobe(0, 0, 0, size * 1.45, size * 0.55, size * 1.2, 0.35),
            Lobe(Cos(seed, 3) * size * 0.7, size * 0.16, Sin(seed, 3) * size * 0.7,
                 size * 0.8, size * 0.42, size * 0.7, 0.3),
        ],
        BoulderForm.Cairn =>
        [
            Lobe(0, size * 0.35, 0, size * 0.9, size * 0.62, size * 0.9, 0.2),
            Lobe(-size * 0.15, size * 1.1, size * 0.1, size * 0.6, size * 0.45, size * 0.6, 0.2),
            Lobe(-size * 0.3, size * 1.7, size * 0.2, size * 0.36, size * 0.3, size * 0.36, 0.15),
        ],
        _ => Erratic(size, seed, erosion: 0.16),
    };

    /// <summary>The share of an erratic's height that stands below the ground: enough to bed it, not enough
    /// to bury it. It is a share rather than a course count, so a rock of any size stands in one
    /// proportion.</summary>
    public const double Bed = 0.30;

    /// <summary>A rock a glacier moved: one mass standing on the ground with a haunch at its foot and a
    /// shoulder over it, each on its own hashed bearing. <paramref name="erosion"/> is what separates the two
    /// round forms — a low one weathers the surface into broad facets, a high one breaks it.</summary>
    private static IReadOnlyList<Geom.Algorithms.BlobLobe> Erratic(double size, uint seed, double erosion)
    {
        var reach = size * 0.95;                     // the main mass's vertical half-reach
        var stand = reach * (1 - 2 * Bed);           // its middle, lifted so only Bed of the height is buried
        return
        [
            Lobe(0, stand, 0, size, reach, size * 0.92, erosion),
            Lobe(Cos(seed, 1) * size * 0.45, stand * 0.35, Sin(seed, 1) * size * 0.45,
                 size * 0.66, reach * 0.72, size * 0.66, erosion),
            Lobe(Cos(seed, 2) * size * 0.38, stand + reach * 0.42, Sin(seed, 2) * size * 0.38,
                 size * 0.55, reach * 0.5, size * 0.55, erosion),
        ];
    }

    // A bearing per lobe, hashed off the rock's own seed so every image of its orbit turns together.
    private static double Cos(uint seed, int lobe) => Math.Cos(Bearing(seed, lobe));
    private static double Sin(uint seed, int lobe) => Math.Sin(Bearing(seed, lobe));
    private static double Bearing(uint seed, int lobe)
        => Geom.Algorithms.PatternNoise.Unit(lobe, 61, seed) * Math.PI * 2;

    private static Geom.Algorithms.BlobLobe Lobe(double x, double y, double z, double rx, double ry, double rz, double erosion)
        => new(new Geom.Vec3(x, y, z), new Geom.Vec3(rx, ry, rz), erosion);
}

/// <summary>
/// A prop's recipe, named once and referenced by every placement that wears it.
///
/// <para><b>What is placed is a position; what it is made of is a recipe.</b> A board carries 618 trees over
/// 75 distinct recipes and 247 boulders over a handful, so a knob per placement is the same answer written out
/// hundreds of times — and one an author cannot change without editing hundreds of placements. A placement
/// therefore names a key into <see cref="DressingDoc.Styles"/>, the way a shape names a key into the layout's
/// theme registry, and the registry is what a library row is pulled into.</para>
///
/// <para><b>The registry is the document's, not the library's.</b> The world export reads a stored document and
/// has no database to resolve a library row against, and a shipped map must build the same way next year as it
/// did today. So a library row is <em>pulled in</em> — copied into the registry under a key — and the key is
/// what every placement carries. Editing the library row changes the next pull, not a map already written.</para>
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(TreeStyle), "tree")]
[JsonDerivedType(typeof(BoulderStyle), "boulder")]
[JsonDerivedType(typeof(HouseStyleRef), "house")]
public abstract record PropStyle;

/// <summary>
/// One tree, as a recipe — and one of <b>two</b> trees, which <see cref="Form"/> picks.
///
/// <para>A <see cref="TreeForm.Template"/> tree is vanilla: <see cref="Species"/> names its wood, its canopy
/// profile and its proportions, and <see cref="Height"/> scales the lot. A <see cref="TreeForm.Copied"/> tree
/// is the blocks an author placed, carried in <see cref="Body"/>. Each form reads only its own fields, so the
/// ones it does not read are inert rather than wrong.</para>
/// </summary>
public sealed record TreeStyle : PropStyle
{
    public TreeForm Form { get; init; } = TreeForm.Template;

    /// <summary>Copied only — the tree's blocks as <c>[x, y, z, id, data]</c> offsets from its foot, the foot
    /// being the lowest wood block: it stands at <c>(0, 0, 0)</c> and every block at <c>y 0</c> rests on the
    /// ground. Leaves carry whatever data they were cut with; the stamp sets the no-decay bit and clears the
    /// game's own check bit, so a copied crown neither rots nor depends on how it was saved.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<int[]>? Body { get; init; }

    /// <summary>The body as cells, dropping any row that does not carry five numbers. A document is read by
    /// hand as often as by a tool, and a short row is a slip rather than a reason to refuse the whole recipe.</summary>
    [JsonIgnore] public IEnumerable<(int X, int Y, int Z, int Id, int Data)> BodyCells =>
        (Body ?? []).Where(row => row.Length >= 5).Select(row => (row[0], row[1], row[2], row[3], row[4]));

    /// <summary>How many courses the body stands, or nought where it carries none.</summary>
    [JsonIgnore] public int BodyHeight =>
        Body is { Count: > 0 } ? BodyCells.Max(cell => cell.Y) - BodyCells.Min(cell => cell.Y) + 1 : 0;

    /// <summary>Template only — a row in <see cref="DressingPalette.Species"/>: the wood, the canopy profile
    /// and the proportions of one vanilla species.</summary>
    public string Species { get; init; } = "oak";

    /// <summary>Overall height in blocks: on a template it scales the species' proportions; on a copy it is
    /// the courses the body stands, read off the blocks rather than stated.</summary>
    public double Height { get; init; } = 12;

    /// <summary>How tall this tree is built, held to the range the editor offers.
    ///
    /// <para>The bound is load-bearing rather than tidiness. A tree's cost is superlinear in its reach — the
    /// sample patch a preview cuts is quadratic in it — so a height of 999 asks for a patch hundreds of blocks
    /// on a side. Holding the value here covers every caller instead of each guarding its own input, and means
    /// a stored recipe that is out of range still builds something.</para></summary>
    [JsonIgnore] public double Reach => Math.Clamp(Form == TreeForm.Copied ? BodyHeight : Height, 5, 40);

    /// <summary>The blocks this tree is made of: its species' wood. Read on a template, which is the form that
    /// builds its own blocks — a copied tree is written in the blocks it was cut with.</summary>
    [JsonIgnore] public TreeWood Timber => DressingPalette.SpeciesNamed(Species).Wood;
}

/// <summary>One boulder, as a recipe: a glacial erratic's form, its reach, what it is cut from and whether moss
/// takes its sky-lit faces.</summary>
public sealed record BoulderStyle : PropStyle
{
    public BoulderForm Form { get; init; } = BoulderForm.Round;

    /// <summary>How far the rock reaches from its centre, in blocks. A boulder is an erratic — a mass a glacier
    /// carried and left — so the default is a rock a player takes cover behind rather than one they step
    /// over.</summary>
    public double Size { get; init; } = 4;

    /// <summary>That reach held to the range the editor offers, for the reason <see cref="TreeStyle.Reach"/>
    /// holds a tree's: it sizes both the lobes built and the sample patch a preview cuts.</summary>
    [JsonIgnore] public double Reach => Math.Clamp(Size, 2, 10);

    /// <summary>What the rock is cut from — a full terrain material, resolved in the boulder's <em>own</em>
    /// frame rather than the map's, so a mottled rock carries the same mottling to every image of its orbit
    /// instead of sampling whatever the world pattern happens to say where each image landed.</summary>
    public TerrainMaterial Rock { get; init; } = new SolidMaterial(Palette.Blocks.Stone);

    /// <summary>Whether moss creeps onto the sky-lit faces — the rock's own micro-flora, laid over whatever
    /// <see cref="Rock"/> resolved.</summary>
    public bool Mossy { get; init; } = true;
}

/// <summary>A building's shell, as a registry entry. The shell itself is a <see cref="Houses.HouseStyle"/> —
/// the stamper's own type, which a room style composes to — so this is the wrapper that lets one registry hold
/// all three kinds under one discriminator rather than three registries differing only in what they hold.</summary>
public sealed record HouseStyleRef : PropStyle
{
    public Houses.HouseStyle Shell { get; init; } = new();
}
