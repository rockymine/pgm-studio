#!/usr/bin/env python3
"""Wool-approach shape-read investigation lab.

A faithful Python port of PgmStudio.Geom.Cells + PgmStudio.Pgm.Shapes.ShapeClassifier
(Classify / ClassifyOpen) and the WoolBoxEmitter family geometries, for running
shape-read experiments in environments without the .NET SDK. The port is validated
against tools/deriver/shapes/*.plan.json (filename = expected family) and the
emit -> classify mirror before any experiment runs; treat an E1 mismatch as "the
port drifted from the C# classifier — fix before trusting E2+".

Experiments:
  E1  port validation (fixtures + emitter mirror)
  E2  three candidate reads over every wool of tools/seeds/traced/
      (unscoped component Classify / junction-stop lane read / terrain-union-zones)
  E3  fragmentation: promote one emitted slot piece to a build zone, reclassify
  E4  emitter-variant grids (entry shift / wool shift / side-dock) read standalone
      vs with hub terrain attached — the scope dependence of the family read

Findings + interpretation: docs/wool-approach-read-investigation.md.
"""
import json, glob, os
from collections import Counter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

# ---------------- Cells (port of src/PgmStudio.Geom/Cells.cs) ----------------

def n4(c):
    x, z = c
    return ((x+1, z), (x-1, z), (x, z+1), (x, z-1))

def bbox(cells):
    xs = [c[0] for c in cells]; zs = [c[1] for c in cells]
    return min(xs), min(zs), max(xs), max(zs)

def flood(seeds, within):
    comp = set(); q = []
    for s in seeds:
        if s in within and s not in comp:
            comp.add(s); q.append(s)
    while q:
        c = q.pop()
        for n in n4(c):
            if n in within and n not in comp:
                comp.add(n); q.append(n)
    return comp

def components(cells):
    seen = set(); n = 0
    for c in cells:
        if c in seen: continue
        n += 1
        q = [c]; seen.add(c)
        while q:
            d = q.pop()
            for m in n4(d):
                if m in cells and m not in seen:
                    seen.add(m); q.append(m)
    return n

def has_enclosed_void(fill):
    mnx, mnz, mxx, mxz = bbox(fill)
    mnx -= 1; mnz -= 1; mxx += 1; mxz += 1
    outside = {(mnx, mnz)}; q = [(mnx, mnz)]
    while q:
        c = q.pop()
        for nb in n4(c):
            if mnx <= nb[0] <= mxx and mnz <= nb[1] <= mxz and nb not in fill and nb not in outside:
                outside.add(nb); q.append(nb)
    for x in range(mnx, mxx+1):
        for z in range(mnz, mxz+1):
            if (x, z) not in fill and (x, z) not in outside:
                return True
    return False

def reflex_corners(cells):
    mnx, mnz, mxx, mxz = bbox(cells)
    r = 0
    for x in range(mnx, mxx+2):
        for z in range(mnz, mxz+2):
            cnt = sum(1 for p in ((x, z), (x-1, z), (x, z-1), (x-1, z-1)) if p in cells)
            if cnt == 3: r += 1
    return r

def has_bay(cells):
    mnx, mnz, mxx, mxz = bbox(cells)
    seen = set()
    for x in range(mnx, mxx+1):
        for z in range(mnz, mxz+1):
            if (x, z) in cells or (x, z) in seen: continue
            q = [(x, z)]; seen.add((x, z)); region = []
            while q:
                c = q.pop(); region.append(c)
                for nb in n4(c):
                    if mnx <= nb[0] <= mxx and mnz <= nb[1] <= mxz and nb not in cells and nb not in seen:
                        seen.add(nb); q.append(nb)
            mask = 0
            for c in region:
                if c[0] == mnx: mask |= 1
                if c[0] == mxx: mask |= 2
                if c[1] == mnz: mask |= 4
                if c[1] == mxz: mask |= 8
            if bin(mask).count('1') == 1: return True
    return False

def min_run_width(cells, seeds):
    def hrun(c):
        n = 1; x = c[0]-1
        while (x, c[1]) in cells: n += 1; x -= 1
        x = c[0]+1
        while (x, c[1]) in cells: n += 1; x += 1
        return n
    def vrun(c):
        n = 1; z = c[1]-1
        while (c[0], z) in cells: n += 1; z -= 1
        z = c[1]+1
        while (c[0], z) in cells: n += 1; z += 1
        return n
    m = min(min(hrun(c), vrun(c)) for c in seeds)
    return max(2, min(6, m))

# ------------- ShapeClassifier (port of Shapes/ShapeClassifier.cs) -------------

def classify(filled, terminal):
    comp = flood(terminal, filled)
    terr = comp - set(terminal)
    if not any(any(nb in terminal for nb in n4(c)) for c in terr):
        return ("Isolated", 0)
    seeds = []
    for r in terminal:
        for nb in n4(r):
            if nb in terr: seeds.append(nb)
    width = min_run_width(terr, seeds)
    if has_enclosed_void(comp):
        return ("Donut", width)
    rminx = min(c[0] for c in terminal); rmaxx = max(c[0] for c in terminal)
    rminz = min(c[1] for c in terminal); rmaxz = max(c[1] for c in terminal)
    def side(x0, x1, z0, z1):
        return any((x, z) in terr for x in range(x0, x1+1) for z in range(z0, z1+1))
    top = side(rminx, rmaxx, rminz-1, rminz-1); bot = side(rminx, rmaxx, rmaxz+1, rmaxz+1)
    left = side(rminx-1, rminx-1, rminz, rmaxz); right = side(rmaxx+1, rmaxx+1, rminz, rmaxz)
    if ((top and bot) or (left and right)) and components(terr) >= 2:
        return ("Clamp", width)
    bends = reflex_corners(terr)
    if bends == 0: return ("I", width)
    if bends == 1: return ("L", width)
    if parallel_arms(comp, terr, terminal):
        return ("U" if flush_on_bar(terr, rminx, rmaxx, rminz, rmaxz) else "H", width)
    return ("Scythe" if has_bay(comp) else "Z", width)

def flush_on_bar(terr, rminx, rmaxx, rminz, rmaxz):
    def col_has(x): return any((x, z) in terr for z in range(rminz, rmaxz+1))
    def row_has(z): return any((x, z) in terr for x in range(rminx, rmaxx+1))
    if col_has(rminx-1) and ((rminx-1, rminz-1) in terr or (rminx-1, rmaxz+1) in terr): return True
    if col_has(rmaxx+1) and ((rmaxx+1, rminz-1) in terr or (rmaxx+1, rmaxz+1) in terr): return True
    if row_has(rminz-1) and ((rminx-1, rminz-1) in terr or (rmaxx+1, rminz-1) in terr): return True
    if row_has(rmaxz+1) and ((rminx-1, rmaxz+1) in terr or (rmaxx+1, rmaxz+1) in terr): return True
    return False

def parallel_arms(comp, terr, terminal):
    mnx, mnz, mxx, mxz = bbox(comp)
    def two_runs(line):
        cells = list(line)
        if any(c in terminal for c in cells): return False
        runs = 0; in_run = False
        for c in cells:
            if c in terr:
                if not in_run: runs += 1; in_run = True
            else:
                in_run = False
        return runs >= 2
    north = [(x, mnz) for x in range(mnx, mxx+1)]
    south = [(x, mxz) for x in range(mnx, mxx+1)]
    west = [(mnx, z) for z in range(mnz, mxz+1)]
    east = [(mxx, z) for z in range(mnz, mxz+1)]
    return two_runs(north) or two_runs(south) or two_runs(west) or two_runs(east)

def classify_open(filled, terminal):
    """Returns ((read, width), lane_cells); lane_cells is empty for none/plaza."""
    seeds = []
    for r in terminal:
        for nb in n4(r):
            if nb in filled and nb not in terminal: seeds.append(nb)
    if not seeds: return ("none", 0), set()
    w = min_run_width(filled, seeds); k = w + 1
    def blk(o):
        for dx in range(k):
            for dz in range(k):
                if (o[0]+dx, o[1]+dz) not in filled: return False
        return True
    def thick(c):
        for x in range(c[0]-k+1, c[0]+1):
            for z in range(c[1]-k+1, c[1]+1):
                if blk((x, z)): return True
        return False
    def narrow(c):
        return c in filled and c not in terminal and not thick(c)
    lane = set(); q = []
    for s in seeds:
        if narrow(s) and s not in lane:
            lane.add(s); q.append(s)
    while q:
        cur = q.pop()
        for nb in n4(cur):
            if narrow(nb) and nb not in lane:
                lane.add(nb); q.append(nb)
    if not lane: return ("plaza", w), set()
    reflex = reflex_corners(lane)
    return ({0: "I", 1: "L", 2: "Z"}.get(reflex, "complex"), w), lane

# ------------- emitter geometries (port of Compose/WoolBoxEmitter.cs) -------------

ROOM_DEPTH = 2

def emit(family, W, H, cw=2):
    """Base-configuration emission (no flip / side-tuck / extra knobs).
    Returns (terrain [(rect, slot)], room rect); rects are [x, z, w, h] box-local."""
    t = []; room = None
    if family == "I":
        lx = (W - cw)//2; laneH = H - ROOM_DEPTH
        t.append(([lx, 0, cw, laneH], "entry"))
        room = [lx, laneH, cw, ROOM_DEPTH]
    elif family == "L":
        bandZ = H - cw
        t.append(([0, 0, cw, bandZ], "entry"))
        t.append(([0, bandZ, W - ROOM_DEPTH, cw], "run"))
        room = [W - ROOM_DEPTH, bandZ, ROOM_DEPTH, cw]
    elif family == "Z":
        z1 = (H - ROOM_DEPTH - cw)//2
        botZ = z1 + cw; botLen = H - ROOM_DEPTH - botZ
        t.append(([0, 0, cw, z1], "entry"))
        t.append(([0, z1, W, cw], "bar"))
        t.append(([W - cw, botZ, cw, botLen], "room-run"))
        room = [W - cw, H - ROOM_DEPTH, cw, ROOM_DEPTH]
    elif family == "Scythe":
        botZ = H - cw
        t.append(([0, 0, cw, cw], "entry"))
        t.append(([cw, 0, cw, botZ], "entry-run"))
        t.append(([cw, botZ, 3*cw, cw], "bar"))
        t.append(([3*cw, ROOM_DEPTH, cw, botZ - ROOM_DEPTH], "room-run"))
        room = [3*cw, 0, cw, ROOM_DEPTH]
    elif family == "Clamp":
        barLen = 2*cw
        t.append(([0, 0, barLen, cw], "entry"))
        t.append(([0, H - cw, barLen, cw], "entry"))
        room = [barLen - cw, cw, cw, H - 2*cw]
    elif family == "U":
        barZ = ROOM_DEPTH
        wx = (W - cw)//2
        t.append(([0, barZ, W, cw], "bar"))
        t.append(([0, barZ + cw, cw, H - barZ - cw], "entry"))
        t.append(([W - cw, barZ + cw, cw, H - barZ - cw], "entry"))
        room = [wx, 0, cw, ROOM_DEPTH]
    elif family == "H":
        barZ = 2*ROOM_DEPTH
        wx = (W - cw)//2
        t.append(([0, barZ, W, cw], "bar"))
        t.append(([0, barZ + cw, cw, H - barZ - cw], "entry"))
        t.append(([W - cw, barZ + cw, cw, H - barZ - cw], "entry"))
        t.append(([wx, ROOM_DEPTH, cw, ROOM_DEPTH], "room-run"))
        room = [wx, 0, cw, ROOM_DEPTH]
    elif family == "Donut":
        aw = cw
        ax = cw; ringH = H; span = 3*cw
        t.append(([ax, 0, span, cw], "entry-bar"))
        t.append(([ax, cw, cw, ringH - 2*cw], "leg"))
        t.append(([ax + 2*cw, cw, cw, ringH - 2*cw], "leg"))
        t.append(([0, 0, cw, aw], "entry"))
        t.append(([ax, ringH - cw, span, cw], "room-bar"))
        room = [ax + span, ringH - cw, ROOM_DEPTH, cw]
    else:
        raise ValueError(family)
    return t, room

def rect_cells(r):
    return {(x, z) for x in range(r[0], r[0]+r[2]) for z in range(r[1], r[1]+r[3])}

EMIT_BOX = {  # near-minimal boxes per family at cw=2
    "I": (4, 8), "L": (7, 8), "Z": (6, 10), "Scythe": (8, 8),
    "Clamp": (4, 6), "U": (6, 7), "H": (6, 9), "Donut": (11, 8),
}

# ---------------- plan loading ----------------

def load_plan(path):
    d = json.load(open(path))
    terrain = {}   # id -> cell set (generating roles only)
    roles = {}
    for p in d.get("pieces", []):
        role = p.get("role", "piece")
        roles[p["id"]] = role
        if role in ("buffer", "connector"): continue
        terrain[p["id"]] = rect_cells(p["rect"])
    zones = set()
    for z in d.get("zones", []):
        zc = rect_cells(z["rect"])
        for h in z.get("holes", []):
            zc -= rect_cells(h)
        zones |= zc
    wools = [w["piece"] for w in d.get("placements", {}).get("wools", [])]
    return d, terrain, zones, wools, roles

# -------- variant grids (entry shift / wool shift / side-dock, ± hub context) --------

def grid_cells(rows, scale=2):
    """Character grid -> cell sets. t terrain, w wool room, h hub-context terrain,
    anything else empty. Scaled uniformly (the family read is width-independent)."""
    filled = set(); term = set(); hub = set()
    for z, row in enumerate(rows):
        for x, ch in enumerate(row):
            if ch in "twh":
                for dx in range(scale):
                    for dz in range(scale):
                        c = (x*scale+dx, z*scale+dz)
                        filled.add(c)
                        if ch == "w": term.add(c)
                        if ch == "h": hub.add(c)
    return filled, term, hub

VARIANT_CASES = [
    ("scythe standard",            ["ttbw", "btbt", "bttt"], "Scythe"),
    ("scythe shifted entry",       ["bbbw", "ttbt", "bttt"], "Scythe"),
    ("scythe shifted entry + hub", ["hbbbw", "httbt", "hbttt", "hbbbb"], "Scythe"),
    ("scythe shifted wool",        ["ttbb", "btbw", "btbt", "bttt"], "Scythe"),
    ("scythe shifted wool + hub",  ["httbb", "hbtbw", "hbtbt", "hbttt", "hbbbb"], "Scythe"),
    ("scythe side-dock",           ["ttbb", "btbtw", "bttt"], "Scythe"),
    ("scythe side-dock + hub",     ["httbbb", "hbtbtw", "hbtttb", "hbbbbb"], "Scythe"),
    ("donut standard",             ["ttttb", "btvtb", "btttw"], "Donut"),
    ("donut moved attachment",     ["btttb", "ttvtb", "btttw"], "Donut"),
    ("Z extend",                   ["ttbbb", "btttw"], "Z"),
    ("Z side-dock-up",             ["ttbb", "btbw", "bttt"], "Z"),
    ("Z side-dock-down",           ["ttbb", "bttt", "bbbw"], "Z"),
]

# ---------------- experiments ----------------

def e1_validate(verbose=True):
    expected = {
        "clamp": "Clamp", "donut": "Donut", "h-stub": "H", "i-sidetuck": "I",
        "i-straight": "I", "isolated": "Isolated", "l-corner": "L",
        "scythe": "Scythe", "u-flush": "U",
    }
    bad = 0
    if verbose: print("== E1: port validation ==")
    for path in sorted(glob.glob(f"{ROOT}/tools/deriver/shapes/*.plan.json")):
        name = os.path.basename(path).replace(".plan.json", "")
        want = expected[next(k for k in expected if name.startswith(k))]
        d, terrain, zones, wools, roles = load_plan(path)
        fam, w = classify(set().union(*terrain.values()), terrain[wools[0]])
        if fam != want:
            bad += 1
            print(f"  FIXTURE MISMATCH {name}: want {want} got {fam}")
    for fam, (W, H) in EMIT_BOX.items():
        t, room = emit(fam, W, H)
        filled = set().union(*[rect_cells(r) for r, _ in t]) | rect_cells(room)
        got, _ = classify(filled, rect_cells(room))
        if got != fam:
            bad += 1
            print(f"  MIRROR MISMATCH emit {fam} -> classify {got}")
    if verbose: print(f"  fixtures+mirror mismatches: {bad}")
    return bad == 0

def e2_traced():
    print("\n== E2: the three reads over the traced corpus ==")
    print(f"  {'map':22} {'wool':10} {'terrain':9} {'lane':9} {'terr+zones':10} scope")
    rows = []
    for path in sorted(glob.glob(f"{ROOT}/tools/seeds/traced/*.plan.json")):
        name = os.path.basename(path).replace(".plan.json", "")
        d, terrain, zones, wools, roles = load_plan(path)
        filled = set().union(*terrain.values())
        for wid in wools:
            if wid not in terrain: continue
            term = terrain[wid]
            comp = flood(term, filled)
            famT, _ = classify(filled, term)
            (laneRead, _), laneCells = classify_open(filled, term)
            famZ, _ = classify(filled | zones, term)
            scope = "whole-unit" if comp == filled else "island-fragment"
            rows.append(dict(map=name, wool=wid, terr=famT, lane=laneRead, zones=famZ, scope=scope))
            print(f"  {name:22} {wid:10} {famT:9} {laneRead:9} {famZ:10} {scope}")
    for k, label in (("terr", "terrain reads"), ("lane", "lane reads"), ("zones", "terr+zones reads")):
        print(f"  {label:18}: {dict(Counter(r[k] for r in rows))}")
    print(f"  scope             : {dict(Counter(r['scope'] for r in rows))}")
    return rows

def e3_fragmentation():
    print("\n== E3: one slot piece promoted to build, reclassified ==")
    print(f"  {'family':8} {'promoted':10} {'terrain-only':13} terrain+cut")
    for fam in ("L", "Z", "Scythe", "U", "Donut"):
        W, H = EMIT_BOX[fam]
        t, room = emit(fam, W, H)
        roomC = rect_cells(room)
        for r, slot in t:
            if slot == "entry" and fam != "U":
                continue
            terr = set().union(*[rect_cells(rr) for rr, _ in t if rr is not r]) | roomC
            famT, _ = classify(terr, roomC)
            famZ, _ = classify(terr | rect_cells(r), roomC)
            print(f"  {fam:8} {slot:10} {famT:13} {famZ}")

def e4_variants():
    print("\n== E4: variant grids, standalone vs hub-attached ==")
    for name, rows, want in VARIANT_CASES:
        filled, term, hub = grid_cells(rows)
        fam, _ = classify(filled, term)
        mark = "ok" if fam == want else "DIFFERS"
        print(f"  {name:30} want {want:7} got {fam:7} {mark}")

if __name__ == "__main__":
    if e1_validate():
        e2_traced()
        e3_fragmentation()
        e4_variants()
    else:
        print("port drifted from the C# classifier — experiments not run")
