# The Generator tool

## What it is

The generator rolls whole boards. It is the one tool in the studio that authors nothing: a board here is not
drawn, it is **composed** — out of a player count, a team count, a symmetry mode and a seed, and out of
nothing else. Its route is `/generator`, and what it shows is a feed of candidates, each one a complete plan
document rendered as a picture of the fanned board.

The work it supports is sieving, not editing. Every knob on the page decides which boards are *shown*; none
of them changes a board. A candidate worth keeping is **pinned**, which stores it, and leaves the tool by
being **authored**, which originates a map at `stage=plan` and hands it to the Plan tool. From there the
map follows the ordinary lifecycle — plan, sketch, configure — and the generator is done with it.

Its companion page is the shape catalog at `/catalog`, which shows the vocabulary the composer fills boxes
with. That is `shapes.md`'s subject, and the two pages link to each other from the top bar.

What the composer *is* — the pipeline, the shape model, the rule kinds — is `docs/generator/model.md`, which
governs. This document is the tool: what a request is, what comes back, what the numbers on a card mean, and
how to drive the whole thing without a browser.

## What it writes

**Browsing writes nothing.** Every card in the feed is composed on demand from its seed and thrown away when
the page reloads; there is no cache, no draft and no row behind a board that has not been kept.

**Pinning writes a `plan` row** with origin `generated` (`PlanStore.SaveGeneratedAsync`), holding the
canonical plan document, the descriptor that reproduces it, the composer version that made it, and its
structural bucket key. Two things happen only at this point rather than in the feed: the partition is written
into the document as the authored `boxes` annotation, so a kept board opens in the editor already carrying the
grouping that produced it, and the row is **deduplicated by content hash** — pinning a board whose geometry is
already stored returns the existing row instead of a second copy.

**Authoring writes a `map` row** at `stage=plan` (`POST /api/plan/{planId}/author`), seeded with the
candidate's plan document and carrying a `plan_source_id` back to it. The candidate is left in the pool: the
map holds its own copy from that moment on, and editing the map cannot disturb the candidate it came from.

The hold tray is not a session — it *is* the generated half of the candidate pool. It lists every generated
row the database holds, so a board pinned weeks ago is still in it, and unpinning is a delete.

## The request

A compose takes five values and no geometry.

| Field | Default | Is |
|---|---|---|
| `players` | 12 | Players per team, clamped 6–47. The only size input, and its job is to name a **size band** — nano 6–13, micro 14–21, milli 22–31, centi 32 and up — which is what the land budget and every structural ladder read. Two counts in one band compose to the same budget, and a count above centi's range is clamped into it because centi is the top of the ladder. |
| `teams` | 2 | 2 or 4 at the plan tier. The browse endpoint composes two-team boards only and answers 400 `RQ1` on any other count, naming the field — a board that is not the one asked for is worse than no board. |
| `symmetry` | `rot_180` | `rot_180` or `mirror_z` through the feed. `mirror_x` and `rot_90` are legal `ComposeRequest` values but the endpoint answers 400. |
| `cell` | 4 | Blocks per proxy cell — the plan grid's scale. No control writes it; it is honoured as a query parameter. |
| `seed` | — | Any unsigned 64-bit integer. Drives every draw the composer makes. |

The feed walks the seed axis and holds the other four fixed, so a request is really the first four values plus
a cursor. `seedStart` is where the walk resumes and `count` how many *matching* boards to return (clamped
1–48; the page asks for 9).

**A seed reproduces its board exactly, within one composer version.** The generator behind it is a small
deterministic one chosen for that reason rather than the platform's, and no clock or identifier enters it.
Sampling *order* is part of the promise: draws come off in one fixed sequence, so inserting a draw anywhere
re-rolls every seed downstream of it. That is why any change to composition geometry bumps
`ComposerVersion.Current` — `marker-id-1` today — and why the version rides on every stored candidate.

A stored row whose version is not the current one is **stale**, and the tray badges it. Nothing about the
stored board has changed: it is loaded, never recomposed, and opens exactly as it was kept. What has lapsed is
its descriptor's claim to reproduce it, so re-composing that same request today yields a different board.

The descriptor is the card's identity and the whole of what a pin needs:

```json
{ "players": 12, "teams": 2, "symmetry": "rot_180", "cell": 4, "seed": 0,
  "composerVersion": "marker-id-1", "schema": 1 }
```

`schema` is the descriptor's own shape version, bumped if these fields change, so an old stored descriptor
still reads.

## What a compose produces

`Composer.ComposeStages` runs one direction and never reopens what an earlier step settled. The **envelope**
turns the player count into a land budget, a fanned board extent and the cell bounds one team unit may fill.
The **crossing** fixes the gap between the two fronts while the board is still empty, because the allocator
takes it as the axis margin everything else is laid out behind: one hop either side of the stone the band will
carry, or a flat 30 blocks front to front where it will carry none. It decides once whether this board wants a
split band, which is a crossing that carries none. **Allocation** places the hub,
chooses its form, works out what hangs off it, and seats each neighbour on the hub's real free surface,
producing typed boxes and the joints between them. Every unit carries a frontline on the hub's front edge, and
a spawn on a side edge is seated level with the hub's middle or behind it, never toward the front. **Filling** emits the hub first as the constraint source
and each neighbour to the width its own joint was granted. The finished unit is then **re-anchored on its
face**, so the band it will meet is the face itself rather than the hull of two offset copies. The **carve**
lays the mid band flush against the fronts. **Walling** then gives each wool approach its defence wall (below).
**Assembly** turns labelled pieces into a plan, dropping the labels, and the plan is put to the evaluator's
hard-term gate.

A rejected attempt is resampled whole; sixty are allowed before the compose throws, and a throw is skipped
rather than reported (below). The gate is eight hard terms — structural integrity, the `PC-C` corner-contact
and `G2` narrow-corridor lints, the `G5` void-hop band, the mid band's two-cell wool clearance (`BZ6`), the
20-block spawn-to-wool floor (`WL2`), and two floors on the crossing: the spawn at least 55 blocks by the walk
from the build band (`SP10`) and every wool at least 59 (`WL10`). It runs the **composer profile**, every term
on at flat weight, and short-circuits on the first that fires. The two crossing floors are the author's
judgement of composed boards and bind them alone: the default profile the editor lint runs leaves them off.

**Every hub hole is at least 12 blocks across.** A ring's hole, and the ring inside a P, double-hole or G, keeps
`WL12`'s floor for a plain hole, so the composer never draws a slit a player jumps. **Every wool approach gets
one defence wall** where a seam qualifies: across the route the attack takes into the approach, on a seam with
no land beyond either end so it is crossed rather than rounded (`PL17`), a lane mouth wide and 10–20 blocks in
front of the room (`ST8`). A straight lane is cut in two to make that seam, which is why a walled approach
carries a `-inner` piece, and a back-room lane is built at least four cells long so one fits. A two-legged
approach whose room lies deeper than the window takes the nearest qualifying seam its attack crosses.

What comes out is a plan document, and it is the same format the Plan tool edits. This is seed 2 at twelve
players under `rot_180`, exactly as `POST /api/compose/pin` stored it:

```json
{
  "plan": 2,
  "meta": { "name": "Composed p12 t2 #2" },
  "globals": { "cell": 4, "symmetry": "rot_180", "maxPlayers": 12, "surface": 9 },
  "pieces": [
    { "id": "hub-t1",          "role": "piece",     "rect": [-5, 15, 11, 3] },
    { "id": "hub-t2",          "role": "piece",     "rect": [-5, 9, 11, 3] },
    { "id": "hub-t3",          "role": "piece",     "rect": [-5, 12, 3, 3] },
    { "id": "hub-t4",          "role": "piece",     "rect": [3, 12, 3, 3] },
    { "id": "spawn-t1",        "role": "piece",     "rect": [6, 13, 1, 3] },
    { "id": "spawn-room",      "role": "spawn",     "rect": [7, 13, 2, 3] },
    { "id": "wool-a-room",     "role": "wool-room", "rect": [-2, 22, 3, 2] },
    { "id": "frontline-t1",    "role": "piece",     "rect": [-3, 4, 6, 5] },
    { "id": "wool-a-t1",       "role": "piece",     "rect": [-2, 18, 3, 1] },
    { "id": "wool-a-t1-inner", "role": "piece",     "rect": [-2, 19, 3, 3] }
  ],
  "zones": [ { "id": "mid-band", "rect": [-3, -4, 6, 8], "holes": [] } ],
  "placements": {
    "spawns": [ { "id": "spawn-1", "piece": "spawn-room", "at": [4, 6], "facing": "left" } ],
    "wools":  [ { "id": "wool-1",  "piece": "wool-a-room", "at": [6, 4] } ],
    "iron": [], "destroyables": [], "cores": []
  },
  "walls": [ { "a": "wool-a-t1", "b": "wool-a-t1-inner" } ],
  "boxes": [
    { "id": "hub",        "kind": "hub",        "rect": [-5, 9, 11, 9],   "members": ["hub-t1", "hub-t2", "hub-t3", "hub-t4"] },
    { "id": "spawn",      "kind": "spawn",      "rect": [6, 13, 3, 3],    "members": ["spawn-t1", "spawn-room"] },
    { "id": "wool-a",     "kind": "wool",       "rect": [-2, 18, 3, 6],   "members": ["wool-a-room", "wool-a-t1", "wool-a-t1-inner"] },
    { "id": "frontline",  "kind": "frontline",  "rect": [-3, 4, 6, 5],    "members": ["frontline-t1"] }
  ]
}
```

That document compiles clean, evaluates at score 0 with no lint, and reports every box producible. Its hub is
a ring whose hole is five cells by three; its spawn docks the hub's right side level with the hole and faces
left, into the hub; and its one wool approach is cut a cell off the hub into `wool-a-t1` and
`wool-a-t1-inner`, with the wall on that seam twelve blocks in front of the room. It also shows what a
composed plan invariably lacks, and the empty arrays are the honest part of it. **No piece carries a
`surface`**, so a
generated board is flat at the global 9 and every height on it arrives later, in the Sketch tool's relief
phase. **`iron`, `destroyables` and `cores` are always empty**: the composer makes CTW boards and places wools
and one spawn, nothing else. There is exactly **one zone**, the mid band, and it is a build zone — no water
lanes, no stepping stones, no centre island, and the `mid` box kind that would hold them appears in no
annotation because the band is a zone rather than a piece.

The `boxes` annotation is the composer explaining itself. It is authoring annotation only — the compiler, the
validator and the derivers ignore it — and it is what the Plan tool's feasibility panel reads a board against.

## The board a request produces

The four numbers do not scale a board smoothly; the player count lands in a band and the band is what changes
the board's shape. Each band carries its measured land per team — 2250 blocks² at nano, 4025 at micro, 7075 at
milli and 8730 at centi — its corridor width in blocks — 12 · 14 · 16 · 16, with the wool
approach one rung under — and its wool count: one a team at nano, two from micro up, sometimes three. Beside
the ladders sit roughly a dozen sampling weights — how often a wool bends, how often a bent wool is a donut, how often a big
square hub takes the ring — which steer the output's character more than anything else in the generator and
are, by `docs/generator/audit.md`'s own account, the least principled part of the model.

What that produces is measurable rather than arguable, and the endpoint reports it. Every response carries an
`observed` tally of the forms it saw, counted **before** the sieve, so asking for something a request never
makes still says what it does make. Four hundred boards per row, `rot_180`, taken from
`GET /api/compose?players=N&wools=z` — a filter nothing matches, which is what makes the scan run its full
budget:

| Players | Wool families seen | Hub forms | Frontline |
|---|---|---|---|
| 8 | I 339 · L 75 · U 2 · clamp 2 | ring 257 · single 94 · bar 33 · twin 16 | bar 203 · single 110 · twin 87 |
| 12 | I 339 · L 75 · U 2 · clamp 2 | ring 257 · single 94 · bar 33 · twin 16 | bar 203 · single 110 · twin 87 |
| 20 | I 362 · L 146 · donut 43 · H 15 · U 9 · clamp 7 | ring 220 · bar 94 · single 65 · twin 21 | bar 249 · single 76 · twin 75 |
| 30 | I 375 · L 152 · donut 46 · clamp 20 · U 19 · H 18 | ring 206 · twin 71 · G 41 · double-hole 34 · bar 27 · P 11 · single 10 | single 155 · twin 126 · bar 119 |

Read down the columns and the ladders are visible as behaviour. Eight and twelve players compose the same
boards, because both are the nano band. Every board carries a frontline. The ring is the commonest hub at every
size, and the wide holed bodies — double-hole, G and P — arrive only at thirty, where a hub is wide enough to
keep a bar beside a ring whose hole is still 12 blocks. A wool count sums past the board count because a family
is counted once per board however many approaches of it that board carries.

## The feed

One workspace, no phases. The rail on the left holds the filters, the grid in the middle holds the cards, and
the hold tray sits above them when anything is pinned.

**The filters split in two, and the split is about cost.** Players, symmetry, max score and wool count apply
on the Apply button and start the seed walk over. The structural filters — wool families, hub form, frontline
form — apply the moment a chip is clicked. Wool families are **must-include**: every family named has to be
present on the board. Hub and frontline are **any-of**. Max score is a slider to 8 where 8 means *any* and the
bound is simply not sent; wool count is a min/max pair where 0 means unset. The player slider runs 6 to 32 in
steps of two — 32 is the top band's floor, so the slider reaches every band — and a script is not bound
by it: the request's own clamp is 6–47.

The Z and scythe chips render disabled with the reason on the tooltip, because neither is in the production
mix — the Z is on the fill menu and asked for by no sampler, the scythe is off the menu outright. That is the
same distinction the shape catalog badges as *reachable* against *emitter only*, and `shapes.md` has the
reasons.

**The sieve runs cheapest-first, and that ordering is why a strict filter stays responsive.** For each seed
the endpoint composes the board, derives its structure — which is a classification of a handful of tiny cell
masks — and applies the structural filter. Only survivors are evaluated and only survivors are rendered, so a
board rejected on its wool families costs no evaluation and no SVG. Crucially the filters live wholly
*outside* the compose call and never abort an attempt mid-loop, which is what keeps a seed meaning the same
board under every filter and keeps the descriptor's reproduction promise honest.

The scan is bounded rather than open-ended. Without a structural filter the endpoint gives up after
`count × 4` seeds; with one it scans up to 400, because a conjunction like *donut and L* can be a few percent
of seeds. The response reports how many it scanned, and the page shows `scanned N · matched M` whenever a
structural filter is on — with a nudge when the match rate is under one in twelve, since a mix that rare is
better promoted to a held target than fished for.

**The census is what makes an empty grid legible.** Counts accumulate across pages into per-chip tallies, and
past 150 boards an absence starts being reported as an absence: a chip nothing has produced is dimmed, and an
empty grid says *this is not a mix these players and symmetry produce* rather than *no boards match*. The
census survives a re-sieve of the same request — it is counted before the sieve, so picking a filter cannot
hide the forms it filters against — and resets when players or symmetry change, because that is a different
request making different forms.

**A card carries the board and its verdicts.** The picture is the whole fanned board, server-rendered from the
same scene the PNG endpoint draws, coloured by role — hub violet, spawn green, wool amber, frontline orange —
with a build zone in pink and a water lane in blue under a diagonal hatch, and a legend along the bottom
naming every one of them. Badges along the top are the structural read, which are the same tokens the filter
chips use. The
foot carries the evaluator score, the wool count, the seed, and the land spend. Opening a card gives the same
in a drawer, with the score to two places, the per-box spend table, the top three soft terms by contribution,
and the descriptor as copyable JSON.

**Land spend is two currencies and the card says so.** *Footprint* is the box rectangle, fixed when the box
was seated; *land* is what the filled pieces actually cover, which is what the spend gate holds against the
budget. The per-box rows are footprints — a box does not know what its body left standing until it is filled —
and the total land is the unit's own, for **one team unit**, the board being that unit fanned.

The band's land buys two things, and the card reports both against their own shares. The **unit** takes nine
tenths of it; the **mid** takes the tenth each unit gave up, twice over, because the crossing's stones are one
piece of ground both teams stand on. So a twelve-player board reads `nano 104/81 · 128% · mid 16`: the unit
against the unit's budget, then the stones the crossing carries, counted once for the board. **The budget is
eaten.** The spawn, the frontline and each wool claim a fixed share as they are sized and the hub takes what
is left, never under a third; a unit whose built land falls outside 70–130% of *its* budget is resampled
rather than shipped.

**The score is a distance, not a grade.** Zero means the board sits inside every envelope the authored corpus
occupies — of 240 boards each at twelve, twenty and thirty players, 209, 74 and 30 respectively scored exactly
zero, with the ninetieth percentile at 0.28, 3.85 and 6.37. The terms that fire are almost always
`spawn-wool-ratio` and `wool-front-ratio`, then `frontline-width` at thirty: a spawn seated level with the
hub's middle or behind it stands nearer the wool on its own side than the one across the hub. A hard violation would add 1000 and dominate any
soft sum, which is why the slider stops at 8.

**Pinning and authoring are the two exits.** The pin toggle stores the descriptor's board and refreshes the
tray; the tray's thumbnails come from the stored rows rather than from the cards, so a board held in an
earlier session looks the same as one held a moment ago. *Author this plan* pins first if the board is not
already held, then commits the candidate to a map and navigates to `/maps/{slug}/plan`.

## What it refuses

The generator has almost no gate, because the gate it needs already ran inside the compose. Three things
nonetheless refuse.

**An unsupported symmetry is 400.** `rot_90` and `mirror_x` answer the refusal envelope every gate answers in
— `{"error": "unsupported symmetry", "message", "findings": [{"rule": "RQ1", "field": "symmetry", …}]}` —
rather than composing something wrong, and the page renders those two chips disabled with the reason on the
tooltip. An invalid parameter combination — a bad team count, a
symmetry that team count cannot fan — is 400 the same way, thrown where the request is made rather than
surfacing deep inside generation.

**A seed that composes nothing is skipped, silently.** Sixty attempts that all fail the acceptance gate raise
a `ComposeException`, and the browse loop catches it and moves to the next seed. That is deliberate — one
unusable seed is not a failure of the request — but it means the feed cannot distinguish a seed that produced
nothing from one that produced a board the filter rejected, and neither is reported. The scanned count
includes both.

**A descriptor that will not compose is 422 on pin.** `POST /api/compose/pin` re-composes from the descriptor
rather than trusting anything the client sends, so a descriptor from a different composer version can fail
there; a malformed one is 400.

Nothing else is refused. There is no minimum board, no rule about what a candidate must contain, and no check
that a pinned board is any good — the score is advice, and a board scoring 12 is as pinnable as one scoring 0.

## The API

Every endpoint is anonymous and rooted at `/api`.

| Endpoint | Answers | Fails with |
|---|---|---|
| `GET /compose?players=&symmetry=&cell=&seedStart=&count=` | `{cards, nextSeed, exhausted, scanned, observed}` — each card its descriptor, score, wool count, structural read, hard terms, top three soft terms, board SVG and land spend | 400 unsupported symmetry · 400 invalid parameters |
| … `&maxScore=&woolMin=&woolMax=` | the same, sieved on the evaluator score and the wool count | — |
| … `&wools=&hub=&front=` | the same, sieved structurally — `wools` must-include, `hub` and `front` any-of, all CSV | — |
| `POST /compose/pin` | the stored `PlanDetail` — re-composes from a **descriptor body**, the same `{players, teams, symmetry, seed, …}` record `GET /compose` is queried with, annotates its boxes and saves it as a generated row (idempotent by content hash) | 400 `RQ1` invalid descriptor · 422 `CO1` a board the composer cannot emit, its message naming the knob and the value |
| `GET /plans?origin=generated` | the hold tray: summaries newest-touched first, each with its descriptor and whether it is stale | — |
| `GET /plans/{id}` | the row plus its `planJson` | 404 |
| `GET /plans/{id}/svg` · `GET /plans/{id}/png` | the stored board as a thumbnail or as an image an image reader can open — both off one shared scene, so the encodings cannot disagree | 404 unknown · 422 unreadable plan |
| `DELETE /plans/{id}` | 204 — unpin | — |
| `POST /plan/{planId}/author` | `{slug}` — a `map` row at `stage=plan` seeded from the candidate | 404 unknown candidate |

Every plan-side endpoint a composed board can be put to — compile, evaluate, feasibility, inspect — is the
Plan tool's and takes the document as its body. `plan.md` has them.

## Driving it without the UI

The whole loop is three calls, and the first one does the work.

```
GET  /api/compose?players=20&symmetry=rot_180&seedStart=0&count=9
POST /api/compose/pin        <the chosen card's descriptor verbatim>   → {"id": 18, …}
POST /api/plan/18/author                                               → {"slug": "composed-p20-t2-42"}
```

From there the map is an ordinary plan-stage map and `plan.md`'s six-call chain finishes it, and the slug is
the candidate's name slugified — `Composed p20 t2 #42` becomes `composed-p20-t2-42`.

**The feed never hands over a plan document**, which is the one thing worth knowing before scripting against
it: a card carries its descriptor and a picture, and the only call that turns a descriptor back into the
document is `POST /api/compose/pin`. An agent that wants the JSON rather than a map therefore pins, reads
`planJson` off the response, and deletes the row — three calls where one would do, and the reason is that the
composer is the only thing that can build it.

Three habits make the feed usable from a script. **Walk with the cursor**: pass the previous response's
`nextSeed` as the next `seedStart` and stop on `exhausted`, rather than guessing a stride. **Ask for the
census before filtering**: a request with a filter nothing matches (`&wools=z` is the reliable one, since no
sampler draws a Z) runs the full 400-seed budget and returns `observed` for the whole scan, which says what
that player count and symmetry actually produce before a single card is fetched. And **read `scanned` against
the card count**: a strict conjunction returning three cards from four hundred seeds is a signal about the
request, not about the run.

The composer is also reachable without the server. `tools/compose/` holds file-based scripts that reference
`PgmStudio.Pgm` directly — `reproduction-gate.cs` checks every composed board reads back as producible, and
`fingerprints.cs` with `unit-fingerprint.cs` are the determinism gate. They build the project rather than
talking to the API, which makes them the right tool for measuring a change to composition and the wrong one
for fetching a board. Their cache is keyed on the
*script*, so an unchanged script re-runs its old binary against old project output and reports pre-change
numbers with no error — `CLAUDE.md`'s runfile note is load-bearing before any before/after measurement.

## Limits

**Nothing composed can be adjusted here.** There is no way to nudge a hub, re-roll one wool, or ask for the
same board a little wider. The unit of work is a whole board, and the only response to a board that is nearly
right is to author it and fix it in the Plan tool.

**Two of the four symmetries and one of the two team counts are unreachable.** The feed composes `rot_180` and
`mirror_z` at two teams. `mirror_x`, `rot_90` and every team count but two are legal at the type level and
refused at the endpoint, so the four-team board the model describes cannot be produced through this tool at
all — it is authored at the plan tier instead, which is where a four-team capture board is built.

**The composer reaches less of the shape vocabulary than the emitter builds**, and the gap is plumbing rather
than geometry. `ShapeEmitter.Emit` takes five placement knobs — a second donut attachment, a moved attachment,
an extended wool, and both scythe endpoint shifts — which `WoolBoxEmitter.Emit` passes through and
`WoolBoxEmitter.Fill`, the only path the compose pipeline uses, forwards none of (`G145`). Two families are in
the same position for a different reason: the **Z** is on the production menu and filled correctly but no
sampler ever draws one, and the **scythe** is off the menu outright for a stated reason (`G146`). So a board
can never carry any of the seven, however many seeds are walked — which is why both family chips render
disabled rather than simply never matching. `shapes.md` badges each of them, and is where to look for what a
knob does and why it stops.

**A generated board is flat, unpainted and CTW.** No elevation, no theme, no dressing, no destroy objective,
no water lane and no iron — every one of those is a later tool's or an unbuilt pass.

**A wall goes where a seam allows, not where a defence would choose.** About one wool in six hundred has no
seam that the attack crosses with void beyond both ends, and is left unwalled. Nothing weighs a wall against the
board's other walls: each wool is walled on its own terms.

**The mid is one plain band.** Twenty blocks of build zone spanning the axis, flush against both fronts, with
no stones and no centre island. A board's crossing is therefore the same crossing on every board, and the
richer mids the model describes layer in later. The one variation is the split band, drawn on about a third of
laterally-flipping boards and granted only where the face can host it.

**The drawer's hard-term list is structurally unreachable.** The browse endpoint evaluates with the same
profile the composer's acceptance gate used, so a board with a hard violation was already resampled away:
across 300 cards at twenty players, none carried one. The panel is honest but dead, and a board that reaches
the feed carries soft distance only.

**The census counts boards, not seeds.** Re-sieving the same request re-scans seeds already counted and adds
them again, so the denominator inflates across a long session of chip-toggling. The proportions stay right;
the absolute number does not, and it is the number the confidence threshold reads.

**The picture now carries a key.** The board render colours by role, and used to say so nowhere; blue being
the universal visual code for water is what let a generated board's central build zone be read as water on a
map that carried none. The card now draws a legend naming every role swatch and the two zone kinds, and a
build zone paints in a hue no water ever wears rather than a second shade of blue (`B95`) — a card still
answers *did this compose*, never *what is this*, but a reader can no longer mistake the colours for an
answer to the second question.
