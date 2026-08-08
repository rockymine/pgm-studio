using PgmStudio.Domain;
using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Pgm.Detect;

/// <summary>How a water lane is wired — the four shapes the corpus authors it in, newest first among the
/// ones that open mid-match. The generation says which elements carry the mechanism, not what the lane looks
/// like: the footprint is a set of <c>y=0</c> rectangles in every one of them.</summary>
public enum WaterLaneForm
{
    /// <summary>An <c>&lt;include id="water-lanes"/&gt;</c> plus a region under the matching id. The map
    /// states only where the lanes are; the shared fragment supplies every rule.</summary>
    Include,

    /// <summary>A <c>&lt;fill material="water"&gt;</c> action over a <c>y=0</c> region, fired by a
    /// trigger.</summary>
    Fill,

    /// <summary>A hidden destroyable over a <c>y=0</c> region whose mode swaps it to water at a match time —
    /// the oldest form, and the reason a water lane reads as a DTM objective until the phantom is
    /// classified.</summary>
    Phantom,

    /// <summary>A region under the lane naming convention that nothing in the map ever fills, swaps or
    /// applies. It carries water already and opens nothing: the id exempts it from the void rule so draining
    /// it does not make its columns unbuildable. Named by convention alone, so it is the one form detected
    /// from a name rather than a mechanism.</summary>
    Static,
}

/// <summary>One detected water lane: the region that holds it, how it is wired, when it opens, and the
/// <c>y=0</c> rectangles it covers.</summary>
/// <param name="Form">Which of the four wirings carries this lane.</param>
/// <param name="RegionId">The region the mechanism names. Empty when the lane's region was declared inline
/// (a destroyable may hold its cuboids directly rather than reference an id).</param>
/// <param name="Trigger">When the lane opens — a mode's <c>after</c> duration, the filter or countdown a
/// fill's trigger reads, or for the shared fragment the <c>water-lane-timer</c> constant the map set, falling
/// back to the fragment's own default. Empty only when the map states the condition in a form this parser
/// does not read.</param>
/// <param name="Footprint">The <c>y=0</c> rectangles the lane covers, in map coordinates.</param>
/// <param name="Evidence">The element that carried the verdict (a mode id, a fill id/region, the include).</param>
public sealed record WaterLane(
    WaterLaneForm Form,
    string RegionId,
    string Trigger,
    IReadOnlyList<Rect> Footprint,
    string Evidence);

/// <summary>
/// Finds a map's water lanes — routes that open <b>mid-match</b> when a gap between islands becomes
/// bridgeable, adding a late way to the wool.
///
/// <para>The mechanism is PGM's <c>VoidFilter</c>, and everything here follows from how it reads: a column is
/// void iff the block at <c>(x, 0, z)</c> is air, evaluated at query time rather than at load. So
/// <c>&lt;apply block="deny(void)"&gt;</c> stops applying to a column the instant anything lands at
/// <c>y=0</c>. Fill that layer with water part-way through a match and a gap nobody could bridge becomes
/// buildable. That is why the geometric test is <b>a cuboid spanning y=0</b> and nothing else: a region that
/// misses the void layer cannot open a route however it is filled, and a water fill high above the floor
/// (a decorative pool, a flag-status indicator) is not a lane no matter what it is called.</para>
///
/// <para>The wirings are ranked, and a region is reported once under the strongest that claims it
/// (<see cref="WaterLaneForm"/> declaration order). A map that both fills a region and names it under the
/// convention is a fill, not a static lane.</para>
///
/// <para>Two of the four need parser surface the studio holds only for reading:
/// <see cref="MapXml.Includes"/> and <see cref="MapXml.Fills"/>. Neither resolves a shared fragment's body —
/// see <c>docs/contracts/water-lanes.md</c>.</para>
/// </summary>
public static class WaterLaneDetector
{
    /// <summary>The agreed id of the shared fragment, and of the region that keys it. The fragment is
    /// resolved server-side out of the includes directory, so this string is the whole of the contract
    /// between a map and the behaviour it pulls in.</summary>
    public const string LaneInclude = "water-lanes";

    /// <summary>The constant the shared fragment exposes for when its lanes open. It declares the knob as a
    /// <c>fallback</c>, so a map that names it overrides the fragment; a map that does not gets
    /// <see cref="LaneTimerDefault"/>.</summary>
    public const string LaneTimerConstant = "water-lane-timer";

    /// <summary>The fragment's own default for <see cref="LaneTimerConstant"/>. It lives in the fragment
    /// rather than here, so this is a copy of a value the studio does not own — reported as the default it is,
    /// never as something the map said.</summary>
    public const string LaneTimerDefault = "45m";

    // The lane naming convention, as the corpus writes it: `water-lanes`, `water-lane-regions`,
    // `blue-water-lanes`, `orange-water-lane`. Matching on "water" + "lane" together keeps `wool-lanes` and
    // `side-lanes` out — both exist in maps that also have real lanes, and neither is one.
    private static bool IsLaneName(string id) =>
        id.Contains("water", StringComparison.OrdinalIgnoreCase)
        && id.Contains("lane", StringComparison.OrdinalIgnoreCase);

    // A mode/fill target counts as water when the material match names water. PGM material matches are
    // ';'-separated `name[:data]` patterns, and a phantom's own materials list the blocks being replaced
    // (`air`, or `air;water` once the author allows the swap to re-run), so only the mode's target is read.
    private static bool IsWater(string material) =>
        material.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Any(part => part.Split(':')[0].Trim().Equals("water", StringComparison.OrdinalIgnoreCase));

    /// <summary>Detect every water lane in a parsed map, strongest wiring first. Empty for the great majority
    /// of maps — a lane is a deliberate late-game route, not incidental water.</summary>
    public static IReadOnlyList<WaterLane> Detect(MapXml map)
    {
        var lanes = new List<WaterLane>();
        // A claim covers the region named and everything under it, so a wiring that names a union claims the
        // cuboids inside it too and a weaker wiring cannot re-report the same ground.
        var claimed = new HashSet<string>(StringComparer.Ordinal);

        void Claim(WaterLane lane)
        {
            if (lane.RegionId.Length == 0) { lanes.Add(lane); return; }   // declared inline; nothing to collide with
            if (!claimed.Add(lane.RegionId)) return;
            foreach (var id in Subtree(map.Regions, lane.RegionId)) claimed.Add(id);
            lanes.Add(lane);
        }

        foreach (var lane in FromInclude(map)) Claim(lane);
        foreach (var lane in FromFills(map)) Claim(lane);
        foreach (var lane in FromPhantoms(map)) Claim(lane);

        // Naming is last and weakest, and it yields two further ways. To a claim from *below*: a union named
        // after the convention whose lanes are filled one by one is that map's fill wiring, not a second static
        // lane. And to a claim on the same ground: a lane commonly carries a flat companion region covering the
        // identical footprint under a near-identical name, which is one lane described twice, not two.
        var claimedGround = lanes.SelectMany(lane => lane.Footprint).ToList();
        foreach (var lane in FromNaming(map))
        {
            if (Subtree(map.Regions, lane.RegionId).Any(claimed.Contains)) continue;
            if (lane.Footprint.All(r => claimedGround.Any(c => Overlaps(r, c)))) continue;
            Claim(lane);
        }
        return lanes;
    }

    /// <summary>Every region id reachable beneath one, transforms and composites alike, excluding the root.</summary>
    private static HashSet<string> Subtree(IReadOnlyDictionary<string, Region> registry, string rootId)
    {
        var found = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>();
        queue.Enqueue(rootId);
        while (queue.Count > 0)
        {
            if (!registry.TryGetValue(queue.Dequeue(), out var region)) continue;
            foreach (var childId in (region.Children ?? []).Concat(region.SourceId is { } s ? [s] : []))
                if (found.Add(childId)) queue.Enqueue(childId);
        }
        found.Remove(rootId);
        return found;
    }

    /// <summary>Whether the map has any water lane at all.</summary>
    public static bool Any(MapXml map) => Detect(map).Count > 0;

    // ── the four wirings ────────────────────────────────────────────────────────

    // The include names the behaviour and the region under the same id names the place. Both are required:
    // the include alone applies to nothing, and the region alone is the static form.
    private static IEnumerable<WaterLane> FromInclude(MapXml map)
    {
        if (!map.Includes.Contains(LaneInclude, StringComparer.Ordinal)) yield break;
        if (!map.Regions.TryGetValue(LaneInclude, out var region)) yield break;

        // The fragment owns the timing but exposes it as an overridable constant, so the answer is in the
        // document after all: the map's own value when it declares one, the fragment's default otherwise.
        var stated = map.Constants.GetValueOrDefault(LaneTimerConstant);
        var opens = string.IsNullOrWhiteSpace(stated) ? LaneTimerDefault : stated.Trim();
        var evidence = stated is null ? $"include:{LaneInclude} (default)" : $"include:{LaneInclude}";

        yield return new WaterLane(WaterLaneForm.Include, LaneInclude, opens,
                                   VoidLayerRects(map.Regions, region), evidence);
    }

    private static IEnumerable<WaterLane> FromFills(MapXml map)
    {
        foreach (var fill in map.Fills)
        {
            if (!IsWater(fill.Material)) continue;
            if (!map.Regions.TryGetValue(fill.RegionId, out var region)) continue;
            var rects = VoidLayerRects(map.Regions, region);
            if (rects.Count == 0) continue;         // water above the floor opens no route

            var evidence = fill.Id.Length > 0 ? $"fill:{fill.Id}" : $"fill:{fill.RegionId}";
            yield return new WaterLane(WaterLaneForm.Fill, fill.RegionId, fill.Trigger, rects, evidence);
        }
    }

    // A phantom destroyable is a scripted block swap wearing a goal's element (`show="false"`). It is a lane
    // when one of its modes turns it to water and its region reaches the void layer. The modes are the
    // destroyable's own set, or every mode when it opted into all of them.
    private static IEnumerable<WaterLane> FromPhantoms(MapXml map)
    {
        foreach (var destroyable in map.Destroyables)
        {
            if (destroyable.Phantom != PhantomKind.BlockSwap) continue;

            var modes = destroyable.ModeChanges
                ? map.Modes
                : map.Modes.Where(m => destroyable.Modes?.Contains(m.Id) == true).ToList();
            if (modes.FirstOrDefault(m => IsWater(m.Material)) is not { } waterMode) continue;

            if (!map.Regions.TryGetValue(destroyable.RegionId, out var region)) continue;
            var rects = VoidLayerRects(map.Regions, region);
            if (rects.Count == 0) continue;

            yield return new WaterLane(WaterLaneForm.Phantom, destroyable.RegionId, waterMode.After, rects,
                                       $"mode:{waterMode.Id}");
        }
    }

    // The last resort, and the only one that reads a name: a region called after the convention that no
    // mechanism above claimed. It opens nothing — it exempts standing water from the void rule — so it is
    // reported as its own form rather than folded in with the three that do.
    private static IEnumerable<WaterLane> FromNaming(MapXml map)
    {
        // Only the outermost lane-named region of a tree: a union of `*-water-lane` cuboids must report once,
        // not once for the union and again per leaf. Nesting under a region named something else — the void
        // negative that every static lane sits in — is exactly how the form is written and never suppresses it.
        var nested = map.Regions
            .Where(kv => IsLaneName(kv.Key))
            .SelectMany(kv => Subtree(map.Regions, kv.Key))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var (id, region) in map.Regions)
        {
            if (!IsLaneName(id) || nested.Contains(id)) continue;
            var rects = VoidLayerRects(map.Regions, region);
            if (rects.Count == 0) continue;
            yield return new WaterLane(WaterLaneForm.Static, id, "", rects, $"region:{id}");
        }
    }

    // ── the void-layer footprint ────────────────────────────────────────────────

    /// <summary>The <c>y=0</c> rectangles under a region: every cuboid leaf in its tree whose Y range covers
    /// the void layer, plus every flat rectangle (a rectangle is unbounded in Y, so it always covers it).
    /// Empty means the region cannot open a route — which is the whole test.</summary>
    public static IReadOnlyList<Rect> VoidLayerRects(IReadOnlyDictionary<string, Region> registry, Region root) =>
        Walk(registry, root, 0, []);

    // `shiftY` accumulates the Y shift of enclosing translates, so a translated cuboid is tested where it
    // actually sits. `path` guards a cyclic reference without collapsing a legitimate second visit: a mirrored
    // lane names the same source as the lane it reflects, and the two are different places.
    private static List<Rect> Walk(IReadOnlyDictionary<string, Region> registry, Region region,
                                   double shiftY, HashSet<string> path)
    {
        var rects = new List<Rect>();
        if (region.Id.Length > 0 && !path.Add(region.Id)) return rects;

        switch (region.Type)
        {
            case "cuboid":
                if (CoversVoidLayer(region, shiftY)) rects.Add(RectOf(region));
                break;

            case "rectangle":
                rects.Add(RectOf(region));
                break;

            case "union" or "intersect" or "complement" or "negative":
                // Only the first child of a complement/negative is additive — the rest are subtracted, and a
                // lane is never the hole cut out of something else.
                var children = region.Children ?? [];
                var additive = region.Type is "union" or "intersect" ? children : children.Take(1);
                foreach (var childId in additive)
                    if (registry.TryGetValue(childId, out var child))
                        rects.AddRange(Walk(registry, child, shiftY, [.. path]));
                break;

            // A transform is its own place, not another visit to its source: reflecting a lane produces a
            // second lane somewhere else. The source is walked only to ask whether it reaches the void layer,
            // and what gets recorded is the transform's own derived footprint.
            case "mirror" or "translate":
                if (region.SourceId is not { } sourceId || !registry.TryGetValue(sourceId, out var source)) break;
                var offsetY = region.Type == "translate" ? region.OffsetY ?? 0 : 0;
                if (Walk(registry, source, shiftY + offsetY, [.. path]).Count > 0) rects.Add(RectOf(region));
                break;
        }
        return rects;
    }

    // The void layer is the single block plane at y=0. A cuboid covers it when its Y span contains 0 —
    // PGM cuboid bounds are [min, max), so a `min="…,0,…" max="…,1,…"` cuboid is exactly that one layer.
    private static bool CoversVoidLayer(Region cuboid, double dy)
    {
        var minY = (cuboid.MinY ?? double.NegativeInfinity) + dy;
        var maxY = (cuboid.MaxY ?? double.PositiveInfinity) + dy;
        return Math.Min(minY, maxY) <= 0 && 0 < Math.Max(minY, maxY);
    }

    // Shared area, not a shared edge — two lanes meeting along a border are still two lanes.
    private static bool Overlaps(Rect a, Rect b) =>
        Math.Min(a.MaxX, b.MaxX) > Math.Max(a.MinX, b.MinX)
        && Math.Min(a.MaxZ, b.MaxZ) > Math.Max(a.MinZ, b.MinZ);

    private static Rect RectOf(Region region)
    {
        var bounds = region.Bounds2d;
        if (bounds is not null) return new Rect(bounds.MinX, bounds.MinZ, bounds.MaxX, bounds.MaxZ);
        double minX = region.MinX ?? 0, minZ = region.MinZ ?? 0, maxX = region.MaxX ?? 0, maxZ = region.MaxZ ?? 0;
        return new Rect(Math.Min(minX, maxX), Math.Min(minZ, maxZ), Math.Max(minX, maxX), Math.Max(minZ, maxZ));
    }
}
