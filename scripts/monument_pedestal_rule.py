#!/usr/bin/env python3
"""Validate the "freestanding pedestal" rule against the corpus (follow-up to F9 / the
monument-candidate-store §4.1 geometry false positives — exposed stained-clay terrain).

Hypothesis (from looking at pigland/thunder in-game): a REAL monument's pedestal is always a bit
freestanding / accessible — it has at least one AIR or SIGN block among its horizontal neighbours, so
players can see and mark it. A clay block buried in the ground with only open sky above is terrain.

So the rule "BURIED pedestal (no air/sign neighbour) AND >=3 sky-air above => terrain, drop it" should
drop a HEAP of geometry FPs while (almost) never dropping a real monument. This script measures both.

Reuses read_world / load_monuments from monument_corpus_analysis.py. Phase 2 reads the gathered
geometry candidates for thunder + pigland from /tmp/geom_<slug>.tsv (cand_x cand_y cand_z, tab-sep).
"""
import sys, os, glob
from collections import Counter
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from monument_corpus_analysis import read_world, load_monuments, bname, ROOTS

SIGN_IDS = {63, 68}
N4 = [(1, 0), (-1, 0), (0, 1), (0, -1)]
N8 = N4 + [(1, 1), (1, -1), (-1, 1), (-1, -1)]


def region_dir(slug):
    for r in ROOTS:
        d = os.path.join(r, slug, "region")
        if os.path.isdir(d):
            return d
    return None


def cell_features(vol, cx, cy, cz):
    """(pedestal_id, air_above, free4, free8) for an air cell at (cx,cy,cz).
       free4/free8 = the pedestal has an AIR or SIGN block among its 4 / 8 horizontal neighbours."""
    ped = vol.get((cx, cy - 1, cz))
    ped_id = ped[0] if ped else 0
    air_above = 0
    for dy in range(1, 6):
        if (cx, cy + dy, cz) in vol:
            break
        air_above += 1

    def free(neigh):
        for dx, dz in neigh:
            b = vol.get((cx + dx, cy - 1, cz + dz))
            if b is None or b[0] in SIGN_IDS:   # air OR sign neighbour → accessible / marked
                return True
        return False

    return ped_id, air_above, free(N4), free(N8)


def same_material_neighbours(vol, cx, cy, cz, mat):
    """How many of the pedestal's 8 horizontal neighbours are the SAME material (a clay block in a clay
    mass has many; an isolated pedestal has few)."""
    return sum(1 for dx, dz in N8
               if (b := vol.get((cx + dx, cy - 1, cz + dz))) is not None and b[0] == mat)


# ── Phase 1: real monuments across the corpus ───────────────────────────────────────────────
slugs = sorted({os.path.basename(os.path.dirname(d))
                for r in ROOTS for d in glob.glob(os.path.join(r, "*", "region"))})
limit = int(sys.argv[1]) if len(sys.argv) > 1 else None

maps = reals = 0
ped_mat = Counter()
clay = clay_sky3 = 0
free4 = free8 = 0
buried4_any = buried8_any = 0           # reals with a buried pedestal regardless of sky
rej4_3 = rej8_3 = rej4_2 = rej8_2 = 0   # would the rule drop a REAL monument? (buried{4,8} AND >={2,3} air)
buried_real_air = Counter()             # air_above distribution of the BURIED real pedestals
clay_real_n8 = Counter()                # same-clay-neighbour count for REAL clay-pedestal monuments
clay_real_air = Counter()
real_same_n8 = Counter()                # same-MATERIAL-neighbour count for ALL reals (is isolation general?)
real_mass_open = 0                      # reals that would die to a general "in a mass AND open sky" rule

for slug in (slugs[:limit] if limit else slugs):
    mons = load_monuments(slug)
    rd = region_dir(slug)
    if not mons or not rd:
        continue
    try:
        vol, signs, ents = read_world(rd)
    except Exception:
        continue
    maps += 1
    for m in mons:
        reals += 1
        ped_id, aa, f4, f8 = cell_features(vol, m["x"], m["y"], m["z"])
        ped_mat[bname(ped_id)] += 1
        sn8 = same_material_neighbours(vol, m["x"], m["y"], m["z"], ped_id) if ped_id else 0
        real_same_n8[min(sn8, 8)] += 1
        if sn8 >= 3 and aa >= 2:
            real_mass_open += 1
        if ped_id == 159:
            clay += 1
            clay_sky3 += aa >= 3
            clay_real_n8[same_material_neighbours(vol, m["x"], m["y"], m["z"], 159)] += 1
            clay_real_air[min(aa, 5)] += 1
        free4 += f4
        free8 += f8
        buried4_any += not f4
        buried8_any += not f8
        if not f4:
            buried_real_air[min(aa, 5)] += 1
        rej4_3 += (not f4) and aa >= 3
        rej8_3 += (not f8) and aa >= 3
        rej4_2 += (not f4) and aa >= 2
        rej8_2 += (not f8) and aa >= 2

pct = lambda n: f"{n} ({100 * n / reals:.2f}%)" if reals else "0"
print(f"=== REAL monuments: {reals} over {maps} maps ===")
print("pedestal material:", ped_mat.most_common(12))
print(f"stained_clay pedestal: {pct(clay)}   |   clay AND >=3 sky-air above: {pct(clay_sky3)}")
print(f"freestanding pedestal (air/sign neighbour) — 4-face: {pct(free4)}   8-neigh: {pct(free8)}")
print(f"buried pedestal (ANY, ignoring sky)        — buried4: {pct(buried4_any)}   buried8: {pct(buried8_any)}")
print(f"WOULD-REJECT a REAL (buried AND >=3 air)  — buried4: {pct(rej4_3)}   buried8: {pct(rej8_3)}")
print(f"WOULD-REJECT a REAL (buried AND >=2 air)  — buried4: {pct(rej4_2)}   buried8: {pct(rej8_2)}")
print(f"buried REAL pedestals — air_above dist {dict(sorted(buried_real_air.items()))}  (key: do ANY reach 2+ air?)")
print(f"REAL clay-pedestal monuments ({clay}) — same-clay-8-neigh dist {dict(sorted(clay_real_n8.items()))} | air_above dist {dict(sorted(clay_real_air.items()))}")
print(f"ALL reals — same-material-8-neigh dist {dict(sorted(real_same_n8.items()))}  | reals killed by general 'mass(>=3 same)+open(>=2 air)': {pct(real_mass_open)}")

# ── Phase 2: how much each candidate rule variant drops of the gathered geometry FPs ─────────
print("\n=== geometry-FP drop by rule variant (thunder + pigland) ===")
for slug in ("thunder", "pigland"):
    tsv = f"/tmp/geom_{slug}.tsv"
    rd = region_dir(slug)
    if not os.path.exists(tsv) or not rd:
        print(f"  {slug}: (no {tsv} / world)")
        continue
    vol, _, _ = read_world(rd)
    cells = [tuple(map(int, ln.split())) for ln in open(tsv) if ln.strip()]
    n = len(cells) or 1
    combined = freeonly = noclay = either = 0   # # dropped under each rule
    for cx, cy, cz in cells:
        ped_id, aa, f4, _ = cell_features(vol, cx, cy, cz)
        d_combined = (not f4) and aa >= 3       # user's rule: buried AND open sky
        d_freeonly = not f4                     # stronger: drop any buried pedestal
        d_noclay = ped_id == 159                # drop stained-clay pedestal entirely
        combined += d_combined
        freeonly += d_freeonly
        noclay += d_noclay
        either += d_combined or d_noclay        # buried+sky OR clay pedestal
    print(f"  {slug}: {len(cells)} geometry FPs")
    print(f"     buried4 AND >=3 air (your rule): {combined} ({100*combined/n:.0f}%)")
    print(f"     buried4 (any, drop buried)     : {freeonly} ({100*freeonly/n:.0f}%)")
    print(f"     stained_clay pedestal          : {noclay} ({100*noclay/n:.0f}%)")
    print(f"     (your rule) OR clay pedestal   : {either} ({100*either/n:.0f}%)")

    # deep dive: the clay FPs that SURVIVE the combined rule — what do they share?
    kept_air = Counter(); kept_n8 = Counter(); kept_free4 = 0; kept_total = 0; kept_capped = 0
    for cx, cy, cz in cells:
        ped_id, aa, f4, _ = cell_features(vol, cx, cy, cz)
        if ped_id != 159 or ((not f4) and aa >= 3):   # only clay FPs kept by the combined rule
            continue
        kept_total += 1
        kept_air[min(aa, 5)] += 1
        kept_n8[same_material_neighbours(vol, cx, cy, cz, 159)] += 1
        kept_free4 += f4
        kept_capped += aa < 3
    if kept_total:
        print(f"     surviving CLAY FPs: {kept_total} — same-clay-8-neigh dist {dict(sorted(kept_n8.items()))}")
        print(f"        air_above dist {dict(sorted(kept_air.items()))} | freestanding {kept_free4}/{kept_total} | capped(<3 air) {kept_capped}/{kept_total}")
