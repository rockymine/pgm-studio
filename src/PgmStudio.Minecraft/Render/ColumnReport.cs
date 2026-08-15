using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Palette;
namespace PgmStudio.Minecraft.Render;

/// <summary>
/// One or more vertical columns, bedrock to sky, named block by block — the textual half of the section
/// read-back (<see cref="SectionRender"/> is the image half). Every plan-view renderer in this suite answers
/// what a place looks like from above; neither answers what stands over one exact point, which is the
/// question a layer stack, a wall's courses, a stamped room's floor or a void column actually needs.
///
/// <para>A column is read straight off the decoded blocks with no filtering and no aggregation — every solid
/// block from the highest down, in id:data and the same <see cref="BlockPalette"/> name every other render
/// uses, so a probe here and a colour anywhere else in the studio can never disagree about what a block
/// is.</para>
/// </summary>
public static class ColumnReport
{
    public sealed record ColumnBlock(int Y, int Id, int Data, string Name);

    /// <summary>Reads a built region directory from disk and prints every requested column — the
    /// <c>RoundTrip</c> CLI's entry point.</summary>
    public static int Run(string regionDir, IReadOnlyList<(int X, int Z)> columns)
    {
        if (!Directory.Exists(regionDir)) { Console.Error.WriteLine($"no region dir: {regionDir}"); return 1; }
        var mcas = Directory.GetFiles(regionDir, "*.mca");
        if (mcas.Length == 0) { Console.Error.WriteLine($"no region files in {regionDir}"); return 1; }
        if (columns.Count == 0) { Console.Error.WriteLine("no columns requested"); return 1; }

        var byColumn = Render(mcas.SelectMany(AnvilRegion.ReadChunks), columns);
        foreach (var cell in columns)
        {
            var stack = byColumn[cell];
            Console.WriteLine($"=== column ({cell.X}, {cell.Z}) — {stack.Count} solid block(s) ===");
            if (stack.Count == 0) { Console.WriteLine("  (void — no block recorded at any height)"); continue; }
            foreach (var block in stack)
                Console.WriteLine($"  y{block.Y,3}  {block.Id,4}:{block.Data,-2}  {block.Name}");
        }
        return 0;
    }

    /// <summary>The pure read: chunks in, one block list per requested column out (highest first), no file or
    /// console I/O. A column absent from the world entirely still gets an (empty) entry, so a caller can tell
    /// "asked for and found nothing" from "never asked".</summary>
    public static Dictionary<(int X, int Z), List<ColumnBlock>> Render(
        IEnumerable<AnvilRegion.Chunk> chunks, IReadOnlyList<(int X, int Z)> columns)
    {
        var wanted = new HashSet<(int X, int Z)>(columns);
        var result = wanted.ToDictionary(cell => cell, _ => new List<ColumnBlock>());
        foreach (var chunk in chunks)
            foreach (var block in AnvilRegion.Blocks(chunk))
                if (result.TryGetValue((block.X, block.Z), out var stack))
                    stack.Add(new ColumnBlock(block.Y, block.Id, block.Data, BlockPalette.Name(block.Id, block.Data)));

        foreach (var stack in result.Values) stack.Sort((left, right) => right.Y.CompareTo(left.Y));
        return result;
    }
}
