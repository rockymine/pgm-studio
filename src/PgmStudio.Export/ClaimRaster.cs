using System.Text;
using PgmStudio.Geom.Render;
using PgmStudio.Domain;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Dressing;
using PgmStudio.Minecraft.Stamping;
using PgmStudio.Vocabulary;

namespace PgmStudio.Export;

/// <summary>
/// What the dressing pass would say about every cell of a board, read off its own claims rather than guessed
/// at: a prop's footprint where one landed, the keep-out holding a cell nothing claims, or the ground both
/// leave free — the same question <see cref="Decorator"/> asks one cell at a time while it places a prop,
/// turned into a grid a preview can show whole. A placement is looked up on this raster rather than tried.
///
/// <para>A cell's class is decided in the order the pass itself asks it. A claim, once made, is final — the
/// dressing pass never asks a keep-out or a clearance about ground it already covered — so a claimed cell
/// always shows the claim. An unclaimed cell is read the way <see cref="Decorator"/>'s own seating asks it:
/// a goal's clearance before the map's keep-out mask, which is what lets an author reading this raster find
/// the rule a placement beside a goal would actually be refused under.</para>
/// </summary>
public static class ClaimRaster
{
    public const int Free = 0, Water = 1, Route = 2, Structure = 3, Tree = 4, Boulder = 5, Flora = 6,
        SpawnKeepOut = 7, DoorApproach = 8, GoalClearance = 9, WoolRoomKeepOut = 10, StructureKeepOut = 11;

    /// <summary>The class every cell can be, indexed by the digit it prints as: <c>0</c>–<c>9</c> then
    /// <c>a</c>, <c>b</c>.</summary>
    public static readonly IReadOnlyList<string> Classes =
    [
        "free", "water", "paving", "structure", "tree", "boulder", "flora",
        "spawn keep-out", "door approach", "goal clearance", "wool-room keep-out", "structure keep-out",
    ];

    /// <summary>Rows over the board's own surface — its bounding box, one character per cell, void outside
    /// the surface as a space. <see cref="Rows"/> runs <paramref name="MinZ"/> to its far edge, each row
    /// <paramref name="MinX"/> to its far edge.</summary>
    public sealed record Grid(int MinX, int MinZ, int Width, int Height, IReadOnlyList<string> Rows);

    /// <summary>Classify every cell of <paramref name="surface"/>: the prop that claims it first, then a
    /// goal's clearance, then the map's keep-out mask, then free — the order <see cref="Decorator"/> asks a
    /// candidate site the same three questions in, so a reader finds the rule a placement would actually be
    /// refused under.</summary>
    public static Grid Read(
        IReadOnlyList<PlacementClaim> placements, IReadOnlyDictionary<(int X, int Z), int> surface,
        Func<int, int, KeepOut?> keptClearAt, Func<int, int, bool> goalClearanceAt)
    {
        if (surface.Count == 0) return new Grid(0, 0, 0, 0, []);
        var minX = surface.Keys.Min(cell => cell.X);
        var maxX = surface.Keys.Max(cell => cell.X);
        var minZ = surface.Keys.Min(cell => cell.Z);
        var maxZ = surface.Keys.Max(cell => cell.Z);
        var width = maxX - minX + 1;
        var claimed = ClaimedClasses(placements);

        var rows = new List<string>(maxZ - minZ + 1);
        for (var z = minZ; z <= maxZ; z++)
        {
            var row = new char[width];
            for (var x = minX; x <= maxX; x++)
                row[x - minX] = surface.ContainsKey((x, z))
                    ? TextGrid.Base36(ClassAt((x, z), claimed, keptClearAt, goalClearanceAt))
                    : ' ';
            rows.Add(new string(row));
        }
        return new Grid(minX, minZ, width, maxZ - minZ + 1, rows);
    }

    /// <summary>The grid as characters: the scale and the extent, a key naming every class, the ruler and the
    /// rows, then every decline as a line and the placed/declined tally — what a reader scans for a free cell.</summary>
    public static string Render(Grid grid, int placed, IReadOnlyList<Finding> declines)
    {
        var text = new StringBuilder();
        text.Append($"CLAIMS  1 char = 1 block  x {grid.MinX}..{grid.MinX + grid.Width - 1} across, ")
            .Append($"z {grid.MinZ}..{grid.MinZ + grid.Height - 1} down\n");
        text.Append("KEY  ")
            .AppendJoin("  ", Classes.Select((name, index) => $"{TextGrid.Base36(index)} {name}"))
            .Append("  space void\n");
        TextGrid.Frame(text, grid.MinX, grid.MinZ, grid.Width, grid.Height, 1, (column, row) => grid.Rows[row][column]);
        foreach (var decline in declines) text.Append($"decline {decline.Rule} {decline.Message}\n");
        text.Append($"placed {placed}, declined {declines.Count}\n");
        return text.ToString();
    }

    /// <summary>Why an anchor cannot take a footprint, and how many anchors that rule turned away — the
    /// tally that separates a board with no room from a board whose room is all inside one keep-out.</summary>
    public sealed record Because(string Rule, int Cells);

    /// <summary>Where a prop of a stated kind and footprint may seat, as digit rows over the same box the
    /// raster covers: <c>1</c> an anchor the footprint stands at, <c>0</c> one it does not, a space where the
    /// anchor cell itself is off the board. <see cref="Refused"/> counts the anchors each rule turned away.</summary>
    public sealed record Seating(int MinX, int MinZ, int Width, int Height, IReadOnlyList<string> Rows,
        string Kind, int Standoff, int FootprintWidth, int FootprintDepth, int Seats,
        IReadOnlyList<Because> Refused);

    /// <summary>
    /// The raster read forwards: every anchor a footprint of <paramref name="width"/>×<paramref name="depth"/>
    /// blocks would seat at, with its minimum corner on that cell — the question <see cref="Decorator"/>
    /// answers one placement at a time, asked of the whole board at once.
    ///
    /// <para>An anchor is refused by the first rule the footprint meets, scanned row by row, each cell asked
    /// in the pass's own order: a goal's clearance, the map's keep-out, the ground another prop claims, the
    /// standoff from a route, and last the ground itself. The cell order is the board's rather than the
    /// prop's — a prop states its own cells and this states a box — so a footprint refused by two rules at
    /// once may name either, and the count is what the tally is for.</para>
    ///
    /// <para><paramref name="standoff"/> is the kind's own <see cref="PlacedProp.RouteStandoff"/>, and it is
    /// measured the way <see cref="GroundClaims.Storey.NearerThan"/> measures it: a route cell strictly
    /// nearer than the standoff in Chebyshev blocks refuses, so a cell exactly at the distance stands.</para>
    ///
    /// <para>What it does not run is the four rules a <b>building</b> is judged by after it seats —
    /// <c>DR-PASS</c>, <c>DR-CROSS</c>, <c>DR-WAY</c> and <c>DR-SLOPE</c> — each of which reads the built
    /// world rather than the ground. A seat here is a seat the pass will not decline for a cell it rests on;
    /// a house also has to leave a way past itself.</para>
    /// </summary>
    public static Seating Seat(Grid grid, string kind, int standoff, int width, int depth)
    {
        width = Math.Max(1, width);
        depth = Math.Max(1, depth);
        var near = NearRoute(grid, standoff);
        var after = PlacedProp.PlacementOrderOf(kind) ?? int.MaxValue;

        var rows = new List<string>(grid.Height);
        var refused = new Dictionary<string, int>();
        var seats = 0;
        for (var row = 0; row < grid.Height; row++)
        {
            var line = new char[grid.Width];
            for (var column = 0; column < grid.Width; column++)
            {
                if (grid.Rows[row][column] == ' ') { line[column] = ' '; continue; }
                var stopped = Stops(grid, near, after, column, row, width, depth);
                if (stopped is null) { line[column] = '1'; seats++; continue; }
                line[column] = '0';
                refused[stopped] = refused.GetValueOrDefault(stopped) + 1;
            }
            rows.Add(new string(line));
        }

        return new Seating(grid.MinX, grid.MinZ, grid.Width, grid.Height, rows, kind, standoff, width, depth,
            seats, [.. refused.OrderByDescending(entry => entry.Value).Select(entry => new Because(entry.Key, entry.Value))]);
    }

    /// <summary>The rule that turns this anchor away, or null where the whole footprint seats.
    /// <paramref name="after"/> is the asked kind's place in the pass's order: a cell claimed by a kind that
    /// places later is free ground for this one, because on the next pass that prop meets this claim rather
    /// than the other way round.</summary>
    private static string? Stops(Grid grid, bool[] near, int after, int column, int row, int width, int depth)
    {
        for (var dz = 0; dz < depth; dz++)
            for (var dx = 0; dx < width; dx++)
            {
                int x = column + dx, z = row + dz;
                if (x >= grid.Width || z >= grid.Height) return DressingRules.NoGround;
                var held = At(grid.Rows[z][x]);
                var stopped = held switch
                {
                    GoalClearance => ObjectiveRules.PropInClearance,
                    SpawnKeepOut or DoorApproach or WoolRoomKeepOut or StructureKeepOut => DressingRules.KeptClear,
                    null => DressingRules.NoGround,
                    _ when held != Free && Places(held.Value) <= after => DressingRules.GroundTaken,
                    _ => null,
                };
                stopped ??= near[z * grid.Width + x] ? DressingRules.RoadStandoff : null;
                if (stopped is not null) return stopped;
            }
        return null;
    }

    /// <summary>Where the kind that claimed a cell stands in the pass's order — the claim classes are the
    /// prop kinds, so the answer is that kind's own <see cref="PlacedProp.PlacementOrder"/>. A class no prop
    /// kind carries places before everything, since nothing the pass runs later can move it.</summary>
    private static int Places(int claimedClass) => PlacedProp.PlacementOrderOf(claimedClass switch
    {
        Water => "water", Route => "stroke", Structure => "house",
        Tree => "tree", Boulder => "boulder", Flora => "flora", _ => "",
    }) ?? int.MinValue;

    /// <summary>Which cells stand nearer a route than the standoff allows — one sweep out from every route
    /// cell rather than a ring walk per anchor, which is the same answer at a hundredth of the work.</summary>
    private static bool[] NearRoute(Grid grid, int standoff)
    {
        var near = new bool[grid.Width * grid.Height];
        if (standoff <= 0) return near;
        for (var row = 0; row < grid.Height; row++)
            for (var column = 0; column < grid.Width; column++)
            {
                if (At(grid.Rows[row][column]) != Route) continue;
                for (var dz = -(standoff - 1); dz <= standoff - 1; dz++)
                    for (var dx = -(standoff - 1); dx <= standoff - 1; dx++)
                    {
                        int x = column + dx, z = row + dz;
                        if (x >= 0 && x < grid.Width && z >= 0 && z < grid.Height) near[z * grid.Width + x] = true;
                    }
            }
        return near;
    }

    /// <summary>The class a printed character stands for, or null for the space a cell off the board
    /// prints — the inverse of <see cref="TextGrid.Base36"/> over <see cref="Classes"/>.</summary>
    private static int? At(char printed) =>
        printed == ' ' ? null : printed <= '9' ? printed - '0' : printed - 'a' + 10;

    /// <summary>The seat mask as characters, with the footprint and the standoff it was read for on the
    /// first line and the refusals tallied under the grid.</summary>
    public static string RenderSeats(Seating seating)
    {
        var text = new StringBuilder();
        text.Append($"SEATS  {seating.Kind}, footprint {seating.FootprintWidth}x{seating.FootprintDepth}")
            .Append(seating.Standoff > 0 ? $", {seating.Standoff} blocks off a route" : ", no route standoff")
            .Append($"  1 char = 1 block  x {seating.MinX}..{seating.MinX + seating.Width - 1} across, ")
            .Append($"z {seating.MinZ}..{seating.MinZ + seating.Height - 1} down\n");
        text.Append("KEY  1 the footprint seats with its minimum corner here  0 refused  space void\n");
        TextGrid.Frame(text, seating.MinX, seating.MinZ, seating.Width, seating.Height, 1,
            (column, row) => seating.Rows[row][column]);
        text.Append($"seats {seating.Seats}");
        if (seating.Refused.Count > 0)
            text.Append("; refused ").Append(string.Join(", ",
                seating.Refused.Select(because => $"{because.Cells} {because.Rule}")));
        text.Append('\n');
        return text.ToString();
    }

    private static int ClassAt(
        (int X, int Z) cell, IReadOnlyDictionary<(int X, int Z), int> claimed,
        Func<int, int, KeepOut?> keptClearAt, Func<int, int, bool> goalClearanceAt)
    {
        if (claimed.TryGetValue(cell, out var claimedClass)) return claimedClass;
        if (goalClearanceAt(cell.X, cell.Z)) return GoalClearance;
        return keptClearAt(cell.X, cell.Z) switch
        {
            KeepOut.Spawn => SpawnKeepOut,
            KeepOut.Approach => DoorApproach,
            KeepOut.WoolRoom => WoolRoomKeepOut,
            KeepOut.Structure or KeepOut.Built => StructureKeepOut,
            _ => Free,
        };
    }

    /// <summary>Every claim's own class, keyed by the cells it covers — a <see cref="ProvenancePass.Structure"/>
    /// claim is <see cref="Structure"/> whatever placed it, and everything else is read off its
    /// <see cref="Domain.StampId.Kind"/>. The <b>first</b> claimant of a cell keeps it, which is
    /// <see cref="GroundClaims"/>'s own rule and not the provenance record's: a building ignores a route's
    /// claim and may stamp over pavement, so the cell a house stands on is still the road a tree keeps its
    /// standoff from. Reading it the other way round would hide that road under the porch it runs to.</summary>
    private static Dictionary<(int X, int Z), int> ClaimedClasses(IReadOnlyList<PlacementClaim> placements)
    {
        var claimed = new Dictionary<(int X, int Z), int>();
        foreach (var claim in placements)
        {
            var claimedClass = claim.Pass == ProvenancePass.Structure ? Structure : claim.Owner.Kind switch
            {
                "water" => Water,
                "stroke" => Route,
                "tree" => Tree,
                "boulder" => Boulder,
                "flora" => Flora,
                _ => Structure,
            };
            foreach (var cell in claim.Cells) claimed.TryAdd(cell, claimedClass);
        }
        return claimed;
    }
}
