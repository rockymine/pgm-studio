namespace PgmStudio.Client.Components;

/// <summary>
/// The obsidian a core's stated interior implies: its width and depth are the lava's footprint walled in one
/// block on every side, and its height the lava's courses plus the floor, plus the cap unless the top is open.
/// The plan tool's marker panel and the Configure wizard's casing step both show it live as an author drags
/// the lava numbers.
///
/// <para><b><c>PgmStudio.Domain.ObjectiveDefaults.CoreCasing</c> is the authority</b>, and this is the
/// client's copy of it because the WASM half references <c>Contracts</c> and <c>Geom</c> only and cannot reach
/// <c>Domain</c>. <c>CoreCasingDriftTests</c> pins it to the authority so the pair cannot part.</para>
/// </summary>
public static class CoreCasing
{
    private const int Shell = 1;

    /// <summary>The casing's footprint and height around <paramref name="lava"/> blocks of lava,
    /// <paramref name="lavaHeight"/> courses deep.</summary>
    public static (int Size, int Height) Of(int lava, int lavaHeight, bool openTop)
        => (lava + 2 * Shell, lavaHeight + (openTop ? Shell : 2 * Shell));
}
