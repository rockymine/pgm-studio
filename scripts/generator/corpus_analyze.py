#!/usr/bin/env python3
"""Same void-clearance / geodesic / pinch analysis as the generator harness, but on a REAL corpus map.

Terrain comes from the <map>_skel.json export (island rawExterior - rawHoles, plus the build cells); spawns,
wools and spawn protection come from map.xml. Computes and renders, on the navigable grid:
  - per-cell void clearance heatmap (same colour scale as the generator render via the ceiling arg)
  - geodesic spawn<->spawn and spawn->captured-wool distances
  - thinnest terrain (skeleton pinch strips), width = 2 x clearance
  - clearance distribution stats (how open the map is)
Usage: corpus_analyze.py <skel.json> <map.xml> <out.png> [colour_ceiling=16]
"""
import sys, json, heapq, re, struct, zlib, xml.etree.ElementTree as ET
import numpy as np
from skimage.draw import polygon as draw_polygon
from skimage.morphology import skeletonize
from scipy.ndimage import distance_transform_edt, label

skel_json = json.load(open(sys.argv[1])); root = ET.parse(sys.argv[2]).getroot()
OUT = sys.argv[3]; CEIL = float(sys.argv[4]) if len(sys.argv) > 4 else 16.0
name = skel_json["name"]

xs = [p[0] for isl in skel_json["islands"] for p in isl["rawExterior"]] + [c[0] for c in skel_json["buildCells"]]
zs = [p[1] for isl in skel_json["islands"] for p in isl["rawExterior"]] + [c[1] for c in skel_json["buildCells"]]
MINX, MINZ = int(min(xs)) - 30, int(min(zs)) - 30          # pad so protection rects past the terrain still fit
W, H = int(max(xs)) - MINX + 60, int(max(zs)) - MINZ + 60

def v2(s):
    p = [float(x) for x in s.replace(" ", "").split(",")]; return (p[0], p[-1])
byid = {el.get("id"): el for el in root.iter() if el.get("id")}
def fill_box(m, x0, z0, x1, z1):
    a, b = sorted((x0, x1)); c, d = sorted((z0, z1))
    m[max(0, int(c) - MINZ):int(d) - MINZ + 1, max(0, int(a) - MINX):int(b) - MINX + 1] = True
def fill_disk(m, cx, cz, r):
    for z in range(int(cz - r), int(cz + r) + 1):
        for x in range(int(cx - r), int(cx + r) + 1):
            if (x - cx)**2 + (z - cz)**2 <= r*r and 0 <= z - MINZ < H and 0 <= x - MINX < W: m[z - MINZ, x - MINX] = True
def raster(el, m, depth=0):
    if depth > 6 or el is None: return
    t = el.tag
    if t in ("rectangle", "cuboid") and el.get("min") and el.get("max"):
        a = v2(el.get("min")); b = v2(el.get("max")); fill_box(m, a[0], a[1], b[0], b[1])
    elif t == "cylinder" and el.get("base"):
        c = v2(el.get("base")); fill_disk(m, c[0], c[1], float(el.get("radius", "1")))
    elif t in ("block", "point") and el.text and "," in el.text:
        p = v2(el.text); fill_box(m, p[0]-.5, p[1]-.5, p[0]+.5, p[1]+.5)
    elif t in ("union", "intersect", "region"):
        if el.get("id") and t == "region" and el.get("id") in byid: raster(byid[el.get("id")], m, depth+1)
        for ch in el: raster(byid.get(ch.get("id"), ch) if ch.tag == "region" else ch, m, depth+1)
def raster_ref(el, m):
    reg = el.get("region")
    if reg and reg in byid: raster(byid[reg], m)
    for ch in el:
        if ch.tag == "region" or ch.tag in ("rectangle", "cuboid", "cylinder", "block", "point", "union", "intersect"):
            raster(byid.get(ch.get("id"), ch) if ch.tag == "region" and ch.get("id") in byid else ch, m)

# terrain + bridges -> navigable
terrain = np.zeros((H, W), bool)
for isl in skel_json["islands"]:
    rr, cc = draw_polygon([int(z-MINZ) for x, z in isl["rawExterior"]], [int(x-MINX) for x, z in isl["rawExterior"]], terrain.shape); terrain[rr, cc] = True
    for hole in isl["rawHoles"]:
        rr, cc = draw_polygon([int(z-MINZ) for x, z in hole], [int(x-MINX) for x, z in hole], terrain.shape); terrain[rr, cc] = False
nav = terrain.copy()
for c in skel_json["buildCells"]:
    if 0 <= c[1]-MINZ < H and 0 <= c[0]-MINX < W: nav[c[1]-MINZ, c[0]-MINX] = True
edt = distance_transform_edt(nav)

spawns = {}
for s in root.iter("spawn"):
    team = s.get("team")
    if not team: continue
    m = np.zeros((H, W), bool); raster_ref(s, m); yy, xx = np.where(m)
    if len(xx): spawns[team] = (xx.mean()+MINX, yy.mean()+MINZ)
seen = set(); wools = []
for w in root.iter("wool"):
    loc, team = w.get("location"), w.get("team")
    if loc and team:
        x, z = v2(loc); k = (round(x), round(z), team)
        if k not in seen: seen.add(k); wools.append({"captor": team, "pt": (x, z)})

def gxz(p): return int(round(p[0]-MINX)), int(round(p[1]-MINZ))
def snap(p):
    x, z = gxz(p)
    for r in range(20):
        for dz in range(-r, r+1):
            for dx in range(-r, r+1):
                if 0 <= z+dz < H and 0 <= x+dx < W and nav[z+dz, x+dx]: return (x+dx, z+dz)
    return None
SQ2 = 2**0.5
NBR = [(1,0,1),(-1,0,1),(0,1,1),(0,-1,1),(1,1,SQ2),(1,-1,SQ2),(-1,1,SQ2),(-1,-1,SQ2)]
def dijkstra(src):
    dist = np.full((H, W), np.inf); pred = {}; pq = [(0.0, src[0], src[1])]; dist[src[1], src[0]] = 0
    while pq:
        d, x, z = heapq.heappop(pq)
        if d > dist[z, x]: continue
        for dx, dz, c in NBR:
            nx, nz = x+dx, z+dz
            if 0 <= nx < W and 0 <= nz < H and nav[nz, nx] and d+c < dist[nz, nx]:
                dist[nz, nx] = d+c; pred[(nx, nz)] = (x, z); heapq.heappush(pq, (d+c, nx, nz))
    return dist, pred
def trace(pred, end):
    p = [end]
    while p[-1] in pred: p.append(pred[p[-1]])
    return p
def voidc(p):
    s = snap(p); return float(edt[s[1], s[0]]) if s else 0.0

trees = {t: dijkstra(snap(spawns[t])) for t in spawns}
def geo(t, p):
    s = snap(p); d = trees[t][0][s[1], s[0]] if s else np.inf
    return None if not np.isfinite(d) else float(d)
def pathto(t, p):
    s = snap(p); return trace(trees[t][1], s) if s else []

# ---- metrics --------------------------------------------------------------
print(f"\n=== {name}  (grid {W}x{H}, {len(skel_json['islands'])} islands, {len(spawns)} teams, {len(wools)} wools) ===")
navc = edt[nav]; skl = skeletonize(nav); skc = edt[skl]
print(f"  void clearance over playable area: median {np.median(navc):.1f}  p90 {np.percentile(navc,90):.1f}  max {navc.max():.1f}  (blocks to nearest edge)")
print(f"  corridor width along centreline (2x skeleton clearance): median {2*np.median(skc):.1f}  p10 {2*np.percentile(skc,10):.1f}")
print(f"  void clearance at objectives: spawns {[round(voidc(spawns[t]),1) for t in spawns]}  wools median {np.median([voidc(w['pt']) for w in wools]):.1f}")
teams = list(spawns)
print(f"  spawn<->spawn geodesic:")
for i in range(len(teams)):
    for j in range(i+1, len(teams)):
        d = geo(teams[i], spawns[teams[j]])
        if d is not None and d < 400: print(f"    {teams[i]} <-> {teams[j]}: {d:.0f}")
print(f"  spawn -> captured wool geodesic (min/median/max over each team's wools):")
for t in teams:
    ds = [geo(t, w["pt"]) for w in wools if w["captor"] == t]; ds = [d for d in ds if d is not None]
    if ds: print(f"    {t}: {min(ds):.0f} / {np.median(ds):.0f} / {max(ds):.0f}  ({len(ds)} wools)")

# ---- render ---------------------------------------------------------------
CEILv = CEIL
STOPS = [(0.0,(175,45,45)),(0.18,(215,110,45)),(0.4,(220,200,70)),(0.65,(95,195,95)),(1.0,(60,150,195))]
def clr(c):
    t = min(c/CEILv, 1.0)
    for i in range(len(STOPS)-1):
        a, ca = STOPS[i]; b, cb = STOPS[i+1]
        if a <= t <= b:
            f = (t-a)/(b-a); return tuple(int(ca[j]+(cb[j]-ca[j])*f) for j in range(3))
    return STOPS[-1][1]
IW = 760; IH = int(IW*H/W) + 30
if IH > 1100: IH = 1100; IW = int(IH*W/H) + 30
PAD = 18; sc = min((IW-2*PAD)/W, (IH-2*PAD)/H); ox = (IW-W*sc)/2; oy = (IH-H*sc)/2
def tx(gx, gz): return ox+gx*sc, oy+gz*sc
img = bytearray((10,14,22)*(IW*IH))
def bl(x, y, c, a=1.0):
    if 0 <= x < IW and 0 <= y < IH:
        o = (y*IW+x)*3
        for i in range(3): img[o+i] = int(img[o+i]*(1-a)+c[i]*a)
def cell(gx, gz, c, a=1.0):
    px, py = tx(gx, gz)
    for dx in range(int(sc)+1):
        for dy in range(int(sc)+1): bl(int(px)+dx, int(py)+dy, c, a)
def disc(gx, gz, rad, c, a=1.0):
    px, py = tx(gx+.5, gz+.5)
    for dy in range(-rad, rad+1):
        for dx in range(-rad, rad+1):
            if dx*dx+dy*dy <= rad*rad: bl(int(px)+dx, int(py)+dy, c, a)
def marker(p, c, shape, s=6):
    g = snap(p) or gxz(p); px, py = tx(g[0]+.5, g[1]+.5)
    pts = [(px+(s if i%2==0 else s*.45)*np.cos(-np.pi/2+i*np.pi/5), py+(s if i%2==0 else s*.45)*np.sin(-np.pi/2+i*np.pi/5)) for i in range(10)] if shape=="star" else [(px,py-s),(px+s,py),(px,py+s),(px-s,py)]
    ys = [q[1] for q in pts]
    for y in range(int(min(ys)), int(max(ys))+1):
        xi = []
        for i in range(len(pts)):
            x1,y1 = pts[i]; x2,y2 = pts[(i+1)%len(pts)]
            if (y1<=y<y2) or (y2<=y<y1): xi.append(x1+(y-y1)*(x2-x1)/(y2-y1))
        xi.sort()
        for k in range(0, len(xi)-1, 2):
            for x in range(int(xi[k]), int(xi[k+1])+1): bl(x, y, c, 1.0)

for gz in range(H):
    for gx in range(W):
        if nav[gz, gx]: cell(gx, gz, clr(edt[gz, gx]))
# routes: each spawn -> wools it captures
for t in teams:
    for w in wools:
        if w["captor"] == t:
            pp = pathto(t, w["pt"])
            for (gx, gz) in pp: disc(gx, gz, 1, (245,245,248), 0.85)
# thinnest terrain (skeleton pinch strips), away from objective tips
T = 0.5*float(np.median(skc)); thin = skl & (edt <= T)
objs = [snap(spawns[t]) for t in teams] + [snap(w["pt"]) for w in wools]; objs = [o for o in objs if o]
lbl, k = label(thin, structure=np.ones((3,3)))
pin = []
for i in range(1, k+1):
    comp = np.argwhere(lbl == i)
    if len(comp) < 3: continue
    zc, xc = min(comp, key=lambda c: edt[c[0], c[1]])
    if any((xc-o[0])**2+(zc-o[1])**2 < 14**2 for o in objs): continue
    pin.append((xc, zc, float(edt[zc, xc])))
for (xc, zc, c) in pin: disc(xc, zc, 4, (255,255,255), 1.0); disc(xc, zc, 2, (180,30,30), 1.0)
TEAMC = {"red-team":(255,90,90),"blue-team":(95,120,255),"green-team":(90,210,110),"yellow-team":(235,215,70),"red":(255,90,90),"blue":(95,120,255)}
for w in wools: marker(w["pt"], TEAMC.get(w["captor"], (220,)*3), "diamond", 5)
for t in teams: marker(spawns[t], TEAMC.get(t, (220,)*3), "star", 7)

def png(buf, w, h):
    raw = bytearray()
    for y in range(h): raw.append(0); raw += buf[y*w*3:(y+1)*w*3]
    def ch(tag, d): cc = tag+d; return struct.pack(">I", len(d))+cc+struct.pack(">I", zlib.crc32(cc)&0xffffffff)
    return b"\x89PNG\r\n\x1a\n"+ch(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 2, 0, 0, 0))+ch(b"IDAT", zlib.compress(bytes(raw), 9))+ch(b"IEND", b"")
open(OUT, "wb").write(png(img, IW, IH))
print(f"  pinch strips found: {len(pin)} (width <= {2*T:.1f}); rendered {OUT}  [colour ceiling {CEIL} blocks]")
