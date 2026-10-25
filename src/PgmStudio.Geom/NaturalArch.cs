namespace PgmStudio.Geom;

/// <summary>
/// A natural arch laid along a line: a deck whose height changes along the span, with rock hanging under it. The
/// deck is the walkable surface and the rock is how far the arch's underside is below it, so the gap a player
/// can walk under is whatever lies between the underside and the ground.
///
/// <para>A column is described by where it lies along the span and how far it is from the centreline.
/// <c>Along</c> runs 0 at one foot to 1 at the other and the arch is mirror-symmetric about its crown at
/// <c>Along = 0.5</c>. The deck stands at <c>crown + (end − crown) · t^power</c> with <c>t</c> the distance from
/// the crown as a fraction of the half-span, and falls <see cref="Camber"/> blocks towards the sides; the rock is
/// <c>crownThick + (endThick − crownThick) · t^thickPower</c> blocks thick under it. One run per column: the arch is
/// convex in the column model, and what is under it belongs to the ground beneath.</para>
/// </summary>
public readonly record struct NaturalArch(
    double HalfWidth, double CrownDeck, double EndDeck, double DeckPower,
    double CrownThick, double EndThick, double ThickPower, double Camber = 0)
{
    /// <summary>The power a deck or a thickness takes where none is stated: a straight easing.</summary>
    public const double DefaultPower = 1;

    /// <summary>The rock thickness (crown, end, power) of an arch that states none.</summary>
    public static readonly double[] DefaultThick = [4, 12, 1.5];

    /// <summary>The arch a stated <c>deck</c> and <c>thick</c> describe. A deck is <c>[crown, end]</c> or
    /// <c>[crown, end, power]</c>; a thickness the same; either absent takes the arch's default: a deck flat at
    /// <paramref name="flatDeck"/>, rock <see cref="DefaultThick"/>.</summary>
    public static NaturalArch Of(double halfWidth, double[]? deck, double[]? thick, double camber, double flatDeck)
    {
        double crown = deck is { Length: >= 2 } ? deck[0] : flatDeck, end = deck is { Length: >= 2 } ? deck[1] : flatDeck;
        var deckPower = deck is { Length: >= 3 } ? deck[2] : DefaultPower;
        var rock = thick is { Length: >= 2 } ? thick : DefaultThick;
        return new NaturalArch(halfWidth, crown, end, deckPower, rock[0], rock[1],
                               rock.Length >= 3 ? rock[2] : DefaultPower, camber);
    }

    /// <summary>The run a column makes: <c>Floor</c> where the rock ends and <c>Top</c> one block over the
    /// deck. <paramref name="along"/> is the column's place along the span (0 to 1) and
    /// <paramref name="distance"/> how far it is from the centreline.</summary>
    public (int Floor, int Top) Run(double along, double distance)
    {
        var fromCrown = Math.Abs(2 * along - 1);
        var side = HalfWidth > 0 ? distance / HalfWidth : 0;
        var deck = Round(CrownDeck + (EndDeck - CrownDeck) * Math.Pow(fromCrown, DeckPower) - Camber * side * side);
        var rock = Math.Max(0, Round(CrownThick + (EndThick - CrownThick) * Math.Pow(fromCrown, ThickPower)));
        return (deck - rock, deck + 1);
    }

    private static int Round(double value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);
}
