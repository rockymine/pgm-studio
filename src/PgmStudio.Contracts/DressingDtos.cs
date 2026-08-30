namespace PgmStudio.Contracts;

/// <summary>What a prop's preview patch placed, so an author can tell a knob that is doing nothing from one
/// whose effect is simply too small to read at card size.</summary>
/// <param name="Plants">Grass, ferns and flowers seated on the patch — what a flora spec's coverage
/// actually produced, against what it asked for.</param>
/// <param name="Boulders">Rocks seated on the patch, each counted once however many blocks it stands in.</param>
/// <param name="Trees">Trees grown on the patch, counted by trunk rather than by leaf.</param>
/// <param name="PathCells">Cells a path repainted. A band that reads as a ruled stripe and one that reads as
/// a track differ by this number more than by anything in the document.</param>
/// <param name="WaterCells">Cells a water prop filled.</param>
public sealed record DressingCountsDto(int Plants, int Boulders, int Trees, int PathCells, int WaterCells);

/// <summary>Both views of one placed prop (POST /api/terrain/prop-preview), produced by running the real pass
/// over a sample patch.</summary>
/// <param name="Plan">The patch from above, as SVG — where a path's paving and an area's density read.
/// <c>?format=png&amp;view=plan</c> answers the same view as an image.</param>
/// <param name="Section">The same patch cut open, as SVG — the only view a tree's silhouette or a boulder's
/// profile against the ground is visible in. <c>?format=png&amp;view=section</c> answers it as an image.</param>
/// <param name="Counts">What the pass placed, so a knob doing nothing is told apart from one whose effect is
/// too small to see at card size.</param>
public sealed record DressingPreviewDto(string Plan, string Section, DressingCountsDto Counts);

/// <summary>A prop previewed against a theme (POST /api/terrain/prop-preview): the prop's own JSON, and the
/// terrain finish it stands on — what the paint leaves on top is exactly what decides whether flora grows at
/// all and what a path may repaint, so a preview that ignored the theme would promise a meadow on a plaza.
/// <para>The prop arrives at whatever position it was placed at; the preview re-centres it on the sample, so a
/// card shows the prop rather than the empty corner of the map it happens to sit in.</para></summary>
/// <param name="PropJson">The prop's own JSON — one entry of a dressing document, at whatever position it was
/// placed at.</param>
/// <param name="ThemeJson">The painter's theme JSON the prop stands on. Absent previews it on the built-in
/// finish, which is a different question from how it will look on the map.</param>
public sealed record PropPreviewRequest(string PropJson, string? ThemeJson);

/// <summary>One option a prop's picker offers, drawn rather than described (GET /api/terrain/path-styles,
/// /boulder-forms, /species). <paramref name="Key"/> is the value the prop stores, <paramref name="Label"/>
/// what an author reads, and <paramref name="Svg"/> the same algorithm the export runs, at card size — so a
/// picker can never offer a look the pass does not produce.
/// <para><paramref name="Defaults"/> is the rest of the prop the option implies, as a JSON patch: picking
/// "spruce" has to take a spruce's proportions too, or the tree is named one thing and shaped another. It
/// travels with the card rather than being tabulated in the client, so the species table stays stated once —
/// on the server, where the grower reads it.</para></summary>
/// <param name="Key">What a prop names the option by.</param>
/// <param name="Label">The option as an author reads it.</param>
/// <param name="Svg">The card picture, drawn by the algorithm the export runs.</param>
/// <param name="Defaults">The rest of the prop the option implies, as a JSON patch: picking "spruce" has to
/// take a spruce's proportions too, or the tree is named one thing and shaped another.</param>
public sealed record PropOptionDto(string Key, string Label, string Svg, string? Defaults = null);
