#!/usr/bin/env python3
"""Renders the wool-approach shape-read evidence gallery to
tools/deriver/out/approach-read-gallery.html: the emitter's canonical family
profiles, the three candidate reads over the traced corpus, the fragmentation
experiment, and the emitter-variant context-dependence cards.
Reads come from approach_read_lab (the validated classifier port)."""
import glob, os, html
import approach_read_lab as lab

CELL = 11

SLOT_CLASS = {
    "entry": "s-entry", "run": "s-run", "bar": "s-bar", "leg": "s-leg",
    "entry-run": "s-run", "room-run": "s-run", "entry-bar": "s-bar", "room-bar": "s-bar",
}

def svg_grid(layers):
    allc = set()
    for cells, _ in layers:
        allc |= set(cells)
    if not allc:
        return "<svg></svg>"
    mnx, mnz, mxx, mxz = lab.bbox(allc)
    w = (mxx - mnx + 1) * CELL; h = (mxz - mnz + 1) * CELL
    out = [f'<svg viewBox="-2 -2 {w+4} {h+4}" style="width:min(100%,{(w+4)*1.6:.0f}px)" role="img">']
    for cells, cls in layers:
        for (x, z) in sorted(cells):
            out.append(f'<rect class="{cls}" x="{(x-mnx)*CELL}" y="{(z-mnz)*CELL}" width="{CELL}" height="{CELL}"/>')
    out.append("</svg>")
    return "".join(out)

def pill(label, kind="ok"):
    return f'<span class="pill pill--{kind}">{html.escape(label)}</span>'

def card(title, svg, caption_html):
    return (f'<figure class="card"><figcaption><strong>{html.escape(title)}</strong></figcaption>'
            f'{svg}<div class="reads">{caption_html}</div></figure>')

def section_profiles():
    cards = []
    for fam, (W, H) in lab.EMIT_BOX.items():
        t, room = lab.emit(fam, W, H)
        layers = [(lab.rect_cells([0, 0, W, H]), "c-box")]
        for r, slot in t:
            layers.append((lab.rect_cells(r), SLOT_CLASS.get(slot, "s-run")))
        layers.append((lab.rect_cells(room), "c-wool"))
        filled = set().union(*[lab.rect_cells(r) for r, _ in t]) | lab.rect_cells(room)
        got, _ = lab.classify(filled, lab.rect_cells(room))
        cards.append(card(fam, svg_grid(layers),
                          f'classifies as {pill(got, "ok" if got == fam else "bad")}'))
    return cards

def section_traced():
    cards = []
    counts = {"terr": {}, "lane": {}, "zones": {}}
    for path in sorted(glob.glob(f"{lab.ROOT}/tools/seeds/traced/*.plan.json")):
        name = os.path.basename(path).replace(".plan.json", "")
        d, terrain, zones, wools, roles = lab.load_plan(path)
        filled = set().union(*terrain.values())
        spawn = set().union(*[c for i, c in terrain.items() if roles.get(i) == "spawn"]) \
            if any(roles.get(i) == "spawn" for i in terrain) else set()
        for wid in wools:
            if wid not in terrain: continue
            term = terrain[wid]
            comp = lab.flood(term, filled)
            famT, _ = lab.classify(filled, term)
            (laneRead, _), laneCells = lab.classify_open(filled, term)
            famZ, _ = lab.classify(filled | zones, term)
            for k, v in (("terr", famT), ("lane", laneRead), ("zones", famZ)):
                counts[k][v] = counts[k].get(v, 0) + 1
            layers = [
                (zones, "c-zone"),
                (filled - comp, "c-ter"),
                (comp - term - laneCells, "c-comp"),
                (laneCells, "c-lane"),
                (spawn - term, "c-spawn"),
                (term, "c-wool"),
            ]
            scope = ("component = whole unit" if comp == filled else "component = a fragment island")
            cap = (f'Classify(terrain) {pill(famT, "bad")} &middot; lane read {pill(laneRead, "warn")} '
                   f'&middot; Classify(terrain &cup; zones) {pill(famZ, "bad")} &middot; '
                   f'<span class="note">{scope}</span>')
            cards.append(card(f"{name} / {wid}", svg_grid(layers), cap))
    return cards, counts

def section_frag():
    cards = []
    for fam in ("L", "Z", "Scythe", "U", "Donut"):
        W, H = lab.EMIT_BOX[fam]
        t, room = lab.emit(fam, W, H)
        roomC = lab.rect_cells(room)
        for r, slot in t:
            if slot == "entry" and fam != "U":
                continue
            terr = set().union(*[lab.rect_cells(rr) for rr, _ in t if rr is not r]) | roomC
            promoted = lab.rect_cells(r)
            famT, _ = lab.classify(terr, roomC)
            famZ, _ = lab.classify(terr | promoted, roomC)
            layers = [(terr - roomC, "c-ter"), (promoted, "c-zone"), (roomC, "c-wool")]
            cap = (f'terrain-only {pill(famT, "ok" if famT == fam else "bad")} &middot; '
                   f'terrain &cup; cut {pill(famZ, "ok" if famZ == fam else "bad")}')
            cards.append(card(f"{fam} — {slot} promoted to build", svg_grid(layers), cap))
    return cards

def section_variants():
    cards = []
    for name, rows, want in lab.VARIANT_CASES:
        filled, term, hub = lab.grid_cells(rows)
        fam, _ = lab.classify(filled, term)
        layers = [(hub, "c-comp"), (filled - term - hub, "c-ter"), (term, "c-wool")]
        cap = f'expected {pill(want, "ok")} &rarr; reads {pill(fam, "ok" if fam == want else "bad")}'
        cards.append(card(name, svg_grid(layers), cap))
    return cards

def counts_table(counts):
    fams = ["Isolated", "I", "L", "Z", "Scythe", "Clamp", "U", "H", "Donut",
            "complex", "plaza", "none"]
    labels = {"terr": "Classify on terrain (unscoped — today)",
              "lane": "ClassifyOpen lane read (junction-stop)",
              "zones": "Classify on terrain ∪ zones (naive surface)"}
    head = "".join(f"<th>{f}</th>" for f in fams)
    rows = []
    for k in ("terr", "lane", "zones"):
        cells = "".join(f"<td>{counts[k].get(f, '') or ''}</td>" for f in fams)
        rows.append(f"<tr><th>{labels[k]}</th>{cells}</tr>")
    return ('<div class="tablewrap"><table><thead><tr><th>read (19 wools, 12 traced maps)</th>'
            + head + "</tr></thead><tbody>" + "".join(rows) + "</tbody></table></div>")

CSS = """
:root {
  --bg:#f6f7f8; --panel:#ffffff; --ink:#22272d; --sub:#5b6672; --line:#dbe1e6;
  --ter:#aeb9c2; --comp:#316f79; --lane:#7fc4cf; --zone:#a8c4e6; --wool:#d9a13c;
  --spawn:#6faa6f; --box:#eef1f3; --bad:#b3423f; --warn:#8a6d1f; --ok:#2f6b46;
  --s-entry:#4d7f9e; --s-run:#7d95a6; --s-bar:#5e7286; --s-leg:#93a68f;
}
:root[data-theme="dark"] { --bg:#181c20; --panel:#20262c; --ink:#dde3e8; --sub:#96a1ab;
  --line:#333b43; --ter:#4c565f; --comp:#57b3c0; --lane:#2e6f79; --zone:#3a5578;
  --wool:#d9a13c; --spawn:#5d8f5d; --box:#262d33; --bad:#e07a77; --warn:#cbb35e; --ok:#7bbf95;
  --s-entry:#5d93b5; --s-run:#66788a; --s-bar:#8fa5ba; --s-leg:#7d9179; }
@media (prefers-color-scheme: dark) {
  :root:not([data-theme="light"]) { --bg:#181c20; --panel:#20262c; --ink:#dde3e8; --sub:#96a1ab;
  --line:#333b43; --ter:#4c565f; --comp:#57b3c0; --lane:#2e6f79; --zone:#3a5578;
  --wool:#d9a13c; --spawn:#5d8f5d; --box:#262d33; --bad:#e07a77; --warn:#cbb35e; --ok:#7bbf95;
  --s-entry:#5d93b5; --s-run:#66788a; --s-bar:#8fa5ba; --s-leg:#7d9179; }
}
* { box-sizing:border-box }
body { background:var(--bg); color:var(--ink); margin:0;
  font:15px/1.55 system-ui, -apple-system, "Segoe UI", sans-serif; }
main { max-width:1080px; margin:0 auto; padding:2.5rem 1.25rem 5rem; }
h1 { font-size:1.7rem; line-height:1.25; text-wrap:balance; margin:0 0 .4rem; }
h2 { font-size:1.15rem; margin:2.8rem 0 .3rem; }
p.lead, section > p, main > p { color:var(--sub); max-width:68ch; margin:.2rem 0 1rem; }
.kicker { font:600 .72rem/1 ui-monospace, "SF Mono", Consolas, monospace;
  letter-spacing:.12em; text-transform:uppercase; color:var(--comp); }
.grid { display:grid; grid-template-columns:repeat(auto-fill, minmax(215px, 1fr)); gap:14px; }
.grid--wide { grid-template-columns:repeat(auto-fill, minmax(320px, 1fr)); }
.card { background:var(--panel); border:1px solid var(--line); border-radius:6px;
  margin:0; padding:12px; display:flex; flex-direction:column; gap:8px; }
.card figcaption { font:600 .8rem/1.3 ui-monospace, "SF Mono", Consolas, monospace; }
.card svg { display:block; }
.reads { font:.74rem/1.6 ui-monospace, "SF Mono", Consolas, monospace; color:var(--sub); }
.pill { display:inline-block; padding:0 .45em; border-radius:99px; font-weight:700; }
.pill--ok { color:var(--ok); background:color-mix(in srgb, var(--ok) 12%, transparent); }
.pill--bad { color:var(--bad); background:color-mix(in srgb, var(--bad) 12%, transparent); }
.pill--warn { color:var(--warn); background:color-mix(in srgb, var(--warn) 14%, transparent); }
.note { opacity:.85 }
rect { shape-rendering:crispEdges }
.c-ter{fill:var(--ter)} .c-comp{fill:var(--comp)} .c-lane{fill:var(--lane)}
.c-zone{fill:var(--zone)} .c-wool{fill:var(--wool)} .c-spawn{fill:var(--spawn)}
.c-box{fill:var(--box)}
.s-entry{fill:var(--s-entry)} .s-run{fill:var(--s-run)} .s-bar{fill:var(--s-bar)} .s-leg{fill:var(--s-leg)}
.legend { display:flex; flex-wrap:wrap; gap:.4rem 1.1rem; font:.74rem/1.6 ui-monospace, Consolas, monospace;
  color:var(--sub); margin:.6rem 0 1.2rem; }
.legend span { display:inline-flex; align-items:center; gap:.35em; }
.legend i { width:.8em; height:.8em; display:inline-block; border-radius:2px; }
.tablewrap { overflow-x:auto; margin:1rem 0; }
table { border-collapse:collapse; font:.78rem/1.5 ui-monospace, Consolas, monospace; }
th, td { border:1px solid var(--line); padding:.3em .6em; text-align:center;
  font-variant-numeric:tabular-nums; }
tbody th { text-align:left; font-weight:600; }
"""

def build():
    prof = section_profiles()
    traced, counts = section_traced()
    frag = section_frag()
    variants = section_variants()

    def legend(items):
        return '<div class="legend">' + "".join(
            f'<span><i style="background:var(--{v})"></i>{k}</span>' for k, v in items) + "</div>"

    page = f"""<title>Reading wool approach shapes in real maps</title>
<style>{CSS}</style>
<main>
<span class="kicker">pgm-studio &middot; layout generation &middot; shape-read evidence</span>
<h1>Can the shape deriver read wool approaches inside actual maps?</h1>
<p class="lead">Evidence gallery for the derive-side scope question. All reads come from a Python port of
<code>ShapeClassifier</code> validated against every fixture in <code>tools/deriver/shapes/</code>
and the emit&rarr;classify mirror (scripts/approach_read_lab.py). Interpretation:
<code>docs/wool-approach-read-investigation.md</code>.</p>

<h2>1 &middot; What the wool-box emitter emits today</h2>
<p>The eight non-isolated families at near-minimal boxes (cw&nbsp;=&nbsp;2), slot-coloured. Every one
round-trips through the classifier &mdash; on a <em>standalone</em> plan.</p>
{legend([("entry","s-entry"),("run","s-run"),("bar","s-bar"),("leg","s-leg"),("wool room","wool"),("box envelope","box")])}
<div class="grid">{''.join(prof)}</div>

<h2>2 &middot; The traced corpus &mdash; three reads, none of them the approach</h2>
<p>19 wools across 12 traced real maps. Teal is the terrain component <code>Classify</code> actually
reads (flooded from the wool); light teal is the junction-stop lane read's extent; blue is build zones.
The unscoped family read returns the <em>unit's</em> topology, the lane read stops at the first
junction or widening, and unioning zones wholesale collapses most wools to Donut.</p>
{counts_table(counts)}
{legend([("classify component","comp"),("lane-read extent","lane"),("other terrain","ter"),("build zone","zone"),("wool room","wool"),("spawn","spawn")])}
<div class="grid grid--wide">{''.join(traced)}</div>

<h2>3 &middot; Fragmentation destroys the terrain-only read</h2>
<p>The pipeline's <em>fragment</em> step (and every hand-authored map) converts approach terrain to
build zones. Promoting a single slot piece to build (blue) changes the terrain-only family read in
every case; reading terrain&nbsp;&cup;&nbsp;that cut recovers it in every case. Post-fragment, family
identity lives on the <em>play surface</em> (land + lane-width build links), not on terrain.</p>
<div class="grid">{''.join(frag)}</div>

<h2>4 &middot; Emitter variants &mdash; family stability under the fold read</h2>
<p>The entry-shift / wool-shift / side-dock variant grids, drawn at uniform 2&times; scale, standalone
and with hub terrain docked at the entry. Under the fold-based scythe test (some row/column crosses
the terrain in two runs) every variant keeps its family in both contexts; the earlier bounding-box
bay test read the shifted scythes as Z standalone, flipping with context. Pinned in
<code>ShapeVariantTests</code>.</p>
{legend([("hub context","comp"),("variant terrain","ter"),("wool room","wool")])}
<div class="grid">{''.join(variants)}</div>
</main>
"""
    out_dir = os.path.join(lab.ROOT, "tools", "deriver", "out")
    os.makedirs(out_dir, exist_ok=True)
    out = os.path.join(out_dir, "approach-read-gallery.html")
    with open(out, "w") as f:
        f.write(page)
    print("wrote", out, len(page), "bytes")

if __name__ == "__main__":
    if lab.e1_validate(verbose=False):
        build()
    else:
        print("port drifted from the C# classifier — gallery not rendered")
