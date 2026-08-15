namespace PgmStudio.Minecraft;

/// <summary>One band of a stack: the material its blocks resolve through, and how far along the stack's axis
/// it claims before the next takes over.</summary>
public readonly record struct Band(TerrainMaterial Material, int Thickness = 1);

/// <summary>
/// What a stack answers where the distance runs past its last band. <b>Not implied by the axis</b>, which is
/// why it is stated: the two are independent choices and the repo makes both.
/// </summary>
[System.Text.Json.Serialization.JsonConverter(typeof(BandEndingConverter))]
public enum BandEnding
{
    /// <summary>The last band claims everything beyond it. Right where the stack <b>owns the whole space</b>:
    /// a wall grows in whatever its top course is, and a layer stack deeper than it was declared never falls
    /// through to nothing.</summary>
    Repeat,

    /// <summary>Nothing is claimed past the last band, and whatever is under the stack shows. Right where the
    /// stack is a band <b>inside</b> a larger space: a rim two blocks in and then the surface, rather than two
    /// blocks of rim and then rim forever.</summary>
    HandOver,
}

/// <summary>Written as <c>"repeat"</c>/<c>"handOver"</c> rather than as 0/1, the way a theme's other enums
/// are. It had never been read by a person before, because nothing authored a stack that ends — an inward
/// stack is the first thing that does, and a stored theme saying <c>"ending":1</c> is a theme nobody can
/// check by eye.</summary>
public sealed class BandEndingConverter()
    : System.Text.Json.Serialization.JsonStringEnumConverter<BandEnding>(System.Text.Json.JsonNamingPolicy.CamelCase);

/// <summary>
/// Bands along a distance: an ordered list of materials, each claiming a stated thickness, read by how far
/// along the axis a block sits.
///
/// <para><b>The axis is the caller's and the ending is the stack's.</b> A layer stack is read as depth from the
/// top of its bucket, a room part as courses from the part's own base; a stack states neither, because the
/// distance is measured by whoever has it. What the stack does stand for is where its bands run out —
/// <see cref="BandEnding"/> — and that had been assumed rather than stated, which is how one rule came to be
/// written out three times with two different answers to the same question.</para>
///
/// <para>A band that repeats does <b>not</b> reseed a pattern. A pattern's seed lives on the material and its
/// field is sampled from world coordinates, so two bands of one material draw one field read twice rather than
/// two fields.</para>
/// </summary>
public sealed record BandStack(IReadOnlyList<Band> Bands, BandEnding Ending = BandEnding.Repeat)
{
    /// <summary>A stack of one material, claiming everything.</summary>
    public static BandStack Of(TerrainMaterial material) => new([new Band(material)]);

    /// <summary>The band <paramref name="step"/> along the axis, and how far into that band the step is — the
    /// distance a nested stack reads, so a band may itself be a stack.
    ///
    /// <para>Null where nothing claims the step: past the last band under <see cref="BandEnding.HandOver"/>,
    /// and on a stack with no bands at all. What shows there is the caller's to answer, since what is under a
    /// stack is not something the stack can see.</para></summary>
    public (TerrainMaterial? Material, int Into) At(int step)
    {
        var remaining = Math.Max(0, step);
        foreach (var (material, thickness) in Bands)
        {
            if (remaining < thickness) return (material, remaining);
            remaining -= thickness;
        }
        return Bands.Count > 0 && Ending == BandEnding.Repeat
            ? (Bands[^1].Material, remaining)
            : (null, remaining);
    }

    /// <summary>Band for band, not list for list: the generated equality compares the collection by
    /// <em>reference</em>, so two stacks of the same bands — one built in code, one read back from JSON —
    /// would come out different, and that is the comparison a round trip is made of.</summary>
    public bool Equals(BandStack? other)
        => other is not null && Ending == other.Ending && Bands.SequenceEqual(other.Bands);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Ending);
        foreach (var band in Bands) hash.Add(band);
        return hash.ToHashCode();
    }
}
