using PgmStudio.Geom;

namespace PgmStudio.Minecraft.Render;

/// <summary>How a step to a neighbour reads, by <see cref="Walk"/>'s own tiers.</summary>
public enum SlopeClass { Walked, Scrambled, Barrier }

/// <summary>
/// The board's relief as a grid of steps: for each sampled cell, the worst height difference to a sampled
/// neighbour, classed by the same tiers a walk is priced in. A cliff reads as a line of barrier cells, a ramp
/// as a band of walked ones crossing it, and an overdone relief as a page of barrier.
///
/// <para>The grid samples one block per <c>every</c>x<c>every</c> cell, its top-left corner — the same corner
/// <see cref="HeightmapText"/> samples — so a lip that never touches a sampled corner does not show. That is
/// the cost of <c>every</c>, not a fault of the reading.</para>
/// </summary>
public static class SlopeGrid
{
    /// <summary>One connected run of barrier cells, 8-connected over the sampled grid.</summary>
    public sealed record Face(int Cells, int MinX, int MinZ, int MaxX, int MaxZ);

    /// <summary>The grid, its tallies and its faces, largest first and capped at twenty.</summary>
    public sealed record Result(int MinX, int MinZ, int Every, int Width, int Height,
        SlopeClass?[] Classes, int Walked, int Scrambled, int Barrier, IReadOnlyList<Face> Faces)
    {
        public SlopeClass? At(int col, int row) => Classes[row * Width + col];
    }

    /// <summary>Null where the board has no ground column at all.</summary>
    public static Result? Build(IReadOnlyDictionary<(int X, int Z), int> surface, int every)
    {
        if (surface.Count == 0) return null;
        every = Math.Clamp(every, 1, 8);

        int minX = surface.Keys.Min(cell => cell.X), maxX = surface.Keys.Max(cell => cell.X);
        int minZ = surface.Keys.Min(cell => cell.Z), maxZ = surface.Keys.Max(cell => cell.Z);
        var width = (maxX - minX) / every + 1;
        var height = (maxZ - minZ) / every + 1;

        int? HeightAt(int col, int row) =>
            col < 0 || col >= width || row < 0 || row >= height ? null
            : surface.TryGetValue((minX + col * every, minZ + row * every), out var elevation) ? elevation : null;

        var classes = new SlopeClass?[width * height];
        int walked = 0, scrambled = 0, barrier = 0;
        var offsets = new[] { (1, 0), (-1, 0), (0, 1), (0, -1) };
        for (var row = 0; row < height; row++)
            for (var col = 0; col < width; col++)
            {
                if (HeightAt(col, row) is not { } here) continue;

                var worst = 0;
                foreach (var (dCol, dRow) in offsets)
                    if (HeightAt(col + dCol, row + dRow) is { } neighbour)
                        worst = Math.Max(worst, Math.Abs(here - neighbour));

                var stepClass = worst <= Walk.FreeRise ? SlopeClass.Walked
                    : worst <= Walk.ScrambleStep ? SlopeClass.Scrambled : SlopeClass.Barrier;
                classes[row * width + col] = stepClass;
                switch (stepClass)
                {
                    case SlopeClass.Walked: walked++; break;
                    case SlopeClass.Scrambled: scrambled++; break;
                    default: barrier++; break;
                }
            }

        return new Result(minX, minZ, every, width, height, classes, walked, scrambled, barrier,
            Faces(classes, width, height, minX, minZ, every));
    }

    /// <summary>The connected runs of barrier cells, 8-connected over the sampled grid, largest first, capped
    /// at twenty — the read that names <b>where</b> a relief is overdone rather than merely that it is.</summary>
    private static List<Face> Faces(SlopeClass?[] classes, int width, int height, int minX, int minZ, int every)
    {
        var seen = new bool[classes.Length];
        var faces = new List<Face>();
        for (var seedRow = 0; seedRow < height; seedRow++)
            for (var seedCol = 0; seedCol < width; seedCol++)
            {
                var seedIndex = seedRow * width + seedCol;
                if (seen[seedIndex] || classes[seedIndex] != SlopeClass.Barrier) continue;

                var component = new List<(int Col, int Row)>();
                var queue = new Queue<(int Col, int Row)>();
                seen[seedIndex] = true;
                queue.Enqueue((seedCol, seedRow));
                while (queue.Count > 0)
                {
                    var (col, row) = queue.Dequeue();
                    component.Add((col, row));
                    for (var dRow = -1; dRow <= 1; dRow++)
                        for (var dCol = -1; dCol <= 1; dCol++)
                        {
                            if (dCol == 0 && dRow == 0) continue;
                            int neighbourCol = col + dCol, neighbourRow = row + dRow;
                            if (neighbourCol < 0 || neighbourCol >= width || neighbourRow < 0 || neighbourRow >= height) continue;
                            var index = neighbourRow * width + neighbourCol;
                            if (seen[index] || classes[index] != SlopeClass.Barrier) continue;
                            seen[index] = true;
                            queue.Enqueue((neighbourCol, neighbourRow));
                        }
                }

                faces.Add(new Face(component.Count,
                    minX + component.Min(cell => cell.Col) * every, minZ + component.Min(cell => cell.Row) * every,
                    minX + component.Max(cell => cell.Col) * every, minZ + component.Max(cell => cell.Row) * every));
            }
        return [.. faces.OrderByDescending(face => face.Cells).Take(20)];
    }

    /// <summary>One digit string per row — <c>0</c> walked, <c>1</c> scrambled, <c>2</c> barrier, space void —
    /// the convention <c>CoverageDto.Rows</c> already answers a class grid in.</summary>
    public static IReadOnlyList<string> Rows(Result grid)
    {
        var rows = new List<string>(grid.Height);
        for (var row = 0; row < grid.Height; row++)
        {
            var line = new System.Text.StringBuilder(grid.Width);
            for (var col = 0; col < grid.Width; col++)
                line.Append(grid.At(col, row) switch
                {
                    SlopeClass.Walked => '0', SlopeClass.Scrambled => '1', SlopeClass.Barrier => '2', _ => ' ',
                });
            rows.Add(line.ToString());
        }
        return rows;
    }

    public static string Render(Result grid)
    {
        var text = new System.Text.StringBuilder();
        text.Append($"SLOPES  1 char = {grid.Every}x{grid.Every} blocks, the worst step to a neighbour inside it  ")
            .Append($"x {grid.MinX}..{grid.MinX + (grid.Width - 1) * grid.Every} across, ")
            .Append($"z {grid.MinZ}..{grid.MinZ + (grid.Height - 1) * grid.Every} down\n");

        static string Range(int lowInclusive, int highInclusive) =>
            lowInclusive == highInclusive ? lowInclusive.ToString() : $"{lowInclusive}-{highInclusive}";
        text.Append($"KEY  . walked (rise 0-{Walk.FreeRise})  ")
            .Append($": scrambled with a block (rise {Range(Walk.FreeRise + 1, Walk.ScrambleStep)})  ")
            .Append($"# barrier (rise {Walk.ScrambleStep + 1}+)  space void\n");

        TextGridRows.Append(text, grid.Width, grid.Height, grid.MinX, grid.MinZ, grid.Every,
            (col, row) => grid.At(col, row) switch
            {
                SlopeClass.Walked => '.', SlopeClass.Scrambled => ':', SlopeClass.Barrier => '#', _ => ' ',
            });

        var faceLine = grid.Faces.Count == 0 ? "faces: 0"
            : $"faces: {grid.Faces.Count}, largest {grid.Faces[0].Cells} at "
              + $"x {grid.Faces[0].MinX}..{grid.Faces[0].MaxX} z {grid.Faces[0].MinZ}..{grid.Faces[0].MaxZ}";
        text.Append($"cells: {grid.Walked} walked, {grid.Scrambled} scrambled, {grid.Barrier} barrier; {faceLine}\n");
        return text.ToString();
    }
}
