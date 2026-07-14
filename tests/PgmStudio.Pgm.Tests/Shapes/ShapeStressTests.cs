using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Tests.Shapes;

/// <summary>
/// Stresses <see cref="ShapeClassifier"/> with GRAMMAR-VALID but geometrically EXTREME shapes: each family's
/// pieces stay flush-aligned per the §5.3 template, but individual pieces are pushed to extremes (a box for one
/// leg + a thin one for the other, a box-type bar, a very long room-run, a wide scythe entry, …). The classifier
/// is width-independent — it reads bends / bays / air-remainder — so a wide spot must NOT read as a fork. Each
/// shape must be well-formed (no overlaps, terrain connected to the room) and classify to its family.
/// </summary>
public sealed class ShapeStressTests
{
    private const int W = 2;   // reference lane width for every shape (hold W, vary the pieces)

    public static IEnumerable<Func<StressShape>> Cases() =>
    [
        // H — the two-leg branch with a room-run stub
        () => H("H control",           ShapeFamily.H, W, 2, 2, 2, 2, 8, 2, 4),
        () => H("H box+thin legs",     ShapeFamily.H, W, 6, 2, 2, 2, 8, 2, 4),
        () => H("H box bar",           ShapeFamily.H, W, 2, 2, 2, 5, 8, 2, 4),
        () => H("H very long room-run",ShapeFamily.H, W, 2, 2, 2, 2, 8, 2, 18),
        () => H("H long legs",         ShapeFamily.H, W, 2, 2, 2, 2, 20, 2, 4),
        () => H("H ALL extreme",       ShapeFamily.H, W, 6, 2, 2, 5, 10, 2, 18),
        () => H("H wide gap",          ShapeFamily.H, W, 2, 2, 8, 2, 8, 2, 4),
        // Z — two opposing turns
        () => Z("Z control",           ShapeFamily.Z, W, 2, 6, 10, 2, 2, 6),
        () => Z("Z box entry",         ShapeFamily.Z, W, 6, 6, 12, 2, 2, 6),
        () => Z("Z box bar",           ShapeFamily.Z, W, 2, 6, 10, 5, 2, 6),
        () => Z("Z very long room-run",ShapeFamily.Z, W, 2, 6, 10, 2, 2, 20),
        () => Z("Z wide bar",          ShapeFamily.Z, W, 4, 6, 24, 2, 4, 6),
        // Scythe — a fold with an open bay
        () => Scythe("Scythe control",    ShapeFamily.Scythe, W, 2, 2, 2, 2, 2, 2, 6),
        () => Scythe("Scythe wide entry", ShapeFamily.Scythe, W, 2, 8, 2, 2, 2, 2, 6),
        () => Scythe("Scythe box spine",  ShapeFamily.Scythe, W, 2, 2, 5, 2, 2, 2, 6),
        () => Scythe("Scythe box bar",    ShapeFamily.Scythe, W, 2, 2, 2, 5, 2, 2, 6),
        () => Scythe("Scythe long run",   ShapeFamily.Scythe, W, 2, 2, 2, 2, 2, 2, 20),
        () => Scythe("Scythe wide bay",   ShapeFamily.Scythe, W, 2, 2, 2, 2, 6, 2, 6),
        // Clamp — the wool caught between two parallel bars (it bridges them)
        () => Clamp("Clamp control",      ShapeFamily.Clamp, W, 4, 2, 2, 4, 2),
        () => Clamp("Clamp box+thin bar", ShapeFamily.Clamp, W, 4, 5, 2, 4, 2),
        () => Clamp("Clamp long span",    ShapeFamily.Clamp, W, 4, 2, 2, 20, 2),
        // U — the wool flush on a crossbar wider than itself
        () => U("U control",       ShapeFamily.U, W, 2, 2, 2, 2, 2, 6),
        () => U("U box+thin legs", ShapeFamily.U, W, 6, 2, 2, 2, 2, 6),
        () => U("U box bar",       ShapeFamily.U, W, 2, 2, 2, 5, 2, 6),
        () => U("U wide gap",      ShapeFamily.U, W, 2, 2, 8, 2, 2, 6),
        () => U("U long legs",     ShapeFamily.U, W, 2, 2, 2, 2, 2, 20),
        // Donut — a ring around a hole, docked by an attachment
        () => Donut("Donut control",   ShapeFamily.Donut, W, 2, 2, 2, 2, 2),
        () => Donut("Donut box legs",  ShapeFamily.Donut, W, 5, 2, 2, 2, 2),
        () => Donut("Donut box bars",  ShapeFamily.Donut, W, 2, 5, 4, 4, 2),
        () => Donut("Donut big hole",  ShapeFamily.Donut, W, 2, 2, 10, 10, 2),
        () => Donut("Donut wide attach",ShapeFamily.Donut, W, 2, 2, 2, 2, 6),
    ];

    [Test]
    [MethodDataSource(nameof(Cases))]
    public async Task Extreme_shape_is_wellformed_and_classifies_to_its_family(StressShape s)
    {
        var filled = new HashSet<(int, int)>();
        var room = new HashSet<(int, int)>();
        int overlaps = 0;
        foreach (var p in s.Pieces)
            for (var x = p.X; x < p.X + p.W; x++)
                for (var z = p.Z; z < p.Z + p.H; z++)
                {
                    if (!filled.Add((x, z))) overlaps++;
                    if (p.Role == "room") room.Add((x, z));
                }
        // connectivity: every filled cell reachable from the room (else the shape is malformed, not a break)
        var seen = new HashSet<(int, int)>(); var q = new Queue<(int, int)>();
        foreach (var r in room) if (seen.Add(r)) q.Enqueue(r);
        while (q.Count > 0)
        {
            var (cx, cz) = q.Dequeue();
            foreach (var n in new[] { (cx + 1, cz), (cx - 1, cz), (cx, cz + 1), (cx, cz - 1) })
                if (filled.Contains(n) && seen.Add(n)) q.Enqueue(n);
        }
        await Assert.That(overlaps).IsEqualTo(0);                 // well-formed builder: no overlaps
        await Assert.That(seen.Count).IsEqualTo(filled.Count);   // well-formed builder: terrain connected to the room
        var (derived, _) = ShapeClassifier.Classify(filled, room);
        await Assert.That(derived).IsEqualTo(s.Family);
    }

    // ---- builders (local frame; every piece abuts its neighbour along a full edge) ---------------------

    private static StressShape H(string name, ShapeFamily fam, int cw, int leftW, int rightW, int gap, int barTh, int legLen, int runW, int runLen, int roomDepth = 2)
    {
        int wtot = leftW + gap + rightW;
        int barZ = roomDepth + runLen, legZ = barZ + barTh;
        int runX = (wtot - runW) / 2;
        return new StressShape(name, fam,
        [
            new("room",     runX, 0,         runW, roomDepth),
            new("room-run", runX, roomDepth, runW, runLen),
            new("bar",      0,    barZ,      wtot, barTh),
            new("entry",    0,    legZ,      leftW, legLen),
            new("entry",    wtot - rightW, legZ, rightW, legLen),
        ]);
    }

    private static StressShape Z(string name, ShapeFamily fam, int cw, int entryW, int entryLen, int barLen, int barTh, int runW, int runLen, int roomDepth = 2) =>
        new(name, fam,
        [
            new("entry",    0, 0, entryW, entryLen),
            new("bar",      0, entryLen, barLen, barTh),
            new("room-run", barLen - runW, entryLen + barTh, runW, runLen),
            new("room",     barLen - runW, entryLen + barTh + runLen, runW, roomDepth),
        ]);

    private static StressShape Scythe(string name, ShapeFamily fam, int cw, int tailW, int tailH, int spineW, int barTh, int bayW, int runW, int runLen, int roomDepth = 2)
    {
        int spineLen = runLen + roomDepth;
        int botZ = spineLen;
        int spineX = tailW;
        int returnX = spineX + spineW + bayW;
        int barLen = spineW + bayW + runW;
        return new StressShape(name, fam,
        [
            new("entry",     0,       0,         tailW,  tailH),
            new("entry-run", spineX,  0,         spineW, spineLen),
            new("bar",       spineX,  botZ,      barLen, barTh),
            new("room-run",  returnX, roomDepth, runW,   runLen),
            new("room",      returnX, 0,         runW,   roomDepth),
        ]);
    }

    private static StressShape Clamp(string name, ShapeFamily fam, int cw, int barW, int topTh, int botTh, int gap, int roomW)
    {
        int botZ = topTh + gap;
        return new StressShape(name, fam,
        [
            new("entry", 0, 0,    barW, topTh),
            new("entry", 0, botZ, barW, botTh),
            new("room",  barW - roomW, topTh, roomW, gap),
        ]);
    }

    private static StressShape U(string name, ShapeFamily fam, int cw, int leftW, int rightW, int gap, int barTh, int roomW, int legLen, int roomDepth = 2)
    {
        int wtot = leftW + gap + rightW, barZ = roomDepth, legZ = roomDepth + barTh, wx = (wtot - roomW) / 2;
        return new StressShape(name, fam,
        [
            new("room",  wx, 0,    roomW, roomDepth),
            new("bar",   0,  barZ, wtot, barTh),
            new("entry", 0,  legZ, leftW, legLen),
            new("entry", wtot - rightW, legZ, rightW, legLen),
        ]);
    }

    private static StressShape Donut(string name, ShapeFamily fam, int cw, int legTh, int barTh, int holeW, int holeH, int awH, int roomDepth = 2)
    {
        int ax = cw;
        int ringW = legTh + holeW + legTh;
        return new StressShape(name, fam,
        [
            new("entry-bar", ax, 0,               ringW, barTh),
            new("leg",       ax, barTh,           legTh, holeH),
            new("leg",       ax + legTh + holeW, barTh, legTh, holeH),
            new("entry",     0,  0,               cw,    awH),
            new("room-bar",  ax, barTh + holeH,   ringW, barTh),
            new("room",      ax + ringW, barTh + holeH, roomDepth, barTh),
        ]);
    }

    // role-tagged rectangle: [x, z, w, h] in cells; z grows downward, mouth/hub is the deep end
    public sealed record P(string Role, int X, int Z, int W, int H);
    public sealed record StressShape(string Name, ShapeFamily Family, List<P> Pieces);
}
