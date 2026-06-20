#!/usr/bin/env python3
"""Render the measured geodesic paths over a generated map (gen-map-preview JSON).

Reconstructs, on the navigable grid (terrain UNION bridge), the shortest walking paths that the parameters
were measured from: spawn<->spawn, each spawn->own wool, each spawn->enemy(target) wool, and marks the hub
(fork of a team's wool lanes). Usage: viz_paths.py <preview.json> <out.png>
"""
import sys, json, heapq, struct, zlib
import numpy as np
from skimage.draw import polygon as draw_polygon

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

# hub = fork of a team's two own wool lanes (deepest pixel common to both lane paths)
hubs = {}
for t in spawns:
    own = [pathto(t, w["pt"]) for w in wools if w["owner"] == t]
    if len(own) >= 2:
        common = set(own[0]) & set(own[1]); dist = trees[t][0]
        if common: hubs[t] = max(common, key=lambda c: dist[c[1], c[0]])

# ---- raster ---------------------------------------------------------------
IW, IH = 720, int(720 * H / W) + 60; PAD = 24
sc = min((IW - 2 * PAD) / W, (IH - 2 * PAD) / H); ox = (IW - W * sc) / 2; oy = PAD
def tx(gx, gz): return ox + gx * sc, oy + gz * sc
BG = (12, 17, 26); LAND = (88, 99, 116); BR = (210, 140, 60); PROT = (200, 70, 70); ROOM = (70, 110, 210)
img = bytearray(BG * (IW * IH))
def bl(x, y, c, a=1.0):
    if 0 <= x < IW and 0 <= y < IH:
        o = (y * IW + x) * 3
        for i in range(3): img[o + i] = int(img[o + i] * (1 - a) + c[i] * a)
def cell(gx, gz, c, a):
    px, py = tx(gx, gz)
    for dx in range(int(sc) + 1):
        for dy in range(int(sc) + 1): bl(int(px) + dx, int(py) + dy, c, a)
def disc(gx, gz, rad, c, a=1.0):
    px, py = tx(gx + 0.5, gz + 0.5)
    for dy in range(-rad, rad + 1):
        for dx in range(-rad, rad + 1):
            if dx * dx + dy * dy <= rad * rad: bl(int(px) + dx, int(py) + dy, c, a)
def draw_path(pp, c, rad=2):
    for (gx, gz) in pp: disc(gx, gz, rad, c, 1.0)
def box(r, c, a):
    for gz in range(max(0, int(r["minZ"]) - MINZ), int(r["maxZ"]) - MINZ + 1):
        for gx in range(max(0, int(r["minX"]) - MINX), int(r["maxX"]) - MINX + 1): cell(gx, gz, c, a)
def marker(p, c, shape, s=7):
    gx, gz = snap(p) if snap(p) else gxz(p); px, py = tx(gx + 0.5, gz + 0.5)
    pts = [(px + (s if i % 2 == 0 else s * .45) * np.cos(-np.pi/2 + i*np.pi/5), py + (s if i % 2 == 0 else s*.45)*np.sin(-np.pi/2 + i*np.pi/5)) for i in range(10)] if shape == "star" \
        else [(px, py - s), (px + s, py), (px, py + s), (px - s, py)]
    ys = [q[1] for q in pts]
    for y in range(int(min(ys)), int(max(ys)) + 1):
        xint = []
        for i in range(len(pts)):
            x1, y1 = pts[i]; x2, y2 = pts[(i + 1) % len(pts)]
            if (y1 <= y < y2) or (y2 <= y < y1): xint.append(x1 + (y - y1) * (x2 - x1) / (y2 - y1))
        xint.sort()
        for k in range(0, len(xint) - 1, 2):
            for x in range(int(xint[k]), int(xint[k + 1]) + 1): bl(x, y, c, 1.0)
    # outline
    for i in range(len(pts)):
        x1, y1 = pts[i]; x2, y2 = pts[(i + 1) % len(pts)]; n = int(max(abs(x2-x1), abs(y2-y1))) + 1
        for j in range(n + 1): bl(int(x1 + (x2-x1)*j/n), int(y1 + (y2-y1)*j/n), (10,12,18), 1.0)

# terrain, bridge, regions
for gz in range(H):
    for gx in range(W):
        if nav[gz, gx]: cell(gx, gz, LAND, 0.95)
for r in builds: box(r, BR, 0.40)
for t in spawns:
    if spawns[t][2]: box(spawns[t][2], PROT, 0.30)
for w in wools:
    if w["room"]: box(w["room"], ROOM, 0.28)

# paths — computed for the TOP team and mirrored across the mid line, since a shortest path isn't unique and
# Dijkstra tie-breaking isn't mirror-symmetric (the map is). This keeps the picture as symmetric as the map.
SS = (245, 245, 245)        # spawn<->spawn (the contested crossing)
OWN_S, OWN_L = (90, 210, 110), (240, 210, 70)   # own wool: short lane green, long lane yellow
ENEMY = (200, 90, 210)      # spawn -> enemy/target wool
TEAMC = {"red": (255, 90, 90), "blue": (95, 120, 255)}
teams = list(spawns)
cz = sum(spawns[t][1] for t in teams) / len(teams); czg = cz - MINZ
def mir(pp): return [(gx, int(round(2 * czg - gz))) for (gx, gz) in pp]
def draw_sym(pp, c, rad=2): draw_path(pp, c, rad); draw_path(mir(pp), c, rad)
t0 = min(teams, key=lambda t: spawns[t][1]); t1 = [t for t in teams if t != t0][0]
# enemy/target wool (faint, underneath), then spawn<->spawn, then own wools (longer = yellow)
for w in wools:
    if w["captor"] == t0: draw_sym(pathto(t0, w["pt"]), ENEMY, 1)
draw_sym(pathto(t0, (spawns[t1][0], cz)), SS, 2)
own = sorted([w for w in wools if w["owner"] == t0], key=lambda w: trees[t0][0][snap(w["pt"])[1], snap(w["pt"])[0]])
for idx, w in enumerate(own):
    draw_sym(pathto(t0, w["pt"]), OWN_L if idx == len(own) - 1 else OWN_S, 2)

# markers + hubs
for t in hubs: disc(hubs[t][0], hubs[t][1], 5, (255, 255, 255), 1.0); disc(hubs[t][0], hubs[t][1], 3, (20, 20, 20), 1.0)
for w in wools: marker(w["pt"], TEAMC.get(w["owner"], (220,)*3), "diamond", 6)
for t in teams: marker((spawns[t][0], spawns[t][1]), TEAMC.get(t, (220,)*3), "star", 8)

def png(buf, w, h):
    raw = bytearray()
    for y in range(h): raw.append(0); raw += buf[y*w*3:(y+1)*w*3]
    def ch(tag, d): cc = tag + d; return struct.pack(">I", len(d)) + cc + struct.pack(">I", zlib.crc32(cc) & 0xffffffff)
    return b"\x89PNG\r\n\x1a\n" + ch(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 2, 0, 0, 0)) + ch(b"IDAT", zlib.compress(bytes(raw), 9)) + ch(b"IEND", b"")
open(OUT, "wb").write(png(img, IW, IH))
print(f"{name}: rendered {OUT}")
print("  white line=spawn<->spawn  green=spawn->own short wool  yellow=spawn->own LONG wool  magenta=spawn->enemy wool  white dot=hub(fork)")
