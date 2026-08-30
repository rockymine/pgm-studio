using System.Text.Json.Serialization;

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

/// <summary>A vanilla tree species as data: its wood and its proportions. A species is a row, not a class —
/// the profile is a radius table (<see cref="Geom.Algorithms.CanopyProfiles"/>), so adding one adds no code.</summary>
/// <param name="Height">The species' natural height in blocks: trunk plus canopy. A prop scales from it.</param>
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
