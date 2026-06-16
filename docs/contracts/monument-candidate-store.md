# Monument candidate store — pre-computed suggestion via a relational table

How monument suggestion stops being a **`.mca`-at-runtime** feature and becomes a **DB query**, by
splitting the detector into a *gather* pass (runs once at ingest, needs the world) and a *score* pass
(runs at authoring, pure). The gathered candidates persist in a relational `monument_candidate` table.

Read alongside:
- `monument-suggestion.md` — the detector this design factors. The schema here stores the output of its
  **gather** half; the **score** half is the query-time function.
- `new-map-authoring.md` §Wools — the Monuments step that consumes the suggestions.
- `region-data-flow.md` — why derived data lives in MariaDB; this table is another *derived projection*
  of the world, persisted so the authoring host never touches the world files.

> **Status:** design only. Backend detector (`MonumentSuggester`) is complete and corpus-validated
> (precision 96.6% / recall 57.8% over 1721 monuments, auto-style). This doc specifies the refactor +
> table that make it servable without the world on disk.

---

## 1. Why — the hosting constraint

The end goal is a hosted tool: on the Minecraft multiplayer server where the map is built, the mapmaker
types an **in-game command** that a server-side **plugin** handles — it saves/flushes the world, zips the
region files, uploads them, and posts back a clickable link to author the map to XML. The web tier should
be **stateless** — no mounted `.mca` corpus. Today three operations read the world at runtime (`scan-world`, on-demand layer
generation B9, **monument suggestion**); monument suggestion is the hardest because `layer_segment` /
`layer.parquet` can't drive it (no block materials, signs, or entities — see `monument-suggestion.md`
§Scope).

The fix is the same shape as the rest of the app's data model: **process the world once at ingest,
persist the derived result, query it at runtime.** For monuments the derived result is a small set of
**candidate monument cells with their evidence** — dozens of rows per map, not the whole world.

**Why a table, not a parquet.** Unlike `layer.parquet` (a dense per-column grid read whole), candidates
are sparse and queried *by predicate*: spatial box filter, `source`/colour filter, join to the map. A
relational table fits the hybrid rule in `CLAUDE.md` ("real tables for entities we list/query/edit") and
queries trivially (`WHERE map_id = ? AND x BETWEEN …`); a blob would force a full read-and-deserialize on
every suggestion call. It is *not* the `map_artifact` blob path.

---

## 2. The refactor — split `Suggest` into `Gather` + `Score`

`MonumentSuggester.Suggest(chunks, box, style)` today interleaves two separable phases:

| phase | what it does | needs the world? | when it runs |
|---|---|---|---|
| **Gather** | `RegionScan.Read` → find anchors (monument-label wall signs, wool-head / named armour stands) → project each to a candidate **air cell** + capture surrounding evidence (pedestal/cap ids, sign text, facing, stand payload) | **yes** (`.mca`) | **ingest, once** |
| **Score** | per candidate: `PedestalMatches`/`CapMatches` against the declared `MonumentStyle`, colour = `ColorFromStain(below) ?? ColorFromStain(above) ?? hint`, `Confidence`, `Offer` cell-merge + agreeing-sign boost, order | **no** (only ids/data/text already in hand) | **authoring, per call** |

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
(`--suggest-monuments[-corpus]`) and its parity numbers continue to guard the *combined* behaviour —
the refactor is required to be a pure factoring, not a behaviour change.

**Gather must be style-agnostic.** It runs every anchor type and accepts any pedestal/cap (the declared
style isn't known at ingest), storing the raw `below`/`above` ids+data so `Score` can re-apply the
author's `MonumentStyle` later. The `LabelKind` branch logic in today's `Suggest` (which anchors to run)
moves into `Score` as a filter on the stored `source`.

---

## 3. The `monument_candidate` table

One row **per cell**, keeping the strongest anchor (**armorstand > sign > geometry**). Several wall signs
ringing one monument all project to the *same* air cell (pigland places 4 against each block), and a
monument is often marked by *both* a stand and a sign at that cell. `Score` cell-merges anyway and the
stand always scores ≥ the sign, so storing the duplicates just bloats the table — pigland's 64 sign
emissions collapse to **8** candidates (4 stand cells + 4 sign cells). Columns are exactly what `Score`
needs to reproduce the style filter / `Confidence`, and nothing it can recompute.

**No-barrier-pedestal cut.** A cell sitting directly on a **barrier** (id 166) is dropped at gather. A
barrier is *never* a real pedestal (0/593 corpus — it appears only as a *cap*, 78×, e.g. pigland caps its
glass-pedestal monuments with a signed barrier), so an air cell *above* one is a deliberately-blocked,
unreachable spot — the phantom that a barrier-mounted sign's "beside, monument-above" placement projects
onto the cap. Dropping these is **zero real-monument loss** (corpus TP/FP/colour all unchanged) and takes
pigland's stored rows **8 → 4** — exactly the 4 real (stand) monuments. (The reachability intuition "solid
pedestal but air directly below → too high to place" is *not* used as a blanket rule: 28/541 real
solid-pedestal monuments are legitimately raised that way; the barrier-pedestal signal is the precise,
loss-free version.)

A wall sign emits two placement families: **beside** (the sign faces the monument — always tried) and
**in-column** (the sign sits in the monument's own column, e.g. nutrient's "v WOOL v" cap). The in-column
pair is emitted **only when the sign's column has a solid block within ±2** — a real in-column monument has
a pedestal there (corpus: 16/16), whereas wool signs that merely *ring* a monument from open air (pigland's
4-per-block) float (0/16) and would only store noise. This keeps every nutrient-style monument (corpus TP
unchanged) and takes pigland 44 → 12 candidates. Validation: `scripts/monument_pedestal_rule.py` and the
sign-column corpus check.

### DDL (FluentMigrator, mirrors `spawner_block` conventions)

```csharp
Create.Table("monument_candidate")
    .WithColumn("id").AsInt64().PrimaryKey().Identity()
    .WithColumn("map_id").AsInt64().NotNullable()
    // candidate (air) monument cell — world coords; box filter + cell-merge key
    .WithColumn("cand_x").AsInt32().NotNullable()
    .WithColumn("cand_y").AsInt32().NotNullable()
    .WithColumn("cand_z").AsInt32().NotNullable()
    .WithColumn("source").AsString(16).NotNullable()        // sign | armorstand | geometry
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
via the existing `CreateForeignKeysAndIndexes` loop (add `"monument_candidate"` to its table list and to
the `Down()` drop list). A composite index on `(map_id, cand_x, cand_z)` is optional — the per-map row
count is small enough that the `map_id` index plus an in-memory box filter is fine for v1.

### linq2db entity (`PgmStudio.Data/Entities.cs`)

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

`MonumentCandidate` (the domain record `Gather` emits / `Score` consumes) carries the same fields without
`Id`/`MapId`; the ingest writer maps record → row, the suggestion endpoint maps row → record.

### Column rationale (what `Score` does with each)

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

**Not stored — recomputed by `Score`** (they depend on the author's declared `MonumentStyle`, unknown at
ingest): `Confidence`, the final resolved `Color`, and the pass/fail of the pedestal/cap/label filter.

---

## 4. Two correctness rules

1. **Bound the geometry pass — it's a genuine last resort.** Today's `LabelKind.None` fallback iterates
   *every* non-air block — fine for a small author box, catastrophic over a whole world (un-tuned, thunder
   gathered **2193** candidates, ~99% exposed-stained-clay terrain). `Gather` bounds it, all
   corpus-validated against the real monuments (`scripts/monument_pedestal_rule.py`, 593 monuments / 145
   maps — **0% real-monument loss** for each rule below):
   - **Skip geometry entirely when the map has monument anchors** (a `IsMonumentLabel` sign, or a wool-head
     / named armour stand). Geometry is only ever scored for `Label=None`, which no author declares on a
     labelled map. This alone takes **thunder 2193→24, pigland 258→68**.
   - For a genuinely label-free map, still require a **distinctive pedestal or cap**
     (`ClassifyPedestal ∉ {Any, Floating}` **or** `ClassifyCap ∉ {Any, Open}`), then drop the two terrain
     signatures: a **walled-in pedestal** (no air/sign among its 4 faces) **with open sky** (≥2 air above) —
     real buried pedestals top out at 1 air above; and a **stained-clay pedestal that is part of a clay
     mass** (≥3 same-clay neighbours among 8) — real clay pedestals are isolated (≤2). *(Isolation is
     scoped to clay on purpose: a general "mass + open sky" rule would also kill ~1.3% of real monuments on
     bedrock/wool floors.)*

2. **Box-scoped, author-driven — the box is the mode.** The mapmaker *knows* where they placed the
   monument, so the UX is: the author **marks the area** (a required `ScanBox`) and optionally declares the
   style; `Score(candidates, box, style)` filters the pre-gathered candidates to that box and ranks them.
   Displaying *every* candidate on the map is explicitly **not** the model — it's noise. `Gather` is still
   whole-world at ingest (the box isn't known then); the box is a `WHERE cand_* BETWEEN …` / in-memory
   filter at `Score` time. Keep a small `Expand` margin on the box (the live path's 2-block slack) so an
   anchor projected to a cell at the box edge still resolves.

---

## 5. Orbit completion — one authored unit → all teams

The mapmaker builds and boxes the monument(s) for **one** symmetry orbit unit (their own team's wool).
The other teams' monuments are the symmetric images, so once the author confirms the boxed suggestions, a
**second request** reflects/rotates each confirmed position onto every other team to complete the wool's
monument configuration.

- **Input:** the confirmed monument cell(s) `(x, y, z)` (+ colour) from the box step.
- **DB read (the second request):** the map's confirmed symmetry (`symmetry_json` artifact — mode +
  centre), the same source `POST /regions/{id}/orbit` (F3) and `SymmetryExpander` already read.
- **Transform:** the existing 2D `Geometry2d` reflect/rotate on **XZ only** (Y is preserved — symmetry is
  horizontal). `rot_90` yields 3 counterparts (→ 4 total), `mirror_*` / `rot_180` yield 1 (→ 2 total). Each
  counterpart's **capturing team shifts by the orbit step `k`**, per `new-map-authoring.md` §2.
- **No candidate-table read here.** Orbit operates on the *confirmed* positions, not the gathered
  candidates — it is pure geometry over the symmetry artifact. The candidate table answers *"where did the
  author place it?"*; symmetry answers *"where are its mirrors?"*. Two distinct requests, two distinct
  stores.

This keeps the intent model honest: the intent persists the **authored** wool + its monument(s) plus the
symmetry, and `SymmetryExpander.Expand` reproduces the very same orbit at generate time
(`new-map-authoring.md` §2). The authoring-time orbit is the live **preview/confirm** of what the
generator will emit — same transform, surfaced so the author sees all teams' monuments before export.

## 6. Where it sits in the ingest pipeline

The table is populated by the same once-per-upload worker that already does the world scan. After
ingest, **no authoring operation reads `.mca`** — the web tier is stateless.

```
in-game command → server plugin ──HTTP upload──▶ ingest worker
   (saves world, zips region/)                     │ unzip to scratch
                                                    │ scan-world      → features + layer.parquet + islands   (DB)
                                                    │ pre-bake layers → surface/y0/bedrock/base               (DB, kills B9's .mca read)
                                                    │ Gather monuments → monument_candidate rows              (DB, kills suggestion's .mca read)
                                                    │ create map row → slug
                                                    ▼
                                raw world zip → object storage (cold; re-process only, never at edit time)
                                                    ▼
   plugin posts clickable link in chat ◀──slug──── return edit link → /editor/{slug}   (runs 100% off MariaDB)
```

- **The plugin is the upload client, not a parser.** It only saves + zips + uploads the region files (and
  posts the returned link); all decoding/detection stays in the C# ingest worker, so `MonumentSuggester`
  remains the single source of truth — no Java reimplementation of the detector.
- **Flush before zip.** A live server holds chunks in memory; the plugin must force a world save (so the
  on-disk `.mca` is current) before zipping, or freshly placed monuments/signs are missed.
- Gather is one extra pass over the already-decoded chunk stream (the scan worker holds them), so it adds
  little cost beyond what `scan-world` already pays.
- Re-gathering after a detector improvement is a worker job over the retained zip — no re-upload, so no
  need to re-run the in-game command.
- Candidates are map-scoped and cascade-delete with the map, like every other feature table.

---

## 7. Authoring endpoints

**Suggest within the boxed area** — `box` is required:

```
GET /api/map/{slug}/monument-suggestions?box=x0,y0,z0,x1,y1,z1[&style=<pedestal>,<label>,<cap>]
```

Loads `monument_candidate` rows for the map, runs `Score(rows, box, style)`, returns ranked
`MonumentSuggestion`s — the existing output contract from `monument-suggestion.md` §Output. No world
access. `style` defaults to `Any,Any,Any`. (This replaces the world-reading suggestion endpoint that
`monument-suggestion.md` anticipated but was never wired.)

**Complete the orbit** — after the author confirms positions (§5):

```
POST /api/map/{slug}/monument-orbit   { positions: [ { x, y, z, color? } ] }
```

Reads the confirmed `symmetry_json`, reflects/rotates each position onto the other teams, and returns the
full per-team monument set (each tagged with its capturing team) for the author to confirm. Reuses the F3
counterpart geometry; no candidate-table or world access.

---

## 8. Change checklist

- [ ] `PgmStudio.Minecraft`: factor `MonumentSuggester` into `Gather` (world → `List<MonumentCandidate>`)
      + `Score` (`candidates, box?, style → List<MonumentSuggestion>`); keep `Suggest` =
      `Score(Gather(...), box, style)`. Apply the geometry-bounding rule (§4.1) inside `Gather`.
- [ ] `PgmStudio.Minecraft`: `MonumentCandidate` record.
- [ ] Migration `M0002_MonumentCandidate` (table + FK + index; add to `Down()` drops).
- [ ] `PgmStudio.Data`: `MonumentCandidateRow` entity + `ITable` on `PgmDb`; writer (record→row) +
      reader (row→record), the latter likely on `MapReader` or a small `MonumentCandidateStore`.
- [ ] Ingest: call `Gather` in the scan worker (`WorldFeatureWriter` or its caller) and persist rows;
      delete-then-insert per map so re-gather is idempotent (mirrors the feature-row pattern).
- [ ] `PgmStudio.Api`: `GET /map/{slug}/monument-suggestions` (box required; load rows → `Score` → rank)
      and `POST /map/{slug}/monument-orbit` (read `symmetry_json` → reflect/rotate confirmed positions →
      per-team set, reusing the F3 counterpart geometry).
- [ ] Tests: `Gather`/`Score` round-trip equals `Suggest` on the existing fixtures (thunder, pigland,
      dragons_hearth); re-run `--suggest-monuments-corpus` to confirm parity numbers unchanged.

---

## 9. Scope & open questions

- **v1 is gather-at-ingest only.** Live `.mca` re-gather for a one-off is out of scope (the zip in object
  storage is the re-process source).
- **Recall is still labelling-bounded** (§`monument-suggestion.md` Scope) — the table inherits, not fixes,
  that limit; it only changes *where/when* detection runs.
- **Packed-cluster mis-attribution** (a sign at `dy=0` beside a neighbour) carries over unchanged; the
  author corrects on confirm.
- **Open: per-map vs cross-map gather params.** Detection is currently global-constant; if a future tuner
  wants per-map thresholds, they belong in `map_config_json`, not this table (the table stores *results*).
- **Open: snap-to-buildable downstream.** A confirmed suggestion can be validated/snapped onto buildable
  ground using `layer_segment` (DB), per `monument-suggestion.md` — independent of this table.
