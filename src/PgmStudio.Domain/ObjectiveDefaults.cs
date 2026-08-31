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
    /// terrain, and that breaking it means committing to the climb. A <b>minimum</b> in spirit —
    /// <see cref="MaxFloat"/> is the other end.</summary>
    public const int DestroyableFloat = 4;

    // ── core (DTC) ─────────────────────────────────────────────────────────────────────────────────────

    /// <summary>The lava's own footprint, in blocks — <b>what an author states</b>, and one of the three
    /// knobs a core has. 3 leaves the 5×5×5 obsidian casing that dominates the corpus (DC1).</summary>
    public const int CoreLava = 3;

    /// <summary>How many courses of lava stand inside the casing. The second knob.</summary>
    public const int CoreLavaHeight = 3;

    /// <summary>The closed range each of the two takes. A core is chosen from presets rather than designed:
    /// stating the <em>interior</em> and bounding it is what makes an impossible casing unstatable, where
    /// independent size and shell knobs let one be typed and refused a phase later.</summary>
    public const int MinCoreLava = 2, MaxCoreLava = 5;
    public const int MinCoreLavaHeight = 2, MaxCoreLavaHeight = 5;

    /// <summary>Casing thickness. Not a knob: 1 block in 65% of corpus cores, and the wall a lava footprint
    /// is measured inside — the author states the lava and the casing follows from it.</summary>
    public const int CoreShell = 1;

    /// <summary>The casing a stated interior implies: its width and depth are the lava's footprint walled on
    /// both sides, and its height the lava's courses plus the floor, plus the cap where there is one. An open
    /// top is a course of casing the author gave up, so the same lava height reads one block shorter.</summary>
    public static (int Size, int Height) CoreCasing(int lava, int lavaHeight, bool openTop) =>
        (lava + 2 * CoreShell, lavaHeight + (openTop ? CoreShell : 2 * CoreShell));

    /// <summary>Blocks of air under the casing. Pairs with <see cref="CoreLeak"/> (DC2).</summary>
    public const int CoreFloat = 6;

    /// <summary>How far lava must fall below the casing to count as leaked — the corpus mode, and PGM's own
    /// default. Pairs with <see cref="CoreFloat"/> (DC2).</summary>
    public const int CoreLeak = 5;

    /// <summary>How high over the ground a goal may float, in blocks (the author's ceiling). The other end of
    /// <see cref="DestroyableFloat"/> and <see cref="CoreFloat"/>, which are both floors: without a maximum an
    /// authored <c>float</c> puts a goal anywhere, and a goal high enough to need a tower under it is a goal
    /// nobody reaches. Read against the <em>stated</em> number, which is why it is a plan rule rather than a
    /// world one — the derived check the built terrain makes possible is the goal's box against
    /// <see cref="BuildCeiling"/>.</summary>
    public const int MaxFloat = 12;

    /// <summary>
    /// How many blocks players must dig into the terrain under a core before its lava can leak (DC2).
    ///
    /// <para>Escaping lava free-falls to the first air cell over the terrain, which the float puts at
    /// <c>B − float</c>, and PGM's leak is one course lower than the leak level reads: its leak region spans
    /// up to <c>y = B − leak</c> and is tested against the lava block's <b>centre</b>, so a block at
    /// <c>B − leak</c> is half a block too high and the core leaks at <c>y ≤ B − leak − 1</c>. PGM's own
    /// <c>leakRequired</c> says the same thing arithmetically — <c>lavaBottom − (B − leak) + 1</c>, reached
    /// only at <c>y = B − leak − 1</c> (<c>Core.java</c>, <c>CoreMatchModule.leakCheck</c>).</para>
    ///
    /// <para>So a core with <c>leak &lt; float</c> leaks the moment its casing is breached and one with
    /// <c>leak ≥ float</c> makes digging part of the capture. Both are legitimate; the author picks. The
    /// defaults here give 0 — no dig, matching the corpus centre.</para>
    /// </summary>
    public static int DigDepth(int leak, int floatBlocks) => Math.Max(0, leak + 1 - floatBlocks);
}
