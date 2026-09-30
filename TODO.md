# pgm-studio — TODO (current focus)

The **Now & Next** board — only the *current focus theme* lives here. Everything not in the immediate
slice is in **`BACKLOG.md`** (the long tail); shipped capabilities are in **`FEATURES.md`** (the Done
column). The three move left → right: **`BACKLOG.md` → `TODO.md` → `FEATURES.md`**.

**Holds only open work:** `[ ]` to-do, `[~]` in progress — **never `[x]`.** When a task ships, a commit
lands (its message references the id), the task **leaves this file**, and a line is added to `FEATURES.md`.
Board rules live in `CLAUDE.md` (§ "Status & task board").

**Three numbers are the author's and are not to be re-derived.** A protection region is at most **20×30**
blocks (`ST10`), a building footprint at most **20×20** (`ST9`), and the smallest room with no building over
it is **4×4** (`WX2`). A dressed prop's 192-cell ceiling (`HP3`) and a room building's 20×20 measure the same
concept since `WE71`, and holding them apart is a deliberate not-yet.

## The programme: a board's source, every change to it, and the words it is stated in
**The author put this programme first.** The studio keeps the source a board is stated in and every change to
it, hands the author's hand edits to the agent's next round, and hands out the vocabulary the source is
written in. The two groups below do not depend on each other and may run side by side; `pgm-studio-mapgen`
moves onto both as the next group in `BACKLOG.md`, and the layer word (`B264`, `WE28`, `TS64`) waits behind
it.

### A map is its source and every change to it

- [ ] **RP91 — The finish becomes a studio document, the refinement, and one route stores a map from its
  source.** `pgm-studio-mapgen/tools/drive.py` applies the finish to the compiled documents in Python
  (`patch_layout`, `patch_intent`), stores the result through `POST /map/from-documents`, posts each vertex
  edit and bend as a request of its own and finishes again. Move that into the studio: a `Refinement` in `Pgm`
  beside `PlanCompiler`, typed in the schema and read with `RQ3`, carrying every statement the finish does,
  and `PUT /map/{slug}/source` taking `{name, plan | layout + intent, refinement, origin}`, which compiles,
  applies (vertex edits and bends in the document), gates, stores one change, then finishes and projects
  once; `?dry=true` answers the diff and stores nothing. It retires `from-documents`, and `drive.py` and
  `tools/sculpt/board.py` switch in the change that deploys it. The word `finish` keeps one meaning, the
  rasterize step. `docs/tools/flow.md`, `docs/tools/sketch.md`.

  *Evidence: storing Gypsum Reach is 47 requests, 43 of them vertex edits, and its 122 KB finish never
  reaches the studio.*

- [ ] **RP92 — An apply over changes its source has not seen is refused, and the changes are handed over.**
  A source states the change it was built against (`after`). Where another writer's changes have landed since,
  `PUT …/source` answers `409` with one `SR1` per change: its writer, its time, the note pinned to it, and the
  edit written as refinement entries — a shape's outline and theme, a prop placed or moved, a theme changed —
  or, where only the plan can state it, the document path and both values. `?discard=` names the changes an
  apply drops, and the drop is recorded on the change. A map with no refinement never meets the refusal.
  `docs/tools/flow.md`, `docs/refusals.md`; the `pgm-board` skill's first moment reads the changes since the
  last apply beside the open notes.

  *Evidence: the author edits a board by hand to fix it or to show the agent how, and the next drive replaces
  the edit without a word.*

- [ ] **TL32 — A refinement names a material, theme or style instead of copying it.** Two kinds of name. A
  local one: a `materials` registry states a material once, and a theme's bucket says `{"use": "strata"}`. A
  library one: `{"library": "dunes"}` wherever a material, theme, room style, prop style or biome is accepted,
  with the fields that differ stated beside it, resolved at apply into the copy the stored layout holds and
  recorded with the library row and a hash of what was copied — the record `themeSources` keeps for themes,
  for every kind. A library edit never rebuilds a stored board; `GET …/state` names the entries whose row has
  moved on, and the next apply takes the new one and its diff shows it. `docs/tools/library.md`,
  `docs/tools/flow.md`.

  *Evidence: Gypsum Reach's finish carries 18 copies of one 4,041-byte strata material, 80 KB of its 122.*

### The schema is the vocabulary, and the kit is made from it

- [ ] **RP65 — The layout DTO still says `JsonElement` where its own routes say `DressingDoc` and
  `BiomeField`.** `SketchLayout.Dressing` and `.Biome` are `JsonElement?` because their types live in
  `Minecraft` and `SketchLayout` lives in `Pgm`, which are siblings over `Domain` + `Geom` — the fields' own
  docstrings say so (`SketchLayout.cs:47-66`). The part routes publish both models and the store-time gate
  reads both (`FEATURES.md`), so what is left is the whole-layout route: `GET`/`PUT /map/{slug}/sketch` names
  the two fields with no shape under them, and a reader who starts from the document rather than from the
  parts finds two holes in it. Move the dressing and material model down to a project both reach, or publish
  the two schemas from `Minecraft` and reference them from the layout.

- [ ] **RP93 — The schema says what the wire does.** A client generated from `/api/openapi/v1.json` fails
  against the studio in seven places. Thirteen enums the schema lists as words cross as integers — only the
  generator carries `JsonStringEnumConverter` (`Program.cs:74-75`), so `GET …/sketch/props` answers
  `"style": 0` where `StrokeStyle` promises `solid`. Thirty-two fields publish no type, `SketchLayout.themes`,
  `SketchRoomStyles` and the load's own documents among them (`RP65` is the dressing and biome). Every
  polymorphic leaf repeats `additionalProperties: false` beside its base, which a strict validator reads as
  refusing every real material. 235 of 260 operations publish none of the query words they read, no field
  publishes its default, operation ids are class names (`PgmStudioApiEndpoints…`), and the security scheme
  says JWT where a token is opaque. Each becomes a count in `SchemaCompletenessTests` that only moves down.
  `docs/architecture.md`, whose schema figures are stale.

- [ ] **RP94 — Every write route's schema carries a body known to be accepted.** `DocumentedBodyTests` posts
  the 11 fenced JSON bodies in `docs/tools/*.md` that name a route and asserts a 2xx; 26 more name none and are
  never posted, all fourteen material examples in `library.md` among them. Route every fence, share the test's
  extractor with a schema processor, and attach each block to its operation's `requestBody` examples from
  docs embedded in the `Api` assembly, so `/api-docs` and the kit show a body the test has proved. A write
  route with no example becomes a count in `SchemaCompletenessTests`. `docs/architecture.md`, which counts 8
  bodies against 93 write routes where there are 11 against 107.

- [ ] **RP95 — The studio serves a Python kit generated from its own schema.** `GET /api/kit.py`, generated
  from the live document on request with the document's hash as its `ETag`, and committed nowhere: one
  constructor per document type with the studio's field names, allowed words and descriptions, writing only
  what the caller stated so the studio's defaults stay its own; checks before the request; a `Studio` client
  over every operation that waits out `429`, prints `warnings` and raises a refusal with its findings; and
  `find()` over the descriptions. The `Api` tests generate it, compile it with `python3 -m py_compile` and
  build every documented body through it. `docs/architecture.md`'s *"no generated client, and will not get
  one"* is amended to name this one, for Python callers; the Blazor client keeps reading `Contracts`.

  *Evidence: outside `specs/archive` in `pgm-studio-mapgen`, `solid` is defined by hand in 54 scripts,
  `layered` in 24 and `cell` in 22.*

- [ ] **TS125 — What mapgen's kits compute becomes something a refinement states.** Four computations live
  in the kits and scripts: `coast_edits` (points inserted along named edges and pulled inward), `ring` (a lobed
  outline), a fluid's floor copied by hand from `column` reads, and mirroring (`image(p)`, in two conventions,
  `(−x, −z)` and `(−x−1, −z−1)`). State them instead: a `coast` on a compiled shape — its edges, depth and
  seed, inward only and tested against the ring's own inside; lobes on a circle or ellipse; a fluid that fills
  to a level inside its ring; and a fan on hills, spawners and edits to a shape on the axis, which follow the
  board's symmetry unless they say otherwise. `docs/tools/sketch.md`, `docs/pgm/control-points.md`.

  *Evidence: `coast_edits` pulled toward the ring's centroid and put Sootcombe's coast two blocks past the end
  of its east wall (note 48); Gypsum Reach's wash floor is 72 points copied from reads.*
