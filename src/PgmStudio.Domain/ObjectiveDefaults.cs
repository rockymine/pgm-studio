namespace PgmStudio.Domain;

/// <summary>
/// The default shape of a generated DTM/DTC objective — each value the corpus's own centre of mass, so a
/// bare marker authors a typical structure and the optional plan fields exist for the exceptions.
/// <para>One home, below both the plan layer that defaults an unauthored field and the stamper that builds
/// the blocks. They must agree: a compiler that defaults the float to 4 while the stamper assumes 6 produces
/// a structure at a height nothing asked for, and nothing would report it.</para>
/// </summary>
public static class ObjectiveDefaults
{
    // ── destroyable (DTM) ──────────────────────────────────────────────────────────────────────────────

    /// <summary>The 1×3×1 pillar — tall enough to read as a monument, small enough to break in a raid.</summary>
    public const DestroyableStyle Style = DestroyableStyle.Pillar3;

    /// <summary>Over half the corpus, and the right structure for a goal: opaque, blast-resistant, unmistakable.</summary>
    public const string Materials = "obsidian";

    /// <summary>Blocks of air under a destroyable (DT3): enough that it reads as a monument rather than
    /// terrain, and that breaking it means committing to the climb.</summary>
    public const int DestroyableFloat = 4;

    // ── core (DTC) ─────────────────────────────────────────────────────────────────────────────────────

    /// <summary>Casing width/depth: 5×5×5 obsidian is the dominant corpus casing (DC1).</summary>
    public const int CoreSize = 5;
    public const int CoreHeight = 5;

    /// <summary>Casing thickness — 1 block in 65% of corpus cores, leaving a 3×3×3 lava interior.</summary>
    public const int CoreShell = 1;

    /// <summary>Blocks of air under the casing. Pairs with <see cref="CoreLeak"/> (DC2).</summary>
    public const int CoreFloat = 6;

    /// <summary>How far lava must fall below the casing to count as leaked — the corpus mode, and PGM's own
    /// default. Pairs with <see cref="CoreFloat"/> (DC2).</summary>
    public const int CoreLeak = 5;

    /// <summary>
    /// How many blocks players must dig into the terrain under a core before its lava can leak (DC2).
    /// Escaping lava free-falls to the terrain at <c>B − float</c> while the core leaks at <c>y ≤ B − leak</c>,
    /// so a core with <c>leak ≤ float</c> leaks the moment its casing is breached and one with
    /// <c>leak &gt; float</c> makes digging part of the capture. Both are legitimate; the author picks. The
    /// defaults here give 0 — no dig, matching the corpus centre.
    /// </summary>
    public static int DigDepth(int leak, int floatBlocks) => Math.Max(0, leak - floatBlocks);
}
