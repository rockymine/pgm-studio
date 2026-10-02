using PgmStudio.Minecraft.Houses;
using PgmStudio.Minecraft.Library;

namespace PgmStudio.Minecraft.Tests;

/// <summary>The seeded houses the tests build their variations from, each read out of the seed folder by the name
/// it is filed under.</summary>
internal static class SeededHouses
{
    /// <summary>Acacia logs checkered between spruce posts, on a mixed stone plinth.</summary>
    public static HouseStyle Alpine => SeedFolder.House("acacia-log-checkered-house");

    /// <summary>Sandstone under a brick roof, with an arched doorway.</summary>
    public static HouseStyle Desert => SeedFolder.House("brick-roofed-sandstone-house");

    /// <summary>Two timbered storeys of oak and spruce.</summary>
    public static HouseStyle Townside => SeedFolder.House("oak-and-spruce-timbered-house");

    /// <summary>The timbered house raised on stilts over a plate of air.</summary>
    public static HouseStyle Stilts => SeedFolder.House("jungle-trimmed-stilt-house");

    public static HouseStyle Cottage => SeedFolder.House("spruce-roofed-stone-cottage");

    public static HouseStyle Longhouse => SeedFolder.House("spruce-roofed-stone-longhouse");

    public static HouseStyle Counting => SeedFolder.House("stone-and-sandstone-townhouse");

    public static HouseStyle Darkwood => SeedFolder.House("black-clay-and-dark-oak-house");

    /// <summary>The eight, by the names they are filed under.</summary>
    public static IReadOnlyList<(string Name, HouseStyle Style)> All =>
    [
        .. new[]
        {
            "acacia-log-checkered-house", "brick-roofed-sandstone-house", "oak-and-spruce-timbered-house",
            "jungle-trimmed-stilt-house", "spruce-roofed-stone-cottage", "spruce-roofed-stone-longhouse",
            "stone-and-sandstone-townhouse", "black-clay-and-dark-oak-house",
        }.Select(name => (name, SeedFolder.House(name))),
    ];
}
