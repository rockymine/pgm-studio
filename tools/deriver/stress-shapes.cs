#:project ../../src/PgmStudio.Pgm/PgmStudio.Pgm.csproj
#:property JsonSerializerIsReflectionEnabledByDefault=true
// Stress the wool-approach classifier (WoolApproachShape) with GRAMMAR-VALID but geometrically EXTREME
// shapes: each family's pieces (entry/entry-run/bar/room-run/leg/room) stay flush-aligned per the §2
// template, but individual pieces are pushed to extremes (a box for one H entry + a thin one for the other,
// a box-type bar, a very long room-run, a wide scythe entry, ...). The classifier must be width-independent
// and read the bends / bays / air-remainder — so a wide spot must NOT read as a fork. Any row where
// got != expect is a G53 break. Emits a JSON dump for the debug artifact.
// Run: dotnet run tools/deriver/stress-shapes.cs
using System.Text.Json;
using PgmStudio.Pgm.Plan;

var shapes = new List<Shape>();

// ---- builders (local frame; every piece abuts its neighbour along a full edge) ---------------------

// H = bar · entry · entry · room-run · room. Wool at top (z=0); two legs (entries) dock the hub at the
// bottom; a room-run I lifts the wool off the crossbar. leftW/rightW = the two leg widths; gap = the void
// fork-opening between them; barTh = crossbar thickness; legLen = leg length; runLen = room-run length.
Shape H(string name, string note, int cw, int leftW, int rightW, int gap, int barTh, int legLen, int runW, int runLen, int roomDepth = 2)
{
    int wtot = leftW + gap + rightW;
    int barZ = roomDepth + runLen, legZ = barZ + barTh;
    int runX = (wtot - runW) / 2;
    var ps = new List<P>
    {
        new("room",     runX, 0,         runW, roomDepth),
        new("room-run", runX, roomDepth, runW, runLen),
        new("bar",      0,    barZ,      wtot, barTh),
        new("entry",    0,    legZ,      leftW, legLen),
        new("entry",    wtot - rightW, legZ, rightW, legLen),
    };
    return new Shape(name, "H", note, cw, ps);
}

// Z = entry · bar · room-run · room. Two opposing turns (an S). entry = top-left vertical; bar = horizontal
// band; room-run = bottom-right vertical ending in the room.
Shape Z(string name, string note, int cw, int entryW, int entryLen, int barLen, int barTh, int runW, int runLen, int roomDepth = 2)
{
    var ps = new List<P>
    {
        new("entry",    0, 0, entryW, entryLen),
        new("bar",      0, entryLen, barLen, barTh),
        new("room-run", barLen - runW, entryLen + barTh, runW, runLen),
        new("room",     barLen - runW, entryLen + barTh + runLen, runW, roomDepth),
    };
    return new Shape(name, "Z", note, cw, ps);
}

// Scythe = entry · entry-run · bar · room-run · room. A fold with an open bay. entry = top-left tail (the
// mouth); entry-run = spine down; bar = bottom bar; room-run = return leg up; room caps it top-right. The bay
// is the void between the spine and the return leg. tailH lets the entry widen ALONG the spine (G51 grammar).
Shape Scythe(string name, string note, int cw, int tailW, int tailH, int spineW, int barTh, int bayW, int runW, int runLen, int roomDepth = 2)
{
    int spineLen = runLen + roomDepth;               // spine reaches the bottom bar
    int botZ = spineLen;                             // bar sits below the spine
    int spineX = tailW;                             // spine starts right of the tail
    int returnX = spineX + spineW + bayW;           // return leg one bay over
    int barLen = spineW + bayW + runW;              // spine -> bay -> return leg
    var ps = new List<P>
    {
        new("entry",     0,       0,         tailW,  tailH),          // tail (mouth), may widen down the spine
        new("entry-run", spineX,  0,         spineW, spineLen),       // spine
        new("bar",       spineX,  botZ,      barLen, barTh),          // bottom bar
        new("room-run",  returnX, roomDepth, runW,   runLen),         // return leg (up)
        new("room",      returnX, 0,         runW,   roomDepth),      // wool caps the return leg
    };
    return new Shape(name, "Scythe", note, cw, ps);
}

// Clamp = the wool caught between two parallel bars (it bridges them — remove it and they fall apart). barW =
// bar length; topTh/botTh = the two bar thicknesses (push one to a box, one thin); gap = the room span.
Shape Clamp(string name, string note, int cw, int barW, int topTh, int botTh, int gap, int roomW)
{
    int botZ = topTh + gap;
    var ps = new List<P>
    {
        new("entry", 0, 0,    barW, topTh),                 // top bar
        new("entry", 0, botZ, barW, botTh),                 // bottom bar
        new("room",  barW - roomW, topTh, roomW, gap),      // closing wall between the bars
    };
    return new Shape(name, "Clamp", note, cw, ps);
}

// U = the wool docked FLUSH on the crossbar (no stub) — two legs down + a crossbar the wool sits directly on,
// with bays flanking it. The crossbar reaches past the wool toward the legs, so the wool sits on a bar wider
// than itself (what separates U from H). leftW/rightW = leg widths (push one to a box), gap = space between the
// legs, barTh = crossbar thickness, roomW = wool width, legLen = leg length.
Shape U(string name, string note, int cw, int leftW, int rightW, int gap, int barTh, int roomW, int legLen, int roomDepth = 2)
{
    int wtot = leftW + gap + rightW, barZ = roomDepth, legZ = roomDepth + barTh, wx = (wtot - roomW) / 2;
    var ps = new List<P>
    {
        new("room",  wx, 0,    roomW, roomDepth),           // wool flush on the crossbar
        new("bar",   0,  barZ, wtot, barTh),                // crossbar (overhangs the wool toward the legs)
        new("entry", 0,  legZ, leftW, legLen),              // left leg
        new("entry", wtot - rightW, legZ, rightW, legLen),  // right leg
    };
    return new Shape(name, "U", note, cw, ps);
}

// Donut = entry-bar · leg · leg · entry · room-bar · room. A ring around a hole, docked by an attachment.
// legTh = the ring leg thickness (push to a box); barTh = top/bottom bar thickness; holeW/holeH = the
// enclosed void; awH = attachment interface width (along the ring edge).
Shape Donut(string name, string note, int cw, int legTh, int barTh, int holeW, int holeH, int awH, int roomDepth = 2)
{
    int ax = cw;                                    // ring x-origin; attachment sits in [0, cw)
    int ringW = legTh + holeW + legTh;              // left leg + hole + right leg
    int ringH = barTh + holeH + barTh;
    var ps = new List<P>
    {
        new("entry-bar", ax, 0,               ringW, barTh),                 // top bar
        new("leg",       ax, barTh,           legTh, holeH),                 // left leg (middle)
        new("leg",       ax + legTh + holeW, barTh, legTh, holeH),           // right leg (middle)
        new("entry",     0,  0,               cw,    awH),                   // hub attachment (top-left)
        new("room-bar",  ax, barTh + holeH,   ringW, barTh),                 // bottom bar
        new("room",      ax + ringW, barTh + holeH, roomDepth, barTh),       // wool off the bottom-right
    };
    return new Shape(name, "Donut", note, cw, ps);
}

// ---- the stress set: one control per family, then each piece pushed to an extreme -------------------
int W = 2;   // reference lane width for every shape (so "width-independent" means: hold W, vary the pieces)

// H — the user's headline case
shapes.Add(H("H control",          "normal H",                                   W, 2, 2, 2, 2, 8, 2, 4));
shapes.Add(H("H box+thin legs",    "left entry a 6-wide box, right entry thin",  W, 6, 2, 2, 2, 8, 2, 4));
shapes.Add(H("H box bar",          "crossbar 5 cells thick (a box bar)",         W, 2, 2, 2, 5, 8, 2, 4));
shapes.Add(H("H very long room-run","room-run 18 cells long",                    W, 2, 2, 2, 2, 8, 2, 18));
shapes.Add(H("H long legs",        "legs 20 cells long",                         W, 2, 2, 2, 2, 20, 2, 4));
shapes.Add(H("H ALL extreme",      "box left leg, thin right, box bar, long run",W, 6, 2, 2, 5, 10, 2, 18));
shapes.Add(H("H wide gap",         "fork opening 8 cells wide",                  W, 2, 2, 8, 2, 8, 2, 4));

// Z
shapes.Add(Z("Z control",          "normal Z",                                   W, 2, 6, 10, 2, 2, 6));
shapes.Add(Z("Z box entry",        "entry a 6-wide box",                         W, 6, 6, 12, 2, 2, 6));
shapes.Add(Z("Z box bar",          "bar 5 cells thick",                          W, 2, 6, 10, 5, 2, 6));
shapes.Add(Z("Z very long room-run","room-run 20 cells long",                    W, 2, 6, 10, 2, 2, 20));
shapes.Add(Z("Z wide bar",         "bar 24 long, wide entry+run",                W, 4, 6, 24, 2, 4, 6));

// Scythe — the user's headline case
shapes.Add(Scythe("Scythe control",   "normal scythe",                           W, 2, 2, 2, 2, 2, 2, 6));
shapes.Add(Scythe("Scythe wide entry","tail widened 8 down the spine",           W, 2, 8, 2, 2, 2, 2, 6));
shapes.Add(Scythe("Scythe box spine", "spine (entry-run) 5 wide",                W, 2, 2, 5, 2, 2, 2, 6));
shapes.Add(Scythe("Scythe box bar",   "bottom bar 5 thick",                      W, 2, 2, 2, 5, 2, 2, 6));
shapes.Add(Scythe("Scythe long run",  "return leg 20 long",                      W, 2, 2, 2, 2, 2, 2, 20));
shapes.Add(Scythe("Scythe wide bay",  "bay 6 wide",                              W, 2, 2, 2, 2, 6, 2, 6));

// Clamp (the wool caught between two parallel bars — bridges them)
shapes.Add(Clamp("Clamp control",     "normal clamp (flanked)",                  W, 4, 2, 2, 4, 2));
shapes.Add(Clamp("Clamp box+thin bar","top bar a 5-thick box, bottom thin",      W, 4, 5, 2, 4, 2));
shapes.Add(Clamp("Clamp long span",   "room span 20 between the bars",           W, 4, 2, 2, 20, 2));

// U (wool flush on a crossbar wider than itself, legs down, bays flanking)
shapes.Add(U("U control",       "normal U (flush on the bar)",                   W, 2, 2, 2, 2, 2, 6));
shapes.Add(U("U box+thin legs", "left leg a 6-wide box, right leg thin",         W, 6, 2, 2, 2, 2, 6));
shapes.Add(U("U box bar",       "crossbar 5 cells thick",                        W, 2, 2, 2, 5, 2, 6));
shapes.Add(U("U wide gap",      "legs 8 cells apart",                            W, 2, 2, 8, 2, 2, 6));
shapes.Add(U("U long legs",     "legs 20 cells long",                            W, 2, 2, 2, 2, 2, 20));

// Donut
shapes.Add(Donut("Donut control",  "normal ring",                               W, 2, 2, 2, 2, 2));
shapes.Add(Donut("Donut box legs", "ring legs 5 thick",                          W, 5, 2, 2, 2, 2));
shapes.Add(Donut("Donut box bars", "top+bottom bars 5 thick",                    W, 2, 5, 4, 4, 2));
shapes.Add(Donut("Donut big hole", "enclosed hole 10x10",                        W, 2, 2, 10, 10, 2));
shapes.Add(Donut("Donut wide attach","attachment 6 along the ring edge",         W, 2, 2, 2, 2, 6));

// ---- classify + report ------------------------------------------------------------------------------
(ApproachShape shape, int width, int overlaps, bool connected) Eval(Shape s)
{
    var filled = new HashSet<(int, int)>();
    var room = new HashSet<(int, int)>();
    int ov = 0;
    foreach (var p in s.Pieces)
        for (var x = p.X; x < p.X + p.W; x++)
            for (var z = p.Z; z < p.Z + p.H; z++)
            {
                if (!filled.Add((x, z))) ov++;
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
    var (shp, wd) = WoolApproachShape.Classify(filled, room, s.Cw);
    return (shp, wd, ov, seen.Count == filled.Count);
}

Console.WriteLine($"{"shape",-24}{"expect",-9}{"got",-12}{"malformed",-11}status");
Console.WriteLine(new string('-', 70));
int breaks = 0, malformed = 0;
var outRows = new List<object>();
foreach (var s in shapes)
{
    var (shp, wd, ov, conn) = Eval(s);
    bool bad = shp.ToString() != s.Family;
    bool mal = ov > 0 || !conn;
    string flag = mal ? $"OVERLAP {ov}/{(conn ? "conn" : "DISCONN")}" : "";
    string status = mal ? "MALFORMED" : bad ? "*** BREAK ***" : "ok";
    if (mal) malformed++; else if (bad) breaks++;
    Console.WriteLine($"{s.Name,-24}{s.Family,-9}{shp + "·w" + wd,-12}{flag,-11}{status}");
    outRows.Add(new { name = s.Name, family = s.Family, note = s.Note, cw = s.Cw, derived = shp.ToString(), width = wd, malformed = mal, brk = bad && !mal, pieces = s.Pieces });
}
Console.WriteLine(new string('-', 70));
Console.WriteLine($"{shapes.Count} shapes: {breaks} classifier BREAKS, {malformed} malformed (fix the builder, not a break)");

// optional JSON dump (a render feed for the debug artifact) — pass an output path as the first arg.
if (args.Length > 0)
{
    var json = JsonSerializer.Serialize(new { rows = outRows }, new JsonSerializerOptions { WriteIndented = false });
    File.WriteAllText(args[0], json);
    Console.WriteLine($"-> {args[0]}");
}

// role-tagged rectangle: [x, z, w, h] in cells; z grows downward, mouth/hub is the deep end
record P(string Role, int X, int Z, int W, int H);
record Shape(string Name, string Family, string Note, int Cw, List<P> Pieces);
