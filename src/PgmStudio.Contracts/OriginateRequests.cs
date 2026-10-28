using PgmStudio.Vocabulary;

namespace PgmStudio.Contracts;

/// <summary>
/// How a map is brought into being. Three routes create the <c>map</c> row every later call names by slug, and
/// these are what each takes: one originates a blank sketch to author into, two read a world somebody else
/// built. A fourth door, <c>PUT /map/{slug}/source</c>, takes a map's whole source rather than a few fields and
/// carries its own request beside them.
///
/// <para>Each answers the slug it took, which is what to use from then on. A sketch takes a fresh random id
/// unless the request states a slug; a world read from a folder or a link takes that folder's or link's name.
/// A stated or derived slug is made unique against what is stored, so a second map of the same name is a
/// second slug rather than a refusal. The fourth states it in its route, because storing a source again is
/// storing the same map.</para>
/// </summary>
/// <remarks>Originate a blank sketch — a map at the sketch stage with an empty board, which the sketch tool
/// draws on. The whole body is optional, and an empty one originates <c>Untitled sketch</c> on the default
/// frame.</remarks>
/// <param name="Name">What the map is called. Absent means <c>Untitled sketch</c>, which is also the mark of a
/// draft nobody has touched.</param>
/// <param name="Slug">The slug to take, made unique against what is stored. Absent means a fresh random id of
/// 16 lowercase letters and digits.</param>
/// <param name="Width">The board's east–west extent, in blocks. Absent means 120.</param>
/// <param name="Depth">The board's north–south extent, in blocks. Absent means 80.</param>
/// <param name="Mode">The symmetry the board is authored under, <c>none</c> for none. Absent, or a word
/// outside the set, means <c>rot_180</c>.</param>
/// <param name="CenterX">Where the board is centred, east–west. Absent means 0.</param>
/// <param name="CenterZ">Where the board is centred, north–south. Absent means 0.</param>
public sealed record SketchOriginateRequest(
    string? Name = null,
    string? Slug = null,
    double? Width = null,
    double? Depth = null,
    [property: WordSet(typeof(SymmetryModes))] string? Mode = null,
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
