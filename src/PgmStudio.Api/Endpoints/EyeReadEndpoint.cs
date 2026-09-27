using System.Globalization;
using System.Text;
using PgmStudio.Api.Services;
using PgmStudio.Data.Map;
using PgmStudio.Domain;
using PgmStudio.Minecraft.Palette;
using PgmStudio.Minecraft.Render;

namespace PgmStudio.Api.Endpoints;

/// <summary>GET /api/map/{slug}/render/eye — the built world seen from a player's eye, drawn with the game's
/// own block sprites (<see cref="EyeScene"/>). <c>look</c> names a thing to see and the eye finds a place to
/// see it from; <c>from</c> says where to stand; both together stand there and face it. <c>flat</c> draws the
/// same frame with every sprite reduced to its mean. <c>?format=text</c> answers where the eye ended up and
/// what fills the frame, by share. Without the sprites it is a 503 (<c>RQ10</c>).
///
/// <para>Pictures are drawn one at a time and kept with the world they were drawn from
/// (<see cref="EyeRenders"/>), so asking again for a picture of an unchanged board answers at once.</para></summary>
internal sealed class EyeReadEndpoint(MapRepository repo, MapReader reader, MapArtifactStore artifacts,
                                      BlockTextureStore textures)
    : WorldRenderEndpoint(repo, reader, artifacts)
{
    private BlockTextureSet? _textures;
    private string _empty = "nothing to draw";

    public override void Configure()
    {
        Get("/map/{slug}/render/eye");
        Summary(s => s.Summary = WorldReadCatalog.Sentence("render/eye"));
        Description(b => b.Png().Refuses(400, 404, 422, 503).AlsoText().Reads(
            new QueryWord("look", "A thing to see, as `x,z` — a boulder, a spawn, a wall, a house. Alone, the eye "
                + "finds a place on ground within about ten blocks that sees its middle, both flanks and its top "
                + "with nothing in the way, and hovers over it where no ground does; with `from`, the eye stands "
                + "at `from` and faces it."),
            new QueryWord("from", "Where the eye stands, as `x,z`: a player's eye height over the ground there. "
                + "Facing a `look` from a column with no ground, the eye hovers level with the thing's middle."),
            new QueryWord("y", "The eye's height, overriding the one `from` stands at. Ignored with `look` alone."),
            new QueryWord("yaw", "Which way the eye faces with `from` alone, in the game's own degrees: 0 south "
                + "(+z), 90 west, 180 north, 270 east. Absent is 0. Ignored with `look`."),
            new QueryWord("pitch", "Degrees below the horizon, negative looking up. Absent is 10 with `from` "
                + "alone; with `look` and `from`, absent tips the eye to the thing's middle. Ignored with `look` "
                + "alone."),
            new QueryWord("fov", "Horizontal field of view, 30 to 110 degrees. Absent is 70.", Min: 30, Max: 110),
            new QueryWord("width", "Pixels across, 160 to 1920. Absent is 960.", Min: 160, Max: 1920),
            new QueryWord("height", "Pixels down, 90 to 1080. Absent is 540.", Min: 90, Max: 1080),
            new QueryWord("flat", "`1` draws every sprite as its own mean colour — the picture the other reads "
                + "draw, in perspective, so the difference texture makes can be seen.", ["0", "1"])));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var (set, reason) = await textures.GetAsync(ct);
        if (set is null)
        {
            await Refusals.WriteAsync(HttpContext, 503, "no block textures",
                [new Vocabulary.Finding(RequestRules.TexturesUnavailable, reason ?? "no block textures")], ct);
            return;
        }
        _textures = set;
        using (await EyeRenders.TurnAsync(ct))
            await base.HandleAsync(ct);
    }

    protected override string Empty => _empty;

    protected override byte[]? Draw(BuiltRead read) => Shot(read)?.Png;

    protected override string? Text(BuiltRead read) => Shot(read)?.Text;

    private EyeShot? Shot(BuiltRead read)
    {
        var look = Pair("look");
        var from = Pair("from");
        if (look is null && from is null)
            throw new ArgumentException("name `look=x,z` for the eye to find a place to see it from, or `from=x,z` "
                + "for where it stands");

        var fov = Math.Clamp(Number("fov") ?? 70, 30, 110);
        var flat = Query<string?>("flat", isRequired: false) is "1" or "true";
        var width = Math.Clamp(OptionalInt("width") ?? 960, 160, 1920);
        var height = Math.Clamp(OptionalInt("height") ?? 540, 90, 1080);
        var y = Number("y");
        var yaw = Number("yaw") ?? 0;
        var pitch = Number("pitch");
        _empty = (look, from) switch
        {
            ({ } seen, null) => $"no place within reach of {seen.X},{seen.Z} sees it with nothing in the way — "
                + "stand the eye yourself with `from`",
            (null, { } stand) => $"there is no ground to stand on at {stand.X},{stand.Z}; give `y` to stand the "
                + "eye in the air",
            _ => "nothing to draw",
        };
        var asked = string.Create(CultureInfo.InvariantCulture,
            $"{look}|{from}|{y}|{yaw}|{pitch}|{fov}|{width}|{height}|{flat}");

        return EyeRenders.Of(read.Built, _textures!, flat, asked, scene =>
        {
            EyeCamera? camera;
            string aim;
            if (look is { } seen && from is { } stand)
            {
                camera = scene.Facing(stand.X, stand.Z, seen.X, seen.Z, fov, y, pitch);
                aim = $"standing at {stand.X},{stand.Z} and facing {seen.X},{seen.Z}";
            }
            else if (look is { } target)
            {
                camera = scene.Frame(target.X, target.Z, fov: fov);
                aim = $"placed to see {target.X},{target.Z}";
            }
            else
            {
                var (x, z) = from!.Value;
                camera = (y ?? scene.EyeAt(x, z)) is { } eye
                    ? new EyeCamera(x + 0.5, eye, z + 0.5, yaw, pitch ?? 10, fov)
                    : null;
                aim = $"standing at {x},{z}";
            }
            if (camera is not { } resolved) return null;
            var picture = scene.Draw(resolved, width, height);
            return new EyeShot(picture.Png(), Describe(picture, resolved, aim));
        });
    }

    private (int X, int Z)? Pair(string name)
    {
        if (Query<string?>(name, isRequired: false) is not { Length: > 0 } asked) return null;
        var parts = asked.Split(',');
        if (parts.Length == 2
            && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
            && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
            return ((int)Math.Floor(x), (int)Math.Floor(z));
        throw new ArgumentException($"`{name}` is `x,z`, and '{asked}' is not");
    }

    private double? Number(string name) =>
        Query<string?>(name, isRequired: false) is { Length: > 0 } asked
            ? double.TryParse(asked, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                ? value
                : throw new ArgumentException($"`{name}` is a number, and '{asked}' is not")
            : null;

    private static string Describe(EyePicture picture, EyeCamera camera, string aim)
    {
        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture,
            $"eye   x {camera.X:0.#}  y {camera.Y:0.##}  z {camera.Z:0.#}   yaw {camera.Yaw:0}  pitch {camera.Pitch:0}  fov {camera.Fov:0}\n");
        text.Append(CultureInfo.InvariantCulture, $"      {aim}\n");
        text.Append(CultureInfo.InvariantCulture,
            $"frame {picture.Width}x{picture.Height}   sky {picture.Sky:P0}   drawn without a sprite {picture.Untextured:P1}\n\n");
        text.Append("share   block\n");
        foreach (var seen in picture.Seen.Take(12))
            text.Append(CultureInfo.InvariantCulture,
                $"{seen.Share,6:P1}  {BlockPalette.Name(seen.Id, seen.Data)} ({seen.Id}:{seen.Data})\n");
        var rest = picture.Seen.Skip(12).Sum(seen => seen.Share);
        if (rest > 0) text.Append(CultureInfo.InvariantCulture, $"{rest,6:P1}  {picture.Seen.Count - 12} others\n");
        return text.ToString();
    }
}
