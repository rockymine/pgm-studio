namespace PgmStudio.Vocabulary;

/// <summary>
/// What a play piece the sketch draws is. The studio names a piece's kind on the wire, the client draws and
/// labels it by the word, and a room's two kinds are spelled as the room's region words are.
/// </summary>
public static class PlayPieceKinds
{
    /// <summary>A team's spawn room.</summary>
    public const string Spawn = StructuralRoles.Spawn;

    /// <summary>A wool room.</summary>
    public const string WoolRoom = StructuralRoles.WoolRoom;

    /// <summary>A destroyable objective.</summary>
    public const string Destroyable = "destroyable";

    /// <summary>A core objective.</summary>
    public const string Core = "core";

    /// <summary>A build area: ground over the void a player may build on.</summary>
    public const string BuildArea = "buildArea";

    /// <summary>A water lane: a gap that opens part-way through the match.</summary>
    public const string WaterLane = "waterLane";

    /// <summary>An iron cube.</summary>
    public const string IronCube = "ironCube";

    /// <summary>A control point: a pad a team holds by standing on it.</summary>
    public const string ControlPoint = "controlPoint";

    /// <summary>A bedrock approach wall.</summary>
    public const string Wall = "wall";

    /// <summary>A defence chest, standing on the ground at one block.</summary>
    public const string Chest = "chest";

    /// <summary>The observer's spawn.</summary>
    public const string Observer = "observer";

    /// <summary>The point a spawn's team arrives at, inside its spawn room, which it comes and goes with.</summary>
    public const string SpawnPoint = "spawnPoint";

    /// <summary>The point a wool is dispensed at, inside its wool room, the same way.</summary>
    public const string WoolSpawn = "woolSpawn";

    /// <summary>Every kind.</summary>
    public static readonly string[] All = [Spawn, WoolRoom, Destroyable, Core, BuildArea, WaterLane, IronCube, ControlPoint, Wall, Chest, Observer, SpawnPoint, WoolSpawn];
}
