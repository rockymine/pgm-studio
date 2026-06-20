#!/usr/bin/env python3
"""Run the protection-aware reachability check on a real corpus map.

Terrain + bridges come from the <map>_skel.json export; spawn protection, spawns and wools are parsed from
map.xml (<apply enter="deny(TEAM)" region="R"> = R blocks TEAM). For each wool, BFS the captor's spawn to
the wool over terrain∪bridges with every region the captor is denied entry removed.

Usage: corpus_validate.py <skel.json> <map.xml>
"""
import sys, json, math, re, xml.etree.ElementTree as ET
import numpy as np
from skimage.draw import polygon as draw_polygon

skel = json.load(open(sys.argv[1])); root = ET.parse(sys.argv[2]).getroot()
name = skel["name"]

xs = [p[0] for isl in skel["islands"] for p in isl["rawExterior"]]
zs = [p[1] for isl in skel["islands"] for p in isl["rawExterior"]]
for c in skel["buildCells"]: xs.append(c[0]); zs.append(c[1])
MINX, MINZ = int(min(xs))-40, int(min(zs))-40            # pad: protection rects can sit past the terrain
W, H = int(max(xs))-MINX+80, int(max(zs))-MINZ+80

def v2(s):
    p=[float(x) for x in s.replace(" ","").split(",")]; return (p[0], p[-1])
def fill_box(m,x0,z0,x1,z1):
    a,b=sorted((x0,x1)); c,d=sorted((z0,z1))
    m[max(0,int(c)-MINZ):int(d)-MINZ+1, max(0,int(a)-MINX):int(b)-MINX+1]=True
def fill_disk(m,cx,cz,r):
    for z in range(int(cz-r),int(cz+r)+1):
        for x in range(int(cx-r),int(cx+r)+1):
            if (x-cx)**2+(z-cz)**2<=r*r and 0<=z-MINZ<H and 0<=x-MINX<W: m[z-MINZ,x-MINX]=True

byid={el.get("id"):el for el in root.iter() if el.get("id")}
def raster(el,m,depth=0):
    if depth>6 or el is None: return
    t=el.tag
    if t in ("rectangle","cuboid") and el.get("min") and el.get("max"):
        a=v2(el.get("min")); b=v2(el.get("max")); fill_box(m,a[0],a[1],b[0],b[1])
    elif t=="cylinder" and el.get("base"):
        c=v2(el.get("base")); fill_disk(m,c[0],c[1],float(el.get("radius","1")))
    elif t in ("block","point") and el.text and "," in el.text:
        p=v2(el.text); fill_box(m,p[0]-0.5,p[1]-0.5,p[0]+0.5,p[1]+0.5)
    elif t in ("union","intersect","region"):
        if el.get("id") and t=="region" and el.get("id") in byid: raster(byid[el.get("id")],m,depth+1)
        for ch in el: raster(byid.get(ch.get("id"),ch) if ch.tag=="region" else ch, m, depth+1)

# terrain + bridges
terrain=np.zeros((H,W),bool)
for isl in skel["islands"]:
    rr,cc=draw_polygon([int(z-MINZ) for x,z in isl["rawExterior"]],[int(x-MINX) for x,z in isl["rawExterior"]],terrain.shape); terrain[rr,cc]=True
    for hole in isl["rawHoles"]:
        rr,cc=draw_polygon([int(z-MINZ) for x,z in hole],[int(x-MINX) for x,z in hole],terrain.shape); terrain[rr,cc]=False
nav=terrain.copy()
for c in skel["buildCells"]:
    if 0<=c[1]-MINZ<H and 0<=c[0]-MINX<W: nav[c[1]-MINZ,c[0]-MINX]=True

def raster_ref(el,m):
    """Raster a region given by an element: a region="id" attr, or inline shape/region children."""
    reg=el.get("region")
    if reg and reg in byid: raster(byid[reg],m)
    for ch in el:
        if ch.tag=="region" or ch.tag in ("rectangle","cuboid","cylinder","block","point","union","intersect"):
            raster(byid.get(ch.get("id"),ch) if ch.tag=="region" and ch.get("id") in byid else ch, m)

# blocked[team] = regions that team is denied entry (deny(team))
blocked={}
for ap in root.iter("apply"):
    e=ap.get("enter")
    if not e: continue
    mt=re.match(r"deny\(([^)]+)\)", e)
    if mt:
        team=mt.group(1); m=blocked.setdefault(team,np.zeros((H,W),bool)); raster_ref(ap,m)

spawns={}
for s in root.iter("spawn"):
    team=s.get("team")
    if not team: continue
    m=np.zeros((H,W),bool); raster_ref(s,m); yy,xx=np.where(m)
    if len(xx): spawns[team]=(int(xx.mean())+MINX,int(yy.mean())+MINZ)
seen=set(); wools=[]
for w in root.iter("wool"):
    loc=w.get("location"); team=w.get("team")
    if loc and team:
        x,z=v2(loc); k=(round(x),round(z),team)
        if k not in seen: seen.add(k); wools.append((team,x,z))

def bfs(g,start,goal):
    def snap(p):
        x,z=int(p[0]-MINX),int(p[1]-MINZ)
        for r in range(9):
            for dx in range(-r,r+1):
                for dz in range(-r,r+1):
                    if 0<=z+dz<H and 0<=x+dx<W and g[z+dz,x+dx]: return (x+dx,z+dz)
        return None
    s,t=snap(start),snap(goal)
    if not s or not t: return None
    seen=np.zeros_like(g); st=[s]; seen[s[1],s[0]]=True
    while st:
        x,z=st.pop()
        if (x,z)==t: return True
        for dx,dz in ((1,0),(-1,0),(0,1),(0,-1)):
            nx,nz=x+dx,z+dz
            if 0<=nx<W and 0<=nz<H and g[nz,nx] and not seen[nz,nx]: seen[nz,nx]=True; st.append((nx,nz))
    return bool(seen[t[1],t[0]])

print(f"=== {name}: {len(wools)} wools, teams {list(spawns)} ===")
ok=True; checked=0
for (team,wx,wz) in wools:
    if team not in spawns: continue
    g=nav & ~blocked.get(team,np.zeros((H,W),bool))
    reach=bfs(g,spawns[team],(wx,wz)); base=bfs(nav,spawns[team],(wx,wz))
    if reach is None: continue
    checked+=1; ok=ok and reach
    print(f"  {team:10} -> wool@({wx:.0f},{wz:.0f}): around-protection={reach}  (ignoring={base})  {'OK' if reach else 'BLOCKED'}")
print(f"  => {'PLAYABLE' if ok and checked else ('UNPLAYABLE' if checked else 'n/a (parse)')}\n")
