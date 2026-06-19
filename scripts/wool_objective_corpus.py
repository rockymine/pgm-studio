#!/usr/bin/env python3
"""Cross-reference physically-present wool (wool_block + chest_item) against declared
objective wools, and validate island-membership owner inference, across the corpus.

Grounds the N04 Wools authoring step (docs/contracts/new-map-authoring.md §5):
  1. decorative vs objective  — how much physical wool is NOT an objective (must be rejectable)?
  2. detection gaps           — objectives with no physical wool block (must allow manual add)?
  3. chest-dispensed wool     — is wool-in-chests a useful objective signal?
  4. island -> owner          — does the island a wool sits on identify its defender?
                                (owner ground-truth = the team NOT capturing it, from monuments)

Reads the populated MariaDB via the mysql CLI (no connector dep). Dev creds only.
"""
import json, subprocess
from collections import defaultdict

DB = ["mysql", "--user=pgm", "--password=pgm_dev_pw", "--host=127.0.0.1", "pgm_studio"]

WOOL_DMG = {0: "white", 1: "orange", 2: "magenta", 3: "light_blue", 4: "yellow", 5: "lime",
            6: "pink", 7: "gray", 8: "silver", 9: "cyan", 10: "purple", 11: "blue",
            12: "brown", 13: "green", 14: "red", 15: "black"}


def norm(c):
    return (c or "").strip().lower().replace(" ", "_")


def q(sql):
    """Batch query -> list of column-lists (tab-split). Compact values only (no embedded newlines)."""
    out = subprocess.run(DB + ["-N", "-B", "-e", sql], capture_output=True, text=True).stdout
    return [line.split("\t") for line in out.splitlines() if line]


def raw(sql):
    """Unescaped single-value fetch (for the multi-line islands_json blob)."""
    return subprocess.run(DB + ["-N", "--raw", "-e", sql], capture_output=True, text=True).stdout


def point_in_ring(x, z, ring):
    """Ray-casting point-in-polygon over a [[x,z],...] exterior ring."""
    inside = False
    n = len(ring)
    j = n - 1
    for i in range(n):
        xi, zi = ring[i][0], ring[i][1]
        xj, zj = ring[j][0], ring[j][1]
        if ((zi > z) != (zj > z)) and (x < (xj - xi) * (z - zi) / (zj - zi + 1e-12) + xi):
            inside = not inside
        j = i
    return inside


# ── gather ────────────────────────────────────────────────────────────────────
maps = {int(i): s for i, s in q("SELECT id, slug FROM map")}
teams = defaultdict(set)
for mid, tk in q("SELECT map_id, team_key FROM team"):
    teams[int(mid)].add(norm(tk))

wools = defaultdict(list)   # map_id -> [ {id,color,team,x,z} ]
for mid, wid, color, loc, team in q(
        "SELECT map_id, id, color, COALESCE(location_json,''), COALESCE(team,'') FROM wool"):
    x = z = None
    if loc:
        try:
            d = json.loads(loc); x, z = d.get("x"), d.get("z")
        except Exception:
            pass
    wools[int(mid)].append({"id": int(wid), "color": norm(color), "team": norm(team), "x": x, "z": z})

capturers = defaultdict(set)   # wool_id -> set(team)
for wid, team in q("SELECT wool_id, team FROM monument"):
    capturers[int(wid)].add(norm(team))

phys = defaultdict(dict)        # map_id -> {color: count}
for mid, color, n in q("SELECT map_id, color, COUNT(*) FROM wool_block GROUP BY map_id, color"):
    phys[int(mid)][norm(color)] = int(n)

chest = defaultdict(dict)       # map_id -> {color: count}
for mid, dmg, n in q("SELECT map_id, item_damage, COUNT(*) FROM chest_item "
                     "WHERE item_id LIKE '%wool%' GROUP BY map_id, item_damage"):
    c = WOOL_DMG.get(int(dmg))
    if c:
        chest[int(mid)][c] = chest[int(mid)].get(c, 0) + int(n)


def owner_of(mid, w):
    """Defender of a wool: explicit team if set, else the single team not capturing it."""
    if w["team"]:
        return w["team"]
    rest = teams[mid] - capturers.get(w["id"], set())
    return next(iter(rest)) if len(rest) == 1 else None


# ── analyse ───────────────────────────────────────────────────────────────────
obj_map_ids = [mid for mid in maps if wools.get(mid)]
N = len(obj_map_ids)

stat = {
    "maps": N, "obj_wools": 0,
    "dec_maps": 0, "dec_colors": 0,         # decorative = physical color not an objective
    "gap_block": 0, "gap_block_chest": 0,   # objective color absent from block / from block+chest
    "obj_in_chest": 0,                       # objective wools whose color is dispensed in a chest
    "wool_has_loc": 0, "wool_in_island": 0,  # owner/island geometry
    "owner_known": 0,
    "iso_maps": 0, "iso_inconsistent": 0,    # island->owner consistency per map
}
dec_hist = defaultdict(int)   # #decorative colors -> #maps
gap_examples, inconsistent_examples = [], []
# island->owner consistency split by team count, and how many islands the wools span
iso_by_teamcount = defaultdict(lambda: [0, 0])   # nteams -> [consistent, total]
span_hist = defaultdict(int)                      # #islands hosting wools -> #maps
single_island_inconsistent = 0                    # inconsistent AND all wools on one island

for mid in obj_map_ids:
    ws = wools[mid]
    stat["obj_wools"] += len(ws)
    obj_colors = {w["color"] for w in ws if w["color"]}
    pcolors = set(phys.get(mid, {}))
    ccolors = set(chest.get(mid, {}))

    decorative = pcolors - obj_colors
    dec_hist[len(decorative)] += 1
    if decorative:
        stat["dec_maps"] += 1
        stat["dec_colors"] += len(decorative)

    for c in obj_colors:
        if c not in pcolors:
            stat["gap_block"] += 1
            if c not in ccolors:
                stat["gap_block_chest"] += 1
                if len(gap_examples) < 12:
                    gap_examples.append((maps[mid], c))
    stat["obj_in_chest"] += sum(1 for w in ws if w["color"] in ccolors)

    # island -> owner: load islands once per map
    blob = raw(f"SELECT data FROM map_artifact WHERE map_id={mid} AND kind='islands_json'")
    islands = []
    if blob.strip():
        try:
            for isl in json.loads(blob):
                ring = isl.get("polygon", {}).get("coordinates", [[]])[0]
                if ring:
                    islands.append((isl["id"], ring))
        except Exception:
            islands = []

    island_owners = defaultdict(set)
    has_geom = bool(islands)
    if has_geom:
        stat["iso_maps"] += 1
    for w in ws:
        owner = owner_of(mid, w)
        if owner:
            stat["owner_known"] += 1
        if w["x"] is None:
            continue
        stat["wool_has_loc"] += 1
        hit = next((iid for iid, ring in islands if point_in_ring(w["x"], w["z"], ring)), None)
        if hit is not None:
            stat["wool_in_island"] += 1
            if owner:
                island_owners[hit].add(owner)
    # consistent iff no island hosts wools of two different owners
    if has_geom:
        nteams = len(teams[mid])
        span_hist[len(island_owners)] += 1
        bad = {iid: o for iid, o in island_owners.items() if len(o) > 1}
        iso_by_teamcount[nteams][1] += 1
        if bad:
            stat["iso_inconsistent"] += 1
            if len(island_owners) <= 1:
                single_island_inconsistent += 1
            if len(inconsistent_examples) < 12:
                inconsistent_examples.append((maps[mid], nteams, dict(bad)))
        else:
            iso_by_teamcount[nteams][0] += 1


# ── report ────────────────────────────────────────────────────────────────────
def pct(a, b):
    return f"{100*a/b:.1f}%" if b else "n/a"

print(f"\n{'='*70}\nWOOL OBJECTIVE CORPUS ANALYSIS  ({N} maps with declared wools, {stat['obj_wools']} objective wools)\n{'='*70}")

print("\n1. DECORATIVE vs OBJECTIVE  (physical wool colours that are NOT objectives)")
print(f"   maps with >=1 decorative wool colour : {stat['dec_maps']}/{N}  ({pct(stat['dec_maps'], N)})")
print(f"   total decorative colours              : {stat['dec_colors']}  (avg {stat['dec_colors']/N:.2f}/map)")
print("   distribution (#decorative colours -> #maps):")
for k in sorted(dec_hist):
    print(f"      {k:2d} decorative -> {dec_hist[k]:3d} maps")

print("\n2. DETECTION GAPS  (objective colour with no physical source)")
print(f"   objectives absent from wool_block        : {stat['gap_block']}/{stat['obj_wools']}  ({pct(stat['gap_block'], stat['obj_wools'])})")
print(f"   objectives absent from block AND chest   : {stat['gap_block_chest']}/{stat['obj_wools']}  ({pct(stat['gap_block_chest'], stat['obj_wools'])})  <- must be hand-added")
if gap_examples:
    print("   examples (slug, colour):", ", ".join(f"{s}:{c}" for s, c in gap_examples))

print("\n3. CHEST SIGNAL  (objective wool dispensed in a chest)")
print(f"   objective wools whose colour is in chests: {stat['obj_in_chest']}/{stat['obj_wools']}  ({pct(stat['obj_in_chest'], stat['obj_wools'])})")

print("\n4. ISLAND -> OWNER INFERENCE")
print(f"   objective wools with a location          : {stat['wool_has_loc']}/{stat['obj_wools']}  ({pct(stat['wool_has_loc'], stat['obj_wools'])})")
print(f"   ... that fall inside an island polygon   : {stat['wool_in_island']}/{stat['wool_has_loc']}  ({pct(stat['wool_in_island'], stat['wool_has_loc'])})")
print(f"   owner derivable (monument-complement)    : {stat['owner_known']}/{stat['obj_wools']}  ({pct(stat['owner_known'], stat['obj_wools'])})")
print(f"   maps with island geometry tested         : {stat['iso_maps']}")
print(f"   ... where island->owner is CONSISTENT    : {stat['iso_maps']-stat['iso_inconsistent']}/{stat['iso_maps']}  ({pct(stat['iso_maps']-stat['iso_inconsistent'], stat['iso_maps'])})")
print(f"   ... inconsistent where ALL wools share one island: {single_island_inconsistent}/{stat['iso_inconsistent']}  (islands can't separate teams at all)")
print("   consistency by team count (consistent/total):")
for nt in sorted(iso_by_teamcount):
    c, t = iso_by_teamcount[nt]
    print(f"      {nt}-team -> {c}/{t}  ({pct(c, t)})")
print("   #islands the wools span -> #maps:")
for k in sorted(span_hist):
    print(f"      {k:2d} island(s) -> {span_hist[k]:3d} maps")
if inconsistent_examples:
    print("   inconsistent examples (slug, #teams, island->owners):")
    for s, nt, bad in inconsistent_examples:
        print(f"      {s} ({nt}t): {bad}")


# ── Cobalt Crossing worked example ──────────────────────────────────────────────
cc = next((mid for mid, s in maps.items() if s == "n00_demo"), None)
if cc:
    print(f"\n{'='*70}\nWORKED EXAMPLE — n00_demo (Cobalt Crossing)\n{'='*70}")
    print(f"   teams            : {sorted(teams[cc])}")
    print(f"   physical wool    : {phys.get(cc, {})}")
    print(f"   chest wool       : {chest.get(cc, {}) or '(none)'}")
    print(f"   declared objectives: {[w['color'] for w in wools.get(cc, [])] or '(none yet)'}")
    print(f"   decorative (phys not objective): {sorted(set(phys.get(cc, {})) - {w['color'] for w in wools.get(cc, [])})}")
