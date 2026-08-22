# pgm-studio — TODO (current focus)

The **Now & Next** board — only the *current focus theme* lives here. Everything not in the immediate
slice is in **`BACKLOG.md`** (the long tail); shipped capabilities are in **`FEATURES.md`** (the Done
column). The three move left → right: **`BACKLOG.md` → `TODO.md` → `FEATURES.md`**.

**Holds only open work:** `[ ]` to-do, `[~]` in progress — **never `[x]`.** When a task ships, a commit
lands (its message references the id), the task **leaves this file**, and a line is added to `FEATURES.md`.
Board rules live in `CLAUDE.md` (§ "Status & task board").

## The studio's eyes: what it can be asked to show, and what it says about what it drew

Five entries out of one afternoon of driving the pipeline, and one concept under them. The studio can be
asked for almost anything and describes every answer — **except what a board looks like once it is built**.
It cannot show a world at all outside a CLI; where it can draw a picture, nothing declares how to ask for
one; the document that would draw one is refused for the order of its own keys; a board carrying no finish
at all raises nothing, because there is nothing stated to disagree with; and what it records itself having
drawn is a column wider than what it laid.

The order is what each one unblocks. `TL2` is a reader fix that makes the previews answerable today; `RP50`
puts the way to ask for them in the schema; `TS15` gives the finish stage a voice; `WS6` is the substantial
one, and `WE14` is a claim to correct while that code is open.

- [ ] **TL2 — A material is refused for the order of its own properties, and a mis-shaped bucket is blamed
  on the studio.** Two faults in one reader, both about whose fault a document is.

  **`kind` must be the first property of a material object.** Move it last and nothing else, and
  `POST /terrain/material-preview` and `/room-styles/preview-snapshot` answer **400 — "a material names no
  kind"** on a document whose `kind` is right there. The discriminator is read positionally, so any generic
  tool that reorders JSON — a formatter, a re-serializer, `json.dumps(…, sort_keys=True)` — breaks a document
  that worked. Read `kind` wherever it sits (`AllowOutOfOrderMetadataProperties`, or the converter's own
  lookahead); a caller cannot be asked to preserve key order in JSON, where it carries no meaning.

  **A bucket given the wrong shape answers 500.** `{"surface": {"kind":"solid","id":2,"data":0}}` — a bare
  material where the surface bucket takes a stack — takes `POST /terrain/theme-preview` down as *"the studio
  failed to answer this request"*. The document is wrong and says so; the answer blames the reader. `RQ1` at
  400, naming the field, is what the same class already answers everywhere else.

  *Both found by `tools/drive.py` taking the render of every authored house: eight refusals across four
  styles that preview at 200 when their keys are left where the author wrote them.*

- [ ] **RP50 — Five routes declare a PNG answer and nothing declares how to ask for one.** `?format=png` and
  `?view=` are read by `PngAnswer.Wanted`/`.View` straight off `HttpContext.Request.Query`, so they reach no
  parameter list: `room-styles/preview`, `preview-snapshot`, `terrain/theme-preview`, `material-preview` and
  `prop-preview` each publish `image/png` as a response content type over `parameters: []`. The schema says a
  picture can come back and nothing says how to get one, which leaves the one instruction the brief cannot
  drop — *read the schema, not a document* — false at the five routes that draw a picture.

  Declare both as query parameters where `.AlsoPng()` is applied, so the flag that makes a route answer PNG
  is also the flag that documents it, and let `view` carry the names each route actually has —
  `preview-snapshot` refuses `isometric` and `cutaway` by name, which is a closed word set the schema can
  publish rather than a sentence in `sketch.md`. `docs/tools/library.md`'s endpoint table gains the two
  columns in the same commit.

  **A third knob is missing rather than undeclared: there is no size.** A house section answers **72 × 108**
  and a theme swatch **128 × 104**, which is the picture `AD-S6` asks an author to read a roof idiom off. A
  scale or a width belongs beside `format` and `view`.

  *Found by the run-5 authoring test: an agent that read the schema first could not find the query surface
  and fell back to `sketch.md` prose, which is the failure the schema exists to prevent.*

- [ ] **TS15 — A board with no finish at all is the one silence the sketch stage keeps.** `SK3` names a shape
  citing a theme the layout does not carry, `SK4` a shape drawing nothing, `SK7` a layout rasterizing to no
  ground — but a layout carrying **no** theme registry, **no** relief and **no** props raises nothing, because
  there is nothing to disagree with. Add `SK8` as a **complaint** on `POST /api/map/{slug}/sketch/finish`,
  beside `SK6`/`SK7` in `Pgm/Sketch/SketchRules.cs`, naming which of the three the stored layout is missing.
  A complaint rather than a refusal: a bare test board is legitimate, and finishing is the stage that declares
  the drawing done, which is why the other two live there.

  The gap is not the author's attention, it is the pipeline's. A weak model fixes every finding it is given
  and stops where the findings stop; silence reads as approval.

  *`maps/haiku-r5-hollow-crown` exported clean with `themes: null`, `mapTheme: null`, `relief: null`,
  `roomStyles: null`, `dressing.props: []`. Every stage answered 200. The same author fixed `ST9`, `G8`,
  `LN2`, `PL11` and `DC3` the moment each was raised. Haiku's four run-4 boards are the same shape.*

- [ ] **WS6 — The read-backs answer over HTTP, and say what each one answers.** Everything an agent does
  runs through the API and the API describes itself — except the one thing it does *after* building, which is
  look at what it built. Eight renderers live in `Minecraft/Render/` (`TopDownRender`, `SectionRender`,
  `HeightProfileRender`, `SurfaceReport`, `TraversabilityRender`, `StructureFinder`, `ColumnReport`,
  `MirrorReport`) and reach a caller only through `PgmStudio.RoundTrip`'s flags, which no schema names — so a
  brief has to carry a table of them and an agent has to know a .NET binary exists.

  `Api` already references `Minecraft`, and the pattern is already built: `?format=png` through
  `PngAnswer` + `.AlsoPng()`, which six routes use, and `/map/{slug}/coverage` proves a world read can be
  answered from stored segments and the layout artifact rather than from a region directory on disk. Settle
  the source per read — `--section` and `--column` want voxels, which is what `/export` builds — and give
  each its own route.

  **What each read answers is then written once**, as the endpoint description the schema publishes, and the
  CLI prints the same sentence. Three caveats belong in it, each having cost a reader a wrong conclusion:
  `--traversability-map` reads an approach wall's cobweb course as impassable, so every board carrying one
  reports isolated markers (`B99`); `--buildings` finds roofs by material and cannot see a town this studio
  built (`B149`); `--section` samples **one plane** (`B129`). Withdraws `B245`, which asked for that sentence
  in `--help` alone.

- [ ] **WE14 — An approach wall is claimed one column wider than it is built, on both axes.**
  `StructureStamper.StampWall` walks its footprint **max-exclusive** — which is what the intent's rect means,
  and `SketchWorldBuilder` says so — while `ClaimStructures` hands the same rect to
  `WorldProvenance.ClaimRect`, which walks it **max-inclusive**. `StampRoomFloors` already takes the
  foundation cells from the stamper rather than re-deriving them; a wall wants the same treatment, so the
  claim is the cells the stamp filled. Every read that trusts the sidecar — `--topdown --layer structure`
  says `STRUCTURE READING: RECORDED PROVENANCE` — draws the wall a column thicker than it plays, and a bedrock
  line's thickness is exactly what decides whether it can be built over.

  *`maps/grok-ridge`: the sidecar draws 26 × 3, the world holds 25 × 2. `--column` at `(−25, 36)`,
  `(−12, 36)` and `(−1, 36)` reads stone brick y17 — the mid terrace, no wall — while `(−25, 35)`, `(−12, 35)`
  and `(−1, 34)` read cobweb y21 over bedrock y20…16.*

## What the front door still cannot say, and the copies that outlived their reason

Not a headed group in `BACKLOG.md` — these were scattered across three of them, and they are one concept all
the same: the residue of *The boundary*. That programme made the surface describe itself and stopped three
steps short. **One key rides on every success and no schema names it**, which is the half of the contract the
last two entries never reached. **A route is still written in more than one place**, on the client this time —
the same question asked of the caller rather than the server. **An answer shape says what type a field is and
not what it is**, the mirror of the request side.

Beside them, three duplications the same rule catches, each already named as one: one write verb asked nine
ways, one team record declared four times, and one runtime answer written out as prose in two repositories,
which `GET /map/{slug}/layers` unblocked.

**One decision was taken rather than filed.** RFC 9457 Problem Details was weighed against the studio's own
refusal envelope and declined — the interoperability it buys needs a caller outside this deployment and there
is none, while the dereference it is prized for is already reachable from the `rule` each finding carries.
The reasoning is `docs/design-decisions.md` § *The HTTP surface*; the entry that asked it is retired.

- [~] **RP23 — `docs/tools/capabilities.md` is 707 lines answering "what can I ask for", which the API now
  answers itself.** The schema names every route, its body and its failure codes; `GET /api/rules` names
  every refusal with its fix; `GET /map/{slug}/layers` puts the allowed moves on the map's own response.
  What prose is good at and this file is not organised around is the other half: **how to make a good map** —
  what an objective needs around it, what the corpus does — as against **what the system can be asked for**.
  Split it on that line: the capability half goes, the craft half moves to where its subject lives under
  `docs/gameplay/`.

  The mapgen half landed: `pgm-studio-mapgen`'s six root documents became two, and `AUTHORING-BRIEF.md`
  points at the four self-describing reads instead of restating them. This entry is the studio's own side.
