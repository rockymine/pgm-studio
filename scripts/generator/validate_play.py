#!/usr/bin/env python3
"""Validate playability: can each attacker reach the enemy wool WITHOUT crossing the enemy spawn protection?

Generates a map via the API, pulls the intent (spawns+protection, wools, build bridges) and the island
polygons, builds the navigable terrain, and BFSes attacker-spawn -> enemy-wool with the defender's spawn
protection removed (enemies cannot enter it). Renders the regions and reports reachable/BLOCKED per wool.

Usage: validate_play.py <archetype> <seed> <out.png>
"""
import sys, json, subprocess, struct, zlib, math
import numpy as np
from skimage.draw import polygon as draw_polygon

DATA, OUT = sys.argv[1], sys.argv[2]
slug = DATA
D = json.load(open(DATA))
spawns = {s["team"]: s for s in D["spawns"]}
wools = D["wools"]
builds = D["build"]["areas"]
islands = [{"polygon": {"coordinates": [isl["exterior"]] + isl["holes"]},
            "bounds": [min(p[0] for p in isl["exterior"]), min(p[1] for p in isl["exterior"]),
                       max(p[0] for p in isl["exterior"]), max(p[1] for p in isl["exterior"])]}
           for isl in D["islands"]]
def _pt(o): return {"x": o["x"], "z": o["z"]}
for s in D["spawns"]: s["point"] = _pt(s)
for w in D["wools"]: w["spawn"] = _pt(w)

# ---- world grid -----------------------------------------------------------
xs = [c for isl in islands for c in (isl["bounds"][0], isl["bounds"][2])]
zs = [c for isl in islands for c in (isl["bounds"][1], isl["bounds"][3])]
for r in builds: xs += [r["minX"], r["maxX"]]; zs += [r["minZ"], r["maxZ"]]
MINX, MINZ = int(min(xs))-2, int(min(zs))-2
W, Hh = int(max(xs))-MINX+3, int(max(zs))-MINZ+3
terrain = np.zeros((Hh, W), bool)
for isl in islands:
    rr, cc = draw_polygon([int(z-MINZ) for x, z in isl["polygon"]["coordinates"][0]],
                          [int(x-MINX) for x, z in isl["polygon"]["coordinates"][0]], terrain.shape)
    terrain[rr, cc] = True
    for hole in isl["polygon"]["coordinates"][1:]:
        rr, cc = draw_polygon([int(z-MINZ) for x, z in hole], [int(x-MINX) for x, z in hole], terrain.shape)
        terrain[rr, cc] = False
def rect_cells(r):
    out = np.zeros((Hh, W), bool)
    out[max(0,int(r["minZ"])-MINZ):int(r["maxZ"])-MINZ+1, max(0,int(r["minX"])-MINX):int(r["maxX"])-MINX+1] = True
    return out
navigable = terrain.copy()
for r in builds: navigable |= rect_cells(r)        # bridges make the void crossable

def bfs(navg, start, goal):
    sx, sz = int(start[0]-MINX), int(start[1]-MINZ)
    gx, gz = int(goal[0]-MINX), int(goal[1]-MINZ)
    # snap start/goal to nearest navigable within 6
    def snap(x, z):
        for r in range(7):
            for dx in range(-r, r+1):
                for dz in range(-r, r+1):
                    if 0<=z+dz<Hh and 0<=x+dx<W and navg[z+dz, x+dx]: return (x+dx, z+dz)
        return None
    s, g = snap(sx, sz), snap(gx, gz)
    if not s or not g: return False
    seen = np.zeros_like(navg); stack=[s]; seen[s[1],s[0]]=True
    while stack:
        x, z = stack.pop()
        if (x, z) == g: return True
        for dx, dz in ((1,0),(-1,0),(0,1),(0,-1)):
            nx, nz = x+dx, z+dz
            if 0<=nx<W and 0<=nz<Hh and navg[nz,nx] and not seen[nz,nx]: seen[nz,nx]=True; stack.append((nx,nz))
    return seen[g[1], g[0]]

print(f"\n=== {slug}: attacker -> enemy wool, avoiding enemy spawn protection ===")
ok = True
for w in wools:
    defender = w["owner"]; captor = w["monuments"][0]["team"]
    cap_spawn = spawns[captor]["point"]
    wool_pt = w["spawn"]
    # navigable for THIS attacker = navigable minus the defender's spawn protection (a wall to enemies)
    attg = navigable.copy()
    prot = spawns[defender].get("protection")
    if prot: attg &= ~rect_cells(prot)
    reach = bfs(attg, (cap_spawn["x"], cap_spawn["z"]), (wool_pt["x"], wool_pt["z"]))
    base = bfs(navigable.copy(), (cap_spawn["x"], cap_spawn["z"]), (wool_pt["x"], wool_pt["z"]))
    print(f"  {captor:5} -> {w.get('color','?')} wool (def {defender}): "
          f"reachable-ignoring-protection={base}  reachable-around-protection={reach}  "
          f"{'OK' if reach else ('BLOCKED by enemy spawn' if base else 'disconnected')}")
    ok = ok and reach
print(f"  => {'PLAYABLE' if ok else 'UNPLAYABLE (a wool is unreachable around the enemy spawn)'}")

# ---- render ---------------------------------------------------------------
IW, IH = 760, 920; PAD = 26
sc = min((IW-2*PAD)/W, (IH-2*PAD)/Hh); ox=PAD; oy=PAD
def tx(wx, wz): return (ox+(wx-MINX)*sc, oy+(wz-MINZ)*sc)
BG=(12,17,26); LAND=(95,108,125); BR=(250,150,60); PROT=(235,70,70); ROOM=(70,120,235)
TEAM={"red":(255,85,85),"blue":(85,85,255)}; DYE={"red":(153,51,51),"blue":(51,76,178)}
img=bytearray(BG*(IW*IH))
def bl(x,y,c,a=1.0):
    if 0<=x<IW and 0<=y<IH:
        o=(y*IW+x)*3
        for i in range(3): img[o+i]=int(img[o+i]*(1-a)+c[i]*a)
def fill_grid(mask,c,a):
    for z in range(Hh):
        for x in range(W):
            if mask[z,x]:
                px,py=tx(x+MINX,z+MINZ)
                for dx in range(int(sc)+1):
                    for dy in range(int(sc)+1): bl(int(px)+dx,int(py)+dy,c,a)
def box(r,c,a):
    fill_grid(rect_cells(r),c,a)
def marker(wx,wz,c,shape):
    cx,cy=tx(wx,wz)
    if shape=="star":
        pts=[(cx+(9 if i%2==0 else 4)*math.cos(-math.pi/2+i*math.pi/5),cy+(9 if i%2==0 else 4)*math.sin(-math.pi/2+i*math.pi/5)) for i in range(10)]
    else:
        pts=[(cx,cy-7),(cx+7,cy),(cx,cy+7),(cx-7,cy)]
    ys=[p[1] for p in pts]
    for y in range(int(min(ys)),int(max(ys))+1):
        xs2=[]
        for i in range(len(pts)):
            x1,y1=pts[i]; x2,y2=pts[(i+1)%len(pts)]
            if (y1<=y<y2) or (y2<=y<y1): xs2.append(x1+(y-y1)*(x2-x1)/(y2-y1))
        xs2.sort()
        for k in range(0,len(xs2)-1,2):
            for x in range(int(xs2[k]),int(xs2[k+1])+1): bl(x,y,c,1.0)
fill_grid(terrain, LAND, 0.95)
for r in builds: box(r, BR, 0.35)
for w in wools:
    if w.get("room"): box(w["room"], ROOM, 0.30)
for t, s in spawns.items():
    if s.get("protection"): box(s["protection"], PROT, 0.34)
for w in wools: marker(w["spawn"]["x"], w["spawn"]["z"], DYE.get(w["owner"], (220,220,220)), "diamond")
for t, s in spawns.items(): marker(s["point"]["x"], s["point"]["z"], TEAM.get(t,(220,220,220)), "star")
def png(buf,w,h):
    raw=bytearray()
    for y in range(h): raw.append(0); raw+=buf[y*w*3:(y+1)*w*3]
    def ch(tag,d): cc=tag+d; return struct.pack(">I",len(d))+cc+struct.pack(">I",zlib.crc32(cc)&0xffffffff)
    return b"\x89PNG\r\n\x1a\n"+ch(b"IHDR",struct.pack(">IIBBBBB",w,h,8,2,0,0,0))+ch(b"IDAT",zlib.compress(bytes(raw),9))+ch(b"IEND",b"")
open(OUT,"wb").write(png(img,IW,IH))
print(f"  rendered {OUT} (red=spawn protection, blue=wool room, orange=bridge)")
