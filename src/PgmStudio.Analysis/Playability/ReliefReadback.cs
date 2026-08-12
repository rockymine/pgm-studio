namespace PgmStudio.Analysis.Playability;

using PgmStudio.Geom.Algorithms;
using PgmStudio.Geom.Relief;

/// <summary>
/// What a solved surface <b>charges</b>, rather than how far it is from flat.
///
/// <para>A relief that is walkable everywhere is a field, not a map. The reason to author elevation is that
/// some ground should be harder to reach, so a single walkability score answers the wrong question: it ranks
/// every deliberate barrier as a defect and every plain as ideal. What decides play is that the game's
/// movement has <b>three thresholds</b>, not one — a step of 0–1 is a jump, a step of 2 costs a placed block
/// (slow, and the player stands still in the open to do it), and 3 or more is building in earnest and is not
/// crossed in a fight. None of the three is a fault and a good map is a mix, so every measure here is
/// reported <em>per tier</em>.</para>
///
/// <para>It lives in Analysis rather than beside the solver because the readings are not geometry. Whether a
/// face counts as a cliff, and whether a patch of ground counts as somewhere players go, are corpus rules
/// about play — the same reason the other derivations that read a surface live here.</para>
/// </summary>
public static class ReliefReadback
{
    /// <summary>The step a player crosses freely, and the one they cross by placing a block. Above the second
    /// is a face. These are the game's numbers, not a tuning choice.</summary>
    public const int WalkStep = 1;
    public const int ScrambleStep = 2;

    /// <summary>The share of the ground a connected region has to hold before it is a <b>place</b> rather than
    /// a <b>ledge</b>. Counting them together is what turns "one connected map with twenty cliff-top ledges"
    /// into a meaningless "twenty-one pieces" — and the distinction is the difference between a relief that
    /// added pressure and one that broke the map.</summary>
    public const double PlaceShare = 0.01;

    /// <summary>How wide a face has to be, and how far it has to fall, to count as a <b>cliff</b> rather than
    /// as a bank. A monolith's 8-wide face at a big drop is a structure; a landform is wider than it is
    /// tall (rules.md EL6).</summary>
    public const int CliffWidth = 10;
    public const int CliffDrop = 6;

    /// <summary>How a surface reads at one passability tier: what share of it a player can cross, how many
    /// <b>places</b> that leaves, how much of the ground the largest holds, and how many <b>ledges</b> are
    /// stranded off it.</summary>
    public sealed record Tier(string Name, int MaxStep, double Share, int Places, double LargestPlace, int Ledges);

    /// <summary>One face the terrain presents: which way it looks, how wide it runs, and how far it drops. A
    /// cliff is the qualifying subset, judged by the corpus rule rather than by the author's intent.</summary>
    public sealed record Face(string Facing, int Width, int Drop, bool Cliff);

    /// <summary>What a barrier costs to cross in one direction. A drop is not a barrier — a player walks off
    /// a ledge and takes the fall — so a face that stops a crossing one way lets it through the other, and
    /// measuring only one direction cannot tell a wall from a gradient of pressure.</summary>
    public sealed record Fords(int Rows, int OnFoot, int WithBlock, int Descended);

    public sealed record Result(
        int Cells, int Low, int High, int Relief,
        IReadOnlyList<Tier> Tiers,
        IReadOnlyDictionary<string, int> Steps,
        IReadOnlyList<Face> Faces, int Cliffs,
        Fords AcrossX, Fords AcrossZ,
        int SymmetryError);

    /// <summary>Reads a solved field. <paramref name="foldCentreX"/>/<paramref name="foldCentreZ"/> and
    /// <paramref name="foldMode"/> are the map's symmetry, so the report can say whether the two halves
    /// actually agree — a relief that is unfair is unfair invisibly, since nothing else looks wrong.</summary>
    public static Result Read(HeightField field, string? foldMode = null,
                              double foldCentreX = 0, double foldCentreZ = 0)
    {
        var footprint = field.Footprint;
        var land = footprint.Land().ToList();
        if (land.Count == 0)
            return new Result(0, 0, 0, 0, [], new Dictionary<string, int>(), [], 0,
                new Fords(0, 0, 0, 0), new Fords(0, 0, 0, 0), 0);

        var low = land.Min(cell => field.At(cell.X, cell.Z));
        var high = land.Max(cell => field.At(cell.X, cell.Z));

        // The step histogram: how far the ground moves between one cell and the next, over every orthogonal
        // pair. It is what every tier below is a cut of, so it is reported raw as well.
        var steps = new Dictionary<string, int>();
        var faces = new List<Face>();
        foreach (var (x, z) in land)
            foreach (var (dx, dz) in GridComponents.N4)
            {
                if (dx + dz < 0 || !footprint.Inside(x + dx, z + dz)) continue;   // each pair once
                var step = Math.Abs(field.At(x, z) - field.At(x + dx, z + dz));
                var bucket = step <= WalkStep ? "walk" : step <= ScrambleStep ? "scramble" : "barrier";
                steps[bucket] = steps.GetValueOrDefault(bucket) + 1;
            }

        var tiers = new List<Tier>
        {
            TierAt("walk", WalkStep, field, land),
            TierAt("scramble", ScrambleStep, field, land),
        };

        // Faces: the runs of barrier-stepping edges, joined into one face where they touch, so a bank reads
        // as one thing rather than as its several hundred cell boundaries.
        foreach (var run in FaceRuns(field, land)) faces.Add(run);
        var cliffs = faces.Count(face => face.Cliff);

        return new Result(land.Count, low, high, high - low, tiers, steps, faces, cliffs,
            FordsAlong(field, footprint, alongX: true),
            FordsAlong(field, footprint, alongX: false),
            SymmetryError(field, foldMode, foldCentreX, foldCentreZ));
    }

    /// <summary>The surface as one tier sees it: cells joined to their neighbours only where the step between
    /// them is within reach.</summary>
    private static Tier TierAt(string name, int maxStep, HeightField field, List<(int X, int Z)> land)
    {
        var footprint = field.Footprint;
        var reachable = new HashSet<(int, int)>(land);
        var components = GridComponents.Label(reachable, 4, (from, to) =>
            Math.Abs(field.At(from.X, from.Z) - field.At(to.X, to.Z)) <= maxStep);

        var total = (double)land.Count;
        var places = components.Where(part => part.Count >= total * PlaceShare).ToList();
        var largest = components.Count == 0 ? 0 : components.Max(part => part.Count) / total;

        // The share a player can cross: the boundaries within reach, over all of them. That is what "how much
        // of this surface is open at this tier" means — not how many cells there are.
        var within = 0; var all = 0;
        foreach (var (x, z) in land)
            foreach (var (dx, dz) in GridComponents.N4)
            {
                if (dx + dz < 0 || !footprint.Inside(x + dx, z + dz)) continue;
                all++;
                if (Math.Abs(field.At(x, z) - field.At(x + dx, z + dz)) <= maxStep) within++;
            }

        return new Tier(name, maxStep, all == 0 ? 1 : within / (double)all,
                        places.Count, largest, components.Count - places.Count);
    }

    /// <summary>
    /// Every face the terrain presents: the cells whose ground falls away by more than a scramble, grouped
    /// by <b>which way they look</b> and then joined where they touch.
    ///
    /// <para>The grouping by facing is what makes the width mean anything. A face is a thing that faces a
    /// direction, and joining the brink without regard to direction wraps a small block's four sides into one
    /// run — a 6×6 monolith then measures twenty cells "wide" and qualifies as a cliff, which is exactly the
    /// call the rule exists to get right. Split by facing, its four faces are six wide each and none of them
    /// is a landform.</para>
    ///
    /// <para>The drop is the largest anywhere along the run, because what makes a face a cliff is its worst
    /// crossing rather than its average.</para>
    /// </summary>
    private static List<Face> FaceRuns(HeightField field, List<(int X, int Z)> land)
    {
        var footprint = field.Footprint;
        var byFacing = new Dictionary<string, (HashSet<(int, int)> Cells, Dictionary<(int, int), int> Drop)>();

        foreach (var (x, z) in land)
            foreach (var ((dx, dz), facing) in Facings)
            {
                if (!footprint.Inside(x + dx, z + dz)) continue;
                var drop = field.At(x, z) - field.At(x + dx, z + dz);      // downhill only
                if (drop <= ScrambleStep) continue;
                if (!byFacing.TryGetValue(facing, out var group))
                    byFacing[facing] = group = ([], []);
                group.Cells.Add((x, z));
                group.Drop[(x, z)] = Math.Max(group.Drop.GetValueOrDefault((x, z)), drop);
            }

        var faces = new List<Face>();
        foreach (var (facing, group) in byFacing)
            foreach (var run in GridComponents.Label(group.Cells))
            {
                // A face's WIDTH is how far it runs across, which for a run all looking one way is its extent
                // along the perpendicular axis — not how many cells it holds, since a ragged face two cells
                // deep would otherwise measure twice its own width.
                var eastWest = facing is "east" or "west";
                var width = eastWest
                    ? run.Max(cell => cell.Z) - run.Min(cell => cell.Z) + 1
                    : run.Max(cell => cell.X) - run.Min(cell => cell.X) + 1;
                var drop = run.Max(cell => group.Drop[cell]);
                faces.Add(new Face(facing, width, drop, width >= CliffWidth && drop >= CliffDrop));
            }
        return [.. faces.OrderByDescending(face => face.Width)];
    }

    /// <summary>The four ways ground can fall away, named. A face looks the way its drop points.</summary>
    private static readonly ((int X, int Z) Step, string Facing)[] Facings =
        [((1, 0), "east"), ((-1, 0), "west"), ((0, 1), "south"), ((0, -1), "north")];

    /// <summary>How many rows cross the surface end to end at each tier, and how many can be <b>descended</b>.
    ///
    /// <para>The asymmetry is the whole reading. Crossing a row one way, the climbs are what stop you and the
    /// falls are free; crossing it the other way, they swap. So a face that refuses a crossing one way lets it
    /// through the other — a one-way cliff rather than a wall (rules.md EL5). <c>OnFoot</c> and
    /// <c>WithBlock</c> count the rows crossable in the <em>harder</em> direction; <c>Descended</c> counts
    /// those crossable on foot in the <em>easier</em> one, which is what a drop costs: nothing.</para></summary>
    private static Fords FordsAlong(HeightField field, Footprint footprint, bool alongX)
    {
        int rows = alongX ? footprint.Depth : footprint.Width;
        int span = alongX ? footprint.Width : footprint.Depth;
        int onFoot = 0, withBlock = 0, descended = 0, counted = 0;

        for (var row = 0; row < rows; row++)
        {
            var cells = new List<int>();
            for (var i = 0; i < span; i++)
            {
                int x = alongX ? footprint.MinX + i : footprint.MinX + row;
                int z = alongX ? footprint.MinZ + row : footprint.MinZ + i;
                if (!footprint.Inside(x, z)) { cells.Clear(); continue; }   // a gap breaks the crossing
                cells.Add(field.At(x, z));
            }
            if (cells.Count < 2) continue;
            counted++;

            // The worst climb each way round. Walking the row left to right, `up` is what has to be climbed;
            // walking it right to left, `down` is.
            var up = 0; var down = 0;
            for (var i = 1; i < cells.Count; i++)
            {
                up = Math.Max(up, cells[i] - cells[i - 1]);
                down = Math.Max(down, cells[i - 1] - cells[i]);
            }
            var harder = Math.Max(up, down);
            var easier = Math.Min(up, down);
            if (harder <= WalkStep) onFoot++;
            if (harder <= ScrambleStep) withBlock++;
            if (easier <= WalkStep) descended++;
        }
        return new Fords(counted, onFoot, withBlock, descended);
    }

    /// <summary>The worst block a cell differs from its own mirror image. Zero is a fair map; anything else is
    /// one team owning a step the other does not, which nothing else in the report would show.</summary>
    private static int SymmetryError(HeightField field, string? mode, double centreX, double centreZ)
    {
        if (mode is null) return 0;
        var worst = 0;
        foreach (var (x, z) in field.Footprint.Land())
        {
            int mirroredX = (int)Math.Floor(2 * centreX - (x + 0.5));
            int mirroredZ = (int)Math.Floor(2 * centreZ - (z + 0.5));
            var (mx, mz) = mode switch
            {
                "mirror_x" => (mirroredX, z),
                "mirror_z" => (x, mirroredZ),
                _ => (mirroredX, mirroredZ),
            };
            if (!field.Has(mx, mz)) continue;
            worst = Math.Max(worst, Math.Abs(field.At(x, z) - field.At(mx, mz)));
        }
        return worst;
    }
}
