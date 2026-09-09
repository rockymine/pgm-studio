namespace PgmStudio.Minecraft.Palette;

/// <summary>What one block shows on one face: the sprite it wears there, and what that sprite reads as.
///
/// <para><see cref="Texture"/> is the 1.8 sprite's own name, and it is the field that answers a question no
/// colour can: two blocks naming the same sprite are the same block on that face. Sandstone, smooth
/// sandstone and the double sandstone slab all wear <c>sandstone_top</c>, so a surface pattern built from
/// two of them has one block in it whatever the document says — while a wall pattern from the same two has
/// two, because their sides differ.</para>
///
/// <para><see cref="Contrast"/> is the standard deviation of luma over the sprite's opaque pixels: the axis
/// a mean colour cannot carry. Stone and cobblestone are four counts apart per channel and differ by a
/// factor of 2.5 here. <see cref="Colours"/> counts distinct RGB values in the sprite's 256 — five means a
/// hand-picked ramp, two hundred a generated field.</para></summary>
/// <param name="Texture">The 1.8 sprite this face wears.</param>
/// <param name="Contrast">Spread of luma over the opaque pixels.</param>
/// <param name="Colours">Distinct RGB values in the sprite.</param>
/// <param name="Construction">How the sprite is drawn — see <see cref="BlockLook.Flags"/>.</param>
public readonly record struct FaceLook(string Texture, double Contrast, int Colours,
                                       IReadOnlyList<string> Construction);

/// <summary>
/// A block's appearance beyond its mean colour, on each of the two faces a terrain theme paints.
///
/// <para>The palette answers one colour per block, which is the right answer for a pixel and a poor one for
/// a choice: it puts andesite and its polished variant four counts apart, and stone and cobblestone the
/// same, while the pairs read nothing alike. This says what a colour cannot — how much the sprite varies,
/// how many values it is drawn from, and how it is constructed.</para>
///
/// <para><b>Two faces, because a theme paints two.</b> The surface and rim buckets write what a player sees
/// from above and the wall and fill buckets what they see from the side, so a block whose faces differ
/// answers differently to each. Most blocks wear one sprite everywhere and answer alike, which is itself
/// the useful reading.</para>
///
/// <para>Measured off the 1.8 textures and stored as a table (<see cref="BlockLookData"/>): the sprites are
/// Mojang's and are not in this repository, so nothing here can re-derive these numbers.</para>
///
/// <para><b>Read on the sprite the file holds, which is what the world draws with one exception.</b> A
/// biome-tinted block — grass, and the leaves a terrain paint does not offer — is greyscale in the file and
/// multiplied by its biome's colour at render, so <c>grass_top</c> reads here as the grey it is stored as
/// while the swatch beside it carries the tint. The <em>structure</em> is what these numbers are for and a
/// tint is a per-channel multiply, which moves the spread without changing what the sprite is made of. A
/// grass block's <b>side</b> needs no such caveat: <c>grass_side</c> ships with its green fringe drawn in,
/// and the overlay beside it is only the mask the game re-tints over the same pixels.</para>
/// </summary>
public static class BlockLook
{
    /// <summary>The words <see cref="FaceLook.Construction"/> is drawn from, each with what it means. They
    /// are not exclusive — stone brick is <c>masonry</c> and <c>bevelled</c>, and saying both is more use
    /// than choosing one.</summary>
    public static readonly IReadOnlyDictionary<string, string> Flags = new Dictionary<string, string>
    {
        ["inlaid"] = "another sprite with a material painted into it — every stone ore is stone with its own",
        ["masonry"] = "straight mortar lines: rows or columns running flat and stepping away from their neighbours",
        ["tiled"] = "the sprite repeats at a period of 2, 4 or 8",
        ["panelled"] = "few distinct rows or columns — regular structure with no clean period",
        ["bevelled"] = "a chamfered edge: one border lit, another shaded",
        ["dithered"] = "150 or more distinct colours in 256 pixels — a generated field",
        ["ramped"] = "twelve colours or fewer — drawn from a small hand-picked set",
        ["grained-x"] = "runs visibly across",
        ["grained-y"] = "runs visibly down",
        ["flat"] = "contrast under 8",
    };

    /// <summary>How a block reads on the face a surface or rim bucket paints, or null for a block the table
    /// does not carry — it holds what <see cref="TerrainPalette"/> offers and a material may name any id.</summary>
    public static FaceLook? Top(int id, int data) => Of(id, data)?.Top;

    /// <summary>How it reads on the face a wall or fill bucket paints.</summary>
    public static FaceLook? Side(int id, int data) => Of(id, data)?.Side;

    /// <summary>Whether two blocks wear the <b>same sprite</b> on the face a bucket paints, and so cannot be
    /// told apart in a pattern that uses it. Null where either is not carried.</summary>
    public static bool? SameOn(TerrainBucketFace face, int idA, int dataA, int idB, int dataB)
    {
        var a = face == TerrainBucketFace.Top ? Top(idA, dataA) : Side(idA, dataA);
        var b = face == TerrainBucketFace.Top ? Top(idB, dataB) : Side(idB, dataB);
        return a is null || b is null ? null : string.Equals(a.Value.Texture, b.Value.Texture, StringComparison.Ordinal);
    }

    private static (FaceLook Top, FaceLook Side)? Of(int id, int data)
    {
        if (BlockLookData.ByBlock.TryGetValue((id << 4) | (data & 0xF), out var exact))
            return (Face(exact.Top), Face(exact.Side));
        return null;
    }

    private static FaceLook Face(BlockLookData.Face f) => new(f.Texture, f.Contrast, f.Colours, f.Construction);
}

/// <summary>Which face of a block a theme's bucket paints. The surface and the rim are the courses a player
/// walks on and sees from above; the wall is the vertical face of cut terrain and the fill is what shows
/// behind it, so both are seen from the side.</summary>
public enum TerrainBucketFace
{
    /// <summary>What the surface and rim buckets write.</summary>
    Top,

    /// <summary>What the wall and fill buckets write.</summary>
    Side,
}
