namespace PgmStudio.Vocabulary;

/// <summary>Where a wool's attackers take it from: a spawner the studio writes into the room, or a chest the
/// world already holds there with the wool in it, which PGM refills by itself.</summary>
public static class WoolSupplies
{
    /// <summary>A <c>&lt;spawner&gt;</c> dropping the wool at its spawn point while an attacker is in the room.</summary>
    public const string Spawner = "spawner";

    /// <summary>A chest at the wool's location holding the wool: PGM takes any container holding objective wool
    /// when it is first opened as a wool chest and puts one back per slot every 30 seconds.</summary>
    public const string Chest = "chest";

    /// <summary>Every supply, the default first.</summary>
    public static readonly string[] All = [Spawner, Chest];
}
