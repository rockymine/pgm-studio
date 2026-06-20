#!/usr/bin/env python3
"""Render the RAW sketch polygons (the base model) for a generated layout, before rasterization.

Reads a --gen-sketch dump ({setup:{mirror_mode,center}, layout:{shapes,islands}}). Each shape is a polygon
with explicit vertices and an add/subtract role; the island mirrors across the centre (mirror_z reflects z
about cz). Draws team 0's polygons (warm) and their mirror images (cool): add = filled+outlined with vertex
dots, subtract (diamond holes) = red cut outline. Usage: render_sketch.py <sketch.json> <out.png>
"""
import sys, json, struct, zlib
D = json.load(open(sys.argv[1])); OUT = sys.argv[2]
cz = D["setup"]["center"]["cz"]; mirror = D["setup"]["mirror_mode"]
shapes = D["layout"]["shapes"]
mirror_ids = {sid for isl in D["layout"]["islands"] if isl["mirrors"] for sid in isl["shapeIds"]}

def verts(s): return [(v[0], v[1]) for v in (s.get("vertices") or [])]
def mir(p): return (p[0], 2 * cz - p[1]) if mirror == "mirror_z" else (2 * D["setup"]["center"]["cx"] - p[0], p[1])

# build the two teams' shape instances: (vertices, operation, team)
insts = []
for s in shapes:
    vs = verts(s)
    if not vs: continue
    insts.append((vs, s["operation"], 0))
    if s["id"] in mirror_ids: insts.append(([mir(p) for p in vs], s["operation"], 1))

allpts = [p for vs, _, _ in insts for p in vs]
minx = min(p[0] for p in allpts); maxx = max(p[0] for p in allpts)
minz = min(p[1] for p in allpts); maxz = max(p[1] for p in allpts)
W, Hh = maxx - minx, maxz - minz
IW = 720; IH = int(IW * Hh / W) + 40; PAD = 28
sc = min((IW - 2 * PAD) / W, (IH - 2 * PAD) / Hh); ox = (IW - W * sc) / 2; oy = (IH - Hh * sc) / 2
def tx(x, z): return ox + (x - minx) * sc, oy + (z - minz) * sc

img = bytearray((12, 16, 24) * (IW * IH))
def bl(x, y, c, a=1.0):
    if 0 <= x < IW and 0 <= y < IH:
        o = (y * IW + x) * 3
        for i in range(3): img[o + i] = int(img[o + i] * (1 - a) + c[i] * a)
def fill(poly, c, a):
    pts = [tx(*p) for p in poly]; ys = [p[1] for p in pts]
    for y in range(int(min(ys)), int(max(ys)) + 1):
        xi = []
        for i in range(len(pts)):
            x1, y1 = pts[i]; x2, y2 = pts[(i + 1) % len(pts)]
            if (y1 <= y < y2) or (y2 <= y < y1): xi.append(x1 + (y - y1) * (x2 - x1) / (y2 - y1))
        xi.sort()
        for k in range(0, len(xi) - 1, 2):
            for x in range(int(xi[k]), int(xi[k + 1]) + 1): bl(x, y, c, a)
def line(p1, p2, c, w=1):
    x1, y1 = tx(*p1); x2, y2 = tx(*p2); n = int(max(abs(x2 - x1), abs(y2 - y1))) + 1
    for j in range(n + 1):
        x = x1 + (x2 - x1) * j / n; y = y1 + (y2 - y1) * j / n
        for dx in range(-w, w + 1):
            for dy in range(-w, w + 1): bl(int(x) + dx, int(y) + dy, c, 1.0)
def outline(poly, c, w=1):
    for i in range(len(poly)): line(poly[i], poly[(i + 1) % len(poly)], c, w)
def dot(p, c, r=3):
    cx, cy = tx(*p)
    for dx in range(-r, r + 1):
        for dy in range(-r, r + 1):
            if dx * dx + dy * dy <= r * r: bl(int(cx) + dx, int(cy) + dy, c, 1.0)

TEAM_FILL = [(150, 96, 60), (60, 92, 150)]      # team 0 warm, team 1 (mirror) cool
TEAM_EDGE = [(240, 170, 90), (110, 160, 245)]
HOLE_EDGE = (235, 80, 80)
BG = (12, 16, 24)
# 1) fill all 'add' polygons, 2) cut 'subtract' (fill with bg), 3) outlines + vertices on top
for vs, op, team in insts:
    if op != "subtract": fill(vs, TEAM_FILL[team], 0.55)
for vs, op, team in insts:
    if op == "subtract": fill(vs, BG, 1.0)
for vs, op, team in insts:
    if op == "subtract":
        outline(vs, HOLE_EDGE, 1)
        for p in vs: dot(p, HOLE_EDGE, 2)
    else:
        outline(vs, TEAM_EDGE[team], 1)
        for p in vs: dot(p, TEAM_EDGE[team], 2)

# mid line (mirror axis)
if mirror == "mirror_z": line((minx, cz), (maxx, cz), (90, 90, 110), 0)

def png(buf, w, h):
    raw = bytearray()
    for y in range(h): raw.append(0); raw += buf[y * w * 3:(y + 1) * w * 3]
    def ch(t, d): cc = t + d; return struct.pack(">I", len(d)) + cc + struct.pack(">I", zlib.crc32(cc) & 0xffffffff)
    return b"\x89PNG\r\n\x1a\n" + ch(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 2, 0, 0, 0)) + ch(b"IDAT", zlib.compress(bytes(raw), 9)) + ch(b"IEND", b"")
open(OUT, "wb").write(png(img, IW, IH))
nshape = len([1 for _, op, t in insts if t == 0])
print(f"rendered {OUT}: {nshape} team-0 polygons + their mirror, {sum(1 for _,op,_ in insts if op=='subtract')} holes; vertices marked")
