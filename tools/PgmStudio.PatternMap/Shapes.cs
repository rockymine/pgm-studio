using System.Text.Json;
using PgmStudio.Pgm.Sketch;

namespace PatternMap;

/// <summary>
/// The showcase's twenty-five plateaus, expressed as the shapes an author would actually draw.
///
/// <para>The point of building them this way rather than emitting columns directly is that it can only
/// contain what the sketch tool can contain. A plateau here is a <c>rectangle</c>, a <c>circle</c> or a
/// <c>polygon</c> with a floor and a thickness, which is the whole vocabulary; anything the showcase wants
/// that cannot be said in those terms is a gap in the system rather than a gap in the tool, and building
/// through the rasterizer is what makes the difference visible.</para>
///
/// <para>Every island is <c>mirrors: false</c>. The map is deliberately asymmetric — twenty-five different
/// plateaus, not one fanned across an orbit — which the layout allows and which keeps each plateau showing
/// exactly one thing.</para>
/// </summary>
public static class Shapes
{
    /// <summary>Solid to this Y, so every plateau carries a wall of about this height above the void.</summary>
    public const int Top = 26;

    public const int Pitch = 56;
    private const int Cols = 5;

    public static (int X, int Z) Origin(int index) =>
        ((index % Cols - 2) * Pitch, (index / Cols - 2) * Pitch + Pitch / 2);

    /// <summary>One plateau: the shape an author draws, plus the theme its cells paint through.</summary>
    public static SketchLayout Layout(IReadOnlyList<(string Name, string Kind, string ThemeId)> plateaus,
                                      Dictionary<string, JsonElement> themes, string mapTheme)
    {
        var shapes = new List<SketchShape>();
        var islands = new List<SketchIsland>();

        for (var index = 0; index < plateaus.Count; index++)
        {
            var (_, kind, themeId) = plateaus[index];
            var (ox, oz) = Origin(index);
            var id = $"plateau-{index}";
            shapes.Add(Of(id, kind, ox, oz, themeId));
            islands.Add(new SketchIsland { Id = $"island-{index}", Mirrors = false, ShapeIds = [id] });
        }

        return new SketchLayout
        {
            Setup = new SketchSetup { MirrorMode = "none", Center = new SketchCenter { Cx = 0, Cz = 0 } },
            Layout = new SketchShapes { Shapes = shapes, Islands = islands },
            Themes = themes,
            MapTheme = mapTheme,
        };
    }

    /// <summary>A plateau of the named kind, centred on its grid slot. Floor 1 and a thickness reaching
    /// <see cref="Top"/>, so the column is solid from just above bedrock to the surface players walk on.</summary>
    private static SketchShape Of(string id, string kind, int ox, int oz, string themeId)
    {
        var shape = new SketchShape
        {
            Id = id, Operation = "add", Floor = 1, BaseHeight = Top - 1, Theme = themeId,
        };

        switch (kind)
        {
            case "rectangle":
                shape.Type = "rectangle";
                (shape.MinX, shape.MaxX, shape.MinZ, shape.MaxZ) = (ox - 17, ox + 17, oz - 13, oz + 13);
                break;

            // A circle is a first-class shape, not a polygon approximating one — which is why the round
            // plateau is drawable at all.
            case "circle":
                shape.Type = "circle";
                (shape.CenterX, shape.CenterZ, shape.Radius) = (ox, oz, 14);
                break;

            case "octagon":
                shape.Type = "polygon";
                shape.Vertices = Octagon(ox, oz, 15);
                break;

            case "cross":
                shape.Type = "polygon";
                shape.Vertices = Cross(ox, oz, 15, 5);
                break;

            case "wedge":
                shape.Type = "polygon";
                shape.Vertices =
                [
                    [ox - 16, oz - 15], [ox + 16, oz - 15], [ox + 16, oz + 15], [ox - 9, oz + 15],
                ];
                break;

            default: throw new ArgumentException($"no such plateau kind: {kind}", nameof(kind));
        }
        return shape;
    }

    /// <summary>A regular octagon — every vertex turns 45°, which is exactly a wall frame's default
    /// threshold, so this is the plateau that sits on the boundary of being cornered at all.</summary>
    private static double[][] Octagon(int cx, int cz, double radius)
    {
        var cut = radius * 0.414;                    // the flat that makes eight equal sides
        return
        [
            [cx - cut, cz - radius], [cx + cut, cz - radius],
            [cx + radius, cz - cut], [cx + radius, cz + cut],
            [cx + cut, cz + radius], [cx - cut, cz + radius],
            [cx - radius, cz + cut], [cx - radius, cz - cut],
        ];
    }

    /// <summary>A cross, as one twelve-vertex outline rather than two overlapping rectangles: four outward
    /// right angles and four inward ones, so a frame has to ink both kinds.</summary>
    private static double[][] Cross(int cx, int cz, double arm, double half) =>
    [
        [cx - half, cz - arm], [cx + half, cz - arm], [cx + half, cz - half],
        [cx + arm, cz - half], [cx + arm, cz + half], [cx + half, cz + half],
        [cx + half, cz + arm], [cx - half, cz + arm], [cx - half, cz + half],
        [cx - arm, cz + half], [cx - arm, cz - half], [cx - half, cz - half],
    ];
}
