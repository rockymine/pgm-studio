using PgmStudio.Domain;
using PgmStudio.Geom;

namespace PgmStudio.Analysis.Suggest;

/// <summary>
/// One destroyable proposed from the world: the structure's block box, what it is made of, and the two
/// neighbourhood readings that proposed it.
/// </summary>
/// <param name="Structure">The connected mass's bounding box — the blocks themselves, never a region drawn
/// around them (OB12). A confirmed suggestion carries this box into the intent.</param>
/// <param name="Materials">Which of the four buildable materials it is, as
/// <see cref="DestroyableMaterials.All"/> spells it.</param>
/// <param name="Blocks">How many blocks the mass holds. Descriptive, never a filter: the corpus spans one
/// block to 31,105 and any cap discards truth.</param>
/// <param name="SameNearby">Same-material blocks in the neighbourhood, excluding the mass itself, counted to
/// <see cref="SameNearbyCap"/> and no further. The isolation signal: decoration repeats, a goal is placed
/// once.</param>
/// <param name="Elevation">Blocks between the mass's underside and the median terrain height of the ring
/// around it. The prominence signal.</param>
public sealed record DestroyableSuggestion(
    BlockBox Structure, string Materials, int Blocks, int SameNearby, int Elevation);

/// <summary>
/// Proposes DTM destroyables from a world by what surrounds them rather than by what they are.
///
/// <para><b>The structure says almost nothing.</b> Size spans four orders of magnitude — median 8 blocks, p90
/// 120, and a 31,105-block maximum that is a real declared goal — so any size cap throws away truth. Fill is
/// uninformative for the same reason a single block fills its own bounding box perfectly. Support is bimodal:
/// 353 of 614 declared structures rest fully on something and 163 hover, so "destroyables float" is the
/// generator's default rather than a property to detect on.</para>
///
/// <para><b>Two neighbourhood readings separate a goal from decoration</b>, measured over 614 declared
/// structures across 223 maps (<c>docs/world-scan/objective-suggestion.md</c> §3).
/// <see cref="DestroyableSuggestion.SameNearby"/> is isolation: a declared destroyable has a median of 6
/// same-material blocks within <see cref="NeighbourhoodRadius"/>, against 65+ for a false cluster, because a
/// material chosen for a wall or a floor appears again immediately while a goal is placed once. This is what
/// dissolves the pathological maps, where hundreds of ender-stone clusters are surrounded by each other.
/// <see cref="DestroyableSuggestion.Elevation"/> is prominence: a declared structure sits a median +5 blocks
/// above the ring of terrain around it, against −2 for a false cluster.</para>
///
/// <para><b>The operating point is a confirm flow's, not a gate's.</b>
/// <see cref="MaxSameNearby"/> ≤ 8 with <see cref="MinElevation"/> ≥ +2 keeps 553 of 1,062 true clusters
/// against 600 false — about one true proposal in two, and a four-fold precision gain on the previous best.
/// A stricter pair (same ≤ 0) reaches 65.6% precision and is the wrong trade for a surface whose whole job is
/// to be confirmed by a person.</para>
///
/// <para><b>Four materials, and the ceiling is 84%.</b> The set is
/// <see cref="DestroyableMaterials.All"/> — the same four the generator builds — because those carry 84% of
/// declared destroyables. Wool, stained clay and stained glass carry another 8% and stay out: admitting wool
/// takes the candidate set from 15,488 clusters to 439,440, because a CTW map is largely made of wool. A
/// material a map is built from cannot mark a goal inside it, so those are unreachable by this method and 84%
/// is the honest ceiling.</para>
///
/// <para><b>Gather, not serve.</b> This runs once, inside the single ingest pass, beside
/// <see cref="CoreSuggester.Gather"/> and <see cref="MonumentSuggester.Gather"/>. The world is discarded
/// afterwards, so a suggestion not captured then cannot be recovered without re-importing the map.</para>
/// </summary>
public static class DestroyableSuggester
{
    /// <summary>How far the neighbourhood reaches: ten blocks outward on each horizontal axis and ten up,
    /// and all the way down to bedrock, which is the volume the corpus reading was taken over.</summary>
    public const int NeighbourhoodRadius = 10;

    /// <summary>Where the isolation count stops. Past this the answer is the same — "this material is
    /// everywhere here" — and counting further is work for a number nothing reads.</summary>
    public const int SameNearbyCap = 65;

    /// <summary>The isolation an accepted proposal is at or under.</summary>
    public const int MaxSameNearby = 8;

    /// <summary>The prominence an accepted proposal is at or above, in blocks over the ring's median.</summary>
    public const int MinElevation = 2;

    /// <summary>How close two masses of one material have to be before they are one proposal rather than two.
    ///
    /// <para>This is not a test of whether either is a goal — a display pair obeys a map's symmetry and sits
    /// as far from the centreline as the real thing, and both a minimum-separation rule and a mirror-partner
    /// rule were measured against the corpus and cost multiples of the recall they bought
    /// (<c>docs/world-scan/objective-suggestion.md</c> §3). What it removes is <b>duplication</b>: a
    /// structure that clusters into several masses is proposed several times, and a list that offers the same
    /// goal four ways is a worse list to confirm from. Collapsing at 16 leaves coverage untouched, drops the
    /// proposals from 125 to 101 over the sample, and lands at exactly one proposal per declared
    /// structure.</para></summary>
    public const int MinSeparation = 16;

    private static readonly (int X, int Y, int Z)[] Faces =
        [(1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1)];

    /// <summary>Propose every destroyable in a decoded world (block position → id, air absent).</summary>
    public static List<DestroyableSuggestion> Gather(IReadOnlyDictionary<(int X, int Y, int Z), int> world)
    {
        var surface = Surface(world);
        var found = new List<DestroyableSuggestion>();

        foreach (var material in DestroyableMaterials.All)
        {
            var id = DestroyableMaterials.BlockId(material);
            var cells = world.Where(entry => entry.Value == id).Select(entry => entry.Key).ToHashSet();
            if (cells.Count == 0) continue;

            // The material's own cells, bucketed by chunk column, so the isolation count reads the
            // neighbourhood rather than every block of that material in the map.
            var buckets = new Dictionary<(int X, int Z), List<(int X, int Y, int Z)>>();
            foreach (var cell in cells)
            {
                var key = (cell.X >> 4, cell.Z >> 4);
                if (!buckets.TryGetValue(key, out var bucket)) buckets[key] = bucket = [];
                bucket.Add(cell);
            }

            var visited = new HashSet<(int X, int Y, int Z)>();
            foreach (var start in cells.OrderBy(c => c.Y).ThenBy(c => c.X).ThenBy(c => c.Z))
            {
                if (!visited.Add(start)) continue;
                var mass = Flood(start, cells, visited);
                var box = BoxOf(mass);

                var same = SameWithin(box, mass, buckets);
                if (same > MaxSameNearby) continue;

                var elevation = box.MinY - RingHeight(box, surface);
                if (elevation < MinElevation) continue;

                found.Add(new DestroyableSuggestion(box, material, mass.Count, same, elevation));
            }
        }
        return Deduplicate(found);
    }

    /// <summary>One proposal per structure: where several masses of one material sit within
    /// <see cref="MinSeparation"/> of each other, the most isolated of them stands for the group and the rest
    /// are dropped. Isolation decides because it is the reading the whole detector turns on; height over the
    /// ring breaks a tie.</summary>
    private static List<DestroyableSuggestion> Deduplicate(List<DestroyableSuggestion> found)
    {
        var dropped = new HashSet<int>();
        for (var i = 0; i < found.Count; i++)
        {
            if (dropped.Contains(i)) continue;
            for (var j = i + 1; j < found.Count; j++)
            {
                if (dropped.Contains(j) || found[i].Materials != found[j].Materials) continue;
                if (Apart(found[i].Structure, found[j].Structure) >= MinSeparation) continue;
                var keepI = found[i].SameNearby != found[j].SameNearby
                    ? found[i].SameNearby < found[j].SameNearby
                    : found[i].Elevation >= found[j].Elevation;
                dropped.Add(keepI ? j : i);
                if (!keepI) break;
            }
        }
        return [.. found.Where((_, i) => !dropped.Contains(i))];
    }

    private static double Apart(BlockBox a, BlockBox b)
    {
        double dx = (a.MinX + a.MaxX) / 2.0 - (b.MinX + b.MaxX) / 2.0;
        double dy = (a.MinY + a.MaxY) / 2.0 - (b.MinY + b.MaxY) / 2.0;
        double dz = (a.MinZ + a.MaxZ) / 2.0 - (b.MinZ + b.MaxZ) / 2.0;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    /// <summary>Topmost block per column. Air is absent from a reading, so the highest key a column holds is
    /// its surface; a column the world does not reach has none.</summary>
    private static Dictionary<(int X, int Z), int> Surface(IReadOnlyDictionary<(int X, int Y, int Z), int> world)
    {
        var top = new Dictionary<(int X, int Z), int>();
        foreach (var (cell, _) in world)
        {
            var column = (cell.X, cell.Z);
            if (!top.TryGetValue(column, out var y) || cell.Y > y) top[column] = cell.Y;
        }
        return top;
    }

    private static List<(int X, int Y, int Z)> Flood((int X, int Y, int Z) start,
                                                     HashSet<(int X, int Y, int Z)> cells,
                                                     HashSet<(int X, int Y, int Z)> visited)
    {
        var mass = new List<(int X, int Y, int Z)> { start };
        var queue = new Queue<(int X, int Y, int Z)>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            foreach (var face in Faces)
            {
                var next = (cell.X + face.X, cell.Y + face.Y, cell.Z + face.Z);
                if (cells.Contains(next) && visited.Add(next)) { mass.Add(next); queue.Enqueue(next); }
            }
        }
        return mass;
    }

    private static BlockBox BoxOf(List<(int X, int Y, int Z)> mass) => new(
        mass.Min(c => c.X), mass.Min(c => c.Y), mass.Min(c => c.Z),
        mass.Max(c => c.X), mass.Max(c => c.Y), mass.Max(c => c.Z));

    /// <summary>Same-material blocks in the neighbourhood that are not the mass itself, counted no further
    /// than <see cref="SameNearbyCap"/>.</summary>
    private static int SameWithin(BlockBox box, List<(int X, int Y, int Z)> mass,
                                  Dictionary<(int X, int Z), List<(int X, int Y, int Z)>> buckets)
    {
        var own = mass.ToHashSet();
        int minX = box.MinX - NeighbourhoodRadius, maxX = box.MaxX + NeighbourhoodRadius;
        int minZ = box.MinZ - NeighbourhoodRadius, maxZ = box.MaxZ + NeighbourhoodRadius;
        var maxY = box.MaxY + NeighbourhoodRadius;   // downward the neighbourhood runs to bedrock

        var same = 0;
        for (var bx = minX >> 4; bx <= maxX >> 4; bx++)
            for (var bz = minZ >> 4; bz <= maxZ >> 4; bz++)
            {
                if (!buckets.TryGetValue((bx, bz), out var bucket)) continue;
                foreach (var cell in bucket)
                {
                    if (cell.X < minX || cell.X > maxX || cell.Z < minZ || cell.Z > maxZ || cell.Y > maxY) continue;
                    if (own.Contains(cell)) continue;
                    if (++same >= SameNearbyCap) return same;
                }
            }
        return same;
    }

    /// <summary>The median terrain height of the ring around the mass — the columns the neighbourhood covers
    /// that the mass's own footprint does not, so the structure cannot raise the ground it is measured
    /// against. A ring the world does not reach reads as the mass's own underside, which is an elevation of
    /// zero rather than a proposal made on nothing.</summary>
    private static int RingHeight(BlockBox box, Dictionary<(int X, int Z), int> surface)
    {
        var heights = new List<int>();
        for (var x = box.MinX - NeighbourhoodRadius; x <= box.MaxX + NeighbourhoodRadius; x++)
            for (var z = box.MinZ - NeighbourhoodRadius; z <= box.MaxZ + NeighbourhoodRadius; z++)
            {
                if (x >= box.MinX && x <= box.MaxX && z >= box.MinZ && z <= box.MaxZ) continue;
                if (surface.TryGetValue((x, z), out var y)) heights.Add(y);
            }
        if (heights.Count == 0) return box.MinY;
        heights.Sort();
        return heights[heights.Count / 2];
    }
}
