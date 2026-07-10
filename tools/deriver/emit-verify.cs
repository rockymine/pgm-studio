#:project ../../src/PgmStudio.Pgm/PgmStudio.Pgm.csproj
#:property JsonSerializerIsReflectionEnabledByDefault=true
// Round-trip verification for the composer's wool-box base-shape emitter (docs/contracts/layout-generation.md §2).
// Emits every base family with WoolBoxEmitter and reads each back with the canonical classifier
// (WoolApproachShape, laneWidth = cw): requested == derived is the mirror closing — what the composer meant to
// make is what the categorizer sees. Run: dotnet run tools/deriver/emit-verify.cs
using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Plan;

var families = Enum.GetValues<ApproachFamily>();
var cws = new[] { 2, 3 };
var boxes = new (int W, int H)[] { (12, 16), (16, 22) };   // large enough for every family's turns
int ok = 0, bad = 0, skip = 0; var fails = new List<string>();
Console.WriteLine($"{"family",-8} {"box",-7} {"cw",3} {"flip",6}  {"derived",-11} status");
Console.WriteLine(new string('-', 48));
foreach (var f in families)
    foreach (var cw in cws)
        foreach (var (W, H) in boxes)
            foreach (var flip in new[] { false, true })
            {
                EmittedApproach a;
                try { a = WoolBoxEmitter.Emit(f, new WoolBox(0, 0, W, H), cw, flip); }
                catch (ComposeException) { skip++; continue; }
                var plan = WoolBoxEmitter.AsPlan(a);
                var (s, wd) = WoolApproachShape.Classify(plan, a.WoolRoom.Id, cw);
                var pass = s.ToString() == f.ToString();
                if (pass) ok++; else { bad++; fails.Add($"{f} {W}x{H} cw{cw} flip{flip}: got {s}"); }
                Console.WriteLine($"{f,-8} {W + "x" + H,-7} {cw,3} {flip,6}  {s + "·w" + wd,-11} {(pass ? "OK" : "MISMATCH")}");
            }

// side-tuck: an I lane whose room turns perpendicular at the terminal — must read I and be side-attached
// (the room shares a vertical edge with the lane), never a cap extending the lane's end.
foreach (var cw in cws)
    foreach (var (W, H) in boxes)
        foreach (var flip in new[] { false, true })
        {
            EmittedApproach a;
            try { a = WoolBoxEmitter.Emit(ApproachFamily.I, new WoolBox(0, 0, W, H), cw, flip, RoomPlacement.SideTuck); }
            catch (ComposeException) { skip++; continue; }
            var plan = WoolBoxEmitter.AsPlan(a);
            var (s, wd) = WoolApproachShape.Classify(plan, a.WoolRoom.Id, cw);
            int[] lane = a.Terrain[0].Rect, rm = a.WoolRoom.Rect;
            var side = rm[0] == lane[0] + lane[2] || rm[0] + rm[2] == lane[0];
            var pass = s == ApproachShape.I && side;
            if (pass) ok++; else { bad++; fails.Add($"I-tuck {W}x{H} cw{cw} flip{flip}: got {s} side={side}"); }
            Console.WriteLine($"{"I-tuck",-8} {W + "x" + H,-7} {cw,3} {flip,6}  {s + "·w" + wd,-11} {(pass ? "OK" : "MISMATCH")}");
        }

Console.WriteLine(new string('-', 48));
Console.WriteLine($"{ok} OK / {bad} MISMATCH / {skip} skipped (box too small)");
foreach (var m in fails) Console.WriteLine($"  FAIL {m}");
