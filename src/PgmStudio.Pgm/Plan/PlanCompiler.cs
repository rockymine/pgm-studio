using PgmStudio.Geom;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Pgm.Plan;

/// <summary>
/// Compiles a <see cref="PlanModel"/> one-way into the pair the downstream pipeline consumes: a
/// <see cref="SketchLayout"/> (terrain — land-connected pieces unioned into rectilinear shapes, grouped into
/// mirror islands) and a <see cref="MapIntent"/> (teams/spawns/wools/build fanned from the authored team-0
/// unit by symmetry). Pure + deterministic: the same plan compiles to the same pair on the server and in the
/// editor. The validator's errors are checked separately (see <see cref="PlanValidator"/>).
/// </summary>
public static class PlanCompiler
{
    // Team slot palette, in orbit order (2 teams → red/blue, 4 → red/blue/yellow/green).
    private static readonly (string Id, string Name, string Color)[] Palette =
        [("red", "Red", "red"), ("blue", "Blue", "blue"), ("yellow", "Yellow", "yellow"), ("green", "Green", "green")];

    // Distinct dyes for a team's non-first wools, so every wool across the board keys to a unique colour.
    private static readonly string[] Dyes =
        ["orange", "light_blue", "pink", "lime", "cyan", "purple", "magenta", "white", "gray", "brown", "black", "silver"];

    public static (SketchLayout Layout, MapIntent Intent) Compile(PlanModel plan)
    {
        var d = PlanDerived.Build(plan);
        var layout = BuildLayout(plan, d);
        var intent = BuildIntent(plan, d, layout);
        return (layout, intent);
    }

    // ── layout: unioned shapes + mirror islands + framing ───────────────────────────────────────────────

    private static SketchLayout BuildLayout(PlanModel plan, PlanDerived d)
    {
        var shapes = new List<SketchShape>();
        var islandShapes = new List<(bool Mirrors, string ShapeId)>();
        var shapeIndex = 0;

        foreach (var component in d.Components)
        {
            var pieces = component.Select(id => d.Piece(id)!.Value).ToList();
            if (pieces.Select(p => p.Mirrors).Distinct().Count() > 1)
                throw new InvalidOperationException($"component [{string.Join(", ", component)}] mixes mirrored and non-mirrored pieces");
            // one shape per distinct surface within the component (a stepped island → stacked plateaus)
            foreach (var group in pieces.GroupBy(p => p.Surface).OrderBy(g => g.Key))
            {
                var rects = group.Select(p => (p.Rect.MinX, p.Rect.MinZ, p.Rect.MaxX, p.Rect.MaxZ)).ToList();
                var ring = RectilinearUnion.Outline(rects);
                var id = $"s{shapeIndex++}";
                shapes.Add(new SketchShape
                {
                    Id = id,
                    Type = "polygon",
                    Operation = "add",
                    BaseHeight = group.Key,
                    Vertices = [.. ring],
                });
                islandShapes.Add((group.First().Mirrors, id));
            }
        }

        // islands = mirror groups: one per distinct mirrors flag (all-true seeds → a single "team" island)
        var islands = new List<SketchIsland>();
        foreach (var mirrors in islandShapes.Select(s => s.Mirrors).Distinct())
        {
            var ids = islandShapes.Where(s => s.Mirrors == mirrors).Select(s => s.ShapeId).ToList();
            islands.Add(new SketchIsland
            {
                Id = mirrors ? "team" : "neutral",
                Name = mirrors ? "Team island" : "Neutral",
                Mirrors = mirrors,
                ShapeIds = ids,
            });
        }

        return new SketchLayout
        {
            Setup = new SketchSetup
            {
                MirrorMode = d.Mode,
                Center = new SketchCenter { Cx = 0, Cz = 0 },
                Bbox = FannedBbox(shapes, d),
            },
            Layout = new SketchShapes { Shapes = shapes, Islands = islands },
        };
    }

    // The framing box: the extent of every shape vertex fanned across the orbit, expanded one cell all round.
    private static SketchBbox FannedBbox(List<SketchShape> shapes, PlanDerived d)
    {
        double minX = double.MaxValue, minZ = double.MaxValue, maxX = double.MinValue, maxZ = double.MinValue;
        foreach (var s in shapes)
            foreach (var v in s.Vertices ?? [])
                for (var k = 0; k < d.Order; k++)
                {
                    var (x, z) = d.FanPoint(v[0], v[1], k);
                    minX = Math.Min(minX, x); minZ = Math.Min(minZ, z);
                    maxX = Math.Max(maxX, x); maxZ = Math.Max(maxZ, z);
                }
        if (minX > maxX) (minX, minZ, maxX, maxZ) = (0, 0, 0, 0);
        return new SketchBbox
        {
            MinX = minX - d.Cell, MinZ = minZ - d.Cell,
            MaxX = maxX + d.Cell, MaxZ = maxZ + d.Cell,
        };
    }

    // ── intent: teams/spawns/wools/build fanned from team 0 ─────────────────────────────────────────────

    private static MapIntent BuildIntent(PlanModel plan, PlanDerived d, SketchLayout layout)
    {
        var order = d.Order;
        var teams = Palette.Take(Math.Max(1, order))
            .Select(s => new TeamDef { Id = s.Id, Name = s.Name, Color = s.Color }).ToList();

        var spawns = new List<SpawnIntent>();
        for (var k = 0; k < order; k++)
            foreach (var s in plan.Placements.Spawns)
            {
                var piece = d.Piece(s.Piece);
                if (piece is null) continue;
                var (bx, bz) = Resolve(piece.Value.Rect, s.At, d.Cell);
                var (fx, fz) = FacingDir(bx, bz, s.Facing);
                var (px, pz) = d.FanPoint(bx, bz, k);
                spawns.Add(new SpawnIntent
                {
                    Team = teams[k].Id,
                    Point = new Pt(px, piece.Value.Surface, pz),
                    Yaw = FanYaw(d, bx, bz, fx, fz, k),
                });
            }

        // wools: team-outer, placement-inner (matches the intent's grouping); auto colour with a global dye cursor
        var wools = new List<WoolIntent>();
        var dyeCursor = 0;
        for (var k = 0; k < order; k++)
            for (var i = 0; i < plan.Placements.Wools.Count; i++)
            {
                var w = plan.Placements.Wools[i];
                var piece = d.Piece(w.Piece);
                if (piece is null) continue;
                var (bx, bz) = Resolve(piece.Value.Rect, w.At, d.Cell);
                var (px, pz) = d.FanPoint(bx, bz, k);
                var color = !string.IsNullOrEmpty(w.Color) ? w.Color
                    : i == 0 ? teams[k].Color
                    : Dyes[dyeCursor++ % Dyes.Length];
                wools.Add(new WoolIntent
                {
                    Owner = teams[k].Id,
                    Color = color,
                    Spawn = new Pt(px, piece.Value.Surface, pz),
                });
            }

        var maxHeight = plan.Globals.Surface + plan.Globals.Headroom;
        var build = new BuildIntent
        {
            MaxHeight = maxHeight,
            Areas = FanRects(plan.Zones.Select(z => z.Rect), d),
            Holes = FanRects(plan.Zones.SelectMany(z => z.Holes), d),
        };

        var observerY = plan.Globals.ObserverY ?? plan.Globals.Surface + 15;

        return new MapIntent
        {
            Teams = teams,
            MaxPlayers = plan.Globals.MaxPlayers,
            Spawns = spawns,
            Wools = wools,
            Observer = new ObserverIntent { Point = new Pt(0, observerY, 0), Yaw = 0 },
            Build = build,
            Meta = new MetaIntent { Name = plan.Meta?.Name ?? "", Authors = [] },
        };
    }

    // Fan a set of cell rects to every orbit image, de-duplicating exact repeats (self-symmetric rects).
    private static List<Rect> FanRects(IEnumerable<int[]> cellRects, PlanDerived d)
    {
        var areas = new List<Rect>();
        var seen = new HashSet<(int, int, int, int)>();
        foreach (var cr in cellRects)
        {
            var block = PlanDerived.ToBlock(cr, d.Cell);
            for (var k = 0; k < d.Order; k++)
            {
                var r = d.FanRect(block, k);
                if (seen.Add((r.MinX, r.MinZ, r.MaxX, r.MaxZ)))
                    areas.Add(new Rect(r.MinX, r.MinZ, r.MaxX, r.MaxZ));
            }
        }
        return areas;
    }

    // Piece-relative half-cell offset → block coordinate (piece origin + offset·cell). A .5 offset lands on a
    // 2.5-block half-cell; downstream flooring/snapping is the export pipeline's job, so the raw value flows on.
    private static (double X, double Z) Resolve(BlockRect piece, double[] at, int cell) =>
        (piece.MinX + at[0] * cell, piece.MinZ + at[1] * cell);

    // The facing unit direction: "front" = the cardinal quantized toward the centre; back/left/right rotate it.
    private static (int Dx, int Dz) FacingDir(double x, double z, string facing)
    {
        double dx = -x, dz = -z;                                           // toward centre (0,0)
        (int cx, int cz) = Math.Abs(dx) >= Math.Abs(dz)
            ? (Math.Sign(dx), 0)
            : (0, Math.Sign(dz));
        if (cx == 0 && cz == 0) cz = -1;                                   // spawn on the centre → face north
        return facing switch
        {
            "back"  => (-cx, -cz),
            "left"  => (-cz, cx),                                          // 90° CCW (matches Symmetry rot)
            "right" => (cz, -cx),                                          // 90° CW
            _ => (cx, cz),                                                 // "front"
        };
    }

    // The k-th orbit image's yaw: fan the facing as a direction (image of point+dir minus image of point).
    private static double FanYaw(PlanDerived d, double x, double z, int dx, int dz, int k)
    {
        var p = d.FanPoint(x, z, k);
        var q = d.FanPoint(x + dx, z + dz, k);
        var yaw = Math.Round(Math.Atan2(-(q.X - p.X), q.Z - p.Z) * 180 / Math.PI);
        return ((yaw % 360) + 360) % 360;
    }
}
