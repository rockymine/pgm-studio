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
    double TallShare = 0.0)
{
    /// <summary>The seed every one of this spec's fields is keyed on.</summary>
    public uint Seed { get; init; } = 7;
}

/// <summary>The shape family a boulder takes. Each is a list of lobes, not a code path — see
/// <see cref="BoulderShapes"/>.</summary>
public enum BoulderForm
{
    /// <summary>One round lobe, half buried.</summary>
    Round,
    /// <summary>One heavily eroded lobe — angular and weathered.</summary>
    Angular,
    /// <summary>One wide flat lobe: a low outcrop rather than a rock.</summary>
    Outcrop,
    /// <summary>Three shrinking lobes stacked up — a cairn.</summary>
    Cairn,
}

/// <summary>Scattered rock. <paramref name="Density"/> drives the scatter spacing and
/// <paramref name="SizeSpread"/> how much the sizes vary; the rest is the finish.</summary>
/// <param name="Mossy">Whether moss creeps onto the sky-lit faces — the rock's own micro-flora.</param>
public sealed record BoulderSpec(
    double Density = 0.35,
    double SizeSpread = 0.5,
    BoulderForm Form = BoulderForm.Round,
    int BlockId = Blocks.Stone,
    int BlockData = 0,
    bool Mossy = true)
{
    public uint Seed { get; init; } = 17;

    /// <summary>The widest a boulder of this spec grows.</summary>
    public double MaxRadius => 1.4 + Math.Clamp(SizeSpread, 0, 1) * 3.2;

    /// <summary>How far apart the scatter puts them. Density alone is not enough: sites spaced closer than a
    /// prop is wide give one merged mass of rock instead of scattered boulders, so the size sets a floor.</summary>
    public int Spacing => Math.Max((int)Math.Ceiling(MaxRadius * 2), DressingSpacing.FromDensity(Density));
}

/// <summary>A tree species as data: which log and leaf blocks it places, and the growth knobs that give it its
/// silhouette. A species is a row, not a class — the same grower builds all of them.</summary>
/// <param name="Leader">How far the central axis climbs — low spreads like an oak, high spires like a birch.</param>
public sealed record TreeSpecies(
    string Name,
    int LogId, int LogData,
    int LeafId, int LeafData,
    double Height = 18,
    double Leader = 0.5,
    double BranchAngle = 0.75,
    int Levels = 2,
    double LeafSize = 0.5);

/// <summary>Scattered trees. Trees clump into groves with clearings between, which is a second, coarser noise
/// field gating the scatter — an evenly spaced forest reads as an orchard.</summary>
/// <param name="GroveScale">The grove field's feature size in blocks.</param>
/// <param name="GroveThreshold">0–1; how much of the map the groves claim.</param>
/// <param name="SizeSpread">0–1; how much tree heights vary within a stand.</param>
public sealed record TreeSpec(
    double Density = 0.3,
    int GroveScale = 28,
    double GroveThreshold = 0.45,
    double SizeSpread = 0.4,
    IReadOnlyList<string>? Species = null)
{
    public uint Seed { get; init; } = 23;

    /// <summary>The species this stand draws from, by name — resolved against <see cref="TreeSpecies"/> rows.
    /// Empty means every species the palette knows.</summary>
    public IReadOnlyList<string> SpeciesNames => Species ?? [];

    /// <summary>How far apart the scatter puts them. Canopies may touch — that is what a forest looks like —
    /// but trunks may not, so the floor is a trunk's width rather than a crown's.</summary>
    public int Spacing => Math.Max(4, DressingSpacing.FromDensity(Density));

    // A record's generated equality compares the species list by reference, which would make two specs that say
    // the same thing unequal — and the pass groups cells by spec, so that would scatter one stand as several.
    public bool Equals(TreeSpec? other)
        => other is not null
        && (Density, GroveScale, GroveThreshold, SizeSpread, Seed) == (other.Density, other.GroveScale, other.GroveThreshold, other.SizeSpread, other.Seed)
        && SpeciesNames.SequenceEqual(other.SpeciesNames);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Density); hash.Add(GroveScale); hash.Add(GroveThreshold); hash.Add(SizeSpread); hash.Add(Seed);
        foreach (var name in SpeciesNames) hash.Add(name);
        return hash.ToHashCode();
    }
}

/// <summary>How a density knob becomes a scatter radius. Shared so "dense" means the same thing to every prop
/// before its own size widens it.</summary>
public static class DressingSpacing
{
    /// <summary>Dense is tight spacing. Two blocks is the floor, or one-cell props touch.</summary>
    public static int FromDensity(double density)
        => Math.Max(2, (int)Math.Round(2 + (1 - Math.Clamp(density, 0, 1)) * 6));
}

/// <summary>
/// A dressing: what a stretch of ground grows, in the same sense a <c>TerrainTheme</c> is what it is made of.
/// Each part is optional — a dressing with only flora is a meadow, one with only boulders is a scree slope —
/// and each is a noise-driven recipe rather than a placement list, so it can be applied to any footprint of
/// any shape without being re-authored.
/// </summary>
public sealed record DressingRecipe
{
    public FloraSpec? Flora { get; init; }
    public BoulderSpec? Boulders { get; init; }
    public TreeSpec? Trees { get; init; }

    /// <summary>Nothing grows — the default for undressed ground, so a map that never opens the phase exports
    /// exactly as it did before.</summary>
    public static DressingRecipe Bare { get; } = new();

    /// <summary>A plain grassland: flowers and grass, no props. Everything in it is cosmetic, so it is also the
    /// one preset that is fair on any map without the orbit fan doing anything.</summary>
    public static DressingRecipe Meadow { get; } = new() { Flora = new FloraSpec(Coverage: 0.55, FlowerShare: 0.3) };

    /// <summary>Whether the recipe places anything at all — the cheap check before a pass walks a footprint.</summary>
    [JsonIgnore]
    public bool IsBare => Flora is null && Boulders is null && Trees is null;
}

/// <summary>The lobe lists behind <see cref="BoulderForm"/> — style-as-data, the same seam the structure
/// presets use. A form is a shape, so it is a table rather than a branch.</summary>
public static class BoulderShapes
{
    /// <summary>The lobes of a boulder of the given form at <paramref name="size"/> blocks across, centred on
    /// the origin with <b>half of it below y = 0</b> — a rock that sits entirely on the ground reads as
    /// dropped, and one that emerges from it reads as always having been there.</summary>
    public static IReadOnlyList<Geom.Algorithms.BlobLobe> Of(BoulderForm form, double size) => form switch
    {
        BoulderForm.Angular => [Lobe(0, 0, 0, size, size * 0.85, size, 0.5)],
        BoulderForm.Outcrop => [Lobe(0, 0, 0, size * 1.45, size * 0.45, size * 1.2, 0.35)],
        BoulderForm.Cairn =>
        [
            Lobe(0, 0, 0, size * 0.9, size * 0.6, size * 0.9, 0.2),
            Lobe(-size * 0.15, size * 0.7, size * 0.1, size * 0.6, size * 0.45, size * 0.6, 0.2),
            Lobe(-size * 0.3, size * 1.3, size * 0.2, size * 0.36, size * 0.3, size * 0.36, 0.15),
        ],
        _ => [Lobe(0, 0, 0, size, size * 0.85, size, 0)],
    };

    private static Geom.Algorithms.BlobLobe Lobe(double x, double y, double z, double rx, double ry, double rz, double erosion)
        => new(new Geom.Vec3(x, y, z), new Geom.Vec3(rx, ry, rz), erosion);
}
