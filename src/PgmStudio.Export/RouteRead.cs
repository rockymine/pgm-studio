using System.Text;
using PgmStudio.Geom;
using PgmStudio.Geom.Algorithms;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Dressing;
using PgmStudio.Minecraft.Palette;

namespace PgmStudio.Export;

/// <summary>
/// A drawn route, walked end to end — the road as it was laid rather than the journey a walker would choose.
/// <c>route: true</c> on a stroke makes every other prop keep its distance (<c>DR-ROAD</c>), and nothing read
/// the stroke itself: <c>walk</c> answers the way a player finds between two points, which is not the way the
/// author drew, and a picture of the board shows paving without saying what a player crossing it meets.
///
/// <para>The line walked is the stroke's <b>own</b> centreline, taken through the same spline
/// (<see cref="PathBand.Centerline"/>) and the same orbit fan (<see cref="DressingSymmetry.ImageRing"/>) the
/// pass laid it with, so a curve is followed rather than cut across and the mirrored image is the road that
/// image actually stands on. Whether a station is <b>paved</b> is read off the claim the pass recorded, since
/// the style, the coverage and the seed decide which cells of the band take surface and none of the three can
/// be reasoned about from the document.</para>
///
/// <para>Three things come back that a column read cannot say: the worst step between neighbouring stations,
/// what the road is made of over its length, and where it stops being a road — a run of stations the paving
/// does not reach, which is ground kept clear, coverage the style left out, or the road running off the
/// board.</para>
/// </summary>
public static class RouteRead
{
    /// <summary>One block of the walk down the centreline. <see cref="Ground"/> is the terrain's own recorded
    /// height, null over void; <see cref="Material"/> is the surface block under the station, named, or null
    /// where there is no ground; <see cref="Step"/> is the rise from the station before, and
    /// <see cref="Word"/> is <see cref="Walk.StepWord"/>'s reading of it — <c>void</c> where there is no
    /// ground to step onto.</summary>
    public readonly record struct Station(
        int X, int Z, int? Ground, bool Paved, string? Material, int? Step, string Word);

    /// <summary>One surface block the road is paved with, and how many of its stations carry it.</summary>
    public sealed record Run(string Material, int Cells);

    /// <summary>A stretch the paving does not reach, from the first station of the gap to the last.</summary>
    public sealed record Gap(int FromX, int FromZ, int ToX, int ToZ, int Cells);

    /// <summary>The walk and what it adds up to. <see cref="Images"/> is how many the orbit has, so a caller
    /// reading one knows what else there is to read.</summary>
    public sealed record Walked(
        string Id, int Image, int Images, bool Route, IReadOnlyList<Station> Stations, int Paved,
        int Rises, int Falls, int WorstStep, IReadOnlyList<string> Events,
        IReadOnlyList<Run> Materials, int MaterialRuns, IReadOnlyList<Gap> Gaps);

    /// <summary>The named stroke's <paramref name="image"/>-th road, walked. Null where the document carries
    /// no stroke of that id — the caller's cue to name the ones it does.</summary>
    public static Walked? Of(BuiltWorld built, string layoutJson, string id, int image)
    {
        if (DressingScope.PropsOf(layoutJson).OfType<StrokeProp>().FirstOrDefault(prop => prop.Id == id)
            is not { } stroke) return null;

        var symmetry = DressingScope.SymmetryOf(layoutJson);
        var images = Math.Max(1, symmetry.Order);
        image = Math.Clamp(image, 0, images - 1);

        var paved = built.Dressing.Placements
            .Where(claim => claim.Owner.Kind == "stroke" && claim.Owner.Unit == id && claim.Owner.Image == image)
            .SelectMany(claim => claim.Cells).ToHashSet();

        var stations = new List<Station>();
        int? before = null;
        foreach (var cell in Down(PathBand.Centerline(symmetry.ImageRing(stroke.Points, image))))
        {
            var ground = built.Surface.TryGetValue(cell, out var top) ? top : (int?)null;
            int? step = before is { } prior && ground is { } here ? here - prior : null;
            stations.Add(new Station(cell.X, cell.Z, ground, paved.Contains(cell),
                ground is null ? null : Material(built, cell, ground.Value),
                step, ground is null ? "void" : step is { } delta ? Walk.StepWord(delta) : "walk"));
            before = ground;
        }

        var events = stations.Where(station => station.Word is "barrier" or "scramble" or "drop")
            .Select(station => $"{Word(station.Word)} {Signed(station.Step!.Value)} at ({station.X}, {station.Z})")
            .ToList();

        return new Walked(id, image, images, stroke.Route, stations,
            stations.Count(station => station.Paved),
            stations.Count(station => station.Step is > 0), stations.Count(station => station.Step is < 0),
            stations.Where(station => station.Step is not null)
                    .Select(station => Math.Abs(station.Step!.Value)).DefaultIfEmpty(0).Max(),
            events, Materials(stations), Runs(stations), Gaps(stations));
    }

    /// <summary>The centreline as the cells it runs through, in order and each once — the sampled spline
    /// joined block by block, so a station is a block a player stands on rather than a sample point.</summary>
    private static IEnumerable<(int X, int Z)> Down(IReadOnlyList<double[]> centerline)
    {
        (int X, int Z)? last = null;
        foreach (var point in centerline)
        {
            var cell = ((int)Math.Floor(point[0]), (int)Math.Floor(point[1]));
            if (last is { } previous)
            {
                foreach (var walked in Cells.Line(previous, cell).Skip(1))
                {
                    if (walked == last) continue;
                    yield return walked;
                    last = walked;
                }
            }
            else
            {
                yield return cell;
                last = cell;
            }
        }
    }

    /// <summary>What the road is made of at a station: the first solid block under the recorded surface,
    /// which is the course a stroke's paving replaced.</summary>
    private static string? Material(BuiltWorld built, (int X, int Z) cell, int ground)
    {
        for (var y = ground - 1; y >= 0; y--)
        {
            var (id, data) = built.World.GetBlock(cell.X, y, cell.Z);
            if (id != 0) return $"{id}:{data} {BlockPalette.Name(id, data)}";
        }
        return null;
    }

    /// <summary>What the road is paved with, most of it first — the blocks under its paved stations. A road
    /// crossing a patterned surface carries several, which is the reading a single "material" cannot give.</summary>
    private static List<Run> Materials(IReadOnlyList<Station> stations)
    {
        var cells = new Dictionary<string, int>();
        foreach (var station in stations)
            if (station.Paved && station.Material is { } material)
                cells[material] = cells.GetValueOrDefault(material) + 1;
        return [.. cells.OrderByDescending(entry => entry.Value).Select(entry => new Run(entry.Key, entry.Value))];
    }

    /// <summary>How many unbroken runs of one material the paving falls into — one is a road of a single
    /// block, and a count near its own length is a road speckled over a patterned ground.</summary>
    private static int Runs(IReadOnlyList<Station> stations)
    {
        var runs = 0;
        string? held = null;
        foreach (var station in stations)
        {
            if (!station.Paved || station.Material is not { } material) { held = null; continue; }
            if (material != held) runs++;
            held = material;
        }
        return runs;
    }

    /// <summary>Every stretch the paving does not reach, each from its first station to its last.</summary>
    private static List<Gap> Gaps(IReadOnlyList<Station> stations)
    {
        var gaps = new List<Gap>();
        for (var i = 0; i < stations.Count; i++)
        {
            if (stations[i].Paved) continue;
            var from = i;
            while (i + 1 < stations.Count && !stations[i + 1].Paved) i++;
            gaps.Add(new Gap(stations[from].X, stations[from].Z, stations[i].X, stations[i].Z, i - from + 1));
        }
        return gaps;
    }

    /// <summary>The walk as characters: the road's own line, a station a row, then what it is made of, where
    /// the paving stops and every step that is not a plain walk.</summary>
    public static string Render(Walked walked)
    {
        var text = new StringBuilder();
        var first = walked.Stations.Count > 0 ? walked.Stations[0] : default;
        var last = walked.Stations.Count > 0 ? walked.Stations[^1] : default;
        text.Append($"ROUTE '{walked.Id}' image {walked.Image} of {walked.Images}")
            .Append(walked.Route ? "" : " (paint, not a route)")
            .Append($": {walked.Stations.Count} stations from ({first.X}, {first.Z}) to ({last.X}, {last.Z}), ")
            .Append($"{walked.Paved} paved\n");

        text.Append("  station        ground  paved  material                       step\n");
        foreach (var station in walked.Stations)
            text.Append($"  ({station.X,5}, {station.Z,5})  {station.Ground?.ToString() ?? "",6}  ")
                .Append($"{(station.Paved ? "yes" : "no"),5}  {station.Material ?? "",-28}  ")
                .Append($"{Step(station)}\n");

        text.Append($"rises {walked.Rises}, falls {walked.Falls}, worst step {walked.WorstStep}: ")
            .Append(walked.Events.Count == 0 ? "walked end to end" : string.Join("; ", walked.Events))
            .Append('\n');
        text.Append("materials: ")
            .Append(walked.Materials.Count == 0 ? "(none paved)"
                : string.Join(", ", walked.Materials.Select(run => $"{run.Cells} × {run.Material}"))
                  + $", in {walked.MaterialRuns} run(s)")
            .Append('\n');
        text.Append("unpaved: ")
            .Append(walked.Gaps.Count == 0 ? "(none)"
                : string.Join(", ", walked.Gaps.Select(gap =>
                    $"{gap.Cells} at ({gap.FromX}, {gap.FromZ})..({gap.ToX}, {gap.ToZ})")))
            .Append('\n');
        return text.ToString();
    }

    private static string Step(Station station) => station.Word switch
    {
        "void" => "void",
        "barrier" or "drop" or "scramble" => $"{Word(station.Word)} {Signed(station.Step!.Value)}",
        _ => station.Step is null or 0 ? "" : Signed(station.Step.Value),
    };

    private static string Word(string word) => word is "barrier" or "drop" ? word.ToUpperInvariant() : word;

    private static string Signed(int value) => value > 0 ? $"+{value}" : value.ToString();
}
