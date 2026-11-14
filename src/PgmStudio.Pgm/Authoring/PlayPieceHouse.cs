using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Authoring;

/// <summary>
/// Set a room's house whole, by the room's id: its wings and the style it states. The named image takes the
/// house as posted, and unless <c>alone</c> every other image of its unit takes it carried through the symmetry
/// step between them, a wing's ridge turning with a quarter turn and every chest and seat it states carried with
/// its wings. Refused where the house has no wing, a wing is under <see cref="RoomFrames.MinFootprintSpan"/> blocks
/// across, a wing reaches outside the room's region, the room's wing no longer holds the room's point, a stated
/// cell is not an <c>[x, z]</c>, or the house states chests for a spawn or seats for a wool. Where the cells stand
/// in the room is the room's own check (<c>RoomCheck.Interior</c>), since it needs the frame the map's style
/// raises.
/// </summary>
public static class PlayPieceHouse
{
    /// <summary>The intent with the house of the room <paramref name="id"/> made <paramref name="house"/>, or
    /// the findings that stopped it and a null intent.</summary>
    public static (MapIntent? Intent, Findings Refused) Apply(
        MapIntent intent, string id, RoomHouse house, bool alone = false, SymmetryIntent? board = null)
    {
        var symmetry = intent.Symmetry ?? board;
        if (intent.Spawns.FindIndex(spawn => spawn.Id == id) is var spawnAt and >= 0)
        {
            var spawn = intent.Spawns[spawnAt];
            if (Refused(house, spawn.Protection, spawn.Point, id, spawn: true) is { } refused) return (null, refused);
            return (intent with
            {
                Spawns = PlayPieceMove.Images(intent.Spawns, spawnAt, entry => entry.Stamp, symmetry?.Mode, alone,
                    (entry, step) => entry with { House = Carried(house, symmetry, step) }),
            }, Findings.None);
        }
        var wools = intent.Wools ?? [];
        if (wools.FindIndex(wool => wool.Id == id) is var woolAt and >= 0)
        {
            var wool = wools[woolAt];
            if (Refused(house, wool.Protection, wool.Spawn, id, spawn: false) is { } refused) return (null, refused);
            return (intent with
            {
                Wools = PlayPieceMove.Images(wools, woolAt, entry => entry.Stamp, symmetry?.Mode, alone,
                    (entry, step) => entry with { House = Carried(house, symmetry, step) }),
            }, Findings.None);
        }
        return (null, Findings.Of(new Finding(RequestRules.NoSuchSubject,
            $"the map has no spawn or wool room with the id '{id}'", Field: "id", Subjects: [id])));
    }

    private static Findings? Refused(RoomHouse house, IReadOnlyList<Rect> region, Pt point, string id, bool spawn)
    {
        var (unused, unusedField) = spawn ? (house.Chests, "chests") : (house.Monuments, "monuments");
        if (unused is not null)
            return Findings.Of(new Finding(RequestRules.Unreadable,
                $"the house of '{id}' states `{unusedField}`, which a {(spawn ? "spawn" : "wool")} room does not hold",
                Field: $"house.{unusedField}", Subjects: [id]));
        foreach (var (cells, field) in new[] { (house.Chests, "chests"), (house.Monuments, "monuments") })
            for (var at = 0; at < (cells?.Count ?? 0); at++)
                if (cells![at] is not { Length: 2 })
                    return Findings.Of(new Finding(RequestRules.Unreadable,
                        $"cell {at} of the house's `{field}` is not an x and a z", Field: $"house.{field}[{at}]"));
        if (house.Wings.Count == 0)
            return Findings.Of(new Finding(RequestRules.Unreadable, "the house holds no wing", Field: "house.wings"));
        for (var at = 0; at < house.Wings.Count; at++)
        {
            if (house.Wings[at].Corners is not { Count: 2 } corners || corners.Any(corner => corner.Length != 2))
                return Findings.Of(new Finding(RequestRules.Unreadable,
                    $"wing {at} is not two corners with an x and a z each", Field: $"house.wings[{at}]"));
            var cells = RoomHouse.RectOf(house.Wings[at]);
            if (cells.MaxX - cells.MinX < RoomFrames.MinFootprintSpan || cells.MaxZ - cells.MinZ < RoomFrames.MinFootprintSpan)
                return Findings.Of(new Finding(RoomFrameRules.FootprintTooSmall,
                    $"wing {at} is {cells.MaxX - cells.MinX:0} by {cells.MaxZ - cells.MinZ:0} blocks, less than "
                    + $"{RoomFrames.MinFootprintSpan} blocks across its shorter side", Field: $"house.wings[{at}]", Subjects: [id]));
            if (region.Count > 0 && !Inside(cells, region))
                return Findings.Of(new Finding(RoomFrameRules.FootprintOffPiece,
                    $"wing {at} of the house of '{id}' would reach outside the region of its room",
                    Field: $"house.wings[{at}]", Subjects: [id]));
        }
        var room = house.Room!.Value;
        var (x, z) = (Math.Floor(point.X), Math.Floor(point.Z));
        if (x < room.MinX || x >= room.MaxX || z < room.MinZ || z >= room.MaxZ)
            return Findings.Of(new Finding(RequestRules.Unreadable,
                $"the marker of '{id}' would stand outside its room's wing", Field: "house.wings[0]",
                Subjects: [PlayIds.Marker(id)]));
        return null;
    }

    /// <summary>Whether every cell of <paramref name="cells"/> lies in one of the rectangles of
    /// <paramref name="region"/>.</summary>
    private static bool Inside(Rect cells, IReadOnlyList<Rect> region)
    {
        for (var x = cells.MinX; x < cells.MaxX; x++)
            for (var z = cells.MinZ; z < cells.MaxZ; z++)
                if (!region.Any(rect => x >= rect.MinX && x < rect.MaxX && z >= rect.MinZ && z < rect.MaxZ))
                    return false;
        return true;
    }

    /// <summary>The house carried <paramref name="step"/> images round the orbit: every wing's cells mapped
    /// about the board's centre, a wing's ridge swapping axis where the step turns x onto z, and the chests and
    /// seats carried with the wings.</summary>
    private static RoomHouse Carried(RoomHouse house, SymmetryIntent? symmetry, int step)
    {
        if (step == 0 || symmetry is null) return house;
        var (ax, az) = Symmetry.Point(1, 0, symmetry.Mode, 0, 0, step);
        var swaps = Math.Abs(az) > Math.Abs(ax);
        var carried = house.Mapped(cells =>
        {
            var (minX, minZ, maxX, maxZ) = Symmetry.Rect(cells.MinX, cells.MinZ, cells.MaxX, cells.MaxZ,
                symmetry.Mode, symmetry.CenterX, symmetry.CenterZ, step);
            return new Rect(Math.Round(minX), Math.Round(minZ), Math.Round(maxX), Math.Round(maxZ));
        });
        if (!swaps) return carried;
        return carried with
        {
            Wings = [.. carried.Wings.Select(wing => wing.Spec.Ridge is { } axis
                ? wing with { Spec = wing.Spec with { Ridge = axis == RidgeAxis.AlongX ? RidgeAxis.AlongZ : RidgeAxis.AlongX } }
                : wing)],
        };
    }
}
