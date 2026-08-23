using PgmStudio.Geom;
using PgmStudio.Geom.Render;

namespace PgmStudio.Export;

/// <summary>
/// What a walk charges, drawn. The traversability picture answers whether a board joins up; this answers the
/// question underneath it — <em>at what price</em> — which is the half no read could show while every
/// distance was a flat step count.
///
/// <para>One cell per column, shaded by what reaching it costs from a stated start, with the chosen route
/// laid over the top. Two fields, because the walk answers in two units and a picture can only ramp one at a
/// time: <b>blocks</b> is what a player must place to get there — the climb and the bridging, which is the
/// field a kit budget is read against — and <b>distance</b> is how far away it is, which is the field a
/// separation rule is read in. Ground the field never reached is drawn as ground and left unshaded, so
/// unreachable is visibly different from far.</para>
/// </summary>
public static class WalkRender
{
    /// <summary>The fields a caller may ask to see shaded.</summary>
    public static readonly string[] Fields = ["blocks", "distance", "drops"];

    private const int Cell = 3;
    private const int StripHeight = 9;
    private const int Margin = 6;
    private const string Paper = "#14161c";
    private const string Ink = "#edeff2";
    private const string Void = "#14161a";
    private const string Unreached = "#3a4149";
    private const string Bridgeable = "#233043";
    private const string RouteRgb = "#f2c14e";
    private const string StartRgb = "#5ad4a0";
    private const string TargetRgb = "#e8695c";

    /// <param name="Pixels">The finished PNG bytes.</param>
    /// <param name="Reached">How many passable cells the field priced.</param>
    /// <param name="Unreachable">How many it could not.</param>
    /// <param name="Highest">The dearest cell in the field, in the field's own unit.</param>
    public sealed record Result(byte[] Pixels, int Reached, int Unreachable, int Highest);

    /// <summary>The picture. <paramref name="route"/> is drawn over the field where one was asked for.</summary>
    public static Result Png(WalkGround ground, IReadOnlyDictionary<(int X, int Z), WalkCost> field,
        string what, (int X, int Z)? start, (int X, int Z)? target, WalkPath? route, int scale = 2)
    {
        var read = Reader(what);
        var onRoute = route is null ? [] : new HashSet<(int X, int Z)>(route.Cells);
        var highest = field.Count == 0 ? 0 : field.Values.Max(read);
        var unreachable = ground.Passable.Count(cell => !field.ContainsKey(cell));

        var bounds = ground.Bounds;
        int step = Cell * Math.Max(1, scale), width = bounds.Width * step, height = bounds.Height * step;
        var canvas = new byte[width * height * 3];
        Paint(canvas, width, Void, 0, 0, width, height);

        for (var row = 0; row < bounds.Height; row++)
        for (var column = 0; column < bounds.Width; column++)
        {
            var cell = (X: bounds.X + column, Z: bounds.Z + row);
            var hex = cell == start ? StartRgb
                : cell == target ? TargetRgb
                : onRoute.Contains(cell) ? RouteRgb
                : !ground.Passable.Contains(cell) ? null
                : !field.TryGetValue(cell, out var cost)
                    ? (ground.Ground.Contains(cell) ? Unreached : Bridgeable)
                    : Ramp(read(cost), highest, Under(cell, ground));
            if (hex is not null) Paint(canvas, width, hex, column * step, row * step, step, step);
        }

        var withKey = Key(canvas, width, height, what, highest, out var keyed_height);
        var keyed = Legend.AppendBelow(withKey, width, keyed_height,
        [
            new Legend.Entry("the route", Rgb(RouteRgb)),
            new Legend.Entry("from", Rgb(StartRgb)),
            new Legend.Entry("to", Rgb(TargetRgb)),
            new Legend.Entry("never reached", Rgb(Unreached)),
            new Legend.Entry("nothing to stand on", Rgb(Void)),
        ], out var tall,
            $"SCALE: 1 BLOCK = {step} PX - {bounds.Width} X {bounds.Height} BLOCKS");
        return new Result(PngWriter.Encode(width, tall, keyed), field.Count, unreachable, highest);
    }

    /// <summary>The ramp key: three gradient strips, one per footing, under one shared number line. The
    /// picture varies in two ways at once — how dear a cell is, and what a player is standing on to be there —
    /// and a list of swatches can only name one of them, so a reader given one reads the hue as a class and
    /// the class as a hue. Reading across a strip is the price; reading down is the footing.</summary>
    private static byte[] Key(byte[] pixels, int width, int height, string what, int highest, out int keyed)
    {
        var heading = $"HOW DEAR A CELL IS TO REACH - {what.ToUpperInvariant()}";
        var widest = Footings.Select(entry => entry.Name).Append(heading).Max(name => name.Length);
        var scale = Raster.TextWidth(new string('M', widest), 2) + Margin * 2 <= width ? 2 : 1;
        var text = PixelFont.GlyphSize * scale;
        var band = Margin * 2 + text + 6 + Footings.Length * (text + 2 + StripHeight + 7) + text;
        keyed = height + band;

        var canvas = new byte[width * keyed * 3];
        Array.Copy(pixels, canvas, pixels.Length);
        Raster.FillRect(canvas, width, keyed, 0, height, width, band, Rgb(Paper));

        var ramp = Math.Max(16, width - Margin * 2);
        var y = height + Margin;
        Raster.DrawText(canvas, width, keyed, Margin, y, heading, Rgb(Ink), scale);
        y += text + 6;

        foreach (var (footing, name) in Footings)
        {
            Raster.DrawText(canvas, width, keyed, Margin, y, name, Rgb(Ink), scale);
            y += text + 2;
            for (var column = 0; column < ramp; column++)
                Paint(canvas, width, Ramp(column, ramp - 1, footing), Margin + column, y, 1, StripHeight);
            y += StripHeight + 7;
        }

        // One number line under all three strips, because the value axis is the same for every footing —
        // what differs down the key is what a player is standing on, not what a shade is worth.
        Raster.DrawText(canvas, width, keyed, Margin, y - 4, "0", Rgb(Ink), scale);
        var most = highest.ToString();
        Raster.DrawText(canvas, width, keyed, Margin + ramp - Raster.TextWidth(most, scale), y - 4, most,
            Rgb(Ink), scale);
        return canvas;
    }

    /// <summary>One flat rectangle of colour into a raw RGB canvas.</summary>
    private static void Paint(byte[] canvas, int width, string hex, int x, int y, int across, int down)
    {
        var packed = Rgb(hex);
        byte r = (byte)(packed >> 16), g = (byte)(packed >> 8), b = (byte)packed;
        for (var row = y; row < y + down; row++)
        {
            var at = (row * width + x) * 3;
            for (var column = 0; column < across; column++)
            {
                canvas[at++] = r; canvas[at++] = g; canvas[at++] = b;
            }
        }
    }

    /// <summary>Which number of a cost this picture is shading by.</summary>
    private static Func<WalkCost, int> Reader(string what) => what switch
    {
        "distance" => cost => cost.Distance,
        "drops" => cost => cost.Drops,
        _ => cost => cost.Blocks,
    };

    /// <summary>What a player is standing on, which is what decides the cell's hue. The value decides how
    /// far along that hue it is drawn.</summary>
    private enum Footing { Ground, Bridged, Water }

    /// <summary>What each strip of the key stands for, in the order a player meets them: ground costs
    /// nothing to stand on, void costs a block a cell, water costs twice the walk.</summary>
    private static readonly (Footing Footing, string Name)[] Footings =
    [
        (Footing.Ground, "ON GROUND - FREE TO STAND ON"),
        (Footing.Bridged, "OVER VOID - A BLOCK PLACED EACH CELL"),
        (Footing.Water, "IN WATER - TWICE THE DISTANCE AND NO BLOCK"),
    ];

    /// <summary>Which of the three a cell is. Water is read before ground, because a swum cell is ground
    /// whose cost the walk doubles and drawing it as plain ground hides the reason the field slows.</summary>
    private static Footing Under((int X, int Z) cell, WalkGround ground)
        => ground.Water?.Contains(cell) == true ? Footing.Water
            : ground.Ground.Contains(cell) ? Footing.Ground : Footing.Bridged;

    /// <summary>Cheap to dear over one hue per footing, so the ramp reads as a quantity while the hue says
    /// what the cell asks of a player: ground is free, bridged is a block placed, water is twice the walk.
    /// </summary>
    private static string Ramp(int value, int highest, Footing footing)
    {
        var t = highest <= 0 ? 0 : Math.Clamp(value / (double)highest, 0, 1);
        var (r0, g0, b0) = footing switch
        {
            Footing.Bridged => (0x27, 0x4A, 0x6B),
            Footing.Water => (0x14, 0x5C, 0x74),
            _ => (0x2F, 0x6D, 0x4F),
        };
        var (r1, g1, b1) = footing switch
        {
            Footing.Bridged => (0xD8, 0x7C, 0x5A),
            Footing.Water => (0x6F, 0xD8, 0xE8),
            _ => (0xE8, 0xC5, 0x4E),
        };
        return $"#{Mix(r0, r1, t):x2}{Mix(g0, g1, t):x2}{Mix(b0, b1, t):x2}";
    }

    private static int Mix(int from, int to, double t) => (int)Math.Round(from + (to - from) * t);

    private static int Rgb(string hex) => Convert.ToInt32(hex.TrimStart('#'), 16);
}
