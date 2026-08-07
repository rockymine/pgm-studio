#:project ../../src/PgmStudio.Minecraft/PgmStudio.Minecraft.csproj
// Texture means for the block palette: reads a directory of Java 1.8 block textures
// (assets/minecraft/textures/blocks/), computes each one's alpha-masked mean colour, and either dumps the
// means, emits ready-to-paste palette rows, or diffs the shipped BlockPaletteData table against them.
//
// The palette is hand-authored from these means rather than generated at build time, because three families
// deliberately depart from the raw mean and cannot be derived from a texture alone: biome-tinted blocks
// (grass, leaves, vines, water, sugar cane, lily pad, tall grass) need a temperate tint the texture does not
// carry, ores need their accent rather than the four-fifths stone matrix they average to, and blocks whose
// visible face is chosen by the render (a log's rings, a hay bale's binding) need that choice made for them.
// This tool covers the rest — every block whose colour IS its texture mean — which is most of the table.
//
// Alpha is a mask, not a weight: a pixel is either in or out at a 50% threshold, so a mostly-transparent
// texture (glass, panes, bars, torches) averages the material rather than fading toward black. Animated
// textures are taller than they are wide (water, lava, fire, portal); only the first frame is read.
//
// Run:
//   dotnet run tools/palette/texture-average.cs -- --dump  <textureDir>   every texture's mean, one per line
//   dotnet run tools/palette/texture-average.cs -- --rows  <textureDir>   palette rows for the mapped entries
//   dotnet run tools/palette/texture-average.cs -- --check <textureDir>   diff the shipped table against them
//
// --check exits non-zero when any mapped entry is further than --tolerance (default 12) from its texture
// mean on any channel. PNG decoding is done here rather than through a package: the repo carries no image
// dependency, and a 16x16 non-interlaced PNG is a zlib stream plus five filter cases.
using System.IO.Compression;
using PgmStudio.Minecraft;

var mode = args.Length > 0 ? args[0] : "--help";
var dir = args.Length > 1 ? args[1] : "";
var tolerance = 12;
for (var i = 2; i < args.Length - 1; i++)
    if (args[i] == "--tolerance") tolerance = int.Parse(args[i + 1]);

if (mode is "--help" or "-h" || dir.Length == 0)
{
    Console.WriteLine("usage: texture-average.cs --dump|--rows|--check <textureDir> [--tolerance N]");
    return mode is "--help" or "-h" ? 0 : 2;
}
if (!Directory.Exists(dir))
{
    Console.Error.WriteLine($"texture directory not found: {dir}");
    return 2;
}

var means = new Dictionary<string, (int R, int G, int B)>(StringComparer.OrdinalIgnoreCase);
foreach (var path in Directory.EnumerateFiles(dir, "*.png").OrderBy(p => p, StringComparer.Ordinal))
{
    var mean = MeanColor(path);
    if (mean is { } value) means[Path.GetFileNameWithoutExtension(path)] = value;
}
if (means.Count == 0)
{
    Console.Error.WriteLine($"no readable textures in {dir}");
    return 2;
}

var mapped = BuildMap();

switch (mode)
{
    case "--dump":
        foreach (var (name, mean) in means.OrderBy(entry => entry.Key, StringComparer.Ordinal))
            Console.WriteLine($"{name,-36} {Hex(mean)}");
        return 0;

    case "--rows":
        foreach (var (texture, id, meta) in mapped)
        {
            if (!means.TryGetValue(texture, out var mean)) { Console.WriteLine($"// missing texture: {texture}"); continue; }
            var call = meta < 0
                ? $"Block({id}, \"{BlockPalette.Name(id, -1)}\", 0x{Hex(mean)[1..].ToUpperInvariant()});"
                : $"Variant({id}, {meta}, \"{BlockPalette.Name(id, meta)}\", 0x{Hex(mean)[1..].ToUpperInvariant()});";
            Console.WriteLine($"{call,-72} // {texture}");
        }
        return 0;

    case "--check":
        var drifted = 0;
        var missing = 0;
        foreach (var (texture, id, meta) in mapped)
        {
            if (!means.TryGetValue(texture, out var mean)) { missing++; Console.WriteLine($"MISSING  {texture}"); continue; }
            var shipped = BlockPalette.Color(id, meta);
            var delta = Math.Max(Math.Abs(shipped.R - mean.R), Math.Max(Math.Abs(shipped.G - mean.G), Math.Abs(shipped.B - mean.B)));
            if (delta <= tolerance) continue;
            drifted++;
            Console.WriteLine($"DRIFT    {id}:{(meta < 0 ? "*" : meta),-2} {BlockPalette.Name(id, meta),-28} " +
                              $"shipped {BlockPalette.Hex(id, meta)}  texture {Hex(mean)}  delta {delta}");
        }
        Console.WriteLine($"\n{mapped.Length} mapped entries, {drifted} beyond tolerance {tolerance}, {missing} missing.");
        return drifted > 0 ? 1 : 0;

    default:
        Console.Error.WriteLine($"unknown mode: {mode}");
        return 2;
}

static string Hex((int R, int G, int B) color) => $"#{color.R:x2}{color.G:x2}{color.B:x2}";

/// <summary>The alpha-masked mean of a texture's first frame, or null if it cannot be decoded.</summary>
static (int R, int G, int B)? MeanColor(string path)
{
    byte[] pixels;
    int width, height;
    try { (pixels, width, height) = Png.Decode(File.ReadAllBytes(path)); }
    catch (Exception error) { Console.Error.WriteLine($"skip {Path.GetFileName(path)}: {error.Message}"); return null; }

    // An animated texture stacks its frames vertically; only the first is the block's resting look.
    var frameHeight = height > width && width > 0 ? width : height;
    long sumR = 0, sumG = 0, sumB = 0, opaque = 0;
    for (var y = 0; y < frameHeight; y++)
        for (var x = 0; x < width; x++)
        {
            var offset = (y * width + x) * 4;
            if (pixels[offset + 3] < 128) continue;      // alpha is a mask at 50%, not a weight
            sumR += pixels[offset];
            sumG += pixels[offset + 1];
            sumB += pixels[offset + 2];
            opaque++;
        }
    if (opaque == 0) return null;
    return ((int)(sumR / opaque), (int)(sumG / opaque), (int)(sumB / opaque));
}

/// <summary>
/// The entries whose colour is exactly their texture's mean, and the 1.8 texture each reads. Blocks absent
/// from this map are the deliberate departures documented at the top of this file, plus the state and
/// redstone blocks whose visible face depends on placement.
/// </summary>
static (string Texture, int Id, int Meta)[] BuildMap()
{
    var map = new List<(string, int, int)>
    {
        ("stone", 1, 0), ("stone_granite", 1, 1), ("stone_granite_smooth", 1, 2), ("stone_diorite", 1, 3),
        ("stone_diorite_smooth", 1, 4), ("stone_andesite", 1, 5), ("stone_andesite_smooth", 1, 6),
        ("cobblestone", 4, -1), ("cobblestone_mossy", 48, -1), ("bedrock", 7, -1), ("obsidian", 49, -1),
        ("stonebrick", 98, 0), ("stonebrick_mossy", 98, 1), ("stonebrick_cracked", 98, 2), ("stonebrick_carved", 98, 3),
        ("brick", 45, -1), ("netherrack", 87, -1), ("soul_sand", 88, -1), ("nether_brick", 112, -1),
        ("end_stone", 121, -1), ("quartz_block_top", 155, 0), ("quartz_block_chiseled", 155, 1),
        ("quartz_block_lines", 155, 2), ("prismarine_rough", 168, 0), ("prismarine_bricks", 168, 1),
        ("prismarine_dark", 168, 2), ("sea_lantern", 169, -1),

        ("dirt", 3, 0), ("coarse_dirt", 3, 1), ("dirt_podzol_top", 3, 2), ("gravel", 13, -1),
        ("sand", 12, 0), ("red_sand", 12, 1), ("clay", 82, -1), ("hardened_clay", 172, -1),
        ("mycelium_top", 110, -1), ("snow", 80, -1), ("ice_packed", 174, -1),
        ("sandstone_normal", 24, 0), ("sandstone_carved", 24, 1), ("sandstone_smooth", 24, 2),
        ("red_sandstone_normal", 179, 0), ("red_sandstone_carved", 179, 1), ("red_sandstone_smooth", 179, 2),

        ("planks_oak", 5, 0), ("planks_spruce", 5, 1), ("planks_birch", 5, 2), ("planks_jungle", 5, 3),
        ("planks_acacia", 5, 4), ("planks_big_oak", 5, 5),
        ("log_oak_top", 17, 0), ("log_spruce_top", 17, 1), ("log_birch_top", 17, 2), ("log_jungle_top", 17, 3),
        ("log_acacia_top", 162, 0), ("log_big_oak_top", 162, 1),
        ("door_wood_lower", 64, -1), ("door_spruce_lower", 193, -1), ("door_birch_lower", 194, -1),
        ("door_jungle_lower", 195, -1), ("door_acacia_lower", 196, -1), ("door_dark_oak_lower", 197, -1),
        ("bookshelf", 47, -1), ("crafting_table_top", 58, -1), ("hay_block_top", 170, -1),

        ("gold_block", 41, -1), ("iron_block", 42, -1), ("diamond_block", 57, -1), ("emerald_block", 133, -1),
        ("lapis_block", 22, -1), ("redstone_block", 152, -1), ("coal_block", 173, -1), ("glowstone", 89, -1),
        ("slime", 165, -1), ("melon_top", 103, -1), ("pumpkin_top", 86, -1),
    };

    // 1.8 names light gray "silver" in every dye family.
    string[] dyes =
    [
        "white", "orange", "magenta", "light_blue", "yellow", "lime", "pink", "gray",
        "silver", "cyan", "purple", "blue", "brown", "green", "red", "black",
    ];
    for (var dye = 0; dye < dyes.Length; dye++)
    {
        map.Add(($"wool_colored_{dyes[dye]}", 35, dye));
        map.Add(($"hardened_clay_stained_{dyes[dye]}", 159, dye));
        map.Add(($"glass_{dyes[dye]}", 95, dye));
    }
    return [.. map];
}

static class Png
{
    /// <summary>Decodes a non-interlaced 8-bit PNG to tightly packed RGBA, with its width and height.</summary>
    public static (byte[] Rgba, int Width, int Height) Decode(byte[] file)
    {
        if (file.Length < 8 || file[0] != 0x89 || file[1] != 'P' || file[2] != 'N' || file[3] != 'G')
            throw new InvalidDataException("not a PNG");

        int width = 0, height = 0, bitDepth = 0, colorType = 0;
        byte[]? palette = null, paletteAlpha = null;
        var compressed = new MemoryStream();

        var offset = 8;
        while (offset + 8 <= file.Length)
        {
            var length = ReadInt32(file, offset);
            var type = System.Text.Encoding.ASCII.GetString(file, offset + 4, 4);
            var data = offset + 8;
            switch (type)
            {
                case "IHDR":
                    width = ReadInt32(file, data);
                    height = ReadInt32(file, data + 4);
                    bitDepth = file[data + 8];
                    colorType = file[data + 9];
                    if (file[data + 12] != 0) throw new NotSupportedException("interlaced PNG");
                    break;
                case "PLTE": palette = file[data..(data + length)]; break;
                case "tRNS": paletteAlpha = file[data..(data + length)]; break;
                case "IDAT": compressed.Write(file, data, length); break;
            }
            if (type == "IEND") break;
            offset = data + length + 4;                  // + CRC
        }
        if (bitDepth != 8) throw new NotSupportedException($"bit depth {bitDepth}");

        var channels = colorType switch
        {
            0 => 1, 2 => 3, 3 => 1, 4 => 2, 6 => 4,
            _ => throw new NotSupportedException($"colour type {colorType}"),
        };
        compressed.Position = 0;
        using var inflated = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionMode.Decompress)) zlib.CopyTo(inflated);
        var raw = inflated.ToArray();

        var stride = width * channels;
        var lines = new byte[height * stride];
        for (var row = 0; row < height; row++)
        {
            var filter = raw[row * (stride + 1)];
            var source = row * (stride + 1) + 1;
            var target = row * stride;
            for (var i = 0; i < stride; i++)
            {
                int left = i >= channels ? lines[target + i - channels] : 0;
                int up = row > 0 ? lines[target + i - stride] : 0;
                int upLeft = row > 0 && i >= channels ? lines[target + i - stride - channels] : 0;
                lines[target + i] = (byte)(raw[source + i] + filter switch
                {
                    0 => 0,
                    1 => left,
                    2 => up,
                    3 => (left + up) / 2,
                    4 => Paeth(left, up, upLeft),
                    _ => throw new InvalidDataException($"filter {filter}"),
                });
            }
        }

        var rgba = new byte[width * height * 4];
        for (var pixel = 0; pixel < width * height; pixel++)
        {
            var source = pixel * channels;
            var target = pixel * 4;
            switch (colorType)
            {
                case 0: rgba[target] = rgba[target + 1] = rgba[target + 2] = lines[source]; rgba[target + 3] = 255; break;
                case 2: rgba[target] = lines[source]; rgba[target + 1] = lines[source + 1]; rgba[target + 2] = lines[source + 2]; rgba[target + 3] = 255; break;
                case 3:
                    var index = lines[source];
                    if (palette is null) throw new InvalidDataException("palette image without PLTE");
                    rgba[target] = palette[index * 3];
                    rgba[target + 1] = palette[index * 3 + 1];
                    rgba[target + 2] = palette[index * 3 + 2];
                    rgba[target + 3] = paletteAlpha is not null && index < paletteAlpha.Length ? paletteAlpha[index] : (byte)255;
                    break;
                case 4: rgba[target] = rgba[target + 1] = rgba[target + 2] = lines[source]; rgba[target + 3] = lines[source + 1]; break;
                default: rgba[target] = lines[source]; rgba[target + 1] = lines[source + 1]; rgba[target + 2] = lines[source + 2]; rgba[target + 3] = lines[source + 3]; break;
            }
        }
        return (rgba, width, height);
    }

    private static int ReadInt32(byte[] buffer, int at) =>
        (buffer[at] << 24) | (buffer[at + 1] << 16) | (buffer[at + 2] << 8) | buffer[at + 3];

    private static int Paeth(int left, int up, int upLeft)
    {
        var estimate = left + up - upLeft;
        var toLeft = Math.Abs(estimate - left);
        var toUp = Math.Abs(estimate - up);
        var toUpLeft = Math.Abs(estimate - upLeft);
        return toLeft <= toUp && toLeft <= toUpLeft ? left : toUp <= toUpLeft ? up : upLeft;
    }
}
