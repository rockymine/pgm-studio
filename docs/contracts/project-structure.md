# Project structure: the package map, what each project is, and where code belongs

> A whole-board inventory and classification, written after `PgmStudio.Geom` was split out. The
> question this answers: **do the project boundaries earn their keep, and which code is mis-homed?**
> Companion to the `## Code placement` rule in `CLAUDE.md` (the *rule*) and
> `geometry-consolidation.md` (the geometry *leaf*). This doc is the *map*.
>
> **Headline finding:** the cross-project boundaries are sound — namespaces are internally clean, and
> there are **no illegal dependency edges** (`Pgm`⊥`Analysis`, `Client` sees only `Contracts`+`Geom`,
> the sketch rasterizer stays NTS-free). The friction the braindump names is **(a) organization
> *inside* three over-stuffed projects** (`Pgm/Editing` = 20 files/4 concerns, `Data` = 5 concerns,
> `Analysis` = 12 flat files) and **(b) legibility of the data-model layering** (six representations of
> "a map", spread across four projects). Both are fixable without moving project boundaries.

> **Status (A5 landed).** Two of the three internal folds in §5 are done, behaviour-preserving
> (build clean · all unit tests green · `RoundTrip --parity` 350/350 · endpoints serve 200):
> - **`Pgm`** folded into `Editing/` (edit), `Authoring/` (configure), `Sketch/` (sketch); the codec stays
>   flat at the root (§5.1). **`RegionCategorizer` moved `Analysis` → `Pgm/Authoring/`** (next to its
>   generator inverse) and its **`RegionFacet` record moved to `Domain`**; the generator↔categorizer
>   round-trip guard tests moved `Analysis.Tests` → `Pgm.Tests` (§5.2).
> - **`Analysis`** folded into `Region/` · `Layer/` · `Playability/` · `Footprint/` (§5.2); `WoolColors`
>   was already merged into `Domain.WoolColors`.
>
> **Remaining: `Data` (§5.3)** — tracked as `A6` in `TODO.md`.

## 1. The verified dependency graph

```
            ┌─────────────────────────── Api (composition root: refs everything) ───────────────────────────┐
            │                                                                                                │
         Client ──────────────┐                Import ───┬──────────┬──────────┐                            │
        (Blazor WASM)         │                (ingest CLI)│         │          │                            │
            │  └──> Contracts  │                          │         │          │                            │
            │  └──> Geom       │                          ▼         ▼          ▼                            │
            │                  │                         Data ──> Migrations   Pgm ──> Domain                │
            │                  │                    (persistence) │     ▲       │  └──> Geom                 │
            │                  │                    refs: Domain, │     │    Analysis ──> Domain             │
            │                  │                    Contracts,    │     │       │     └──> Geom              │
            │                  │                    Pgm, Minecraft,│     │    Minecraft ──> Domain           │
            │                  │                    Analysis ─────┘     │                                    │
            └──────────────────┴──────────────────────────────────────-┘                                    │
                                                                                                            │
  Pure leaves (0 project deps):  Geom   Domain   Contracts   Migrations                                     │
  ──────────────────────────────────────────────────────────────────────────────────────────────────────--┘
```

Reads bottom-up; every arrow points at a thing the project is *allowed* to reach. **Two facts make the
split load-bearing, not arbitrary:**

- **`Client` cannot see `Domain` or `Pgm`** (WASM boundary — it refs only `Contracts` + `Geom`). So the
  wire DTOs *must* live somewhere `Analysis` can't drag in. That somewhere is `Contracts`.
- **`Analysis` cannot see `Contracts`** (it would create a cycle of intent: DTOs depending on analysis
  which depends on DTOs). So anything `Analysis` *and* the client both need is forced down into a true
  leaf — that is exactly what `Geom` is for, and why the old duplicated reflect/rotate existed before it.

These two constraints are the real reason there are "two model projects." It is not an accident.

## 2. The four *kinds* of project

| Kind | Projects | One-line charter |
|---|---|---|
| **Pure leaves** (0 deps) | `Geom`, `Domain`, `Contracts`, `Migrations` | math primitives · PGM entities · wire DTOs · DB schema |
| **Format / domain logic** | `Pgm`, `Analysis`, `Minecraft` | read/edit/write `map.xml` · NTS-backed derivations · Anvil world reading |
| **Persistence / ingest** | `Data`, `Import` | DB codec + repositories · parquet→relational CLI |
| **Presentation** | `Client`, `Api` | Blazor UI · FastEndpoints composition root |

Your own mental buckets map onto this cleanly: **PGM stuff** = `Pgm` + `Domain` + the region-semantics
half of `Analysis`; **Minecraft world stuff** = `Minecraft` + `Import` + the block-feature half of
`Analysis`; **data models** = `Domain` (internal) + `Contracts` (wire); **database** = `Data` +
`Migrations`; **geometry** = `Geom`; **API** = `Api` + `Client`. The only bucket that is genuinely
*split across two charters* is `Analysis` (PGM-semantic region work vs. world-feature playability work) —
see §5.

## 3. The key insight: six representations of "a map" (this is the "data spread over" feeling)

There is no single "map model." There are **six**, each correct for its pipeline stage. The friction
is not that they exist — it is that their names/locations don't announce the pipeline. The flow:

```
 (1) map.xml ──Pgm.MapParser──> MapXml + entities (Domain)         "what a finished map IS" (typed)
                    │
                    └──Pgm codec──> Dict doc / xml_data.json        "the loose JSON tree" (Pgm.JsonTree)
                                        │  ▲
                                        │  └── patched in place by Pgm/Editing/*Editor   ← EDIT tool
                                        │
 (4) MapIntent (Pgm/Editing) ──Generators──┘                       "what the author WANTS" ← CONFIGURE tool
                                        │
                                        └──Data/MapWriter codec──> *Row POCOs (Data/Entities) ──> MariaDB
                                                                        │
 (5) world features ──Minecraft──> Data/WorldFeatureWriter ──> feature *Row ──Analysis──> (6) DTOs (Contracts) ──> Client
 (3) SketchLayout (sketch_layout_json) ──Pgm/SketchRasterizer + Analysis/IslandDetector──> cells/parquet ──> world  ← SKETCH tool
```

1. **`MapXml` + entities** — `Domain/MapModel.cs`. The typed, parsed PGM model. *What a map is.*
2. **`Dict` doc** (`xml_data.json`) — the loose `Dictionary<string,object?>` tree (`Pgm/JsonTree.cs`).
   The round-trip currency; what the **edit** tool patches and what the **generators** emit.
3. **`SketchLayout`** (`sketch_layout_json`) — the **sketch** tool's draft-geometry format.
4. **`MapIntent`** — `Pgm/Editing/MapIntent.cs`. The **configure** tool's declarative input.
5. **`*Row` POCOs** — `Data/Entities.cs` (23 tables). The relational shape; the hybrid persistence model.
6. **Wire DTOs** — `Contracts/*.cs`. What crosses `/api` to the Blazor client.

Each tool "owns" one authoring format (edit→Dict patch, configure→MapIntent, sketch→SketchLayout), and
all three converge on the **Dict doc → codec → Rows** spine. That is the spine to make legible.

## 4. Per-project verdict

| Project | Files / LOC | Verdict | Action |
|---|---|---|---|
| `Geom` | 2 / 112 | **Exemplary** | none — it's the model the others should imitate |
| `Domain` | 4 / 284 | **Earns its place** | none (one open Q: §6.1 MapIntent) |
| `Contracts` | 3 / 93 | **Earns its place** | name is fine; add a one-liner so it's not confused with `Domain` |
| `Migrations` | 5 / 460 | **Clean** | none |
| `Minecraft` | 8 / 1381 | **Clean** | none — your "cleanest project" read is right |
| `Import` | 3 / 265 | **Clean but identity-blurred** | clarify: it is parquet→relational, **not** world-scan (§5) |
| `Pgm` | 30 / 4583 | **Right project, wrong internal shape** | ✅ **folded `Editing/`/`Authoring/`/`Sketch/` (A5)** |
| `Analysis` | 12 / 2159 | **Right project, flat** | ✅ **folded `Region/`/`Layer/`/`Playability/`/`Footprint/` (A5)** |
| `Data` | 8 / 1084 | **Mixed bag — 5 concerns** | **sub-folder; reconsider `WorldFeatureWriter` (§5.3)** |
| `Api` | 27 / — | **Acceptable for a composition root** | optional: group 20 endpoint files into feature folders |
| `Client` | ~60 / — | **Well-organized** | none (already foldered by tool; JS is 5-layer) |

## 5. The three projects that need internal reorganization

### 5.1 `Pgm` — fold the 20-file `Editing/` into four concerns — ✅ landed (A5)

> **Concrete, file-by-file plan with the verified roundtrip + migration steps:
> `pgm-codec-foldering.md`.** (Churn is 2 `Api` `using` lines; the codec doesn't move.)

`Pgm`'s charter (`CLAUDE.md`) is **parse / edit / generate `map.xml`** — *and the whole of `Editing/`
honours that*: every file there reads or writes the Dict doc. The braindump's worry "do the generators
belong in `Pgm` at all?" resolves to **yes** by the placement rule — the generators *emit the Dict doc*,
need the codec (`Pgm`) and `Geom`, and must be reachable by `Api`; `Pgm` is the lowest project that is
all three. Splitting them into a new `PgmStudio.Authoring` project would just be "`Pgm` + `Geom` + a `Pgm`
dependency" — a new name, **no new boundary**, pure churn. Keep them in `Pgm`; give them folders:

| New sub-folder | Files (currently all flat in `Editing/`) | Tool |
|---|---|---|
| `Pgm/` (root, the **codec** — already here) | Deserializer, Serializer, JsonTree, Xml, XmlWriter, MapParser, RegionParser, FilterParser, RegionBoundsDeriver | roundtrip |
| `Pgm/Editing/` (**doc-patch CRUD**) | ApplyRuleEditor, FilterEditor, RegionEditor, SpawnEditor, TeamEditor, WoolEditor, RegionBuilder, DocAccess, EditException, **SymmetryAuthoring** | **edit** |
| `Pgm/Authoring/` (**intent → doc**) | MapIntent, IntentGenerator, IntentNaming, MetaGenerator, TeamsGenerator, WoolGenerator, BuildGenerator, TeamPalette, **SymmetryExpander** | **configure** |
| `Pgm/Sketch/` | SketchRasterizer | **sketch** |

Notes that resolve braindump questions:
- **The two symmetries are different things, correctly.** `SymmetryAuthoring` (called from
  `Api/RegionEndpoints`) creates a *counterpart region* in the doc — it's an **edit-tool** region op.
  `SymmetryExpander` (called only from `IntentGenerator`) orbit-fills a `MapIntent` *before* generation —
  a **configure-tool** intent op. So "edit understands symmetry but is otherwise dumb" is accurate: its
  one symmetry verb is `SymmetryAuthoring`. They land in different sub-folders above.
- **`SketchRasterizer` is pure** (refs `Geom` only). The "rasterize → IslandDetector → parquet → polygon"
  chain you described is **orchestrated by `Api/SketchEndpoints`**, which feeds the rasterizer's cell
  output into `Analysis.IslandDetector`. That's why `Pgm` stays NTS-free — good, keep it that way.

### 5.2 `Analysis` — fold into four concerns; relocate two misfits — ✅ landed (A5)

12 files. Two don't belong in `Analysis` at all (they aren't NTS-backed derivations — the charter); the
other ten fold into four concerns, verified against the internal dependency graph:

| Sub-folder | Files | Concern |
|---|---|---|
| `Analysis/Region/` | RegionAuthoringEncoder, RegionGeometry2d | region shape/encoding (NTS). `RegionGeometry2d` is the shared dict→geometry adapter `Playability` also leans on |
| `Analysis/Layer/` | SegmentIndex, SideView | structure the raw block layer: the vertical-segment index (feeds the checks) + the side-view depth projection (feeds the canvas). `SideView` takes raw `(x,z,ys,ye)` segments → depth grid — a view-feed, not a verdict |
| `Analysis/Playability/` | Buildability, Traversability, WoolSources, ResourceSources | gameplay verdicts (NTS). `Buildability` is the hub (← `Traversability`, `ResourceSources`) |
| `Analysis/Footprint/` | IslandDetector, SymmetryDetector | landmass geometry (NTS). `SymmetryDetector` consumes `IslandDetector`'s islands |

**Two files leave `Analysis`:**
- **`RegionCategorizer` → `Pgm`** (+ its `RegionFacet` record → `Domain`). It's pure (`Dict`+regex, **no
  NTS**) and PGM-semantic — it derives what a region *means* from `map.xml` usage, the literal inverse of
  the `Pgm/Authoring` generators. No `Analysis` source calls it (only `Api`+tests do); `RegionAuthoringEncoder`
  needs only the `RegionFacet` *type* (passed in by `Api`), which `Domain` lets both projects see.
- **`WoolColors` → `Domain`, merged with `Minecraft/WoolData`** — they are **duplicates** (same wool+dye
  damage→slug tables, both "port of minecraft/wool.py"). One canonical copy in the lowest leaf every consumer
  (`Minecraft`, `Analysis`, `Api`) reaches = `Domain`; fold in `WoolColors.Normalize`/`Aliases` +
  `WoolData.WoolColor(fallback)`.

**The gotcha — categorizer ↔ generators (the `RegionCategorizer` move resolves it).** The `Pgm/Authoring`
generators are documented "mirror of `RegionCategorizer`": inverse functions of one region-authoring
contract (intent→xml vs xml→facets). There is **no code edge** (Pgm⊥Analysis; the mentions are comments) —
yet the contract *is* enforced, by round-trip tests in `Analysis.Tests` (the one test project that sees both
sides): generate from intent → `DeriveFacets` → assert the emitted regions categorize back (e.g.
`WoolGeneratorTests` → `wool/room` + `wool/spawner`). The fragility is that this guard is **invisible** — it
works only because of where the tests happen to be filed. Moving `RegionCategorizer` into `Pgm` puts the
inverse pair in one project: the mirror becomes a real in-project relationship and the round-trip suite moves
to `Pgm.Tests` where it's findable. (Same shape as the `Geometry2d` fix — a file misfiled against its
project's charter, whose move also dissolves a coupling.) Contract spec: `region-authoring.md` /
`region-categorization.md`.

### 5.3 `Data` — the real mixed bag (5 concerns, your "tables + file mgmt" intuition under-counts)

`Data` conflates five things; this is the project most worth untangling:

| Concern | Files | Really is… |
|---|---|---|
| **Schema / POCOs** | Entities.cs (23 `*Row`), PgmDb.cs | the relational *model* + linq2db context |
| **Map codec** | MapReader, MapWriter, MapRepository | Dict doc ⇄ entity rows (entity-replace; see `region-data-flow.md`) |
| **World-feature ingest** | **WorldFeatureWriter** (refs `Minecraft`+`Analysis`) | a **pipeline stage**, not persistence — it *scans the Anvil world* |
| **Artifact decode** | SurfaceLayer (reads `layer.parquet` blob) | parquet → cells |
| **Candidate store** | MonumentCandidateStore | the monument-suggestion query store |

Two recommendations:
- **Sub-folder it** — e.g. `Data/Schema/`, `Data/Map/` (codec), `Data/Features/` (WorldFeatureWriter +
  SurfaceLayer + MonumentCandidateStore). Cheap, immediately clarifying.
- **Reconsider `WorldFeatureWriter`'s home.** It is the live-world-scan half of ingest (it pulls in
  `Minecraft` *and* `Analysis` to turn a world into feature rows). `Import` is the *other* half of ingest
  (parquet→relational) but **doesn't ref `Minecraft`**. So the ingest story is split: replay lives in
  `Import`, live-scan lives in `Data`. That's the "is Minecraft the import project?" confusion —
  **answer: no.** `Minecraft` = reading; `Import` = parquet replay CLI; the live world→DB scan is
  `Data/WorldFeatureWriter` driven by `Api` endpoints. Consider unifying the two ingest pathways (both
  into `Import`, or a thin `Pipeline` seam) so "how a world becomes rows" has one home.

## 6. Open decisions for you (genuinely your call — recommendations given, not executed)

1. **`MapIntent` → `Domain`?** It's a pure, zero-dep data model — it *could* join the other map models
   in `Domain`, leaving only the *generators* in `Pgm/Authoring/`. **Recommendation: leave it in `Pgm`.**
   `Domain` is deliberately "what a *parsed* map IS"; `MapIntent` is "what an author *wants*" — a
   different lifecycle, and it lives next to the only code that consumes it. Promoting it buys nothing
   (nothing below `Pgm` needs it; `Client` posts loose JSON, §6.3).
2. **Rename `Contracts`?** The name reads generic, but it's the correct API term (the wire contract) and
   it's the one model project `Client` can see. **Recommendation: keep the name**, add a header comment
   distinguishing it from `Domain` (internal entities vs. wire DTOs). A rename is churn across every
   endpoint for no boundary change.
3. **The intent contract is stringly-typed.** The Configure client (`ConfigureWizard.razor.cs`) builds a
   loose `JsonObject` and PUTs it; `Api/IntentStore` deserializes into `MapIntent` by camelCase
   convention. This is *why* `MapIntent` can sit in `Pgm` without breaking `Client`'s leaf set — but the
   contract between the wizard and `MapIntent` is **unchecked**. If it bites, the fix is a `Contracts`
   intent-DTO shared by both sides; until then it's a known, deliberate trade (decoupling over safety).

## 7. Cheap, correct fixes (independent of the bigger reorg)

- **`CLAUDE.md` placement rule** — ✅ the `Contracts` bullet no longer lists `(Symmetry)` (the geometry
  move relocated it to `Geom`); the placement rule matches reality.
- The bigger reorg (§5) is a **clean-session, cross-cutting job** — per `geometry-consolidation.md`'s own
  warning about reshuffles, don't bolt it onto feature work; land it as folder moves with no logic changes,
  one project at a time, parity between each. **`Pgm` + `Analysis` done (A5)** (lowest-risk first);
  **`Data` (§5.3) remains — `A6`.**
