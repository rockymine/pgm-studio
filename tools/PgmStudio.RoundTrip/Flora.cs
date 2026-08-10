using PgmStudio.Minecraft;

namespace PgmStudio.RoundTrip;

/// <summary>
/// Counts a world's trees and names each one's species, separating them from the timber built into its
/// structures.
///
/// <para><b>A tree is a stem, not a connected lump of wood.</b> Counting log components counts whatever the
/// wood happens to be joined as: a detailed oak whose limbs meet the trunk only diagonally shatters into a
/// dozen pieces, while a canopy bridging two neighbours fuses them into one. Counting every log that rests on
/// the ground is no better — a conifer's lower branches droop until they touch, and each contact reads as
/// another tree.</para>
///
/// <para><b>The stable unit is a rooted vertical run.</b> A stem is a log standing on something that is not
/// wood, carrying an unbroken column of logs above it; the run length is what separates a real trunk from a
/// branch tip brushing the ground, since a limb angles away within a block or two and a trunk does not.
/// Stems are then grouped by plan adjacency, so a trunk two blocks square is one tree rather than four, which
/// is what lets a detailed oak count the same as a single-stem spruce.</para>
///
/// <para><b>What marks the run is read off the map, not assumed.</b> Where an author faces trunks in all-bark
/// wood, that facing is the sharpest trunk marker there is — it separates a trunk from its own branches, which
/// are ordinary rotated logs. Where no all-bark wood is used at all, demanding it finds nothing, so the run
/// falls back to logs standing upright: still a vertical column of one species, just marked by its axis rather
/// than its facing. The choice is made once from the share of all-bark wood in the world, because the two
/// tests disagree on a map that uses the convention — a map framing its buildings in upright timber would
/// return every post as a tree under the fallback. The choice is made on <b>outcome, not proportion</b>: a
/// world with six trees and four hundred structural logs uses all-bark for every trunk it has while all-bark
/// is a few per cent of its wood, so any share threshold reads it backwards. Asking the sharper test first
/// and falling back only when it finds nothing needs no threshold at all.</para>
///
/// <para><b>Species is read from the canopy, not the trunk.</b> An author is free to pair any wood with any
/// leaf — a pine built from acacia log under birch leaves is exactly such a pairing — so the leaves standing
/// around a stem decide its species and the trunk material is recorded rather than trusted.</para>
///
/// <para>Wood that roots nowhere is not discarded silently: logs in a component with no all-bark and no
/// leaves are structural timber, and all-bark wood carrying no canopy is returned apart, because an all-bark
/// post is what an author reaches for when a pillar should show no cut end.</para>
/// </summary>
internal static class Flora
{
    private const int OakLog = 17;       // data & 3: 0 oak, 1 spruce, 2 birch, 3 jungle
    private const int AcaciaLog = 162;   // data & 1: 0 acacia, 1 dark oak
    private const int OakLeaves = 18;
    private const int AcaciaLeaves = 161;

    /// <summary>How tall a rooted column must be to be a trunk rather than a branch touching down.</summary>
    private const int MinimumStem = 3;

    /// <summary>Which trunk marker this world was read as using. Set by <see cref="Classify"/>.</summary>
    public static string Convention { get; private set; } = "";

    /// <summary>How far from the trunk a leaf still names its tree.</summary>
    private const int CanopyReach = 3;

    public static bool IsAllBark(int id, int data) => (id is OakLog or AcaciaLog) && (data >> 2 & 3) == 3;
    public static bool IsLog(int id) => id is OakLog or AcaciaLog;
    public static bool IsLeaf(int id) => id is OakLeaves or AcaciaLeaves;

    /// <summary>The species a leaf belongs to. Only the low bits name the wood; the rest carry the decay and
    /// permanence flags, which say nothing about what kind of tree it is.</summary>
    public static string LeafSpecies(int id, int data) => id == OakLeaves
        ? (data & 3) switch { 0 => "oak", 1 => "spruce", 2 => "birch", _ => "jungle" }
        : (data & 1) == 0 ? "acacia" : "dark oak";

    public static string LogSpecies(int id, int data) => id == OakLog
        ? (data & 3) switch { 0 => "oak", 1 => "spruce", 2 => "birch", _ => "jungle" }
        : (data & 1) == 0 ? "acacia" : "dark oak";

    /// <summary>One tree: the stems it stands on and the canopy that names it.</summary>
    public sealed record Tree(string Species, string Trunk, int Stems, int TrunkLogs, int LeafCount,
                              int MinX, int MaxX, int MinZ, int MaxZ, int BaseY, int TopY,
                              IReadOnlyList<(int X, int Y, int Z)> Wood)
    {
        /// <summary>Widest span of the canopy in plan, in blocks. Measured over the leaves assigned to this
        /// tree rather than a fixed radius around its trunk, so it reports the crown the author drew.</summary>
        public int CanopyWidth { get; set; }

        /// <summary>Leaves assigned to this tree by nearest trunk.</summary>
        public int Canopy { get; set; }
    }

    public sealed record Result(IReadOnlyList<Tree> Trees, IReadOnlyList<Tree> Bare, int StructuralLogs,
                                int UnrootedWood);

    public static Result Classify(IEnumerable<AnvilRegion.Chunk> chunks)
    {
        var logs = new Dictionary<(int X, int Y, int Z), (int Id, int Data)>();
        var leaves = new Dictionary<(int X, int Y, int Z), (int Id, int Data)>();
        List<((int X, int Y, int Z) Cell, int Run)> rooted;
        var footings = new List<(int X, int Y, int Z)>();

        foreach (var chunk in chunks)
        {
            var ids = new ushort[256 * 256];
            var data = new byte[256 * 256];
            foreach (var section in AnvilRegion.Sections(chunk))
            {
                var yStart = section.SectionY * 16;
                if (yStart is < 0 or >= 256) continue;
                Array.Copy(section.Ids, 0, ids, yStart * 256, 4096);
                Array.Copy(section.Data, 0, data, yStart * 256, 4096);
            }

            for (var lz = 0; lz < 16; lz++)
                for (var lx = 0; lx < 16; lx++)
                {
                    var col = (lz << 4) | lx;
                    int worldX = chunk.ChunkX * 16 + lx, worldZ = chunk.ChunkZ * 16 + lz;
                    for (var y = 0; y < 256; y++)
                    {
                        var id = ids[(y << 8) | col];
                        if (IsLeaf(id)) { leaves[(worldX, y, worldZ)] = (id, data[(y << 8) | col]); continue; }
                        if (!IsLog(id)) continue;
                        logs[(worldX, y, worldZ)] = (id, data[(y << 8) | col]);

                        var under = y > 0 ? ids[((y - 1) << 8) | col] : 0;
                        if (under != 0 && !IsLog(under) && !IsLeaf(under)) footings.Add((worldX, y, worldZ));
                    }
                }
        }

        // Which facing marks a trunk on this world, decided by asking the sharper test first. All-bark is the
        // better marker wherever it is used, because a branch leaving a trunk is rotated and drops out on its
        // own. A world that never uses it yields no stems at all under it, and upright logs are what remain.
        List<((int X, int Y, int Z) Cell, int Run)> Stems(Func<int, int, bool> isTrunkWood)
        {
            var found = new List<((int X, int Y, int Z) Cell, int Run)>();
            foreach (var footing in footings)
            {
                var (id, data) = logs[footing];
                if (!isTrunkWood(id, data)) continue;
                var species = LogSpecies(id, data);
                var run = 0;
                while (logs.TryGetValue((footing.X, footing.Y + run, footing.Z), out var above)
                       && isTrunkWood(above.Id, above.Data) && LogSpecies(above.Id, above.Data) == species) run++;
                if (run >= MinimumStem) found.Add((footing, run));
            }
            return found;
        }

        rooted = Stems(IsAllBark);
        var allBark = logs.Count(entry => IsAllBark(entry.Value.Id, entry.Value.Data));
        if (rooted.Count > 0)
            Convention = $"all-bark ({rooted.Count} stems; {allBark} of {logs.Count} logs carry it)";
        else
        {
            rooted = Stems((id, data) => (data >> 2 & 3) == 0);
            Convention = $"upright logs (all-bark yielded no stems from {allBark} such blocks)";
        }

        var trees = new List<Tree>();
        var claimed = new HashSet<(int X, int Y, int Z)>();
        var pending = new Dictionary<(int X, int Y, int Z), int>(rooted.Select(entry =>
            new KeyValuePair<(int X, int Y, int Z), int>(entry.Cell, entry.Run)));

        while (pending.Count > 0)
        {
            var seed = pending.Keys.First();
            var runs = new List<int>();
            var cluster = new List<(int X, int Y, int Z)>();
            var queue = new Queue<(int X, int Y, int Z)>([seed]);
            runs.Add(pending[seed]);
            pending.Remove(seed);

            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                cluster.Add(cell);
                foreach (var other in pending.Keys.Where(other =>
                             Math.Abs(other.X - cell.X) <= 1 && Math.Abs(other.Z - cell.Z) <= 1 &&
                             Math.Abs(other.Y - cell.Y) <= 2).ToList())
                {
                    runs.Add(pending[other]);
                    pending.Remove(other);
                    queue.Enqueue(other);
                }
            }

            // The trunk is every log stacked over the cluster's footprint; the canopy is the leaves within
            // reach of it, which is what names the tree.
            var trunkCells = new List<(int X, int Y, int Z)>();
            foreach (var root in cluster)
                for (var y = root.Y; y < 256 && logs.ContainsKey((root.X, y, root.Z)); y++)
                    trunkCells.Add((root.X, y, root.Z));
            foreach (var cell in trunkCells) claimed.Add(cell);

            var canopy = new Dictionary<string, int>();
            var seen = new HashSet<(int X, int Y, int Z)>();
            foreach (var cell in trunkCells)
                for (var dy = -1; dy <= CanopyReach; dy++)
                    for (var dz = -CanopyReach; dz <= CanopyReach; dz++)
                        for (var dx = -CanopyReach; dx <= CanopyReach; dx++)
                        {
                            var near = (cell.X + dx, cell.Y + dy, cell.Z + dz);
                            if (!seen.Add(near) || !leaves.TryGetValue(near, out var leaf)) continue;
                            var kind = LeafSpecies(leaf.Id, leaf.Data);
                            canopy[kind] = canopy.GetValueOrDefault(kind) + 1;
                        }

            var trunkWood = trunkCells.GroupBy(cell => LogSpecies(logs[cell].Id, logs[cell].Data))
                .OrderByDescending(group => group.Count()).First().Key;
            var species = canopy.Count > 0 ? canopy.OrderByDescending(entry => entry.Value).First().Key : trunkWood;

            trees.Add(new Tree(species, trunkWood, cluster.Count, trunkCells.Count, canopy.Values.Sum(),
                cluster.Min(cell => cell.X), cluster.Max(cell => cell.X),
                cluster.Min(cell => cell.Z), cluster.Max(cell => cell.Z),
                cluster.Min(cell => cell.Y), cluster.Min(cell => cell.Y) + runs.Max() - 1, trunkCells));
        }

        // What the stem pass did not claim: wood that never rooted. Split by whether it carries bark, since
        // an all-bark post is a deliberate choice and plain oriented timber is a wall.
        var leftover = logs.Keys.Where(cell => !claimed.Contains(cell)).ToList();
        var bareBark = leftover.Count(cell => IsAllBark(logs[cell].Id, logs[cell].Data));

        var bare = trees.Where(tree => tree.LeafCount == 0).ToList();
        MeasureCanopies(trees, leaves);
        return new Result([.. trees.Where(tree => tree.LeafCount > 0)], bare,
            leftover.Count - bareBark, bareBark);
    }

    /// <summary>How far from a trunk a leaf may still belong to it. Beyond this a leaf is nobody's.</summary>
    private const int CanopySearch = 12;

    /// <summary>Gives every leaf to the nearest trunk and measures each crown from what it was given.
    ///
    /// <para>A fixed radius around a trunk cannot measure a canopy, since what it reports is the radius.
    /// Nearest-trunk assignment reports the crown instead, and it is also what keeps neighbouring trees apart:
    /// where two canopies stand close without overlapping, every leaf is unambiguously nearer one trunk than
    /// the other and each keeps its own width. Where they do overlap the boundary falls midway, which
    /// understates both rather than merging them into one.</para></summary>
    private static void MeasureCanopies(List<Tree> trees, Dictionary<(int X, int Y, int Z), (int Id, int Data)> leaves)
    {
        var owner = new Dictionary<(int X, int Z), int>();
        for (var index = 0; index < trees.Count; index++)
            foreach (var log in trees[index].Wood) owner.TryAdd((log.X, log.Z), index);

        var spanX = new Dictionary<int, (int Min, int Max)>();
        var spanZ = new Dictionary<int, (int Min, int Max)>();
        var counts = new Dictionary<int, int>();

        foreach (var leaf in leaves.Keys)
        {
            var best = -1;
            var bestDistance = int.MaxValue;
            for (var radius = 0; radius <= CanopySearch && best < 0; radius++)
                for (var dx = -radius; dx <= radius; dx++)
                    for (var dz = -radius; dz <= radius; dz++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dz)) != radius) continue;
                        if (!owner.TryGetValue((leaf.X + dx, leaf.Z + dz), out var tree)) continue;
                        var distance = dx * dx + dz * dz;
                        if (distance >= bestDistance) continue;
                        bestDistance = distance;
                        best = tree;
                    }
            if (best < 0) continue;

            counts[best] = counts.GetValueOrDefault(best) + 1;
            spanX[best] = spanX.TryGetValue(best, out var sx)
                ? (Math.Min(sx.Min, leaf.X), Math.Max(sx.Max, leaf.X)) : (leaf.X, leaf.X);
            spanZ[best] = spanZ.TryGetValue(best, out var sz)
                ? (Math.Min(sz.Min, leaf.Z), Math.Max(sz.Max, leaf.Z)) : (leaf.Z, leaf.Z);
        }

        for (var index = 0; index < trees.Count; index++)
        {
            trees[index].Canopy = counts.GetValueOrDefault(index);
            if (!spanX.TryGetValue(index, out var sx)) continue;
            var sz = spanZ[index];
            trees[index].CanopyWidth = Math.Max(sx.Max - sx.Min, sz.Max - sz.Min) + 1;
        }
    }
}
