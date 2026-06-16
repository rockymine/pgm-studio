#!/usr/bin/env python3
"""Corpus analysis of CTW wool-monument surroundings + a world-only 'intelligent extractor' prototype.

Phase 1 (descriptive): over every CTW map with a world + resolved monuments, tally
  - sign usage (does a monument have a labelling sign? within the strict 3x3x5 slice? in a wider box?)
  - sign placement relative to the monument block (dx,dy,dz offset distribution)
  - the block directly BELOW and ABOVE the monument block (bedrock / stained clay / stained glass / ...)
  - armour-stand presence near the monument

Phase 2 (detector): anchored on the hypothesis "signs are placed against a block below the monument
  block", predict monument air cells from the WORLD ONLY (no xml) and measure precision / recall /
  false positives against the ground-truth monuments.
"""
import os, sys, json, glob, math
from collections import Counter, defaultdict
from nbt import region

ROOTS = ["/media/sf_repos/PublicMaps/ctw", "/media/sf_repos/CommunityMaps/ctw"]
OUT_ROOT = "/media/sf_repos/pgm-map-studio-output"

SIGN_IDS = {63, 68}                       # sign post, wall sign
WOOL_COLORS = ["white","orange","magenta","light blue","lightblue","yellow","lime","pink",
               "gray","grey","silver","cyan","purple","blue","brown","green","red","black"]
KEYWORDS = WOOL_COLORS + ["monument","wool","place","capture","objective"]
BLOCK_NAMES = {0:"air",7:"bedrock",95:"stained_glass",159:"stained_clay",20:"glass",35:"wool",
               89:"glowstone",1:"stone",98:"stone_bricks",5:"planks",45:"bricks",24:"sandstone",
               155:"quartz",57:"diamond_block",41:"gold_block",42:"iron_block",49:"obsidian",
               87:"netherrack",112:"nether_brick",44:"slab",43:"double_slab",139:"wall",
               101:"iron_bars",85:"fence",4:"cobblestone",48:"mossy_cobble",82:"clay",
               80:"snow_block",79:"ice",174:"packed_ice",133:"emerald_block",152:"redstone_block",
               22:"lapis_block",173:"coal_block",165:"slime",3:"dirt",2:"grass",17:"log",
               18:"leaves",251:"concrete",252:"concrete_powder"}

def bname(bid):
    return BLOCK_NAMES.get(bid, f"id{bid}")

def decode_sign_line(raw):
    raw = (raw or "").strip()
    if not raw: return ""
    if raw[0] not in "{[\"":
        return raw
    try:
        obj = json.loads(raw)
    except Exception:
        return raw
    out = []
    def walk(o):
        if isinstance(o, str): out.append(o)
        elif isinstance(o, list):
            for x in o: walk(x)
        elif isinstance(o, dict):
            if isinstance(o.get("text"), str): out.append(o["text"])
            if "extra" in o: walk(o["extra"])
    walk(obj)
    return "".join(out)

def sign_text(tag):
    lines = [decode_sign_line(tag[f"Text{i}"].value) if f"Text{i}" in tag else "" for i in (1,2,3,4)]
    return "\n".join(lines)

def is_keyword(text):
    t = text.lower()
    # strip section-sign color codes
    t = t.replace("§", " ")
    return any(k in t for k in KEYWORDS)

def load_monuments(slug):
    p = os.path.join(OUT_ROOT, slug, "xml_data.json")
    if not os.path.exists(p): return None
    try: d = json.load(open(p))
    except Exception: return None
    mons = []
    for w in d.get("wools", []):
        wid = w.get("id"); color = w.get("color", wid)
        for m in w.get("monuments", []):
            l = m.get("location") or {}
            if not all(k in l for k in ("x","y","z")): continue
            mons.append(dict(id=m.get("id"), color=color, team=m.get("team"),
                             x=int(math.floor(l["x"])), y=int(math.floor(l["y"])), z=int(math.floor(l["z"]))))
    return mons

def read_world(rd):
    """Full non-air volume + all signs + all entities."""
    vol = {}
    signs = []   # (x,y,z,text,keyword)
    ents = []    # (id, fx,fy,fz)
    for f in glob.glob(os.path.join(rd, "r.*.mca")):
        try: rf = region.RegionFile(f)
        except Exception: continue
        for meta in rf.get_metadata():
            try: ch = rf.get_chunk(meta.x, meta.z)
            except Exception: continue
            lvl = ch["Level"]; bx = lvl["xPos"].value*16; bz = lvl["zPos"].value*16
            if "Sections" in lvl:
                for sec in lvl["Sections"]:
                    sy = sec["Y"].value*16
                    if "Blocks" not in sec or "Data" not in sec: continue
                    B = sec["Blocks"].value; D = sec["Data"].value
                    add = sec["Add"].value if "Add" in sec else None
                    for idx in range(4096):
                        bid = B[idx]
                        if add is not None:
                            bid |= ((add[idx>>1]>>4) if idx&1 else (add[idx>>1]&0xF)) << 8
                        if bid == 0: continue
                        d = (D[idx>>1]>>4) if idx&1 else (D[idx>>1]&0xF)
                        vol[(bx+(idx&0xF), sy+(idx>>8), bz+((idx>>4)&0xF))] = (bid, d)
            for te in (lvl["TileEntities"] if "TileEntities" in lvl else []):
                if te["id"].value == "Sign":
                    t = sign_text(te)
                    signs.append((te["x"].value, te["y"].value, te["z"].value, t, is_keyword(t)))
            for en in (lvl["Entities"] if "Entities" in lvl else []):
                p = en["Pos"]
                ents.append((en["id"].value, p[0].value, p[1].value, p[2].value))
    return vol, signs, ents

def chebyshev(a, b):
    return max(abs(a[0]-b[0]), abs(a[1]-b[1]), abs(a[2]-b[2]))

# ------------------------- main -------------------------
def main(limit=None):
    maps = []
    for root in ROOTS:
        if not os.path.isdir(root): continue
        for slug in sorted(os.listdir(root)):
            rd = os.path.join(root, slug, "region")
            if os.path.isdir(rd) and glob.glob(os.path.join(rd, "r.*.mca")):
                maps.append((slug, rd))
    if limit: maps = maps[:limit]

    # Phase 1 aggregates
    n_maps = 0
    n_mon = 0
    mon_air = 0
    below_cat = Counter(); above_cat = Counter()
    below_stain_colormatch = 0; below_stain_total = 0
    sign_in_slice = 0           # keyword sign within strict 3x3x5
    sign_in_box = 0             # keyword sign within wider box
    sign_any_in_box = 0         # ANY sign (incl non-keyword) within box
    mon_with_armorstand = 0
    sign_offset = Counter()     # (dx,dy,dz) of keyword signs rel. monument
    sign_dy = Counter()         # dy level of keyword signs
    signs_against_below = 0     # monument has a keyword sign at dy in {-1,0} adjacent to a solid block at dy-1 col
    # learned inverse geometry: sign block (id,data) -> Counter of (monument - sign) offset vectors
    face_to_offset = defaultdict(Counter)

    # Phase 2 aggregates: naive (any air near sign) / naive-keyword / data-driven (learned facing)
    det = dict(tp_naive=0,fp_naive=0,fn_naive=0, tp_naivekw=0,fp_naivekw=0,fn_naivekw=0,
               tp_geo=0,fp_geo=0,fn_geo=0)
    per_map_rows = []
    map_mons = {}   # slug -> monuments

    BOX_H, BOX_DY_LO, BOX_DY_HI = 2, -3, 2   # wider descriptive search box

    # =================== PASS 1: descriptive tallies + learn sign→monument geometry ===================
    for slug, rd in maps:
        mons = load_monuments(slug)
        if not mons: continue
        try:
            vol, signs, ents = read_world(rd)
        except Exception as e:
            print(f"  !! {slug}: {type(e).__name__} {e}", file=sys.stderr); continue
        map_mons[slug] = mons
        n_maps += 1; n_mon += len(mons)

        # ---- Phase 1 per monument ----
        for m in mons:
            cx,cy,cz = m["x"],m["y"],m["z"]
            center = vol.get((cx,cy,cz))
            if center is None: mon_air += 1
            below = vol.get((cx,cy-1,cz)); above = vol.get((cx,cy+1,cz))
            bc = bname(below[0]) if below else "air"
            ac = bname(above[0]) if above else "air"
            below_cat[bc]+=1; above_cat[ac]+=1
            if below and below[0]==159:
                below_stain_total+=1
            # signs near monument
            in_slice = in_box = any_box = False
            has_against_below = False
            for (sx,sy,sz,txt,kw) in signs:
                dx,dy,dz = sx-cx, sy-cy, sz-cz
                if abs(dx)<=BOX_H and abs(dz)<=BOX_H and BOX_DY_LO<=dy<=BOX_DY_HI:
                    any_box=True
                    if kw:
                        in_box=True
                        sign_offset[(dx,dy,dz)]+=1
                        sign_dy[dy]+=1
                        sb = vol.get((sx,sy,sz))   # the sign block carries the facing in its data nibble
                        if sb and sb[0] in SIGN_IDS:
                            face_to_offset[(sb[0],sb[1])][(cx-sx,cy-sy,cz-sz)] += 1
                        if abs(dx)<=1 and abs(dz)<=1 and -2<=dy<=2:
                            in_slice=True
                        # "against a block below the monument": sign at the level of / below the
                        # monument block, with a solid block directly below the monument.
                        if dy in (-1,0) and below is not None and below[0] not in SIGN_IDS:
                            has_against_below=True
            if in_slice: sign_in_slice+=1
            if in_box: sign_in_box+=1
            if any_box: sign_any_in_box+=1
            if has_against_below: signs_against_below+=1
            # armour stand (vertical reach): within +-1 horizontal, feet floor in [cy-3, cy+2]
            for (eid,ex,ey,ez) in ents:
                if eid!="ArmorStand": continue
                if abs(int(math.floor(ex))-cx)<=1 and abs(int(math.floor(ez))-cz)<=1 and (cy-3)<=int(math.floor(ey))<=(cy+2):
                    mon_with_armorstand+=1; break

        if n_maps % 50 == 0:
            print(f"  pass1 ...{n_maps} maps", file=sys.stderr)

    # derive the inverse facing geometry per sign block (id,data): the modal (monument − sign) offset
    # (for the report) and the small set of well-supported offsets (≥20% and ≥3 occurrences) the
    # detector actually predicts from — recovers recall where a facing is used with a few offsets.
    best_offset = {}     # (id,data) -> (modal_off, modal_cnt, total)
    offsets_for = {}     # (id,data) -> [offsets to predict]
    for key, ctr in face_to_offset.items():
        off, cnt = ctr.most_common(1)[0]
        tot = sum(ctr.values())
        if cnt >= 3:
            best_offset[key] = (off, cnt, tot)
            offsets_for[key] = [o for o,c in ctr.items() if c >= 3 and c >= 0.2*tot]

    def cluster(cand):
        sites=[]
        for c in sorted(cand):
            for s in sites:
                if chebyshev(c, s[0])<=1: s.append(c); break
            else: sites.append([c])
        return [min(s) for s in sites]

    def evaluate(sites, mon_pts, prefix):
        matched_mon=set(); matched_site=set()
        for i,sp in enumerate(sites):
            for j,mp in enumerate(mon_pts):
                if j in matched_mon: continue
                if chebyshev(sp,mp)<=1: matched_site.add(i); matched_mon.add(j); break
        tp=len(matched_mon); fp=len(sites)-len(matched_site); fn=len(mon_pts)-len(matched_mon)
        det["tp"+prefix]+=tp; det["fp"+prefix]+=fp; det["fn"+prefix]+=fn
        return tp,fp,fn

    # =================== PASS 2: world-only detectors + precision / false positives ===================
    done=0
    for slug, rd in maps:
        mons = map_mons.get(slug)
        if not mons: continue
        try:
            vol, signs, ents = read_world(rd)
        except Exception: continue
        mon_pts = [(m["x"],m["y"],m["z"]) for m in mons]

        def naive(keyword_only):
            cand=set()
            for (sx,sy,sz,txt,kw) in signs:
                if keyword_only and not kw: continue
                for ddx in (-1,0,1):
                    for ddz in (-1,0,1):
                        for ddy in (-1,0,1,2):
                            C=(sx+ddx, sy+ddy, sz+ddz)
                            if vol.get(C) is not None: continue
                            bl=vol.get((C[0],C[1]-1,C[2]))
                            if bl is None or bl[0] in SIGN_IDS: continue
                            cand.add(C)
            return cluster(cand)

        def geo():   # hypothesis-encoded: invert the learned sign-block facing → monument cell
            cand=set()
            for (sx,sy,sz,txt,kw) in signs:
                if not kw: continue
                sb=vol.get((sx,sy,sz))
                if not sb or sb[0] not in SIGN_IDS: continue
                for (dx,dy,dz) in offsets_for.get((sb[0],sb[1]), ()):
                    C=(sx+dx, sy+dy, sz+dz)
                    if vol.get(C) is not None: continue             # predicted cell must be air
                    bl=vol.get((C[0],C[1]-1,C[2]))
                    if bl is None or bl[0] in SIGN_IDS: continue     # pedestal below
                    cand.add(C)
            return cluster(cand)

        evaluate(naive(False), mon_pts, "_naive")
        evaluate(naive(True),  mon_pts, "_naivekw")
        tp,fp,fn = evaluate(geo(), mon_pts, "_geo")
        per_map_rows.append((slug, len(mons), tp, fp, fn))
        done+=1
        if done % 50 == 0:
            print(f"  pass2 ...{done} maps", file=sys.stderr)

    # ------------------------- report -------------------------
    R=[]
    R.append(f"# CTW monument-surround corpus analysis\n")
    R.append(f"Maps analysed: **{n_maps}**  ·  monuments: **{n_mon}**  (PublicMaps/ctw + CommunityMaps/ctw)\n")
    R.append(f"Monument block is air: **{mon_air}/{n_mon}** ({100*mon_air/n_mon:.1f}%)\n")

    R.append("\n## Q1 — How often are signs used?\n")
    R.append(f"- monument has a **keyword** sign in the strict 3×3×5 slice: **{sign_in_slice}/{n_mon}** ({100*sign_in_slice/n_mon:.1f}%)")
    R.append(f"- monument has a **keyword** sign in the wider box (±2 h, dy −3..+2): **{sign_in_box}/{n_mon}** ({100*sign_in_box/n_mon:.1f}%)")
    R.append(f"- monument has **any** sign in the wider box: **{sign_any_in_box}/{n_mon}** ({100*sign_any_in_box/n_mon:.1f}%)")

    R.append("\n## Q2 — Sign placement relative to the monument block\n")
    R.append(f"- keyword sign at the monument level or just below (dy∈{{−1,0}}) **against a solid block below the monument**: **{signs_against_below}/{n_mon}** ({100*signs_against_below/n_mon:.1f}%)  ← the hypothesis")
    R.append("- dy level of keyword signs (relative to monument block):")
    for dy,c in sorted(sign_dy.items()):
        R.append(f"    dy={dy:+d}: {c}")
    R.append("- top (dx,dy,dz) offsets of keyword signs:")
    for off,c in sign_offset.most_common(12):
        R.append(f"    {off}: {c}")

    R.append("\n## Q3 — Block directly BELOW the monument block\n")
    for cat,c in below_cat.most_common(15):
        R.append(f"- {cat}: {c} ({100*c/n_mon:.1f}%)")
    R.append("\n### Block directly ABOVE the monument block\n")
    for cat,c in above_cat.most_common(12):
        R.append(f"- {cat}: {c} ({100*c/n_mon:.1f}%)")

    R.append("\n## Q4 — How often is an armour stand included?\n")
    R.append(f"- monument with an armour stand nearby (±1 h, reach): **{mon_with_armorstand}/{n_mon}** ({100*mon_with_armorstand/n_mon:.1f}%)")

    R.append("\n## Learned inverse geometry (sign block → monument offset)\n")
    R.append("Modal (monument − sign) vector per sign block (id,data), with support:")
    for (bid,bdat),(off,cnt,tot) in sorted(best_offset.items(), key=lambda kv:-kv[1][2]):
        nm = "wall_sign" if bid==68 else "sign_post" if bid==63 else f"id{bid}"
        R.append(f"- {nm}(data={bdat}): monument at {off}  ({cnt}/{tot} agree)")

    R.append("\n## Phase 2 — World-only monument detection (precision / recall / false positives)\n")
    def prf(tp,fp,fn,label):
        prec = tp/(tp+fp) if tp+fp else 0
        rec = tp/(tp+fn) if tp+fn else 0
        R.append(f"- **{label}**: TP={tp} FP={fp} FN={fn}  ·  precision={100*prec:.1f}%  recall={100*rec:.1f}%")
    prf(det["tp_geo"],det["fp_geo"],det["fn_geo"],"data-driven: keyword sign + learned facing (the hypothesis)")
    prf(det["tp_naivekw"],det["fp_naivekw"],det["fn_naivekw"],"naive: any air-on-pedestal near a keyword sign")
    prf(det["tp_naive"],det["fp_naive"],det["fn_naive"],"naive: any air-on-pedestal near ANY sign (no text filter)")

    per_map_rows.sort(key=lambda r:-r[3])
    R.append("\n### Maps with the most false positives (data-driven detector): slug (mons, TP, FP, FN)")
    for slug,nm,tp,fp,fn in per_map_rows[:15]:
        R.append(f"- {slug}: ({nm}, {tp}, {fp}, {fn})")

    print("\n".join(R))
    # also dump machine-readable
    json.dump(dict(n_maps=n_maps,n_mon=n_mon,mon_air=mon_air,
                   below=dict(below_cat),above=dict(above_cat),
                   sign_in_slice=sign_in_slice,sign_in_box=sign_in_box,sign_any_in_box=sign_any_in_box,
                   signs_against_below=signs_against_below,sign_dy=dict(sign_dy),
                   sign_offset={f"{k[0]},{k[1]},{k[2]}":v for k,v in sign_offset.items()},
                   armorstand=mon_with_armorstand, detector=det),
              open("/tmp/monument_analysis.json","w"), indent=2)

if __name__=="__main__":
    lim = int(sys.argv[1]) if len(sys.argv)>1 else None
    main(lim)
