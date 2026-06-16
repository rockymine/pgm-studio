#!/usr/bin/env python3
"""Quantify two authoring-flow aids:
 (1) the user selecting the island / bounding box that holds the monuments  -> restrict sign anchors;
 (2) the user picking a monument STYLE (pedestal + label type)              -> require that signature.
Builds on the refined text classifier from sign_text_analysis."""
import os, sys, json, glob, math, re
from collections import Counter, defaultdict
from nbt import region
import importlib.util
spec=importlib.util.spec_from_file_location("mca",os.path.join(os.path.dirname(os.path.abspath(__file__)),"monument_corpus_analysis.py"))
mca=importlib.util.module_from_spec(spec); spec.loader.exec_module(mca)
SIGN_IDS=mca.SIGN_IDS
WOOL_COLORS=["white","orange","magenta","light blue","lightblue","light_blue","yellow","lime","pink",
             "gray","grey","silver","cyan","purple","blue","brown","green","red","black"]
SECTION_COLOR={"0":"black","1":"dark_blue","2":"dark_green","3":"dark_aqua","4":"dark_red","5":"dark_purple",
               "6":"gold","7":"gray","8":"dark_gray","9":"blue","a":"green","b":"aqua","c":"red",
               "d":"light_purple","e":"yellow","f":"white"}
NEG_PHRASES=["team only","only reds","only blues","beyond this","behind you","behind this","back to",
             "entrance","kill","break wool","break the","get wool","return the","return your","victory monument",
             "can't build","cannot build","no build","do not","this way","exit","welcome","spawn","shop","buy","sell",
             "woolroom","wool room","this line","blocked","->","<-","this point","pick up","stand","portal",
             "to:","teleport","defend","danger","redstone","button","helicopter","use before"]
def norm_text(s):
    cols=set()
    for m in re.finditer("§(.)", s):
        c=m.group(1).lower()
        if c in SECTION_COLOR: cols.add(SECTION_COLOR[c])
    p=re.sub("§.","",s).lower(); p=re.sub(r"\s+"," ",p).strip()
    for c in WOOL_COLORS:
        if c in p: cols.add(c.replace(" ","_"))
    return p,cols
def is_label(plain, cols):
    t=plain
    if any(p in t for p in NEG_PHRASES): return False
    has_color = bool(cols) or any(c in t for c in WOOL_COLORS)
    if "place" in t and "here" in t and has_color: return True
    if "monument" in t and has_color and len(t)<=40: return True
    if "wool" in t and has_color and len(t)<=24: return True
    return False
def bname(bid): return mca.bname(bid)

maps=[]
for root in mca.ROOTS:
    if not os.path.isdir(root): continue
    for slug in sorted(os.listdir(root)):
        rd=os.path.join(root,slug,"region")
        if os.path.isdir(rd) and glob.glob(os.path.join(rd,"r.*.mca")):
            maps.append((slug,rd))

def clusters(mpts, link=16):
    """Greedy clusters of monuments (Chebyshev<=link), each → (min,max) bbox."""
    cl=[]
    for p in mpts:
        for c in cl:
            if any(mca.chebyshev(p,q)<=link for q in c): c.append(p); break
        else: cl.append([p])
    boxes=[]
    for c in cl:
        xs=[q[0] for q in c]; ys=[q[1] for q in c]; zs=[q[2] for q in c]
        boxes.append((min(xs),min(ys),min(zs),max(xs),max(ys),max(zs)))
    return boxes
def in_boxes(p, boxes, m):
    for (x0,y0,z0,x1,y1,z1) in boxes:
        if x0-m<=p[0]<=x1+m and y0-m<=p[1]<=y1+m and z0-m<=p[2]<=z1+m: return True
    return False

# ---------- PART A (cheap): how much does a user box exclude FP-source signs? ----------
exc=defaultdict(lambda:[0,0])   # margin -> [kept_far(FP), total_far]
lbl_kept=defaultdict(lambda:[0,0])
def read_signs_only(rd):
    out=[]
    for f in glob.glob(os.path.join(rd,"r.*.mca")):
        try: rf=region.RegionFile(f)
        except Exception: continue
        for meta in rf.get_metadata():
            try:
                ch=rf.get_chunk(meta.x,meta.z); lvl=ch["Level"]
            except Exception: continue
            for te in (lvl["TileEntities"] if "TileEntities" in lvl else []):
                if te["id"].value=="Sign":
                    p,cols=norm_text("\n".join(te[f"Text{i}"].value for i in (1,2,3,4) if f"Text{i}" in te))
                    out.append((te["x"].value,te["y"].value,te["z"].value,p,cols))
    return out
MARGINS=[0,6,12,24]
map_mons={}
for slug,rd in maps:
    mons=mca.load_monuments(slug)
    if not mons: continue
    map_mons[slug]=mons
    mpts=[(m["x"],m["y"],m["z"]) for m in mons]
    boxes=clusters(mpts)
    signs=read_signs_only(rd)
    for (sx,sy,sz,plain,cols) in signs:
        near=any(abs(sx-mx)<=2 and abs(sz-mz)<=2 and -3<=sy-my<=2 for (mx,my,mz) in mpts)
        kw=is_label(plain,cols)
        if not near and any(k in plain for k in mca.KEYWORDS):   # an FP-source keyword sign
            for M in MARGINS:
                exc[M][1]+=1
                if in_boxes((sx,sy,sz),boxes,M): exc[M][0]+=1
        if near and kw:
            for M in MARGINS:
                lbl_kept[M][1]+=1
                if in_boxes((sx,sy,sz),boxes,M): lbl_kept[M][0]+=1

R=[]
R.append("# Authoring-flow scoping: user-selected box + monument-style menu\n")
R.append("## (1) How much does a user-drawn monument box exclude false-positive signs?\n")
R.append("Box = bounding box of each monument cluster (Chebyshev≤16) expanded by margin M.\n")
R.append("| margin | FP-source signs still inside box | label signs retained |")
R.append("|---|---|---|")
for M in MARGINS:
    fk,ft=exc[M]; lk,lt=lbl_kept[M]
    R.append(f"| ±{M} | {fk}/{ft} ({100*fk/max(ft,1):.0f}%) | {lk}/{lt} ({100*lk/max(lt,1):.0f}%) |")

# ---------- PART B (volume): detector refined vs +box vs +box+pedestal; + style menu ----------
face_to_offset=defaultdict(Counter)
for slug,rd in maps:
    mons=map_mons.get(slug)
    if not mons: continue
    try: vol,signs,ents=mca.read_world(rd)
    except Exception: continue
    mpts=[(m["x"],m["y"],m["z"]) for m in mons]
    for (sx,sy,sz,txt,kw) in signs:
        if not kw: continue
        sb=vol.get((sx,sy,sz))
        if not (sb and sb[0] in SIGN_IDS): continue
        for (mx,my,mz) in mpts:
            if abs(sx-mx)<=2 and abs(sz-mz)<=2 and -3<=sy-my<=2:
                face_to_offset[(sb[0],sb[1])][(mx-sx,my-sy,mz-sz)]+=1
offsets_for={}
for key,ctr in face_to_offset.items():
    tot=sum(ctr.values()); off,cnt=ctr.most_common(1)[0]
    if cnt>=3: offsets_for[key]=[o for o,c in ctr.items() if c>=3 and c>=0.2*tot]
def cluster_sites(cand):
    sites=[]
    for c in sorted(cand):
        for s in sites:
            if mca.chebyshev(c,s[0])<=1: s.append(c); break
        else: sites.append([c])
    return [min(s) for s in sites]

PEDESTALS={7,159,95,35,82,159}   # bedrock, stained clay, stained glass, wool, hardened clay
det=dict(a_tp=0,a_fp=0,a_fn=0, b_tp=0,b_fp=0,b_fn=0, c_tp=0,c_fp=0,c_fn=0)
joint=Counter(); labeltype=Counter()
MBOX=8
for slug,rd in maps:
    mons=map_mons.get(slug)
    if not mons: continue
    try: vol,signs,ents=mca.read_world(rd)
    except Exception: continue
    mpts=[(m["x"],m["y"],m["z"]) for m in mons]
    boxes=clusters(mpts)
    # style menu tallies (ground truth)
    for m in mons:
        cx,cy,cz=m["x"],m["y"],m["z"]
        bl=vol.get((cx,cy-1,cz)); ab=vol.get((cx,cy+1,cz))
        joint[(bname(bl[0]) if bl else "air", bname(ab[0]) if ab else "air")]+=1
        # label type
        lt="none"
        has_sign_below=has_sign_above=has_as=False
        for (sx,sy,sz,txt,kw) in signs:
            if abs(sx-cx)<=1 and abs(sz-cz)<=1 and is_label(*norm_text(txt)):
                if sy-cy==-1: has_sign_below=True
                elif sy-cy>=1: has_sign_above=True
        for (eid,ex,ey,ez) in ents:
            if eid=="ArmorStand" and abs(int(math.floor(ex))-cx)<=1 and abs(int(math.floor(ez))-cz)<=1 and (cy-3)<=int(math.floor(ey))<=(cy+2):
                has_as=True
        if has_as: lt="armorstand"
        elif has_sign_below: lt="sign_below"
        elif has_sign_above: lt="sign_above"
        labeltype[lt]+=1

    def detect(use_box, use_ped):
        cand=set()
        for (sx,sy,sz,txt,kw) in signs:
            if not is_label(*norm_text(txt)): continue
            if use_box and not in_boxes((sx,sy,sz),boxes,MBOX): continue
            sb=vol.get((sx,sy,sz))
            if not (sb and sb[0] in SIGN_IDS): continue
            for (dx,dy,dz) in offsets_for.get((sb[0],sb[1]),()):
                C=(sx+dx,sy+dy,sz+dz)
                if vol.get(C) is not None: continue
                bl=vol.get((C[0],C[1]-1,C[2]))
                if bl is None or bl[0] in SIGN_IDS: continue
                if use_ped and bl[0] not in PEDESTALS: continue
                cand.add(C)
        return cluster_sites(cand)
    for (ub,up,pre) in [(False,False,"a_"),(True,False,"b_"),(True,True,"c_")]:
        sites=detect(ub,up)
        mm=set(); ms=set()
        for i,sp in enumerate(sites):
            for j,mp in enumerate(mpts):
                if j in mm: continue
                if mca.chebyshev(sp,mp)<=1: ms.add(i); mm.add(j); break
        det[pre+"tp"]+=len(mm); det[pre+"fp"]+=len(sites)-len(ms); det[pre+"fn"]+=len(mpts)-len(mm)

def prf(tp,fp,fn,label):
    pr=tp/(tp+fp) if tp+fp else 0; rc=tp/(tp+fn) if tp+fn else 0
    R.append(f"- **{label}**: TP={tp} FP={fp} FN={fn}  ·  precision={100*pr:.1f}%  recall={100*rc:.1f}%")
R.append("\n## (1) Detector precision with the user box (refined text classifier throughout)\n")
prf(det["a_tp"],det["a_fp"],det["a_fn"],"refined text, whole map")
prf(det["b_tp"],det["b_fp"],det["b_fn"],"refined text + user box (margin ±8)")
prf(det["c_tp"],det["c_fp"],det["c_fn"],"refined text + user box + require known pedestal below")

R.append("\n## (2) Monument-style menu — joint (block below, block above) of the monument\n")
tot=sum(joint.values())
for (b,a),c in joint.most_common(14):
    R.append(f"- below={b}, above={a}: {c} ({100*c/tot:.1f}%)")
R.append("\n### Label type per monument (what to ask 'how did you mark it?')\n")
ttot=sum(labeltype.values())
for lt,c in labeltype.most_common():
    R.append(f"- {lt}: {c} ({100*c/ttot:.1f}%)")
print("\n".join(R))
