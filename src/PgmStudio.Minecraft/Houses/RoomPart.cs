using System.Text.Json.Serialization;
using PgmStudio.Minecraft.Painting;
using PgmStudio.Minecraft.Palette;

namespace PgmStudio.Minecraft.Houses;

/// <summary>
/// A part of a room shell — its wall, or the plate its foundation stands on — as a
/// <see cref="BandStack"/> plus how far the part runs. The stack is read from the part's own base outward: a
/// plate downward from the surface players stand on, a wall upward.
///
/// <para>The part owns the whole of its own space, so its stack <b>repeats</b>: <see cref="Extent"/> moves
/// without the bands being re-authored — a taller wall grows in whatever its top course is, and a band written
/// at the fourth course stays at the fourth course rather than sliding with the height.</para>
/// </summary>
public sealed record RoomPart([property: JsonPropertyName("stack")] BandStack Stack, int Extent)
{
    /// <summary>The material <paramref name="step"/> courses along the part, and how deep into that band the
    /// step is — what a nested stack reads, so a band may itself be one. A part with no bands at all is air:
    /// nothing claims the course, and a part is the only thing that could have.</summary>
    public (TerrainMaterial Material, int DepthInCourse) At(int step)
    {
        var (material, into) = Stack.At(step);
        return (material ?? Air, into);
    }

    /// <summary>A part of one material, <paramref name="extent"/> courses deep — the common case.</summary>
    public static RoomPart Of(TerrainMaterial material, int extent = 1) => new(BandStack.Of(material), extent);

    private static readonly TerrainMaterial Air = new SolidMaterial(Blocks.Air);
}
