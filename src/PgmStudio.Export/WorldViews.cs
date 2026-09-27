using System.Globalization;
using System.Text.Json.Serialization;
using PgmStudio.Geom;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Vocabulary;

namespace PgmStudio.Export;

/// <summary>
/// One picture of a board from a player's eye, by name: the thing it looks at, and where the eye stands, how
/// high, and how far it tips down, where the view says. A view without <see cref="FromX"/> leaves the eye to
/// find its own place when the picture is drawn, which is <c>render/eye</c>'s <c>look</c>.
/// </summary>
public sealed record WorldView(string Id, string Name, int LookX, int LookZ,
                               int? FromX = null, int? FromZ = null, double? Y = null, double? Pitch = null)
{
    /// <summary>The <c>render/eye</c> query words that draw this view.</summary>
    [JsonIgnore]
    public string Query =>
        string.Create(CultureInfo.InvariantCulture, $"look={LookX},{LookZ}")
        + (FromX is { } x && FromZ is { } z ? string.Create(CultureInfo.InvariantCulture, $"&from={x},{z}") : "")
        + (Y is { } y ? string.Create(CultureInfo.InvariantCulture, $"&y={y:0.##}") : "")
        + (Pitch is { } pitch ? string.Create(CultureInfo.InvariantCulture, $"&pitch={pitch:0.#}") : "");
}

/// <summary>
/// The pictures of a built board worth looking at before anyone has asked for one: the board from above one
/// side, every team's spawn and the view a player gets on arriving there, every objective, and the first of
/// the buildings and boulders the dressing placed.
///
/// <para>Spawns and objectives are shown for every team, because each is a different team's first sight of
/// the board. A prop is shown at its first orbit image only: the others are the same thing turned.</para>
///
/// <para><b>A building is framed here, not by the eye.</b> The eye finds its own place for a small thing, but
/// what it reads as a thing's ground and top is the columns around it, and inside a building those are its
/// roof — and a wool room's marker floats above that. What is known here is the building's footprint, so the
/// eye is stood on the terrain outside it, far enough back that its width and height fit the frame, and
/// tipped to the middle of its body: the ground under it to the height its walls reach from that ground.</para>
///
/// <para><b>So is a goal</b>, from the box it was built in: the ground under it to the box's top. The goal's
/// sky marker hangs far over that box and is not part of it.</para>
/// </summary>
public static class WorldViews
{
    private const int HousesShown = 4, BouldersShown = 2, OutOfTheRoom = 4, DownTheRoad = 24;
    private const double EyeHeight = 1.62;

    /// <summary>The most air a building's own column holds between one block and the next — a doorway.</summary>
    private const int DoorwayGap = 3;

    /// <summary>How far past its footprint a building's roof may reach — where a sight meeting something is
    /// meeting the building itself.</summary>
    private const int RoofReach = 3;

    /// <summary>How much of the frame a framed building may fill across and down — the tangents of the eye's
    /// default half-angles, 35° across and about 21.5° down a 16:9 frame, with a margin.</summary>
    private const double AcrossFill = 0.56, DownFill = 0.3;

    /// <summary>How steeply the eye looks down on a thing, in degrees, where no stand on the ground sees it:
    /// from the ground first, then from ever higher in the air.</summary>
    private static readonly double?[] Lifts = [null, 25, 45, 65];

    /// <summary>The views <paramref name="built"/> suggests, in the order a player meets the board: the whole
    /// of it, where they arrive, what they play for, and what stands on the way.</summary>
    public static IReadOnlyList<WorldView> Suggested(BuiltWorld built)
    {
        var intent = built.ResolvedIntent;
        var views = new List<WorldView>();
        var middle = Middle(built.Surface);
        if (Overview(built.Surface) is { } overview) views.Add(overview);

        foreach (var (spawn, index) in intent.Spawns.Select((spawn, index) => (spawn, index)))
        {
            var team = TeamName(intent, spawn.Team);
            var name = $"{team} spawn";
            if ((spawn.Footprint ?? Bounds(spawn.Protection)) is not { } room)
            {
                views.Add(At($"spawn-{index}", name, spawn.Point));
                continue;
            }
            var (dx, dz) = Heading(spawn.Yaw);
            var (centreX, centreZ) = Centre(room);
            views.Add(Framed($"spawn-{index}", name, built, room, (centreX + dx * 100, centreZ + dz * 100)));
            var (standX, standZ, lookX, lookZ) = Arriving(room, dx, dz);
            views.Add(new WorldView($"spawn-{index}-out", $"Out of the {team.ToLowerInvariant()} spawn",
                lookX, lookZ, standX, standZ));
        }

        foreach (var (wool, index) in (intent.Wools ?? []).Select((wool, index) => (wool, index)))
        {
            var colour = wool.Color.Length > 0 ? wool.Color.Replace('_', ' ') : TeamName(intent, wool.Owner).ToLowerInvariant();
            var name = $"The {colour} wool";
            views.Add((wool.Footprint ?? Bounds(wool.Protection)) is { } room
                ? Framed($"wool-{index}", name, built, room, middle)
                : At($"wool-{index}", name, wool.Spawn));
        }
        foreach (var (goal, index) in (intent.Destroyables ?? []).Select((goal, index) => (goal, index)))
            views.Add(Goal($"destroyable-{index}", goal.Name.Length > 0 ? goal.Name : $"{TeamName(intent, goal.Owner)} monument",
                           built, goal.Box, goal.Anchor, middle));
        foreach (var (core, index) in (intent.Cores ?? []).Select((core, index) => (core, index)))
            views.Add(Goal($"core-{index}", core.Name.Length > 0 ? core.Name : $"{TeamName(intent, core.Owner)} core",
                           built, core.Box, core.Anchor, middle));
        foreach (var (point, index) in (intent.ControlPoints ?? []).Select((point, index) => (point, index)))
            views.Add(Goal($"point-{index}", point.Name.Length > 0 ? point.Name : "Control point",
                           built, point.PadBox, point.Anchor, middle));

        var placements = built.Dressing.Placements.Where(claim => claim.Owner.Image == 0 && claim.Cells.Count > 0).ToList();
        foreach (var (house, index) in placements.Where(claim => claim.Owner.Kind == PropKinds.House).Take(HousesShown).Select((claim, index) => (claim, index)))
            views.Add(Framed($"house-{index}", $"House {index + 1}", built, Bounds(house.Cells), middle));
        foreach (var (boulder, index) in placements.Where(claim => claim.Owner.Kind == PropKinds.Boulder).Take(BouldersShown).Select((claim, index) => (claim, index)))
            views.Add(Over($"boulder-{index}", $"Boulder {index + 1}", boulder.Cells));
        return views;
    }

    /// <summary>A goal framed on the box it was built in, or left to the eye where the build resolved
    /// none.</summary>
    private static WorldView Goal(string id, string name, BuiltWorld built, BlockBox? box, Pt anchor, (double X, double Z) toward) =>
        box is { } volume ? Framed(id, name, built, new Rect(volume.MinX, volume.MinZ, volume.MaxX, volume.MaxZ), toward, volume.MaxY)
                         : At(id, name, anchor);

    /// <summary>
    /// A building over <paramref name="room"/> seen whole from the side facing <paramref name="toward"/>, or
    /// from the nearest side to it where the eye has terrain to stand on and nothing between it and the
    /// building. The eye stands back until the footprint across the view and the body's height both fit, and
    /// comes closer where that is what clears the way. Where no stand on the ground is clear the eye rises
    /// into the air over the same places (<see cref="Lifts"/>), and where none of those is either it is left
    /// to find its own place, as it does for a small thing. <paramref name="stated"/> is the thing's top where
    /// its box states one, in place of the height its columns reach from the ground.
    /// </summary>
    private static WorldView Framed(string id, string name, BuiltWorld built, Rect room, (double X, double Z) toward,
                                    int? stated = null)
    {
        var (lookX, lookZ) = Centre(room);
        var cells = Cells(room).ToList();
        var grounds = cells.Where(built.Surface.ContainsKey).Select(cell => built.Surface[cell]).Order().ToList();
        if (grounds.Count == 0) return new WorldView(id, name, lookX, lookZ);
        var ground = grounds[grounds.Count / 2];
        var top = stated ?? cells.Max(cell => Rise(built.World, cell.X, cell.Z, ground));
        var aim = (ground + 1 + top + 1) / 2.0;

        double centreX = (room.MinX + room.MaxX) / 2, centreZ = (room.MinZ + room.MaxZ) / 2;
        double halfX = (room.MaxX - room.MinX + 1) / 2, halfZ = (room.MaxZ - room.MinZ + 1) / 2;
        var facing = Math.Atan2(toward.Z - centreZ, toward.X - centreX);
        foreach (var lift in Lifts)
        foreach (var turn in (int[])[0, 1, -1, 2, -2, 3, -3, 4])
        {
            var angle = facing + turn * Math.PI / 4;
            double dx = Math.Cos(angle), dz = Math.Sin(angle);
            var across = Math.Abs(dz) * halfX + Math.Abs(dx) * halfZ;
            var reach = Math.Min(Math.Abs(dx) > 1e-6 ? halfX / Math.Abs(dx) : double.MaxValue,
                                 Math.Abs(dz) > 1e-6 ? halfZ / Math.Abs(dz) : double.MaxValue);
            var back = Math.Max(across / AcrossFill, (top - ground + 1) / 2.0 / DownFill);
            foreach (var closer in (double[])[1, 0.75, 0.5])
            {
                var distance = reach + Math.Max(6, back * closer);
                int x = (int)Math.Round(centreX + dx * distance), z = (int)Math.Round(centreZ + dz * distance);
                var standing = built.Surface.TryGetValue((x, z), out var underfoot);
                double eye;
                if (lift is { } degrees)
                {
                    eye = aim + distance * Math.Tan(degrees * Math.PI / 180);
                    if (standing && eye < underfoot + 1 + EyeHeight) continue;
                }
                else if (standing) eye = underfoot + 1 + EyeHeight;
                else continue;
                if (!Clear(built.World, (x + 0.5, eye, z + 0.5), (centreX + 0.5, aim, centreZ + 0.5), room)) continue;
                var pitch = Math.Atan2(eye - aim, distance) * 180 / Math.PI;
                return new WorldView(id, name, lookX, lookZ, x, z, Math.Round(eye, 2), Math.Round(pitch, 1));
            }
        }
        return new WorldView(id, name, lookX, lookZ);
    }

    /// <summary>Whether the sight from <paramref name="eye"/> to <paramref name="at"/> reaches
    /// <paramref name="room"/> with nothing standing in it but low plants — the eye itself not buried, and
    /// no hill, rock, wall or tree across the way.</summary>
    private static bool Clear(VoxelWorld world, (double X, double Y, double Z) eye, (double X, double Y, double Z) at, Rect room)
    {
        double dx = at.X - eye.X, dy = at.Y - eye.Y, dz = at.Z - eye.Z;
        var steps = (int)(Math.Sqrt(dx * dx + dy * dy + dz * dz) * 4);
        for (var i = 0; i < steps; i++)
        {
            var along = (double)i / steps;
            double x = eye.X + dx * along, y = eye.Y + dy * along, z = eye.Z + dz * along;
            if (x >= room.MinX - RoofReach && x < room.MaxX + 1 + RoofReach
                && z >= room.MinZ - RoofReach && z < room.MaxZ + 1 + RoofReach) return true;
            var block = world.GetBlock((int)Math.Floor(x), (int)Math.Floor(y), (int)Math.Floor(z)).Id;
            if (block != 0 && !SeenThrough.Contains(block)) return false;
        }
        return true;
    }

    /// <summary>What a sight passes through: grass, ferns, flowers, dead bushes, vines and snow cover.</summary>
    private static readonly HashSet<int> SeenThrough = [31, 32, 37, 38, 39, 40, 78, 106, 175];

    /// <summary>The board from above the middle of its long side, far enough back and high enough up that
    /// the whole of it is in the frame. Null for a board with no ground.</summary>
    private static WorldView? Overview(IReadOnlyDictionary<(int X, int Z), int> surface)
    {
        if (surface.Count == 0) return null;
        int minX = surface.Keys.Min(cell => cell.X), maxX = surface.Keys.Max(cell => cell.X);
        int minZ = surface.Keys.Min(cell => cell.Z), maxZ = surface.Keys.Max(cell => cell.Z);
        int centreX = (minX + maxX) / 2, centreZ = (minZ + maxZ) / 2;
        var top = surface.Values.Max();
        var wide = maxX - minX >= maxZ - minZ;
        var back = (int)Math.Ceiling(0.45 * Math.Max(maxX - minX, maxZ - minZ)) + 8;
        var (fromX, fromZ) = wide ? (centreX, maxZ + back) : (maxX + back, centreZ);
        return new WorldView("overview", "The whole board", centreX, centreZ, fromX, fromZ, top + 0.6 * back);
    }

    /// <summary>The middle of the board's ground, which a building is seen from the side facing.</summary>
    private static (double X, double Z) Middle(IReadOnlyDictionary<(int X, int Z), int> surface) =>
        surface.Count == 0 ? (0, 0)
            : ((surface.Keys.Min(cell => cell.X) + surface.Keys.Max(cell => cell.X)) / 2.0,
               (surface.Keys.Min(cell => cell.Z) + surface.Keys.Max(cell => cell.Z)) / 2.0);

    private static WorldView At(string id, string name, Pt anchor) =>
        new(id, name, (int)Math.Floor(anchor.X), (int)Math.Floor(anchor.Z));

    private static WorldView Over(string id, string name, IReadOnlyList<(int X, int Z)> cells) =>
        new(id, name, (int)Math.Round(cells.Average(cell => cell.X)), (int)Math.Round(cells.Average(cell => cell.Z)));

    private static string TeamName(MapIntent intent, string team) =>
        intent.Teams?.FirstOrDefault(def => def.Id == team) is { Name.Length: > 0 } def ? def.Name : team;

    private static Rect? Bounds(IReadOnlyList<Rect> rects) =>
        rects.Count == 0 ? null
            : new Rect(rects.Min(rect => rect.MinX), rects.Min(rect => rect.MinZ),
                       rects.Max(rect => rect.MaxX), rects.Max(rect => rect.MaxZ));

    private static Rect Bounds(IReadOnlyList<(int X, int Z)> cells) =>
        new(cells.Min(cell => cell.X), cells.Min(cell => cell.Z), cells.Max(cell => cell.X), cells.Max(cell => cell.Z));

    private static IEnumerable<(int X, int Z)> Cells(Rect rect)
    {
        for (var x = (int)Math.Floor(rect.MinX); x <= (int)Math.Floor(rect.MaxX); x++)
            for (var z = (int)Math.Floor(rect.MinZ); z <= (int)Math.Floor(rect.MaxZ); z++)
                yield return (x, z);
    }

    /// <summary>How high a column stands joined to <paramref name="ground"/>: the highest block reached from it
    /// upward without crossing more than <see cref="DoorwayGap"/> blocks of air. A wall reaches its eaves; a room's
    /// inside stops at its floor; and a marker floating over the roof is never reached.</summary>
    private static int Rise(VoxelWorld world, int x, int z, int ground)
    {
        int top = ground, air = 0;
        for (var y = ground + 1; y < VoxelWorld.MaxHeight && air <= DoorwayGap; y++)
        {
            if (world.GetBlock(x, y, z).Id == 0) { air++; continue; }
            top = y;
            air = 0;
        }
        return top;
    }

    private static (int X, int Z) Centre(Rect rect) =>
        ((int)Math.Floor((rect.MinX + rect.MaxX) / 2), (int)Math.Floor((rect.MinZ + rect.MaxZ) / 2));

    /// <summary>The way a yaw faces, as a step on the board. The yaw is the game's — 0 faces south (+z), 90
    /// west.</summary>
    private static (double Dx, double Dz) Heading(double yaw)
    {
        var radians = yaw * Math.PI / 180;
        return (-Math.Sin(radians), Math.Cos(radians));
    }

    /// <summary>Where a player arriving in <paramref name="room"/> heading <paramref name="dx"/>,
    /// <paramref name="dz"/> stands once out of it, and what is ahead: a few blocks past the room's wall on that
    /// heading, looking on down it.</summary>
    private static (int StandX, int StandZ, int LookX, int LookZ) Arriving(Rect room, double dx, double dz)
    {
        double centreX = (room.MinX + room.MaxX) / 2, centreZ = (room.MinZ + room.MaxZ) / 2;
        var across = Math.Min(Math.Abs(dx) > 1e-6 ? (room.MaxX - room.MinX) / 2 / Math.Abs(dx) : double.MaxValue,
                              Math.Abs(dz) > 1e-6 ? (room.MaxZ - room.MinZ) / 2 / Math.Abs(dz) : double.MaxValue);
        double standX = centreX + dx * (across + OutOfTheRoom), standZ = centreZ + dz * (across + OutOfTheRoom);
        return ((int)Math.Round(standX), (int)Math.Round(standZ),
                (int)Math.Round(standX + dx * DownTheRoad), (int)Math.Round(standZ + dz * DownTheRoad));
    }
}
