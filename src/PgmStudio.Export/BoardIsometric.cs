using System.Globalization;
using System.Text;
using PgmStudio.Geom;
using PgmStudio.Geom.Render;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Palette;

namespace PgmStudio.Export;

/// <summary>
/// A built board drawn from above at 2:1 as cubes painted back to front, from one of its four corners: the
/// isometric, every block; the x-ray, only the blocks that line a void the ground roofs, with its ceiling lifted
/// off so the underground is seen into from above; and the void scan, what covered space the board holds, how big, and which
/// of it nothing can walk into. Drawn off the world block by block in the colour the 3-D preview gives it, so what
/// is drawn is what was built. Both pictures take a box, and draw only what is inside it.
///
/// <para>The camera sits at (+∞, +∞, +∞) and never moves; turning the board a quarter at a time about the
/// vertical is what turns the view, so one projection looks from all four corners.</para>
/// </summary>
public static class BoardIsometric
{
    /// <summary>One roofed void: air with solid over it in its own column, and the air that runs into it.</summary>
    /// <param name="Cells">How many blocks of air it holds.</param>
    /// <param name="Min">Its least corner, in the world's own blocks.</param>
    /// <param name="Max">Its greatest corner.</param>
    /// <param name="Sealed">Whether no air that reaches the open sky reaches it — a space nothing can walk
    /// into, which on a board that meant to build a room is a finding.</param>
    /// <param name="Underground">Whether the ground roofs it: a block a sketch layer laid, not a made thing,
    /// stands over some of its air. A building's rooms and the shade under a crown are not; on a world with no
    /// layer attribution every void is.</param>
    public sealed record Cavity(int Cells, (int X, int Y, int Z) Min, (int X, int Y, int Z) Max, bool Sealed,
                                bool Underground);

    /// <summary>The smallest roofed void worth naming, in blocks of air.</summary>
    public const int SmallestCavity = 6;

    /// <summary>The tallest run of air a room is. Past it the air is what a cloud, a sky-written letter or an
    /// observer platform roofs, which covers everything under it without being a ceiling.</summary>
    public const int TallestRoom = 24;

    /// <summary>The corners the camera stands at, in the order a quarter turn of the board takes it round —
    /// the south-east, over +x and +z, first.</summary>
    public static readonly IReadOnlyList<string> Corners = ["south-east", "north-east", "north-west", "south-west"];

    // A face keeps the block's own colour on top and falls away on the two flanks, rather than the top being
    // lit: a sculpture in quartz is near white already, and lifting its top flattens it into a silhouette.
    private const double TopLight = 1.0, RightLight = 0.78, LeftLight = 0.55;

    private const int BackgroundRgb = 0xF7F7F4, CaptionRgb = 0x6E6E76, Margin = 30, Foot = 26, CaptionPixel = 2;

    /// <summary>The board from above, seen from the <paramref name="quarter"/>th of <see cref="Corners"/>, each
    /// cube <paramref name="scale"/> pixels across half its width, and only what lies in <paramref name="within"/>
    /// where one is given. Null where that holds no block.</summary>
    public static byte[]? Isometric(BuiltWorld built, int quarter, int scale, string name, BlockBox? within = null)
    {
        var blocks = Turn(Blocks(built, within), quarter);
        if (blocks.Count == 0) return null;
        var shown = blocks.Where(block => Shows(blocks, block.Key)).ToDictionary();
        return Paint(shown, scale, $"{name} - isometric from the {Corners[Quarter(quarter)]}{Framed(within)}");
    }

    /// <summary>The board's underground: what lines every <see cref="Cavity.Underground"/> void — a tunnel, a
    /// crypt or a cave, not a house's rooms or the shade under a crown. Every block with a face onto such a void
    /// is drawn, less its ceiling — a block with the void's air under it and no void air over it — so each is
    /// seen as its floor, its walls and whatever stands in it, open to the camera. The voids are found over the
    /// whole world and only the drawing takes
    /// <paramref name="within"/>, so a void the box cuts is still the void it is; the cavities answered are all
    /// of them, as <see cref="Scan"/> answers. Null where the board, or the box, holds no lining.</summary>
    public static (byte[] Png, IReadOnlyList<Cavity> Cavities)? XRay(
        BuiltWorld built, int quarter, int scale, string name, BlockBox? within = null)
    {
        var (blocks, found) = Found(built);
        if (blocks.Count == 0) return null;
        var air = found.Where(entry => entry.Cavity.Underground).SelectMany(entry => entry.Air).ToHashSet();

        var lining = new Dictionary<(int X, int Y, int Z), int>();
        foreach (var (x, y, z) in air)
            foreach (var cell in Around(x, y, z))
                if (blocks.TryGetValue(cell, out var rgb)
                    && (within is not { } box || box.Contains(cell.X, cell.Y, cell.Z))
                    && !(air.Contains((cell.X, cell.Y - 1, cell.Z)) && !air.Contains((cell.X, cell.Y + 1, cell.Z))))
                    lining[cell] = rgb;
        if (lining.Count == 0) return null;

        var turned = Turn(lining, quarter);
        var shown = turned.Where(block => Shows(turned, block.Key)).ToDictionary();
        var cavities = found.Select(entry => entry.Cavity).ToList();
        var caption = $"{name} - x-ray from the {Corners[Quarter(quarter)]}{Framed(within)} - {Summary(cavities)}";
        return (Paint(shown, scale, caption), cavities);
    }

    /// <summary>The void scan as text: every roofed void the board holds, largest first, with its size, its
    /// bounds and whether anything can walk into it.</summary>
    public static string VoidText(IReadOnlyList<Cavity> cavities, int blocks)
    {
        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture,
            $"roofed voids  {cavities.Count}  ({cavities.Count(cavity => cavity.Sealed)} sealed)  of {blocks} blocks\n");
        text.Append(CultureInfo.InvariantCulture,
            $"a void is air with solid over it in its own column, {SmallestCavity} blocks or more, no run taller than {TallestRoom}\n\n");
        if (cavities.Count == 0) return text.Append("nothing roofed\n").ToString();
        text.Append("cells   x range          y range      z range          roofed by  reached\n");
        foreach (var cavity in cavities)
            text.Append(CultureInfo.InvariantCulture,
                $"{cavity.Cells,5}   {cavity.Min.X,5}..{cavity.Max.X,-6}  {cavity.Min.Y,4}..{cavity.Max.Y,-5}  "
                + $"{cavity.Min.Z,5}..{cavity.Max.Z,-6}  {(cavity.Underground ? "ground   " : "a build  ")}  "
                + $"{(cavity.Sealed ? "SEALED - nothing walks in" : "open")}\n");
        return text.ToString();
    }

    /// <summary>Every roofed void of the board, largest first, and how many blocks it holds.</summary>
    public static (IReadOnlyList<Cavity> Cavities, int Blocks) Scan(BuiltWorld built)
    {
        var (blocks, found) = Found(built);
        return ([.. found.Select(entry => entry.Cavity)], blocks.Count);
    }

    /// <summary>The whole world's blocks and every roofed void in them, each told whether the ground roofs it.</summary>
    private static (Dictionary<(int X, int Y, int Z), int> Blocks,
                    List<(Cavity Cavity, HashSet<(int X, int Y, int Z)> Air)> Found) Found(BuiltWorld built)
    {
        var ground = built.Columns is null ? null : new HashSet<(int X, int Y, int Z)>();
        var blocks = Blocks(built, within: null, ground);
        return (blocks, blocks.Count == 0 ? [] : Voids(blocks, ground));
    }

    /// <summary>The voids in a caption's words — the pixel font has no comma, and the bounds are the text
    /// twin's to give.</summary>
    private static string Summary(IReadOnlyList<Cavity> cavities) => cavities.Count == 0
        ? "nothing roofed"
        : string.Create(CultureInfo.InvariantCulture,
            $"{cavities.Count} roofed voids - {cavities.Count(cavity => cavity.Sealed)} sealed");

    /// <summary>The box a picture was framed to, in a caption's words; nothing for the whole board.</summary>
    private static string Framed(BlockBox? within) => within is not { } box
        ? ""
        : string.Create(CultureInfo.InvariantCulture,
            $" - x {box.MinX}..{box.MaxX} y {box.MinY}..{box.MaxY} z {box.MinZ}..{box.MaxZ}");

    // ── the blocks ─────────────────────────────────────────────────────────────────

    /// <summary>Every block the world holds inside <paramref name="within"/>, or all of them, in its colour and
    /// at the world's own coordinates. Where <paramref name="ground"/> is given it collects every block of the
    /// ground: a run a sketch layer laid that is not a made thing.</summary>
    private static Dictionary<(int X, int Y, int Z), int> Blocks(
        BuiltWorld built, BlockBox? within, HashSet<(int X, int Y, int Z)>? ground = null)
    {
        var made = built.MadeLayers ?? new HashSet<string>();
        var blocks = new Dictionary<(int X, int Y, int Z), int>();
        foreach (var (x, z, runs) in WorldColumns.Attributed(built.World, ground is null ? null : built.Columns, within))
        {
            var biome = built.World.GetBiome(x, z);
            foreach (var (run, layer) in runs)
            {
                var rgb = BlockPalette.PackedRgbIn(run.BlockId, run.BlockData, biome, x, z);
                var laid = ground is not null && layer is not null && !made.Contains(layer);
                for (var y = run.YBottom; y <= run.YTop; y++)
                {
                    blocks[(x, y, z)] = rgb;
                    if (laid) ground!.Add((x, y, z));
                }
            }
        }
        return blocks;
    }

    private static int Quarter(int quarter) => ((quarter % 4) + 4) % 4;

    /// <summary>The blocks turned <paramref name="quarter"/> quarters about the vertical, which is what stands
    /// the camera at that corner.</summary>
    private static Dictionary<(int X, int Y, int Z), int> Turn(Dictionary<(int X, int Y, int Z), int> blocks, int quarter)
    {
        var turned = new Dictionary<(int X, int Y, int Z), int>(blocks.Count);
        foreach (var ((x, y, z), rgb) in blocks)
        {
            var (turnedX, turnedZ) = (x, z);
            for (var turn = 0; turn < Quarter(quarter); turn++) (turnedX, turnedZ) = (-turnedZ, turnedX);
            turned[(turnedX, y, turnedZ)] = rgb;
        }
        return turned;
    }

    /// <summary>Whether a block shows the camera anything: one of its three camera-facing neighbours is missing.</summary>
    private static bool Shows<T>(Dictionary<(int X, int Y, int Z), T> blocks, (int X, int Y, int Z) cell) =>
        !blocks.ContainsKey((cell.X + 1, cell.Y, cell.Z)) || !blocks.ContainsKey((cell.X, cell.Y + 1, cell.Z))
        || !blocks.ContainsKey((cell.X, cell.Y, cell.Z + 1));

    private static (int X, int Y, int Z)[] Around(int x, int y, int z) =>
        [(x + 1, y, z), (x - 1, y, z), (x, y + 1, z), (x, y - 1, z), (x, y, z + 1), (x, y, z - 1)];

    // ── painting ───────────────────────────────────────────────────────────────────

    /// <summary>The blocks painted back to front with a caption under them. Depth along the camera is x + y + z,
    /// so ascending order draws the near ones last and no depth buffer is needed.</summary>
    private static byte[] Paint(Dictionary<(int X, int Y, int Z), int> shown, int scale, string caption)
    {
        int across = scale, down = Math.Max(1, scale / 2), up = scale;
        int leftmost = int.MaxValue, rightmost = int.MinValue, topmost = int.MaxValue, bottommost = int.MinValue;
        foreach (var (x, y, z) in shown.Keys)
        {
            leftmost = Math.Min(leftmost, (x - z) * across);
            rightmost = Math.Max(rightmost, (x - z) * across);
            topmost = Math.Min(topmost, (x + z) * down - y * up);
            bottommost = Math.Max(bottommost, (x + z) * down - y * up);
        }
        (leftmost, rightmost, topmost, bottommost) = (leftmost - across, rightmost + across, topmost - up, bottommost + 2 * down);

        // A caption wider than the drawing is set at half size rather than stretching the picture round it.
        var drawn = rightmost - leftmost + Margin * 2;
        var captionPixel = Raster.TextWidth(caption, CaptionPixel) + Margin * 2 <= drawn ? CaptionPixel : 1;
        var width = Math.Max(drawn, Margin * 2 + Raster.TextWidth(caption, captionPixel));
        var height = bottommost - topmost + Margin * 2 + Foot;
        var pixels = new byte[width * height * 3];
        Raster.FillRect(pixels, width, height, 0, 0, width, height, BackgroundRgb);

        (double Light, List<(int Row, int Left, int Right)> Spans)[] faces =
        [
            (TopLight, Spans([(0, -up), (across, down - up), (0, 2 * down - up), (-across, down - up)])),
            (RightLight, Spans([(across, down - up), (across, down), (0, 2 * down), (0, 2 * down - up)])),
            (LeftLight, Spans([(-across, down - up), (-across, down), (0, 2 * down), (0, 2 * down - up)])),
        ];
        int originX = Margin - leftmost, originY = Margin - topmost;
        foreach (var cell in shown.Keys.OrderBy(cell => cell.X + cell.Y + cell.Z))
        {
            var anchorX = originX + (cell.X - cell.Z) * across;
            var anchorY = originY + (cell.X + cell.Z) * down - cell.Y * up;
            foreach (var (light, spans) in faces)
            {
                var face = Shade(shown[cell], light);
                foreach (var (row, left, right) in spans)
                    for (var column = left; column < right; column++)
                    {
                        int pixelX = anchorX + column, pixelY = anchorY + row;
                        if (pixelX < 0 || pixelY < 0 || pixelX >= width || pixelY >= height) continue;
                        Raster.Set(pixels, width, pixelX, pixelY, face);
                    }
            }
        }
        Raster.DrawText(pixels, width, height, Margin, height - Foot + 6, caption, CaptionRgb, captionPixel);
        return PngWriter.Encode(width, height, pixels);
    }

    /// <summary>The rows and runs a polygon fills, relative to its own origin, the right end exclusive — a cube
    /// face is filled once here and stamped at every cube.</summary>
    private static List<(int Row, int Left, int Right)> Spans((int X, int Y)[] points)
    {
        var spans = new List<(int Row, int Left, int Right)>();
        int lowest = points.Min(point => point.Y), highest = points.Max(point => point.Y);
        for (var row = lowest; row <= highest + 1; row++)
        {
            var centre = row + 0.5;
            var crossings = new List<double>();
            for (var index = 0; index < points.Length; index++)
            {
                var (fromX, fromY) = points[index];
                var (toX, toY) = points[(index + 1) % points.Length];
                if ((fromY <= centre && centre < toY) || (toY <= centre && centre < fromY))
                    crossings.Add(fromX + (centre - fromY) * (toX - fromX) / (toY - fromY));
            }
            crossings.Sort();
            for (var index = 0; index + 1 < crossings.Count; index += 2)
            {
                int left = (int)Math.Floor(crossings[index] + 0.5), right = (int)Math.Floor(crossings[index + 1] + 0.5);
                if (right > left) spans.Add((row, left, right));
            }
        }
        return spans;
    }

    private static int Shade(int rgb, double light) =>
        (Channel(rgb >> 16, light) << 16) | (Channel(rgb >> 8, light) << 8) | Channel(rgb, light);

    private static int Channel(int value, double light) => Math.Clamp((int)((value & 0xFF) * light), 0, 255);

    // ── the void scan ──────────────────────────────────────────────────────────────

    /// <summary>Every roofed void in the blocks, largest first, with the air it holds. A void is air with solid
    /// over it in its own column — the plain meaning of underground, and the only test that finds a room without
    /// being told where to look. Each column is taken one run of air at a time between its own lowest block and
    /// its highest, so a run too tall to be a room is dropped without dropping the room in the same column. A
    /// void that no air reaching the open sky reaches is sealed; the sky is flooded from the shell of air padded
    /// a block round the board.</summary>
    private static List<(Cavity Cavity, HashSet<(int X, int Y, int Z)> Air)> Voids(
        Dictionary<(int X, int Y, int Z), int> blocks, HashSet<(int X, int Y, int Z)>? ground)
    {
        var columns = new Dictionary<(int X, int Z), List<int>>();
        foreach (var (x, y, z) in blocks.Keys)
        {
            if (!columns.TryGetValue((x, z), out var heights)) columns[(x, z)] = heights = [];
            heights.Add(y);
        }
        foreach (var heights in columns.Values) heights.Sort();

        int lowX = columns.Keys.Min(cell => cell.X) - 1, lowZ = columns.Keys.Min(cell => cell.Z) - 1;
        var lowY = columns.Values.Min(heights => heights[0]) - 1;
        int spanX = columns.Keys.Max(cell => cell.X) - lowX + 2, spanZ = columns.Keys.Max(cell => cell.Z) - lowZ + 2;
        var spanY = columns.Values.Max(heights => heights[^1]) - lowY + 2;
        long plane = (long)spanX * spanZ, size = plane * spanY;

        const byte Solid = 1, Roofed = 2, Held = 3;
        var state = new byte[size];
        var sky = new byte[size];
        Array.Fill(sky, (byte)1);
        var hollow = new List<long>();
        var roofed = new List<long>();
        foreach (var ((x, z), heights) in columns)
        {
            var column = (long)(z - lowZ) * spanX + (x - lowX);
            int low = heights[0], high = heights[^1];
            for (var y = low; y <= high; y++) sky[(y - lowY) * plane + column] = 0;
            foreach (var y in heights) state[(y - lowY) * plane + column] = Solid;
            for (var index = 0; index + 1 < heights.Count; index++)
            {
                int below = heights[index], above = heights[index + 1];
                if (above - below == 1) continue;
                var room = above - below - 1 <= TallestRoom;
                for (var y = below + 1; y < above; y++)
                {
                    var at = (y - lowY) * plane + column;
                    hollow.Add(at);
                    if (!room) continue;
                    roofed.Add(at);
                    state[at] = Roofed;
                }
            }
        }

        long[] steps = [1, -1, spanX, -spanX, plane, -plane];
        var flooding = new Stack<long>(hollow.Where(index => steps.Any(step => sky[index + step] == 1)).ToList());
        foreach (var index in flooding) sky[index] = 1;
        while (flooding.Count > 0)
        {
            var index = flooding.Pop();
            foreach (var step in steps)
            {
                var ahead = index + step;
                if (sky[ahead] == 0 && state[ahead] != Solid)
                {
                    sky[ahead] = 1;
                    flooding.Push(ahead);
                }
            }
        }

        var found = new List<(Cavity Cavity, HashSet<(int X, int Y, int Z)> Air)>();
        roofed.Sort();
        foreach (var start in roofed)
        {
            if (state[start] != Roofed) continue;
            state[start] = Held;
            var pending = new Stack<long>([start]);
            var held = new List<long>();
            while (pending.Count > 0)
            {
                var index = pending.Pop();
                held.Add(index);
                foreach (var step in steps)
                {
                    var ahead = index + step;
                    if (ahead < 0 || ahead >= size || state[ahead] != Roofed) continue;
                    state[ahead] = Held;
                    pending.Push(ahead);
                }
            }
            if (held.Count < SmallestCavity) continue;

            var air = held.Select((int X, int Y, int Z) (index) =>
            {
                var (rest, x) = Math.DivRem(index, spanX);
                var (y, z) = Math.DivRem(rest, spanZ);
                return ((int)x + lowX, (int)y + lowY, (int)z + lowZ);
            }).ToHashSet();
            found.Add((new Cavity(air.Count,
                (air.Min(cell => cell.X), air.Min(cell => cell.Y), air.Min(cell => cell.Z)),
                (air.Max(cell => cell.X), air.Max(cell => cell.Y), air.Max(cell => cell.Z)),
                !held.Any(index => sky[index] == 1),
                ground is null || air.Any(cell => ground.Contains((cell.X, cell.Y + 1, cell.Z)))), air));
        }
        // Stable, so two voids of one size keep the order the scan met them in and a world reads back the
        // same way twice.
        return [.. found.OrderByDescending(entry => entry.Cavity.Cells)];
    }
}
