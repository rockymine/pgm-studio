namespace PgmStudio.Minecraft.Render;

/// <summary>One way of reading a built world back: what it draws, what it is the read for, and where it is
/// known to mislead.</summary>
/// <param name="Route">The HTTP route, relative to a map.</param>
/// <param name="Flag">The <c>PgmStudio.RoundTrip</c> flag that takes the same reading off a region directory.</param>
/// <param name="Answers">What it draws and what question it is the answer to.</param>
/// <param name="Misleads">Where it is known to be read wrongly, or null where it has no such trap. Naming it
/// beside the read is the point: a caveat a reader meets after drawing a conclusion has already cost them
/// the conclusion.</param>
public sealed record WorldRead(string Route, string Flag, string Answers, string? Misleads = null);

/// <summary>
/// What each world read answers, written once.
///
/// <para>The sentences live here rather than at the routes because two surfaces serve them — the HTTP
/// endpoints publish them as their summaries, and <c>PgmStudio.RoundTrip --help</c> prints them beside the
/// flags — and a description of what a read shows is exactly the kind of text that is written twice and then
/// disagrees. It sits beside the renderers because that is what it describes.</para>
/// </summary>
public static class WorldReadCatalog
{
    public static readonly IReadOnlyList<WorldRead> All =
    [
        new("render/topdown", "--topdown --layer …",
            "The board from above, one question per image: `layer` draws the terrain alone (`ground`), what "
            + "the build recorded itself placing (`structure`), the planting (`foliage`), the goals and "
            + "spawns (`objectives`), or all of it (`combined`); `material` colours by the real palette "
            + "instead of by category. The read for what was built and where.",
            "It keeps no Y at all, so a riser, a ramp's step heights, a stamped room's floor and a goal's "
            + "clearance are none of them in it — `section` and `column` are the two reads that keep Y."),

        new("render/section", "--section",
            "A vertical cut with a Y scale: `axis` which way it runs, `from`/`to` its extent, `at` the other "
            + "coordinate. One of only two reads that keep Y, and the one that shows a layer stack, a roof "
            + "idiom and a riser's courses.",
            "It samples ONE plane, so anything a few blocks either side of the cut is not in the picture "
            + "(`B129`). A cut through a house that misses its walls reads as floor, air, roof — a correct "
            + "reading of that plane rather than a broken building."),

        new("render/heightmap", "--heightmap",
            "Elevation as tone, with contour lines every `contour` blocks. The read for whether a relief "
            + "solved into the shape it was drawn as, and the one that shows a flat pad butted against a hill "
            + "as the ruled edge it is."),

        new("render/surface", "--surface",
            "The paint, read as the tone families `TerrainPalette.Families` names — so a board can be checked "
            + "against the palette it was authored from, and a whole family taken where two members were "
            + "meant reads as the noise it is.",
            "Magenta is not a material: it is the honest answer for a block no family claims, and the legend "
            + "says how many there were."),

        new("render/traversability", "--traversability-map",
            "The navigable components, with the spawns and goals drawn on them. The read for whether a board "
            + "hangs together as one place a player can walk.",
            "An approach wall's cobweb course reads as impassable (`B99`), so every board carrying one "
            + "reports its wool room isolated — and the export gate, which navigates the columns rather than "
            + "this picture, passes it. A wall is meant to be crossed over the top."),

        new("render/structures", "--structures",
            "The building census by block material, for a world the studio did NOT build — a scanned map, or "
            + "a corpus one.",
            "It finds roofs by material and cannot see a town this studio built (`B149`): a cottage roofed in "
            + "stained clay reads as ground. On a studio-built world take `render/topdown?layer=structure`, "
            + "which draws what the build recorded itself placing."),

        new("render/mirror", "--mirror",
            "The board against its own symmetry: the columns that agree with their image, and the ones that "
            + "do not. The read for whether a board somebody believes is symmetric actually is."),

        new("column", "--column",
            "One or more columns bedrock-to-sky, every block named, as text. THE WORKHORSE: every picture "
            + "beside it is a projection, and this is what is actually at a coordinate — which is why it is "
            + "the read to reach for when a picture and a document disagree.",
            "A column through the middle of a house reads floor, air, roof. The walls are at the perimeter; "
            + "that is a correct building, not a broken one."),
    ];

    /// <summary>What one read answers, and where it misleads — the sentence a route publishes as its own
    /// summary, so the schema and the CLI serve the same text.</summary>
    public static string Sentence(string route)
    {
        var read = All.First(entry => entry.Route == route);
        return read.Misleads is null ? read.Answers : $"{read.Answers}  MISLEADS: {read.Misleads}";
    }

    /// <summary>The catalogue as the CLI prints it — one flag, what it answers, and its caveat under it.</summary>
    public static string Help()
    {
        var written = new System.Text.StringBuilder();
        written.AppendLine("Reading a built world back. Every one of these also answers over HTTP, at");
        written.AppendLine("GET /api/map/{slug}/<route>, where the schema publishes the same sentences.");
        written.AppendLine();
        foreach (var read in All)
        {
            written.AppendLine($"  {read.Flag}");
            written.AppendLine($"      route: {read.Route}");
            foreach (var line in Wrapped(read.Answers)) written.AppendLine($"      {line}");
            if (read.Misleads is { } trap)
            {
                written.AppendLine("      MISLEADS:");
                foreach (var line in Wrapped(trap)) written.AppendLine($"        {line}");
            }
            written.AppendLine();
        }
        return written.ToString();
    }

    /// <summary>A sentence broken to a terminal's width, so a caveat is read rather than scrolled past.</summary>
    private static IEnumerable<string> Wrapped(string text, int width = 84)
    {
        var line = new System.Text.StringBuilder();
        foreach (var word in text.Split(' '))
        {
            if (line.Length > 0 && line.Length + 1 + word.Length > width)
            {
                yield return line.ToString();
                line.Clear();
            }
            if (line.Length > 0) line.Append(' ');
            line.Append(word);
        }
        if (line.Length > 0) yield return line.ToString();
    }
}
