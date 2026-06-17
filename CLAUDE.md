# CLAUDE.md — pgm-studio

## What this is
A from-scratch **ASP.NET Core** rewrite of the Python `pgm-map-studio`
(`/media/sf_repos/pgm-map-studio`), which stays in place as the **behavioural reference**
(routes, the data contract, analysis oracles, and the 350-map corpus). Goal: full feature
parity in C# with all map data in **MariaDB**.

Approved plan: `/root/.claude/plans/imperative-whistling-key.md`.
Contract specs (copied from the reference) live in `docs/` — `contracts/region-authoring.md`,
`contracts/region-categorization.md`, `contracts/filter-region-wiring.md`, `filter-use-cases.md` —
the design for the authoring (`N`) / `F`-series tasks in `TODO.md`. `contracts/new-map-authoring.md`
is the **declarative intent-model** direction for new maps (meaning→structure; generator = mirror of
the categorizer) — original to this repo, the current headline direction and the design for the `N`
tasks; it **supersedes** the split-view-model bits of `region-authoring.md` for new maps (§7).

## Stack (decided, do not relitigate)
ASP.NET Core · FastEndpoints (`/api`) · Blazor WebAssembly hosted by the backend ·
MariaDB · FluentMigrator · linq2db (MySqlConnector) · TUnit · Parquet.Net.
Target framework **net10.0** (SDK 10.0.109; pinned in `global.json`).

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

## Code comments
Comments stay **purely functional** — describe what the code does and why. **Never** reference the
Python reference app ("port of", "mirrors the reference", parity/oracle) or implementation-phase /
task ids (`NS`, `N00`, `B8`, `P5`, `ND2`, …). Existing non-conforming comments are swept separately
(see `TODO.md`).

## Git
Commit **only when the user explicitly asks**; branch first; end commit messages with
`Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`.

## Status & task board
Two files, two jobs — keep them current, don't duplicate either here:
- **`TODO.md`** — **open work only** (`[ ]` to-do, `[~]` in progress). The live board.
- **`FEATURES.md`** — the catalog of **shipped** capabilities (the "done" half), by area, with the
  task id(s) that delivered each (for git traceability).

**Task-board rules** (the board kept exploding; these keep it honest):
1. **`TODO.md` never holds `[x]`.** When a task is done a commit lands (its message references the id),
   the task **leaves `TODO.md`**, and — if it's a shipped capability — one line is added to `FEATURES.md`.
2. **No trailing "Next:/Remaining:/Deferred:" notes inside a task.** Future work is its **own** `[ ]`
   task in the right section, not a footnote on a (near-)done one.
3. **`[~]` describes only what REMAINS.** When a task is partly landed, reword it down to the open
   slice; the landed part moves to `FEATURES.md`.
4. **File a task where its REMAINING work lives.** Backend done + only UI left ⇒ it belongs in the
   feature/UI section, not the backend section.
5. **Sections are few and stable; ids never get renumbered.** Add the next number under an existing
   section — commits + memory reference ids — don't spin up a new category for one task. Sections:
   Authoring (`N`, the new Configure wizard at `/maps/{id}/configure` — mirrors the concept page's 00…07 + Validation steps),
   Existing-editor canvas/infra (`C`), Backend/pipeline/internals, Lower-priority/parked.
6. **Deferred *decisions* are parked**, clearly marked with the blocking question — not interleaved
   with actionable tasks.

**Where it stands:** M0–M5 + the M6 editor shells + the M7 pipeline are landed (`FEATURES.md`). The
forward direction is **new-map authoring** — the intent-model *backend* is done; the open work is the
authoring **UI**, the Configure wizard at `/maps/{id}/configure` (the concept mock now lives at
`/concepts`; routes/labels in `docs/contracts/routing-and-ia.md`; `docs/contracts/new-map-authoring.md`
is the contract). Then editor depth: the analysis-backed feature UIs (`F`) over their done services, and the
cross-cutting editor/canvas infra (`C`). See `TODO.md` "Current focus".

## Verification & gotchas (load-bearing, easy to lose)
- Run the app with **`./tools/dev.sh restart`** (`:7894`); after a host reboot MariaDB auto-starts
  but the dotnet bg process doesn't, and the claude-in-chrome MCP needs reconnecting (extension panel).
- Parity harnesses in `tools/PgmStudio.RoundTrip` (`--parity`/`--categorize`/`--buildability`/
  `--traversability`/`--wool`/`--extract`/`--islands`/`--authoring`); regenerate Python oracles into
  `/tmp/pyfresh` (wiped on reboot) via `parser.parse + serializer.to_dict` over the corpus.
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
- Don't make the format fit: reject malformed maps (e.g. kytriak_te) rather than weakening the schema.
- **Wool-location flooring asymmetry is intentional (PGM-grounded).** The intent generator floors the
  wool `<location>` but passes the monument block coords through raw — *because PGM treats them
  differently*. `<wool location="x,y,z">` parses via `XMLUtils.parseVector` → a raw `Vector` kept for
  proximity distance and **never block-snapped**, so the generator floors it to keep the wool's goal
  reference block-aligned. The monument is a `<block>` region whose `BlockRegion(Vector)` ctor floors
  itself (`new Vector(getBlockX(), getBlockY(), getBlockZ())`), so flooring in the generator would be
  redundant. Verified against `/media/sf_repos/PGM` (`wool/WoolModule`, `regions/BlockRegion`); generated
  XML exports valid. See `docs/contracts/new-map-authoring.md` §4.
