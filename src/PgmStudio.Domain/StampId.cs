using System.Text.Json.Serialization;

namespace PgmStudio.Domain;

/// <summary>
/// Which stamped thing a column belongs to: what kind it is, which authored unit it came from, and which
/// image of that unit this one is.
///
/// <para><b>The three are one answer, and splitting them is what made a board unreadable.</b> The record used
/// to be a hand-built string per stamp site, and the sites did not agree on what the number in it meant:
/// <c>house:h1:0</c> put the <em>orbit image</em> last while <c>spawn:0</c>, <c>wool:0</c> and
/// <c>destroyable:0</c> put a running index into the <em>already-fanned</em> list there. So two entries of the
/// same form meant different things, both images of one thing were separate entries with nothing saying which
/// thing they were two of, and a reader wanting to pair a stamp with its own mirror had to recover the
/// pairing geometrically and could get it wrong — which is exactly what happened.</para>
///
/// <para><b>It is minted where the fan happens, not where the blocks land.</b> Whoever produced the orbit
/// images knows which authored unit they came from and which image each one is; a stamper receiving an entry
/// out of an already-fanned list does not, and can only count. So the id travels on the thing to be stamped
/// and the stamper passes it through, the same direction <c>PlacementClaim</c> takes a claim
/// from the placement rather than rebuilding it beside one.</para>
/// </summary>
/// <param name="Kind">The family: <c>spawn</c>, <c>wool</c>, <c>house</c>, <c>destroyable</c>, <c>core</c>,
/// <c>wall</c>, <c>roomfloor</c>, <c>redstoneline</c>, <c>ironcube</c>, <c>tree</c>, <c>boulder</c>,
/// <c>path</c>, <c>water</c>.</param>
/// <param name="Unit">Which authored thing this is an image of — the placement's own id where it has one, and
/// its index in the authored list where it does not. Two images of one unit share it; two different units of
/// one kind do not.</param>
/// <param name="Image">Which orbit image this is, <c>0</c> for the authored unit itself. A map with no
/// symmetry has only image 0.</param>
public readonly record struct StampId(string Kind, string Unit, int Image)
{
    /// <summary>What this is, with which image of it dropped — the key a reader groups on to find every image
    /// of one thing, and the one a render colours by so a mirrored pair shares a hue.</summary>
    /// <remarks>Not serialized, for the reason <see cref="Geom.BlockBox.CuboidMax"/> is not: a tuple crosses
    /// as <c>{item1, item2}</c> and this is the two fields beside it.</remarks>
    [JsonIgnore]
    public (string Kind, string Unit) Identity => (Kind, Unit);

    /// <summary>The same unit seen as its <paramref name="image"/>-th image, for a caller naming the partner
    /// it expects to find rather than searching for one.</summary>
    public StampId At(int image) => this with { Image = image };

    /// <summary>A short human reading for a report, a log line or a legend — never parsed back. The fields
    /// are the record; this is only how it is shown.</summary>
    public override string ToString() => $"{Kind}:{Unit}#{Image}";
}
