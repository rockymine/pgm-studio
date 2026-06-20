#!/usr/bin/env python3
"""Void-clearance heatmap for a generated map (gen-map-preview JSON).

Every navigable cell is shaded by its distance to the nearest void/edge (EDT): hot (red) = thin & exposed,
cool (teal) = deep interior with cover. The measured player-flow paths are overlaid, and the tightest pinch
(min clearance, ignoring the trivially-thin spawn/wool tip endpoints) along each route is reported.
Usage: viz_void.py <preview.json> <out.png>
"""
import sys, json, heapq, struct, zlib
import numpy as np
from skimage.draw import polygon as draw_polygon
from skimage.morphology import skeletonize
from scipy.ndimage import distance_transform_edt, label

D = json.load(open(sys.argv[1])); OUT = sys.argv[2]
name = f"{D['archetype']} seed {D['seed']}"
spawns = {s["team"]: (s["x"], s["z"], s.get("protection")) for s in D["spawns"]}
wools = [{"owner": w["owner"], "captor": w["monuments"][0]["team"], "pt": (w["x"], w["z"]), "room": w.get("room")} for w in D["wools"]]
builds = D["build"]["areas"]; islands = D["islands"]

xs = [p[0] for isl in islands for p in isl["exterior"]] + [r["minX"] for r in builds] + [r["maxX"] for r in builds]
zs = [p[1] for isl in islands for p in isl["exterior"]] + [r["minZ"] for r in builds] + [r["maxZ"] for r in builds]
MINX, MINZ = int(min(xs)) - 2, int(min(zs)) - 2
W, H = int(max(xs)) - MINX + 3, int(max(zs)) - MINZ + 3

terrain = np.zeros((H, W), bool)
for isl in islands:
    rr, cc = draw_polygon([int(z - MINZ) for x, z in isl["exterior"]], [int(x - MINX) for x, z in isl["exterior"]], terrain.shape); terrain[rr, cc] = True
    for hole in isl["holes"]:
        rr, cc = draw_polygon([int(z - MINZ) for x, z in hole], [int(x - MINX) for x, z in hole], terrain.shape); terrain[rr, cc] = False
nav = terrain.copy()
for r in builds: nav[max(0, int(r["minZ"]) - MINZ):int(r["maxZ"]) - MINZ + 1, max(0, int(r["minX"]) - MINX):int(r["maxX"]) - MINX + 1] = True
edt = distance_transform_edt(nav)        # clearance: cells to nearest non-navigable

def gxz(p): return int(round(p[0] - MINX)), int(round(p[1] - MINZ))
def snap(p):
    x, z = gxz(p)
    for r in range(12):
        for dz in range(-r, r + 1):
            for dx in range(-r, r + 1):
                if 0 <= z + dz < H and 0 <= x + dx < W and nav[z + dz, x + dx]: return (x + dx, z + dz)
    return None
SQ2 = 2 ** 0.5
NBR = [(1,0,1),(-1,0,1),(0,1,1),(0,-1,1),(1,1,SQ2),(1,-1,SQ2),(-1,1,SQ2),(-1,-1,SQ2)]
def dijkstra(src):
    dist = np.full((H, W), np.inf); pred = {}; pq = [(0.0, src[0], src[1])]; dist[src[1], src[0]] = 0
    while pq:
        d, x, z = heapq.heappop(pq)
        if d > dist[z, x]: continue
        for dx, dz, c in NBR:
            nx, nz = x + dx, z + dz
            if 0 <= nx < W and 0 <= nz < H and nav[nz, nx] and d + c < dist[nz, nx]:
                dist[nz, nx] = d + c; pred[(nx, nz)] = (x, z); heapq.heappush(pq, (d + c, nx, nz))
    return dist, pred
def trace(pred, end):
    p = [end]
    while p[-1] in pred: p.append(pred[p[-1]])
    return p
trees = {t: dijkstra(snap((spawns[t][0], spawns[t][1]))) for t in spawns}
def pathto(t, p):
    s = snap(p); return trace(trees[t][1], s) if s else []

def widest(src):
    """widest-path tree from src: maximise the MIN clearance (EDT) along the route (max-min bottleneck)."""
    best = np.full((H, W), -1.0); pred = {}
    best[src[1], src[0]] = edt[src[1], src[0]]; pq = [(-best[src[1], src[0]], src[0], src[1])]
    while pq:
        nb, x, z = heapq.heappop(pq); b = -nb
        if b < best[z, x]: continue
        for dx, dz, _ in NBR:
            nx, nz = x + dx, z + dz
            if 0 <= nx < W and 0 <= nz < H and nav[nz, nx]:
                cand = min(b, edt[nz, nx])
                if cand > best[nz, nx]:
                    best[nz, nx] = cand; pred[(nx, nz)] = (x, z); heapq.heappush(pq, (-cand, nx, nz))
    return best, pred

# hub = fork of a team's two wool lanes (deepest pixel common to both shortest-path lanes), then SNAPPED to
# the local clearance peak so it lands at the wide centre of the junction rather than the edge-hugging path.
def peak(cx, cz, rad=7):
    best = (cx, cz)
    for dz in range(-rad, rad + 1):
        for dx in range(-rad, rad + 1):
            nx, nz = cx + dx, cz + dz
            if 0 <= nx < W and 0 <= nz < H and nav[nz, nx] and edt[nz, nx] > edt[best[1], best[0]]: best = (nx, nz)
    return best
hubs = {}
for t in spawns:
    own = [pathto(t, w["pt"]) for w in wools if w["owner"] == t]
    if len(own) >= 2:
        common = set(own[0]) & set(own[1]); dist = trees[t][0]
        if common:
            fork = max(common, key=lambda c: dist[c[1], c[0]]); hubs[t] = peak(*fork)

# ---- colour ramp for clearance -------------------------------------------
CEIL = float(sys.argv[3]) if len(sys.argv) > 3 else 8.0   # clearance (blocks) mapped to the top (teal) colour
STOPS = [(0.0,(175,45,45)),(0.18,(215,110,45)),(0.4,(220,200,70)),(0.65,(95,195,95)),(1.0,(60,150,195))]
def clr(c):
    t = min(c / CEIL, 1.0)
    for i in range(len(STOPS) - 1):
        a, ca = STOPS[i]; b, cb = STOPS[i + 1]
        if a <= t <= b:
            f = (t - a) / (b - a); return tuple(int(ca[j] + (cb[j] - ca[j]) * f) for j in range(3))
    return STOPS[-1][1]

IW = 720; IH = int(IW * H / W) + 40; PAD = 22
sc = min((IW - 2 * PAD) / W, (IH - 2 * PAD) / H); ox = (IW - W * sc) / 2; oy = PAD
def tx(gx, gz): return ox + gx * sc, oy + gz * sc
BG = (10, 14, 22)
img = bytearray(BG * (IW * IH))
def bl(x, y, c, a=1.0):
    if 0 <= x < IW and 0 <= y < IH:
        o = (y * IW + x) * 3
        for i in range(3): img[o + i] = int(img[o + i] * (1 - a) + c[i] * a)
def cell(gx, gz, c, a=1.0):
    px, py = tx(gx, gz)
    for dx in range(int(sc) + 1):
        for dy in range(int(sc) + 1): bl(int(px) + dx, int(py) + dy, c, a)
def disc(gx, gz, rad, c, a=1.0):
    px, py = tx(gx + 0.5, gz + 0.5)
    for dy in range(-rad, rad + 1):
        for dx in range(-rad, rad + 1):
            if dx * dx + dy * dy <= rad * rad: bl(int(px) + dx, int(py) + dy, c, a)
def marker(p, c, shape, s=7):
    g = snap(p) or gxz(p); px, py = tx(g[0] + 0.5, g[1] + 0.5)
    pts = [(px + (s if i%2==0 else s*.45)*np.cos(-np.pi/2+i*np.pi/5), py + (s if i%2==0 else s*.45)*np.sin(-np.pi/2+i*np.pi/5)) for i in range(10)] if shape=="star" \
        else [(px, py-s), (px+s, py), (px, py+s), (px-s, py)]
    ys = [q[1] for q in pts]
    for y in range(int(min(ys)), int(max(ys)) + 1):
        xi = []
        for i in range(len(pts)):
            x1, y1 = pts[i]; x2, y2 = pts[(i+1) % len(pts)]
            if (y1 <= y < y2) or (y2 <= y < y1): xi.append(x1 + (y-y1)*(x2-x1)/(y2-y1))
        xi.sort()
        for k in range(0, len(xi) - 1, 2):
            for x in range(int(xi[k]), int(xi[k+1]) + 1): bl(x, y, c, 1.0)
    for i in range(len(pts)):
        x1, y1 = pts[i]; x2, y2 = pts[(i+1) % len(pts)]; n = int(max(abs(x2-x1), abs(y2-y1))) + 1
        for j in range(n + 1): bl(int(x1 + (x2-x1)*j/n), int(y1 + (y2-y1)*j/n), (8,10,16), 1.0)

# clearance heatmap
for gz in range(H):
    for gx in range(W):
        if nav[gz, gx]: cell(gx, gz, clr(edt[gz, gx]))

# player-flow paths, drawn SYMMETRICALLY: the map is mirror_z about cz, but a shortest path is only one of
# many equal-length routes and Dijkstra tie-breaking isn't mirror-invariant, so we compute every route for
# the top team only and reflect each across cz for the bottom team (and reflect the crossing onto itself).
teams = list(spawns)
cz = sum(spawns[t][1] for t in teams) / len(teams); czg = cz - MINZ
def mir(pp): return [(gx, int(round(2 * czg - gz))) for (gx, gz) in pp]
def draw_path(pp):
    for (gx, gz) in pp: disc(gx, gz, 2, (15, 18, 26), 0.55)
    for (gx, gz) in pp: disc(gx, gz, 1, (240, 240, 245), 0.9)
t0 = min(teams, key=lambda t: spawns[t][1])         # top team
routes = [pathto(t0, w["pt"]) for w in wools]        # spawn -> every wool (own + target)
routes.append(pathto(t0, (spawns[[t for t in teams if t != t0][0]][0], cz)))  # crossing, to the mid line
for pp in routes:
    draw_path(pp); draw_path(mir(pp))

# genuine pinch points = the thinnest NAVIGABLE TERRAIN, i.e. the medial axis (skeleton) where clearance is
# locally lowest — the strips squeezing past each diamond hole. Found on the skeleton (so the 1-block outer
# rim isn't flagged), thresholded relative to the typical lane, and away from the trivially-thin objective tips.
skel = skeletonize(nav)
sk_vals = edt[skel]
T = 0.5 * float(np.median(sk_vals))                  # ~half the typical lane half-width
thin = skel & (edt <= T)
objs = [snap((spawns[t][0], spawns[t][1])) for t in teams] + [snap(w["pt"]) for w in wools]
objs = [o for o in objs if o]
lbl, k = label(thin, structure=np.ones((3, 3)))
print(f"\n=== {name}: thinnest terrain (pinch strips), width = 2 x clearance ===")
pinches = []
for i in range(1, k + 1):
    comp = np.argwhere(lbl == i)                      # (z, x)
    if len(comp) < 2: continue
    zc, xc = min(comp, key=lambda c: edt[c[0], c[1]])
    if any((xc - o[0])**2 + (zc - o[1])**2 < 11**2 for o in objs): continue   # skip the dead-end tips
    pinches.append((xc, zc, float(edt[zc, xc])))
for (xc, zc, c) in sorted(pinches, key=lambda p: p[2]):
    disc(xc, zc, 5, (255, 255, 255), 1.0); disc(xc, zc, 3, (180, 30, 30), 1.0)
    print(f"  pinch @({xc+MINX},{zc+MINZ}): width {2*c:.1f}")

for w in wools: marker(w["pt"], {"red":(255,90,90),"blue":(95,120,255)}.get(w["owner"], (220,)*3), "diamond", 6)
for t in teams: marker((spawns[t][0], spawns[t][1]), {"red":(255,90,90),"blue":(95,120,255)}.get(t, (220,)*3), "star", 8)

def png(buf, w, h):
    raw = bytearray()
    for y in range(h): raw.append(0); raw += buf[y*w*3:(y+1)*w*3]
    def ch(tag, d): cc = tag + d; return struct.pack(">I", len(d)) + cc + struct.pack(">I", zlib.crc32(cc) & 0xffffffff)
    return b"\x89PNG\r\n\x1a\n" + ch(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 2, 0, 0, 0)) + ch(b"IDAT", zlib.compress(bytes(raw), 9)) + ch(b"IEND", b"")
open(OUT, "wb").write(png(img, IW, IH))
print(f"  rendered {OUT}  (red=thin/exposed -> teal=deep interior; white dots=routes; red-ringed=thinnest terrain/pinch strips)")
