using Microsoft.AspNetCore.WebUtilities;
using PgmStudio.Export;

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
        if (set is null) return (null, $"the export carries no map.png: {reason}");
        if (WorldViews.PictureOf(built, kept) is not { } view)
            return (null, "the export carries no map.png: the board has no ground to frame");

        var words = QueryHelpers.ParseQuery($"{view.Query}&width={Width}&height={Height}");
        var aim = EyeAim.Read(word => words.TryGetValue(word, out var value) ? value.ToString() : null);
        using (await EyeRenders.TurnAsync(ct))
        {
            var shot = EyeRenders.Of(built, set, flat: false, $"map.png|{aim.Key}", scene =>
                aim.Resolve(scene) is ({ } camera, var how)
                    ? new EyeShot(scene.Draw(camera, Width, Height).Png(), how)
                    : null);
            return shot is null ? (null, $"the export carries no map.png: {aim.Empty}") : (shot.Png, null);
        }
    }
}
