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
written in. The source and its changes have shipped; what remains is the vocabulary the source is written in.
`pgm-studio-mapgen` moves onto both as the next group in `BACKLOG.md`, and the layer word (`B264`, `WE28`,
`TS64`) waits behind it.

### The schema is the vocabulary, and the kit is made from it

- [~] **RP93 — The schema says everything the wire does.** 41 fields publish no type —
  `SketchLayout.themes`, `SketchRoomStyles`, `SketchShape.material`, the documents an answer carries whole
  (`CompiledPlanDto`, `MapSourceDto`, `MapChangeDocumentsDto`) and the refinement's statements among them —
  where `CarriedShapes` can name the type, with the map document's open encodings named as open. 24 operations
  read 48 query words they do not publish, no field publishes its default, operation ids are class names
  (`PgmStudioApiEndpoints…`), and the security scheme says JWT where a token is opaque. Each becomes a count in
  `SchemaCompletenessTests` that only moves down. `docs/architecture.md`, whose schema figures are stale.

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
