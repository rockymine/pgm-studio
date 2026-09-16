namespace PgmStudio.Vocabulary;

/// <summary>
/// The five sizes a CTW map is built at, named by the players-per-team band each serves. A map is not built
/// for one count — it works across a range — so the size a board is composed at is the band, and the player
/// count only selects it.
///
/// <para>Three parties spell these: the composer keys its land budget and its corridor width on a band, the
/// compose surface answers which band a board was built to, and the client offers them as the sizes a caller
/// may ask for.</para>
/// </summary>
public static class SizeBands
{
    /// <summary>6–13 players a team.</summary>
    public const string Nano = "nano";

    /// <summary>14–21.</summary>
    public const string Micro = "micro";

    /// <summary>22–31.</summary>
    public const string Milli = "milli";

    /// <summary>32–47.</summary>
    public const string Centi = "centi";

    /// <summary>48 and up.</summary>
    public const string Hecto = "hecto";

    /// <summary>The five, smallest first.</summary>
    public static readonly string[] All = [Nano, Micro, Milli, Centi, Hecto];

    /// <summary>The band a per-team player count is served by. A count below the nano band's floor takes the
    /// nano band — there is no smaller size to build, and the smallest maps measured sit in it.</summary>
    public static string Of(int playersPerTeam) =>
        playersPerTeam >= 48 ? Hecto
        : playersPerTeam >= 32 ? Centi
        : playersPerTeam >= 22 ? Milli
        : playersPerTeam >= 14 ? Micro
        : Nano;

    /// <summary>The band's place in the ladder, smallest first, so an ordering over bands runs by size rather
    /// than alphabetically. A word nothing matches ranks with <see cref="Nano"/>.</summary>
    public static int Rank(string? band) => Math.Max(0, Array.IndexOf(All, Canonical(band)));

    /// <summary>The band's player range, as the lowest and highest per-team count it serves — the upper end of
    /// <see cref="Hecto"/> being open, stated as the highest count a request may carry.</summary>
    public static (int Low, int High) Players(string? band) => Canonical(band) switch
    {
        Micro => (14, 21),
        Milli => (22, 31),
        Centi => (32, 47),
        Hecto => (48, 64),
        _ => (6, 13),
    };

    /// <summary>The word, or <see cref="Nano"/> for one that is not a band.</summary>
    public static string Canonical(string? band) => All.Contains(band) ? band! : Nano;
}
