namespace PgmStudio.Client.Components;

/// <summary>
/// What a core's <c>float</c> and <c>leak</c> pair costs a player: how many blocks they must dig into the
/// terrain under the casing before its lava can leak. Two steps read it live as an author drags the two
/// numbers — the plan tool's marker panel and the Configure wizard's casing step — which is why it is a
/// derivation here and not a round trip for an integer.
///
/// <para><b><c>PgmStudio.Domain.ObjectiveDefaults.DigDepth</c> is the authority</b>, and this is the client's
/// copy of it because the WASM half references <c>Contracts</c> and <c>Geom</c> only and cannot reach
/// <c>Domain</c>. It is <em>one</em> copy rather than the two the two steps had, and
/// <c>CoreDigDepthDriftTests</c> pins it to the authority so the pair cannot part.</para>
/// </summary>
public static class CoreDig
{
    /// <summary>How far players must dig under a core with this <paramref name="leak"/> and
    /// <paramref name="floatBlocks"/>. Escaping lava free-falls to the first air cell over the terrain, which
    /// the float puts at <c>B − float</c>, and PGM's leak region is tested against the lava block's centre —
    /// so a block resting exactly at <c>B − leak</c> is half a block too high and the core leaks one course
    /// lower than the leak level reads.</summary>
    public static int Depth(int leak, int floatBlocks) => Math.Max(0, leak + 1 - floatBlocks);
}
