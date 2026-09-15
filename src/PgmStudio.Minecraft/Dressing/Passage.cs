namespace PgmStudio.Minecraft.Dressing;

/// <summary>What one side of a building has beside it: a passage
/// <see cref="DressingRules.PassAroundWidth"/> blocks deep along the building's whole run, the map's own
/// edge, or too little of either.</summary>
public enum Flank
{
    /// <summary>The full band is passable — terrain with nothing <em>built</em> on it, so a road or a
    /// channel alongside the wall is a way past and a building outside this one's group is not.</summary>
    Clear,
    /// <summary>No ground at all beside that side: the building stands flush against the board's edge or
    /// a hole in it.</summary>
    Edge,
    /// <summary>Ground, and not enough of it — the case the rule exists for.</summary>
    Short,
}

/// <summary>What a building takes up, as the passage rule reads it: the extent it <b>stamps</b>, which is
/// where the band starts, and the run of its <b>walls</b>, which is what the band runs along — an eave may
/// oversail the void at a coast, and a column the building does not stand on says nothing about the ground
/// beside it. <see cref="Join"/> makes one of these out of two, which is how a group of buildings is measured
/// as the one block of buildings a player walks round.</summary>
public readonly record struct Footing(
    int StampMinX, int StampMinZ, int StampMaxX, int StampMaxZ,
    int WallMinX, int WallMinZ, int WallMaxX, int WallMaxZ)
{
    /// <summary>A footing from the walls out: the stamp is the walls grown by the roof's own eave.</summary>
    public static Footing OfWalls(int minX, int minZ, int maxX, int maxZ, int eave) =>
        new(minX - eave, minZ - eave, maxX + eave, maxZ + eave, minX, minZ, maxX, maxZ);

    /// <summary>A footing from the stamp in, for a building read back off the ground it claims rather than
    /// off a plan: the walls are the stamp less the eave the claim was written with.</summary>
    public static Footing OfStamp(int minX, int minZ, int maxX, int maxZ, int eave) =>
        new(minX, minZ, maxX, maxZ,
            Math.Min(minX + eave, maxX), Math.Min(minZ + eave, maxZ),
            Math.Max(maxX - eave, minX), Math.Max(maxZ - eave, minZ));

    public Footing Join(Footing other) => new(
        Math.Min(StampMinX, other.StampMinX), Math.Min(StampMinZ, other.StampMinZ),
        Math.Max(StampMaxX, other.StampMaxX), Math.Max(StampMaxZ, other.StampMaxZ),
        Math.Min(WallMinX, other.WallMinX), Math.Min(WallMinZ, other.WallMinZ),
        Math.Max(WallMaxX, other.WallMaxX), Math.Max(WallMaxZ, other.WallMaxZ));

    /// <summary>Whether two buildings stand near enough that a player goes round the pair rather than
    /// between them. The reach is the passage plus the ring a building holds beyond its stamp, which is
    /// exactly what two standing side by side have to leave each other: at one block further apart each
    /// answers for itself and clears the passage on its own, so no gap between two buildings is one the
    /// rule has no reading of.</summary>
    public bool Neighbours(Footing other) =>
        Near(StampMinX, other.StampMaxX) && Near(other.StampMinX, StampMaxX)
        && Near(StampMinZ, other.StampMaxZ) && Near(other.StampMinZ, StampMaxZ);

    private static bool Near(int from, int to) =>
        from - to <= DressingRules.PassAroundWidth + DressingRules.StructureClearance;

    /// <summary>Whether this extent is a group around <paramref name="one"/> rather than <paramref name="one"/>
    /// itself — what tells a building standing alone from one standing in a village.</summary>
    public bool Holds(Footing one) =>
        StampMinX <= one.StampMinX && StampMaxX >= one.StampMaxX
        && StampMinZ <= one.StampMinZ && StampMaxZ >= one.StampMaxZ && !Equals(one);
}

/// <summary>
/// <c>DR-PASS</c>: whether a building, or the group of buildings it stands in, leaves a way past itself.
///
/// <para>One reading serves both directions. The dressing pass asks it of a placement over its own claim
/// book; <c>ClaimRaster</c> asks it of every anchor on a board over the claim raster, which is what lets
/// <c>sketch/seats</c> answer where a building may go rather than only whether one guess landed. What the two
/// differ in is how a cell is looked up, so that is what they pass in.</para>
/// </summary>
public static class Passage
{
    /// <summary>Whether <paramref name="group"/> leaves a way past itself: <b>every</b> side carries a band of
    /// passable ground <see cref="DressingRules.PassAroundWidth"/> blocks deep along the run of its walls, and
    /// a side the ground stops flush against is a coast it may stand on — but not two facing each other, which
    /// is a building spanning the land it stands on rather than one seated at its edge.
    ///
    /// <para>Over the bounding run: the notch of an L is the building's own ground, not a public route through
    /// it, and the same holds of the yard inside a ring of houses.</para></summary>
    /// <param name="isGround">Whether a cell is terrain a player stands on.</param>
    /// <param name="isBuilt">Whether a building holds that cell. The ring a group holds past its own stamp is
    /// a way past it — a ring is held so that nothing <em>seats</em> under an eave, not so that nobody passes
    /// — so this is asked only outside the group's own ring.</param>
    public static bool Clears(Footing group, Func<int, int, bool> isGround, Func<int, int, bool> isBuilt)
    {
        var depth = DressingRules.PassAroundWidth;
        var ring = DressingRules.StructureClearance;
        var (sx0, sz0, sx1, sz1) = (group.StampMinX, group.StampMinZ, group.StampMaxX, group.StampMaxZ);
        var (wx0, wz0, wx1, wz1) = (group.WallMinX, group.WallMinZ, group.WallMaxX, group.WallMaxZ);

        // Each side is grown outward from the step just off the stamp, along the run of the walls.
        return Across(Side(sx1 + 1, wz0, sx1 + 1, wz1, 1, 0), Side(sx0 - 1, wz0, sx0 - 1, wz1, -1, 0))
            && Across(Side(wx0, sz1 + 1, wx1, sz1 + 1, 0, 1), Side(wx0, sz0 - 1, wx1, sz0 - 1, 0, -1));

        // One side of a facing pair may be a coast; the other still has to be a way past.
        static bool Across(Flank near, Flank far) =>
            near != Flank.Short && far != Flank.Short && (near == Flank.Clear || far == Flank.Clear);

        Flank Side(int x0, int z0, int x1, int z1, int dx, int dz)
        {
            bool clear = true, flush = true;
            for (var step = 0; step < depth; step++)
            for (var z = z0 + step * dz; z <= z1 + step * dz; z++)
            for (var x = x0 + step * dx; x <= x1 + step * dx; x++)
            {
                if (!isGround(x, z)) { clear = false; continue; }
                if (step == 0) flush = false;
                if (isBuilt(x, z) && !Own(x, z)) clear = false;
            }
            return clear ? Flank.Clear : flush ? Flank.Edge : Flank.Short;
        }

        bool Own(int x, int z) =>
            x >= sx0 - ring && x <= sx1 + ring && z >= sz0 - ring && z <= sz1 + ring;
    }

    /// <summary>Each footing paired with its group's — every building it stands within a passage of, and every
    /// building those stand within a passage of, as one extent. A village is a block of buildings players walk
    /// round rather than a row of corridors between them, so the passage is owed around what they make
    /// together and the ground between them is the claim ring's to keep.</summary>
    public static List<(Footing Own, Footing Group)> Grouped(IReadOnlyList<Footing> footings)
    {
        var owner = Enumerable.Range(0, footings.Count).ToArray();
        var groups = footings.ToList();
        for (var again = true; again;)
        {
            again = false;
            for (var i = 0; i < footings.Count && !again; i++)
            for (var j = 0; j < footings.Count; j++)
            {
                if (owner[i] == owner[j] || !groups[owner[i]].Neighbours(groups[owner[j]])) continue;
                int keep = owner[i], drop = owner[j];
                groups[keep] = groups[keep].Join(groups[drop]);
                for (var k = 0; k < owner.Length; k++) if (owner[k] == drop) owner[k] = keep;
                again = true;
                break;
            }
        }
        return [.. footings.Select((footing, i) => (footing, groups[owner[i]]))];
    }

    /// <summary>The group a candidate joins among buildings already standing — itself where it joins none.
    /// <paramref name="standing"/> is expected already grouped among themselves (<see cref="Grouped"/>), which
    /// is what lets an anchor be asked about without regrouping the board.</summary>
    public static Footing JoinedTo(Footing one, IReadOnlyList<Footing> standing)
    {
        var joined = one;
        var taken = new bool[standing.Count];
        for (var again = true; again;)
        {
            again = false;
            for (var i = 0; i < standing.Count; i++)
            {
                if (taken[i] || !joined.Neighbours(standing[i])) continue;
                joined = joined.Join(standing[i]);
                taken[i] = true;
                again = true;
            }
        }
        return joined;
    }
}
