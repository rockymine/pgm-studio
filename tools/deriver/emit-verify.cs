#:project ../../src/PgmStudio.Pgm/PgmStudio.Pgm.csproj
#:property JsonSerializerIsReflectionEnabledByDefault=true
// Round-trip verification for the composer's wool-box base-shape emitter (docs/contracts/map-generation.md §5).
// Emits every base family (and its variants) with WoolBoxEmitter and reads each back with the canonical
// classifier (WoolApproachShape, laneWidth = cw): requested == derived is the mirror closing. Also asserts the
// emitted pieces never overlap. Run: dotnet run tools/deriver/emit-verify.cs
using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Plan;

int ok = 0, bad = 0, skip = 0; var fails = new List<string>();

// pieces must tile without overlap (a cell covered by >1 piece is a bug)
static int Overlaps(EmittedApproach a)
{
    var seen = new HashSet<(int, int)>(); int n = 0;
    foreach (var p in a.Terrain.Append(a.WoolRoom))
        for (var x = p.Rect[0]; x < p.Rect[0] + p.Rect[2]; x++)
            for (var z = p.Rect[1]; z < p.Rect[1] + p.Rect[3]; z++) if (!seen.Add((x, z))) n++;
    return n;
}
void Check(string label, ApproachFamily fam, Func<EmittedApproach> emit, int cw, string expect = null)
{
    EmittedApproach a;
    try { a = emit(); } catch (ComposeException) { skip++; return; }
    var (s, wd) = WoolApproachShape.Classify(WoolBoxEmitter.AsPlan(a), a.WoolRoom.Id, cw);
    int ov = Overlaps(a);
    var want = expect ?? fam.ToString();
    var pass = s.ToString() == want && ov == 0;
    if (pass) ok++; else { bad++; fails.Add($"{label}: got {s}·w{wd} overlaps={ov} (want {want}, no overlap)"); }
    Console.WriteLine($"{label,-26} {s + "·w" + wd,-11} ov={ov}  {(pass ? "OK" : "MISMATCH")}");
}

Console.WriteLine("=== families × sizes × widths × flip ===");
var families = Enum.GetValues<ApproachFamily>();
var cws = new[] { 2, 3 };
var boxes = new (int W, int H)[] { (12, 16), (16, 22) };
foreach (var f in families)
    foreach (var cw in cws)
        foreach (var (W, H) in boxes)
            foreach (var flip in new[] { false, true })
                Check($"{f} {W}x{H} cw{cw}{(flip ? " flip" : "")}", f, () => WoolBoxEmitter.Emit(f, new WoolBox(0, 0, W, H), cw, flip), cw);

// every emitted piece carries its template slot role (piece vocabulary §2); the emitted slot sequence must
// equal ApproachSlots.Template for each family — the stable named sequence the shift/width/docking rules target.
Console.WriteLine("=== slot templates (piece vocabulary §2) ===");
foreach (var f in families)
{
    EmittedApproach a;
    try { a = WoolBoxEmitter.Emit(f, new WoolBox(0, 0, 20, 24), 2); } catch (ComposeException) { skip++; continue; }
    var got = string.Join(" · ", a.Terrain.Select(p => p.Slot).Append(a.WoolRoom.Slot));
    var want = string.Join(" · ", ApproachSlots.Template(f));
    var pass = got == want;
    if (pass) ok++; else { bad++; fails.Add($"{f} slots: got [{got}] want [{want}]"); }
    Console.WriteLine($"{f,-8} {got,-46} {(pass ? "OK" : "MISMATCH")}");
}

Console.WriteLine("=== variants ===");
foreach (var cw in cws)
{
    var db = new WoolBox(0, 0, 6 * cw + 4, 5 * cw);
    Check($"donut single cw{cw}", ApproachFamily.Donut, () => WoolBoxEmitter.Emit(ApproachFamily.Donut, db, cw, attachments: 1), cw);
    Check($"donut double cw{cw}", ApproachFamily.Donut, () => WoolBoxEmitter.Emit(ApproachFamily.Donut, db, cw, attachments: 2), cw);
    Check($"donut short-I cw{cw}", ApproachFamily.Donut, () => WoolBoxEmitter.Emit(ApproachFamily.Donut, db, cw, woolExtend: true), cw);
    Check($"donut corner cw{cw}", ApproachFamily.Donut, () => WoolBoxEmitter.Emit(ApproachFamily.Donut, db, cw, woolAtEnd: true), cw);
    var dbw = new WoolBox(0, 0, 6 * cw + 4, 7 * cw);   // taller box for wide attachments
    Check($"donut attach-w4 cw{cw}", ApproachFamily.Donut, () => WoolBoxEmitter.Emit(ApproachFamily.Donut, dbw, cw, attachmentWidth: 2 * cw), cw);
    Check($"donut attach-w6 cw{cw}", ApproachFamily.Donut, () => WoolBoxEmitter.Emit(ApproachFamily.Donut, dbw, cw, attachments: 2, attachmentWidth: 3 * cw), cw);
    // regression: a SINGLE wide attachment only needs its own minimal ring (aw + cw tall) — it must NOT be
    // forced to the two-stub height (2·aw + 1). This box is exactly aw + cw for a w4 attachment; if the height
    // guard over-constrains again it will ComposeException here and register as a skip, not an OK.
    var dmin = new WoolBox(0, 0, 6 * cw + 4, 3 * cw);   // 3·cw = aw(2·cw) + cw
    Check($"donut attach-w4 min-box cw{cw}", ApproachFamily.Donut, () => WoolBoxEmitter.Emit(ApproachFamily.Donut, dmin, cw, attachmentWidth: 2 * cw), cw);
    // U (wool flush on the crossbar) and H (wool on a room-run stub) — middle and end (woolAtEnd) wool.
    var ub = new WoolBox(0, 0, 4 * cw, 5 * cw);
    Check($"U middle cw{cw}", ApproachFamily.U, () => WoolBoxEmitter.Emit(ApproachFamily.U, ub, cw), cw);
    Check($"U edge cw{cw}", ApproachFamily.U, () => WoolBoxEmitter.Emit(ApproachFamily.U, ub, cw, woolAtEnd: true), cw);
    var hb = new WoolBox(0, 0, 4 * cw, 6 * cw);
    Check($"H middle cw{cw}", ApproachFamily.H, () => WoolBoxEmitter.Emit(ApproachFamily.H, hb, cw), cw);
    Check($"H edge cw{cw}", ApproachFamily.H, () => WoolBoxEmitter.Emit(ApproachFamily.H, hb, cw, woolAtEnd: true), cw);
}

// a uniformly-widened approach reads the same family as the base lane — the classifier counts turns on the
// terrain outline, so width is invariant. Emit each thin family, then a ×2 and ×3 scaled twin, at the matching
// reference width, and assert requested == derived at every scale.
Console.WriteLine("=== width-invariance (uniform scaling) ===");
foreach (var f in new[] { ApproachFamily.I, ApproachFamily.L, ApproachFamily.Z, ApproachFamily.Scythe, ApproachFamily.Clamp, ApproachFamily.U, ApproachFamily.H })
    foreach (var k in new[] { 1, 2, 3 })
        Check($"{f} x{k}", f, () => WoolBoxEmitter.Emit(f, new WoolBox(0, 0, 6 * (2 * k), 10 * (2 * k)), 2 * k), 2 * k);

Console.WriteLine("=== side-tuck (I, room off the side) ===");
foreach (var cw in cws)
    foreach (var (W, H) in boxes)
        foreach (var flip in new[] { false, true })
        {
            EmittedApproach a;
            try { a = WoolBoxEmitter.Emit(ApproachFamily.I, new WoolBox(0, 0, W, H), cw, flip, RoomPlacement.SideTuck); }
            catch (ComposeException) { skip++; continue; }
            var (s, wd) = WoolApproachShape.Classify(WoolBoxEmitter.AsPlan(a), a.WoolRoom.Id, cw);
            int[] lane = a.Terrain[0].Rect, rm = a.WoolRoom.Rect;
            var side = rm[0] == lane[0] + lane[2] || rm[0] + rm[2] == lane[0];
            var pass = s == ApproachShape.I && side && Overlaps(a) == 0;
            if (pass) ok++; else { bad++; fails.Add($"I-tuck {W}x{H} cw{cw} flip{flip}: got {s} side={side} ov={Overlaps(a)}"); }
            Console.WriteLine($"{"I-tuck " + W + "x" + H + " cw" + cw + (flip ? " flip" : ""),-26} {s + "·w" + wd,-11} ov={Overlaps(a)}  {(pass ? "OK" : "MISMATCH")}");
        }

Console.WriteLine(new string('-', 50));
Console.WriteLine($"{ok} OK / {bad} MISMATCH / {skip} skipped (box too small)");
foreach (var m in fails) Console.WriteLine($"  FAIL {m}");
