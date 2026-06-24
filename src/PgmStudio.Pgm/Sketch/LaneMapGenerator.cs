using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Pgm.Sketch;

/// <summary>
/// Turns a lane layout into a complete, valid Capture-the-Wool map: the <see cref="SketchLayout"/> terrain
/// plus a <see cref="MapIntent"/> that places teams, spawns, wools, monuments, and the bridges that make it
/// traversable. Works for any archetype (the team count and wools-per-team come from the layout's objective
/// hints): each wool is captured by the opposing team across the board, whose monument sits near that
/// opponent's spawn — so a generated map clears the monument-presence and traversability gates on its own.
/// </summary>
public static class LaneMapGenerator
{
    private static readonly (string Id, string Name, string Color)[] Palette =
        [("red", "Red", "red"), ("blue", "Blue", "blue"), ("green", "Green", "green"), ("yellow", "Yellow", "yellow")];

    // Distinct dye colours so a team's several wools get distinct ids (WoolGenerator keys wools by colour).
    private static readonly string[] WoolDyes =
        ["pink", "lime", "cyan", "orange", "purple", "magenta", "light_blue", "white", "gray", "brown", "black", "silver"];

    public static (SketchLayout Layout, MapIntent Intent) Generate(LaneLayoutOptions? options = null, string name = "Generated map")
    {
        var o = options ?? new();
        var (layout, hints) = LaneSketchGenerator.Build(o);

        var cells = SketchRasterizer.Rasterize(layout.ToJson());

        var teamCount = hints.Count == 0 ? 0 : hints.Max(h => h.Team) + 1;
        var slots = Palette.Take(Math.Max(1, teamCount)).ToArray();

        // board centre from the footprint (square pinwheel or rectangular H alike)
        double cx = cells.Count > 0 ? (cells.Min(c => c.X) + cells.Max(c => c.X)) / 2.0 : o.Width / 2;
        double cz = cells.Count > 0 ? (cells.Min(c => c.Z) + cells.Max(c => c.Z)) / 2.0 : o.Height / 2;

        // bridges: if the layout supplied bridge anchors (Organic's mid trunk-tips), span each across the mid
        // line to its mirror so every trunk gets its own crossing; otherwise infer them (MST over the islands).
        var bridgeHints = hints.Where(h => h.Kind == "bridge").ToList();
        var bridges = bridgeHints.Count > 0
            ? bridgeHints.Select(h => AcrossBridge(h.X, h.Z, cz, o.LaneWidth)).ToList()
            : AutoBridge.Infer(cells, o.LaneWidth);

        var spawnOf = hints.Where(h => h.Kind == "spawn").ToDictionary(h => h.Team);
        var woolsOf = hints.Where(h => h.Kind == "wool").GroupBy(h => h.Team).ToDictionary(g => g.Key, g => g.ToList());

        var spawns = new List<SpawnIntent>();
        var wools = new List<WoolIntent>();
        var dye = 0;                                                 // global distinct-colour cursor
        for (var t = 0; t < teamCount; t++)
        {
            if (!spawnOf.TryGetValue(t, out var sp)) continue;
            var rival = (t + teamCount / 2) % teamCount;             // the team across the board
            var rsp = spawnOf.GetValueOrDefault(rival, sp);

            spawns.Add(new SpawnIntent
            {
                Team = slots[t].Id,
                Point = new Pt(sp.X, 1, sp.Z),
                Protection = [Box(sp.X, sp.Z, o.LaneWidth)],
                Yaw = FaceYaw(sp.X, sp.Z, cx, cz),
            });

            // the rival captures this team's wools; their monument sits near their spawn, nudged toward
            // the centre so it stays on the rival's island
            var (monX, monZ) = Toward(rsp.X, rsp.Z, cx, cz, 4);
            var teamWools = woolsOf.GetValueOrDefault(t, []);
            foreach (var wl in teamWools)
                wools.Add(new WoolIntent
                {
                    Owner = slots[t].Id,
                    // single wool → the team's own colour; several → distinct dye colours (unique ids)
                    Color = teamWools.Count == 1 ? slots[t].Color : WoolDyes[dye++ % WoolDyes.Length],
                    Spawn = new Pt(wl.X, 1, wl.Z),
                    Room = [Box(wl.X, wl.Z, o.LaneWidth)],
                    Monuments = [new MonumentIntent { Team = slots[rival].Id, Location = new Pt(monX, 1, monZ) }],
                });
        }

        var intent = new MapIntent
        {
            Teams = [.. slots.Select(s => new TeamDef { Id = s.Id, Name = s.Name, Color = s.Color })],
            Spawns = spawns,
            Wools = wools,
            Observer = new ObserverIntent { Point = new Pt(cx, 24, cz), Yaw = 0 },
            Build = new BuildIntent { Areas = bridges },
            Meta = new MetaIntent { Name = name },
        };
        return (layout, intent);
    }

    private static Rect Box(double x, double z, double size) =>
        new(x - size / 2, z - size / 2, x + size / 2, z + size / 2);

    // A bridge spanning a team-0 trunk tip (x,z) across the mid line (cz) to its mirror, `width` across.
    private static Rect AcrossBridge(double x, double z, double cz, double width)
    {
        double z2 = 2 * cz - z;
        return new Rect(x - width / 2, Math.Min(z, z2), x + width / 2, Math.Max(z, z2));
    }

    // Move (x,z) toward (cx,cz) by `d` blocks.
    private static (double X, double Z) Toward(double x, double z, double cx, double cz, double d)
    {
        double dx = cx - x, dz = cz - z;
        var len = Math.Sqrt(dx * dx + dz * dz);
        return len < 1e-6 ? (x, z) : (x + dx / len * d, z + dz / len * d);
    }

    // Minecraft yaw that faces the board centre (0 = +Z south, 90 = -X west, 180 = -Z north, 270 = +X east).
    private static double FaceYaw(double x, double z, double cx, double cz) =>
        Math.Round(Math.Atan2(-(cx - x), cz - z) * 180 / Math.PI);
}
