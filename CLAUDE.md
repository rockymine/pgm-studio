# CLAUDE.md — pgm-studio

## What this is
A from-scratch **ASP.NET Core** rewrite of the Python `pgm-map-studio`
(`/media/sf_repos/pgm-map-studio`), which stays in place as the **behavioural reference**
(routes, the data contract, analysis oracles, and the 350-map corpus). Goal: full feature
parity in C# with all map data in **MariaDB**.

Approved plan: `/root/.claude/plans/imperative-whistling-key.md`.
Contract specs (copied from the reference) live in `docs/` — `contracts/region-authoring.md`,
`contracts/region-categorization.md`, `contracts/filter-region-wiring.md`, `filter-use-cases.md` —
the design for the `R1` / `F`-series tasks in `TODO.md`.

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

## Git
Commit **only when the user explicitly asks**; branch first; end commit messages with
`Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`.

## Status & current focus
Task board (the single source of truth for what's done / in progress / to-do) is **`TODO.md`** —
checkbox states + category IDs. Don't duplicate the task list here.

**Done:** M0–M5 complete (scaffold, schema/DAL, codec round-trip 350/350, importer, read API,
analysis — all parity-verified). M6 write API complete and the **editor UI is an exact port of the
reference studio frontend** — all six activity shells (Overview, Regions, Teams, Objective, Build
Regions, Configure) are ported and Chrome-verified, on a hybrid canvas (reused `EditorCanvas` JS via
interop) with reusable `RegionTree`/`RegionInspector` components. M7 world-import pipeline (Anvil
reader → extractors → scan-world → islands/artifacts) is parity-verified. `/design` page is ported.

**Now:** the remaining editor work is **cross-cutting infrastructure**, not whole activities — see
`TODO.md` C5 (draw-tool interop → region creation), C6/B4 (blocks overlay), C7/B5 (side-view canvas),
B7 (symmetry detection), plus M7 colours (P5) and M8 sketch (S2).

## Verification & gotchas (load-bearing, easy to lose)
- Run the app with **`./tools/dev.sh restart`** (`:7894`); after a host reboot MariaDB auto-starts
  but the dotnet bg process doesn't, and the claude-in-chrome MCP needs reconnecting (extension panel).
- Parity harnesses in `tools/PgmStudio.RoundTrip` (`--parity`/`--categorize`/`--buildability`/
  `--traversability`/`--wool`/`--extract`/`--islands`/`--authoring`); regenerate Python oracles into
  `/tmp/pyfresh` (wiped on reboot) via `parser.parse + serializer.to_dict` over the corpus.
- **Do NOT use `app.MapStaticAssets()`** in this hosted-WASM setup — it breaks the framework boot
  (`_framework/*` → 500, app stuck on "Loading"). A path-rewrite middleware maps fingerprinted
  `/js/...<hash>.js` → real names; load JS modules via a native `import()` from the classic `studio.js`.
- Blazor interop: `InvokeVoidAsync(name, params object?[])` **spreads** an array arg — box it
  (`(object)ids.ToArray()`) to pass a whole array. Razor markup lambdas can't contain `"` literals
  — use `string.Empty` / method-group handlers. `.control-input--hidden` checkboxes are `display:none`
  — click the wrapping label.
- Don't make the format fit: reject malformed maps (e.g. kytriak_te) rather than weakening the schema.
