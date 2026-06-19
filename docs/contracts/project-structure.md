# Project structure: the package map, what each project is, and where code belongs

> A whole-board inventory and classification. The question this answers: **do the project boundaries
> earn their keep, and which code is mis-homed?** Companion to the `## Code placement` rule in `CLAUDE.md`
> (the *rule*) and `geometry-consolidation.md` (the geometry *leaf*). This doc is the *map*.
>
> **Headline finding:** the cross-project boundaries are sound — namespaces are internally clean, and
> there are **no illegal dependency edges** (`Pgm`⊥`Analysis`, `Client` sees only `Contracts`+`Geom`,
> the sketch rasterizer stays NTS-free). The remaining friction is **(a) organization *inside* a few
> over-stuffed projects** and **(b) legibility of the data-model layering** (six representations of
> "a map", spread across four projects) — both fixable without moving project boundaries.
>
> **A5 landed the internal folds for `Pgm` and `Analysis`** (behaviour-preserving — build clean · all
> tests green · `RoundTrip --parity` 350/350), and relocated `RegionCategorizer` → `Pgm/Authoring/`
> (beside its generator inverse) + `RegionFacet` → `Domain`. **`A6` then folded `Data` into
> `Schema/`/`Map/`/`Features/` (§5.3) — the structure reorg is complete; every project now has a clean
> internal shape.**

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

The mental buckets map onto this cleanly: **PGM stuff** = `Pgm` + `Domain` + the region-semantics
half of `Analysis`; **Minecraft world stuff** = `Minecraft` + `Import` + the block-feature half of
`Analysis`; **data models** = `Domain` (internal) + `Contracts` (wire); **database** = `Data` +
`Migrations`; **geometry** = `Geom`; **API** = `Api` + `Client`. The only bucket genuinely *split across
two charters* is `Analysis` (PGM-semantic region work vs. world-feature playability work) — now folded
by concern (§4).

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
 (4) MapIntent (Pgm/Authoring) ──Generators──┘                     "what the author WANTS" ← CONFIGURE tool
                                        │
                                        └──Data/MapWriter codec──> *Row POCOs (Data/Entities) ──> MariaDB
                                                                        │
 (5) world features ──Minecraft──> Data/WorldFeatureWriter ──> feature *Row ──Analysis──> (6) DTOs (Contracts) ──> Client
 (3) SketchLayout (sketch_layout_json) ──Pgm/Sketch + Analysis/Footprint.IslandDetector──> cells/parquet ──> world  ← SKETCH tool
```

1. **`MapXml` + entities** — `Domain/MapModel.cs`. The typed, parsed PGM model. *What a map is.*
2. **`Dict` doc** (`xml_data.json`) — the loose `Dictionary<string,object?>` tree (`Pgm/JsonTree.cs`).
   The round-trip currency; what the **edit** tool patches and what the **generators** emit.
3. **`SketchLayout`** (`sketch_layout_json`) — the **sketch** tool's draft-geometry format.
4. **`MapIntent`** — `Pgm/Authoring/MapIntent.cs`. The **configure** tool's declarative input.
5. **`*Row` POCOs** — `Data/Entities.cs` (23 tables). The relational shape; the hybrid persistence model.
6. **Wire DTOs** — `Contracts/*.cs`. What crosses `/api` to the Blazor client.

Each tool "owns" one authoring format (edit→Dict patch, configure→MapIntent, sketch→SketchLayout), and
all three converge on the **Dict doc → codec → Rows** spine. That is the spine to make legible.

### 3a. The codec — `XML ⇄ MapXml ⇄ Dict` (the 9 flat `Pgm` root files)

The machinery between representations (1) and (2) lives flat at the `Pgm` root — it *is* the project's
charter. Every file is a pure function over `XML ⇄ MapXml ⇄ Dict`; the codec is the hub the whole
pipeline turns on.

| File | Role | Public entry | Direction |
|---|---|---|---|
| `MapParser` | top-level parser; orchestrates the two registry parsers | `Parse(path)` / `ParseXmlString(xml)` → `MapXml` | XML → domain |
| `RegionParser` | `<regions>` → flat `Region` registry + apply-rules; synthetic ids for anon | `ParseRegionsElem` → `(regions, applyRules)` | XML → domain |
| `FilterParser` | `<filters>` → flat `Filter` registry; seeds `never`/`always` | `ParseFiltersElem` → `filters` | XML → domain |
| `Xml` | `internal` XElement/attr/text/coord helpers | (internal, parse-side) | — |
| `Serializer` | domain → JSON tree (`xml_data.json` shape) + single-entity encoders for the importer | `ToDict(MapXml)` → `Dict`; `RegionToDict`/… | domain → Dict |
| `Deserializer` | inverse of `Serializer` | `FromDict(Dict)` → `MapXml` | Dict → domain |
| `XmlWriter` | domain → PGM `map.xml` (top-level/inline-ref logic, synthetic-id elision) | `ToXml(MapXml)` → `string` | domain → XML |
| `JsonTree` | JSON-string → tree **+** structural tree compare | `FromJson`/`FromJsonLenient`; `DeepEquals`/`Canonical`/`DiffKeys` | Dict util |
| `RegionBoundsDeriver` | recompute derived `bounds_2d` for compound/transform regions after a **DB** rebuild | `Derive(registry)` | Dict-read helper |

**Who drives it:** `Import` (parse→serialize on ingest, then `FromJson`→rows), `Data/MapReader`
(`ToDict` + `RegionBoundsDeriver` to rebuild the doc for the editor), `Data/MapWriter` (`FromDict` on
save), `Api/MapXmlEndpoint` (`FromDict` + `XmlWriter` for the export endpoint), `Api/WriteEndpoints`
(`FromJson` on POSTed edits), and the `RoundTrip` parity harness (all of it). `JsonTree`'s
`DeepEquals`/`Canonical`/`DiffKeys` are roundtrip-verification (used by `Import`'s drift check + the
parity harness); its `FromJson` is the production codec.

## 4. Per-project verdict

| Project | Files / LOC | Verdict | Action |
|---|---|---|---|
| `Geom` | 2 / 112 | **Exemplary** | none — it's the model the others should imitate |
| `Domain` | 6 / 332 | **Earns its place** | none — now also home to `RegionFacet` + `WoolColors` (A5); open Q §6.1 (`MapIntent`) |
| `Contracts` | 3 / 93 | **Earns its place** | name is fine; add a one-liner so it's not confused with `Domain` |
| `Migrations` | 5 / 460 | **Clean** | none |
| `Minecraft` | 8 / 1381 | **Clean** | none — the cleanest project |
| `Import` | 3 / 265 | **Clean but identity-blurred** | clarify: it is parquet→relational, **not** world-scan (§5.3) |
| `Pgm` | 30 / 4951 | **Right internal shape now** | ✅ **folded `Editing/`/`Authoring/`/`Sketch/`; `RegionCategorizer` joined `Authoring/` (A5)** |
| `Analysis` | 10 / 1731 | **Right internal shape now** | ✅ **folded `Region/`/`Layer/`/`Playability/`/`Footprint/` (A5)** |
| `Data` | 8 / 1084 | **Right internal shape now** | ✅ **folded `Schema/`/`Map/`/`Features/`; `WorldFeatureWriter` stays in `Features/` (A6, §5.3)** |
| `Api` | 27 / — | **Acceptable for a composition root** | optional: group 20 endpoint files into feature folders |
| `Client` | ~60 / — | **Well-organized** | none (already foldered by tool; JS is 5-layer) |

## 5. Remaining internal reorganization

> §5.1 (`Pgm`) and §5.2 (`Analysis`) **landed in A5**; §5.3 (`Data`) **landed in A6** — see the §4
> verdict table and the codec map (§3a). All three folds are done.

### 5.3 `Data` — folded into Schema / Map / Features (A6)

`Data` conflated five concerns. **`A6` sub-foldered it behaviour-preservingly** (folders-only — build
clean · `RoundTrip --parity` 350/350 · `Data`/`Import`/`Api` tests green) into three namespaces that
match their folders (the A5 convention):

| Folder / namespace | Files | The concern |
|---|---|---|
| `Schema/` → `PgmStudio.Data.Schema` | Entities.cs (23 `*Row` + `ArtifactKind`), PgmDb.cs | the relational *model* + linq2db context |
| `Map/` → `PgmStudio.Data.Map` | MapReader, MapWriter, MapRepository | Dict doc ⇄ entity rows (the map codec; see `region-data-flow.md`) |
| `Features/` → `PgmStudio.Data.Features` | WorldFeatureWriter, SurfaceLayer, MonumentCandidateStore | world-feature ingest · artifact decode (`layer.parquet` blob) · monument-suggestion query store |

**`WorldFeatureWriter`'s home — decided: stays in `Data` (`Features/`).** It reads as a pipeline stage
(it pulls `Minecraft` *and* `Analysis` to turn a world into feature rows), but its body is fundamentally
a **DB writer** — `PgmDb` `BulkCopyAsync`/`InsertAsync`/`DeleteAsync` over the `*Row` POCOs plus
`MonumentCandidateStore`. Moving it to `Import` would drag `Minecraft` + `Analysis` into the
parquet-replay CLI (which today refs neither) for no boundary gain, and exceeds a folders-only fold, so it
sits beside the rows it writes. The broader "is the ingest story split, and should it be unified?"
question is parked as an open decision (§6.4).

## 6. Open decisions (genuinely your call — recommendations given, not executed)

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
4. **Unify the two ingest pathways?** Ingest is split across two homes: `Import` replays
   parquet→rows (a CLI; refs neither `Minecraft` nor `Analysis`), while `Data/Features/WorldFeatureWriter`
   live-scans the Anvil world→rows (pulls `Minecraft` + `Analysis`), driven by `Api` endpoints. So "how a
   world becomes rows" has two implementations. A thin `Pipeline` seam — or merging the live-scan into
   `Import` — would unify them. **Recommendation: leave it split** until the import story is revisited:
   `WorldFeatureWriter` is DB-write-shaped and sits fine beside the rows it writes, and unifying is a
   project-boundary refactor with new transitive deps, not a fold. (This resolves the `WorldFeatureWriter`
   half of `A6`'s "decide its home" as *keep* — see §5.3.)
