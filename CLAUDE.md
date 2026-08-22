# CLAUDE.md — pgm-studio

## What this is
An **ASP.NET Core** studio for authoring PGM maps — planning a layout, drawing its ground, stating what the
map is played for, and writing the `map.xml` and world a server can load. All map data lives in **MariaDB**.

**Three outside repositories settle arguments, and none is optional.** **PGM** (`PGMDev/PGM`) is the
reference for the contract: what an element means is what that server does with it, so a claim about the
format is settled by reading it. The two **map corpora** — `OvercastCommunity/CommunityMaps` and
`OvercastCommunity/PublicMaps`, about 350 maps between them — are the reference for practice: what authors
actually build, which is the only answer to "is this normal". Neither kind says what is *correct* for a map as
it is **played**; that is the human oracle's, below. All three are public and expected as clones — where they
sit on this machine is under *Running it*.

Start at `docs/tools/flow.md`: the four levels a map is described at, which tool works at which, and how a map
moves between them. One document per tool sits beside it.

## Stack (decided, do not relitigate)
ASP.NET Core · FastEndpoints (`/api`) · Blazor WebAssembly hosted by the backend ·
MariaDB · FluentMigrator · linq2db (MySqlConnector) · TUnit · Parquet.Net.
Target framework **net10.0** (SDK 10.0.109; pinned in `global.json`).

**No backward compatibility.** One deployment, no published API, no third party reading anything this
writes: when a shape changes, **change the callers**. Never keep a legacy path, a compatibility shim or a
second accepted format "just in case" — there is no case, and the second path is what rots. The exception is
the `map.xml` contract itself, which PGM reads and which is therefore not ours to change.

## Where code goes
The rule: **a unit of code lives in the lowest (most-depended-upon) project that (a) already has the
dependencies it needs and (b) every consumer can already reach** — push it down for reuse, never up. Then a
separation by *kind*: **`Domain`** is what things are (entities, and the pure rules over them);
**`Contracts`** is how they cross the API; **`Pgm`** holds the `map.xml` codec *and* the layout generator;
**`Analysis`** the NTS-backed derivations; **`Minecraft`** the world; **`Data`/`Import`** persistence and
ingest; **`Client`** the Blazor UI; **`Api`** the composition root. The full map, with sizes and what has
outgrown its shape, is `docs/project-structure.md`.

**Two boundaries are load-bearing, and everything else follows from them.** `Client` is WASM and references
only `Contracts` + `Geom`, so the wire DTOs must live where the rest cannot be dragged in. And `Analysis`
must not see `Contracts` — DTOs depending on analysis depending on DTOs is a cycle of intent — so anything
`Analysis` *and* the client both need is forced into a true leaf. There are **two leaves**, split by what they
hold, and both reference **nothing**, not even `Domain`.

**`PgmStudio.Geom`** is pure algorithms (geometry, shapes, relief, generative layout) **and the shapes they are
measured in** — `CellRect`, `Rect`, `BlockBox`, `Vec3`. `Domain` references *it*, because a shape both halves of
the studio need has to sit where the client can reach it and `Domain` is not that place. Do not put an algorithm
in `Contracts` — `Analysis` cannot reach it, which is what forced the old duplicated reflect/rotate, and the
same boundary grew a second `BlockBox` and seven copies of `Rect` before `B210`. (It is `Geom`, not `Geometry`,
because `Analysis` uses NetTopologySuite's `Geometry` everywhere and a sibling namespace would shadow it.)

**`PgmStudio.Vocabulary`** is the finding shape — `Finding`, `Findings`, `Severity` — and the **closed sets of
words** three parties have to spell identically: a `map.stage`, a `style.kind`, a theme bucket, a room part, a
roof form. A gate below `Api` raises a finding, the HTTP surface answers it and the client renders it, so the
record goes in the one leaf all three reach and is *serialized* rather than mirrored; the same reasoning puts
the words there, since `Minecraft` writes a `style.kind` and cannot see `Contracts`. `Contracts` references it
and keeps the DTOs. A word set with only one consumer is not vocabulary — it is that consumer's constant.

**Client folders, by role rather than by page.** `Pages/` holds standalone **routable** pages only;
`Features/<Tool>/` is one bundle per tool — its routable `*Tool` host plus that tool's private phases, steps
and inspectors, in a folder-matched namespace so host and bodies resolve each other with no `@using`; and
`Components/` is the shared, tool-agnostic library, deliberately a **flat** namespace regardless of subfolder.
A body used by one tool lives with it; a body used by two moves to `Components/`. Name at the right altitude:
`*Phase` for a whole phase, `*Step` for one step of a multi-step phase.

## One concept, one shape — and the tell is prose
**A concept gets one type, one verb and one wire shape across the whole repo, not one per feature.** The
failure is never a decision to duplicate; it is a second feature quietly growing its own copy because reaching
for the first one was one grep further away. Seven finding records, six refusal envelopes and seven names for
the verb that produces one — `Faults`, `Check`, `Refusals`, `Validate`, `Findings`, `Errors`, `Completeness` —
all arrived that way, over three return types, and nothing failed while they did.

**The tell is a docstring comparing two types.** `Violation`'s said it carried "the same subject-id shape a
`PlanFinding` carries, so the editor highlights them identically" — the duplication written down, in the
codebase, and left there. When prose has to explain that this type is like that other type, that sentence *is*
the finding. Grep for it before adding a type whose name ends in a noun another type already ends in
(`*Finding`, `*Result`, `*Dto`, `*Rules`, `*Scope`), and read what is already there.

**Share the smallest thing that is genuinely common — usually a value or an answer, rarely a base class, almost
never an interface.** The gates take different inputs (`Check(plan)` against `Check(goals, isLand, keepOuts)`),
so an interface over them would have forced the context-carrying ones to lie about what they read. What was
actually common was what they *returned*, so that is what became a type. Ask what every case genuinely shares
before asking what shape would let them share it; inheritance imposed on cases that differ is a second
duplication wearing a hierarchy.

**One type, one responsibility — and a shared field is not a shared responsibility.** Three of a wing's six
statements (`Form`, `Pitch`, `RoofSlab`) are the same three a `HouseStyle` names, which reads at a glance like
a wing being a small house. It is not: a wing has no walls, windows, doors, porch or beams, two of its six are
things only a wing can say, and a third is a *count into* the style's storey list while the style's field of
that name is the list itself. So the answer was not inheritance — a wing is not a kind of house — but
composition of the smallest common thing, and one **resolve** where three had been asked separately. Look for
the responsibility each type actually has before reaching for a hierarchy over the fields they share; sharing
a field name is the weakest evidence there is, and where two things share a name and not a meaning
(`Storeys` over both a count and a list of styles), the fix is the name.

**Two things travel together and both must be shared.** A shared type with seven call verbs is half a fix, and
the half left undone is where the bug lives: while `Finding` was shared but the verb was not, every caller
re-derived "was anything refused" from a `Count`, which is right for a gate reporting only refusals and
silently wrong for one reporting complaints too. **A shape is not consolidated until the callers ask it the
same question by the same name.**

Then the rules already stated apply: the shared unit goes in the **lowest project every consumer can reach**,
and **every caller changes in the same commit** — there is no compatibility path, and a second accepted shape
is what rots.

## Where docs go
**Every folder under `docs/` is named for a subject, never for a form.** A new document joins the folder whose
subject it is about; there is no bucket for "contracts" or "designs", because that names the shape of a
document rather than what a reader is looking for.

| Folder | Holds |
|---|---|
| `tools/` | one document per studio tool, end to end — `flow.md` first, then `plan`, `sketch`, `configure`, `edit`, `generator`, `shapes`, `library`. Written from the code and usable as agent input, which is why each carries its endpoints. Beside them: `capabilities.md`, what the system can be *asked* for at each stage, and `mapgen-review.md`, the `MG` fault pool behind it. |
| `generator/` | the layout-generation track — eight files, no others (below). |
| `world-export/` | what the export **writes** into a world: `relief`, `terrain-painting`, `structures`, `decoration`, `tree-corpus`, `sketch-world-export`, `ideas`. |
| `world-scan/` | what the studio **reads** out of a world it did not build: monument and objective suggestion, terrain ground truth, the block palette, the corpus studies behind them. |
| `pgm/` | the map contract — destroyables/cores, water lanes, regions and filters, include resolution, what the studio refuses to read, the intent model, `template.xml`. |
| `client/` | the browser half: the canvas JS layer, the component vocabulary, routing and how the client is served. |
| `gameplay/` | what a map is played for — `approaches.md` (what the ground around an objective does, and the one document whose claims are the author's), the match-flow account, the traffic ground truth. |
| root | whole-repo notes: `project-structure` (the package map — where a piece of code belongs), `architecture` (the boundary — how a request becomes work, and what the studio can say about itself), `design-decisions`, `refusals` (how every gate says no, and the rule-id catalogue), `cloud-setup`, `doc-status`. |

A document is deleted when a subject folder's document owns its subject; corpus **measurements** are kept even
when the design around them has landed, because nothing else can re-derive them.

`docs/generator/` is the one folder whose files each carry their own discipline, so they are worth naming:

| File | Is | Discipline |
|---|---|---|
| `model.md` | the canonical model — glossary, pipeline, how a layout is constructed | governs on any disagreement |
| `vocabulary.md` | the living type catalog, one row per generation type | a type added, renamed or retired changes its row **in the same commit** |
| `rules.md` | the rule law — every CT/SP/WL/LN/HB/FR/MD/BZ/EL id | amended only by its own correction protocol |
| `evaluator.md` | the deriver-measurable and evaluator-term catalogue | — |
| `audit.md` | where the code and `model.md` measurably disagree | an entry leaves when its fix lands |
| `seed-stats.md` · `seed-envelopes.md` | measured corpus data | envelopes is generated — never hand-edited |
| `ideas.md` | the G-track idea pool | ids preserved; pull one onto the board when it becomes the focus |

**`model.md` has no live twin, and does not want one.** A script rendering the model to a checked-in HTML page
was a reasonable answer while the studio could not draw one; the studio draws them now — `GET /shapes/catalog`
returns every card with its SVG, `/shapes/probe` renders a single emission, and the compose page shows as many
boards as anyone cares to ask for. A second renderer beside them is a copy free to disagree with the app, and
the app is the thing being described. Check a figure against the running studio.

**Do not describe unbuilt machinery in the present tense.** A doc that says a type exists when it does not is
worse than a gap; the whole `WoolApproachShape`/`FamilyDock`/`ComposeTargets` class of drift came from exactly
that. Mark unbuilt things with their task id, or leave them out.

## How a document is written
`model.md` is a paper, not a reference card — and the rest follow it. **Prose carries the claim; a table
supports one already made.** A table gives structure but never says how things connect, so it may not be the
first statement of an idea. Take one topic, explain it to its end, then take the next — in **pipeline order**,
not in the order the decisions happened to be made. State mechanism as fact: no second person, no "we", no
changelog ("this used to be…"). Where something is genuinely a catalog — a code map, a rule-id table — a table
is right and prose would be padding.

**A document that describes a surface describes it end to end, in one shape.** The eight in `docs/tools/` are
the worked examples, and a new one follows them: **what it is** (what the tool does and what it is for) ·
**what it writes** (the artifact, named) · **the document model, field by field** · **what it compiles to** ·
**the phases and their steps**, in the order the work is done · **what it refuses**, and with which status ·
**the API as an endpoint table** carrying the failure codes · **driving it without the UI** · **limits**. Two
of those are conditional — a tool with no gate needs no refusals section, one with no document of its own
needs no model section — and the rest are the spine.

Three properties make such a document usable rather than merely accurate. It is **written from the code**, not
from an older document: read what the thing does now, and if prose and code disagree, the code is what
happened. It is **written for an agent as much as a person**, which is why the endpoints are in it — a
description that cannot be acted on is a description of nothing. And **every JSON shape gets a worked example
that runs**: extracted from the document and posted to the live API, because an example that does not run is
the fastest thing in any document to go quietly wrong.

## Documents rot silently — the rule that stops it
**A change to a feature is not finished until the document that describes it says so.** Code that moves and
prose that does not is worse than no prose: nothing fails, nothing is flagged, and the next reader — a person
or an agent — acts on a description of a system that no longer exists. This has already cost this repo two
rewrites, and both times the document *sounded* current.

So the rule has teeth, in three parts.

**Touch a feature, touch its document, in the same commit.** Not "later", not "a follow-up doc task": the
commit that changes what a tool does also changes what its document says a tool does. If that feels like too
much for one commit, the commit is too big, not the rule.

**Retiring is the dangerous half.** Adding is usually noticed, because someone wants the new thing written
down. Deleting a field, an endpoint, a phase or a whole tool leaves prose behind that reads perfectly and
describes nothing — the `--parity` harness, the `/maps/new-sketch` page and the plan tool's Theme rail each
outlived their code in the docs by months. When something is removed, **grep the docs for its name before the
commit lands**, and delete or reword every hit.

**A task id in a document is a promise to come back.** The `Limits` section of a tool document names open
tasks — "the canvas half is not built (`B107`)", "composing several rectangles into one footprint is `G172`'s
open half" — and those sentences become false the moment the task ships, in the most misleading direction:
the document keeps claiming a limitation the tool no longer has. So **when a task moves to `FEATURES.md`,
grep its id across `docs/` and fix every hit in the same commit.** Citing an id for *provenance* ("this
shipped as B90") is stable and fine; citing one as a *gap* is a debt with a due date.

The corresponding rule for the generator track is stricter still and already stated above: a type added,
renamed or retired changes its `vocabulary.md` row in the same commit.

## Running it
- **In a cloud container (Claude Code on the web), read `docs/cloud-setup.md` first** — sandboxed foreground
  shells, the SDK by apt only, MariaDB without systemd. The rest of this section is the local VM.
- **The outside repositories** sit beside the solution: `/media/sf_repos/PGM` (the server),
  `/media/sf_repos/CommunityMaps` and `/media/sf_repos/PublicMaps` (the corpora, each nesting `ctw/`, `dtcm/`
  and `mixed/` — a sweep that does not recurse silently reads a third of the maps). Scanned world output for
  the corpus is `/media/sf_repos/pgm-studio-output`.
- **.NET 10** via apt; **MariaDB 10.11** under systemd. DB `pgm_studio`, user `pgm`/`pgm_dev_pw` on localhost.
- **`./tools/dev.sh restart`** (`:7894`) — builds once and runs the binary, because `dotnet run` cold-start on
  the VirtualBox shared folder is slow and the first WASM load takes seconds. After a host reboot MariaDB
  comes back but the dotnet process does not, and the claude-in-chrome MCP needs reconnecting.
- **`/api-docs`** is the whole API surface in the browser — every route, its parameters and its response
  schema, expandable and sendable without a client — over the document at `/api/openapi/v1.json`, which is
  generated from the routes and the DTOs. A route that declares no request type appears there with no body
  schema, which is the honest reading of `RP12`.
- **`dotnet test` is not the path** on the .NET 10 SDK (the VSTest bridge is gone) — run a project directly:
  `dotnet run --project tests/<Project>`.
- **`./tools/e2e.sh all`** is the browser gate (icons · paint · plan refusals · smoke), on its own port and
  database so it cannot touch dev data. It needs Playwright **globally** (`npm i -g playwright && npx
  playwright install chromium`). **Stop `dev.sh` first** — two servers on one VM starve each other and the
  failures land as 30s route timeouts that look like page faults.
- **`tools/PgmStudio.RoundTrip --goldens [featureRoot] [--update]`** is the corpus regression net: the four
  map-level derivations over every corpus map, compared against `corpus-goldens.json`, so a change that moves
  a verdict says which maps and what moved. Re-record when the change is deliberate — it buys the look, not a
  veto. Feature root here: `/media/sf_repos/pgm-studio-output`.
- **`dotnet run tools/deriver/figure-check.cs`** gates `model.md`'s ASCII figures by pushing each one, parsed
  out of the doc itself, through the classifier that names that kind of thing. Run it after editing a figure
  or a classifier — a shape that reads as the wrong family cannot be spotted by eye.
- **`./tools/census.sh`** writes `docs/project-structure.md`'s size table from the tree, and `--check` fails
  when it is stale. Run it after anything that moves files between projects or folders: a hand-written census
  is wrong between every pair of commits, which is how every row of the last one came to have drifted.
- **`./tools/build-scripts.sh`** builds the file-based tool scripts, which are **not** in `PgmStudio.slnx` and
  which `dotnet build` therefore never touches. Run it after renaming or moving anything in `src/` that a
  script names: without it a rename leaves a script uncompilable while the solution stays green, which is how
  35 of the then-51 came to be broken at once (`B227`).

## Investigation stays local — only the result is committed
**A script written to answer a question is not an artifact; the answer is.** Measuring a corpus, probing a
world, rendering a board to see what it looks like, sweeping seeds to find where something breaks — all of
that is work, and none of it is product. The finding goes in `docs/` beside the figure it produced, or it goes
**straight into the code** as a rule, a constant or a test. The script that took the reading goes in the
scratchpad and is not committed.

This rule was learned expensively. `tools/` had accumulated **51** one-off scripts, of which 44 had each been
run once, years of investigation frozen at whatever `src/` looked like that afternoon: 35 of them no longer
compiled and nobody knew, because nothing built them (`B227`, `B228`). Restoring them cost a day and bought
nothing, since every finding they held was already written down.

So a script earns a place in `tools/` only by being **re-run**, which is one of three things:

- a **gate** that fails — `reproduction-gate`, `figure-check`, the fingerprints;
- a **generator of a committed artifact** — `envelope-stats` writes `seed-envelopes.md`, `fingerprints` writes
  `composer-fingerprints.json`, `census.sh` writes `project-structure.md`'s size table;
- an **operational tool** the product needs — `seed-library` seeds the database, `library-map` writes the
  catalogue map's layout and intent for `POST /map/from-documents` to load.

Nothing else. **"It might be useful again" is not one of them** — it is the sentence that produced all 44, and
a fresh throwaway against today's `src/` beats a restored one against 2026's every time. Data is judged the
other way round: a hand-built world, a hand-labelled fixture, a traced plan is a *result* and is committed,
because nothing can re-derive it.

## Tests
TUnit, one test class per source unit, mirroring `src/`. Synthetic fixtures only; corpus and round-trip
harnesses live under `tools/`, not `tests/` — and only where they meet the bar above.

- **Test the invariant, not the contents** — assert what must hold (fullness, a constant fall, an ordering),
  and prove the test fails on the old behaviour before trusting it.
- **Never build during a test sweep.** The DB suites share one schema and the DLLs swap mid-run, so a
  contended sweep reports failures that are not real.

## Naming
- **No single-letter identifiers outside a lambda.** `laneWidthCells`, not `w`; `demand`, not `d`; `seat`, not
  `s`. Longer is fine even when it costs a line wrap. Inside a lambda a short binder is expected
  (`.Where(b => b.Kind == …)`), and the axis conventions stay: `x`/`z` for plan coordinates, `u`/`v` for the
  symmetry frame, `i`/`j`/`k` for loop indices. Those are notation, not laziness.
- **No abbreviations of a type's own name.** `abutment`, not `iface`. C# has no keyword clash to dodge.
- **A name must not promise the wrong category.** `BoxJoint.Interface` read like it held a C# interface; it
  holds a shared edge interval, so it is `BoxAbutment`/`.Abutment` — the word the docstrings already used
  whenever they had to explain it. Same reason `BoxJoint.Grant` is not `Offer`: a host publishes a capacity, a
  joint records a selection, and one name for both hid that they are different quantities.

## Code comments
Comments stay **purely functional** — what the code does and why, in the present tense, about the code as it
stands.

**A comment never carries history. Ever.** Not what the code used to do, not how many sites there used to be,
not what was wrong before, not what a refactor replaced, not "this used to be…", "each had grown its own…",
"until now…", "the old…", "N of them did X". History lives in **git** — in the commit message, in
`FEATURES.md`, in the task board — and a docstring is not any of those. A comment recounting the state a
change removed is worse than no comment: it describes a system nobody reading the file can see, it goes stale
the moment the next change lands, and it buries the sentence a reader actually came for under an account of a
problem that no longer exists. The reason a thing is shaped the way it is can always be stated as a **fact
about the shape** — *the ids live in the lowest project every caller reaches, because a second `const`
aliasing one that exists is two rules* — with no before-and-after in it.

**A comment is shorter than the code it describes, or it is in the wrong file.** A docstring says what a
reader needs at the call site: what this does, what it answers, what it refuses. The *argument* for a design
— why this shape and not that one, what the corpus said, which alternative was weighed — is a document's job,
and `docs/` is named for subjects so that there is somewhere to put it. Comments are **27% of `src/`**
(19,306 lines), and the offenders are the ones where the case for a decision was written above the code that
implements it. Two things are deliberately not this: a `*Rules` constant's `<summary>` and `<remarks>` **are**
the `/api/rules` payload, so their length is the answer's length; and a `Traps` note earning its place by
having cost hours. Everything else: state it, then stop.

Also **never** an attribution to what a piece of code was ported from, and **never** an implementation-phase
or task id (`NS`, `N00`, `B8`, `P5`, `ND2`, …) — 62 comment lines across 44 files still carry one. The port
attributions are swept; the task-id half is still open on the board, and so is the history half (`RP10`).

The same rule already governs prose under `docs/` — *How a document is written*: state mechanism as fact, no
changelog. It is one rule, and the two halves are not allowed to disagree.

## Reporting findings
**A world finding needs coordinates.** Report what a scan or an analysis found as a per-item table with the
positions in it, so it can be checked in-game — a prose summary of a geometric claim cannot be verified by the
person who has to trust it. Prefer a local geometric predicate to a proxy measure.

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
- **`TODO.md`** — the **current focus only** (`[ ]` to-do, `[~]` in progress). The *Now & Next* board — kept
  small.
- **`FEATURES.md`** — the catalog of **shipped** capabilities, by area, with the task id(s) that delivered
  each (for git traceability). The *Done* column.

Tasks flow left → right: **`BACKLOG.md` → `TODO.md` → `FEATURES.md`**.

**The board is emptied concept by concept, not task by task.** A group's entries are gathered because they
spend the same foundation, so the work begins by reading that foundation for duplication — two types that are
one concept, one verb under seven names, a fact derived a second time beside where it is stored — and fixing
*that* is what makes the entries small enough to do. `Distance, and the walk every measure is taken with` is
the worked example: the walk, the sets it runs over and the rules stated in it were one cause, and naming the
cause is what turned an unreadable pile into a group whose entries each fit in a paragraph. A group is
emptied when its ground is settled, not when its entries have each been done in turn.

**Task-board rules** (the board kept exploding; these keep it honest):
1. **A task lives in exactly ONE file.** Never duplicate it across `BACKLOG`/`TODO`/`FEATURES` (two stale
   copies is the failure mode). Neither `BACKLOG.md` nor `TODO.md` ever holds `[x]`. Tasks that are retired
   for the moment can live inside `docs/<project>/ideas.md`.
2. **Done = a line in `FEATURES.md`, and no doc still calling it open.** When a task ships a commit lands
   (its message references the id), the task **leaves `TODO.md`**, one line is added to `FEATURES.md` if it's
   a shipped capability, and **every document naming that id as a gap is fixed in the same commit** — grep it
   across `docs/` before calling the task done (see *Documents rot silently*).
3. **`TODO.md` is the current focus, kept small.** Only the active group's now/next tasks (soft cap ~6–12).
   Pull the next **group** up from `BACKLOG.md` when it drains — a whole concept, not a task at a time; if
   `TODO.md` bloats, push items back down. New tasks land in `BACKLOG.md` (or `TODO.md` if they ARE the
   focus). **The cap is over the board's readability, not its length**, so a single programme whose order is
   the point may exceed it on the author's call — the board then says so at the top, states the trade, and
   takes nothing new until a phase drains. Anything found while working still lands in `BACKLOG.md`.
4. **Ids are a prefix + number, GLOBALLY unique + stable across all three files.** Moving a task between
   files **never** changes its id; never renumber or reuse — commits and memory cite ids, so
   `grep <id> TODO.md BACKLOG.md docs/generator/ideas.md` must hit exactly once. The prefix names the
   **document the task is obliged to leave correct** (catalogued below). It is not a section name and does not
   move when the board is regrouped, which is the whole reason it can be stable.

   **A rule id has the same shape, so the boards are not the whole namespace.** `GET /api/rules` answers
   letters-plus-number too, and an id that is a task on the board and a rule on the wire makes every grep for
   either ambiguous. So the uniqueness check is `grep` **plus** `GET /api/rules?rule=<id>`, which has to answer
   empty. A new prefix is two letters or more and is not a rule family; **`G` is the one that is both**, and
   what keeps it apart is the number — the family is `G1`–`G8` and the board numbers above it.
5. **Both boards group by concept, and a heading names one** — "The house: what it stamps, where it stands",
   "Painting: the theme a document states is not what lands". A group gathers whatever entries share a
   foundation, whatever their prefixes, and orders them the way a reader meets them rather than the way the
   ids run. **Never head a group with a letter**: the prefix lives in the id and nowhere else. Keep groups
   few, and let one that has drained to a single entry fold into its neighbour.
6. **No trailing "Next:/Remaining:/Deferred:" notes inside a task.** Future work is its **own** `[ ]` task in
   the right group (in `BACKLOG.md` if not immediate), not a footnote on a (near-)done one.
7. **`[~]` describes only what REMAINS.** When a task is partly landed, reword it down to the open slice; the
   landed part moves to `FEATURES.md`.
8. **File a task in the group whose foundation its REMAINING work touches.** Backend done and only the UI
   left ⇒ it moves to the group that owns the surface. Its id does not change with it.
9. **Deferred *decisions* are parked** in `BACKLOG.md`, clearly marked with the blocking question — not
   interleaved with actionable tasks.
10. **A task is a task, and it is short.** Aim for **≈150 words**; over **250** the entry is not a task yet
    and wants investigating until it is. What earns its place: **what to build**, **where it lands** (the
    type, the file, the line), **the number** where the author has stated one, and **one evidence line**
    carrying the coordinates a reader confirms at. What does not: which half already shipped, what an earlier
    trace got wrong, when a premise was corrected, what a re-check found on which date. An implementer does
    not need an entry's history to do the entry, and a reader scanning for work should not skip three
    paragraphs to learn whether there is any. **File it concrete or do not file it** — a finding written from
    a symptom, unmeasured, is what grows into five paragraphs nobody can act on.

**The prefix catalogue.** A prefix is the subject under `docs/` whose document the task must leave correct —
the taxonomy the documentation already has, so the id itself says what rots if the commit skips it. A task
that ships without touching its prefix's document is the failure *Documents rot silently* describes, and the
id is where that obligation is written down.

| prefix | the document it obliges |
|---|---|
| `TN` | `docs/tools/plan.md` — the plan tool. Its second letter, because `TP` is terrain-painting law |
| `TS` | `docs/tools/sketch.md` |
| `TC` | `docs/tools/configure.md` |
| `TE` | `docs/tools/edit.md` |
| `TG` | `docs/tools/generator.md` |
| `TH` | `docs/tools/shapes.md` |
| `TL` | `docs/tools/library.md` |
| `G` | `docs/generator/` — the layout-generation track, under its own stricter rules |
| `C` | `docs/client/` — the canvas JS layer, the component vocabulary, routing |
| `WE` | `docs/world-export/` — what the export writes into a world |
| `WS` | `docs/world-scan/` — what the studio reads out of a world it did not build |
| `PG` | `docs/pgm/` — the `map.xml` contract |
| `GP` | `docs/gameplay/` — what a map is played for; the author's oracle governs it |
| `RP` | the root documents, and work no subject document owns |

**A tool's UX belongs to the tool, not to the client.** `docs/client/` is the shared browser half — the
canvas layer, the component vocabulary, how the client is routed and served — so `C` is what every tool draws
*through*. A phase, a step or a refusal belongs to the tool that has it, and that tool's document has a
section for exactly those, so it takes the tool's prefix. Where a task genuinely rewrites both, it takes the
prefix of the document it rewrites more, and the same-commit rule still fixes the other.

**No longer issued, and never renamed on the entries that carry them**: `B` — backend/pipeline/internals, 70
of the 114 open entries when this was written, which is the reason a prefix now names a document rather than
a layer — and `N` (→ `TC`), `S` (→ `TS`), `CV` (→ `C`), `P`, `A`, `M`. An id is a handle: renaming one breaks
every commit that cites it, so old entries keep theirs and only new ids follow the table.

## Gameplay decisions have a human oracle — ask before filing one
A rule about the map *as it is played* is not derivable from this repository. The corpus shows what authors
did, the code shows what the tool does, and neither says what is correct — so a question about how a map plays
is answered by **asking the author**, before a task is filed, a doc is written or a fix is made. What counts:
what an objective needs around it, where a goal may sit, what a rule is for, whether a measured difference is
a defect or a convention.

The failure this prevents has already happened. A destroyable and a core **float a few blocks above the
terrain by design** — a core on the ground cannot leak, and a destroyable on the ground is trivially covered —
and that has been PGM's behaviour from the start. Measuring the gap and reasoning from first principles
produced a confident, filed, committed claim that every generated destroy map was unwinnable. The measurement
was right and the conclusion was invented. Neither the corpus nor the code would have corrected it; one
question would have.

## JS dependencies — vendor, never fetch
**No npm dependencies in the repo**: no `node_modules`, no lockfile, nothing whose install step runs code.
Node itself is load-bearing — `package.json` marks the hand-written `.js` as ESM and `npm test` runs Node's
built-in runner (`tools/js-test.sh`), zero dependencies, which is what lets the suite run from the shared
folder at all.

**Browser libraries are vendored**: one reviewed, pinned, self-contained file under
`wwwroot/js/studio/vendor/`, with its regenerate command in the header comment. **Build and test tools are
never installed into the repo** — fetch with `npm pack` (download only, no lifecycle scripts, which is where
npm's supply-chain risk lives) or expect them globally, as the e2e harness does with Playwright. **Never a
runtime CDN tag**: unpinned, fetched by every user's browser, unreviewable, no integrity check, and dead the
moment egress is restricted — `lucide@latest` was all five, and no icon rendered in the cloud container. If a
vendored subset can miss a name (icons are named dynamically from C#), the shim must **fail loudly** with a
console error, which the smoke sweep turns into a failed page; a silent blank is the bug being avoided.

## Traps (each one has cost hours)
- **`dotnet run <script>.cs` caches the built app and will NOT pick up `src/` changes.** The file-based tools
  (`tools/compose/*.cs`, the `#:project` scripts) build into
  `~/.local/share/dotnet/runfile/<script>-<hash>/`, keyed on the **script**, so an unchanged script re-runs its
  old binary against the old project output and reports pre-change numbers with no error. Before measuring a
  `src/` change: **`rm -rf ~/.local/share/dotnet/runfile/<script>-*`**. A brand-new script always builds fresh,
  which is why a scratch copy can disagree with the committed tool.
- **Symmetry / orbit math is ONE canonical C# leaf plus the JS preview twin — do not add a third copy.** The
  canonical is **`PgmStudio.Geom.Symmetry`**; every C# site routes through it (`Pgm/SymmetryAuthoring`,
  `SymmetryExpander`, `SketchRasterizer`, `Analysis/SymmetryDetector`, client `OrbitAssignment`). Live canvas
  previews are JS (`js/studio/geometry/symmetry.js`), the documented twin. A Configure phase needing a
  non-editable orbit *preview* renders it on the canvas via `setAuthorMirror`, not by computing orbit rects in
  Blazor. (Spawn/Protection compute orbit in C# because they *store* it — `docs/pgm/new-map-authoring.md` §4.)
- **Do not use `app.MapStaticAssets()`** in this hosted-WASM setup: it breaks the framework boot. What serves
  the client instead — and the three Blazor interop details that each cost an afternoon — are in
  `docs/client/routing-and-ia.md` and `docs/client/canvas-interaction.md` §6.
- **Nothing is checked against an outside oracle any more, and nothing should be re-added.** The contract
  check went first (the studio's `map.xml` deliberately exceeds the old reference's, so it reported a red it
  could never clear); the four analysis comparisons followed, because they pinned live derivations to a frozen
  copy and made safe refactors look risky. **PGM is the reference for the contract**, `tests/` gate it, and
  `--goldens` catches a derivation moving.
- **Don't make the format fit.** A malformed or out-of-range map is rejected rather than accommodated by
  weakening the schema — three gates in `MapParser.EnsureSupported`, written out in
  `docs/pgm/supported-maps.md`.
- **Coordinate flooring is per-field, not global.** The wool `<location>` is floored and the monument
  `<block>` is not, because PGM floors one itself and never the other. The rule and the PGM sources are in
  `docs/pgm/new-map-authoring.md` §4.
