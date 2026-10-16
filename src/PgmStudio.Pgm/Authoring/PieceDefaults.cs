namespace PgmStudio.Pgm.Authoring;

/// <summary>What a play piece is given where nothing states it: the plan compiler and a piece added in the sketch
/// name and colour pieces the same way.</summary>
public static class PieceDefaults
{
    /// <summary>The dyes a team's wools after its first take, in order, so every wool across the board keys to a
    /// colour of its own.</summary>
    public static readonly string[] Dyes =
        ["orange", "light_blue", "pink", "lime", "cyan", "purple", "magenta", "white", "gray", "brown", "black", "silver"];

    /// <summary>A destroyable's name: "Red Monument", then "Red Monument 2" for a team's second. PGM rejects a
    /// nameless destroyable, and "Monument" is its own player-facing word for one.</summary>
    public static string MonumentName(string teamName, int index) =>
        index == 0 ? $"{teamName} Monument" : $"{teamName} Monument {index + 1}";
}
