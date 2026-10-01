using System.Globalization;
using System.Text;
using PgmStudio.Api.Services;
using PgmStudio.Data.Map;
using PgmStudio.Domain;
using PgmStudio.Export;
using PgmStudio.Minecraft.Palette;
using PgmStudio.Minecraft.Render;

namespace PgmStudio.Api.Endpoints;

/// <summary>GET /api/map/{slug}/render/eye — the built world seen from a player's eye, drawn with the game's
/// own block sprites (<see cref="EyeScene"/>). <c>look</c> names a thing to see and the eye finds a place to
/// see it from; <c>from</c> says where to stand; both together stand there and face it; <c>eye</c> stands the
/// camera exactly (<see cref="EyeAim"/>). <c>flat</c> draws the
/// same frame with every sprite reduced to its mean. <c>?format=text</c> answers where the eye ended up and
/// what fills the frame, by share. Without the sprites it is a 503 (<c>RQ10</c>).
///
/// <para>Pictures are drawn one at a time and kept with the world they were drawn from
/// (<see cref="EyeRenders"/>), so asking again for a picture of an unchanged board answers at once.</para></summary>
[Queued]
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
            new QueryWord("eye", "Exactly where the eye stands, as `x,y,z`, facing `yaw` and `pitch` — the camera "
                + "any other aim resolved to, which `render/eye/pick` answers as `query`, drawn again. Takes neither "
                + "`look` nor `from`."),
            new QueryWord("y", "The eye's height, overriding the one `from` stands at. Ignored with `look` alone."),
            new QueryWord("yaw", "Which way the eye faces with `from` or `eye` alone, in the game's own degrees: 0 "
                + "south (+z), 90 west, 180 north, −90 east, from −180 to 180. Absent is 0. Ignored with `look`."),
            new QueryWord("pitch", "Degrees below the horizon, negative looking up; 90 looks straight down. Absent "
                + "is 10 with `from` or `eye` alone; with `look` and `from`, absent tips the eye to the thing's "
                + "middle. Ignored with `look` alone."),
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
        var aim = EyeAim.Read(word => Query<string?>(word, isRequired: false));
        _empty = aim.Empty;
        return Shot(read.Built, _textures!, aim);
    }

    /// <summary>The picture <paramref name="aim"/> asks of <paramref name="built"/> and its text twin, kept with
    /// the world — the one draw this read and the report both take, so a view drawn by either is the other's.
    /// Null where the aim finds no place to stand. The caller holds a turn (<see cref="EyeRenders.TurnAsync"/>).</summary>
    internal static EyeShot? Shot(BuiltWorld built, BlockTextureSet textures, EyeAim aim) =>
        EyeRenders.Of(built, textures, aim.Flat, aim.Key, scene =>
        {
            if (aim.Resolve(scene) is not ({ } camera, var how)) return null;
            var picture = scene.Draw(camera, aim.Width, aim.Height);
            return new EyeShot(picture.Png(), Describe(picture, camera, how));
        });

    /// <summary>Which way a pitch looks, since the number alone leaves it to the reader: the game counts
    /// degrees below the horizon, so a positive pitch looks down.</summary>
    private static string Tilt(double pitch) => pitch switch { > 0.5 => "down", < -0.5 => "up", _ => "level" };

    private static string Describe(EyePicture picture, EyeCamera camera, string aim)
    {
        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture,
            $"eye   x {camera.X:0.#}  y {camera.Y:0.##}  z {camera.Z:0.#}   yaw {camera.Yaw:0}  pitch {camera.Pitch:0} ({Tilt(camera.Pitch)})  fov {camera.Fov:0}\n");
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
