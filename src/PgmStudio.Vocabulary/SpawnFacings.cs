namespace PgmStudio.Vocabulary;

/// <summary>
/// Which way a player looks on arriving, in absolute board directions on the authored unit. Eight of them:
/// the four walls and the four corners between them, each fanned per orbit image so the authored word is
/// turned rather than repeated.
///
/// <para>A facing is a <b>direction</b>, not a door. A room's doors are cut where its piece abuts more board
/// (docs/world-export/structures.md <c>WX6</c>), which is what lets a corner spawn open on two sides and a
/// player look diagonally between them; the yaw is <see cref="Direction"/> fanned through the orbit and
/// carries whatever angle that gives, 45° included.</para>
///
/// <para>Three parties spell these: the plan states one, the compiler turns it into a yaw, and the canvas
/// draws the arrow.</para>
/// </summary>
public static class SpawnFacings
{
    /// <summary>Toward −z.</summary>
    public const string Front = "front";

    /// <summary>Toward +z.</summary>
    public const string Back = "back";

    /// <summary>Toward −x.</summary>
    public const string Left = "left";

    /// <summary>Toward +x.</summary>
    public const string Right = "right";

    /// <summary>Toward −x−z.</summary>
    public const string FrontLeft = "front-left";

    /// <summary>Toward +x−z.</summary>
    public const string FrontRight = "front-right";

    /// <summary>Toward −x+z.</summary>
    public const string BackLeft = "back-left";

    /// <summary>Toward +x+z.</summary>
    public const string BackRight = "back-right";

    /// <summary>The eight, walking the compass from <c>front</c> clockwise, which is the order a picker
    /// offers them in and the order a quarter turn walks two at a time.</summary>
    public static readonly string[] All =
        [Front, FrontRight, Right, BackRight, Back, BackLeft, Left, FrontLeft];

    /// <summary>The unit step a facing points along, <c>(0, -1)</c> for anything unrecognised. A diagonal is
    /// a unit on both axes rather than a normalised vector: the yaw is an angle and the length is never read,
    /// and integers are what a fan through the symmetry transform stays exact in.</summary>
    public static (int Dx, int Dz) Direction(string? facing) => facing switch
    {
        Back => (0, 1),
        Left => (-1, 0),
        Right => (1, 0),
        FrontLeft => (-1, -1),
        FrontRight => (1, -1),
        BackLeft => (-1, 1),
        BackRight => (1, 1),
        _ => (0, -1),
    };

    /// <summary>The word for a step, or null where the step is not one of the eight. The inverse of
    /// <see cref="Direction"/> over the unit steps, which is what reads a fanned direction back as a word.</summary>
    public static string? Word(int dx, int dz) => (Math.Sign(dx), Math.Sign(dz)) switch
    {
        (0, -1) => Front,
        (0, 1) => Back,
        (-1, 0) => Left,
        (1, 0) => Right,
        (-1, -1) => FrontLeft,
        (1, -1) => FrontRight,
        (-1, 1) => BackLeft,
        (1, 1) => BackRight,
        _ => null,
    };

    /// <summary>Whether the facing points between two walls rather than at one.</summary>
    public static bool IsDiagonal(string? facing) => Direction(facing) is { Dx: not 0, Dz: not 0 };
}
