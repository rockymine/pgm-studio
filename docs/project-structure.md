# Project structure — the package map, and where a piece of code belongs

A whole-board inventory: what each project is, what it may reach, and which parts have outgrown the shape they
were given. It answers one question — **do the project boundaries earn their keep, and which code is
mis-homed?** — and it is the companion to `CLAUDE.md`'s *Code placement* rule: that is the rule, this is the
map.

**Where it stands.** The cross-project boundaries are sound and the two invariants that make them load-bearing
still hold. `B119` cut the first new one since the original shape settled: `PgmStudio.Export` — the sketch
world builder, the destroy/core/wool placement gate and the `map.xml` composer — pulled out of `Api/Services`
into its own project, so a driver that only writes a region folder no longer has to reference ASP.NET Core,
FastEndpoints and the DB layer to reach it. The friction elsewhere is no longer between projects but *inside*
two of them: `Pgm` holds two different things under one name, and `Minecraft` is large enough that a flat
root stopped saying what anything was for, which `A7` folded. Both are internal folds, not boundary moves.

## 1. The dependency graph

Every arrow points at something the project is allowed to reach; read it bottom-up.

```
  Api  (composition root — refs Data · Pgm · Analysis · Minecraft · Contracts · Export · Migrations · Client)
   │
   ├── Client  ──> Contracts, Geom                       (Blazor WASM)
   ├── Data    ──> Domain, Contracts, Pgm, Minecraft, Analysis
   ├── Export  ──> Domain, Analysis, Minecraft, Pgm       (the export path — world build + map.xml, no DB)
   ├── Pgm     ──> Domain, Geom
   ├── Analysis──> Domain, Geom
   ├── Minecraft ──> Domain, Geom
   ├── Contracts ──> Vocabulary                          (the words the DTOs are written in)
   └── Domain  ──> Geom, Vocabulary                      (the shapes its rules measure over · the finding)

  Import ──> Data, Pgm, Contracts, Migrations            (parquet → relational CLI)

  Pure leaves (no project references):  Geom   Vocabulary   Migrations
```

**`Domain → Geom` is the one edge below the middle row, and `B210` added it.** `Domain` and `Geom` were both
leaves, which read as two peers until a shape was needed on both sides of the client boundary: `BlockBox` — the
inclusive 3-D block volume every objective, scan region and stamped volume is — sat in `Domain`, which the WASM
client cannot see, so the Configure step that draws a core casing had grown a field-identical copy. The shape
is geometry, so it moved to the leaf both halves reach, and `Domain` took the reference to keep reading it. The
edge points the way the sentence does: what a shape *is*, below what a map *means*.

**`Vocabulary` is the same move for the words, and `RP28` added it.** A `Finding` is raised by gates in
`Minecraft`, `Pgm`, `Analysis` and `Export`, answered by `Api`, and rendered by the WASM client — three
parties with no project above `Domain` that all three reach, so the record in `Domain` grew a hand-written
wire mirror in `Contracts` and two hand-built dictionaries below `Api`, and the three drifted. The closed
string vocabularies had the same shape of problem from the other side: `MaterialKind` is the value the
editor, the HTTP surface and the `style.kind` column must spell identically, and `Minecraft`, which writes
that value, cannot see `Contracts`. Both are now in a leaf that references nothing, so the record is
serialized rather than mirrored and the words are named rather than re-typed. `Contracts` keeps the DTOs and
takes the leaf as its one reference; `Client` still reaches nothing but `Contracts`, `Geom` and, through the
first, this. A DTO field marks the set it takes with `[WordSet(typeof(MaterialKind))]`, which is what puts
the words in the published schema without a second copy of them: the attribute names the declaring class and
the generator reads them off it.

**`Export` added no edge and inverted none.** Every project it reaches (`Domain`, `Analysis`, `Minecraft`,
`Pgm`) was already reachable from `Api` — directly, or through `Data` — so the cut is free in graph terms:
`Api` reaches the same set of projects through one more hop, and the export path is reachable from a script
that wants it without the web host it would never start.

**Two constraints are what force the shape**, and they are the reason there appear to be "two model projects":

- **`Client` cannot see `Domain` or `Pgm`.** It is WASM and references only `Contracts` and `Geom`, so the
  wire DTOs have to live somewhere that does not drag the rest in. That somewhere is `Contracts`.
- **`Analysis` cannot see `Contracts`.** DTOs depending on analysis depending on DTOs is a cycle of intent, so
  anything `Analysis` *and* the client both need is forced into a true leaf. There are two, split by what they
  hold: `Geom` for the algorithms and the shapes measured in them, `Vocabulary` for the finding shape and the
  closed sets of words. The duplicated reflect/rotate is what the first prevents and the duplicated finding
  record is what the second does.

Both are checked by the `.csproj` files rather than by convention: `Analysis` has no `Contracts` reference and
`Client` has no `Domain` or `Pgm` reference.

## 2. The four kinds of project

| Kind | Projects | Charter |
|---|---|---|
| **Pure leaves** | `Geom`, `Vocabulary`, `Migrations` (and `Contracts`, over `Vocabulary` alone; `Domain`, over those two) | geometry algorithms and the shapes measured in them · the finding shape and the closed word sets · the DB schema · wire DTOs · PGM entities and the rules over them |
| **Format and domain logic** | `Pgm`, `Analysis`, `Minecraft` | the `map.xml` codec *and* the layout generator · NTS-backed derivations · Anvil world reading and writing |
| **Persistence and ingest** | `Data`, `Import` | the DB codec, repositories and stores · the parquet→relational CLI |
| **The export path** | `Export` | sketch layout + intent → voxel world + `map.xml`, DB-free — what every driver needs and nothing else |
| **Presentation** | `Client`, `Api` | the Blazor UI · the FastEndpoints composition root |

`Domain` has drifted past "entities" in a way worth naming: alongside `MapModel`, `Region` and `Filter` it now
holds the **rules that several projects share** — `RoomFrames` (the stamped-room resolver), `RoomEdges` (what a
wall's outward direction *is*: its axis, its normal, its opposite), `ObjectiveFootprint`,
`MiningTiers`, `DestroyableMaterials`, `DoorMaterials`, `Gamemodes`, `PhantomErasure`. That is correct rather
than accidental: each is a pure function of the entities, and each has consumers in more than one project
above. It does mean `Domain` is "the PGM domain", not "the PGM data model".

## 3. Sizes, and the two that have outgrown their shape

**The table below is counted, not typed.** `tools/census.sh` writes it and `tools/census.sh --check` fails
when it is stale, because a hand-written census is wrong between every pair of commits and the last one had
drifted in every row before anyone noticed. A folder's count is what it *holds*, recursively, and one holding
another says `(nested)` — `Compose/` is 42 files and 28 direct children, and only the first answers "how big
is `Compose`". The prose around it cites the shape rather than the totals, for the same reason.


<!-- census: generated by tools/census.sh -->
| Project | Files | Lines | Internal shape |
|---|---|---|---|
| `Analysis` | 17 | 3,415 | `Playability/` 8 · `Region/` 3 · `Footprint/` 2 · `Layer/` 2 · `Suggest/` 2 |
| `Api` | 87 | 10,572 | `Endpoints/` 49 · `Services/` 35 · `Http/` 2 · 1 at root |
| `Client` | 187 | 22,997 | `Features/` 109 (nested) · `Components/` 60 (nested) · `Pages/` 7 · `Models/` 5 · `Layout/` 3 · 3 at root |
| `Contracts` | 31 | 2,117 | flat |
| `Data` | 14 | 2,443 | `Features/` 4 · `Map/` 4 · `Theme/` 3 · `Schema/` 2 · `Plan/` 1 |
| `Domain` | 26 | 2,539 | flat |
| `Export` | 8 | 1,606 | flat |
| `Geom` | 44 | 5,363 | `Algorithms/` 20 · `Relief/` 6 · `Render/` 4 · 14 at root |
| `Import` | 4 | 471 | flat |
| `Migrations` | 24 | 1,577 | `Migrations/` 23 · 1 at root |
| `Minecraft` | 78 | 15,000 | `Stamping/` 16 · `Anvil/` 12 · `Palette/` 11 · `Houses/` 10 · `Painting/` 8 · `Render/` 8 · `Dressing/` 7 · `Views/` 4 · `Suggest/` 1 · 1 at root |
| `Pgm` | 148 | 22,915 | `Compose/` 42 (nested) · `Authoring/` 21 · `Evaluate/` 21 (nested) · `Editing/` 11 · `Derive/` 10 · `Shapes/` 10 · `Plan/` 7 · `Sketch/` 7 · `Render/` 5 · `Detect/` 1 · 13 at root |
| `Vocabulary` | 7 | 679 | flat |
<!-- /census -->

**`Pgm` is two projects wearing one name**, and the table above is where that is visible: it is the largest
project in the tree and its folders fall into two groups that share nothing. `CLAUDE.md` charters it as
"`map.xml` parse/edit/generate", which describes the codec at the root plus `Authoring/` (intent → map),
`Editing/` (patch a parsed doc), `Sketch/` and `Detect/`. The rest — `Compose/`, `Evaluate/`, `Shapes/`,
`Derive/`, `Plan/` and its `Render/`, and the larger half by every count — is the **layout generator**, which
touches no XML at all. It reads only `Domain` and `Geom`, so it would stand on its own as a project with no
new edge: the boundary it already respects is exactly the one it would get. Whether to split it is §7.1; what
is not in question is that one charter sentence does not cover the project.

**`Minecraft` is folded by what a file is *for* rather than by what it is** (`A7`), the same shape `Pgm`,
`Analysis` and `Data` have. Six folders, plus the three already broken out:
**`Anvil/`** reads and writes the world on disk (regions, chunks, `level.dat`, the provenance sidecar, the
layer and feature extractors); **`Palette/`** is the block vocabulary (ids, families, geometry, roles,
variants, the recipes and the material names); **`Stamping/`** writes structures into a world (rooms, cubes,
objectives, chests, signs, markers, the claim each one records); **`Houses/`** is the building family, which is
large enough to be its own subject (the plan, the style, the presets, the stamper, windows, roofs, wing
joints); **`Painting/`** is the terrain painter and its profile/theme family; **`Suggest/`** reads a built
world back into the plain facts a derivation needs — blocks by position, and the signs, armour stands and
wool-bearing item frames that carry an author's words. What those facts *mean* — a monument, a core, the
confidence behind each — is `Analysis/Suggest/`, because a derivation does not belong beside the format it
happened to be read out of. `Dressing/`, `Render/` and `Views/` were already broken out.

The folder is `Palette/` rather than `Blocks/` for a reason worth recording: `Blocks.cs` declares a class
called `Blocks`, and a namespace of that name shadows it — `Blocks.Gravel` stops compiling the moment
`PgmStudio.Minecraft.Blocks` exists as a namespace. A folder name is a namespace, so it competes with every
type name in the project.

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

**Two decisions shape representation 6, and both are about what deserves a column.** The map contract is
persisted **hybrid**: real tables with foreign keys for the entities that get listed, queried and edited —
map, team, region, filter, wool, monument, spawn, apply_rule, kit — and JSON columns for the polymorphic
leaves, a region's or filter's type-specific parameters and an apply-rule's event map, where a column per
variant would be a schema per type. Block data splits the same way by volume rather than by shape: the small
**feature** parquet becomes relational rows (`wool_block`, `resource_block`, `chest_item`, `spawner`,
`layer_segment`), while the raw `layer.parquet` — around 7,700 rows for a single map — stays a regenerable
cached artifact in a `map_artifact` blob rather than a row per block. `PgmStudio.Import` replays parquet into
those rows, so migrating an existing map needs no world re-scan; only importing a new one does.

Every artifact — the cached layer, the detected islands, the scan configuration, the authoring intent, the
sketch and plan blobs, the editor's region drafts — is one row keyed by `(map_id, kind)`, and one store keyed
on that kind answers for all of them: `Data/Map/MapArtifactStore.cs` is the only place the table is queried.
A caller asks for bytes, for a deserialized document, or for the artifact's mere presence — which is what
"is this map intent-authored / sketch-origin / scanned" each reduce to. What stays per-kind is only what
genuinely differs: the type a blob deserializes to, and the default a map with no such artifact reads as.

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
| `Vocabulary` | **Earns its place** — the only leaf a gate below `Api`, the HTTP surface and the client all reach, which is what one finding shape and one spelling of each word require. | none |
| `Migrations` | **Clean** — one file per migration, in order. | none |
| `Minecraft` | **Folded (`A7`).** Six folders by what a file is for, plus the three already broken out (§3). | none |
| `Import` | **Clean, identity blurred.** It is parquet→relational replay; it is *not* the world scan, which lives in `Data/Features/WorldFeatureWriter`. | the distinction stated in its own header |
| `Pgm` | **Two charters in one project** (§3). Both halves are internally well-shaped. | the split decision, §7.1 |
| `Analysis` | **Right internal shape** — `Region/`, `Layer/`, `Playability/`, `Footprint/`, `Suggest/`. | none |
| `Data` | **Right internal shape** — `Schema/`, `Map/`, `Features/`, and since then `Theme/` and `Plan/` for the library and plan stores. | none |
| `Export` | **New (`B119`), flat and small.** Seven files — the world builder, the destroy/core/wool scope readers, and the `map.xml` composer — with no DB reference, so `Api` and a headless CLI reach it identically. | none yet; a fold if it grows the way `Minecraft` did |
| `Api` | **Acceptable for a composition root**, though 41 endpoint files and 21 services is where feature folders start to pay. | optional grouping |
| `Client` | **Well organized** — `Pages/` for routable pages, `Features/<Tool>/` for a tool's own bodies, `Components/` for the shared vocabulary, and 11 JS layers under `wwwroot/js/studio/`. | none |

## 6. `tools/` — drivers, dev harnesses and fixtures

70 files, 18,365 lines — never on this map before, despite being larger than every `src/` project but `Pgm`.
It is not one thing: four are real `.csproj` projects the solution builds, most of the rest are single
file-based scripts `dotnet run` builds on demand, and three folders hold no code at all.

**One project-based tool**, a `ProjectReference` graph like any `src/` project and listed in
`PgmStudio.slnx`:

| Tool | References | Is |
|---|---|---|
| `PgmStudio.RoundTrip` | `Pgm`, `Analysis`, `Minecraft` | the corpus regression net (`--goldens`): the four map-level derivations over every corpus map, diffed against `corpus-goldens.json` |

**`Export` exists so the export path is reachable without the web host.** `WorldBuilder` and
`MapXmlComposer` sat inside `Api` until `B119`, which meant a script wanting to build a world carried
ASP.NET Core, FastEndpoints, the DB layer and the Blazor host to reach them. Cutting `Export` out is what
lets a file-based script link the real composition instead of growing a second copy of it — which is what
`tools/` had done, and what `B118` deleted.

**The rest are file-based scripts** — no `.csproj`, no solution entry, each a `.cs` file opening with
`#:project` directives that name the `src/` projects it needs and running as `dotnet run
tools/<folder>/<script>.cs`. **There are seven, and the count is the point** (`CLAUDE.md`, *Investigation
stays local*): three gates over the composer in `compose/` (`reproduction-gate`, `fingerprints`,
`unit-fingerprint`), two in `deriver/` (`figure-check` gates `model.md`'s figures, `envelope-stats` writes
`seed-envelopes.md`), and two operational tools at the root (`seed-library` seeds the database,
`library-map` writes the catalogue map's layout and intent for `POST /map/from-documents`). A script that is
not re-run does not live here; the reading
it took belongs in `docs/` or in the code, and the script belongs in a scratchpad.

**`dotnet run <script>.cs` caches the built app keyed on the script**, so an unchanged script re-runs stale
`src/` output with no error — `rm -rf ~/.local/share/dotnet/runfile/<script>-*` before trusting a measurement
(`CLAUDE.md` *Traps*). And because none of them is in the solution, `dotnet build` at the root never compiles
one: a rename in `src/` leaves a script uncompilable while the solution stays green and nothing says so, which
is what `tools/build-scripts.sh` exists to catch (`B227`). Run it after moving anything a script names; it
retries a build once, because the shared folder produces the odd spurious NuGet failure and a gate that cries
wolf is not one.

**One folder holds fixtures, not code**: `seeds/` — the checked-in plan, intent and layout documents that
22 test files across `Pgm`, `Export` and `Minecraft` read, including the traced real maps `envelope-stats`
learns the evaluator's bands from. It references no project.

The bar is the same one the scripts face, asked the other way: a fixture stays if it is **read** and could not
be produced again. A capture taken to look at once is investigation like any other, and a corpus nothing in
the repo reads belongs beside the corpus repositories rather than inside this one.

## 7. Open decisions

**7.1 — Should the layout generator be its own project?** `Compose/`, `Evaluate/`, `Shapes/`, `Derive/` and
`Plan/` never touch `map.xml` — they are the larger half of `Pgm` by every count in §3 — and they reference
only `Domain` and `Geom`, so `PgmStudio.Compose` would add no dependency edge; the split is free in graph
terms. What it would buy is
that `Pgm`'s charter sentence becomes true again and the generator's own dependencies become visible (today it
can reach the codec without anything noticing). What it costs is a rename across every citation and the
`PlanCompiler` seam sitting on a boundary rather than inside one. **Recommendation: split it when the
generator next needs a structural change**, not as a standalone refactor.

**7.2 — `MapIntent` → `Domain`?** It is a pure, zero-dependency data model and could join the other map models
there, leaving only the generators in `Pgm/Authoring/`. **Recommendation: leave it in `Pgm`.** `Domain` is
what a *parsed* map is; `MapIntent` is what an author *wants* — a different lifecycle — and it sits beside the
only code that consumes it. Nothing below `Pgm` needs it, and the client posts loose JSON (§7.4).

**7.3 — Rename `Contracts`?** The name reads generic, but it is the correct API term and it is the one model
project `Client` can see. **Recommendation: keep it**, with a header comment separating it from `Domain`. A
rename is churn across every endpoint for no boundary change.

**7.4 — The intent contract is stringly typed.** The Configure client builds a loose `JsonObject`
(`AuthoringContext`, `Wizard.Intent`) and PUTs it; `AuthoringIntentEndpoints` deserializes into `MapIntent` by
camelCase convention. This is *why* `MapIntent` can live in `Pgm` without breaking `Client`'s leaf set, and it
is unchecked in both directions. If it bites, the fix is an intent DTO in `Contracts` shared by both sides;
until then it is a deliberate trade of safety for decoupling.

**7.5 — Two ingest pathways.** `Import` replays parquet → rows as a CLI and references neither `Minecraft` nor
`Analysis`; `Data/Features/WorldFeatureWriter` scans an Anvil world → rows and pulls both, driven by `Api`. So
"how a world becomes rows" has two implementations. **Recommendation: leave it split.** `WorldFeatureWriter`
is DB-write-shaped and sits beside the rows it writes, and unifying would drag `Minecraft` and `Analysis` into
the replay CLI for no boundary gain — a project-boundary refactor, not a fold.
