using PgmStudio.Minecraft.Houses;

namespace PgmStudio.Minecraft.Dressing;

/// <summary>
/// How level a building's site is, and how level it has to be (<see cref="DressingRules.SiteNotLevel"/>): the
/// arithmetic the dressing pass seats a building by and the seat query asks forwards, so the two cannot answer
/// differently about the same footprint.
/// </summary>
public static class SiteLevel
{
    /// <summary>The lowest and highest ground under <paramref name="cells"/>, or the first cell with no ground
    /// under it. Lowest is <see cref="int.MaxValue"/> where there are no cells.</summary>
    public static (int Lowest, int Highest, (int X, int Z)? Bare) Read(
        IReadOnlyDictionary<(int X, int Z), int> ground, IEnumerable<(int X, int Z)> cells)
    {
        int lowest = int.MaxValue, highest = int.MinValue;
        foreach (var cell in cells)
        {
            if (!ground.TryGetValue(cell, out var top)) return (lowest, highest, cell);
            lowest = Math.Min(lowest, top);
            highest = Math.Max(highest, top);
        }
        return (lowest, highest, null);
    }

    /// <summary>The rise a site may not reach for <paramref name="style"/> to stand on it: its wall courses
    /// plus two courses of roof per pitch, the height at which the uphill side is buried roof and all.</summary>
    public static int Limit(HouseStyle style) => style.WallCourses + 2 * Math.Max(1, style.Roof.Pitch);
}
