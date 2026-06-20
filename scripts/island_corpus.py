#!/usr/bin/env python3
"""Island-structure validation of the Organic generator against the ingested CTW corpus.

The generator emits exactly one objective-bearing island per team (mirrored) and NO neutral
mid pieces. This measures the real corpus to quantify that gap:
  - island-count distribution (how far from the 2-4-island "clean archetype" assumption)
  - objective islands vs NEUTRAL islands (the contested middle the generator omits)
  - neutral-island SIZE (relative to the team island) and POSITION (central vs scattered)
  - holes per island (the diamond primitive's real frequency)

Reads the populated MariaDB via the mysql CLI (no connector dep). Dev creds only.
Corpus = gamemode 'ctw' maps that have island geometry + a wool objective.
"""
import json
import subprocess
from collections import Counter, defaultdict

DB = ["mysql", "--user=pgm", "--password=pgm_dev_pw", "--host=127.0.0.1", "pgm_studio"]


def q(sql):
    out = subprocess.run(DB + ["-N", "-B", "-e", sql], capture_output=True, text=True).stdout
    return [line.split("\t") for line in out.splitlines() if line]


def raw(sql):
    return subprocess.run(DB + ["-N", "--raw", "-e", sql], capture_output=True, text=True).stdout


def pt(js):
    try:
        d = json.loads(js)
        return (float(d["x"]), float(d["z"]))
    except Exception:
        return None


def point_in_ring(x, z, ring):
    inside = False
    n = len(ring)
    j = n - 1
    for i in range(n):
        xi, zi = ring[i][0], ring[i][1]
        xj, zj = ring[j][0], ring[j][1]
        if ((zi > z) != (zj > z)) and (x < (xj - xi) * (z - zi) / (zj - zi + 1e-12) + xi):
            inside = not inside
        j = i
    return inside


def pct(a, b):
    return f"{100.0 * a / b:.0f}%" if b else "—"


def median(xs):
    s = sorted(xs)
    n = len(s)
    if not n:
        return 0
    return s[n // 2] if n % 2 else (s[n // 2 - 1] + s[n // 2]) / 2


def quantile(xs, p):
    s = sorted(xs)
    return s[min(len(s) - 1, int(p * len(s)))] if s else 0


# ── gather ──────────────────────────────────────────────────────────────────
maps = {int(i): s for i, s in q("SELECT id, slug FROM map WHERE gamemode='ctw'")}
nteams = defaultdict(int)
for mid, n in q("SELECT map_id, COUNT(*) FROM team GROUP BY map_id"):
    nteams[int(mid)] = int(n)

obj_pts = defaultdict(list)   # map_id -> [(x,z), ...]  wool + monument points
for mid, loc in q("SELECT map_id, location_json FROM wool WHERE location_json LIKE '{%'"):
    p = pt(loc)
    if p:
        obj_pts[int(mid)].append(p)
# monuments join through wool for map_id
for mid, loc in q("SELECT w.map_id, m.location_json FROM monument m JOIN wool w ON m.wool_id=w.id WHERE m.location_json LIKE '{%'"):
    p = pt(loc)
    if p:
        obj_pts[int(mid)].append(p)

# ── per-map island structure ──────────────────────────────────────────────────
island_counts = []          # total islands per map
obj_counts = []             # objective islands per map
neutral_counts = []         # neutral islands per map
clean = 0                   # maps with <= 4 islands
maps_with_neutral = 0
neutral_size_ratios = []    # neutral block_count / largest objective island block_count
neutral_central = 0         # neutral islands within 0.30 of map centre (normalised)
neutral_total = 0
hole_counts = []            # holes per island (all islands)
islands_with_hole = 0
islands_total = 0
analyzed = 0
size_bucket = Counter()     # stepping-stone / satellite / substantial
GAMEPLAY_MIN = 64           # ~8x8: below this a neutral island is likely decoration, not a stepping-stone
gameplay_neutral_per_map = []   # neutral islands >= GAMEPLAY_MIN blocks, per map
abs_bucket = Counter()      # absolute block-count buckets for neutral islands

for mid, slug in maps.items():
    blob = raw(f"SELECT data FROM map_artifact WHERE map_id={mid} AND kind='islands_json'")
    if not blob.strip():
        continue
    try:
        raw_islands = json.loads(blob)
    except Exception:
        continue
    if not raw_islands or not obj_pts.get(mid):
        continue

    islands = []   # (block_count, exterior_ring, nholes, (cx,cz))
    minx = minz = 1e9
    maxx = maxz = -1e9
    for isl in raw_islands:
        coords = isl.get("polygon", {}).get("coordinates") or [[]]
        ring = coords[0]
        if len(ring) < 3:
            continue
        b = isl.get("bounds") or [0, 0, 0, 0]
        cx, cz = (b[0] + b[2]) / 2, (b[1] + b[3]) / 2
        islands.append((int(isl.get("block_count", 0)), ring, len(coords) - 1, (cx, cz)))
        minx, minz, maxx, maxz = min(minx, b[0]), min(minz, b[1]), max(maxx, b[2]), max(maxz, b[3])
    if not islands:
        continue

    mcx, mcz = (minx + maxx) / 2, (minz + maxz) / 2
    half_diag = max(1.0, 0.5 * ((maxx - minx) ** 2 + (maxz - minz) ** 2) ** 0.5)

    obj_isl = []
    neu_isl = []
    for bc, ring, nh, c in islands:
        islands_total += 1
        hole_counts.append(nh)
        if nh:
            islands_with_hole += 1
        hosts = any(point_in_ring(x, z, ring) for (x, z) in obj_pts[mid])
        (obj_isl if hosts else neu_isl).append((bc, c))

    analyzed += 1
    island_counts.append(len(islands))
    obj_counts.append(len(obj_isl))
    neutral_counts.append(len(neu_isl))
    if len(islands) <= 4:
        clean += 1
    if neu_isl:
        maps_with_neutral += 1
    team_scale = max((bc for bc, _ in obj_isl), default=max((bc for bc, _, _, _ in islands), default=1)) or 1
    gameplay_here = 0
    for bc, (cx, cz) in neu_isl:
        neutral_total += 1
        r = bc / team_scale
        neutral_size_ratios.append(r)
        size_bucket["stepping-stone (<3%)" if r < 0.03 else "satellite (3-25%)" if r < 0.25 else "substantial (>25%)"] += 1
        abs_bucket["< 64 (decorative?)" if bc < 64 else "64-255" if bc < 256 else "256-1023" if bc < 1024 else ">= 1024"] += 1
        if bc >= GAMEPLAY_MIN:
            gameplay_here += 1
        d = (((cx - mcx) ** 2 + (cz - mcz) ** 2) ** 0.5) / half_diag
        if d < 0.30:
            neutral_central += 1
    gameplay_neutral_per_map.append(gameplay_here)

# ── report ────────────────────────────────────────────────────────────────────
print(f"\nCTW corpus island-structure analysis — N = {analyzed} maps (gamemode=ctw, island geometry + wool objective)\n")

print("ISLAND COUNT (total connected landmasses per map)")
print(f"   median {median(island_counts):.0f}   mean {sum(island_counts)/len(island_counts):.1f}   "
      f"p25 {quantile(island_counts,0.25)}   p75 {quantile(island_counts,0.75)}   max {max(island_counts)}")
print(f"   maps with <= 4 islands (the 'clean archetype' assumption): {clean}/{analyzed}  ({pct(clean,analyzed)})")
hist = Counter()
for c in island_counts:
    hist["1-2" if c <= 2 else "3-4" if c <= 4 else "5-8" if c <= 8 else "9-15" if c <= 15 else "16+"] += 1
for k in ["1-2", "3-4", "5-8", "9-15", "16+"]:
    print(f"      {k:>5} islands : {hist[k]:3d} maps  ({pct(hist[k],analyzed)})")

print("\nOBJECTIVE vs NEUTRAL islands")
print(f"   objective islands/map : median {median(obj_counts):.0f}  (host a wool/monument — what the generator emits)")
print(f"   NEUTRAL islands/map   : median {median(neutral_counts):.0f}  mean {sum(neutral_counts)/len(neutral_counts):.1f}  max {max(neutral_counts)}  (the contested middle the generator omits)")
print(f"   maps with >= 1 neutral island : {maps_with_neutral}/{analyzed}  ({pct(maps_with_neutral,analyzed)})")

print(f"\nNEUTRAL ISLAND SIZE (relative to the team island) — {neutral_total} neutral islands")
for k in ["stepping-stone (<3%)", "satellite (3-25%)", "substantial (>25%)"]:
    print(f"   {k:<22}: {size_bucket[k]:4d}  ({pct(size_bucket[k],neutral_total)})")
print(f"   median size ratio: {median(neutral_size_ratios):.3f}  (neutral block_count / largest team island)")
print(f"   POSITION: {neutral_central}/{neutral_total} neutral islands within 0.30 of map centre  ({pct(neutral_central,neutral_total)}) — a contested centre")
print("   ABSOLUTE size (filters decorative noise):")
for k in ["< 64 (decorative?)", "64-255", "256-1023", ">= 1024"]:
    print(f"      {k:<20}: {abs_bucket[k]:4d}  ({pct(abs_bucket[k],neutral_total)})")
print(f"   GAMEPLAY-sized neutral islands (>= {GAMEPLAY_MIN} blocks) per map: median {median(gameplay_neutral_per_map):.0f}  mean {sum(gameplay_neutral_per_map)/len(gameplay_neutral_per_map):.1f}  "
      f"maps with >= 1: {pct(sum(1 for x in gameplay_neutral_per_map if x),analyzed)}")

print(f"\nHOLES (the diamond primitive) — {islands_total} islands")
print(f"   islands with >= 1 hole : {islands_with_hole}/{islands_total}  ({pct(islands_with_hole,islands_total)})")
print(f"   total holes: {sum(hole_counts)}   max holes on one island: {max(hole_counts)}")

print("\nGENERATOR GAP")
print(f"   generator emits: {median(obj_counts):.0f} objective island(s) (median), 0 neutral.")
print(f"   corpus median:   {median(island_counts):.0f} islands  ->  deficit of ~{median(neutral_counts):.0f} neutral mid pieces (median).\n")
