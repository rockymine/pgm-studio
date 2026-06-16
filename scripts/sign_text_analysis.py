#!/usr/bin/env python3
"""What do monument-label signs say vs the keyword signs that cause false positives?
Mines sign-text patterns, then re-runs the world-only detector with a refined text classifier to
measure the precision gain over the broad keyword filter."""
import os, sys, json, glob, math, re
from collections import Counter, defaultdict
from nbt import region
import importlib.util
spec=importlib.util.spec_from_file_location("mca",os.path.join(os.path.dirname(os.path.abspath(__file__)),"monument_corpus_analysis.py"))
mca=importlib.util.module_from_spec(spec); spec.loader.exec_module(mca)

ROOTS=mca.ROOTS; SIGN_IDS=mca.SIGN_IDS
WOOL_COLORS=["white","orange","magenta","light blue","lightblue","light_blue","yellow","lime","pink",
             "gray","grey","silver","cyan","purple","blue","brown","green","red","black"]
# § formatting code -> rough colour
SECTION_COLOR={"0":"black","1":"dark_blue","2":"dark_green","3":"dark_aqua","4":"dark_red","5":"dark_purple",
               "6":"gold","7":"gray","8":"dark_gray","9":"blue","a":"green","b":"aqua","c":"red",
               "d":"light_purple","e":"yellow","f":"white"}

def decode_rich(tag):
    """Return (plain_lower, colors:set) from the 4 sign lines, capturing colour words, § codes and
    JSON 'color' fields."""
    colors=set(); parts=[]
    for i in (1,2,3,4):
        raw = tag[f"Text{i}"].value if f"Text{i}" in tag else ""
        raw=(raw or "").strip()
        if not raw: continue
        if raw[0] in "{[\"":
            try:
                obj=json.loads(raw)
                def walk(o):
                    if isinstance(o,str): parts.append(o)
                    elif isinstance(o,list):
                        for x in o: walk(x)
                    elif isinstance(o,dict):
                        if isinstance(o.get("color"),str): colors.add(o["color"])
                        if isinstance(o.get("text"),str): parts.append(o["text"])
                        if "extra" in o: walk(o["extra"])
                walk(obj); continue
            except Exception:
                pass
        parts.append(raw)
    joined=" ".join(parts)
    # § codes → colour, then strip codes
    for m in re.finditer("§(.)", joined):
        c=m.group(1).lower()
        if c in SECTION_COLOR: colors.add(SECTION_COLOR[c])
    plain=re.sub("§.","",joined).lower()
    plain=re.sub(r"\s+"," ",plain).strip()
    # colour words in the text count as colour hints too
    for c in WOOL_COLORS:
        if c in plain: colors.add(c.replace(" ","_"))
    return plain, colors

def read_signs_only(rd):
    """Fast: signs (no section decode) + entities."""
    out=[]
    for f in glob.glob(os.path.join(rd,"r.*.mca")):
        try: rf=region.RegionFile(f)
        except Exception: continue
        for meta in rf.get_metadata():
            try:
                ch=rf.get_chunk(meta.x,meta.z)
                lvl=ch["Level"]
            except Exception: continue
            for te in (lvl["TileEntities"] if "TileEntities" in lvl else []):
                if te["id"].value=="Sign":
                    p,cols=decode_rich(te)
                    out.append((te["x"].value,te["y"].value,te["z"].value,p,cols))
    return out

def tokens(t):
    return [w for w in re.split(r"[^a-z0-9_]+", t) if w and not w.isdigit() and len(w)>1]

# -------- PASS A: build positive (monument-label) vs negative (other keyword) text corpora --------
maps=[]
for root in ROOTS:
    if not os.path.isdir(root): continue
    for slug in sorted(os.listdir(root)):
        rd=os.path.join(root,slug,"region")
        if os.path.isdir(rd) and glob.glob(os.path.join(rd,"r.*.mca")):
            maps.append((slug,rd))

pos_text=[]; neg_text=[]              # full plain texts
pos_tok=Counter(); neg_tok=Counter()
pos_bg=Counter(); neg_bg=Counter()
feat=Counter(); n_pos=0
map_signs={}; map_mons={}
KW=mca.KEYWORDS
def is_keyword(t): return any(k in t for k in KW)

for slug,rd in maps:
    mons=mca.load_monuments(slug)
    if not mons: continue
    signs=read_signs_only(rd)
    map_signs[slug]=signs; map_mons[slug]=mons
    mpts=[(m["x"],m["y"],m["z"]) for m in mons]
    for (sx,sy,sz,plain,cols) in signs:
        in_slice=any(abs(sx-mx)<=1 and abs(sz-mz)<=1 and -2<=sy-my<=2 for (mx,my,mz) in mpts)
        in_box  =any(abs(sx-mx)<=2 and abs(sz-mz)<=2 and -3<=sy-my<=2 for (mx,my,mz) in mpts)
        if in_slice:
            n_pos+=1; pos_text.append(plain)
            tk=tokens(plain); pos_tok.update(tk); pos_bg.update(zip(tk,tk[1:]))
            feat["has_wool"]+= "wool" in plain
            feat["has_monument"]+= "monument" in plain
            feat["has_place"]+= "place" in plain
            feat["has_here"]+= "here" in plain
            feat["has_color"]+= len(cols)>0
            feat["place_here"]+= ("place" in plain and "here" in plain)
            feat["wool_and_color"]+= ("wool" in plain and len(cols)>0)
        elif not in_box and is_keyword(plain):
            neg_text.append(plain)
            tk=tokens(plain); neg_tok.update(tk); neg_bg.update(zip(tk,tk[1:]))

# discriminative score: tokens characteristic of positives vs negatives
def disc(pos_c,neg_c,minn=15):
    P=sum(pos_c.values()); N=sum(neg_c.values())
    rows=[]
    for w in set(pos_c)|set(neg_c):
        p=pos_c[w]; n=neg_c[w]
        if p+n<minn: continue
        # smoothed log-odds toward positive
        score=math.log((p+1)/(P+1))-math.log((n+1)/(N+1))
        rows.append((w,p,n,score))
    return rows

R=[]
R.append(f"# Sign-text patterns: monument labels vs false-positive signs\n")
R.append(f"positive (sign in a monument's 3×3×5 slice): **{n_pos}**  ·  negative (keyword sign not near any monument): **{len(neg_text)}**\n")

R.append("\n## Structural features of monument-label signs\n")
for k in ["has_wool","has_monument","has_color","has_place","has_here","place_here","wool_and_color"]:
    R.append(f"- {k}: {feat[k]}/{n_pos} ({100*feat[k]/max(n_pos,1):.0f}%)")

R.append("\n## Words most characteristic of LABEL signs (token, #pos, #neg, log-odds)\n")
for w,p,n,s in sorted(disc(pos_tok,neg_tok),key=lambda r:-r[3])[:20]:
    R.append(f"- {w}: pos={p} neg={n}  ({s:+.2f})")
R.append("\n## Words most characteristic of NON-monument keyword signs (the FP sources)\n")
for w,p,n,s in sorted(disc(pos_tok,neg_tok),key=lambda r:r[3])[:22]:
    R.append(f"- {w}: pos={p} neg={n}  ({s:+.2f})")

R.append("\n## Top label-sign bigrams\n")
for bg,c in pos_bg.most_common(15):
    R.append(f"- {' '.join(bg)}: {c}")
R.append("\n## Top non-monument keyword-sign bigrams\n")
for bg,c in neg_bg.most_common(15):
    R.append(f"- {' '.join(bg)}: {c}")

# -------- refined classifier --------
NEG_PHRASES=["team only","only reds","only blues","beyond this","behind you","behind this","back to",
             "entrance","kill","break wool","break the","get wool","return the","return your","victory monument",
             "can't build","cannot build","no build","do not","this way","exit","welcome","spawn","shop","buy","sell",
             "woolroom","wool room","this line","blocked","->","<-","this point","pick up","stand","portal",
             "to:","teleport","defend","danger","redstone","button","helicopter","use before"]
POS_COLORS=set(c.replace(" ","_") for c in WOOL_COLORS)
def norm_text(s):
    """Lowercase, drop § codes, collapse whitespace; return (plain, colours from § codes + words)."""
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
    if "place" in t and "here" in t and has_color: return True   # "place <colour> [wool] here"
    if "monument" in t and has_color and len(t)<=40: return True
    if "wool" in t and has_color and len(t)<=24: return True     # short "<colour> wool" label
    return False

# classifier accuracy on the corpora
tp=sum(1 for (p) in pos_text)  # placeholder
clf_tp=sum(1 for p in pos_text if is_label(p,set()) or True and is_label_with(p) ) if False else 0
def is_label_with(plain):
    # recompute colours from words only for corpus eval (we stored text only for neg/pos lists)
    cols=set(c.replace(" ","_") for c in WOOL_COLORS if c in plain)
    return is_label(plain,cols)
clf_pos=sum(1 for p in pos_text if is_label_with(p))
clf_neg=sum(1 for p in neg_text if is_label_with(p))
R.append("\n## Refined text classifier — accuracy on the corpora\n")
R.append(f"- accepts **{clf_pos}/{n_pos}** label signs ({100*clf_pos/max(n_pos,1):.0f}% recall)")
R.append(f"- accepts **{clf_neg}/{len(neg_text)}** non-monument keyword signs ({100*clf_neg/max(len(neg_text),1):.0f}% — lower is better)")
broad_neg=len(neg_text)
R.append(f"- (broad keyword filter accepts 100% of those {broad_neg} non-monument signs by construction)")

# sample false accepts / rejects
fa=[p for p in neg_text if is_label_with(p)][:12]
fr=[p for p in pos_text if not is_label_with(p)][:12]
R.append("\n### Sample non-monument signs the refined classifier still accepts (residual FP)\n")
for p in fa: R.append(f"- {p!r}")
R.append("\n### Sample label signs the refined classifier rejects (recall loss)\n")
for p in fr: R.append(f"- {p!r}")

# -------- PASS B: geo detector, broad keyword vs refined classifier --------
face_to_offset=defaultdict(Counter)
def read_world(rd): return mca.read_world(rd)
# learn facing from volume (re-read with volume)
vols={}
for slug,rd in maps:
    mons=map_mons.get(slug)
    if not mons: continue
    try: vol,signs,ents=read_world(rd)
    except Exception: continue
    vols[slug]=None  # don't keep
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

def cluster(cand):
    sites=[]
    for c in sorted(cand):
        for s in sites:
            if mca.chebyshev(c,s[0])<=1: s.append(c); break
        else: sites.append([c])
    return [min(s) for s in sites]

det=dict(b_tp=0,b_fp=0,b_fn=0, r_tp=0,r_fp=0,r_fn=0)
for slug,rd in maps:
    mons=map_mons.get(slug)
    if not mons: continue
    try: vol,signs,ents=read_world(rd)
    except Exception: continue
    mpts=[(m["x"],m["y"],m["z"]) for m in mons]
    def geo(use_refined):
        cand=set()
        for (sx,sy,sz,txt,kw) in signs:
            if use_refined:
                p,cols=norm_text(txt)
                if not is_label(p,cols): continue
            else:
                if not kw: continue
            sb=vol.get((sx,sy,sz))
            if not (sb and sb[0] in SIGN_IDS): continue
            for (dx,dy,dz) in offsets_for.get((sb[0],sb[1]),()):
                C=(sx+dx,sy+dy,sz+dz)
                if vol.get(C) is not None: continue
                bl=vol.get((C[0],C[1]-1,C[2]))
                if bl is None or bl[0] in SIGN_IDS: continue
                cand.add(C)
        return cluster(cand)
    for refined,pre in [(False,"b_"),(True,"r_")]:
        sites=geo(refined)
        mm=set(); ms=set()
        for i,sp in enumerate(sites):
            for j,mp in enumerate(mpts):
                if j in mm: continue
                if mca.chebyshev(sp,mp)<=1: ms.add(i); mm.add(j); break
        det[pre+"tp"]+=len(mm); det[pre+"fp"]+=len(sites)-len(ms); det[pre+"fn"]+=len(mpts)-len(mm)

def prf(tp,fp,fn,label):
    pr=tp/(tp+fp) if tp+fp else 0; rc=tp/(tp+fn) if tp+fn else 0
    R.append(f"- **{label}**: TP={tp} FP={fp} FN={fn}  ·  precision={100*pr:.1f}%  recall={100*rc:.1f}%")
R.append("\n## Detector precision with refined text classifier vs broad keyword\n")
prf(det["b_tp"],det["b_fp"],det["b_fn"],"broad keyword filter")
prf(det["r_tp"],det["r_fp"],det["r_fn"],"refined text classifier")

print("\n".join(R))
