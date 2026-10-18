namespace PgmStudio.Pgm.Derive;

/// <summary>The ground a board's structure is read off, in cells of <see cref="Cell"/> blocks: what stands, who
/// owns each cell and which team's image it is, where building is allowed, the voids an author declared, which
/// owners host a spawn or a wool, every wool room at every image, and the authored image's own terrain. A plan
/// states one (<see cref="BoardDeriver.Ground"/>), and <see cref="BoardDeriver.Derive(BoardGround)"/> reads any.</summary>
/// <param name="Cell">How many blocks a cell spans.</param>
/// <param name="Filled">Every standing cell, with the owner a finding names it by and the orbit image it is,
/// which is the team it belongs to.</param>
/// <param name="Build">The cells a player may build in.</param>
/// <param name="DeclaredVoid">The empty cells an author meant to leave empty.</param>
/// <param name="SpawnOwners">The owners a spawn stands on.</param>
/// <param name="WoolOwners">The owners a wool room stands on.</param>
/// <param name="WoolRooms">Each wool's room at each image, the first image's first.</param>
/// <param name="UnitFilled">The standing cells of the authored image alone, which a lane's shape is read on.</param>
public sealed record BoardGround(
    int Cell,
    Dictionary<(int, int), (string Owner, int K)> Filled,
    HashSet<(int, int)> Build,
    HashSet<(int, int)> DeclaredVoid,
    IReadOnlySet<string> SpawnOwners,
    IReadOnlySet<string> WoolOwners,
    IReadOnlyList<BoardWoolRoom> WoolRooms,
    HashSet<(int, int)> UnitFilled);

/// <summary>One wool's room at one image: the cells it covers and its marker, in cells.</summary>
/// <param name="Wool">Which wool this is, counted in the order the board states them.</param>
/// <param name="K">Which orbit image.</param>
/// <param name="Cells">The cells the room covers.</param>
/// <param name="MarkerX">The wool's marker, east–west, in cells.</param>
/// <param name="MarkerZ">The marker, north–south.</param>
public sealed record BoardWoolRoom(int Wool, int K, HashSet<(int, int)> Cells, double MarkerX, double MarkerZ);
