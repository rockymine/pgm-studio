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
/// <para><b>The canopy that names a tree is the one assigned to it, not the one within reach of it.</b> A
/// fixed box around a trunk holds whatever stands nearby, so on a densely planted world it holds the
/// neighbour's foliage: where trunks sit three blocks apart a box of that radius reaches the next tree and
/// votes with its leaves, and the species it returns is the denser neighbour's rather than the tree's. Leaves
/// are therefore given to their nearest trunk first and every reading — species, width, count — is taken from
/// what a tree was given, so one canopy answers all three and no leaf votes twice.</para>
///
/// <para><b>A cut trunk is neither a tree nor a post.</b> An author who fells a tree leaves its stump, and the
/// stump says so: the wood wears bark on every side, which is what hides a sawn face, and then ends in an
/// upright log showing one, with open sky above. Being two or three blocks tall it clears no stem threshold,
/// so without a test of its own it is swept into the structural tally and reported as part of a building.</para>
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


    public static bool IsAllBark(int id, int data) => (id is OakLog or AcaciaLog) && (data >> 2 & 3) == 3;

    /// <summary>Whether a log shows a sawn face upward. All-bark wood exists to hide exactly this, so a trunk
    /// wearing bark on every side that ends in an upright log has had its top taken off.</summary>
    public static bool IsCutEnd((int Id, int Data) log) => (log.Data >> 2 & 3) == 0;
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
    }

    /// <summary>One felled trunk: what is left standing where a tree was cut.</summary>
    public sealed record Stump(int X, int Z, int BaseY, int Height, string Wood);

    public sealed record Result(IReadOnlyList<Tree> Trees, IReadOnlyList<Tree> Bare, IReadOnlyList<Stump> Felled,
                                int StructuralLogs, int UnrootedWood);

    public static Result Classify(IEnumerable<AnvilRegion.Chunk> chunks)
    {
        var logs = new Dictionary<(int X, int Y, int Z), (int Id, int Data)>();
        var leaves = new Dictionary<(int X, int Y, int Z), (int Id, int Data)>();
        List<((int X, int Y, int Z) Cell, int Run)> rooted;
        var footings = new List<(int X, int Y, int Z)>();
        var openTop = new HashSet<(int X, int Y, int Z)>();

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
                        if (y == 255 || ids[((y + 1) << 8) | col] == 0) openTop.Add((worldX, y, worldZ));
                    }
                }
        }

        // Trunks that were cut, read before anything else, because a stump is neither a tree nor a post and
        // being short it would otherwise fall through both tests into the structural-timber tally.
        var felled = new List<Stump>();
        var stumpCells = new HashSet<(int X, int Y, int Z)>();
        foreach (var footing in footings)
        {
            var column = new List<(int Id, int Data)>();
            for (var y = footing.Y; logs.TryGetValue((footing.X, y, footing.Z), out var log); y++) column.Add(log);
            var top = footing.Y + column.Count - 1;
            if (!openTop.Contains((footing.X, top, footing.Z))) continue;
            if (!IsCutEnd(column[^1])) continue;
            if (!column.Take(column.Count - 1).Any(log => IsAllBark(log.Id, log.Data))) continue;

            felled.Add(new Stump(footing.X, footing.Z, footing.Y, column.Count,
                LogSpecies(column[0].Id, column[0].Data)));
            for (var height = 0; height < column.Count; height++)
                stumpCells.Add((footing.X, footing.Y + height, footing.Z));
        }

        // Which facing marks a trunk on this world, decided by asking the sharper test first. All-bark is the
        // better marker wherever it is used, because a branch leaving a trunk is rotated and drops out on its
        // own. A world that never uses it yields no stems at all under it, and upright logs are what remain.
        List<((int X, int Y, int Z) Cell, int Run)> Stems(Func<int, int, bool> isTrunkWood)
        {
            var found = new List<((int X, int Y, int Z) Cell, int Run)>();
            foreach (var footing in footings)
            {
                if (stumpCells.Contains(footing)) continue;
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

        // Every stem cluster is resolved into a trunk before a single leaf is named, because a tree is named
        // from the leaves it is given and nothing can be given out until all the claimants are known.
        var stands = new List<(List<(int X, int Y, int Z)> Cluster, List<int> Runs,
                               List<(int X, int Y, int Z)> Trunk, string Wood)>();
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

            // The trunk is every log stacked over the cluster's footprint.
            var trunkCells = new List<(int X, int Y, int Z)>();
            foreach (var root in cluster)
                for (var y = root.Y; y < 256 && logs.ContainsKey((root.X, y, root.Z)); y++)
                    trunkCells.Add((root.X, y, root.Z));
            foreach (var cell in trunkCells) claimed.Add(cell);

            var trunkWood = trunkCells.GroupBy(cell => LogSpecies(logs[cell].Id, logs[cell].Data))
                .OrderByDescending(group => group.Count()).First().Key;
            stands.Add((cluster, runs, trunkCells, trunkWood));
        }

        // Which stems carry a canopy at all, before any leaf is shared out. A crown grows from its own trunk
        // and touches it, so a stem with foliage against it is a tree and one standing in clear air is a post,
        // however much foliage happens to stand nearby.
        var wooded = stands.Where(stand => Touches(stand.Trunk, leaves)).ToList();
        var posts = stands.Where(stand => !Touches(stand.Trunk, leaves)).ToList();

        // Only trees take part in the sharing, so a post can no longer take a leaf off the tree beside it.
        var canopies = AssignLeaves([.. wooded.Select(stand => stand.Trunk)], leaves);

        var trees = new List<Tree>();
        for (var index = 0; index < wooded.Count; index++)
        {
            var (cluster, runs, trunkCells, trunkWood) = wooded[index];
            var canopy = canopies[index];
            var species = canopy.Species.Count > 0
                ? canopy.Species.OrderByDescending(entry => entry.Value).First().Key
                : trunkWood;

            trees.Add(Build(species, canopy.Leaves, canopy.Leaves == 0 ? 0
                : Math.Max(canopy.SpanX.Max - canopy.SpanX.Min, canopy.SpanZ.Max - canopy.SpanZ.Min) + 1));

            Tree Build(string named, int owned, int width) =>
                new(named, trunkWood, cluster.Count, trunkCells.Count, owned,
                    cluster.Min(cell => cell.X), cluster.Max(cell => cell.X),
                    cluster.Min(cell => cell.Z), cluster.Max(cell => cell.Z),
                    cluster.Min(cell => cell.Y), cluster.Min(cell => cell.Y) + runs.Max() - 1, trunkCells)
                { CanopyWidth = width };
        }

        // A post is named for the only material it has, its own wood.
        var bare = posts.Select(stand => new Tree(stand.Wood, stand.Wood, stand.Cluster.Count, stand.Trunk.Count, 0,
            stand.Cluster.Min(cell => cell.X), stand.Cluster.Max(cell => cell.X),
            stand.Cluster.Min(cell => cell.Z), stand.Cluster.Max(cell => cell.Z),
            stand.Cluster.Min(cell => cell.Y), stand.Cluster.Min(cell => cell.Y) + stand.Runs.Max() - 1,
            stand.Trunk)).ToList();

        // What neither pass claimed: wood that never rooted. Split by whether it carries bark, since an
        // all-bark post is a deliberate choice and plain oriented timber is a wall.
        var leftover = logs.Keys.Where(cell => !claimed.Contains(cell) && !stumpCells.Contains(cell)).ToList();
        var bareBark = leftover.Count(cell => IsAllBark(logs[cell].Id, logs[cell].Data));

        return new Result(trees, bare, felled, leftover.Count - bareBark, bareBark);
    }

    /// <summary>Whether any leaf stands against this trunk, on any of its faces, corners included.
    ///
    /// <para>This is what tells a tree from a post, and it is asked before any leaf is shared out. Foliage
    /// grows from the trunk that carries it, so a crown is in contact with its own stem; a pillar planted
    /// among trees has air on every side of it and the nearest leaves belong to the trunk they grew from.
    /// Distance cannot make that distinction, because a post two blocks from a tree is nearer some of that
    /// tree's leaves than the tree itself is.</para></summary>
    private static bool Touches(List<(int X, int Y, int Z)> trunk,
                                Dictionary<(int X, int Y, int Z), (int Id, int Data)> leaves)
    {
        foreach (var log in trunk)
            for (var dy = -1; dy <= 1; dy++)
                for (var dz = -1; dz <= 1; dz++)
                    for (var dx = -1; dx <= 1; dx++)
                        if (leaves.ContainsKey((log.X + dx, log.Y + dy, log.Z + dz))) return true;
        return false;
    }

    /// <summary>How far from a trunk a leaf may still belong to it. Beyond this a leaf is nobody's.</summary>
    private const int CanopySearch = 12;

    /// <summary>How far below its lowest log, and above its highest, a trunk still reaches foliage. A crown sits
    /// on a trunk: it may close a little above the last log and droop a block past the root, but foliage
    /// standing well under the root belongs to whatever grows down there, not to the stem above it.</summary>
    private const int CanopyDroop = 1;
    private const int CanopyCrown = 3;

    /// <summary>The leaves one trunk was given: how many, which species among them, and the plan box they
    /// cover.</summary>
    private readonly record struct Canopy(int Leaves, Dictionary<string, int> Species,
                                          (int Min, int Max) SpanX, (int Min, int Max) SpanZ);

    /// <summary>Gives every leaf to the nearest trunk, so each tree is read from the crown it owns.
    ///
    /// <para>A fixed radius around a trunk cannot measure a canopy, since what it reports is the radius, and it
    /// cannot name one either: on a world planted at three-block spacing that radius reaches the next trunk and
    /// the neighbour's foliage outvotes the tree's own. Nearest-trunk assignment reports the crown instead, and
    /// it is also what keeps neighbouring trees apart: where two canopies stand close without overlapping,
    /// every leaf is unambiguously nearer one trunk than the other and each keeps its own. Where they do
    /// overlap the boundary falls midway, which divides the shared leaves rather than counting them twice.</para>
    ///
    /// <para>Nearness in plan alone is not enough to own a leaf, because a stem is a column and a search over
    /// x and z cannot tell which part of that column it is looking at. A pillar standing on a structure eight
    /// blocks above a canopy is the nearest thing in plan to every leaf in it, and would take the lot. A trunk
    /// therefore only reaches foliage its own height admits, which leaves such a pillar owning nothing and
    /// reported as the bare stem it is.</para>
    /// </summary>
    private static Canopy[] AssignLeaves(List<List<(int X, int Y, int Z)>> trunks,
                                         Dictionary<(int X, int Y, int Z), (int Id, int Data)> leaves)
    {
        var owner = new Dictionary<(int X, int Z), int>();
        var reach = new (int Low, int High)[trunks.Count];
        for (var index = 0; index < trunks.Count; index++)
        {
            foreach (var log in trunks[index]) owner.TryAdd((log.X, log.Z), index);
            reach[index] = (trunks[index].Min(log => log.Y) - CanopyDroop,
                            trunks[index].Max(log => log.Y) + CanopyCrown);
        }

        var spanX = new Dictionary<int, (int Min, int Max)>();
        var spanZ = new Dictionary<int, (int Min, int Max)>();
        var counts = new Dictionary<int, int>();
        var species = new Dictionary<int, Dictionary<string, int>>();

        foreach (var (leaf, block) in leaves)
        {
            var best = -1;
            var bestDistance = int.MaxValue;
            for (var radius = 0; radius <= CanopySearch && best < 0; radius++)
                for (var dx = -radius; dx <= radius; dx++)
                    for (var dz = -radius; dz <= radius; dz++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dz)) != radius) continue;
                        if (!owner.TryGetValue((leaf.X + dx, leaf.Z + dz), out var tree)) continue;
                        if (leaf.Y < reach[tree].Low || leaf.Y > reach[tree].High) continue;
                        var distance = dx * dx + dz * dz;
                        if (distance >= bestDistance) continue;
                        bestDistance = distance;
                        best = tree;
                    }
            if (best < 0) continue;

            counts[best] = counts.GetValueOrDefault(best) + 1;
            var kind = LeafSpecies(block.Id, block.Data);
            var tally = species.TryGetValue(best, out var found) ? found : species[best] = [];
            tally[kind] = tally.GetValueOrDefault(kind) + 1;
            spanX[best] = spanX.TryGetValue(best, out var sx)
                ? (Math.Min(sx.Min, leaf.X), Math.Max(sx.Max, leaf.X)) : (leaf.X, leaf.X);
            spanZ[best] = spanZ.TryGetValue(best, out var sz)
                ? (Math.Min(sz.Min, leaf.Z), Math.Max(sz.Max, leaf.Z)) : (leaf.Z, leaf.Z);
        }

        var canopies = new Canopy[trunks.Count];
        for (var index = 0; index < trunks.Count; index++)
            canopies[index] = new Canopy(counts.GetValueOrDefault(index),
                species.TryGetValue(index, out var tally) ? tally : [],
                spanX.GetValueOrDefault(index), spanZ.GetValueOrDefault(index));
        return canopies;
    }
}
