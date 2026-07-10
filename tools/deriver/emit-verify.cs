#:project ../../src/PgmStudio.Pgm/PgmStudio.Pgm.csproj
#:property JsonSerializerIsReflectionEnabledByDefault=true
// Round-trip verification for the composer's wool-box base-shape emitter (docs/contracts/layout-generation.md §2,
// "base wool-approach shapes"). Emits a matrix of base shapes with WoolBoxEmitter and reads each back with the
// categorizer (WoolLaneShape): requested == derived is the mirror closing. This pass covers the thin-corridor
// families the bend count already names (I/L/Z); the branched/looped/solid families follow once the four-way
// skeleton classifier is promoted into the library. Run: dotnet run tools/deriver/emit-verify.cs
using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Plan;

var families = new[] { ApproachFamily.I, ApproachFamily.L, ApproachFamily.Z };
string Expect(ApproachFamily f) => f.ToString();

var boxes = new (int W, int H)[] { (4, 8), (6, 12), (6, 16), (8, 14), (10, 20) };
var widths = new[] { 2, 3 };
int ok = 0, bad = 0, skip = 0; var fails = new List<string>();
Console.WriteLine($"{"family",-7} {"box",-7} {"cw",3} {"flip",6}  {"derived",-9} status");
Console.WriteLine(new string('-', 47));
foreach (var f in families)
    foreach (var (bw, bh) in boxes)
        foreach (var cw in widths)
            foreach (var flip in new[] { false, true })
            {
                if (cw > bw) { skip++; continue; }
                EmittedApproach a;
                try { a = WoolBoxEmitter.Emit(f, new WoolBox(0, 0, bw, bh), cw, flip); }
                catch (ComposeException) { skip++; continue; }   // box too small for this family/width
                var plan = WoolBoxEmitter.AsPlan(a);
                var (shape, w) = WoolLaneShape.Classify(plan, a.WoolRoom.Id);
                var pass = shape == Expect(f);
                if (pass) ok++; else { bad++; fails.Add($"{f} {bw}x{bh} cw{cw} flip{flip}: got {shape}·w{w}"); }
                Console.WriteLine($"{f,-7} {bw + "x" + bh,-7} {cw,3} {flip,6}  {shape + "·w" + w,-9} {(pass ? "OK" : "MISMATCH")}");
            }
// side-tuck: an I lane whose room turns perpendicular at the end (the catalog's side-tuck). It must still
// read I (the lane is straight — the room is excluded from the bend count) AND the room must be a real tuck
// (wider than the lane column), not an inline continuation.
foreach (var (bw, bh) in boxes)
    foreach (var cw in widths)
        foreach (var flip in new[] { false, true })
        {
            if (cw > bw) { skip++; continue; }
            EmittedApproach a;
            try { a = WoolBoxEmitter.Emit(ApproachFamily.I, new WoolBox(0, 0, bw, bh), cw, flip, RoomPlacement.SideTuck); }
            catch (ComposeException) { skip++; continue; }
            var plan = WoolBoxEmitter.AsPlan(a);
            var (shape, w) = WoolLaneShape.Classify(plan, a.WoolRoom.Id);
            var perp = a.WoolRoom.Rect[2] > a.Terrain[0].Rect[2];   // room wider than the lane column = a real tuck
            var pass = shape == "I" && perp;
            if (pass) ok++; else { bad++; fails.Add($"I-tuck {bw}x{bh} cw{cw} flip{flip}: got {shape}·w{w} perp={perp}"); }
            Console.WriteLine($"{"I-tuck",-7} {bw + "x" + bh,-7} {cw,3} {flip,6}  {shape + "·w" + w,-9} {(pass ? "OK" : "MISMATCH")}");
        }

Console.WriteLine(new string('-', 47));
Console.WriteLine($"{ok} OK / {bad} MISMATCH / {skip} skipped (box too small)");
foreach (var m in fails) Console.WriteLine($"  FAIL {m}");
