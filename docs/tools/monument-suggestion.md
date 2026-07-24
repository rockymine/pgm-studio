# Monument suggestion (world → monument positions)

The authoring-flow **"which monument style? + box"** extractor: from a world, a box the author
draws, and a declared monument style, suggest monument block positions with inferred wool colours,
for the author to confirm. It is the contract for the **Monuments** step of the Wools activity
inside Configure (`docs/tools/configure.md` §Wools). Backend: `MonumentSuggester`
(`PgmStudio.Minecraft`). **Status: backend complete, corpus-validated; UI not built** — this is a
known gap, not resolved (see §5).

Sources consolidated here: the corpus pattern study (formerly `docs/monument-patterns.md`, no code),
the detection spec (formerly `docs/contracts/monument-suggestion.md`), and the candidate-store design
(formerly `docs/contracts/monument-candidate-store.md`, design only). Pattern-study scripts live in
`scripts/`.

---

## 1. What this subsystem is

Given the world (Anvil region chunks), a `ScanBox` (the region the author boxes around the
monument area), and a `MonumentStyle`, return ranked `MonumentSuggestion`s — each a predicted
**air** monument block, an inferred wool colour, a confidence, and the evidence it came from. The
author confirms/places them (a monument is air on a well-formed map: PGM marks the wool "placed"
when an objective wool block appears in the placement region).

**Why a box + a style.** Scoping to the boxed monument area keeps the scan cheap and removes
off-site signage (team barriers, wool-room and lobby signs) that would otherwise produce phantom
hits. Declaring the style lets the detector require a specific signature — the main lever for
precise suggestions.

Intended UI flow (Monuments step):

1. Author **boxes the monument area** on the canvas → `ScanBox` (one call per boxed group).
2. Author picks the **style** — three dropdowns (pedestal / label / cap), defaulting to `Any`.
3. Run → suggestions render as **ghost monument markers** (colour + confidence + evidence on
   hover); the author **confirms** (places the monument block, the capturing team derived by orbit
   per the Wools flow) or **dismisses**. Low-confidence (`None`/geometry) suggestions are shown
   distinctly for a quick confirm.

---

## 2. The corpus pattern study

Behind the detector is a corpus analysis: **345 maps · 1723 monuments** (4 maps skipped —
non-1.8 chunk format), ground truth = resolved monument `<block>` coords from `xml_data.json`.
Reproduce with `scripts/monument_corpus_analysis.py`, `scripts/sign_text_analysis.py`,
`scripts/scoped_analysis.py` (need the `nbt` lib in `/root/ctw-venv`; they read the
`PublicMaps/ctw` + `CommunityMaps/ctw` worlds and `pgm-map-studio-output/*/xml_data.json`).

### How authors build monuments

**The monument block is air.** 1701 / 1723 (98.7%) monument blocks are air — PGM's
placement-region convention (the wool is marked "placed" when an objective wool block appears in
the region). The 1.3% exceptions are region-resolved / pre-filled centres; treat "centre is air" as
a strong but not absolute prior.

**Q1 — signs label ~two-thirds of monuments.**

| scope | monuments with a keyword sign |
|---|---|
| strict 3×3×5 slice (±1 h, ±2 v) | 1162 / 1723 (67.4%) |
| wider box (±2 h, dy −3..+2) | 1166 / 1723 (67.7%) |
| any sign (no text filter), wider box | 1176 / 1723 (68.3%) |

A monument's sign is always immediately adjacent — widening the search barely changes the count.
~1/3 of monuments carry no sign, which caps any sign-based detector at ≈68% recall.

**Q2 — sign placement relative to the monument block.**

| dy (sign − monument) | keyword signs |
|---|---|
| −1 (mounted on the block below the monument) | 1333 |
| +1 (mounted on the block above) | 844 |
| 0 (same level) | 194 |
| other (−3, −2, +2) | 28 |

The dominant `(dx,dy,dz)` offsets are the four cardinal neighbours at dy=−1 — `(0,−1,±1)`,
`(±1,−1,0)` — i.e. a wall sign on the side of the pedestal block, then the same four at dy=+1. So a
labelling sign sits beside the monument, one level below or above it.

**Q3 — block directly below / above the monument.**

| below (pedestal) | share |  | above (cap) | share |
|---|---|---|---|---|
| bedrock | 33.5% |  | stained glass | 18.6% |
| stained clay | 15.8% |  | air | 13.4% |
| stained glass | 13.8% |  | barrier (166) | 11.4% |
| wool | 11.1% |  | slab | 11.1% |
| air | 9.3% |  | bedrock | 9.2% |
| (long tail) | … |  | stained clay / wool / … | … |

A pedestal prior of {bedrock, stained clay, stained glass, wool} covers ~74% of monuments; ~9%
float on air. The cap is usually decorative (glass / barrier / slab) or open.

**Q4 — armour stands.** 54 / 1723 (3.1%) — the `Ruediger_LP` style: an armour stand with wool on
its head, or a `CustomName` like "Place X WOOL here!". A minority decoration; signs are the
dominant label.

### What makes the detector work

**Invert the sign's facing — don't look near signs.** A monument is the air cell a sign *points
at*, not merely a cell near a sign. Anchoring on proximity is the difference between a usable
detector and one that emits ~20k false sites (an order of magnitude more than the 1723 real
monuments). The corpus gives a clean inverse-facing map for wall signs:

```
data 2 → monument = sign + (0,0,+1) + up      data 3 → sign + (0,0,−1) + up
data 4 → monument = sign + (+1,0,0) + up      data 5 → sign + (−1,0,0) + up
```

i.e. monument = sign + facing ± up — `+ up` when the sign is at the pedestal level (`SignBelow`),
`− up` when it is at the cap level (`SignAbove`). A label sign can sit beside the monument (offset
by its facing) or in the monument's own column; both placements are predicted and validated against
air + the declared pedestal/cap, so only the real one survives.

**Classify the sign text — don't keyword-match.** ~Half of all `wool` / `monument` / colour
keyword signs on a map are not monument labels — they are the rest of CTW signage:

```
RED TEAM ONLY            team barrier      Kill sheep to get wool!       wool source
Back to the woolroom     navigation        Wool Monument is behind you   directional
VICTORY MONUMENT         lobby             Only Reds Beyond this Point   no-build line
```

Notably "monument" is a poor keyword — it appears in only ~6% of real labels and occurs as often in
non-labels. What real labels look like, vs the false-positive signage:

| feature of a monument label | share |  | discriminative *non-label* words |
|---|---|---|---|
| contains a colour (word / § code / JSON) | 93% |  | `team only`, `only` |
| contains "wool" | 91% |  | `room` / `woolroom`, `wool room` |
| colour and "wool" | 89% |  | `base`, `use` / `use before`, `before` |
| "place … here" instruction | 54% |  | `portal`, `teleport`, `pick up` |
| contains "monument" | 6% |  | `reds`/`blues`, `red team`, `victory monument` |

The canonical label is "Place \<COLOUR\> WOOL here!" (top bigrams `wool here`, `place the`,
`<colour> wool`). A classifier that accepts `colour + wool` / `place <colour> … here` / short
`<colour> monument` and rejects the barrier / navigation / source / lobby phrasing keeps essentially
all real labels while discarding the off-site signage.

**Scope to the author's box.** A small-margin box around the monument cluster keeps ~all real
labels while excluding ~96% of the off-site keyword signs. Its main value is scan cost (only the
boxed volume is decoded) and recall safety — inside a trusted box the text filter can run looser
without a whole-map false-positive explosion.

**The style menu is the precision lever.** Declaring the monument's pedestal (block below), label
(how it's marked) and cap (block above) lets the detector require a specific signature — the single
biggest precision gain. The corpus supplies the menu:

- **Pedestal:** bedrock 33% · stained clay 16% · stained glass 14% · wool 11% · none/floating 9%.
- **Label:** sign-below 34% · sign-above 16% · armour stand 3% · none ~47% (so nearly half need a
  non-label fallback).
- **Most common (below → above) styles:** bedrock→glass 8.0% · floating (air→air) 7.2% · clay→glass
  6.2% · bedrock→bedrock 6.0% · glass→slab 4.6% · wool→barrier 4.2%.

**Colour from the stained block, not the sign text.** A wool / stained-clay / stained-glass
pedestal or cap encodes the wool colour in its data nibble. It is per-cell unambiguous (no
sign-attribution needed) and so robust exactly where signs are hardest to attribute (packed
clusters), and it is the only colour source for label-free monuments. So colour is
`ColorFromStain(below) ?? ColorFromStain(above) ?? labelColour`; the stained block and a
correctly-attributed sign agree in practice, and the rare genuine conflict (e.g. arabia: a magenta
glass pedestal under a "Purple Wool" sign — magenta is correct) resolves to the placed block and is
flagged in the suggestion's evidence.

### Why `layer_segment.parquet` can't drive this

`layer_segment` stores only `(world_x, world_z, world_y_start, world_y_end)` — per-column solid-run
extents, with no block ids/data, no tile entities (signs), no entities. The decisive monument
signals — pedestal material, sign text + facing, armour-stand NBT — are all absent, so it cannot
detect or classify monuments; neither can the cached `layer.parquet` (surface block per column
only). The suggester reads the Anvil world directly, bounded to the author's box.
`layer_segment`'s reuse is downstream: it (with `/buildability`) establishes where buildable
ground / air gaps are, so it's the right artifact to snap/validate a suggested monument onto a
surface — not to find it.

---

## 3. The detection spec

### Inputs

**`ScanBox`.** Inclusive world-coordinate box: `(MinX, MinY, MinZ, MaxX, MaxY, MaxZ)`. The author
draws it around a monument cluster. It bounds both the block scan and the candidate anchors. Call
`Suggest` **once per box** — if a map's monuments fall in separate groups, the author boxes (and
the UI calls) each group. `Expand(m)` and `Contains(x,y,z)` are provided; the scan internally adds
a 2-block margin so an anchor at the box edge still resolves.

**`MonumentStyle`** — three menu dimensions, all default to `Any`. The UI presents one dropdown per
dimension; the options are the enum values.

| dimension | meaning | values |
|---|---|---|
| `PedestalKind` | block directly below the monument | `Any` · `Bedrock` · `StainedClay` · `StainedGlass` · `Wool` · `Floating` (air below) |
| `LabelKind` | how the monument is marked | `Any` · `SignBelow` · `SignAbove` · `ArmorStand` · `None` |
| `CapKind` | block directly above the monument | `Any` · `Open` (air) · `StainedGlass` · `StainedClay` · `Bedrock` · `Slab` · `Barrier` · `Wool` · `Sign` |

### Detection anchors (what each `LabelKind` does)

- **`SignBelow`** — a colour-label wall sign at the pedestal's level (one below the monument),
  beside it and facing it, *or* in the monument's own column just under it. Geometry:
  `monument = sign + facing + up`.
- **`SignAbove`** — a wall sign at the cap's level (one above), beside and facing the monument, *or*
  in-column directly capping it. Geometry: `monument = sign + facing − up` (in-column: `sign − up`).
- **`ArmorStand`** — an armour stand near the monument. Disambiguated by payload: wool on the head
  ⇒ monument just above the stand; name-only ⇒ monument below it (a down-pointing marker).
- **`None`** — no label; pure geometry: an air cell sitting on the declared pedestal under the
  declared cap. Use for unlabelled monuments; pair with a specific pedestal/cap for precision
  (otherwise it is low-confidence and noisy).
- **`Any`** — try the sign and armour-stand anchors (does not run the geometry-only pass).

A sign only anchors if its text reads like a monument label — `MonumentSuggester.IsMonumentLabel`
accepts `"<colour> wool"`, `"place <colour> … here"`, or a short `"<colour> … monument"`, and
rejects barrier / navigation / source signage (`team only`, `woolroom`, `behind you`,
`kill … wool`, …). Arrow and ascii decoration are stripped before classifying, so
`"---> Red Wool --->"` still passes. Only wall signs carry a reliable facing; sign posts are not
used to predict positions.

### Colour derivation

A stained block (wool / stained clay / stained glass) directly below or above the monument is the
**placed** colour and is authoritative — it wins over parsed sign text. Order:

```
colour = ColorFromStain(below) ?? ColorFromStain(above) ?? labelColour   // sign text / stand head / custom name
```

(The data nibble of wool/clay/glass uses the standard colour order; `silver` = light gray.) When
the block colour and the label colour genuinely conflict, the suggestion's `Evidence` carries a
`[block:X ≠ label:Y]` note so the author can adjudicate; the block colour is used.

### Output — `MonumentSuggestion`

| field | meaning |
|---|---|
| `X, Y, Z` | the predicted (air) monument block |
| `Color` | inferred wool colour slug (PGM names; `silver`=light gray), or `null` |
| `Confidence` | `0..1` (sign + matching pedestal highest; pure geometry lowest) |
| `Source` | `"sign"` · `"armorstand"` · `"geometry"` |
| `PedestalId, PedestalData` | the block directly below |
| `SignX, SignY, SignZ` | the anchor sign, if any |
| `Evidence` | decoded sign text / armour-stand custom name (+ any colour-conflict flag) |

Returned highest-confidence first. Suggestions at the same cell are merged; independent agreeing
signs raise the confidence.

### API & usage

```csharp
List<MonumentSuggestion> MonumentSuggester.Suggest(
    IEnumerable<AnvilRegion.Chunk> chunks, ScanBox box, MonumentStyle style);
```

Helpers: `MonumentSuggester.IsMonumentLabel(text)`, `ColorFromText(text)`, `ClassifyPedestal(belowId)`
/ `ClassifyCap(aboveId)` (block id → kind). The caller supplies chunks (e.g.
`mcas.SelectMany(AnvilRegion.ReadChunks)`); chunks outside the box are skipped.

Explore / validate from the harness (`tools/PgmStudio.RoundTrip`):

```
# one map — derives a box from the map's monument clusters and prints suggestions + a match summary
dotnet run --project tools/PgmStudio.RoundTrip -- --suggest-monuments <regionDir> <xml_data.json> \
    [--auto-style | --pedestal <Kind> --label <Kind> --cap <Kind>] [--margin <N>]
# whole corpus
dotnet run --project tools/PgmStudio.RoundTrip -- --suggest-monuments-corpus [same flags]
```

`--auto-style` reads each cluster's actual pedestal/cap to simulate the author declaring the style.

### Corpus validation

Box = monument clusters + margin ±8.

| style declared? | precision | recall | FP | colour correct |
|---|---|---|---|---|
| auto (style declared) | **96.6%** | 57.8% | **35** | 92.2% |
| Any (undeclared) | 82.1% | 56.9% | 214 | 91.2% |

Over 1721 monuments. Declaring the style takes precision 82% → 97% and false positives 214 → 35 at
negligible recall cost — the corpus-scale confirmation that the style menu is the precision lever.
Per-map spot checks reach 100%/100% with the style declared: thunder (signs on bedrock), kanto
(signs beside a wool cap), pigland (glass pedestal + wool-on-head stand), dragons_hearth (name-only
stand above), lupa/lupain (glass cap, no sign), nutrient (wool pedestal + in-column "v \<colour\>
Wool v" sign).

### Scope & limitations

- **Needs the world.** `layer_segment.parquet` (and the cached `layer.parquet`) cannot drive this —
  they carry no block materials, signs or entities. Reuse them downstream to snap a confirmed
  monument onto buildable ground.
- **Recall is bounded by labelling.** Unlabelled monuments surface only via the `None`/geometry path
  (low confidence, author-confirmed).
- **Packed clusters** whose signs sit beside at the same level (`dy=0`) can attribute to a
  neighbour — an accepted outlier; the author corrects it. (A colour-coded block left/right at the
  same `y`, rather than below/above, is likewise not yet read for colour.)

---

## 4. The candidate-store architecture (design only)

> **Status: design only.** The backend detector (`MonumentSuggester`) above is complete and
> corpus-validated (precision 96.6% / recall 57.8% over 1721 monuments, auto-style). What follows
> specifies a refactor + table that would make suggestion servable **without the world on disk** —
> none of it is implemented yet.

### Why — the hosting constraint

The end goal is a hosted tool: on the Minecraft multiplayer server where the map is built, the
mapmaker types an in-game command that a server-side plugin handles — it saves/flushes the world,
zips the region files, uploads them, and posts back a clickable link to author the map to XML. The
web tier should be stateless — no mounted `.mca` corpus. Today three operations read the world at
runtime (`scan-world`, on-demand layer generation, monument suggestion); monument suggestion is the
hardest because `layer_segment` / `layer.parquet` can't drive it (no block materials, signs, or
entities — see §3 Scope).

The fix is the same shape as the rest of the app's data model: process the world once at ingest,
persist the derived result, query it at runtime. For monuments the derived result is a small set of
candidate monument cells with their evidence — dozens of rows per map, not the whole world.

**Why a table, not a parquet.** Unlike `layer.parquet` (a dense per-column grid read whole),
candidates are sparse and queried by predicate: spatial box filter, `source`/colour filter, join to
the map. A relational table fits the hybrid rule in `CLAUDE.md` ("real tables for entities we
list/query/edit") and queries trivially (`WHERE map_id = ? AND x BETWEEN …`); a blob would force a
full read-and-deserialize on every suggestion call. It is not the `map_artifact` blob path.

### The refactor — split `Suggest` into `Gather` + `Score`

`MonumentSuggester.Suggest(chunks, box, style)` today interleaves two separable phases:

| phase | what it does | needs the world? | when it runs |
|---|---|---|---|
| **Gather** | `RegionScan.Read` → find anchors (monument-label wall signs, wool-head / named armour stands, wool item frames) → project each to a candidate air cell + capture surrounding evidence (pedestal/cap ids, sign text, facing, stand payload) | yes (`.mca`) | ingest, once |
| **Score** | per candidate: `PedestalMatches`/`CapMatches` against the declared `MonumentStyle`, colour = `ColorFromStain(below) ?? ColorFromStain(above) ?? hint`, `Confidence`, `Offer` cell-merge + agreeing-sign boost, order | no (only ids/data/text already in hand) | authoring, per call |

Target API:

```csharp
// ingest — over the whole world (or buildable footprint); style-agnostic, permissive
List<MonumentCandidate> MonumentSuggester.Gather(IEnumerable<AnvilRegion.Chunk> chunks, ScanBox world);

// authoring — pure; box-scoped (the author marks the area they built in)
List<MonumentSuggestion> MonumentSuggester.Score(IEnumerable<MonumentCandidate> candidates,
                                                 ScanBox box, MonumentStyle style);

// live path (parity guard, harness) stays identical in behaviour:
Suggest(chunks, box, style)  ==  Score(Gather(chunks, box.Expand(2)), box, style)
```

`Suggest` is kept as `Score(Gather(...), ...)` so the existing corpus harness
(`--suggest-monuments[-corpus]`) and its parity numbers continue to guard the combined behaviour —
the refactor is required to be a pure factoring, not a behaviour change.

**Gather must be style-agnostic.** It runs every anchor type and accepts any pedestal/cap (the
declared style isn't known at ingest), storing the raw `below`/`above` ids+data so `Score` can
re-apply the author's `MonumentStyle` later. The `LabelKind` branch logic in today's `Suggest`
(which anchors to run) moves into `Score` as a filter on the stored `source`.

### The `monument_candidate` table

One row per cell, keeping the strongest anchor (armorstand > sign > geometry). Several wall signs
ringing one monument all project to the same air cell (pigland places 4 against each block), and a
monument is often marked by both a stand and a sign at that cell. `Score` cell-merges anyway and
the stand always scores ≥ the sign, so storing the duplicates just bloats the table — pigland's 64
sign emissions collapse to 8 candidates (4 stand cells + 4 sign cells). Columns are exactly what
`Score` needs to reproduce the style filter / `Confidence`, and nothing it can recompute.

**Drop pedestals `Score` can never accept.** At gather we drop any candidate whose pedestal block
can't pass the `Score` pedestal filter under any style — pure dead storage:

- **Sign pedestal (wall 68 / post 63).** A sign is never a pedestal: `PedestalMatches` rejects
  63/68 for `Pedestal.Any` (signs are excluded from "any solid") and no specific `PedestalKind` maps
  to them either, so these are provably never scored — a code-level guarantee, not a corpus
  statistic. They are the in-column "monument-above" emissions that land directly on top of the
  sign → thunder 24 → 12 (exactly its 12 real bedrock monuments).
- **Barrier pedestal (166).** A barrier is never a real pedestal (0/593 corpus — it appears only as
  a cap, 78×, e.g. pigland caps its glass-pedestal monuments with a signed barrier), so an air cell
  above one is a deliberately-blocked, unreachable spot — the phantom that a barrier-mounted sign's
  "beside, monument-above" placement projects onto the cap → pigland 8 → 4 (exactly the 4 real stand
  monuments).

Both are zero real-monument loss (corpus TP/FP/colour all unchanged). (The reachability intuition
"solid pedestal but air directly below → too high to place" is not used as a blanket rule: 28/541
real solid-pedestal monuments are legitimately raised that way; the barrier-pedestal signal is the
precise, loss-free version of it.)

A wall sign emits two placement families: beside (the sign faces the monument — always tried) and
in-column (the sign sits in the monument's own column, e.g. nutrient's "v WOOL v" cap). The
in-column pair is emitted only when the sign's column has a solid block within ±2 — a real
in-column monument has a pedestal there (corpus: 16/16), whereas wool signs that merely ring a
monument from open air (pigland's 4-per-block) float (0/16) and would only store noise. This keeps
every nutrient-style monument (corpus TP unchanged) and takes pigland 44 → 12 candidates.
Validation: `scripts/monument_pedestal_rule.py` and the sign-column corpus check.

**Wool item frames — a 4th anchor (`LabelKind.ItemFrame`).** An item frame holding a wool item
marks a monument (the framed wool's `Damage` = the colour). The frame mounts on a vertical face of
the monument's pedestal (→ monument above the support, a_new_day) or its cap (→ monument below,
golden_drought_iii's "sign+frame in one block"); `support = (TileX,TileY,TileZ) + FrameSupport[Facing]`,
and we try the air cell on each side. The catch: a corpus sweep finds 17 maps with wool frames but
only ~6 use them as monument indicators — the other 116/138 frames are decorative (molcein's
40-frame colour palette, mist's 22 on a floating slab, mame's black-wool "frog eyes"). Wool-only is
necessary but not sufficient; the structural pocket test is what excludes the décor: emit only when
the cell is air over a solid pedestal that is either capped (solid above) or itself grounded ≥3
blocks deep. Corpus: 20/20 real frame-cells, 0 FP; adding the anchor took the whole suggester to
TP 995→1010 / colour 919→935, FP unchanged at 35 — and, because a wool-frame candidate now sets
`anchored`, killed the geometry flood on those maps (a_new_day 186→4, a_new_day_ii 171→4).
Decorative-frame maps emit no frame candidate, so they stay unanchored and geometry runs as before
(molcein is still a geometry-flood map, unrelated to its frames). Validation:
`scripts/monument_corpus_analysis.read_world` sweep over the 17 frame maps.

#### DDL (FluentMigrator, mirrors `spawner_block` conventions)

```csharp
Create.Table("monument_candidate")
    .WithColumn("id").AsInt64().PrimaryKey().Identity()
    .WithColumn("map_id").AsInt64().NotNullable()
    // candidate (air) monument cell — world coords; box filter + cell-merge key
    .WithColumn("cand_x").AsInt32().NotNullable()
    .WithColumn("cand_y").AsInt32().NotNullable()
    .WithColumn("cand_z").AsInt32().NotNullable()
    .WithColumn("source").AsString(16).NotNullable()        // sign | armorstand | itemframe | geometry
    // block below / above the cell — PedestalMatches / CapMatches + ColorFromStain
    .WithColumn("pedestal_id").AsInt32().NotNullable()
    .WithColumn("pedestal_data").AsInt32().NotNullable()
    .WithColumn("cap_id").AsInt32().NotNullable()
    .WithColumn("cap_data").AsInt32().NotNullable()
    // fallback colour parsed from label text / stand head / name (stain still wins at Score)
    .WithColumn("color_hint").AsString(24).Nullable()
    // anchoring wall sign (null for non-sign sources)
    .WithColumn("sign_x").AsInt32().Nullable()
    .WithColumn("sign_y").AsInt32().Nullable()
    .WithColumn("sign_z").AsInt32().Nullable()
    .WithColumn("sign_facing").AsInt32().Nullable()         // wall-sign data nibble used to project
    .WithColumn("sign_text").AsString(256).Nullable()       // decoded label — evidence / colour
    // armour-stand evidence
    .WithColumn("stand_head_color").AsString(24).Nullable()
    .WithColumn("stand_name").AsString(256).Nullable()
    .WithColumn("evidence").AsString(256).Nullable();       // human-readable note (incl. colour-conflict)
```

`map_id` gets the standard `fk_monument_candidate_map` (cascade-delete) + `ix_monument_candidate_map`
via the existing `CreateForeignKeysAndIndexes` loop (add `"monument_candidate"` to its table list
and to the `Down()` drop list). A composite index on `(map_id, cand_x, cand_z)` is optional — the
per-map row count is small enough that the `map_id` index plus an in-memory box filter is fine for
v1.

#### linq2db entity (`PgmStudio.Data/Entities.cs`)

```csharp
[Table("monument_candidate")]
public sealed class MonumentCandidateRow
{
    [PrimaryKey, Identity, Column("id")] public long Id { get; set; }
    [Column("map_id"), NotNull] public long MapId { get; set; }
    [Column("cand_x"), NotNull] public int CandX { get; set; }
    [Column("cand_y"), NotNull] public int CandY { get; set; }
    [Column("cand_z"), NotNull] public int CandZ { get; set; }
    [Column("source"), NotNull] public string Source { get; set; } = "";
    [Column("pedestal_id"), NotNull] public int PedestalId { get; set; }
    [Column("pedestal_data"), NotNull] public int PedestalData { get; set; }
    [Column("cap_id"), NotNull] public int CapId { get; set; }
    [Column("cap_data"), NotNull] public int CapData { get; set; }
    [Column("color_hint")] public string? ColorHint { get; set; }
    [Column("sign_x")] public int? SignX { get; set; }
    [Column("sign_y")] public int? SignY { get; set; }
    [Column("sign_z")] public int? SignZ { get; set; }
    [Column("sign_facing")] public int? SignFacing { get; set; }
    [Column("sign_text")] public string? SignText { get; set; }
    [Column("stand_head_color")] public string? StandHeadColor { get; set; }
    [Column("stand_name")] public string? StandName { get; set; }
    [Column("evidence")] public string? Evidence { get; set; }
}
```

`MonumentCandidate` (the domain record `Gather` emits / `Score` consumes) carries the same fields
without `Id`/`MapId`; the ingest writer maps record → row, the suggestion endpoint maps row →
record.

#### Column rationale (what `Score` does with each)

| column(s) | consumed by |
|---|---|
| `cand_x/y/z` | box filter (`box?.Contains`); `Offer` merge key; output `X,Y,Z` |
| `source` | `Confidence`; `LabelKind` filter (`SignBelow/Above`→`sign`, `ArmorStand`→`armorstand`, `None`→`geometry`) |
| `pedestal_id/data` | `PedestalMatches(style.Pedestal, …)`; `ColorFromStain`; output `PedestalId/Data` |
| `cap_id/data` | `CapMatches(style.Cap, …)`; `ColorFromStain` |
| `color_hint` | colour fallback when neither stain colours |
| `sign_x/y/z` | output `SignX/Y/Z`; evidence |
| `sign_facing` | audit / re-projection if a future `Score` re-derives geometry |
| `sign_text` | `Evidence`; re-derivable colour |
| `stand_head_color`, `stand_name` | armour-stand colour / `Evidence` |

**Not stored — recomputed by `Score`** (they depend on the author's declared `MonumentStyle`,
unknown at ingest): `Confidence`, the final resolved `Color`, and the pass/fail of the
pedestal/cap/label filter.

### Two correctness rules

1. **Bound the geometry pass — it's a genuine last resort.** Today's `LabelKind.None` fallback
   iterates every non-air block — fine for a small author box, catastrophic over a whole world
   (un-tuned, thunder gathered 2193 candidates, ~99% exposed-stained-clay terrain). `Gather` bounds
   it, all corpus-validated against the real monuments (`scripts/monument_pedestal_rule.py`, 593
   monuments / 145 maps — 0% real-monument loss for each rule below):
   - **Skip geometry entirely when the map has monument anchors** (a `IsMonumentLabel` sign, or a
     wool-head / named armour stand). Geometry is only ever scored for `Label=None`, which no
     author declares on a labelled map. This alone takes thunder 2193→24, pigland 258→68.
   - For a genuinely label-free map, recognise the unsigned-monument allowlist — a distinctive
     pedestal (`ClassifyPedestal ∉ {Any, Floating}` = bedrock/clay/glass/wool) under a
     colour-or-marker cap (`ClassifyCap ∈ {StainedGlass, StainedClay, Wool, Barrier}` —
     glass/wool/clay encode the colour, barrier marks it). These are the 14 ped×cap combos real
     label-free monuments actually use (corpus: 662 monuments / 38%; lupain = bedrock+glass). This
     is deliberately tighter than "any distinctive cap": slab/sign/bedrock caps are
     terrain-ambiguous (34% of unlabelled reals but low precision) and single-signal (only one of
     the two distinctive) was 0.27%-precision spray — both not gathered. Then two
     accessibility/terrain filters:
       - ≥1 air horizontal neighbour of the cell — else it's a sealed pocket in terrain, not a
         placeable monument (corpus: 99.7% of these have an open side). Cell-level accessibility;
         replaces the old buried-pedestal + open-sky reject (which only checked the pedestal's
         faces and is moot once every candidate is capped).
       - clay-mass reject — a stained-clay pedestal with ≥3 same-clay neighbours among 8 is a clay
         floor, not an isolated pedestal (real clay pedestals ≤2). (Scoped to clay — a general mass
         rule kills ~1.3% of real monuments.)
     Corpus `--label None`: 191 TP / 5 FP / 97.4% precision / 93.7% colour-correct (the allowlist
     trades ~66 slab/bedrock-cap recall for far better colour, since every allowed cap is a
     colour/marker). Store impact vs the old un-bounded geometry: the 76 unanchored maps collapse —
     dreamland 5859→311, fall_of_babylon 5035→40, thundershock 240→12, molcein 216→0, lupain 52→2
     (its 2 real bedrock+glass monuments). Anchored maps and the `Label=Any` path are unchanged
     (TP=1010 / FP=35).

2. **Box-scoped, author-driven — the box is the mode.** The mapmaker knows where they placed the
   monument, so the UX is: the author marks the area (a required `ScanBox`) and optionally declares
   the style; `Score(candidates, box, style)` filters the pre-gathered candidates to that box and
   ranks them. Displaying every candidate on the map is explicitly not the model — it's noise.
   `Gather` is still whole-world at ingest (the box isn't known then); the box is a
   `WHERE cand_* BETWEEN …` / in-memory filter at `Score` time. Keep a small `Expand` margin on the
   box (the live path's 2-block slack) so an anchor projected to a cell at the box edge still
   resolves.

### Orbit completion — one authored unit → all teams

The mapmaker builds and boxes the monument(s) for one symmetry orbit unit (their own team's wool).
The other teams' monuments are the symmetric images, so once the author confirms the boxed
suggestions, a second request reflects/rotates each confirmed position onto every other team to
complete the wool's monument configuration.

- **Input:** the confirmed monument cell(s) `(x, y, z)` (+ colour) from the box step.
- **DB read (the second request):** the map's confirmed symmetry (`symmetry_json` artifact — mode +
  centre), the same source `POST /regions/{id}/orbit` and `SymmetryExpander` already read.
- **Transform:** the canonical `Geom.Symmetry` reflect/rotate on XZ only (Y is preserved — symmetry
  is horizontal). `rot_90` yields 3 counterparts (→ 4 total), `mirror_*` / `rot_180` yield 1
  (→ 2 total). Each counterpart's capturing team shifts by the orbit step `k`, per
  `docs/tools/configure.md` §2.
- **No candidate-table read here.** Orbit operates on the confirmed positions, not the gathered
  candidates — it is pure geometry over the symmetry artifact. The candidate table answers "where
  did the author place it?"; symmetry answers "where are its mirrors?". Two distinct requests, two
  distinct stores.

This keeps the intent model honest: the intent persists the authored wool + its monument(s) plus
the symmetry, and `SymmetryExpander.Expand` reproduces the very same orbit at generate time
(`docs/tools/configure.md` §2). The authoring-time orbit is the live preview/confirm of what the
generator will emit — same transform, surfaced so the author sees all teams' monuments before
export.

### Where it sits in the ingest pipeline

The table is populated by the same once-per-upload worker that already does the world scan. After
ingest, no authoring operation reads `.mca` — the web tier is stateless.

```
in-game command → server plugin ──HTTP upload──▶ ingest worker
   (saves world, zips region/)                     │ unzip to scratch
                                                    │ scan-world      → features + layer.parquet + islands   (DB)
                                                    │ pre-bake layers → surface/y0/bedrock/base               (DB, kills on-demand layer generation's .mca read)
                                                    │ Gather monuments → monument_candidate rows              (DB, kills suggestion's .mca read)
                                                    │ create map row → slug
                                                    ▼
                                raw world zip → object storage (cold; re-process only, never at edit time)
                                                    ▼
   plugin posts clickable link in chat ◀──slug──── return edit link → /maps/{slug}/edit   (runs 100% off MariaDB)
```

- **The plugin is the upload client, not a parser.** It only saves + zips + uploads the region
  files (and posts the returned link); all decoding/detection stays in the C# ingest worker, so
  `MonumentSuggester` remains the single source of truth — no Java reimplementation of the
  detector.
- **Flush before zip.** A live server holds chunks in memory; the plugin must force a world save
  (so the on-disk `.mca` is current) before zipping, or freshly placed monuments/signs are missed.
- Gather is one extra pass over the already-decoded chunk stream (the scan worker holds them), so
  it adds little cost beyond what `scan-world` already pays.
- Re-gathering after a detector improvement is a worker job over the retained zip — no re-upload,
  so no need to re-run the in-game command.
- Candidates are map-scoped and cascade-delete with the map, like every other feature table.

### Authoring endpoints (proposed)

**Suggest within the boxed area** — `box` is required:

```
GET /api/map/{slug}/monument-suggestions?box=x0,y0,z0,x1,y1,z1[&style=<pedestal>,<label>,<cap>]
```

Loads `monument_candidate` rows for the map, runs `Score(rows, box, style)`, returns ranked
`MonumentSuggestion`s — the existing output contract from §3 Output. No world access. `style`
defaults to `Any,Any,Any`. (This would replace the world-reading suggestion endpoint anticipated in
§3 but never wired.)

**Complete the orbit** — after the author confirms positions (see Orbit completion above):

```
POST /api/map/{slug}/monument-orbit   { positions: [ { x, y, z, color? } ] }
```

Reads the confirmed `symmetry_json`, reflects/rotates each position onto the other teams, and
returns the full per-team monument set (each tagged with its capturing team) for the author to
confirm. Reuses the counterpart geometry; no candidate-table or world access.

### Change checklist (design, not yet started)

- [ ] `PgmStudio.Minecraft`: factor `MonumentSuggester` into `Gather` (world → `List<MonumentCandidate>`)
      + `Score` (`candidates, box?, style → List<MonumentSuggestion>`); keep `Suggest` =
      `Score(Gather(...), box, style)`. Apply the geometry-bounding rule (§4 Two correctness rules)
      inside `Gather`.
- [ ] `PgmStudio.Minecraft`: `MonumentCandidate` record.
- [ ] Migration `M0002_MonumentCandidate` (table + FK + index; add to `Down()` drops).
- [ ] `PgmStudio.Data`: `MonumentCandidateRow` entity + `ITable` on `PgmDb`; writer (record→row) +
      reader (row→record), the latter likely on `MapReader` or a small `MonumentCandidateStore`.
- [ ] Ingest: call `Gather` in the scan worker (`WorldFeatureWriter` or its caller) and persist rows;
      delete-then-insert per map so re-gather is idempotent (mirrors the feature-row pattern).
- [ ] `PgmStudio.Api`: `GET /map/{slug}/monument-suggestions` (box required; load rows → `Score` →
      rank) and `POST /map/{slug}/monument-orbit` (read `symmetry_json` → reflect/rotate confirmed
      positions → per-team set, reusing the counterpart geometry).
- [ ] Tests: `Gather`/`Score` round-trip equals `Suggest` on the existing fixtures (thunder,
      pigland, dragons_hearth); re-run `--suggest-monuments-corpus` to confirm parity numbers
      unchanged.

### Scope & open questions

- **v1 is gather-at-ingest only.** Live `.mca` re-gather for a one-off is out of scope (the zip in
  object storage is the re-process source).
- **Recall is still labelling-bounded** (§3 Scope & limitations) — the table inherits, not fixes,
  that limit; it only changes where/when detection runs.
- **Packed-cluster mis-attribution** (a sign at `dy=0` beside a neighbour) carries over unchanged;
  the author corrects on confirm.
- **Open: per-map vs cross-map gather params.** Detection is currently global-constant; if a future
  tuner wants per-map thresholds, they belong in `map_config_json`, not this table (the table
  stores results).
- **Open: snap-to-buildable downstream.** A confirmed suggestion can be validated/snapped onto
  buildable ground using `layer_segment` (DB), per §3 — independent of this table.

---

## 5. Current gap

**No UI.** The detection backend (`MonumentSuggester`, §3) is complete and corpus-validated; the
candidate-store design (§4) that would make it servable without the world on disk has not been
built. Neither has the **Monuments step UI** in Configure — the box-draw, style dropdowns,
ghost-marker render, and confirm/dismiss flow described in §1 do not exist yet. This is a plain
gap, not a resolved one: authoring a map's monuments today has no author-facing suggestion surface,
only the harness (`--suggest-monuments[-corpus]`) for validation.
