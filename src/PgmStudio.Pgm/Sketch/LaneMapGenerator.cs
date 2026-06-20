using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Pgm.Sketch;

/// <summary>
/// Turns a lane layout into a complete, valid Capture-the-Wool map: the <see cref="SketchLayout"/> terrain
/// plus a <see cref="MapIntent"/> that places teams, spawns, wools, monuments, and the bridges that make it
/// traversable. Each wool is owned (defended) by its team and captured by the opponent, whose monument sits
/// near that opponent's spawn — so a generated map clears both the monument-presence and traversability
/// gates on its own. Two-team boards (the 'H' layout); the build areas come from <see cref="AutoBridge"/>.
/// </summary>
public static class LaneMapGenerator
{
    private static readonly (string Id, string Name)[] TeamSlots = [("red", "Red"), ("blue", "Blue")];

    public static (SketchLayout Layout, MapIntent Intent) Generate(LaneLayoutOptions? options = null, string name = "Generated map")
    {
        var o = options ?? new LaneLayoutOptions();
        var (layout, hints) = LaneSketchGenerator.HLayout(o);

        var cells = SketchRasterizer.Rasterize(layout.ToJson());
        var bridges = AutoBridge.Infer(cells, o.LaneWidth);

        var cz = o.Height / 2;
        var wool = hints.Where(h => h.Kind == "wool").ToDictionary(h => h.Team);
        var spawn = hints.Where(h => h.Kind == "spawn").ToDictionary(h => h.Team);

        var spawns = new List<SpawnIntent>();
        var wools = new List<WoolIntent>();
        for (var t = 0; t < TeamSlots.Length; t++)
        {
            var other = 1 - t;
            var sp = spawn[t];
            var wl = wool[t];
            var osp = spawn[other];

            spawns.Add(new SpawnIntent
            {
                Team = TeamSlots[t].Id,
                Point = new Pt(sp.X, 1, sp.Z),
                Protection = Box(sp.X, sp.Z, o.LaneWidth),
                Yaw = sp.Z < cz ? 0 : 180,   // face the centre
            });

            // The opponent captures this team's wool; their monument sits near their own spawn, a few
            // blocks toward the centre so it stays on the spawn island.
            var monZ = osp.Z + Math.Sign(cz - osp.Z) * 4;
            wools.Add(new WoolIntent
            {
                Owner = TeamSlots[t].Id,
                Spawn = new Pt(wl.X, 1, wl.Z),
                Room = Box(wl.X, wl.Z, o.LaneWidth),
                Monuments = [new MonumentIntent { Team = TeamSlots[other].Id, Location = new Pt(osp.X, 1, monZ) }],
            });
        }

        var intent = new MapIntent
        {
            Teams = [.. TeamSlots.Select(s => new TeamDef { Id = s.Id, Name = s.Name, Color = s.Id })],
            Spawns = spawns,
            Wools = wools,
            Observer = new ObserverIntent { Point = new Pt(o.Width / 2, 24, cz), Yaw = 0 },
            Build = new BuildIntent { Areas = bridges },
            Meta = new MetaIntent { Name = name },
        };
        return (layout, intent);
    }

    private static Rect Box(double x, double z, double size) =>
        new(x - size / 2, z - size / 2, x + size / 2, z + size / 2);
}
