namespace PgmStudio.Pgm.Editing;

/// <summary>
/// Canonical team-colour ordering for synthesizing teams from a symmetry (declarative authoring §4).
/// Mirrors the priority list the editor UI already uses for manual team-add
/// (<c>PgmStudio.Client/Models/GameColors.cs</c> → <c>TeamColorPriority</c> / <c>NextTeamColor</c>) — that
/// one carries hex for rendering and lives in the WASM layer, which the backend generator can't reference,
/// so the bare ordering is duplicated here on purpose. Keep the two in sync.
/// <para>Corpus-grounded (350 CTW maps): 2-team is overwhelmingly red/blue, 4-team red/blue/green/yellow;
/// the tail covers the rare 6/8-team maps.</para>
/// </summary>
public static class TeamPalette
{
    /// <summary>Colours in assignment order; team <c>k</c> in an orbit gets <c>Order[k]</c>.</summary>
    public static readonly IReadOnlyList<string> Order =
    [
        "red", "blue", "green", "yellow", "aqua", "gold", "light purple", "dark purple",
        "dark aqua", "dark green", "dark red", "dark blue", "gray", "dark gray", "white", "black",
    ];

    /// <summary>The first <paramref name="n"/> palette colours (clamped to the palette length).</summary>
    public static IReadOnlyList<string> Take(int n) => Order.Take(Math.Clamp(n, 0, Order.Count)).ToList();

    /// <summary>Display name for a colour ("dark red" → "Dark Red").</summary>
    public static string Label(string color) =>
        string.Join(' ', color.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => char.ToUpperInvariant(w[0]) + w[1..]));
}
