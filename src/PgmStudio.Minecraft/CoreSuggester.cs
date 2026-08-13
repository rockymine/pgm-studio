using PgmStudio.Domain;

namespace PgmStudio.Minecraft;

/// <summary>
/// One core proposed from the world: the casing's block box, the lava it encloses, and the shell thickness and
/// leak depth that fall out of the geometry.
/// </summary>
/// <param name="Casing">The obsidian casing's bounding box — the structure, not a region drawn around it.</param>
/// <param name="LavaBlocks">How many lava blocks it encloses. The single strongest confidence signal: a real
/// core's interior is a small cuboid (3×3×3 = 27 is the corpus mode), and a cased lava lake is not a core.</param>
/// <param name="Shell">Casing thickness in blocks, from the gap between the casing and the lava it holds.</param>
/// <param name="Float">Air blocks directly under the casing, smallest across its footprint. Half the corpus's
/// cores rest on something (float 0); the rest hover, and that gap is what escaping lava falls through.</param>
/// <param name="OpenTop">Whether the lava reaches the casing's top layer — the minority style that leaves the
/// rim flush instead of capping it.</param>
public sealed record CoreSuggestion(BlockBox Casing, int LavaBlocks, int Shell, int Float, bool OpenTop);

/// <summary>
/// Proposes DTC cores by scanning a world for the one signature effectively nothing else in a map produces:
/// <b>lava fully enclosed by obsidian</b>.
///
/// <para>The enclosure test is what makes this work. Maps are full of lava — pools, falls, moats, decorative
/// pits — and full of obsidian, but a lava volume whose <i>every</i> non-lava face-neighbour is obsidian is a
/// deliberately sealed container, and the only reason to build one is a core. Measured over 302 corpus maps
/// carrying a declared objective: <b>267 candidates, 219 of them a declared core — 82% precision at 77%
/// recall</b> against 284 declared cores.</para>
///
/// <para>The rim is the one place the seal is allowed to open. A minority of cores leave the lava flush with
/// the casing top rather than capping it, so the cells above the lava's own top layer may be all obsidian or
/// all air — but not a mixture, which is a spill rather than a container. Reading that distinction is worth
/// four points of recall and is the difference between describing a core and describing most of one.</para>
///
/// <para>The parameters are read off the geometry rather than guessed, which is the difference between
/// proposing a box and proposing a core: the casing box is the obsidian shell's extent, the shell thickness is
/// the gap between casing and lava, and <c>openTop</c> is whether the lava reaches the top layer. Nothing here
/// proposes a <c>&lt;region&gt;</c> — that is a human's loose box (OB12), is not in the world, and cannot be
/// detected; a confirmed suggestion gets the exact structure box the authoring side emits.</para>
///
/// <para><b>Gather, not serve.</b> This reads <c>.mca</c> and therefore runs once, inside the single ingest
/// pass, beside <see cref="MonumentSuggester.Gather"/>. The world is discarded afterwards, so a suggestion not
/// captured then cannot be recovered without re-importing the map.</para>
///
/// <para>Destroyables are not proposed here, and not because they are undetectable — 98% of declared ones are
/// a standalone connected mass, and 95% have a symmetric partner. They have no <i>local</i> signature: a DTM
/// structure is one to three ordinary blocks, so it is separated by ranking candidates against each other
/// rather than by any test a single cluster can pass. That is a different shape of detector and lives apart
/// from this one (<c>docs/world-scan/objective-suggestion.md</c> §3).</para>
/// </summary>
public static class CoreSuggester
{
    private const int Obsidian = 49;
    private const int Lava = 10;
    private const int StationaryLava = 11;

    /// <summary>The largest enclosed lava volume still proposed as a core. A casing is a small container: the
    /// corpus mode is 3×3×3 = 27 and a 7×7×7 casing holds 125, so this admits every real form with margin
    /// while rejecting a cased lake. Raising it buys no recall and costs precision.</summary>
    public const int MaxLavaBlocks = 512;

    private static readonly (int X, int Y, int Z)[] Faces =
        [(1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1)];

    /// <summary>Propose every core in the world, ordered by position so the result is stable across runs.</summary>
    public static List<CoreSuggestion> Gather(IEnumerable<AnvilRegion.Chunk> chunks)
    {
        var world = new Dictionary<(int, int, int), int>();
        foreach (var chunk in chunks)
            foreach (var block in AnvilRegion.Blocks(chunk))
                world[(block.X, block.Y, block.Z)] = block.Id;
        return Gather(world);
    }

    /// <summary>Propose every core in a decoded world (block position → id, air absent).</summary>
    public static List<CoreSuggestion> Gather(IReadOnlyDictionary<(int X, int Y, int Z), int> world)
    {
        var lava = world.Where(entry => entry.Value is Lava or StationaryLava).Select(entry => entry.Key).ToHashSet();
        var visited = new HashSet<(int X, int Y, int Z)>();
        var found = new List<CoreSuggestion>();

        foreach (var start in lava.OrderBy(cell => cell.Y).ThenBy(cell => cell.X).ThenBy(cell => cell.Z))
        {
            if (!visited.Add(start)) continue;

            var interior = Flood(start, lava, visited);
            if (interior.Count > MaxLavaBlocks) continue;

            // Sealed means every non-lava neighbour of the volume is obsidian — one gap in the sides or floor
            // and it is a pool, which is what most lava in a map is. The rim is the exception: a minority of
            // cores leave the lava flush with the casing top, so the cells directly above the lava's own top
            // layer may be obsidian (capped) or air (open), but not a mixture, which would be a spill.
            var lavaTop = interior.Max(cell => cell.Y);
            var walls = new HashSet<(int X, int Y, int Z)>();
            var rim = new HashSet<(int X, int Y, int Z)>();
            foreach (var cell in interior)
                foreach (var face in Faces)
                {
                    var next = (cell.X + face.X, cell.Y + face.Y, cell.Z + face.Z);
                    if (lava.Contains(next)) continue;
                    if (face.Y == 1 && cell.Y == lavaTop) rim.Add(next); else walls.Add(next);
                }
            if (walls.Count == 0) continue;
            if (!walls.All(cell => world.GetValueOrDefault(cell) == Obsidian)) continue;

            var cappedRim = rim.All(cell => world.GetValueOrDefault(cell) == Obsidian);
            var openRim = rim.All(cell => !world.ContainsKey(cell));
            if (!cappedRim && !openRim) continue;

            // A cap is casing; an open rim is nothing. Growing the casing from the wrong seed loses the top
            // layer, which is the difference between a 5-high casing and a 4-high one.
            var casingSeed = openRim ? walls : [.. walls, .. rim];
            found.Add(Describe(world, interior, casingSeed, openRim));
        }
        return found;
    }

    private static List<(int X, int Y, int Z)> Flood((int X, int Y, int Z) start,
                                                     HashSet<(int X, int Y, int Z)> lava,
                                                     HashSet<(int X, int Y, int Z)> visited)
    {
        var volume = new List<(int X, int Y, int Z)> { start };
        var queue = new Queue<(int X, int Y, int Z)>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            foreach (var face in Faces)
            {
                var next = (cell.X + face.X, cell.Y + face.Y, cell.Z + face.Z);
                if (lava.Contains(next) && visited.Add(next)) { volume.Add(next); queue.Enqueue(next); }
            }
        }
        return volume;
    }

    // The casing is the obsidian reachable outward from the seal, grown one layer at a time: a shell 2 thick
    // reads as two layers, which is what makes the thickness measurable rather than assumed.
    private static CoreSuggestion Describe(IReadOnlyDictionary<(int X, int Y, int Z), int> world,
                                           List<(int X, int Y, int Z)> interior,
                                           HashSet<(int X, int Y, int Z)> seal,
                                           bool openTop)
    {
        var casing = new HashSet<(int X, int Y, int Z)>(seal);
        var frontier = seal;
        var shell = 1;
        while (shell < 4)
        {
            var next = new HashSet<(int X, int Y, int Z)>();
            foreach (var cell in frontier)
                foreach (var face in Faces)
                {
                    var neighbour = (cell.X + face.X, cell.Y + face.Y, cell.Z + face.Z);
                    if (world.GetValueOrDefault(neighbour) == Obsidian && !casing.Contains(neighbour))
                        next.Add(neighbour);
                }
            // Only a layer that wraps the whole structure is casing; a few blocks touching it are decoration.
            if (next.Count < frontier.Count) break;
            foreach (var cell in next) casing.Add(cell);
            frontier = next;
            shell++;
        }

        var box = new BlockBox(
            casing.Min(cell => cell.X), casing.Min(cell => cell.Y), casing.Min(cell => cell.Z),
            casing.Max(cell => cell.X), casing.Max(cell => cell.Y), casing.Max(cell => cell.Z));

        // Air directly under the casing, smallest across its footprint — a core resting on anything floats 0.
        var gap = int.MaxValue;
        for (var x = box.MinX; x <= box.MaxX; x++)
            for (var z = box.MinZ; z <= box.MaxZ; z++)
            {
                if (!casing.Contains((x, box.MinY, z))) continue;
                var air = 0;
                for (var y = box.MinY - 1; y >= 0 && air < 32; y--)
                {
                    if (world.ContainsKey((x, y, z))) break;
                    air++;
                }
                gap = Math.Min(gap, air);
            }

        return new CoreSuggestion(box, interior.Count, shell, gap == int.MaxValue ? 0 : gap, openTop);
    }
}
