using PgmStudio.Geom;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Render;

namespace PgmStudio.Api.Services;

/// <summary>
/// The library's card for a block structure — a building, a tree or a boulder — drawn the way a pattern's is:
/// with the game's sprites where the studio has them, and in palette colours where it has not.
///
/// <para>A structure is seen as the volume it is, so the textured card is <see cref="StructurePicture"/>'s
/// three-quarter view of the structure on its patch of ground, embedded as an <c>&lt;img&gt;</c>. Without
/// sprites the card is the flat drawing the caller supplies. Either way it is drawn once per distinct input
/// (<see cref="Drawings"/>), and the textured name carries <see cref="PictureSprites.Identity"/> so a studio
/// that gains or changes its sprites draws again.</para>
/// </summary>
public static class StructureCard
{
    /// <summary>The pixels a library card's picture takes, and the multiple an editor's stage takes.</summary>
    public const int CardWide = 160, CardHigh = 120, StageScale = 2;

    /// <summary>An editor's stage, as markup: <paramref name="volume"/> drawn with <paramref name="sprites"/> at
    /// <see cref="StageScale"/>, or <paramref name="flat"/> where there are none. Both are called only when the
    /// picture is not already kept.</summary>
    public static string Stage(string drawer, string input, PictureSprites sprites,
        Func<string> flat, Func<(VoxelWorld World, BlockBox Box)> volume)
    {
        if (sprites.Set is not { } textures) return Drawings.Svg(drawer, input, flat);

        return Drawings.Svg($"{drawer}/render/{StageScale}/{sprites.Identity}", input, () =>
        {
            var (world, box) = volume();
            return Markup(StructurePicture.Png(world, box, textures, CardWide * StageScale, CardHigh * StageScale),
                StageScale);
        });
    }

    /// <summary>A library row's card: <paramref name="volume"/> drawn with <paramref name="sprites"/> as a PNG, or
    /// <paramref name="flat"/>'s SVG where there are none, named before either is drawn.</summary>
    public static LibraryCard Card(string drawer, string input, PictureSprites sprites,
        Func<string> flat, Func<(VoxelWorld World, BlockBox Box)> volume)
    {
        if (sprites.Set is not { } textures)
            return new(Drawings.Name($"card/{drawer}/flat", input), LibraryCard.Rendered, () => LibraryCards.Svg(flat()));
        return new(Drawings.Name($"card/{drawer}/render/{sprites.Identity}", input), LibraryCard.Rendered, () =>
        {
            var (world, box) = volume();
            return StructurePicture.Png(world, box, textures, CardWide, CardHigh);
        });
    }

    /// <summary>A rendered card as the markup the client shows, sized to the card it fills.</summary>
    public static string Markup(byte[] png, int scale = 1)
        => $"<img class=\"block-render\" alt=\"\" width=\"{CardWide * scale}\" height=\"{CardHigh * scale}\" "
           + $"src=\"data:image/png;base64,{Convert.ToBase64String(png)}\">";
}
