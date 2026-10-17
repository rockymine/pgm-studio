using PgmStudio.Domain;
using PgmStudio.Geom;

namespace PgmStudio.Pgm.Authoring;

/// <summary>
/// The approach wall raised over a seam two blocks thick, and the defence chests that serve it (ST4). The wall
/// is the line down the middle of the seam's box; the chests stand on the ground one block out from the face
/// the approach is on, fronting away from the wall — one on a lane up to <see cref="SingleChestMaxLane"/>
/// blocks long, two on a longer one, evenly spaced along it.
/// </summary>
public static class ApproachWalls
{
    /// <summary>A lane this long or shorter carries one chest; a longer one carries two.</summary>
    public const int SingleChestMaxLane = 10;

    /// <summary>The wall over the box <c>[minX, maxX) × [minZ, maxZ)</c>, thin along whichever axis spans
    /// less, and its chests on the low-coordinate face where <paramref name="chestOnMinFace"/> and the high one
    /// where not, the <c>i</c>th stamped <paramref name="chestStamp"/>(<c>i</c>).</summary>
    public static (WallStructure Wall, List<ChestMarker> Chests) OverSeam(
        BlockRect box, int topY, bool chestOnMinFace, StampId wallStamp, Func<int, StampId> chestStamp)
    {
        var thinAlongX = box.Width <= box.Depth;
        var wall = thinAlongX
            ? new WallStructure((box.MinX + box.MaxX) / 2, box.MinZ, (box.MinX + box.MaxX) / 2, box.MaxZ, topY, wallStamp)
            : new WallStructure(box.MinX, (box.MinZ + box.MaxZ) / 2, box.MaxX, (box.MinZ + box.MaxZ) / 2, topY, wallStamp);

        var lane = thinAlongX ? box.Depth : box.Width;
        var count = lane <= SingleChestMaxLane ? 1 : 2;
        var facing = (thinAlongX, chestOnMinFace) switch
        {
            (true, true) => RoomEdge.NegX,
            (true, false) => RoomEdge.PosX,
            (false, true) => RoomEdge.NegZ,
            _ => RoomEdge.PosZ,
        };
        var outside = thinAlongX ? (chestOnMinFace ? box.MinX - 1 : box.MaxX) : (chestOnMinFace ? box.MinZ - 1 : box.MaxZ);
        var chests = new List<ChestMarker>();
        for (var i = 0; i < count; i++)
        {
            var along = (thinAlongX ? box.MinZ : box.MinX) + (int)((long)(i + 1) * lane / (count + 1));
            chests.Add(thinAlongX
                ? new ChestMarker(outside, along, facing.Word(), chestStamp(i))
                : new ChestMarker(along, outside, facing.Word(), chestStamp(i)));
        }
        return (wall, chests);
    }
}
