# CLAUDE.md — pgm-studio

## What this is
A from-scratch **ASP.NET Core** rewrite of the Python `pgm-map-studio`
(`/media/sf_repos/pgm-map-studio`), which stays in place as the **behavioural reference**
(routes, the data contract, analysis oracles, and the 350-map corpus). Goal: full feature
parity in C# with all map data in **MariaDB**.

## Stack (decided, do not relitigate)
ASP.NET Core · FastEndpoints (`/api`) · Blazor WebAssembly hosted by the backend ·
MariaDB · FluentMigrator · linq2db (MySqlConnector) · TUnit · Parquet.Net.
Target framework **net10.0** (SDK 10.0.109; pinned in `global.json`).

## Code placement (which project a piece of code belongs in)
The rule: **a unit of code lives in the lowest (most-depended-upon) project that (a) already has the
dependencies it needs and (b) every consumer can already reach** — push it down for reuse, never up.
That plus a separation of concerns by *kind*:
- **`Domain`** = the entities/value types (pure, zero deps). What things *are*.
- **`Contracts`** = the wire DTOs shared by client and server (`MapStage`, the analysis DTOs). How
  things *cross the API*. It is **not** a dumping ground for algorithms — it is reachable by `Client`/`Pgm`/
  `Api` but **not** by `Analysis`, so anything `Analysis` also needs must **not** live here.
- **`Pgm`** = `map.xml` parse/edit/generate. **`Analysis`** = NTS-backed derivations (refs `Domain` only).
  **`Minecraft`** = world/Anvil. **`Data`/`Import`** = persistence/ingest. **`Client`** = Blazor (refs
  `Contracts` only). **`Api`** = composition root (refs everything).
- **Pure algorithms shared by many projects** (geometry scalar math, shapes, generative layout algos)
  belong in a **dependency-free leaf below them all** — this is **`PgmStudio.Geom`** (`Symmetry`,
  `Polygon`; future: shape model, TSP/annealing layout). It references **nothing** (not even `Domain`),
  so every consumer can take it without dragging in a transitive dep. `Pgm`/`Analysis`/`Client` reference
  it directly; `Api` transitively. (Named `Geom`, not `Geometry`, because `Analysis` uses
  NetTopologySuite's `Geometry` type everywhere and a sibling `PgmStudio.Geometry` namespace would shadow
  it.) Do **not** put algorithms in `Contracts` (the DTO leaf) — `Analysis` can't reference it, which is
  what forced the old duplicate reflect/rotate copy. See `docs/contracts/geometry-consolidation.md`.

**Client (Blazor) folder layout** — three buckets, by role not by "page":
- **`Pages/`** = standalone **routable** pages only (`Index`, `Maps`, `Design`, `NotFound`) — namespace
  `PgmStudio.Client.Pages`.
- **`Features/<Tool>/`** = one **feature bundle** per tool: its routable `*Tool` host (the `@page`) plus that
  tool's **private** bodies — phases, steps, inspectors, layout (`Configure`/`Edit`/`Sketch`/`Plan`/
  `Generator`). Folder-matched namespace `PgmStudio.Client.Features.<Tool>`, so a host and its bodies
  resolve each other with no `@using`. A body used by exactly one tool lives here; a body shared across
  tools moves to `Components/`. **Component-name altitude**: `*Phase` for a whole phase (single-step, or a
  phase that hosts its own steps inline), `*Step` for one step of a multi-step phase (see
  `docs/contracts/tool-consistency.md`).
- **`Components/`** = the shared, tool-agnostic UI library — deliberately **flat** namespace
  `PgmStudio.Client.Components` regardless of subfolder, so any atom/shell resolves without per-subfolder
  usings.

## Key data decisions
- **Map contract persistence = hybrid.** Real tables + FKs for entities we list/query/edit
  (map, team, region, filter, wool, monument, spawn, apply_rule, kit…); JSON columns for the
  polymorphic leaves (region/filter type-specific params, apply-rule event maps).
- **Block data = features relational, raw cached.** Small feature parquet → relational rows
  (wool_block, resource_block, chest_item, spawner, layer_segment); raw `layer.parquet`
  (~7.7k rows/map) kept as a regenerable cached artifact (`map_artifact` blob), not row-per-block.
- **Parquet → relational** via Parquet.Net in `PgmStudio.Import`; no world re-scan needed to
  migrate existing maps (only to import new ones, M7).
- Contract invariants to enforce (from the reference app): wools grouped by colour with
  deterministic ids; region/filter registries id-keyed; compound `children` + transform
  `source_id` are string-id refs; owner team derived from capturing `monument.team`.

## Environment (easy to lose)
- **Running in a cloud container (Claude Code on the web)? Read `docs/cloud-setup.md` FIRST** — the
  cloud runbook: sandboxed foreground shells (run `apt`/`dotnet` as background commands), SDK via
  apt only (the Microsoft CDN is firewalled), dead PPAs to remove, MariaDB without systemd. The
  rest of this section describes the local VM.
- **.NET 10** installed via apt; **MariaDB 10.11** running (systemd, enabled). DB `pgm_studio`,
  user `pgm`/`pgm_dev_pw` on localhost.
- The VirtualBox shared folder hosts the solution fine, but `dotnet run` cold-start is slow and
  the first WASM load can take seconds — **use `./tools/dev.sh`** (builds once, runs the binary;
  background on :7894). Warm requests are sub-ms.
- Reference Python app runs on :7892 (`pgm-map-studio/tools/studio-dev.sh`) for parity checks.

## Tests
TUnit, one test class per source unit, mirroring `src/`. `dotnet test` is **not** the path on
the .NET 10 SDK (VSTest bridge removed) — run a test project directly:
`dotnet run --project tests/<Project>`. Synthetic fixtures only; corpus/round-trip harnesses
live under `tools/`, not `tests/`.

## Naming
- **No single-letter identifiers outside a lambda.** `laneWidthCells`, not `w`; `demand`, not `d`; `seat`,
  not `s`. Longer is fine even when it costs a line wrap. Inside a lambda a short binder is expected
  (`.Where(b => b.Kind == …)`), and the axis/index conventions stay: `x`/`z` for plan coordinates, `u`/`v`
  for the symmetry frame, `i`/`j`/`k` for loop indices. Those are the notation, not laziness.
- **No abbreviations of a type's own name.** `abutment`, not `iface`. C# has no keyword clash to dodge.
- **A name must not promise the wrong category.** `BoxJoint.Interface` read like it held a C# interface; it
  holds a shared edge interval, so it is `BoxAbutment`/`.Abutment` — the word the docstrings already used
  whenever they had to explain it. Same reason `BoxJoint.Grant` is not `Offer`: a host publishes a capacity,
  a joint records a selection, and one name for both hid that they are different quantities.

## Doc style
`model.md` is a paper, not a reference card — and the other seven follow it. **Prose carries the claim; a
table supports one already made.** A table gives structure but never says how things connect, so it may not
be the first statement of an idea. Take one topic, explain it to its end, then take the next — in **pipeline
order**, not in the order the decisions happened to be made. State mechanism as fact: no second person, no
"we", no changelog ("this used to be…"). Where something is genuinely a catalog — a code map, a rule id
table — a table is right and prose would be padding. `model.md`'s shape-model and allocate sections are
the worked examples.

## Code comments
Comments stay **purely functional** — describe what the code does and why. **Never** reference the
Python reference app ("port of", "mirrors the reference", parity/oracle) or implementation-phase /
task ids (`NS`, `N00`, `B8`, `P5`, `ND2`, …). Existing non-conforming comments are swept separately
(see `TODO.md`).

## Git
Commit **only when the user explicitly asks**. **Don't push** unless asked. End commit messages with a
`Co-Authored-By:` trailer naming **the model that actually wrote the commit** — the model fills in its own
name rather than reading a hardcoded one from here, so the trailer stays true as the model changes.

**Branch: depends where the session runs.** On a local machine, **commit directly to `main`** — no feature
branch, keep history linear; if a branch already exists, fast-forward `main` to it. In a **cloud container**
(Claude Code on the web) the session is handed a designated branch and pushing anywhere else is refused, so
there the rule is: develop and push on that branch, and never fast-forward `main` onto it from inside the
container.

## Status & task board
Three files, three Kanban columns — keep them current, **never duplicate a task across them**:
- **`BACKLOG.md`** — the **long tail**: open work not in the current focus (`[ ]` to-do, `[~]`
  started-but-parked). The *Later* column.
- **`TODO.md`** — the **current focus only** (`[ ]` to-do, `[~]` in progress). The *Now & Next* board —
  kept small.
- **`FEATURES.md`** — the catalog of **shipped** capabilities (the "done" half), by area, with the
  task id(s) that delivered each (for git traceability). The *Done* column.

Tasks flow left → right: **`BACKLOG.md` → `TODO.md` → `FEATURES.md`**.

**Task-board rules** (the board kept exploding; these keep it honest):
1. **A task lives in exactly ONE file.** Never duplicate it across `BACKLOG`/`TODO`/`FEATURES` (two stale
   copies is the failure mode). Neither `BACKLOG.md` nor `TODO.md` ever holds `[x]`.
2. **Done = a line in `FEATURES.md`.** When a task ships a commit lands (its message references the id),
   the task **leaves `TODO.md`**, and — if it's a shipped capability — one line is added to `FEATURES.md`.
3. **`TODO.md` is the current focus, kept small.** Only the active theme's now/next tasks (soft cap
   ~6–12). Pull the next theme **up from `BACKLOG.md`** when it drains; if `TODO.md` bloats, push items
   back down. New tasks land in `BACKLOG.md` (or `TODO.md` if they ARE the focus).
4. **Ids are section-letter+number, GLOBALLY unique + stable across all three files.** Moving a task
   between files **never** changes its id; never renumber or reuse (`grep <id> TODO.md BACKLOG.md` must
   hit exactly once — commits + memory reference ids).
5. **Same sections, same order in `TODO.md` and `BACKLOG.md`** so a task slots into the matching section
   wherever it lives. Sections (few + stable — don't spin up a new category for one task): Authoring
   (`N`), Sketch tool (`S`), Editor/canvas infra (`C`/`CV`), Backend/pipeline/internals (`B`/`P`/`A`),
   Layout generation (`G`), Lower-priority/parked.
6. **No trailing "Next:/Remaining:/Deferred:" notes inside a task.** Future work is its **own** `[ ]`
   task in the right section (in `BACKLOG.md` if not immediate), not a footnote on a (near-)done one.
7. **`[~]` describes only what REMAINS.** When a task is partly landed, reword it down to the open
   slice; the landed part moves to `FEATURES.md`.
8. **File a task where its REMAINING work lives.** Backend done + only UI left ⇒ it belongs in the
   feature/UI section, not the backend section.
9. **Deferred *decisions* are parked** in `BACKLOG.md`, clearly marked with the blocking question — not
   interleaved with actionable tasks.

## Layout generation — the docs
**All of them live in `docs/generator/`** (one folder per studio tool; the others follow). Eight files,
no others — a new generation doc means one of these grows, not a ninth file:

| File | Is |
|---|---|
| `model.md` | **canonical** — the glossary (locked terms), the pipeline, and how a layout is actually constructed (request → budget → allocate → fill → carve → gate). Governs on any disagreement. |
| `vocabulary.md` | the **living type catalog** — every type as a map concept, by pipeline order. **When a task adds, renames, or retires a generation type, update its row in the same commit** (same discipline as the task board). |
| `rules.md` | the rule law — every CT/SP/WL/LN/HB/FR/MD/BZ/EL id and number. Amended only by its correction protocol. |
| `evaluator.md` | the deriver-measurable and evaluator-term catalogue. |
| `audit.md` | the **measured record of where the code and `model.md` disagree** — the evidence behind the open G-tasks. An entry leaves it when its fix lands. |
| `seed-stats.md` · `seed-envelopes.md` | measured corpus data (envelopes is generated — do not hand-edit). |
| `ideas.md` | the G-track idea pool (ids preserved; pull one onto the board when it becomes the focus). |

**`tools/compose/showcase.cs` is `model.md`'s live twin** — the same model as one HTML page with every
figure emitted by the real generator (`dotnet run tools/compose/showcase.cs`). It cannot drift, so when
prose and showcase disagree, suspect the prose. Keep it running: it is the fastest check that a model
change is real.

**Do not describe unbuilt machinery in the present tense.** A doc that says a type exists when it does
not is worse than a gap — the whole `WoolApproachShape`/`FamilyDock`/`ComposeTargets` class of drift came
from exactly that. Mark unbuilt things with their task id, or leave them out.

## JS dependencies — vendor, never fetch
The rule is **no npm dependencies in the repo**: no `node_modules`, no lockfile, nothing whose install
step runs code. Node itself is fine and already load-bearing — `package.json` marks the hand-written
`.js` as ESM and `npm test` runs Node's built-in runner (`tools/js-test.sh`), zero dependencies, which
is what lets the suite run from the VirtualBox shared folder at all.

- **Browser libraries are vendored**, not installed and not fetched: one reviewed, pinned,
  self-contained file under `wwwroot/js/studio/vendor/`, with its regenerate command in the header
  comment. Two today — `polygon-clipping.js` (esbuild bundle) and `lucide-icons.js`
  (`tools/vendor-icons.mjs`, which also has a `--check` staleness gate).
- **Build and test tools are never installed into the repo.** Fetch with `npm pack` (download only —
  no install, no lifecycle scripts, which is where npm's supply-chain risk actually lives), or expect
  them globally: the e2e harness resolves Playwright from a global install and says so if it can't
  (`tests/e2e/lib/harness.mjs`). Test infrastructure is never shipped, so it stays a developer-machine
  prerequisite rather than a repo dependency.
- **Never a runtime CDN tag.** It is strictly worse than the npm dependency it avoids: unpinned, fetched
  by every user's browser, unreviewable, no integrity check, and dead the moment egress is restricted.
  `lucide@latest` from `cdn.jsdelivr.net` was all five — no icon rendered in the cloud container, and
  three icon names had already been renamed upstream (C30).
- If a vendored subset can miss a name (icons are named dynamically from C#), the shim must **fail
  loudly** — a console error, which the e2e smoke sweep turns into a failed page. A silent blank is the
  bug being avoided.

## Verification & gotchas (load-bearing, easy to lose)
- Run the app with **`./tools/dev.sh restart`** (`:7894`); after a host reboot MariaDB auto-starts
  but the dotnet bg process doesn't, and the claude-in-chrome MCP needs reconnecting (extension panel).
- Parity harnesses in `tools/PgmStudio.RoundTrip` (`--categorize`/`--buildability`/`--traversability`/
  `--wool`/`--extract`/`--islands`/`--authoring`); regenerate Python oracles into `/tmp/pyfresh` (wiped on
  reboot) via `parser.parse + serializer.to_dict` over the corpus.
- **`dotnet run tools/deriver/figure-check.cs` gates the shape-model section's ASCII figures in `model.md`.** It parses every labelled
  grid **out of the doc** (never a copy) and pushes it through the classifier that names that kind of thing —
  a body figure through `ClassifyBody`, a family figure through `ShapeClassifier`, a negative space through
  `BodyEdges`. Run it after editing a figure or a classifier: three of the drawn figures were wrong on its
  first run, and an ASCII shape that reads as the wrong family cannot be spotted by eye.
- **`dotnet run <script>.cs` caches the built app and will NOT pick up `src/` changes.** The file-based
  single-file tools (`tools/compose/*.cs`, the `#:project` scripts) build into
  `~/.local/share/dotnet/runfile/<script>-<hash>/`, keyed on the **script**, so an unchanged script re-runs
  its old binary against the old project output — it reports pre-change numbers with no error. This silently
  invalidates any before/after measurement. Before measuring a `src/` change, **`rm -rf
  ~/.local/share/dotnet/runfile/<script>-*`** (or edit the script). A brand-new script file always builds
  fresh, which is why a scratch copy can disagree with the committed tool.
- **The `map.xml` contract is no longer checked against the Python oracle — `--parity` is gone (B30).** The
  C# contract deliberately exceeds the reference's (kit `force`/`effects`, `destroyables`/`cores`/`modes`,
  the OB4 group-attribute fix the reference gets wrong), so on those the oracle is *silent*, not
  authoritative, and the comparison reported a red it could never go green on. **PGM itself
  (`/media/sf_repos/PGM`) is the reference for the contract**; `tests/` + `--authoring` gate it. The
  *analysis* oracles above are unaffected — they compare derivations both sides own, and still pass.
- `--suggest-monuments <regionDir> <xml_data.json> [--auto-style|--pedestal K --label K] [--margin M]`
  and `--suggest-monuments-corpus` validate `MonumentSuggester` (`PgmStudio.Minecraft`) — the
  authoring-flow "which monument style? + box" extractor. Given the world, the box the author drew, and
  the declared style (pedestal-below × label × cap-above), it suggests monument blocks: inverts the corpus-learned
  wall-sign facing→monument geometry, classifies sign *text* as a label (not keyword-match), requires
  the declared pedestal under an air cell, plus armour-stand/geometry fallbacks. Corpus (auto-style):
  **precision 96.6% / recall 57.8% / 35 FP** over 1721 monuments (recall capped by ~⅓ unlabelled +
  sign-post-only maps). Contract (modes/usage): `docs/contracts/monument-suggestion.md`; pattern study:
  `docs/monument-patterns.md` (scripts in `scripts/`). `layer_segment.parquet`
  can't drive it (no block materials/signs/entities) — see that doc's reuse note.
- `--monument-slices <regionDir> <xml_data.json> <outParquet>` runs `MonumentSliceExtractor`
  (`PgmStudio.Minecraft`): for every wool monument it samples a fixed **width-3 × depth-3 × height-5**
  block volume centred on the monument `<block>` (1 each horizontal, 2 above/below) → one parquet row
  per cell, tagged with `monument_id`/`wool_color`/`team`. Captures block id+data+name, decoded sign
  text, full tile-entity NBT, and entities (armour stands etc.) — these are attached by vertical reach
  (a wool-indicator stand standing below still has its head in the slice). Monument cell is air by
  PGM's placement-region convention. Validated on thunder (signs), pigland (glass pedestal + wool-on-
  head armour stand, the `Ruediger_LP` pattern) and dragons_hearth (armour stand above the monument).
- **Do NOT use `app.MapStaticAssets()`** in this hosted-WASM setup — it breaks the framework boot
  (`_framework/*` → 500, app stuck on "Loading"). A path-rewrite middleware maps fingerprinted
  `/js/...<hash>.js` → real names; load JS modules via a native `import()` from the classic `studio.js`.
- Blazor interop: `InvokeVoidAsync(name, params object?[])` **spreads** an array arg — box it
  (`(object)ids.ToArray()`) to pass a whole array. Razor markup lambdas can't contain `"` literals
  — use `string.Empty` / method-group handlers. `.control-input--hidden` checkboxes are `display:none`
  — click the wrapping label.
- **Symmetry / orbit math = ONE canonical C# leaf + the JS preview twin — do NOT add a third C# copy.**
  The canonical is **`PgmStudio.Geom.Symmetry`** (`Apply` concrete-axis · `Point`/`Rect` orbit ·
  `ReflectPoint`/`RotatePoint`/`Order`). Every C# site routes through it: `Pgm/SymmetryAuthoring` +
  `SymmetryExpander` (the `map.xml` source of truth), `SketchRasterizer`, `Analysis/SymmetryDetector`,
  and client `OrbitAssignment`. Live **canvas previews** are **JS** (`js/studio/geometry/symmetry.js`
  `applySymmetry`/`applySymmetryToBounds` + `orbitAxes`; editor `setAuthorMirror` + the sketch mirror
  layer) — "the hot path stays in JS", the documented twin of `Geom.Symmetry`. When a Configure phase
  needs a non-editable orbit *preview*, render it on the canvas via `setAuthorMirror`, **not** by computing
  orbit rects in Blazor C#. (Spawn/Protection still compute orbit in C# via `OrbitAssignment` because they
  *store* it with island/point-aware team assignment — see `docs/contracts/new-map-authoring.md` §4 / the
  orbit memory.)
- Don't make the format fit: reject malformed maps (e.g. kytriak_te) rather than weakening the schema.
- **Supported map range (enforced in `MapParser.EnsureSupported`).** Three gates, all
  `UnsupportedMapException`; `--scan-out-all` skips-and-logs them. **(1) proto >= 1.4.0** — the studio targets
  PGM's id-based regions/filters/kits, so the older positional format (e.g. kytriak_te, proto 1.3.0, anonymous
  teams) is rejected. **(2) no modern worlds** — `min-server-version >= 1.13.0` (e.g. allure, 1.21.10) ships a
  post-"flattening" palette world the Anvil reader can't decode yet. **(3) no unread objective module** — a
  module we don't parse would drop the map's goal silently on round-trip, so its presence rejects the map.
  The objective set is PGM's own: the modules contributing a **non-auxiliary `Gamemode` MapTag** — `wools`
  (CTW), `destroyables` (DTM), `cores` (DTC), `control-points`/`king`/`payloads`, `flags` (CTF), `score`
  (TDM). Auxiliary modules (`blitz`, `ffa`, `rage`) modify play, not the goal, and are not objectives. Add a
  tag to `ParsedObjectiveModules` when its parser lands. Corpus (350 slugs): kytriak_te (proto), allure
  (modern) and 3084 / lost_haven (`control-points`) are excluded; the rest parse.
- **Wool-location flooring asymmetry is intentional (PGM-grounded).** The intent generator floors the
  wool `<location>` but passes the monument block coords through raw — *because PGM treats them
  differently*. `<wool location="x,y,z">` parses via `XMLUtils.parseVector` → a raw `Vector` kept for
  proximity distance and **never block-snapped**, so the generator floors it to keep the wool's goal
  reference block-aligned. The monument is a `<block>` region whose `BlockRegion(Vector)` ctor floors
  itself (`new Vector(getBlockX(), getBlockY(), getBlockZ())`), so flooring in the generator would be
  redundant. Verified against `/media/sf_repos/PGM` (`wool/WoolModule`, `regions/BlockRegion`); generated
  XML exports valid. See `docs/contracts/new-map-authoring.md` §4.
