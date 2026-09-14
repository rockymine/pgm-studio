namespace PgmStudio.Domain;

/// <summary>
/// What a player meets a control point as.
///
/// <para>PGM names one from the <c>name</c> attribute where the document states it, and otherwise off a
/// counter: the first unnamed point is <c>Hill</c>, the next <c>Hill 2</c>, then <c>Hill 3</c>
/// (<c>ControlPointParser</c>). Only an unnamed point advances the counter, so a named point between two
/// unnamed ones does not consume a number.</para>
///
/// <para>Stated here because two parties have to spell it identically. The generator names the points an
/// intent states; the playability reads name the points a <em>document</em> carries, and merge the two by
/// name — so a rule written out twice would count one point as two the moment the copies drifted.</para>
/// </summary>
public static class ControlPointNaming
{
    /// <summary>What PGM calls it where the author named nothing.</summary>
    public const string Unnamed = "Hill";

    /// <summary>The display name of each point in turn, given what each states — null or empty for one the
    /// author did not name.</summary>
    public static IEnumerable<string> Displayed(IEnumerable<string?> stated)
    {
        var unnamed = 0;
        foreach (var name in stated)
        {
            if (name is { Length: > 0 }) { yield return name; continue; }
            yield return ++unnamed > 1 ? $"{Unnamed} {unnamed}" : Unnamed;
        }
    }
}
