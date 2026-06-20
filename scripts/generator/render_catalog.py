#!/usr/bin/env python3
"""Render the style catalogue (--gen-catalog) — every hub style + lane behaviour as polygon outlines.

Non-mirrored layout; shapes are pre-positioned in a grid (top row hubs, bottom row lanes). add = filled+
outlined, subtract (holes) = red cut. Labels come from the message, not rendered. Usage: render_catalog.py <in.json> <out.png>
"""
import sys, json, struct, zlib
D = json.load(open(sys.argv[1])); OUT = sys.argv[2]
shapes = D["layout"]["shapes"]
def verts(s): return [(v[0], v[1]) for v in (s.get("vertices") or [])]

allpts = [p for s in shapes for p in verts(s)]
minx = min(p[0] for p in allpts); maxx = max(p[0] for p in allpts)
minz = min(p[1] for p in allpts); maxz = max(p[1] for p in allpts)
W, Hh = maxx - minx, maxz - minz
IW = 1000; IH = int(IW * Hh / W) + 30; PAD = 22
sc = min((IW - 2*PAD)/W, (IH - 2*PAD)/Hh); ox = (IW - W*sc)/2; oy = (IH - Hh*sc)/2
def tx(x, z): return ox + (x-minx)*sc, oy + (z-minz)*sc
img = bytearray((12,16,24)*(IW*IH))
def bl(x,y,c,a=1.0):
    if 0<=x<IW and 0<=y<IH:
        o=(y*IW+x)*3
        for i in range(3): img[o+i]=int(img[o+i]*(1-a)+c[i]*a)
def fill(poly,c,a):
    pts=[tx(*p) for p in poly]; ys=[p[1] for p in pts]
    for y in range(int(min(ys)),int(max(ys))+1):
        xi=[]
        for i in range(len(pts)):
            x1,y1=pts[i]; x2,y2=pts[(i+1)%len(pts)]
            if (y1<=y<y2) or (y2<=y<y1): xi.append(x1+(y-y1)*(x2-x1)/(y2-y1))
        xi.sort()
        for k in range(0,len(xi)-1,2):
            for x in range(int(xi[k]),int(xi[k+1])+1): bl(x,y,c,a)
def line(p1,p2,c,w=1):
    x1,y1=tx(*p1); x2,y2=tx(*p2); n=int(max(abs(x2-x1),abs(y2-y1)))+1
    for j in range(n+1):
        x=x1+(x2-x1)*j/n; y=y1+(y2-y1)*j/n
        for dx in range(-w,w+1):
            for dy in range(-w,w+1): bl(int(x)+dx,int(y)+dy,c,1.0)
def outline(poly,c,w=1):
    for i in range(len(poly)): line(poly[i],poly[(i+1)%len(poly)],c,w)
def dot(p,c,r=2):
    cx,cy=tx(*p)
    for dx in range(-r,r+1):
        for dy in range(-r,r+1):
            if dx*dx+dy*dy<=r*r: bl(int(cx)+dx,int(cy)+dy,c,1.0)

HUB_F=(70,86,110); HUB_E=(120,150,200); LANE_F=(150,96,52); LANE_E=(240,170,90); HOLE=(235,80,80); BG=(12,16,24)
def is_hub(s): return s["id"].startswith("hub") or s["id"].startswith("lhub")
def is_sub(s): return s["operation"]=="subtract"
# fills first (adds), then cut holes, then outlines+vertices
for s in shapes:
    if not is_sub(s): fill(verts(s), HUB_F if is_hub(s) else LANE_F, 0.6)
for s in shapes:
    if is_sub(s): fill(verts(s), BG, 1.0)
for s in shapes:
    vs=verts(s)
    if is_sub(s): outline(vs,HOLE,1); [dot(p,HOLE,2) for p in vs]
    else:
        c = HUB_E if is_hub(s) else LANE_E
        outline(vs,c,1); [dot(p,c,2) for p in vs]
def png(buf,w,h):
    raw=bytearray()
    for y in range(h): raw.append(0); raw+=buf[y*w*3:(y+1)*w*3]
    def ch(t,d): cc=t+d; return struct.pack(">I",len(d))+cc+struct.pack(">I",zlib.crc32(cc)&0xffffffff)
    return b"\x89PNG\r\n\x1a\n"+ch(b"IHDR",struct.pack(">IIBBBBB",w,h,8,2,0,0,0))+ch(b"IDAT",zlib.compress(bytes(raw),9))+ch(b"IEND",b"")
open(OUT,"wb").write(png(img,IW,IH))
print(f"rendered {OUT}: {len(shapes)} shapes")
