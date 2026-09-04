namespace PgmStudio.Minecraft.Render;

/// <summary>One way of reading a built world back: what it draws, what it is the read for, and where it is
/// known to mislead.</summary>
/// <param name="Route">The HTTP route, relative to a map.</param>
/// <param name="Flag">The <c>PgmStudio.RoundTrip</c> flag that takes the same reading off a region directory,
/// or null for a read that answers only over HTTP because it needs the map's own documents rather than a
/// world on disk.</param>
/// <param name="Answers">What it draws and what question it is the answer to.</param>
/// <param name="Misleads">Where it is known to be read wrongly, or null where it has no such trap. Naming it
/// beside the read is the point: a caveat a reader meets after drawing a conclusion has already cost them
/// the conclusion.</param>
public sealed record WorldRead(string Route, string? Flag, string Answers, string? Misleads = null);

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
        new("render/topdown", "--topdown --subject …",
            "The board from above, one question per image: `subject` draws the terrain alone (`ground`), what "
            + "the build recorded itself placing (`structure`), the planting (`foliage`), the goals and "
            + "spawns (`objectives`), or all of it (`combined`); `layer` draws one storey of a stacked board; "
            + "`material` colours by the real palette instead of by category. The read for what was built and "
            + "where.",
            "It keeps no Y at all, so a riser, a ramp's step heights, a stamped room's floor and a goal's "
            + "clearance are none of them in it — `section` and `column` are the two reads that keep Y."),

        new("render/section", "--section",
            "A vertical cut with a Y scale: `axis` which way it runs, `from`/`to` its extent, `at` the other "
            + "coordinate, `depth` how far behind it to project. One of only two reads that keep Y, and the "
            + "one that shows a layer stack, a roof idiom and a riser's courses. `?format=text` answers the "
            + "same cut as characters — one per block, ground and storey and every claim named — for a "
            + "caller that subtracts a height rather than estimating one from a shade.",
            "Without `depth` it samples ONE plane, so a cut through a house that misses its walls reads as "
            + "floor, air, roof — a correct reading of that plane rather than a broken building. With it, "
            + "each column takes the nearest block up to that many behind the cut, in its own material "
            + "dimmed by how far back it stands."),

        new("render/heightmap", "--heightmap",
            "Elevation as tone, with contour lines every `contour` blocks. The read for whether a relief "
            + "solved into the shape it was drawn as, and the one that shows a flat pad butted against a hill "
            + "as the ruled edge it is. `?format=text` answers the same reading as a height-banded grid, "
            + "`every` blocks a character, with the spawns, goals, houses and water overprinted."),

        new("slopes", null,
            "The worst step to a neighbour per sampled cell, classed `.` walked, `:` scrambled with a block, "
            + "`#` a barrier — the same tiers a walk is priced in. A cliff reads as a line, a ramp as a band "
            + "crossing it, and an overdone relief as a page. `faces` names the barrier runs worth checking, "
            + "largest first, each with the box to find it in.",
            "The grid samples one block per cell, so a one-block lip inside a cell of four can hide; "
            + "`every=1` reads every block."),

        new("render/surface", "--surface",
            "The paint, read as the tone families `TerrainPalette.Families` names — so a board can be checked "
            + "against the palette it was authored from, and a whole family taken where two members were "
            + "meant reads as the noise it is.",
            "Magenta is not a material: it is the honest answer for a block no family claims, and the legend "
            + "says how many there were."),

        new("render/traversability", "--traversability-map",
            "The navigable components, with the spawns and goals drawn on them. The read for whether a board "
            + "hangs together as one place a player can walk.",
            "Headroom is what a player's body passes through, not air: a flower, a torch, a carpet and an "
            + "approach wall's cobweb course leave a column navigable, while a fence, a wall and a chest stop "
            + "it. A door and a ladder read as blocking, since a block id does not say whether one is open."),

        new("render/structures", "--structures",
            "The building census by block material, for a world the studio did NOT build — a scanned map, or "
            + "a corpus one.",
            "It finds roofs by material and cannot see a town this studio built (`B149`): a cottage roofed in "
            + "stained clay reads as ground. On a studio-built world take `render/topdown?subject=structure`, "
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

        new("render/walk", null,
            "What crossing this board charges, drawn: every passable cell shaded by what reaching it from "
            + "`from` costs, with the route to `to` over the top. `field` picks which of the walk's answers "
            + "is shaded — `blocks` a player must place, `distance` walked, or `drops` taken — and `aim` "
            + "picks the route: the shortest way, the cheapest one, or the one keeping off an edge. The read "
            + "for why a route goes where it goes, which traversability cannot say because it answers only "
            + "whether one exists.",
            "It is one field from one start, so a cell shaded cheap is cheap FROM THERE. Two teams do not "
            + "share a picture, and a board that is fair reads differently from each spawn."),

        new("walk", null,
            "The same journey as numbers rather than as a picture: whether the place can be reached, how far "
            + "it is in blocks, how many blocks a player must place to get there — the climb at a rise of "
            + "delta costing delta minus one, and one a cell for void bridged — and how many falls over the "
            + "free height it takes. Four answers in four units, none weighed against the others, so a "
            + "caller reads the field its own rule is stated in. `aim` picks which route is measured: the "
            + "shortest (`travel`), the one placing fewest blocks (`reach`), or the least edge-hugging of "
            + "the routes within ten blocks of the shortest (`comfort`). It also says which storey it stood "
            + "on at every place, which steps it scrambled, dropped or climbed as a barrier, and — with "
            + "`?beside=N` — every distinct thing recorded within N cells of the route.",
            "`from` and `to` are snapped to the nearest ground within 24 blocks, because a marker's stated "
            + "coordinates are a block in a room rather than a cell of terrain. A journey between two "
            + "markers deep inside walls is measured between the cells outside them."),

        new("transect", null,
            "A polyline walked block by block, answered as numbers: `points` is the line as `x,z;x,z[;x,z…]`, "
            + "`every` thins the stations, `beside` lists every distinct claim within that many cells of the "
            + "line. Each station carries the ground, the storey stood on, the water and the highest block in "
            + "its column, and the step from the station before it, classed walked / scrambled / a barrier / "
            + "a drop. The read for a claim about a SHAPE — a bank, a wall, a stair, a basin — which a single "
            + "column cannot answer because a shape is a profile and never a point. `?format=text` answers "
            + "the same walk as a table, one row a station.",
            "A transect answers exactly the line asked for and nothing either side of it — a barrier two "
            + "cells off the stated points is invisible to it, which is what `beside` is for."),

        new("stroke", null,
            "A drawn stroke walked end to end, down its own centreline rather than between its points: per "
            + "block the ground, whether the paving reaches it, what it is made of and the step from the "
            + "block before, then the worst step, the material runs and every stretch the paving does not "
            + "reach. The read for a road a player has to cross, which `walk` cannot answer because that "
            + "walks the way a player would choose.",
            "It walks one image of the symmetry orbit. The mirrored road stands on mirrored ground, so its "
            + "steps are its own — read each image that matters."),

        new("themes/census", null,
            "Every ground cell counted by the theme that paints it: cells per theme and their share, the "
            + "distinct surface materials each carries, which theme borders which and over how many cells, "
            + "and the board's whole palette count. The number for a board that mashes its themes, which "
            + "`render/surface`'s tone-family legend has no count for."),
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
            written.AppendLine($"  {read.Flag ?? "(HTTP only — it reads the map's own build zones and dressing)"}");
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
