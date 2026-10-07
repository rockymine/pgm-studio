using Microsoft.AspNetCore.WebUtilities;
using PgmStudio.Export;
using PgmStudio.Minecraft.Palette;
using PgmStudio.Minecraft.Render;

namespace PgmStudio.Api.Services;

/// <summary>
/// The picture a PGM server shows a map by: <c>map.png</c> in the map's folder, an overview of the playing area
/// in the game's own block sprites at the author's size. It is drawn from the view
/// <see cref="WorldViews.PictureOf"/> names — the kept view marked as the picture, else the whole board from
/// above its long side — through the same eye <c>render/eye</c> draws with.
/// </summary>
internal static class MapPicture
{
    /// <summary>The map picture's size, in pixels: the author's standard, and the frame a server's map list
    /// draws it in.</summary>
    public const int Width = 290, Height = 246;

    /// <summary>The picture of <paramref name="built"/>, or why there is none: this server has no block sprites,
    /// the board has no ground to frame, or the view finds no place to stand.</summary>
    public static async Task<(byte[]? Png, string? Missing)> DrawAsync(
        BuiltWorld built, IReadOnlyList<WorldView> kept, BlockTextureStore textures, CancellationToken ct)
    {
        var (set, reason) = await textures.GetAsync(ct);
        if (set is null) return (null, $"the export has no map.png, and {reason}");
        using (await EyeRenders.TurnAsync(ct))
            return Draw(built, kept, set, Width, Height) is { } png
                ? (png, null)
                : (null, "the export has no map.png, and the layout has no ground to frame or no place to stand over it");
    }

    /// <summary>The picture of <paramref name="built"/> at a size, or null where the board has no ground to frame
    /// or its view finds no place to stand. The caller holds a turn (<see cref="EyeRenders.TurnAsync"/>).</summary>
    public static byte[]? Draw(BuiltWorld built, IReadOnlyList<WorldView> kept, BlockTextureSet set, int width, int height)
    {
        if (WorldViews.PictureOf(built, kept) is not { } view) return null;
        var words = QueryHelpers.ParseQuery($"{view.Query}&width={width}&height={height}");
        var aim = EyeAim.Read(word => words.TryGetValue(word, out var value) ? value.ToString() : null);
        return EyeRenders.Of(built, set, flat: false, $"map.png|{aim.Key}", scene =>
            aim.Resolve(scene) is ({ } camera, var how)
                ? new EyeShot(scene.Draw(camera, width, height).Png(), how)
                : null)?.Png;
    }
}
