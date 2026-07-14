#:project ../../src/PgmStudio.Pgm/PgmStudio.Pgm.csproj
#:property JsonSerializerIsReflectionEnabledByDefault=true
// Reconciliation gallery: renders what the DERIVER reads on the 6 teaching seeds + acapulco, so the author can
// see where the deriver's picture diverges from theirs. Terrain is coloured by TEAM (orbit image) to expose the
// offset of the two masses; each neutral stepping stone is labelled with its void-exposure (void-border / total
// non-land border) — the number that came back opposite to the author's visual. Writes a self-contained HTML
// body (for publishing as an Artifact) to the path given as arg[0].
using System.Globalization;
using System.Text;
using PgmStudio.Geom;
using PgmStudio.Pgm.Derive;
using PgmStudio.Pgm.Plan;

string outPath = args.Length > 0 ? args[0] : "reconcile-gallery.html";

// palette (matches the v1 deriver gallery)
const string Canvas = "#080f1a";
const string Team0 = "#3b82f6", Team1 = "#ef4444";   // orbit image 0 / 1 — the two team masses
const string StoneN = "#facc15", StoneT = "#d946ef";  // neutral / team stepping stone (highlighted)
const string BuildC = "#64748b";                       // mid build region (slate) — shows the mid's shape/offset
const string Front = "#f59e0b";                        // frontline edge
const string HoleMid = "#ef4444", HoleFront = "#fbbf24", HoleGap = "#f472b6", HoleEnc = "#818cf8";

var maps = new (string Group, string Label, string Dir, string Name, string Note)[] {
    ("bad", "crammed · single band", "teaching", "crammed-frontline-single-band", "one band, 6 stones — the starting point"),
    ("bad", "crammed · double band", "teaching", "crammed-frontline-double-band", "double band — the author says this is worse"),
    ("bad", "over-stretched middle void", "teaching", "overstretched-middle-void", "double frontline, long dead void, no rotation"),
    ("good", "fix · bridge zone", "teaching", "double-frontline-pocket-mid-internal-crossing", "a neutral-neutral crossing supplies rotation"),
    ("good", "fix · rotation stone", "teaching", "double-frontline-pocket-mid-rotation-stone", "a stone + two bands supply rotation"),
    ("good", "fix · move closer", "teaching", "double-band-middle-void-no-steps", "teams closer — the void becomes crossable"),
    ("ref", "acapulco (real map)", "traced", "acapulco", "offset team masses; three submerged mid stones"),
};

static string N(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

string Card((string Group, string Label, string Dir, string Name, string Note) m)
{
    var plan = PlanModel.Parse(File.ReadAllText(Path.Combine("tools", "seeds", m.Dir, $"{m.Name}.plan.json")))!;
    var d = BoardDeriver.Derive(plan);
    int cell = d.Cell;

    var all = d.Filled.Keys.Concat(d.Build).Concat(d.Voids.SelectMany(v => v.Cells)).ToList();
    int minX = all.Min(c => c.Item1), maxX = all.Max(c => c.Item1), minZ = all.Min(c => c.Item2), maxZ = all.Max(c => c.Item2);
    double s = 300.0 / (maxX - minX + 1), vbw = 300, vbh = (maxZ - minZ + 1) * s;
    double PX(double c) => (c - minX) * s;
    double PY(double c) => (c - minZ) * s;

    var svg = new StringBuilder();
    svg.Append($"<svg viewBox=\"0 0 {N(vbw)} {N(vbh)}\" xmlns=\"http://www.w3.org/2000/svg\" class=\"map\" role=\"img\">");
    svg.Append($"<rect width=\"{N(vbw)}\" height=\"{N(vbh)}\" fill=\"{Canvas}\"/>");
    void Rect(int cx, int cz, string fill, double fo, string stroke, double sw) =>
        svg.Append($"<rect x=\"{N(PX(cx))}\" y=\"{N(PY(cz))}\" width=\"{N(s)}\" height=\"{N(s)}\" fill=\"{fill}\" fill-opacity=\"{N(fo)}\" stroke=\"{stroke}\" stroke-width=\"{N(sw)}\"/>");

    // mid build region (under terrain) — one slate tint so its SHAPE and offset read at a glance
    foreach (var c in d.Build) Rect(c.Item1, c.Item2, BuildC, 0.22, BuildC, 0.4);
    // terrain coloured by TEAM (orbit image); a stepping stone overrides with its highlight colour
    foreach (var c in d.Filled.Keys)
    {
        var kind = d.SteppingKind[d.IslandOf[c]];
        string fill = kind == "neutral" ? StoneN : kind == "team" ? StoneT : (d.Filled[c].K == 0 ? Team0 : Team1);
        double fo = kind is "neutral" or "team" ? 0.85 : 0.5;
        Rect(c.Item1, c.Item2, fill, fo, Canvas, 0.4);
    }
    // enclosed voids — by class, crossRoutes labelled
    foreach (var (vc, _, cls, cross) in d.Voids)
    {
        string hc = cls == "encased" ? HoleEnc : cls == "gap" ? HoleGap : cls == "frontline" ? HoleFront : HoleMid;
        foreach (var c in vc) Rect(c.Item1, c.Item2, hc, 0.28, hc, 1.0);
        double vx = vc.Average(c => (double)c.Item1) + 0.5, vz = vc.Average(c => (double)c.Item2) + 0.5;
        string tag = cls == "middle" && cross == 0 ? "cross0!" : $"cross{cross}";
        svg.Append($"<text x=\"{N(PX(vx))}\" y=\"{N(PY(vz))}\" fill=\"#fff\" font-size=\"9\" font-weight=\"700\" text-anchor=\"middle\" paint-order=\"stroke\" stroke=\"{Canvas}\" stroke-width=\"2.4\">{tag}</text>");
    }
    // frontline edges (amber)
    foreach (var (x1, z1, x2, z2) in d.FrontEdges)
        svg.Append($"<line x1=\"{N(PX(x1))}\" y1=\"{N(PY(z1))}\" x2=\"{N(PX(x2))}\" y2=\"{N(PY(z2))}\" stroke=\"{Front}\" stroke-width=\"2\" stroke-linecap=\"round\"/>");

    // per neutral stone: void-exposure = void-border / (void-border + build-border), labelled on the stone
    var exps = new List<double>();
    for (var i = 0; i < d.Islands.Count; i++)
    {
        if (d.SteppingKind[i] != "neutral") continue;
        int vb = 0, bb = 0;
        foreach (var c in d.Islands[i])
            foreach (var nb in Cells.N4(c))
            {
                if (d.Filled.ContainsKey(nb)) continue;
                if (d.Build.Contains(nb)) bb++; else vb++;
            }
        double e = vb + bb == 0 ? 0 : (double)vb / (vb + bb);
        exps.Add(e);
        double cx = d.Islands[i].Average(c => (double)c.Item1) + 0.5, cz = d.Islands[i].Average(c => (double)c.Item2) + 0.5;
        svg.Append($"<text x=\"{N(PX(cx))}\" y=\"{N(PY(cz) + 3)}\" fill=\"#0f172a\" font-size=\"9\" font-weight=\"800\" text-anchor=\"middle\">{N(e * 100)}</text>");
    }
    svg.Append("</svg>");

    int nStn = d.SteppingKind.Count(k => k == "neutral"), tStn = d.SteppingKind.Count(k => k == "team");
    int uncrossed = d.Voids.Count(v => v.Class == "middle" && v.CrossRoutes == 0);
    int band = d.Zones.Count(z => z.Kind == "front-front"), nn = d.Zones.Count(z => z.Kind == "neutral-neutral");
    string expStr = exps.Count == 0 ? "—" : $"{N(exps.Min() * 100)}–{N(exps.Max() * 100)}%";
    string verdict = uncrossed > 0 ? "<b class=\"v-bad\">CT9 fires — uncrossed middle void</b>"
        : "<b class=\"v-ok\">CT9 clean</b>";

    string chip = m.Group switch { "bad" => "<span class=\"chip chip-bad\">author: bad</span>",
        "good" => "<span class=\"chip chip-good\">author: good</span>", _ => "<span class=\"chip chip-ref\">reference</span>" };

    var rows = new (string, string)[] {
        ("stones", $"{nStn} neutral{(tStn > 0 ? $" · {tStn} team" : "")}"),
        ("stone void-exposure", expStr),
        ("mid form", $"{d.MidForm} · {band} band · {nn} nn"),
        ("rotation", verdict),
    };
    var stats = string.Concat(rows.Select(r => $"<div class=\"srow\"><span class=\"sk\">{r.Item1}</span><span class=\"sv\">{r.Item2}</span></div>"));

    return $"<article class=\"card\"><div class=\"chead\"><span class=\"clabel\">{m.Label}</span>{chip}</div>" +
           $"<div class=\"svg-wrap\">{svg}</div><p class=\"cnote\">{m.Note}</p><div class=\"stats\">{stats}</div></article>";
}

string Grid(string group) => string.Concat(maps.Where(m => m.Group == group).Select(Card));

var css = """
<style>
:root{--bg:#0f172a;--panel:#1e293b;--border:#334155;--ink:#cbd5e1;--ink2:#94a3b8;--muted:#64748b;--bright:#e2e8f0;--accent:#60a5fa;
  --mono:ui-monospace,SFMono-Regular,Menlo,monospace;--sans:-apple-system,BlinkMacSystemFont,"Segoe UI",Roboto,Helvetica,Arial,sans-serif;}
@media (prefers-color-scheme:light){:root{--bg:#eef2f7;--panel:#fff;--border:#cbd5e1;--ink:#1e293b;--ink2:#475569;--muted:#64748b;--bright:#0f172a;--accent:#2563eb;}}
:root[data-theme="dark"]{--bg:#0f172a;--panel:#1e293b;--border:#334155;--ink:#cbd5e1;--ink2:#94a3b8;--bright:#e2e8f0;--accent:#60a5fa;}
:root[data-theme="light"]{--bg:#eef2f7;--panel:#fff;--border:#cbd5e1;--ink:#1e293b;--ink2:#475569;--bright:#0f172a;--accent:#2563eb;}
*{box-sizing:border-box;}
body{margin:0;background:var(--bg);color:var(--ink);font-family:var(--sans);font-size:14px;line-height:1.55;-webkit-font-smoothing:antialiased;}
.wrap{max-width:1200px;margin:0 auto;padding:30px 22px 64px;}
.eyebrow{font-family:var(--mono);font-size:11px;letter-spacing:.14em;text-transform:uppercase;color:var(--accent);margin:0 0 6px;}
h1{font-size:23px;line-height:1.2;margin:0 0 12px;color:var(--bright);font-weight:660;text-wrap:balance;}
.lede{margin:0;max-width:78ch;color:var(--ink2);font-size:13.5px;}
.lede b{color:var(--bright);} .lede code{font-family:var(--mono);font-size:12px;background:var(--panel);padding:1px 5px;border-radius:3px;color:var(--bright);}
h2{font-size:14px;margin:36px 0 2px;padding-top:16px;border-top:1px solid var(--border);color:var(--bright);font-weight:640;letter-spacing:.02em;}
.h2note{font-family:var(--mono);font-size:11.5px;color:var(--muted);font-weight:400;}
.legend{display:flex;flex-wrap:wrap;gap:6px 15px;margin-top:16px;padding:11px 13px;background:var(--panel);border:1px solid var(--border);border-radius:6px;}
.lg{display:inline-flex;align-items:center;gap:6px;font-size:12px;color:var(--ink2);font-family:var(--mono);}
.sw{width:12px;height:12px;border-radius:2px;flex:none;}
.grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(300px,1fr));gap:16px;align-items:start;margin-top:18px;}
.card{background:var(--panel);border:1px solid var(--border);border-radius:9px;padding:11px 11px 12px;display:flex;flex-direction:column;gap:8px;}
.chead{display:flex;align-items:baseline;justify-content:space-between;gap:8px;}
.clabel{font-family:var(--mono);font-size:12.5px;color:var(--bright);font-weight:600;}
.chip{font-family:var(--mono);font-size:9.5px;letter-spacing:.06em;text-transform:uppercase;padding:2px 6px;border-radius:10px;font-weight:700;white-space:nowrap;}
.chip-bad{background:#7f1d1d;color:#fecaca;} .chip-good{background:#14532d;color:#bbf7d0;} .chip-ref{background:#334155;color:#cbd5e1;}
.svg-wrap{background:var(--bg);border:1px solid var(--border);border-radius:6px;overflow:hidden;line-height:0;}
svg.map{display:block;width:100%;height:auto;}
.cnote{margin:0;font-size:12px;color:var(--ink2);}
.stats{display:flex;flex-direction:column;gap:3px;border-top:1px solid var(--border);padding-top:8px;font-family:var(--mono);font-size:11.5px;}
.srow{display:flex;justify-content:space-between;gap:10px;} .sk{color:var(--muted);} .sv{color:var(--bright);text-align:right;}
.v-bad{color:#f87171;} .v-ok{color:#4ade80;}
footer{margin-top:34px;padding-top:14px;border-top:1px solid var(--border);font-family:var(--mono);font-size:11px;color:var(--muted);max-width:90ch;}
</style>
""";

var page = new StringBuilder();
page.Append("<title>Slice C reconciliation — deriver vs the cramming seeds</title>");
page.Append(css);
page.Append("<div class=\"wrap\">");
page.Append("<p class=\"eyebrow\">Layout evaluator · Slice C reconciliation</p>");
page.Append("<h1>What the deriver reads on the cramming seeds</h1>");
page.Append("<p class=\"lede\">Seven derived features all came back <b>opposite</b> to the map: the crammed negatives are " +
    "indistinguishable from good real maps, and the stone void-exposure reads backwards. This gallery shows the deriver's " +
    "own picture so we can find where it diverges from your eye. Terrain is coloured by <b>team</b> " +
    "(<b style=\"color:#3b82f6\">image 0</b> / <b style=\"color:#ef4444\">image 1</b>) so the <b>offset of the two masses</b> is " +
    "visible; the <b style=\"color:#64748b\">slate</b> mid is the build region; each " +
    "<b style=\"color:#facc15\">neutral stone</b> is labelled with its <b>void-exposure %</b> (void-border ÷ non-land border — " +
    "the number that read backwards); each enclosed void is labelled with its <code>crossRoutes</code>. The question for each " +
    "card: does the deriver's team-offset and stone-exposure match what you drew?</p>");

page.Append("<div class=\"legend\">" +
    "<span class=\"lg\"><span class=\"sw\" style=\"background:#3b82f6\"></span>team image 0</span>" +
    "<span class=\"lg\"><span class=\"sw\" style=\"background:#ef4444\"></span>team image 1</span>" +
    "<span class=\"lg\"><span class=\"sw\" style=\"background:#facc15\"></span>neutral stone (n=exposure%)</span>" +
    "<span class=\"lg\"><span class=\"sw\" style=\"background:#d946ef\"></span>team stone</span>" +
    "<span class=\"lg\"><span class=\"sw\" style=\"background:#64748b\"></span>mid build</span>" +
    "<span class=\"lg\"><span class=\"sw\" style=\"background:#f59e0b\"></span>frontline edge</span>" +
    "<span class=\"lg\"><span class=\"sw\" style=\"background:#ef4444\"></span>middle void (cross0! = no rotation)</span>" +
    "</div>");

page.Append("<h2>Author: bad <span class=\"h2note\">— the escalation of defects</span></h2>");
page.Append($"<div class=\"grid\">{Grid("bad")}</div>");
page.Append("<h2>Author: good <span class=\"h2note\">— the three resolutions (all restore rotation)</span></h2>");
page.Append($"<div class=\"grid\">{Grid("good")}</div>");
page.Append("<h2>Reference <span class=\"h2note\">— a good real map the crammed seeds are metrically identical to</span></h2>");
page.Append($"<div class=\"grid\">{Grid("ref")}</div>");

page.Append("<footer>CT9 (uncrossed-middle-void) fires only where a middle void has crossRoutes 0 — the over-stretched card. " +
    "The crammed cards read clean on every current feature; the deriver has no primitive for the offset of opposing team " +
    "masses (G69). Generated by tools/deriver/reconcile-gallery.cs.</footer>");
page.Append("</div>");

File.WriteAllText(outPath, page.ToString());
Console.WriteLine($"wrote {outPath} — {maps.Length} cards");
