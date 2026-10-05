using System.Globalization;
using FastEndpoints;
using PgmStudio.Api.Services;
using PgmStudio.Contracts;
using PgmStudio.Data.Map;
using PgmStudio.Domain;
using PgmStudio.Minecraft.Render;

namespace PgmStudio.Api.Endpoints;

/// <summary>GET /api/map/{slug}/render/eye/pick — what a mark drawn on a <c>render/eye</c> picture is on the
/// ground. It takes the picture's own query words, so it resolves the same camera, and re-casts the ray through
/// each pixel of the mark against the board as stored: <c>at</c> is one pixel and answers the block it hits and
/// the ground under it, <c>box</c> and <c>lasso</c> answer every ground column their pixels' rays hit. With none
/// of the three it answers the camera alone, which is what a note on a whole picture keeps. Every answer names
/// the camera exactly, since a picture that leaves the eye to find its own place may find another once the board
/// changes, and the change it read, since a picture of an earlier one is not of this board.</summary>
[Queued]
public sealed class EyePickEndpoint(MapRepository repo, MapReader reader, MapArtifactStore artifacts,
                                    BlockTextureStore textures, MapChangeLog log)
    : EndpointWithoutRequest<EyePickDto>
{
    /// <summary>The most points a lasso's outline may carry.</summary>
    private const int MostOutline = 2_000;

    public override void Configure()
    {
        Get("/map/{slug}/render/eye/pick");
        Description(b => b.Produces<EyePickDto>(200, "application/json").Refuses(400, 404, 422, 503).Reads(
            new QueryWord("look", "The picture's own `look`, as `render/eye` takes it."),
            new QueryWord("from", "The picture's own `from`."),
            new QueryWord("eye", "The picture's own `eye`."),
            new QueryWord("y", "The picture's own `y`."),
            new QueryWord("yaw", "The picture's own `yaw`."),
            new QueryWord("pitch", "The picture's own `pitch`."),
            new QueryWord("fov", "The picture's own `fov`.", Min: 30, Max: 110),
            new QueryWord("width", "The picture's width, which the pixels are counted in.", Min: 160, Max: 1920),
            new QueryWord("height", "The picture's height.", Min: 90, Max: 1080),
            new QueryWord("at", "One pixel, as `x,y` from the picture's top-left corner: answers the block its "
                + "ray hits and the ground under it."),
            new QueryWord("box", "A rectangle, as two opposite corners `x,y,x,y`: answers every ground column "
                + "the rays through its pixels hit."),
            new QueryWord("lasso", $"An outline, as `x,y;x,y;…` of 3 to {MostOutline} pixels: answers every "
                + "ground column the rays through the pixels inside it hit.")));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;
        var (set, reason) = await textures.GetAsync(ct);
        if (set is null)
        {
            await Refusals.WriteAsync(HttpContext, 503, "no block textures",
                [new Vocabulary.Finding(RequestRules.TexturesUnavailable, reason ?? "the studio has no block textures")], ct);
            return;
        }

        EyeAim aim;
        Mark mark;
        try
        {
            aim = EyeAim.Read(word => Query<string?>(word, isRequired: false));
            mark = Mark.Read(word => Query<string?>(word, isRequired: false), aim.Width, aim.Height);
        }
        catch (ArgumentException fault)
        {
            await Refusals.UnreadableAsync(HttpContext, "cannot pick that", fault.Message, ct);
            return;
        }

        if (await WorldReads.LoadAsync(map, reader, artifacts, ct) is not { } read)
        {
            await Refusals.WriteAsync(HttpContext, 404, "no world to read",
                [new Vocabulary.Finding(PgmStudio.Pgm.Sketch.SketchRules.NothingStored,
                    "the map has no stored sketch layout")], ct);
            return;
        }
        // Read after the world: a change landing between the two then reads as one the pick has seen, which asks
        // for a redraw rather than letting a stale picture through.
        var change = await log.LatestAsync(map.Slug, ct);

        EyePickDto? answer;
        using (await EyeRenders.TurnAsync(ct))
        {
            var scene = EyeRenders.Scene(read.Built, set, aim.Flat);
            answer = aim.Resolve(scene) is ({ } camera, _) ? Pick(scene, aim, camera, mark, change) : null;
        }
        if (answer is null)
        {
            await Refusals.WriteAsync(HttpContext, 422, "nothing to pick",
                [new Vocabulary.Finding(RequestRules.Conflict, aim.Empty)], ct);
            return;
        }
        await Send.OkAsync(answer, ct);
    }

    private static EyePickDto Pick(EyeScene scene, EyeAim aim, EyeCamera camera, Mark mark, long change)
    {
        var lens = new EyeCameraDto(camera.X, camera.Y, camera.Z, camera.Yaw, camera.Pitch, camera.Fov);
        var query = aim.Exact(camera);
        var standing = scene.GroundAt((int)Math.Floor(camera.X), (int)Math.Floor(camera.Z));
        if (mark.Point is { } pixel)
        {
            var hit = scene.Pick(camera, aim.Width, aim.Height, pixel.X, pixel.Y);
            return new EyePickDto(lens, query,
                hit is { Block: var block } ? new BlockAtDto(block.X, block.Y, block.Z) : null,
                hit?.Ground is { } ground ? new BlockAtDto(ground.X, ground.Y, ground.Z) : null,
                [], hit is null ? 1 : 0, [], standing, change);
        }
        if (mark.Pixels.Count == 0) return new EyePickDto(lens, query, null, null, [], 0, [], standing, change);
        var area = scene.Project(camera, aim.Width, aim.Height, mark.Pixels);
        return new EyePickDto(lens, query, null, null, Cells(area.Columns), area.Sky, Cells(area.OverVoid), standing, change);
    }

    private static int[][] Cells(IReadOnlyDictionary<(int X, int Z), int> heights) =>
        [.. heights.OrderBy(entry => entry.Key.X).ThenBy(entry => entry.Key.Z)
            .Select(entry => new[] { entry.Key.X, entry.Value, entry.Key.Z })];

    /// <summary>A mark in a picture's pixels: one pixel, or the pixels an area covers. Neither is the camera
    /// alone.</summary>
    internal sealed record Mark((int X, int Y)? Point, IReadOnlyList<(int X, int Y)> Pixels)
    {
        public static Mark Read(Func<string, string?> word, int width, int height)
        {
            var stated = new[] { "at", "box", "lasso" }.Where(name => word(name) is { Length: > 0 }).ToList();
            if (stated.Count > 1)
                throw new ArgumentException($"a mark is one of `at`, `box` or `lasso`, and {string.Join(" and ", stated)} are both given");
            if (word("at") is { Length: > 0 } at)
            {
                var points = Points(at, "at", width, height);
                if (points.Count != 1) throw new ArgumentException("`at` is one pixel, `x,y`");
                return new Mark(points[0], []);
            }
            if (word("box") is { Length: > 0 } box)
            {
                var corners = Points(box.Replace(',', ';').Split(';') is { Length: 4 } parts
                    ? $"{parts[0]},{parts[1]};{parts[2]},{parts[3]}"
                    : throw new ArgumentException("`box` is two opposite corners, `x,y,x,y`"), "box", width, height);
                int left = Math.Min(corners[0].X, corners[1].X), right = Math.Max(corners[0].X, corners[1].X);
                int top = Math.Min(corners[0].Y, corners[1].Y), bottom = Math.Max(corners[0].Y, corners[1].Y);
                var pixels = new List<(int X, int Y)>((right - left + 1) * (bottom - top + 1));
                for (var y = top; y <= bottom; y++)
                    for (var x = left; x <= right; x++) pixels.Add((x, y));
                return new Mark(null, pixels);
            }
            if (word("lasso") is { Length: > 0 } lasso)
            {
                var outline = Points(lasso, "lasso", width, height);
                if (outline.Count < 3 || outline.Count > MostOutline)
                    throw new ArgumentException($"`lasso` is an outline of 3 to {MostOutline} pixels, `x,y;x,y;…`");
                return new Mark(null, Inside(outline));
            }
            return new Mark(null, []);
        }

        /// <summary>Every pixel whose middle lies inside the outline, by the even-odd rule.</summary>
        private static List<(int X, int Y)> Inside(IReadOnlyList<(int X, int Y)> outline)
        {
            int top = outline.Min(point => point.Y), bottom = outline.Max(point => point.Y);
            var pixels = new List<(int X, int Y)>();
            var crossings = new List<double>();
            for (var y = top; y <= bottom; y++)
            {
                var middle = y + 0.5;
                crossings.Clear();
                for (int i = 0, j = outline.Count - 1; i < outline.Count; j = i++)
                {
                    var (ax, ay) = (outline[i].X + 0.5, outline[i].Y + 0.5);
                    var (bx, by) = (outline[j].X + 0.5, outline[j].Y + 0.5);
                    if ((ay > middle) == (by > middle)) continue;
                    crossings.Add(ax + (middle - ay) / (by - ay) * (bx - ax));
                }
                crossings.Sort();
                for (var k = 0; k + 1 < crossings.Count; k += 2)
                {
                    var from = (int)Math.Ceiling(crossings[k] - 0.5);
                    var to = (int)Math.Floor(crossings[k + 1] - 0.5);
                    for (var x = from; x <= to; x++) pixels.Add((x, y));
                }
            }
            return pixels;
        }

        private static List<(int X, int Y)> Points(string asked, string name, int width, int height)
        {
            var points = new List<(int X, int Y)>();
            foreach (var pair in asked.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = pair.Split(',');
                if (parts.Length != 2
                    || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x)
                    || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y))
                    throw new ArgumentException($"`{name}` is pixels as `x,y`, and '{pair}' is not one");
                if (x < 0 || y < 0 || x >= width || y >= height)
                    throw new ArgumentException($"`{name}` names {x},{y}, which is outside a {width} × {height} picture");
                points.Add((x, y));
            }
            return points;
        }
    }
}
