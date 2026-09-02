using PgmStudio.Geom.Render;
using PgmStudio.Minecraft.Anvil;

namespace PgmStudio.Minecraft.Render;

/// <summary>
/// The board's ground height as characters, one per <c>every</c>x<c>every</c> block cell sampled at its
/// top-left block — the text twin of the tone <see cref="HeightProfileRender"/> draws, in the grid a reader
/// with no eye on a picture can act on: a neighbour's height is a subtraction rather than an estimate.
///
/// <para>A cell's character is the height band it falls in, counted up from the board's lowest ground in
/// <c>0-9a-z</c> — thirty-six bands, wide enough that any board's relief still resolves to a legible sweep of
/// them. A house or a hall overprints its ground as <c>H</c>, water as <c>~</c>, and the map's own spawns and
/// goals overprint both as <c>@</c> and <c>!</c>, because a reader asking where a relief solved wrong is also
/// asking whether it did so under something that matters.</para>
/// </summary>
public static class HeightmapText
{
    /// <summary><c>0-9a-z</c> — the alphabet a height band is spelled in.</summary>
    private const int Bands = 36;

    /// <summary>Null where the board has no ground column at all — the text twin of an empty picture.</summary>
    public static string? Render(IReadOnlyDictionary<(int X, int Z), int> surface, WorldProvenance provenance,
        IReadOnlyList<(string Kind, int X, int Z)> markers, int every)
    {
        if (surface.Count == 0) return null;
        every = Math.Clamp(every, 1, 8);

        int minX = surface.Keys.Min(cell => cell.X), maxX = surface.Keys.Max(cell => cell.X);
        int minZ = surface.Keys.Min(cell => cell.Z), maxZ = surface.Keys.Max(cell => cell.Z);
        int low = surface.Values.Min(), high = surface.Values.Max();
        var band = Math.Max(1, (int)Math.Ceiling((high - low + 1) / (double)Bands));

        var width = (maxX - minX) / every + 1;
        var height = (maxZ - minZ) / every + 1;
        var glyph = new char[width, height];

        for (var row = 0; row < height; row++)
            for (var col = 0; col < width; col++)
            {
                int x = minX + col * every, z = minZ + row * every;
                glyph[col, row] = surface.TryGetValue((x, z), out var elevation)
                    ? TextGrid.Base36(Math.Min(Bands - 1, (elevation - low) / band)) : ' ';

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
        text.Append($"HEIGHTMAP  1 char = {every}x{every} blocks (the top-left block of each)  ")
            .Append($"x {minX}..{maxX} across, z {minZ}..{maxZ} down\n");
        text.Append($"KEY  char = ground height above y{low} in bands of {band} block(s): ")
            .Append($"0 = y{low}..{low + band - 1}, 1 = …; H house or hall  ~ water  @ spawn point  ")
            .Append("! goal  space = void\n");
        TextGrid.Frame(text, minX, minZ, width, height, every, (col, row) => glyph[col, row]);
        text.Append($"low y{low}, high y{high}, range {high - low}\n");
        return text.ToString();
    }

    private static int FloorDiv(int value, int by) => (int)Math.Floor(value / (double)by);
}
