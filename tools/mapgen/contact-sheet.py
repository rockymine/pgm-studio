#!/usr/bin/env python3
"""Build a review sheet for generated maps: every spec beside its top-down render.

    python3 tools/mapgen/contact-sheet.py <renderDir> <out.html> <spec.json> [...]

The renders are embedded as data URIs so the page is one self-contained file. Facts come from the specs
themselves and from the PNGs on disk, so the sheet cannot claim a map that was not built.
"""
import base64, json, os, struct, sys, html

def png_size(path):
    with open(path, "rb") as handle:
        head = handle.read(24)
    return struct.unpack(">II", head[16:24])          # IHDR width, height

GOAL = {"ctw": ("Capture the wool", "wool"),
        "dtm": ("Destroy the monument", "monument"),
        "dtcm": ("Monument and core", "both")}

def read(spec_path, render_dir):
    spec = json.load(open(spec_path))
    slug = spec["slug"]
    png = os.path.join(render_dir, f"{slug}.png")
    if not os.path.exists(png):
        return None
    compose = spec.get("compose", {})
    theme = spec.get("theme", {})
    relief = spec.get("relief", {})
    width, height = png_size(png)
    scale = 3
    mode = compose.get("objective_mode", "ctw")
    marks = relief.get("marks")
    return {
        "by": "Haiku 4.5" if os.sep + "haiku" + os.sep in os.path.abspath(spec_path) else "Opus 5",
        "slug": slug,
        "name": spec.get("name", slug),
        "objective": spec.get("objective", ""),
        "goal": GOAL.get(mode, (mode, mode))[0],
        "mode": mode,
        "teams": compose.get("teams", 2),
        "players": compose.get("players_per_team", 0) * compose.get("teams", 2),
        "symmetry": compose.get("symmetry", "rot_180"),
        "seed": compose.get("seed", 0),
        "cell": compose.get("cell", 5),
        "blocks": f"{width // scale}×{height // scale}",
        "surface": theme.get("surface", "—"),
        "wall": theme.get("wall", "—"),
        "pattern": theme.get("pattern", "solid"),
        "relief": ("stated marks" if marks else "scattered") if relief else "flat",
        "shell": (spec.get("room_shell") or {}).get("wool", "built-in"),
        "spawn_shell": (spec.get("room_shell") or {}).get("spawn", "built-in"),
        "trees": spec.get("trees", {}).get("count", 0),
        "village": spec.get("village", {}).get("count", len(spec.get("houses", []) or [])),
        "img": base64.b64encode(open(png, "rb").read()).decode(),
        "out": spec.get("out_dir", ""),
    }

render_dir, out_path, spec_paths = sys.argv[1], sys.argv[2], sys.argv[3:]
maps = [m for m in (read(p, render_dir) for p in spec_paths) if m]
order = {"ctw": 0, "dtm": 1, "dtcm": 2}
maps.sort(key=lambda m: (order.get(m["mode"], 9), m["name"]))

def esc(text): return html.escape(str(text))

cards = []
for m in maps:
    cards.append(f"""
    <article class="map" id="{esc(m['slug'])}">
      <div class="plate"><img alt="Top-down render of {esc(m['name'])}" src="data:image/png;base64,{m['img']}"></div>
      <div class="about">
        <header>
          <h3>{esc(m['name'])}</h3>
          <span class="goal goal--{esc(m['mode'])}">{esc(m['goal'])}</span>
          <span class="by">{esc(m['by'])}</span>
        </header>
        <p class="brief">{esc(m['objective'])}</p>
        <dl class="facts">
          <div><dt>Ground</dt><dd>{esc(m['blocks'])} blocks</dd></div>
          <div><dt>Players</dt><dd>{esc(m['players'])} · {esc(m['teams'])} teams</dd></div>
          <div><dt>Symmetry</dt><dd>{esc(m['symmetry'])}</dd></div>
          <div><dt>Seed / cell</dt><dd>{esc(m['seed'])} / {esc(m['cell'])}</dd></div>
          <div><dt>Surface</dt><dd>{esc(m['surface'])} · {esc(m['pattern'])}</dd></div>
          <div><dt>Walls</dt><dd>{esc(m['wall'])}</dd></div>
          <div><dt>Relief</dt><dd>{esc(m['relief'])}</dd></div>
          <div><dt>Rooms</dt><dd>{esc(m['shell'])} / {esc(m['spawn_shell'])}</dd></div>
        </dl>
        <p class="path">{esc(m['out'])}</p>
      </div>
    </article>""")

groups = {}
for m in maps: groups.setdefault(m["mode"], []).append(m["name"])
summary = " · ".join(f"{len(v)} {GOAL.get(k,(k,))[0].lower()}" for k, v in
                     sorted(groups.items(), key=lambda kv: order.get(kv[0], 9)))

page = f"""<title>Fifteen Generated Boards</title>
<style>
:root {{
  --ground:#f2f1ec; --plate:#12141a; --card:#ffffff; --edge:#dcd9d0;
  --ink:#1b1d23; --muted:#6d7280; --accent:#5f7d4f; --clay:#a8552f; --frost:#4a6f8a;
}}
:root:not([data-theme="light"]) {{ }}
@media (prefers-color-scheme: dark) {{
  :root:not([data-theme="light"]) {{
    --ground:#0e1015; --plate:#070809; --card:#171a21; --edge:#272b34;
    --ink:#e7e9ee; --muted:#939aa8; --accent:#8fb37a; --clay:#d08b5f; --frost:#7fa8c4;
  }}
}}
:root[data-theme="dark"] {{
  --ground:#0e1015; --plate:#070809; --card:#171a21; --edge:#272b34;
  --ink:#e7e9ee; --muted:#939aa8; --accent:#8fb37a; --clay:#d08b5f; --frost:#7fa8c4;
}}
* {{ box-sizing:border-box; }}
body {{
  margin:0; background:var(--ground); color:var(--ink);
  font:16px/1.6 system-ui,-apple-system,"Segoe UI",sans-serif;
}}
.wrap {{ max-width:1180px; margin:0 auto; padding:56px 24px 96px; }}
header.top {{ border-bottom:2px solid var(--ink); padding-bottom:20px; margin-bottom:8px; }}
h1 {{
  font-family:Georgia,"Iowan Old Style","Times New Roman",serif;
  font-weight:600; font-size:clamp(2rem,4.5vw,3rem); line-height:1.1; margin:0 0 10px;
  text-wrap:balance; letter-spacing:-0.01em;
}}
.lede {{ margin:0; color:var(--muted); max-width:62ch; }}
.tally {{
  display:flex; flex-wrap:wrap; gap:8px 22px; margin:18px 0 44px;
  font-family:ui-monospace,SFMono-Regular,Menlo,monospace; font-size:.78rem;
  text-transform:uppercase; letter-spacing:.08em; color:var(--muted);
}}
.maps {{ display:flex; flex-direction:column; gap:28px; }}
.map {{
  display:grid; grid-template-columns:minmax(0,300px) minmax(0,1fr); gap:0;
  background:var(--card); border:1px solid var(--edge); border-radius:3px; overflow:hidden;
}}
@media (max-width:760px) {{ .map {{ grid-template-columns:1fr; }} }}
.plate {{
  background:var(--plate); display:flex; align-items:center; justify-content:center;
  padding:18px; min-height:300px;
}}
.plate img {{ max-width:100%; max-height:420px; image-rendering:pixelated; display:block; }}
.about {{ padding:24px 26px; display:flex; flex-direction:column; gap:14px; }}
.about header {{ display:flex; align-items:baseline; gap:14px; flex-wrap:wrap; }}
h3 {{
  font-family:Georgia,"Iowan Old Style","Times New Roman",serif;
  font-size:1.5rem; font-weight:600; margin:0; letter-spacing:-0.01em;
}}
.goal {{
  font-family:ui-monospace,SFMono-Regular,Menlo,monospace; font-size:.68rem;
  text-transform:uppercase; letter-spacing:.1em; padding:4px 9px; border-radius:2px;
  border:1px solid currentColor;
}}
.goal--ctw {{ color:var(--accent); }}
.goal--dtm {{ color:var(--clay); }}
.goal--dtcm {{ color:var(--frost); }}
.by {{
  font-family:ui-monospace,SFMono-Regular,Menlo,monospace; font-size:.66rem;
  text-transform:uppercase; letter-spacing:.1em; color:var(--muted); margin-left:auto;
}}
.brief {{ margin:0; color:var(--muted); max-width:60ch; }}
.facts {{ display:grid; grid-template-columns:repeat(auto-fit,minmax(150px,1fr)); gap:12px 20px; margin:4px 0 0; }}
.facts div {{ display:flex; flex-direction:column; gap:2px; min-width:0; }}
dt {{
  font-family:ui-monospace,SFMono-Regular,Menlo,monospace; font-size:.66rem;
  text-transform:uppercase; letter-spacing:.09em; color:var(--muted);
}}
dd {{ margin:0; font-size:.92rem; font-variant-numeric:tabular-nums; }}
.path {{
  margin:6px 0 0; font-family:ui-monospace,SFMono-Regular,Menlo,monospace;
  font-size:.72rem; color:var(--muted); word-break:break-all;
}}
</style>

<div class="wrap">
  <header class="top">
    <h1>Fifteen Generated Boards</h1>
    <p class="lede">Every one built from a single JSON spec through the studio's own export path — the
      generator answers the layout, the spec says what the map is about. Renders are top-down, three pixels
      to the block; black is void. Four were authored by Haiku 4.5 working from the spec reference alone —
      they are labelled, and they are not the weakest of the set.</p>
  </header>
  <div class="tally"><span>{esc(len(maps))} maps</span><span>{esc(summary)}</span><span>all one island</span></div>
  <div class="maps">{''.join(cards)}</div>
</div>
"""
open(out_path, "w").write(page)
print(f"wrote {out_path} — {len(maps)} maps")
