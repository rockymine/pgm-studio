# Project structure — the package map, and where a piece of code belongs

A whole-board inventory: what each project is, what it may reach, and which parts have outgrown the shape they
were given. It answers one question — **do the project boundaries earn their keep, and which code is
mis-homed?** — and it is the companion to `CLAUDE.md`'s *Code placement* rule: that is the rule, this is the
map.

**Where it stands.** The cross-project boundaries are sound and the two invariants that make them load-bearing
still hold. The friction is no longer between projects but *inside* two of them: `Pgm` now holds two different
things under one name, and `Minecraft` has grown 47 files at its root. Both are internal folds, not boundary
moves.

## 1. The dependency graph

Every arrow points at something the project is allowed to reach; read it bottom-up.

```
  Api  (composition root — refs Data · Pgm · Analysis · Minecraft · Contracts · Migrations · Client)
   │
   ├── Client  ──> Contracts, Geom                       (Blazor WASM)
   ├── Data    ──> Domain, Contracts, Pgm, Minecraft, Analysis
   ├── Pgm     ──> Domain, Geom
   ├── Analysis──> Domain, Geom
   └── Minecraft ──> Domain, Geom

  Import ──> Data, Pgm, Contracts, Migrations            (parquet → relational CLI)

  Pure leaves (no project references):  Geom   Domain   Contracts   Migrations
```

**Two constraints are what force the shape**, and they are the reason there appear to be "two model projects":

- **`Client` cannot see `Domain` or `Pgm`.** It is WASM and references only `Contracts` and `Geom`, so the
  wire DTOs have to live somewhere that does not drag the rest in. That somewhere is `Contracts`.
- **`Analysis` cannot see `Contracts`.** DTOs depending on analysis depending on DTOs is a cycle of intent, so
  anything `Analysis` *and* the client both need is forced into a true leaf. That is exactly what `Geom` is
  for, and the duplicated reflect/rotate that existed before it is what the rule prevents.

Both are checked by the `.csproj` files rather than by convention: `Analysis` has no `Contracts` reference and
`Client` has no `Domain` or `Pgm` reference.

## 2. The four kinds of project

| Kind | Projects | Charter |
|---|---|---|
| **Pure leaves** | `Geom`, `Domain`, `Contracts`, `Migrations` | geometry algorithms · PGM entities and the rules over them · wire DTOs · the DB schema |
| **Format and domain logic** | `Pgm`, `Analysis`, `Minecraft` | the `map.xml` codec *and* the layout generator · NTS-backed derivations · Anvil world reading and writing |
| **Persistence and ingest** | `Data`, `Import` | the DB codec, repositories and stores · the parquet→relational CLI |
| **Presentation** | `Client`, `Api` | the Blazor UI · the FastEndpoints composition root |

`Domain` has drifted past "entities" in a way worth naming: alongside `MapModel`, `Region` and `Filter` it now
holds the **rules that several projects share** — `RoomFrames` (the stamped-room resolver), `ObjectiveFootprint`,
`MiningTiers`, `DestroyableMaterials`, `DoorMaterials`, `Gamemodes`, `PhantomErasure`. That is correct rather
than accidental: each is a pure function of the entities, and each has consumers in more than one project
above. It does mean `Domain` is "the PGM domain", not "the PGM data model".

## 3. Sizes, and the two that have outgrown their shape

| Project | Files | Lines | Internal shape |
|---|---|---|---|
| `Geom` | 40 | 4,593 | `Algorithms/` 20 · `Relief/` 6 · `Render/` 2 · 12 at root |
| `Domain` | 18 | 1,616 | flat |
| `Contracts` | 12 | 898 | flat |
| `Migrations` | 20 | 1,448 | `Migrations/` 19 |
| `Minecraft` | 61 | 11,246 | **47 at root** · `Dressing/` 6 · `Render/` 5 · `Views/` 3 |
| `Import` | 4 | 471 | flat |
| `Pgm` | 133 | 20,085 | `Compose/` 42 · `Authoring/` 21 · `Evaluate/` 20 · `Shapes/` 10 · `Editing/` 10 · `Plan/` 6 · `Sketch/` 4 · `Derive/` 4 · `Render/` 3 · `Detect/` 1 · 12 at root |
| `Analysis` | 16 | 2,577 | `Playability/` 7 · `Footprint/` 4 · `Region/` 3 · `Layer/` 2 |
| `Data` | 13 | 2,223 | `Features/` 4 · `Theme/` 3 · `Map/` 3 · `Schema/` 2 · `Plan/` 1 |
| `Api` | 65 | 8,838 | `Endpoints/` 37 · `Services/` 25 · `Http/` 2 |
| `Client` | 80 `.cs` + razor | 13,434 | `Features/<Tool>/` · `Components/` · `Pages/`, plus 11 JS layers |

**`Pgm` is two projects wearing one name.** `CLAUDE.md` charters it as "`map.xml` parse/edit/generate", and
that describes 48 files — the codec at the root, `Authoring/` (intent → map), `Editing/` (patch a parsed doc),
`Sketch/` and `Detect/`. The other 85 files and 11,522 lines are the **layout generator**: `Compose/`,
`Evaluate/`, `Shapes/`, `Derive/`, `Plan/` and its `Render/`. The generator touches no XML. It reads only
`Domain` and `Geom`, which means it would stand on its own as a project with no new edge — the boundary it
already respects is exactly the one it would get. Whether to split it is §6.1; what is not in question is that
one charter sentence no longer covers the project.

**`Minecraft` has 47 files at its root**, 8,220 lines — the shape `Pgm`, `Analysis` and `Data` were folded out
of. The concerns are visible from the filenames and separable without moving a boundary: world reading (Anvil,
regions, chunks), stamping (rooms, cubes, objectives, houses), painting, and the suggesters that read a world
back. It is the last unfolded project.

## 4. Seven representations of "a map"

There is no single map model, and there should not be: each of these is correct for one stage of the pipeline.
The friction is that their names do not announce the order they come in.

| # | Representation | Lives in | Is |
|---|---|---|---|
| 1 | `PlanModel` (`plan_json`) | `Pgm/Plan` | the coarse cell grid — the **plan** tool's document, and what the generator emits; `plan-doc.js` is its browser twin |
| 2 | `SketchLayout` (`sketch_layout_json`) | `Pgm/Sketch` | the real geometry at block resolution — the **sketch** tool's document |
| 3 | `MapIntent` (`map_intent_json`) | `Pgm/Authoring` | what the author *wants*: teams, spawns, objectives, regions — the **configure** tool's input |
| 4 | `MapXml` + entities | `Domain/MapModel.cs` | what a finished map *is*, parsed and typed |
| 5 | the `Dict` doc (`xml_data.json`) | `Pgm/JsonTree.cs` | the loose `Dictionary<string, object?>` tree — the round-trip currency the **edit** tool patches |
| 6 | `*Row` POCOs (39 tables) | `Data/Schema/Entities.cs` | the relational shape — the hybrid persistence model |
| 7 | wire DTOs | `Contracts/*.cs` | what crosses `/api` to the client |

The flow between them is the one `docs/tools/flow.md` describes from the map's side:

```
 plan_json ──PlanCompiler──> sketch_layout_json ─┬─SketchRasterizer──> world geometry ──> Anvil world
                             + map_intent_json   │
                                                 └──Generators──> Dict doc ──MapWriter──> rows ──> MariaDB
 map.xml ──MapParser──> MapXml ──Serializer──> Dict doc ──┘                    │
                                       ▲                                       └──> DTOs ──> Client
                                       └── patched in place by Pgm/Editing/*
```

Each tool owns one authoring document — plan, sketch, intent — and all of them converge on the **Dict doc →
codec → rows** spine.

### 4a. The codec — `XML ⇄ MapXml ⇄ Dict`

The machinery between representations 4 and 5 sits flat at the `Pgm` root, because it *is* that half of the
project's charter. Every file is a pure function over the three forms.

| File | Role | Entry | Direction |
|---|---|---|---|
| `MapParser` | the top-level parser; orchestrates the two registry parsers and gates what is supported | `Parse(path)` / `ParseXmlString(xml)` → `MapXml` | XML → domain |
| `RegionParser` | `<regions>` → a flat `Region` registry plus apply-rules; synthetic ids for anonymous regions | `ParseRegionsElem` → `(regions, applyRules)` | XML → domain |
| `FilterParser` | `<filters>` → a flat `Filter` registry; seeds `never`/`always` | `ParseFiltersElem` → `filters` | XML → domain |
| `Xml` | internal XElement / attribute / text / coordinate helpers | (internal) | — |
| `IncludeLibrary` | resolves `<include>` against the server's includes directory | | XML → XML |
| `Serializer` | domain → the JSON tree, plus single-entity encoders for the importer | `ToDict(MapXml)` → `Dict` | domain → Dict |
| `Deserializer` | the inverse | `FromDict(Dict)` → `MapXml` | Dict → domain |
| `XmlWriter` | domain → PGM `map.xml`, with top-level/inline-ref logic and synthetic-id elision | `ToXml(MapXml)` → `string` | domain → XML |
| `JsonTree` | JSON string → tree, and structural tree comparison | `FromJson`/`FromJsonLenient`; `DeepEquals`/`Canonical`/`DiffKeys` | Dict utility |
| `RegionBoundsDeriver` | recomputes derived `bounds_2d` for compound and transform regions after a DB rebuild | `Derive(registry)` | Dict-read helper |
| `MapValidity` | the rules a map must satisfy to export | | over the domain |
| `UnsupportedMapException` | the refusal `MapParser` raises — proto floor, modern world, unread objective module | | — |

**Who drives it:** `Import` (parse and serialize at ingest, then `FromJson` → rows), `Data/Map/MapReader`
(`ToDict` plus `RegionBoundsDeriver` to rebuild the doc for the editor), `Data/Map/MapWriter` (`FromDict` on
save), `Api/MapXmlEndpoint` (`FromDict` + `XmlWriter` for export), `Api/WriteEndpoints` (`FromJson` on posted
edits), and the round-trip harness. `JsonTree`'s comparison half is verification — the importer's drift check
and the harness; its `FromJson` is the production codec.

## 5. Per-project verdict

| Project | Verdict | What it needs |
|---|---|---|
| `Geom` | **Exemplary.** Zero project references and every consumer can take it — the model the others are measured against. Its growth is the right kind: `Algorithms/`, `Relief/`, `Render/` are all pure. | none |
| `Domain` | **Earns its place**, now as the shared-rule leaf as much as the entity one (§2). | a header line saying so |
| `Contracts` | **Earns its place** — the only model project `Client` can see. | a header line distinguishing it from `Domain` |
| `Migrations` | **Clean** — one file per migration, in order. | none |
| `Minecraft` | **Needs the fold the others got.** 47 root files across four separable concerns (§3). | an internal fold, folders-only |
| `Import` | **Clean, identity blurred.** It is parquet→relational replay; it is *not* the world scan, which lives in `Data/Features/WorldFeatureWriter`. | the distinction stated in its own header |
| `Pgm` | **Two charters in one project** (§3). Both halves are internally well-shaped. | the split decision, §6.1 |
| `Analysis` | **Right internal shape** — `Region/`, `Layer/`, `Playability/`, `Footprint/`. | none |
| `Data` | **Right internal shape** — `Schema/`, `Map/`, `Features/`, and since then `Theme/` and `Plan/` for the library and plan stores. | none |
| `Api` | **Acceptable for a composition root**, though 37 endpoint files and 25 services is where feature folders start to pay. | optional grouping |
| `Client` | **Well organized** — `Pages/` for routable pages, `Features/<Tool>/` for a tool's own bodies, `Components/` for the shared vocabulary, and 11 JS layers under `wwwroot/js/studio/`. | none |

## 6. Open decisions

**6.1 — Should the layout generator be its own project?** `Compose/`, `Evaluate/`, `Shapes/`, `Derive/` and
`Plan/` are 85 files and 11,522 lines that never touch `map.xml`, and they reference only `Domain` and `Geom`,
so `PgmStudio.Compose` would add no dependency edge — the split is free in graph terms. What it would buy is
that `Pgm`'s charter sentence becomes true again and the generator's own dependencies become visible (today it
can reach the codec without anything noticing). What it costs is a rename across every citation and the
`PlanCompiler` seam sitting on a boundary rather than inside one. **Recommendation: split it when the
generator next needs a structural change**, not as a standalone refactor.

**6.2 — `MapIntent` → `Domain`?** It is a pure, zero-dependency data model and could join the other map models
there, leaving only the generators in `Pgm/Authoring/`. **Recommendation: leave it in `Pgm`.** `Domain` is
what a *parsed* map is; `MapIntent` is what an author *wants* — a different lifecycle — and it sits beside the
only code that consumes it. Nothing below `Pgm` needs it, and the client posts loose JSON (§6.4).

**6.3 — Rename `Contracts`?** The name reads generic, but it is the correct API term and it is the one model
project `Client` can see. **Recommendation: keep it**, with a header comment separating it from `Domain`. A
rename is churn across every endpoint for no boundary change.

**6.4 — The intent contract is stringly typed.** The Configure client builds a loose `JsonObject`
(`AuthoringContext`, `Wizard.Intent`) and PUTs it; `AuthoringIntentEndpoints` deserializes into `MapIntent` by
camelCase convention. This is *why* `MapIntent` can live in `Pgm` without breaking `Client`'s leaf set, and it
is unchecked in both directions. If it bites, the fix is an intent DTO in `Contracts` shared by both sides;
until then it is a deliberate trade of safety for decoupling.

**6.5 — Two ingest pathways.** `Import` replays parquet → rows as a CLI and references neither `Minecraft` nor
`Analysis`; `Data/Features/WorldFeatureWriter` scans an Anvil world → rows and pulls both, driven by `Api`. So
"how a world becomes rows" has two implementations. **Recommendation: leave it split.** `WorldFeatureWriter`
is DB-write-shaped and sits beside the rows it writes, and unifying would drag `Minecraft` and `Analysis` into
the replay CLI for no boundary gain — a project-boundary refactor, not a fold.
