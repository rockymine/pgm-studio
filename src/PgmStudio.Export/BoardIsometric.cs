using System.Globalization;
using System.Text;
using PgmStudio.Geom.Render;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Palette;

namespace PgmStudio.Export;

/// <summary>
/// A built board drawn in the round: from above at 2:1 as cubes painted back to front, and as an x-ray that
/// washes out the ground and the buildings between the camera and a roofed room — with the void scan beside it, what
/// covered space the board holds, how big, and which of it nothing can walk into. Drawn off the world block by
/// block in the colour the 3-D preview gives it, so what is drawn is what was built.
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
    public sealed record Cavity(int Cells, (int X, int Y, int Z) Min, (int X, int Y, int Z) Max, bool Sealed);

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

    // The veil keeps almost none of a block's chroma — a hillside drawn over a room at a sixth of its opacity
    // still tints the room its own green — and the mass around a room is pulled most of the way to grey, so the
    // room's own colours are the only chroma in the frame.
    private const double VeilChroma = 0.9, VeilOpacity = 0.15, Calm = 0.6;

    private const int BackgroundRgb = 0xF7F7F4, CaptionRgb = 0x6E6E76, Margin = 30, Foot = 26, CaptionPixel = 2;

    /// <summary>The board from above, seen from the <paramref name="quarter"/>th of <see cref="Corners"/>, each
    /// cube <paramref name="scale"/> pixels across half its width. Null where the world holds no block.</summary>
    public static byte[]? Isometric(BuiltWorld built, int quarter, int scale, string name)
    {
        var blocks = Blocks(built, quarter, kept: null);
        if (blocks.Count == 0) return null;
        var shown = blocks.Where(block => Shows(blocks, block.Key)).ToDictionary();
        return Paint(shown, [], scale, $"{name} - isometric from the {Corners[Quarter(quarter)]}");
    }

    /// <summary>The board with whatever hides a roofed room taken down to a wash, so the room is in the picture,
    /// and the voids it found. Every block of ground or building on the line of sight out of a roofed void is
    /// the veil, drawn thin and near grey, one skin deep; every block with a face onto a void is its lining,
    /// drawn opaque in its own colour; everything else is drawn opaque and calmed toward grey. A made thing and
    /// a placed prop are never veiled: the sight line alone cannot tell a lamp from a lid, and one standing in
    /// a room is the room's subject. On a board with nothing roofed the veil is empty and the picture is the
    /// isometric. Null where the world holds no block.</summary>
    public static (byte[] Png, IReadOnlyList<Cavity> Cavities)? XRay(BuiltWorld built, int quarter, int scale, string name)
    {
        var kept = new HashSet<(int X, int Y, int Z)>();
        var blocks = Blocks(built, quarter, kept);
        if (blocks.Count == 0) return null;
        var found = Voids(blocks, quarter);
        var air = found.SelectMany(entry => entry.Air).ToHashSet();

        var lining = new HashSet<(int X, int Y, int Z)>();
        foreach (var (x, y, z) in air)
            foreach (var cell in Around(x, y, z))
                if (blocks.ContainsKey(cell)) lining.Add(cell);
        var hidden = Sightline(blocks, air);
        hidden.ExceptWith(kept);

        var solid = new Dictionary<(int X, int Y, int Z), int>();
        var veiled = new Dictionary<(int X, int Y, int Z), int>();
        foreach (var (cell, rgb) in blocks)
        {
            if (hidden.Contains(cell)) veiled[cell] = Desaturate(rgb, VeilChroma);
            else solid[cell] = lining.Contains(cell) ? rgb : Desaturate(rgb, Calm);
        }

        // The opaque pass is culled against the opaque set alone, since a block hidden only by veiled mass now
        // shows; the veil is culled against itself, which leaves one skin rather than washes stacked opaque.
        var shown = solid.Where(block => Shows(solid, block.Key)).ToDictionary();
        var skin = veiled.Where(block => Shows(veiled, block.Key)).ToDictionary();
        foreach (var (cell, rgb) in skin) shown[cell] = rgb;

        var cavities = found.Select(entry => entry.Cavity).ToList();
        var caption = $"{name} - x-ray from the {Corners[Quarter(quarter)]} - {Summary(cavities)}";
        return (Paint(shown, [.. skin.Keys], scale, caption), cavities);
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
        text.Append("cells   x range          y range      z range          reached\n");
        foreach (var cavity in cavities)
            text.Append(CultureInfo.InvariantCulture,
                $"{cavity.Cells,5}   {cavity.Min.X,5}..{cavity.Max.X,-6}  {cavity.Min.Y,4}..{cavity.Max.Y,-5}  "
                + $"{cavity.Min.Z,5}..{cavity.Max.Z,-6}  {(cavity.Sealed ? "SEALED - nothing walks in" : "open")}\n");
        return text.ToString();
    }

    /// <summary>Every roofed void of the board, largest first, and how many blocks it holds.</summary>
    public static (IReadOnlyList<Cavity> Cavities, int Blocks) Scan(BuiltWorld built)
    {
        var blocks = Blocks(built, 0, kept: null);
        return blocks.Count == 0 ? ([], 0) : ([.. Voids(blocks, 0).Select(entry => entry.Cavity)], blocks.Count);
    }

    /// <summary>The voids in a caption's words — the pixel font has no comma, and the bounds are the text
    /// twin's to give.</summary>
    private static string Summary(IReadOnlyList<Cavity> cavities) => cavities.Count == 0
        ? "nothing roofed"
        : string.Create(CultureInfo.InvariantCulture,
            $"{cavities.Count} roofed voids - {cavities.Count(cavity => cavity.Sealed)} sealed");

    // ── the blocks ─────────────────────────────────────────────────────────────────

    /// <summary>Every block the world holds, in its colour, turned <paramref name="quarter"/> quarters. Where
    /// <paramref name="kept"/> is given it collects every block of a made thing — a run a made layer drew — and
    /// of a placed prop: a run no layer drew, in a column a prop claimed last. A run no layer drew anywhere
    /// else is a building's.</summary>
    private static Dictionary<(int X, int Y, int Z), int> Blocks(
        BuiltWorld built, int quarter, HashSet<(int X, int Y, int Z)>? kept)
    {
        var made = built.MadeLayers ?? new HashSet<string>();
        var blocks = new Dictionary<(int X, int Y, int Z), int>();
        foreach (var (x, z, runs) in WorldColumns.Attributed(built.World, kept is null ? null : built.Columns))
        {
            var biome = built.World.GetBiome(x, z);
            var (turnedX, turnedZ) = Turned(x, z, quarter);
            foreach (var (run, layer) in runs)
            {
                var rgb = BlockPalette.PackedRgbIn(run.BlockId, run.BlockData, biome, x, z);
                var keep = kept is not null && (layer is null
                    ? built.Provenance.PassAt(x, z) is ProvenancePass.Prop
                    : made.Contains(layer));
                for (var y = run.YBottom; y <= run.YTop; y++)
                {
                    blocks[(turnedX, y, turnedZ)] = rgb;
                    if (keep) kept!.Add((turnedX, y, turnedZ));
                }
            }
        }
        return blocks;
    }

    private static int Quarter(int quarter) => ((quarter % 4) + 4) % 4;

    private static (int X, int Z) Turned(int x, int z, int quarter)
    {
        for (var turn = 0; turn < Quarter(quarter); turn++) (x, z) = (-z, x);
        return (x, z);
    }

    /// <summary>The world's own coordinates of a turned cell.</summary>
    private static (int X, int Y, int Z) Untouched((int X, int Y, int Z) cell, int quarter)
    {
        var (x, z) = (cell.X, cell.Z);
        for (var turn = 0; turn < Quarter(quarter); turn++) (x, z) = (z, -x);
        return (x, cell.Y, z);
    }

    /// <summary>Whether a block shows the camera anything: one of its three camera-facing neighbours is missing.</summary>
    private static bool Shows<T>(Dictionary<(int X, int Y, int Z), T> blocks, (int X, int Y, int Z) cell) =>
        !blocks.ContainsKey((cell.X + 1, cell.Y, cell.Z)) || !blocks.ContainsKey((cell.X, cell.Y + 1, cell.Z))
        || !blocks.ContainsKey((cell.X, cell.Y, cell.Z + 1));

    private static (int X, int Y, int Z)[] Around(int x, int y, int z) =>
        [(x + 1, y, z), (x - 1, y, z), (x, y + 1, z), (x, y - 1, z), (x, y, z + 1), (x, y, z - 1)];

    // ── painting ───────────────────────────────────────────────────────────────────

    /// <summary>The blocks painted back to front with a caption under them. Depth along the camera is x + y + z,
    /// so ascending order draws the near ones last and no depth buffer is needed. <paramref name="veiled"/>
    /// names the blocks painted at the veil's opacity over what is already down.</summary>
    private static byte[] Paint(Dictionary<(int X, int Y, int Z), int> shown, HashSet<(int X, int Y, int Z)> veiled,
                                int scale, string caption)
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
            var opacity = veiled.Contains(cell) ? VeilOpacity : 1.0;
            foreach (var (light, spans) in faces)
            {
                var face = Shade(shown[cell], light);
                foreach (var (row, left, right) in spans)
                    for (var column = left; column < right; column++)
                    {
                        int pixelX = anchorX + column, pixelY = anchorY + row;
                        if (pixelX < 0 || pixelY < 0 || pixelX >= width || pixelY >= height) continue;
                        if (opacity >= 1) Raster.Set(pixels, width, pixelX, pixelY, face);
                        else Raster.Over(pixels, width, pixelX, pixelY, face, opacity);
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

    /// <summary>A colour pulled toward its own grey: 0 leaves it, 1 flattens it to its luminance.</summary>
    private static int Desaturate(int rgb, double amount)
    {
        int red = (rgb >> 16) & 0xFF, green = (rgb >> 8) & 0xFF, blue = rgb & 0xFF;
        var grey = 0.299 * red + 0.587 * green + 0.114 * blue;
        int Pulled(int channel) => Math.Clamp((int)(channel + (grey - channel) * amount), 0, 255);
        return (Pulled(red) << 16) | (Pulled(green) << 8) | Pulled(blue);
    }

    // ── the void scan ──────────────────────────────────────────────────────────────

    /// <summary>Every roofed void in the blocks, largest first, with the air it holds. A void is air with solid
    /// over it in its own column — the plain meaning of underground, and the only test that finds a room without
    /// being told where to look. Each column is taken one run of air at a time between its own lowest block and
    /// its highest, so a run too tall to be a room is dropped without dropping the room in the same column. A
    /// void that no air reaching the open sky reaches is sealed; the sky is flooded from the shell of air padded
    /// a block round the board.</summary>
    private static List<(Cavity Cavity, HashSet<(int X, int Y, int Z)> Air)> Voids(
        Dictionary<(int X, int Y, int Z), int> blocks, int quarter)
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

            var air = held.Select(index =>
            {
                var (rest, x) = Math.DivRem(index, spanX);
                var (y, z) = Math.DivRem(rest, spanZ);
                return ((int)x + lowX, (int)y + lowY, (int)z + lowZ);
            }).ToHashSet();
            var world = air.Select(cell => Untouched(cell, quarter)).ToList();
            found.Add((new Cavity(air.Count,
                (world.Min(cell => cell.X), world.Min(cell => cell.Y), world.Min(cell => cell.Z)),
                (world.Max(cell => cell.X), world.Max(cell => cell.Y), world.Max(cell => cell.Z)),
                !held.Any(index => sky[index] == 1)), air));
        }
        // Stable, so two voids of one size keep the order the scan met them in and a world reads back the
        // same way twice.
        return [.. found.OrderByDescending(entry => entry.Cavity.Cells)];
    }

    /// <summary>The solid blocks standing between the camera and a roofed void. The line of sight out of a block
    /// is the diagonal (+1, +1, +1), every block on one line shares <c>(x − y, z − y)</c>, and one void cell opens
    /// its whole line — so only the lowest void on each line is found, and everything solid above it on that line
    /// is what the world hides the room with.</summary>
    private static HashSet<(int X, int Y, int Z)> Sightline(
        Dictionary<(int X, int Y, int Z), int> blocks, HashSet<(int X, int Y, int Z)> air)
    {
        var hidden = new HashSet<(int X, int Y, int Z)>();
        if (air.Count == 0) return hidden;
        var top = blocks.Keys.Max(cell => cell.Y);
        var lowest = new Dictionary<(int Across, int Into), int>();
        foreach (var (x, y, z) in air)
            if (!lowest.TryGetValue((x - y, z - y), out var floor) || y < floor) lowest[(x - y, z - y)] = y;
        foreach (var ((across, into), floor) in lowest)
            for (var y = floor + 1; y <= top; y++)
                if (blocks.ContainsKey((across + y, y, into + y))) hidden.Add((across + y, y, into + y));
        return hidden;
    }
}
