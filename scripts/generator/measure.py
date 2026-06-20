#!/usr/bin/env python3
"""Geometric/topological parameters for a generated map (gen-map-preview JSON).

Distances are measured on the NAVIGABLE grid = island terrain (minus holes) UNION the build/bridge
rectangles, at 1-block resolution. Geodesic distance = shortest walking path (8-connected Dijkstra, step
costs 1 / sqrt2), so it follows lanes and crosses voids only via bridges. Reports, per map:
  - spawn -> spawn   (geodesic over skeleton+build)
  - spawn -> own wool / enemy (target) wool   (geodesic)
  - spawn / wool -> void   (straight-line clearance to the nearest non-navigable cell)
  - spawn / wool -> nearest hub   (geodesic to the nearest skeleton junction)
"""
import sys, json, heapq
import numpy as np
from skimage.draw import polygon as draw_polygon
from skimage.morphology import skeletonize
from scipy.ndimage import distance_transform_edt, label

D = json.load(open(sys.argv[1]))
name = f"{D['archetype']} seed {D['seed']}"
spawns = {s["team"]: (s["x"], s["z"]) for s in D["spawns"]}
# wool[owner] sits on owner's side and is captured by monuments[0].team (the attacker)
wools = [{"owner": w["owner"], "captor": w["monuments"][0]["team"], "pt": (w["x"], w["z"])} for w in D["wools"]]
builds = D["build"]["areas"]
islands = D["islands"]

xs = [p[0] for isl in islands for p in isl["exterior"]] + [r["minX"] for r in builds] + [r["maxX"] for r in builds]
zs = [p[1] for isl in islands for p in isl["exterior"]] + [r["minZ"] for r in builds] + [r["maxZ"] for r in builds]
MINX, MINZ = int(min(xs)) - 2, int(min(zs)) - 2
W, H = int(max(xs)) - MINX + 3, int(max(zs)) - MINZ + 3

terrain = np.zeros((H, W), bool)
for isl in islands:
    rr, cc = draw_polygon([int(z - MINZ) for x, z in isl["exterior"]], [int(x - MINX) for x, z in isl["exterior"]], terrain.shape)
    terrain[rr, cc] = True
    for hole in isl["holes"]:
        rr, cc = draw_polygon([int(z - MINZ) for x, z in hole], [int(x - MINX) for x, z in hole], terrain.shape)
        terrain[rr, cc] = False
nav = terrain.copy()
for r in builds:
    nav[max(0, int(r["minZ"]) - MINZ):int(r["maxZ"]) - MINZ + 1, max(0, int(r["minX"]) - MINX):int(r["maxX"]) - MINX + 1] = True

def gxz(p): return int(round(p[0] - MINX)), int(round(p[1] - MINZ))
def snap(p):
    x, z = gxz(p)
    for r in range(12):
        for dz in range(-r, r + 1):
            for dx in range(-r, r + 1):
                if 0 <= z + dz < H and 0 <= x + dx < W and nav[z + dz, x + dx]:
                    return (x + dx, z + dz)
    return None

SQ2 = 2 ** 0.5
NBR = [(1,0,1),(-1,0,1),(0,1,1),(0,-1,1),(1,1,SQ2),(1,-1,SQ2),(-1,1,SQ2),(-1,-1,SQ2)]
def dijkstra(sources, want_pred=False):
    dist = np.full((H, W), np.inf); pred = {}
    pq = []
    for s in sources:
        if s is not None:
            dist[s[1], s[0]] = 0; heapq.heappush(pq, (0.0, s[0], s[1]))
    while pq:
        d, x, z = heapq.heappop(pq)
        if d > dist[z, x]: continue
        for dx, dz, c in NBR:
            nx, nz = x + dx, z + dz
            if 0 <= nx < W and 0 <= nz < H and nav[nz, nx]:
                nd = d + c
                if nd < dist[nz, nx]:
                    dist[nz, nx] = nd
                    if want_pred: pred[(nx, nz)] = (x, z)
                    heapq.heappush(pq, (nd, nx, nz))
    return (dist, pred) if want_pred else dist
def geo(a, b):
    sa, sb = snap(a), snap(b)
    if sa is None or sb is None: return None
    return dijkstra([sa])[sb[1], sb[0]]
def path(pred, end):
    p = [end]
    while p[-1] in pred: p.append(pred[p[-1]])
    return p

# distance to void: nearest non-navigable cell (straight-line, blocks)
edt = distance_transform_edt(nav)
def void(p):
    s = snap(p); return None if s is None else float(edt[s[1], s[0]])

# primary hub per team = the FORK of that team's two wool lanes. Build the shortest-path tree rooted at the
# spawn; trace it to each own wool; the deepest pixel common to ALL those paths is where the lanes diverge =
# the hub. No fragile junction detection — robust to ribbon jitter, hole loops and 4-way skeleton splits.
primary, prim_dist = {}, {}
for t in spawns:
    s = snap(spawns[t])
    own = [w for w in wools if w["owner"] == t]
    if s is None or not own: continue
    dist, pred = dijkstra([s], want_pred=True)
    paths = []
    for w in own:
        sw = snap(w["pt"])
        if sw is not None: paths.append(path(pred, sw))      # wool -> ... -> spawn
    if not paths: continue
    common = set(paths[0])
    for pp in paths[1:]: common &= set(pp)
    fork = max(common, key=lambda c: dist[c[1], c[0]]) if common else s   # deepest shared pixel
    primary[t] = fork
    prim_dist[t] = dijkstra([fork])
def hub_geo(p, t):
    """geodesic distance from point p to team t's hub (spawn->hub = spur, wool->hub = lane length)."""
    s = snap(p)
    if s is None or t not in prim_dist: return None
    return float(prim_dist[t][s[1], s[0]])

print(f"\n=== {name} ===")
print(f"  grid {W}x{H}  islands={len(islands)}  bridges={len(builds)}")

teams = list(spawns)
print(f"\n  spawn <-> spawn (geodesic over terrain+bridge):")
for i in range(len(teams)):
    for j in range(i + 1, len(teams)):
        print(f"    {teams[i]} -> {teams[j]}: {geo(spawns[teams[i]], spawns[teams[j]]):.1f} blocks")

print(f"\n  spawn -> wool (geodesic):")
for t in teams:
    own = [w for w in wools if w["owner"] == t]
    tgt = [w for w in wools if w["captor"] == t]
    for w in own:
        print(f"    {t} -> OWN  wool@({w['pt'][0]:.0f},{w['pt'][1]:.0f}): {geo(spawns[t], w['pt']):.1f}")
    for w in tgt:
        print(f"    {t} -> ENEMY wool@({w['pt'][0]:.0f},{w['pt'][1]:.0f}): {geo(spawns[t], w['pt']):.1f}")

print(f"\n  hubs (fork of each team's wool lanes): " +
      ", ".join(f"{t}@({primary[t][0]+MINX},{primary[t][1]+MINZ})" for t in primary))
print(f"  clearance to void (straight-line) & geodesic to own primary hub (= spur/lane length):")
for t in teams:
    hd = hub_geo(spawns[t], t)
    print(f"    spawn {t:5}            : void={void(spawns[t]):.1f}  spur-to-hub={hd:.1f}")
for w in wools:
    o = w["owner"]; hd = hub_geo(w["pt"], o)
    print(f"    wool {o:5}-owned @({w['pt'][0]:.0f},{w['pt'][1]:.0f}): void={void(w['pt']):.1f}  lane-to-hub={hd:.1f}")
