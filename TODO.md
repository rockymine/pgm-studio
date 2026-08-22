# pgm-studio — TODO (current focus)

The **Now & Next** board — only the *current focus theme* lives here. Everything not in the immediate
slice is in **`BACKLOG.md`** (the long tail); shipped capabilities are in **`FEATURES.md`** (the Done
column). The three move left → right: **`BACKLOG.md` → `TODO.md` → `FEATURES.md`**.

**Holds only open work:** `[ ]` to-do, `[~]` in progress — **never `[x]`.** When a task ships, a commit
lands (its message references the id), the task **leaves this file**, and a line is added to `FEATURES.md`.
Board rules live in `CLAUDE.md` (§ "Status & task board").

## The studio's eyes: what it can be asked to show, and what it says about what it drew

Five entries out of one afternoon of driving the pipeline, and one concept under them: the studio can be
asked for almost anything and describes every answer — **except what a board looks like once it is built**.
Four have landed. What remains is the one they were all edges of: it cannot show a world at all outside a
CLI, so the last thing an agent does — look at what it made — is the one thing the API has no answer for.

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
