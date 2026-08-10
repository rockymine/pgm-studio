using PgmStudio.Minecraft;

namespace PgmStudio.RoundTrip;

/// <summary>
/// Separates a world's trees from the timber built into its structures, and names each tree's species.
///
/// <para><b>Orientation alone cannot make the split.</b> A log carries its axis in the low bits of its data
/// value, and an all-bark log — the one with no cut end showing — is the natural choice for a trunk, so it is
/// tempting to call every oriented log structural. Branches break that immediately: a tree's limbs are
/// ordinary rotated logs growing out of an all-bark trunk, and on a map whose spruce and pine carry real
/// branches that rule discards a large part of every conifer.</para>
///
/// <para><b>Connectivity decides instead.</b> Logs are joined into components, and a component is a tree if it
/// contains an all-bark log or touches leaves — either is enough, since a bare trunk and a leafy sapling are
/// both trees. Branches arrive with their own trunk because they are attached to it, which is what makes the
/// rule robust where the per-block test is not. A component with neither mark is timber, and belongs to
/// whatever was built from it.</para>
///
/// <para><b>Species is read from the canopy, not the trunk.</b> An author is free to pair any trunk with any
/// leaf — pine built from acacia log under birch leaves is exactly such a pairing — so the leaves adjacent to
/// a component decide its species and the trunk material is recorded rather than trusted.</para>
/// </summary>
internal static class Flora
{
    private const int OakLog = 17;       // data & 3: 0 oak, 1 spruce, 2 birch, 3 jungle
    private const int AcaciaLog = 162;   // data & 1: 0 acacia, 1 dark oak
    private const int OakLeaves = 18;
    private const int AcaciaLeaves = 161;

    /// <summary>An all-bark log shows no cut end: both axis bits set.</summary>
    public static bool IsAllBark(int id, int data) => (id is OakLog or AcaciaLog) && (data >> 2 & 3) == 3;

    public static bool IsLog(int id) => id is OakLog or AcaciaLog;
    public static bool IsLeaf(int id) => id is OakLeaves or AcaciaLeaves;

    /// <summary>The species a leaf block belongs to. Only the low bits name the wood; the rest carry the
    /// decay and permanence flags, which say nothing about what kind of tree it is.</summary>
    public static string LeafSpecies(int id, int data) => id == OakLeaves
        ? (data & 3) switch { 0 => "oak", 1 => "spruce", 2 => "birch", _ => "jungle" }
        : (data & 1) == 0 ? "acacia" : "dark oak";

    public static string LogSpecies(int id, int data) => id == OakLog
        ? (data & 3) switch { 0 => "oak", 1 => "spruce", 2 => "birch", _ => "jungle" }
        : (data & 1) == 0 ? "acacia" : "dark oak";

    public sealed record Tree(string Species, string Trunk, IReadOnlyList<(int X, int Y, int Z)> Logs,
                              int LeafCount, int AllBark, int Branches, int MinX, int MaxX, int MinZ, int MaxZ,
                              int BaseY, int TopY);

    /// <summary><paramref name="Bare"/> holds the components that carry all-bark wood but no canopy at all.
    /// They are not trees and not plain timber: an all-bark post is exactly what an author reaches for when a
    /// pillar should show no cut end, so calling them trees would inflate the count with architecture, and
    /// calling them timber would hide a real planting choice. They are returned apart and left to be read —
    /// a run of them at one identical height is a structural element, a scatter of stumps is landscaping.</summary>
    public sealed record Result(IReadOnlyList<Tree> Trees, IReadOnlyList<Tree> Bare,
                                IReadOnlySet<(int X, int Y, int Z)> StructuralLogs);

    public static Result Classify(IEnumerable<AnvilRegion.Chunk> chunks)
    {
        var logs = new Dictionary<(int X, int Y, int Z), (int Id, int Data)>();
        var leaves = new Dictionary<(int X, int Y, int Z), (int Id, int Data)>();
        foreach (var chunk in chunks)
            foreach (var block in AnvilRegion.Blocks(chunk))
            {
                if (IsLog(block.Id)) logs[(block.X, block.Y, block.Z)] = (block.Id, block.Data);
                else if (IsLeaf(block.Id)) leaves[(block.X, block.Y, block.Z)] = (block.Id, block.Data);
            }

        var trees = new List<Tree>();
        var bare = new List<Tree>();
        var structural = new HashSet<(int X, int Y, int Z)>();
        var pending = new HashSet<(int X, int Y, int Z)>(logs.Keys);

        while (pending.Count > 0)
        {
            var seed = pending.First();
            pending.Remove(seed);
            var component = new List<(int X, int Y, int Z)>();
            var queue = new Queue<(int X, int Y, int Z)>([seed]);
            var canopy = new Dictionary<string, int>();

            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                component.Add(cell);
                // Leaves are counted within one block of the wood in every direction: a branch tip sits
                // diagonally inside its own canopy as often as it sits square against it.
                for (var dy = -1; dy <= 1; dy++)
                    for (var dz = -1; dz <= 1; dz++)
                        for (var dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0 && dz == 0) continue;
                            var next = (cell.X + dx, cell.Y + dy, cell.Z + dz);
                            if (leaves.TryGetValue(next, out var leaf))
                            {
                                var leafKind = LeafSpecies(leaf.Id, leaf.Data);
                                canopy[leafKind] = canopy.GetValueOrDefault(leafKind) + 1;
                            }
                            // Wood joins only face to face, so two trunks touching at a corner stay apart.
                            if ((dx == 0 ? 0 : 1) + (dy == 0 ? 0 : 1) + (dz == 0 ? 0 : 1) == 1 && pending.Remove(next))
                                queue.Enqueue(next);
                        }
            }

            var allBark = component.Count(cell => IsAllBark(logs[cell].Id, logs[cell].Data));
            if (allBark == 0 && canopy.Count == 0) { foreach (var cell in component) structural.Add(cell); continue; }

            var species = canopy.Count > 0
                ? canopy.OrderByDescending(entry => entry.Value).First().Key
                : LogSpecies(logs[component[0]].Id, logs[component[0]].Data);
            var trunk = component.Where(cell => IsAllBark(logs[cell].Id, logs[cell].Data))
                .Select(cell => LogSpecies(logs[cell].Id, logs[cell].Data))
                .GroupBy(name => name).OrderByDescending(group => group.Count()).FirstOrDefault()?.Key
                ?? LogSpecies(logs[component[0]].Id, logs[component[0]].Data);

            var found = new Tree(species, trunk, component, canopy.Values.Sum(), allBark, component.Count - allBark,
                component.Min(cell => cell.X), component.Max(cell => cell.X),
                component.Min(cell => cell.Z), component.Max(cell => cell.Z),
                component.Min(cell => cell.Y), component.Max(cell => cell.Y));
            (canopy.Count > 0 ? trees : bare).Add(found);
        }

        return new Result(trees, bare, structural);
    }
}
