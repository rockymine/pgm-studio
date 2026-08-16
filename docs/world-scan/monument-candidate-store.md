# The monument candidate store — suggestion as a query, not a world read

Monument suggestion runs at authoring time, and at authoring time there is no world to read. This is why
`MonumentSuggester` is split in two — a **gather** pass that needs the `.mca` and runs once at ingest, and a
**score** pass that is pure and runs per request against `monument_candidate` rows — and what the gathered
rows are allowed to contain.

Read alongside:
- `monument-suggestion.md` — the detector this factors: what it anchors on, what it emits, and the corpus
  numbers it is held to.
- `monument-patterns.md` — the corpus study the anchors and the allowlist come out of.
- `../tools/configure.md` — the endpoints that serve it, in the tool that consumes them.
- `../pgm/region-data-flow.md` — why derived data lives in MariaDB. This table is another derived projection
  of the world, persisted so the authoring host never touches world files.

## 1. Why a store at all

The end state is a hosted tool: on the server where the map is built, the author types an in-game command, a
plugin saves and zips the region files, uploads them, and posts back a link to author the map. For that the
web tier has to be **stateless** — no mounted `.mca` corpus — and monument suggestion is the hardest of the
three operations that used to read a world at runtime, because `layer_segment`/`layer.parquet` cannot drive
it: they carry no block materials, no signs and no entities.

The fix is the shape the rest of the data model already uses: **process the world once at ingest, persist the
derived result, query it at runtime.** For monuments the derived result is a small set of candidate cells with
their evidence — dozens of rows per map, not the whole world.

**A table rather than a parquet**, because the access pattern is the opposite of `layer.parquet`'s. Candidates
are sparse and queried *by predicate* — a spatial box filter, a source or colour filter, a join to the map —
where a dense per-column grid is read whole. That is `CLAUDE.md`'s hybrid rule exactly: real tables for what is
listed and queried, blobs for what is read entire.

## 2. Gather and score

| Phase | Does | Needs the world | Runs |
|---|---|---|---|
| **Gather** | reads the region files, finds anchors — monument-label wall signs, wool-head or named armour stands, wool item frames — projects each to a candidate **air cell**, and captures the surrounding evidence: pedestal and cap ids, sign text and facing, stand payload | **yes** | ingest, once |
| **Score** | per candidate, applies the author's declared style — `PedestalMatches`/`CapMatches`, colour from the stain below then above then the hint, confidence, the cell-merge and agreeing-sign boost, the ranking | no | authoring, per call |

The identity that keeps the split honest is that it is a **factoring, not a behaviour change**:

```
Suggest(chunks, box, style)  ==  Score(Gather(chunks, box.Expand(2)), box, style)
```

`Suggest` remains as exactly that composition, so the corpus harness (`--suggest-monuments`,
`--suggest-monuments-corpus`) still guards the combined behaviour and its precision/recall numbers mean the
same thing they did before the split.

**Gather is style-agnostic**, and it has to be: the declared style is not known at ingest. It runs every
anchor type, accepts any pedestal or cap, and stores the raw below/above ids and data so `Score` can apply the
author's `MonumentStyle` later. Which anchors a declared label admits is a filter on the stored `source` at
score time, not a branch at gather time.

## 3. What a row holds, and what is deliberately not gathered

One row **per cell**, keeping the strongest anchor — armour stand over sign over geometry. Several wall signs
ringing one monument all project to the same air cell (pigland places four against each block), and a monument
is often marked by both a stand and a sign there. `Score` cell-merges anyway and the stand always scores at
least the sign, so storing the duplicates only bloats the table: pigland's 64 sign emissions collapse to
**8** candidates, four stand cells and four sign cells.

The columns are what `Score` consumes and nothing it can recompute (`MonumentCandidateRow`,
`M0002_MonumentCandidate`):

| Column | Consumed by |
|---|---|
| `cand_x/y/z` | the box filter, the cell-merge key, the output position |
| `source` | confidence; the label filter (`sign` · `armorstand` · `itemframe` · `geometry`) |
| `pedestal_id/data` · `cap_id/data` | `PedestalMatches`/`CapMatches`, and the colour read off the stain |
| `color_hint` | the colour fallback when neither stain carries one |
| `sign_x/y/z` · `sign_facing` · `sign_text` | the output, the evidence line, and a re-derivable colour |
| `stand_head_color` · `stand_name` | armour-stand colour and evidence |
| `evidence` | the human-readable note, including a colour conflict |

What is **not** stored is what depends on the style the author has not declared yet: the confidence, the
resolved colour, and the pass or fail of the pedestal/cap/label filter.

**Two pedestals are dropped at gather, because `Score` could never accept them.** A **sign** pedestal (wall 68
or post 63) is never a pedestal — `PedestalMatches` rejects both for `Pedestal.Any`, and no specific
`PedestalKind` maps to them — so those rows are provably dead storage; dropping them takes thunder from 24
candidates to **12**, exactly its twelve real bedrock monuments. A **barrier** pedestal (166) is never real
either (0 of 593 in the corpus; a barrier appears only as a *cap*, 78 times — pigland caps its glass-pedestal
monuments with a signed barrier), so an air cell above one is a deliberately blocked, unreachable spot: the
phantom a barrier-mounted sign's "beside, monument-above" placement projects onto the cap. That takes pigland
from 8 to **4**, exactly its four real stand monuments. Both are **zero real-monument loss**.

The related intuition — solid pedestal with air directly below means too high to place — is deliberately *not*
used as a blanket rule: 28 of 541 real solid-pedestal monuments are legitimately raised that way. The
barrier-pedestal rule is the precise, loss-free version of it.

**A wall sign emits two placement families**, and only one of them is unconditional. *Beside* — the sign faces
the monument — is always tried. *In-column* — the sign sits in the monument's own column, nutrient's
"v WOOL v" cap — is emitted **only when the sign's column has a solid block within ±2**, because a real
in-column monument has a pedestal there (16 of 16) while wool signs that merely ring a monument from open air
float (0 of 16) and would store noise. Every nutrient-style monument survives; pigland drops from 44
candidates to 12.

**The wool item frame is the fourth anchor, and it needs a structural test rather than a material one.** A
frame holding a wool item marks a monument, the framed wool's damage value giving the colour, and it mounts on
a vertical face of either the pedestal (monument above, a_new_day) or the cap (monument below,
golden_drought_iii's sign-and-frame in one block). The catch is that of 17 maps with wool frames only about
six use them as indicators — the other 116 of 138 frames are decoration: molcein's 40-frame colour palette,
mist's 22 on a floating slab, mame's black-wool "frog eyes". Wool-only is necessary and not sufficient, so the
**pocket test** is what excludes the décor: emit only where the cell is air over a solid pedestal that is
either capped or itself grounded at least three blocks deep. Corpus: **20 of 20 real frame cells, no false
positives**, taking the whole suggester to 1010 true positives and 935 colour-correct with false positives
unchanged at 35 — and, because a frame candidate sets `anchored`, killing the geometry flood on those maps
(a_new_day 186 → 4, a_new_day_ii 171 → 4).

## 4. Two rules the gather pass is held to

**Bound the geometry pass — it is a genuine last resort.** The `LabelKind.None` fallback iterates every
non-air block, which is fine over an author's box and catastrophic over a whole world: untuned, thunder
gathered **2193** candidates, about 99% of them exposed stained-clay terrain. Two bounds fix it, both
corpus-validated at **zero real-monument loss** over 593 monuments across 145 maps.

*Skip geometry entirely when the map has anchors* — an `IsMonumentLabel` sign, or a wool-head or named armour
stand. Geometry is only ever scored for `Label=None`, which no author declares on a labelled map. This alone
takes thunder 2193 → 24 and pigland 258 → 68.

*For a genuinely label-free map, recognise the unsigned-monument allowlist* — a distinctive pedestal
(bedrock, clay, glass or wool) under a colour-or-marker cap (stained glass, stained clay, wool or barrier:
three encode the colour and the fourth marks the spot). These are the **14 pedestal×cap combinations real
label-free monuments actually use** (662 monuments, 38% of the corpus; lupain is bedrock under glass), and the
tightness is deliberate — slab, sign and bedrock caps are terrain-ambiguous, and a single distinctive signal
alone measured 0.27% precision. Two accessibility filters sit on top: the cell needs **at least one air
horizontal neighbour**, or it is a sealed pocket in terrain rather than a placeable monument (99.7% of real
ones have an open side); and a stained-clay pedestal with three or more same-clay neighbours among its eight
is a clay *floor*, not a pedestal (real clay pedestals have at most two, and a general mass rule would kill
about 1.3% of real monuments, so this one is scoped to clay).

Measured on `--label None`: **191 true positives, 5 false positives, 97.4% precision, 93.7% colour-correct** —
trading roughly 66 slab/bedrock-cap recall for far better colour, since every allowed cap carries one. The
storage effect on the 76 unanchored maps is the point: dreamland 5859 → 311, fall_of_babylon 5035 → 40,
thundershock 240 → 12, molcein 216 → 0, lupain 52 → 2. Anchored maps and the `Label=Any` path are unchanged.

**The box is the mode.** The author knows where they built the monument, so suggestion is box-scoped: they
mark the area, optionally declare the style, and `Score` filters the pre-gathered candidates to that box and
ranks them. Showing every candidate on the map is explicitly not the model — it is noise. Gather stays
whole-world because the box is not known at ingest, and the box keeps a small expand margin (the live path's
two blocks) so an anchor projected to a cell at the edge still resolves.

## 5. The orbit is a different question, on a different store

The author builds and boxes the monuments for **one** symmetry orbit unit — their own team's wool. The other
teams' monuments are its images, so once the boxed suggestions are confirmed, a second request reflects or
rotates each confirmed position onto every other team: `Geom.Symmetry` on **XZ only**, since symmetry is
horizontal, with `rot_90` yielding three counterparts and `mirror_*`/`rot_180` one, and each counterpart's
capturing team shifting by the orbit step.

**That request reads no candidates.** Orbit operates on the confirmed positions and the map's symmetry, which
is pure geometry: the candidate table answers *where did the author place it*, and symmetry answers *where are
its mirrors*. Two questions, two stores, and keeping them apart is what makes the authoring-time orbit a true
preview of what `SymmetryExpander` will emit at generate time rather than a second implementation of it
(`../pgm/new-map-authoring.md` §2).

## 6. The ingest side, and the half of it that is not built

Gather runs in the same once-per-upload worker that already scans the world (`WorldFeatureWriter`), one extra
pass over a chunk stream it has already decoded, so it costs little beyond what the scan pays. Rows are
map-scoped and cascade-delete with the map, written delete-then-insert so a re-gather is idempotent — which
means a detector improvement is re-run as a worker job over the retained world zip rather than as a re-upload.

**The hosted flow around it is the design, not the tree.** There is no plugin: the in-game command, the
save-zip-upload client and the posted link are what the split was done *for*, and two decisions are recorded
against the day they get built. The plugin is an **upload client, not a parser** — it saves, zips and uploads,
and every bit of decoding stays in the C# worker, so `MonumentSuggester` remains the one implementation of the
detector rather than growing a Java twin. And it must **flush the world before zipping**, since a live server
holds chunks in memory and freshly placed monuments and signs would otherwise be missing from the `.mca` it
uploads.

## 7. What is open

**Recall is still bounded by labelling**, not by this table — the store changes where and when detection runs,
never how well it detects. Packed-cluster mis-attribution (a sign at `dy=0` beside a neighbour) carries over
unchanged, and the author corrects it on confirm.

**Per-map detection parameters have no home yet.** Detection is global-constant today; if a tuner ever wants
per-map thresholds they belong in `map_config_json`, not here, because this table stores results rather than
settings.

**Snapping a confirmed suggestion onto buildable ground** using `layer_segment` is downstream of this and
independent of it.
