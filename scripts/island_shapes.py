#!/usr/bin/env python3
"""Deep-dive on real CTW island SHAPES (studio detection), toward a generator shape language.

Three questions:
  A. What do island shapes look like, from their SIMPLIFIED polygon (Douglas-Peucker)?
     — area / compactness / convexity / aspect / rectangularity, team (objective) vs neutral, a coarse
       blob / rectangle / lane / branched taxonomy.
  B. Can island PLACEMENT live on a grid? — island bbox sizes in lane-width units, and island centres
     quantised to a board grid (are placements lattice-like + symmetric?).
  C. How do real island shapes DISSECT into lanes? — skeletonise each team island, count branches
     (endpoints = lane tips) and junctions (hubs), estimate lane width = 2·area / skeleton-length.

Run with the shapely+skimage venv:  /root/ctw-venv/bin/python scripts/island_shapes.py
Reads the populated MariaDB via the mysql CLI (no connector dep). Dev creds only.
"""
import json
import subprocess
from collections import Counter, defaultdict

import numpy as np
from shapely.geometry import shape as shapely_shape
from skimage.draw import polygon as draw_polygon
from skimage.morphology import skeletonize

DB = ["mysql", "--user=pgm", "--password=pgm_dev_pw", "--host=127.0.0.1", "pgm_studio"]
TOL = 2.0          # Douglas-Peucker tolerance (matches the studio --island-study default)
LANE_W = 12.0      # the generator's default LaneWidth — the grid unit we test against
SKEL_MIN = 200     # only dissect islands above this block_count into lanes (skip specks)


def q(sql):
    out = subprocess.run(DB + ["-N", "-B", "-e", sql], capture_output=True, text=True).stdout
    return [l.split("\t") for l in out.splitlines() if l]


def raw(sql):
    return subprocess.run(DB + ["-N", "--raw", "-e", sql], capture_output=True, text=True).stdout


def pt(js):
    d = json.loads(js)
    return (float(d["x"]), float(d["z"]))


def med(xs):
    return float(np.median(xs)) if len(xs) else 0.0


def pct(a, b):
    return f"{100.0 * a / b:.0f}%" if b else "—"


# ── per-island shape features from the simplified polygon ──────────────────────
def features(geojson):
    poly = shapely_shape(geojson)
    if poly.is_empty or poly.area < 1:
        return None
    simp = poly.simplify(TOL, preserve_topology=True)
    if simp.is_empty or simp.area < 1:
        simp = poly
    ext = list(simp.exterior.coords)
    raw_v = len(poly.exterior.coords)
    area = simp.area
    per = simp.length
    hull = simp.convex_hull.area or area
    minx, miny, maxx, maxy = simp.bounds
    w, h = max(1.0, maxx - minx), max(1.0, maxy - miny)
    lo, hi = min(w, h), max(w, h)
    # PCA elongation on the exterior vertices (major/minor spread)
    pts = np.array(ext[:-1]) if len(ext) > 2 else np.array(ext)
    elong = 1.0
    if len(pts) >= 3:
        c = pts - pts.mean(0)
        ev = np.linalg.eigvalsh(np.cov(c.T))
        ev = np.clip(ev, 1e-9, None)
        elong = float((ev.max() / ev.min()) ** 0.5)
    return {
        "raw_v": raw_v, "simp_v": len(ext) - 1, "area": area,
        "compactness": 4 * np.pi * area / (per * per) if per else 0,   # 1=circle → 0 lane/branchy
        "convexity": area / hull,                                       # 1=convex → low concave/branchy
        "aspect": hi / lo,                                             # bbox long/short
        "rect": area / (w * h),                                        # area / bbox  (1=filled rectangle)
        "elong": elong, "w": w, "h": h,
        "nholes": len(simp.interiors) if hasattr(simp, "interiors") else 0,
    }


def bucket(f):
    if f["convexity"] < 0.70:
        return "branched"                       # concave, multiple arms (a hub with lanes)
    if f["aspect"] >= 3.0 or f["compactness"] < 0.30:
        return "lane/elongated"                 # a strip
    if f["rect"] >= 0.80:
        return "rectangle"                      # a filled box
    return "blob"                               # roundish / compact


# ── lane dissection: skeletonise an island mask → endpoints / junctions / width ─
def dissect(geojson):
    poly = shapely_shape(geojson)
    minx, miny, maxx, maxy = (int(np.floor(v)) for v in poly.bounds)
    H, W = maxy - miny + 3, maxx - minx + 3
    if H < 3 or W < 3 or H * W > 4_000_000:
        return None
    mask = np.zeros((H, W), dtype=np.uint8)
    ext = np.array(poly.exterior.coords)
    rr, cc = draw_polygon(ext[:, 1] - miny + 1, ext[:, 0] - minx + 1, shape=mask.shape)
    mask[rr, cc] = 1
    for ring in poly.interiors:
        h = np.array(ring.coords)
        rr, cc = draw_polygon(h[:, 1] - miny + 1, h[:, 0] - minx + 1, shape=mask.shape)
        mask[rr, cc] = 0
    area = int(mask.sum())
    if area < SKEL_MIN:
        return None
    sk = skeletonize(mask > 0)
    if sk.sum() == 0:
        return None
    nb = np.zeros_like(sk, dtype=np.uint8)
    s = sk.astype(np.uint8)
    nb[1:-1, 1:-1] = (s[:-2, 1:-1] + s[2:, 1:-1] + s[1:-1, :-2] + s[1:-1, 2:]
                      + s[:-2, :-2] + s[:-2, 2:] + s[2:, :-2] + s[2:, 2:])
    on = sk
    endpoints = int(np.sum(on & (nb == 1)))
    junctions = int(np.sum(on & (nb >= 3)))
    length = int(sk.sum())
    return {"endpoints": endpoints, "junctions": junctions, "width": 2 * area / length if length else 0, "area": area}


# ── gather ────────────────────────────────────────────────────────────────────
maps = {int(i): s for i, s in q("SELECT id, slug FROM map WHERE gamemode='ctw'")}
obj_pts = defaultdict(list)
for mid, loc in q("SELECT map_id, location_json FROM wool WHERE location_json LIKE '{%'"):
    obj_pts[int(mid)].append(pt(loc))
for mid, loc in q("SELECT w.map_id, m.location_json FROM monument m JOIN wool w ON m.wool_id=w.id WHERE m.location_json LIKE '{%'"):
    obj_pts[int(mid)].append(pt(loc))


def point_in(poly_xy, x, z):
    inside = False
    n = len(poly_xy)
    j = n - 1
    for i in range(n):
        xi, zi = poly_xy[i]
        xj, zj = poly_xy[j]
        if ((zi > z) != (zj > z)) and (x < (xj - xi) * (z - zi) / (zj - zi + 1e-12) + xi):
            inside = not inside
        j = i
    return inside


feats = {"team": [], "neutral": []}
tax = {"team": Counter(), "neutral": Counter()}
diss = []                       # lane dissection of team islands
grid_wcells, grid_hcells = [], []   # island bbox in lane-width cells
neutral_quad = Counter()        # neutral island centre quantised to a 3x3 board grid
analyzed = 0

for mid in maps:
    if not obj_pts.get(mid):
        continue
    blob = raw(f"SELECT data FROM map_artifact WHERE map_id={mid} AND kind='islands_json'")
    if not blob.strip():
        continue
    try:
        islands = json.loads(blob)
    except Exception:
        continue
    # board extent for the grid analysis
    bx = [i["bounds"] for i in islands if i.get("bounds")]
    if not bx:
        continue
    minX = min(b[0] for b in bx); minZ = min(b[1] for b in bx)
    maxX = max(b[2] for b in bx); maxZ = max(b[3] for b in bx)
    cx, cz = (minX + maxX) / 2, (minZ + maxZ) / 2
    bw, bh = max(1, maxX - minX), max(1, maxZ - minZ)
    analyzed += 1

    for isl in islands:
        gj = isl.get("polygon")
        ring = (gj or {}).get("coordinates", [[]])[0]
        if not gj or len(ring) < 4:
            continue
        f = features(gj)
        if f is None:
            continue
        hosts = any(point_in(ring, x, z) for (x, z) in obj_pts[mid])
        kind = "team" if hosts else "neutral"
        feats[kind].append(f)
        tax[kind][bucket(f)] += 1
        grid_wcells.append(f["w"] / LANE_W)
        grid_hcells.append(f["h"] / LANE_W)
        if not hosts:
            b = isl["bounds"]
            icx, icz = (b[0] + b[2]) / 2, (b[1] + b[3]) / 2
            gx = 0 if icx < cx - bw / 6 else (2 if icx > cx + bw / 6 else 1)
            gz = 0 if icz < cz - bh / 6 else (2 if icz > cz + bh / 6 else 1)
            neutral_quad[(gx, gz)] += 1
        if hosts and isl.get("block_count", 0) >= SKEL_MIN:
            d = dissect(gj)
            if d:
                diss.append(d)


# ── report ────────────────────────────────────────────────────────────────────
def line(kind):
    fs = feats[kind]
    if not fs:
        return f"  {kind:<8}: (none)"
    return (f"  {kind:<8} n={len(fs):<5} area={med([f['area'] for f in fs]):.0f}  "
            f"compact={med([f['compactness'] for f in fs]):.2f}  convex={med([f['convexity'] for f in fs]):.2f}  "
            f"aspect={med([f['aspect'] for f in fs]):.1f}  rect={med([f['rect'] for f in fs]):.2f}  "
            f"holes/isl={sum(f['nholes'] for f in fs) / len(fs):.2f}")


print(f"\nISLAND SHAPES — {analyzed} maps, {len(feats['team']) + len(feats['neutral'])} islands "
      f"(studio detection; simplified @ DP tol={TOL})\n")

allf = feats["team"] + feats["neutral"]
print("SIMPLIFICATION (raw block-outline → Douglas-Peucker)")
print(f"  vertices: raw median {med([f['raw_v'] for f in allf]):.0f} → simplified median {med([f['simp_v'] for f in allf]):.0f}  "
      f"({pct(med([f['simp_v'] for f in allf]), med([f['raw_v'] for f in allf]))} kept)\n")

print("SHAPE FEATURES (median; compact 1=disc→0 strip · convex 1=solid→low branchy · rect 1=filled box)")
print(line("team"))
print(line("neutral"))

print("\nCOARSE TAXONOMY")
for kind in ("team", "neutral"):
    t = tax[kind]; tot = sum(t.values())
    parts = "  ".join(f"{k} {pct(t[k], tot)}" for k in ("blob", "rectangle", "lane/elongated", "branched"))
    print(f"  {kind:<8} {parts}")

print("\nB. GRID PLACEMENT")
wc = np.array(grid_wcells); hc = np.array(grid_hcells)
print(f"  island bbox in lane-widths (lw={LANE_W:.0f}): width median {med(grid_wcells):.1f}  height median {med(grid_hcells):.1f}")
frac = np.concatenate([wc, hc]) % 1.0
near = np.mean(np.minimum(frac, 1 - frac) < 0.20)
print(f"  bbox side within ±0.2 lane-width of an integer multiple: {near*100:.0f}%  (lattice-fit signal)")
tot = sum(neutral_quad.values())
print("  neutral-island centre on a 3×3 board grid (col,row; row 0 = far edge, 1 = mid):")
for gz in range(3):
    print("     " + "  ".join(f"[{gx},{gz}] {pct(neutral_quad[(gx,gz)], tot)}" for gx in range(3)))

print("\nC. LANE DISSECTION (team islands ≥ %d blocks, skeletonised)  n=%d" % (SKEL_MIN, len(diss)))
if diss:
    eps = [d["endpoints"] for d in diss]
    jcs = [d["junctions"] for d in diss]
    wid = [d["width"] for d in diss]
    print(f"  branches (skeleton endpoints = lane tips) per team island: median {med(eps):.0f}  mean {np.mean(eps):.1f}  p90 {np.percentile(eps,90):.0f}")
    print(f"  junctions (hubs, degree ≥ 3) per team island:               median {med(jcs):.0f}  mean {np.mean(jcs):.1f}")
    print(f"  estimated lane width (2·area / skeleton length):            median {med(wid):.1f} blocks")
    bh = Counter(min(e, 8) for e in eps)
    print("  branch-count distribution: " + "  ".join(f"{k}{'+' if k==8 else ''}:{pct(bh[k],len(eps))}" for k in sorted(bh)))
print()
