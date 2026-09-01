using System.Text.Json.Serialization;
using PgmStudio.Geom.Algorithms;
using PgmStudio.Minecraft.Painting;

namespace PgmStudio.Minecraft.Dressing;

/// <summary>
/// Whether a prop changes how the map <em>plays</em> or only how it looks — the one distinction the whole
/// dressing stage is arbitrated by (docs/world-export/ideas.md G162).
///
/// <para>A boulder is cover, a tree breaks a sightline, tall grass hides a footstep: place one of those on a
/// map and not on its mirror and you have decided a fight. So <see cref="Gameplay"/> props are generated on
/// the authored unit and re-fanned across the symmetry orbit, exactly as the layout itself is. A flower bed
/// decides nothing, and mirroring it would make two halves of a map read as eerily identical, so
/// <see cref="Cosmetic"/> props scatter freely — reproducibly, since the field is a hash of the cell, but not
/// symmetrically.</para>
/// </summary>
public enum PropClass
{
    /// <summary>Decides nothing; free to scatter unmirrored.</summary>
    Cosmetic,
    /// <summary>Cover, collision or vision; must exist for every team or none.</summary>
    Gameplay,
}

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
/// the overlay that hides a player and so classes as gameplay.</param>
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
/// profile, per species. It is what a player reads as "an oak", and it is what most maps want.
/// <see cref="Grown"/> is the recursive skeleton — a wandering spline trunk with foliage gathered at its tips
/// — which makes shapes no vanilla generator does, but makes only <em>that</em> family of shapes: it has no
/// notched conifer and no flat umbrella in it. Six named presets of the grower would therefore offer six
/// silhouettes and build one, so the species live on the template and the grown tree takes a
/// <see cref="TreeWood"/> instead.</para>
/// </summary>
public enum TreeForm
{
    /// <summary>The vanilla tree of a named species.</summary>
    Template,
    /// <summary>The grown skeleton, in a chosen wood.</summary>
    Grown,
}

/// <summary>What a tree is made of: the log and leaf blocks, named once. It is separate from the species
/// because both trees need it and only one has a species — a grown tree is a shape the author designed, so
/// the only thing left to choose about its material is which wood it is cut from.</summary>
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
/// profile and its proportions, and <see cref="Height"/> scales the lot. A <see cref="TreeForm.Grown"/> tree is
/// the recursive skeleton: <see cref="Wood"/> names what it is cut from and the knobs below shape it. Each form
/// reads only its own fields, so the ones it does not read are inert rather than wrong.</para>
/// </summary>
public sealed record TreeStyle : PropStyle
{
    public TreeForm Form { get; init; } = TreeForm.Template;

    /// <summary>Template only — a row in <see cref="DressingPalette.Species"/>: the wood, the canopy profile
    /// and the proportions of one vanilla species.</summary>
    public string Species { get; init; } = "oak";

    /// <summary>Grown only — a row in <see cref="DressingPalette.Woods"/>. A grown tree's shape is the
    /// author's, so its wood is all that is left to name.</summary>
    public string Wood { get; init; } = "oak";

    /// <summary>Overall height in blocks. Template: it scales the species' proportions. Grown: not a uniform
    /// scale — a smaller tree also carries a thinner stem and fewer branches, so a sapling reads as a sapling
    /// rather than as a shrunken tree.</summary>
    public double Height { get; init; } = 12;

    /// <summary>Grown only — 1–3 stems at the base.</summary>
    public int Stems { get; init; } = 1;

    /// <summary>Grown only — how far the central axis climbs: low spreads, high spires.</summary>
    public double Leader { get; init; } = 0.55;

    /// <summary>Grown only — how much the trunk wanders on its way up.</summary>
    public double Flow { get; init; } = 0.45;

    /// <summary>Grown only — how far a branch leaves its parent, in radians. A hand-built corpus leaves the
    /// trunk at 59° off vertical and forks its children at 67°, so the default is a radian rather than the
    /// half one a tighter fan wants.</summary>
    public double BranchAngle { get; init; } = 1.1;

    /// <summary>Grown only — branching depth: 2 is a tree, 3 a denser one.</summary>
    public int Levels { get; init; } = 2;

    /// <summary>Grown only — whether the branches are gathered into whorls, a ring every few courses, each ring
    /// shorter than the one below. It is the conifer against the broadleaf, and it is the one shape choice a
    /// picker of six woods cannot make for an author.</summary>
    public bool Whorled { get; init; }

    /// <summary>Grown only — how big each tip's leaf cluster is.</summary>
    public double LeafSize { get; init; } = 0.6;

    /// <summary>How tall this tree is built, held to the range the editor offers.
    ///
    /// <para>The bounds on this and the knobs below are load-bearing rather than tidiness. A tree's cost is
    /// superlinear in its reach — the sample patch a preview cuts is quadratic in it, a grown crown is filled by
    /// testing every cell of its bounding box — while the knobs that set that reach are plain multipliers. A
    /// <see cref="Leader"/> of 55 rather than 0.55 therefore does not draw a strange tree, it asks for a volume
    /// hundreds of blocks on a side and never returns. Holding the values here covers every caller instead of
    /// each guarding its own input, and means a stored recipe that is out of range still builds something.</para></summary>
    [JsonIgnore] public double Reach => Math.Clamp(Height, 5, 40);

    /// <summary>This tree's growth parameters, as the grower wants them, each bounded like
    /// <see cref="Reach"/>. Read only when it is grown.</summary>
    [JsonIgnore] public TreeShape Shape => new(
        Height: Reach, Stems: Math.Clamp(Stems, 1, 3), Levels: Math.Clamp(Levels, 2, 3),
        BranchAngle: Math.Clamp(BranchAngle, 0.2, 1.5), Flow: Math.Clamp(Flow, 0, 1),
        Leader: Math.Clamp(Leader, 0, 1), Whorled: Whorled);

    /// <summary>How big each tip's leaf cluster is, bounded like <see cref="Reach"/>: it scales the crown, and
    /// the crown is filled cell by cell.</summary>
    [JsonIgnore] public double LeafCluster => Math.Clamp(LeafSize, 0.2, 1);

    /// <summary>The blocks this tree is made of, whichever form it is: a template takes its species' wood, a
    /// grown tree the one it was given.</summary>
    [JsonIgnore] public TreeWood Timber => Form == TreeForm.Template
        ? DressingPalette.SpeciesNamed(Species).Wood
        : DressingPalette.WoodNamed(Wood);
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
