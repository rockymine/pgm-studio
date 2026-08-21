namespace PgmStudio.Contracts;

/// <summary>
/// How a map is brought into being. Four routes create the <c>map</c> row every later call names by slug, and
/// these are what each takes: two originate a blank document to author into, two read a world somebody else
/// built. A fifth door, <c>POST /map/from-documents</c>, takes whole documents rather than a few fields and
/// carries its own request beside them.
///
/// <para>The slug is never stated. It is derived from the name and made unique against what is stored, so a
/// second map called the same thing is a second slug rather than a refusal, and the answer is what to use
/// from then on.</para>
/// </summary>
/// <remarks>Originate a blank authored plan — a map at the plan stage with an empty plan document, which the
/// plan editor fills. The whole body is optional, and an empty one originates <c>Untitled plan</c>.</remarks>
/// <param name="Name">What the map is called, and what its slug is derived from.</param>
public sealed record PlanOriginateRequest(string? Name = null);

/// <summary>Originate a blank sketch — a map at the sketch stage with an empty board, which the sketch tool
/// draws on. The whole body is optional, and an empty one originates <c>Untitled sketch</c> on the default
/// frame.</summary>
/// <param name="Name">What the map is called, and what its slug is derived from. Absent means
/// <c>Untitled sketch</c>, which is also the mark of a draft nobody has touched.</param>
/// <param name="Width">The board's east–west extent, in blocks. Absent means 120.</param>
/// <param name="Depth">The board's north–south extent, in blocks. Absent means 80.</param>
/// <param name="Mode">The symmetry the board is authored under — <c>mirror_x</c>, <c>mirror_z</c>,
/// <c>rot_180</c> or <c>rot_90</c>. Absent, or a word outside the four, means <c>rot_180</c>.</param>
/// <param name="CenterX">Where the board is centred, east–west. Absent means 0.</param>
/// <param name="CenterZ">Where the board is centred, north–south. Absent means 0.</param>
public sealed record SketchOriginateRequest(
    string? Name = null,
    double? Width = null,
    double? Depth = null,
    string? Mode = null,
    double? CenterX = null,
    double? CenterZ = null);

/// <summary>Originate a map from a world archive the studio fetches. The archive is read for its region
/// files, and a world that already carries a <c>map.xml</c> is a map rather than a candidate to originate one
/// from.</summary>
/// <param name="Url">Where to fetch the archive. Required, absolute, and <c>https</c> alone — anything else
/// is refused as <c>RQ1</c>. The host is checked against the import policy's allow-list, and one outside it
/// is refused 403 rather than fetched.</param>
public sealed record ImportUrlRequest(string Url);

/// <summary>Originate a map from a world already sitting under the studio's imports root.</summary>
/// <param name="Folder">The world's folder, named by <b>one path segment</b> under the imports root: a name
/// carrying a separator or <c>..</c> is refused as <c>RQ1</c> without being looked up, and one naming no
/// folder with a <c>region</c> directory in it as <c>RQ4</c>.</param>
public sealed record ImportFolderRequest(string Folder);
