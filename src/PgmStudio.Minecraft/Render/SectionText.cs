using PgmStudio.Geom;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Palette;

namespace PgmStudio.Minecraft.Render;

/// <summary>
/// The vertical cut as characters instead of pixels — <see cref="SectionRender"/>'s text twin, drawn off the
/// same <see cref="SectionRender.Gather"/> pass so a picture and its text answer the same cut. A shade is
/// estimated; a character is subtracted from the one beside it, which is the whole reason this exists beside
/// the picture rather than instead of it.
///
/// <para>Every block classes first by what it is — liquid, a log, a leaf or a plant, bedrock — then by where
/// it stands: at or below the recorded terrain surface it is ground, above it and inside a stacked layer's own
/// span it is that layer's storey, and failing both it classes by which pass claimed the column, so a wall, a
/// wool cage, a spawn platform and a made thing each read as themselves.</para>
/// </summary>
public static class SectionText
{
    public const char Air = '.';
    public const char Void = ' ';
    public const char Liquid = '~';
    public const char Log = 'I';
    public const char LeafOrPlant = 'T';
    public const char Bedrock = 'X';
    public const char Ground = '#';
    public const char Storey = 'L';
    public const char House = 'H';
    public const char Made = 'M';
    public const char Spawn = 'S';
    public const char GoalMarker = '!';
    public const char Wool = 'W';
    public const char Prop = 'o';

    private const int BedrockId = 7;

    private static readonly (char Glyph, string Name)[] Legend =
    [
        (Air, "air"), (Void, "void"), (Liquid, "liquid"), (Log, "log"), (LeafOrPlant, "leaf/plant"),
        (Bedrock, "bedrock"), (Ground, "ground"), (Storey, "storey"), (House, "house"), (Made, "made"),
        (Spawn, "spawn"), (GoalMarker, "destroyable/core"), (Wool, "wool"), (Prop, "prop"),
    ];

    /// <summary>
    /// The cut as characters: the same axis, extent and depth <see cref="SectionRender"/> draws, sampled every
    /// <paramref name="every"/> blocks across. Null where nothing stands along the cut, the same refusal the
    /// picture answers.
    ///
    /// <para><paramref name="surface"/> is the terrain's own recorded height (<c>BuiltWorld.Surface</c>);
    /// <paramref name="columns"/> is the rasterizer's own spans (<c>BuiltWorld.Columns</c>), read for the
    /// storey a block above the surface stands on; <paramref name="groundLayer"/> is the layer id that counts
    /// as the terrain itself rather than a stacked storey.</para>
    /// </summary>
    public static string? Render(VoxelWorld world, WorldProvenance provenance,
        IReadOnlyDictionary<(int X, int Z), int> surface, IReadOnlyList<ColumnSegment>? columns,
        string groundLayer, SectionAxis axis, int from, int to, int at, int? yMin, int? yMax, int depth,
        int every)
    {
        if (from > to) (from, to) = (to, from);
        if (every < 1) every = 1;

        var (gathered, loadedChunks) = SectionRender.Gather(
            AnvilRegion.FromWorld(world), axis, from, to, at, SectionRender.ClampDepth(depth));
        if (gathered.Count == 0) return null;

        var lowestFound = gathered.Values.SelectMany(stack => stack.Keys).Min();
        var highestFound = gathered.Values.SelectMany(stack => stack.Keys).Max();
        var lowest = yMin ?? Math.Max(0, lowestFound - 1);
        var highest = yMax ?? Math.Min(255, highestFound + 1);
        if (highest < lowest) (lowest, highest) = (highest, lowest);

        var byCell = (columns ?? []).GroupBy(segment => segment.Cell)
            .ToDictionary(group => group.Key, group => group.ToList());

        char Classify(int cut, int y)
        {
            if (!gathered.TryGetValue(cut, out var stack) || !stack.TryGetValue(y, out var block))
                return loadedChunks.Contains(SectionRender.ChunkOf(axis, cut, at)) ? Air : Void;

            if (BlockRoles.IsLiquid(block.Id)) return Liquid;
            if (BlockFamilies.IsLog(block.Id)) return Log;
            if (BlockFamilies.IsLeaf(block.Id) || BlockRoles.IsFlora(block.Id)) return LeafOrPlant;
            if (block.Id == BedrockId) return Bedrock;

            (int X, int Z) cell = axis == SectionAxis.AlongX ? (cut, at + block.Behind) : (at + block.Behind, cut);
            if (surface.TryGetValue(cell, out var top) && y <= top) return Ground;

            if (byCell.TryGetValue(cell, out var segments))
                foreach (var segment in segments)
                    if (segment.Layer != groundLayer && y >= segment.YFloor && y < segment.YTop) return Storey;

            var owner = provenance.OwnerAt(cell.X, cell.Z);
            var pass = provenance.PassAt(cell.X, cell.Z);
            if (owner?.Kind == "spawn") return Spawn;
            if (owner?.Kind is "destroyable" or "core") return GoalMarker;
            if (owner?.Kind == "wool") return Wool;
            if (pass == ProvenancePass.Made) return Made;
            if (pass == ProvenancePass.Structure || owner?.Kind == "house") return House;
            return Prop;
        }

        var along = axis == SectionAxis.AlongX ? "x" : "z";
        var fixedLabel = axis == SectionAxis.AlongX ? "z" : "x";
        var used = new HashSet<char>();
        var rows = new System.Text.StringBuilder();
        for (var y = highest; y >= lowest; y--)
        {
            rows.Append(y % 4 == 0 ? $"y{y,3} " : "     ");
            for (var cut = from; cut <= to; cut += every)
            {
                var glyph = Classify(cut, y);
                used.Add(glyph);
                rows.Append(glyph);
            }
            rows.Append('\n');
        }

        var written = new System.Text.StringBuilder();
        written.AppendLine($"SECTION  cut at {fixedLabel}={at} (along {along}), {along} {from}..{to} across "
            + $"({every} block per char), y{highest} at the top row down to y{lowest}");

        written.Append("KEY  ");
        foreach (var (glyph, name) in Legend)
            if (used.Contains(glyph)) written.Append(glyph).Append(' ').Append(name).Append("   ");
        written.AppendLine();

        written.Append(rows);

        written.Append("     ");
        for (var cut = from; cut <= to; cut += every)
            written.Append(cut % 10 == 0 ? Digit(Math.Abs(cut / 10) % 10) : ' ');
        written.AppendLine();

        written.Append("grnd ");
        for (var cut = from; cut <= to; cut += every)
        {
            (int X, int Z) cell = axis == SectionAxis.AlongX ? (cut, at) : (at, cut);
            written.Append(surface.TryGetValue(cell, out var top) ? Band(top - lowest) : ' ');
        }
        written.AppendLine();
        return written.ToString();
    }

    private static char Digit(int value) => (char)('0' + value);

    /// <summary>The height above the bottom row as one character, base-36 so a two-hundred-block board still
    /// fits a single column: <c>0</c>-<c>9</c> then <c>a</c>-<c>z</c>, clamped at <c>z</c> rather than
    /// wrapping — a clamped band still reads as "tall", where a wrapped one would read as "low".</summary>
    private static char Band(int value) =>
        value < 0 ? ' ' : value <= 9 ? (char)('0' + value) : value <= 35 ? (char)('a' + value - 10) : 'z';
}
