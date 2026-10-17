namespace PgmStudio.Domain;

/// <summary>
/// Which roof a house wears. They are the same building underneath — every one of them is a height field over
/// the same plan (<c>RoofField</c>) — which is why one stamper builds all six.
/// </summary>
public enum RoofForm
{
    /// <summary>Two slopes meeting at a ridge that runs the length of the building.</summary>
    Gable,

    /// <summary>A lid over the walls: the shell a wool structure and a spawn have always had, and the only form
    /// that can carry a hole.</summary>
    Flat,

    /// <summary>Four slopes, one off every wall, meeting at a ridge along the building's length — a pyramid
    /// where the footprint is square, because the ridge has no length left to run along.</summary>
    Hip,

    /// <summary>A barn roof: each slope steep for its first courses in from the eave, then shallow to the
    /// ridge, so the building carries a usable volume under a roof that still sheds.</summary>
    Gambrel,

    /// <summary>One plane, low at the front wall and climbing to the back — a lean-to. Read and built, and
    /// complained of wherever a style states it (<c>HS14</c>).</summary>
    Shed,

    /// <summary>A gable whose two slopes climb at different rates, so they meet off centre: short and steep
    /// over the front, long and shallow over the back.</summary>
    Saltbox,
}

/// <summary>Which way a wing's ridge runs, where its own proportions are not what should decide.</summary>
public enum RidgeAxis
{
    /// <summary>Along x, so the slopes are taken across z.</summary>
    AlongX,

    /// <summary>Along z, so the slopes are taken across x.</summary>
    AlongZ,
}

/// <summary>
/// Everything a wing states about itself apart from where it stands — how tall, what roof, and how it meets
/// its neighbour. <b>Stated once and read everywhere</b>: a <c>Wing</c> carries one, and so does the
/// authored entry a dressing document draws one from, so the two cannot drift into different vocabularies for
/// the same building.
///
/// <para><see cref="StoreysHigh"/> is how many of the house's storeys stand on this wing, and <b>nought takes
/// them all</b> — which is what a building whose wings are all of a height wants, and what one wing on its own
/// can only mean. A hall of one storey with a two-storey cross wing is the shape that needs the number. It is
/// deliberately not <c>Storeys</c>: <c>HouseStyle.Storeys</c> is the <em>list of storey styles</em> a building
/// is made of, and one name over a count and a list of styles reads as the same thing twice.</para>
///
/// <para><see cref="Form"/>, <see cref="Pitch"/> and <see cref="RoofSlab"/> are the roof this wing wears where
/// it does not simply wear the building's; naming a slab is what makes a roof climb in halves, so a
/// half-stepping wing is one that names one. Unset takes the style's.</para>
///
/// <para><b><see cref="Projects"/> is the one thing about a joint that the rectangles cannot say.</b> A wing
/// running into a hall either <em>marches</em> — each course stepping on into the hall's roof until that roof
/// already stands as tall, which draws the crossing as a valley and closes the building — or it
/// <em>projects</em>, carrying its roof clean across the hall to the far wall and showing a second gable there.
/// Both are buildings and neither is derivable: the same two rectangles make an L that closes and an L that
/// pushes through. Marching is what a wing does unless it says otherwise, because it is the shape that reads as
/// one house.</para></summary>
/// <param name="StoreysHigh">How many of the house's storeys stand on this wing; 0 takes them all.</param>
/// <param name="Form">The roof this wing wears, or absent for the building's.</param>
/// <param name="Pitch">How steeply its roof climbs, or 0 for the building's.</param>
/// <param name="RoofSlab">The slab its roof climbs in halves with, or absent for none.</param>
/// <param name="Ridge">Which way its ridge runs, or absent to follow its proportions.</param>
/// <param name="Projects">Whether it carries its roof across the wing it runs into, showing a second gable,
/// rather than marching into that roof.</param>
public readonly record struct WingSpec(
    int StoreysHigh = 0, RoofForm? Form = null, int Pitch = 0, int? RoofSlab = null,
    RidgeAxis? Ridge = null, bool Projects = false);

/// <summary>
/// One wing as an author drew it: the two opposite corners of its rectangle, and everything it states about
/// itself.
///
/// <para>The corners are <b>doubles in any order</b>, because that is what a drag produces — an author pulling
/// up-left and one pulling down-right placed the same building — and they are floored and normalized on the
/// way to a <c>Wing</c>. What the wing states is the very same <see cref="WingSpec"/> the model
/// carries, rather than a second copy of its fields under the same names, so a wing cannot mean one thing in
/// a document and another in the building raised from it.</para>
/// </summary>
/// <param name="Corners">Exactly two opposite <c>[x, z]</c> corners.</param>
/// <param name="Spec">How tall, what roof, which way the ridge runs, and whether it projects. Every field
/// optional: a wing that states nothing is a rectangle wearing the building's own everything, which is what
/// every wing meant before there was anything else to say.</param>
public sealed record AuthoredWing(IReadOnlyList<double[]> Corners, WingSpec Spec = default);
