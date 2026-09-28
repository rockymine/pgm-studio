using System.Globalization;
using PgmStudio.Geom;
using PgmStudio.Minecraft.Render;

namespace PgmStudio.Api.Services;

/// <summary>
/// Where an eye is asked to stand and look, as <c>render/eye</c>'s query words state it, and the camera that
/// makes of a scene. Drawing a picture and picking a block in one resolve the same words the same way, so the
/// pixel a mark was drawn on is the pixel the pick re-casts.
///
/// <para>Four ways to aim, by which words are given: <c>look</c> alone lets the eye find its own place;
/// <c>from</c> and <c>look</c> stand at one and face the other; <c>from</c> alone stands there facing
/// <c>yaw</c>; <c>eye=x,y,z</c> stands exactly there facing <c>yaw</c> and <c>pitch</c>, which is how a camera
/// any of the others resolved to is drawn again. A yaw is wrapped into the game's (−180, 180].</para>
/// </summary>
internal sealed record EyeAim(
    (int X, int Z)? Look, (int X, int Z)? From, (double X, double Y, double Z)? Eye,
    double? Y, double Yaw, double? Pitch, double Fov, int Width, int Height, bool Flat)
{
    /// <summary>The query words, read through <paramref name="word"/>. Throws <see cref="ArgumentException"/>
    /// naming the word that is wrong.</summary>
    public static EyeAim Read(Func<string, string?> word)
    {
        var look = Pair(word, "look");
        var from = Pair(word, "from");
        var eye = Triple(word, "eye");
        if (eye is not null && (look is not null || from is not null))
            throw new ArgumentException("`eye=x,y,z` stands the camera whole, facing `yaw` and `pitch` — it takes "
                + "neither `look` nor `from`");
        if (look is null && from is null && eye is null)
            throw new ArgumentException("name `look=x,z` for the eye to find a place to see it from, `from=x,z` "
                + "for where it stands, or `eye=x,y,z` for exactly where");
        return new EyeAim(look, from, eye, Number(word, "y"), Heading.Wrap(Number(word, "yaw") ?? 0), Number(word, "pitch"),
            Math.Clamp(Number(word, "fov") ?? 70, 30, 110),
            Math.Clamp(Whole(word, "width") ?? 960, 160, 1920),
            Math.Clamp(Whole(word, "height") ?? 540, 90, 1080),
            word("flat") is "1" or "true");
    }

    /// <summary>What names this picture among the ones drawn of one world.</summary>
    public string Key => string.Create(CultureInfo.InvariantCulture,
        $"{Look}|{From}|{Eye}|{Y}|{Yaw}|{Pitch}|{Fov}|{Width}|{Height}|{Flat}");

    /// <summary>Why this aim can find no camera, for the 422 that says so.</summary>
    public string Empty => (Look, From) switch
    {
        ({ } seen, null) => $"no place within reach of {seen.X},{seen.Z} sees it with nothing in the way — "
            + "stand the eye yourself with `from`",
        (null, { } stand) => $"there is no ground to stand on at {stand.X},{stand.Z}; give `y` to stand the "
            + "eye in the air",
        _ => "nothing to draw",
    };

    /// <summary>The camera this aim makes of <paramref name="scene"/>, and a line saying how it was aimed; null
    /// where no camera can be placed.</summary>
    public (EyeCamera Camera, string Aim)? Resolve(EyeScene scene)
    {
        if (Eye is { } exact)
            return (new EyeCamera(exact.X, exact.Y, exact.Z, Yaw, Pitch ?? 10, Fov),
                    string.Create(CultureInfo.InvariantCulture, $"standing at {exact.X:0.##},{exact.Y:0.##},{exact.Z:0.##}"));
        if (Look is { } seen && From is { } stand)
            return (scene.Facing(stand.X, stand.Z, seen.X, seen.Z, Fov, Y, Pitch),
                    $"standing at {stand.X},{stand.Z} and facing {seen.X},{seen.Z}");
        if (Look is { } target)
            return scene.Frame(target.X, target.Z, fov: Fov) is { } framed
                ? (framed, $"placed to see {target.X},{target.Z}")
                : null;
        var (x, z) = From!.Value;
        return (Y ?? scene.EyeAt(x, z)) is { } height
            ? (new EyeCamera(x + 0.5, height, z + 0.5, Yaw, Pitch ?? 10, Fov), $"standing at {x},{z}")
            : null;
    }

    /// <summary>The query words that draw <paramref name="camera"/> again exactly, at this aim's size.</summary>
    public string Exact(EyeCamera camera) => string.Create(CultureInfo.InvariantCulture,
        $"eye={camera.X:0.####},{camera.Y:0.####},{camera.Z:0.####}&yaw={camera.Yaw:0.####}&pitch={camera.Pitch:0.####}"
        + $"&fov={camera.Fov:0.##}&width={Width}&height={Height}");

    private static (int X, int Z)? Pair(Func<string, string?> word, string name)
    {
        if (word(name) is not { Length: > 0 } asked) return null;
        var parts = asked.Split(',');
        if (parts.Length == 2
            && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
            && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
            return ((int)Math.Floor(x), (int)Math.Floor(z));
        throw new ArgumentException($"`{name}` is `x,z`, and '{asked}' is not");
    }

    private static (double X, double Y, double Z)? Triple(Func<string, string?> word, string name)
    {
        if (word(name) is not { Length: > 0 } asked) return null;
        var parts = asked.Split(',');
        if (parts.Length == 3
            && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
            && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y)
            && double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var z)
            && double.IsFinite(x + y + z))
            return (x, y, z);
        throw new ArgumentException($"`{name}` is `x,y,z`, and '{asked}' is not");
    }

    private static double? Number(Func<string, string?> word, string name) =>
        word(name) is { Length: > 0 } asked
            ? double.TryParse(asked, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value)
                ? value
                : throw new ArgumentException($"`{name}` is a number, and '{asked}' is not")
            : null;

    private static int? Whole(Func<string, string?> word, string name) =>
        word(name) is { Length: > 0 } asked
            ? int.TryParse(asked, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value
                : throw new ArgumentException($"`{name}` is a whole number, and '{asked}' is not")
            : null;
}
