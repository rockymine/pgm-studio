using PgmStudio.Geom.Render;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Painting;

namespace PgmStudio.Minecraft.Render;

/// <summary>
/// How steeply the ground is inclined, cell by cell, as characters — <b>the angle a slope band is picked
/// by</b> (TP24), in the grid a reader with no eye on a picture can act on.
///
/// <para>A cell's character is the <em>tens</em> of its angle: <c>0</c> is ground under ten degrees from
/// level, <c>4</c> is ground between forty and fifty, <c>8</c> is a face. So the glyph is the number and
/// needs no key, and an author holding a mask cut at 20 and 45 reads its two boundaries straight off the
/// page as the step from <c>1</c> to <c>2</c> and from <c>4</c> to <c>5</c>.</para>
///
/// <para>It answers the same <see cref="TerrainProfile.SlopeAt"/> the painter resolves against, over the same
/// terrain surface, so a band that came out wrong and the number that chose it cannot disagree. Water,
/// houses and the map's own spawns and goals overprint as they do on the heightmap, because ground under
/// something that matters is read differently from ground that is merely steep.</para>
/// </summary>
public static class InclineText
{
    /// <summary>Null where the board has no ground column at all — the text twin of an empty picture.</summary>
    public static string? Render(IReadOnlyDictionary<(int X, int Z), int> surface, WorldProvenance provenance,
        IReadOnlyList<(string Kind, int X, int Z)> markers, int every,
        int window = TerrainProfile.SlopeWindow)
    {
        if (surface.Count == 0) return null;
        every = Math.Clamp(every, 1, 8);
        window = Math.Max(1, window);

        int minX = surface.Keys.Min(cell => cell.X), maxX = surface.Keys.Max(cell => cell.X);
        int minZ = surface.Keys.Min(cell => cell.Z), maxZ = surface.Keys.Max(cell => cell.Z);

        var width = (maxX - minX) / every + 1;
        var height = (maxZ - minZ) / every + 1;
        var glyph = new char[width, height];

        // How much ground stands in each ten degrees, which is what says whether a cut lands where the
        // author thinks it does. A distribution is the reading here — the picture says where, this says how
        // much, and a mask is chosen on the second.
        var decade = new int[9];
        var ground = 0;

        for (var row = 0; row < height; row++)
            for (var col = 0; col < width; col++)
            {
                int x = minX + col * every, z = minZ + row * every;
                if (!surface.ContainsKey((x, z))) { glyph[col, row] = ' '; continue; }

                var angle = TerrainProfile.SlopeAt(surface, x, z, window);
                var tens = Math.Clamp(angle / 10, 0, 8);
                decade[tens]++;
                ground++;
                glyph[col, row] = (char)('0' + tens);

                var owner = provenance.OwnerAt(x, z);
                if (owner?.Kind == "house" || provenance.PassAt(x, z) == ProvenancePass.Structure)
                    glyph[col, row] = 'H';
                else if (owner?.Kind == "water")
                    glyph[col, row] = '~';
            }

        foreach (var marker in markers)
        {
            var col = FloorDiv(marker.X - minX, every);
            var row = FloorDiv(marker.Z - minZ, every);
            if (col < 0 || col >= width || row < 0 || row >= height) continue;
            glyph[col, row] = marker.Kind == "spawn" ? '@' : '!';
        }

        var text = new System.Text.StringBuilder();
        text.Append($"INCLINE  1 char = {every}x{every} blocks (the top-left block of each)  ")
            .Append($"x {minX}..{minX + (width - 1) * every} across, z {minZ}..{minZ + (height - 1) * every} down\n");
        text.Append($"KEY  char = tens of degrees from level, read {window} cell(s) either side: ")
            .Append("0 = 0..9°, 4 = 40..49°, 8 = 80°+;  H house or hall  ~ water  @ spawn point  ")
            .Append("! goal  space = void\n");
        TextGrid.Frame(text, minX, minZ, width, height, every, (col, row) => glyph[col, row]);

        if (ground > 0)
        {
            text.Append("ground by angle: ");
            for (var tens = 0; tens < decade.Length; tens++)
                if (decade[tens] > 0)
                    text.Append($"{tens}0-{tens}9° {100.0 * decade[tens] / ground:0.#}%  ");
            text.Append('\n');
            var steep = decade.Skip(4).Sum();
            text.Append($"{ground} sampled cell(s); {100.0 * steep / ground:0.#}% at 40° or steeper\n");
        }
        return text.ToString();
    }

    private static int FloorDiv(int value, int by) => (int)Math.Floor(value / (double)by);
}
