using System.IO.Compression;

namespace PgmStudio.Relief;

/// <summary>A 24-bit RGB pixel buffer with the drawing operations the relief figures need: block fills,
/// alpha mixing, convex-quad scanline fill (the isometric cube faces) and integer upscaling.</summary>
internal sealed class Canvas
{
    public readonly int Width, Height;
    private readonly byte[] _pixels;

    public Canvas(int width, int height, int background)
    {
        Width = width; Height = height;
        _pixels = new byte[width * height * 3];
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++) Set(x, y, background);
    }

    public byte[] Pixels => _pixels;

    public void Set(int x, int y, int rgb)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height) return;
        var at = (y * Width + x) * 3;
        _pixels[at] = (byte)((rgb >> 16) & 0xFF);
        _pixels[at + 1] = (byte)((rgb >> 8) & 0xFF);
        _pixels[at + 2] = (byte)(rgb & 0xFF);
    }

    public int Get(int x, int y)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height) return 0;
        var at = (y * Width + x) * 3;
        return (_pixels[at] << 16) | (_pixels[at + 1] << 8) | _pixels[at + 2];
    }

    public void Over(int x, int y, int rgb, double weight)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height || weight <= 0) return;
        var at = (y * Width + x) * 3;
        _pixels[at] = Mix(_pixels[at], (rgb >> 16) & 0xFF, weight);
        _pixels[at + 1] = Mix(_pixels[at + 1], (rgb >> 8) & 0xFF, weight);
        _pixels[at + 2] = Mix(_pixels[at + 2], rgb & 0xFF, weight);
    }

    public void FillRect(int x, int y, int width, int height, int rgb)
    {
        for (var row = y; row < y + height; row++)
            for (var col = x; col < x + width; col++) Set(col, row, rgb);
    }

    /// <summary>Fills a convex polygon by scanline — the isometric cube faces and the mark overlays.</summary>
    public void FillPolygon((double X, double Y)[] points, int rgb)
    {
        if (points.Length < 3) return;
        double top = points.Min(p => p.Y), bottom = points.Max(p => p.Y);
        int first = Math.Max(0, (int)Math.Floor(top)), last = Math.Min(Height - 1, (int)Math.Ceiling(bottom));
        for (var row = first; row <= last; row++)
        {
            double centre = row + 0.5, left = double.MaxValue, right = double.MinValue;
            for (var i = 0; i < points.Length; i++)
            {
                var (ax, ay) = points[i];
                var (bx, by) = points[(i + 1) % points.Length];
                if (ay == by) continue;
                if (centre < Math.Min(ay, by) || centre >= Math.Max(ay, by)) continue;
                var crossing = ax + (centre - ay) / (by - ay) * (bx - ax);
                left = Math.Min(left, crossing); right = Math.Max(right, crossing);
            }
            if (left > right) continue;
            for (var col = Math.Max(0, (int)Math.Round(left)); col <= Math.Min(Width - 1, (int)Math.Round(right) - 1); col++)
                Set(col, row, rgb);
        }
    }

    /// <summary>A one-pixel line, Bresenham — the mark strokes and the section rules.</summary>
    public void Line(int x0, int y0, int x1, int y1, int rgb, double weight = 1.0)
    {
        int dx = Math.Abs(x1 - x0), dy = -Math.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1, error = dx + dy;
        while (true)
        {
            Over(x0, y0, rgb, weight);
            if (x0 == x1 && y0 == y1) break;
            var doubled = 2 * error;
            if (doubled >= dy) { error += dy; x0 += sx; }
            if (doubled <= dx) { error += dx; y0 += sy; }
        }
    }

    public void Disc(double cx, double cy, double radius, int rgb, double weight = 1.0)
    {
        for (var row = (int)(cy - radius) - 1; row <= cy + radius + 1; row++)
            for (var col = (int)(cx - radius) - 1; col <= cx + radius + 1; col++)
            {
                double dx = col + 0.5 - cx, dy = row + 0.5 - cy;
                if (dx * dx + dy * dy <= radius * radius) Over(col, row, rgb, weight);
            }
    }

    public void Ring(double cx, double cy, double radius, double thickness, int rgb)
    {
        double outer = radius + thickness / 2, inner = radius - thickness / 2;
        for (var row = (int)(cy - outer) - 1; row <= cy + outer + 1; row++)
            for (var col = (int)(cx - outer) - 1; col <= cx + outer + 1; col++)
            {
                double dx = col + 0.5 - cx, dy = row + 0.5 - cy;
                var distance = Math.Sqrt(dx * dx + dy * dy);
                if (distance <= outer && distance >= inner) Set(col, row, rgb);
            }
    }

    public Canvas Upscale(int factor)
    {
        if (factor <= 1) return this;
        var big = new Canvas(Width * factor, Height * factor, 0);
        for (var row = 0; row < Height; row++)
            for (var col = 0; col < Width; col++)
            {
                var rgb = Get(col, row);
                big.FillRect(col * factor, row * factor, factor, factor, rgb);
            }
        return big;
    }

    /// <summary>Blits another canvas at an offset — how the multi-panel figures are assembled.</summary>
    public void Blit(Canvas source, int x, int y)
    {
        for (var row = 0; row < source.Height; row++)
            for (var col = 0; col < source.Width; col++) Set(x + col, y + row, source.Get(col, row));
    }

    private static byte Mix(byte under, int over, double weight)
        => (byte)Math.Clamp((int)(under * (1 - weight) + over * weight), 0, 255);

    public static int Lerp(int from, int to, double position)
    {
        var at = Math.Clamp(position, 0, 1);
        int fr = (from >> 16) & 0xFF, fg = (from >> 8) & 0xFF, fb = from & 0xFF;
        int tr = (to >> 16) & 0xFF, tg = (to >> 8) & 0xFF, tb = to & 0xFF;
        return ((int)(fr + (tr - fr) * at) << 16) | ((int)(fg + (tg - fg) * at) << 8) | (int)(fb + (tb - fb) * at);
    }

    public static int Scale(int rgb, double keep)
        => (Math.Clamp((int)(((rgb >> 16) & 0xFF) * keep), 0, 255) << 16)
         | (Math.Clamp((int)(((rgb >> 8) & 0xFF) * keep), 0, 255) << 8)
         | Math.Clamp((int)((rgb & 0xFF) * keep), 0, 255);
}

/// <summary>Writes a canvas as a PNG: signature, IHDR, one IDAT (Up-filtered scanlines through zlib), IEND.</summary>
internal static class Png
{
    private static readonly byte[] Signature = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];

    public static void Write(string path, Canvas canvas)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var file = File.Create(path);
        file.Write(Signature);

        var header = new byte[13];
        BigEndian(header, 0, canvas.Width);
        BigEndian(header, 4, canvas.Height);
        header[8] = 8; header[9] = 2;
        WriteChunk(file, "IHDR", header);
        WriteChunk(file, "IDAT", Deflate(canvas.Width, canvas.Height, canvas.Pixels));
        WriteChunk(file, "IEND", []);
    }

    private static byte[] Deflate(int width, int height, byte[] rgb)
    {
        var stride = width * 3;
        var filtered = new byte[height * (stride + 1)];
        for (var row = 0; row < height; row++)
        {
            var source = row * stride;
            var target = row * (stride + 1);
            filtered[target] = 2;
            for (var offset = 0; offset < stride; offset++)
            {
                var above = row == 0 ? (byte)0 : rgb[source - stride + offset];
                filtered[target + 1 + offset] = (byte)(rgb[source + offset] - above);
            }
        }
        using var memory = new MemoryStream();
        using (var zlib = new ZLibStream(memory, CompressionLevel.Optimal, leaveOpen: true)) zlib.Write(filtered);
        return memory.ToArray();
    }

    private static void WriteChunk(Stream file, string type, byte[] data)
    {
        var length = new byte[4];
        BigEndian(length, 0, data.Length);
        file.Write(length);
        var body = new byte[4 + data.Length];
        for (var i = 0; i < 4; i++) body[i] = (byte)type[i];
        data.CopyTo(body, 4);
        file.Write(body);
        var crc = new byte[4];
        BigEndian(crc, 0, (int)Crc32(body));
        file.Write(crc);
    }

    private static void BigEndian(byte[] buffer, int at, int value)
    {
        buffer[at] = (byte)(value >> 24); buffer[at + 1] = (byte)(value >> 16);
        buffer[at + 2] = (byte)(value >> 8); buffer[at + 3] = (byte)value;
    }

    private static readonly uint[] CrcTable = BuildCrcTable();

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            table[n] = c;
        }
        return table;
    }

    private static uint Crc32(byte[] data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in data) crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc ^ 0xFFFFFFFFu;
    }
}
