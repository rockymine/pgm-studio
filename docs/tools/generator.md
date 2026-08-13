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
| `players` | 12 | Players per team, clamped 5–32. The only size input: the land budget and every structural ladder derive from it. |
| `teams` | 2 | 2 or 4. Fixed at 2 by the browse endpoint. |
| `symmetry` | `rot_180` | `rot_180` or `mirror_z` through the feed. `mirror_x` and `rot_90` are legal `ComposeRequest` values but the endpoint answers 400. |
| `cell` | 5 | Blocks per proxy cell — the plan grid's scale. No control writes it; it is honoured as a query parameter. |
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
{ "players": 12, "teams": 2, "symmetry": "rot_180", "cell": 5, "seed": 0,
  "composerVersion": "marker-id-1", "schema": 1 }
```

`schema` is the descriptor's own shape version, bumped if these fields change, so an old stored descriptor
still reads.

## What a compose produces

`Composer.ComposeStages` runs one direction and never reopens what an earlier step settled. The **envelope**
turns the player count into a land budget, a fanned board extent and the cell bounds one team unit may fill.
The **crossing** fixes the gap between the two fronts while the board is still empty — 20 blocks, four cells
at the default scale — and decides once whether this board wants a split band. **Allocation** places the hub,
chooses its form, works out what hangs off it, and seats each neighbour on the hub's real free surface,
producing typed boxes and the joints between them. **Filling** emits the hub first as the constraint source
and each neighbour to the width its own joint was granted. The finished unit is then **re-anchored on its
face**, so the band it will meet is the face itself rather than the hull of two offset copies. The **carve**
lays the mid band flush against the fronts. **Assembly** turns labelled pieces into a plan, dropping the
labels, and the plan is put to the evaluator's hard-term gate.

A rejected attempt is resampled whole; sixty are allowed before the compose throws, and a throw is skipped
rather than reported (below). The gate is seven hard terms — structural integrity, the `PC-C` corner-contact
and `G2` narrow-corridor lints, the `G5` void-hop band, the mid band's two-cell wool clearance, a wool ringed
by a hole, and the spawn-to-wool floor — every one of them on, at flat weight, in the default profile, and it
short-circuits on the first that fires.

What comes out is a plan document, and it is the same format the Plan tool edits. This is seed 0 at twelve
players under `rot_180`, exactly as `POST /api/compose/pin` stored it:

```json
{
  "plan": 1,
  "meta": { "name": "Composed p12 t2 #0" },
  "globals": { "cell": 5, "symmetry": "rot_180", "maxPlayers": 12, "surface": 9, "headroom": 11 },
  "pieces": [
    { "id": "hub-t1",       "role": "piece",     "rect": [-6, 6, 6, 4] },
    { "id": "spawn-t1",     "role": "piece",     "rect": [-7, 6, 1, 2] },
    { "id": "spawn-room",   "role": "spawn",     "rect": [-9, 6, 2, 2] },
    { "id": "wool-a-t1",    "role": "piece",     "rect": [-5, 10, 2, 1] },
    { "id": "wool-a-room",  "role": "wool-room", "rect": [-5, 11, 2, 2] },
    { "id": "frontline-t1", "role": "piece",     "rect": [-2, 2, 4, 4] }
  ],
  "zones": [ { "id": "mid-band", "rect": [-2, -2, 4, 4], "holes": [] } ],
  "placements": {
    "spawns": [ { "id": "spawn-1", "piece": "spawn-room", "at": [1, 1], "facing": "front" } ],
    "wools":  [ { "id": "wool-1",  "piece": "wool-a-room", "at": [1, 1] } ],
    "iron": [], "destroyables": [], "cores": []
  },
  "cliffs": [], "walls": [],
  "boxes": [
    { "id": "hub",       "kind": "hub",       "rect": [-6, 6, 6, 4],  "members": ["hub-t1"] },
    { "id": "spawn",     "kind": "spawn",     "rect": [-9, 6, 3, 2],  "members": ["spawn-t1", "spawn-room"] },
    { "id": "wool-a",    "kind": "wool",      "rect": [-5, 10, 2, 3], "members": ["wool-a-t1", "wool-a-room"] },
    { "id": "frontline", "kind": "frontline", "rect": [-2, 2, 4, 4],  "members": ["frontline-t1"] }
  ]
}
```

That document compiles clean, evaluates at score 0, and reports every box producible. It also shows what a
composed plan invariably lacks, and the empty arrays are the honest part of it. **`cliffs` and `walls` are
always empty** — the elevation pass that would write them is not built. **No piece carries a `surface`**, so a
generated board is flat at the global 9 and every height on it arrives later, in the Sketch tool's relief
phase. **`iron`, `destroyables` and `cores` are always empty**: the composer makes CTW boards and places wools
and one spawn, nothing else. There is exactly **one zone**, the mid band, and it is a build zone — no water
lanes, no stepping stones, no centre island, and the `mid` box kind that would hold them appears in no
annotation because the band is a zone rather than a piece.

The `boxes` annotation is the composer explaining itself. It is authoring annotation only — the compiler, the
validator and the derivers ignore it — and it is what the Plan tool's feasibility panel reads a board against.

## The board a request produces

The four numbers do not scale a board smoothly; they cross thresholds, and a threshold changes its shape. Land
above 2500 widens every non-wool corridor from two cells to three; below 800 there is no budget for a
frontline at all and the hub fronts the mid directly; below 600 the unit carries a single wool; sixteen
players or more makes it a full team, two or three wools rather than one or two. Beside the ladders sit
roughly a dozen sampling weights — how often a wool bends, how often a bent wool is a donut, how often a big
square hub takes the ring — which steer the output's character more than anything else in the generator and
are, by `docs/generator/audit.md`'s own account, the least principled part of the model.

What that produces is measurable rather than arguable, and the endpoint reports it. Every response carries an
`observed` tally of the forms it saw, counted **before** the sieve, so asking for something a request never
makes still says what it does make. Four hundred boards per row, `rot_180`, taken from
`GET /api/compose?players=N&wools=z` — a filter nothing matches, which is what makes the scan run its full
budget:

| Players | Wool families seen | Hub forms | Frontline |
|---|---|---|---|
| 8 | I 278 · L 115 · donut 7 | bar 284 · single 116 | none 400 |
| 12 | I 308 · L 124 · donut 23 · clamp 1 · U 1 | bar 218 · single 122 · twin 60 | bar 263 · single 43 · twin 34 · none 60 |
| 20 | I 370 · L 133 · donut 37 · U 8 · H 6 · clamp 4 | ring 221 · bar 86 · double-hole 38 · twin 28 · P 9 · G 9 · single 9 | bar 185 · single 86 · twin 75 · none 54 |
| 30 | I 369 · L 145 · donut 42 · U 8 · H 8 · clamp 3 | ring 214 · bar 86 · double-hole 39 · twin 28 · G 13 · P 12 · single 8 | bar 193 · single 80 · twin 75 · none 52 |

Read down the columns and the ladders are visible as behaviour. An eight-player board never has a frontline
and never has a hub with a hole in it, because neither is affordable; at twelve the frontline appears on
six boards in seven and the hub is still solid or branched; by twenty the ring has taken over the hub menu
outright and the holed bodies arrive with it. A wool count sums past the board count because a family is
counted once per board however many approaches of it that board carries.

## The feed

One workspace, no phases. The rail on the left holds the filters, the grid in the middle holds the cards, and
the hold tray sits above them when anything is pinned.

**The filters split in two, and the split is about cost.** Players, symmetry, max score and wool count apply
on the Apply button and start the seed walk over. The structural filters — wool families, hub form, frontline
form — apply the moment a chip is clicked. Wool families are **must-include**: every family named has to be
present on the board. Hub and frontline are **any-of**. Max score is a slider to 8 where 8 means *any* and the
bound is simply not sent; wool count is a min/max pair where 0 means unset. The player slider runs 6 to 30 in
steps of two, which is narrower than the request's own 5–32 clamp, and a script is not bound by it.

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
same scene the PNG endpoint draws, coloured by role — hub violet, spawn green, wool amber, frontline orange,
zones blue. Badges along the top are the structural read, which are the same tokens the filter chips use. The
foot carries the evaluator score, the wool count, the seed, and the land spend. Opening a card gives the same
in a drawer, with the score to two places, the per-box spend table, the top three soft terms by contribution,
and the descriptor as copyable JSON.

**Land spend is two currencies and the card says so.** *Footprint* is the box rectangle, fixed when the box
was seated; *land* is the walkable terrain inside it, which is what a fill actually spends. Both are reported
per box kind, for **one team unit** — the board is that unit fanned — against the envelope's own per-team land
budget converted from blocks² to cells. Seed 0 above spends 52 land cells against a budget of 50.4, so its
card reads `52/50 · 103%`, split hub 24, frontline 16, spawn 6, wool 6.

**The score is a distance, not a grade.** Zero means the board sits inside every envelope the authored corpus
occupies, which is most of them — of 240 boards each at twelve, twenty and thirty players, 167, 131 and 109
respectively scored exactly zero, with the ninetieth percentile at 1.25, 3.06 and 3.52. The terms that fire
are almost always `spawn-wool-ratio` and `wool-front-ratio`. A hard violation would add 1000 and dominate any
soft sum, which is why the slider stops at 8.

**Pinning and authoring are the two exits.** The pin toggle stores the descriptor's board and refreshes the
tray; the tray's thumbnails come from the stored rows rather than from the cards, so a board held in an
earlier session looks the same as one held a moment ago. *Author this plan* pins first if the board is not
already held, then commits the candidate to a map and navigates to `/maps/{slug}/plan`.

## What it refuses

The generator has almost no gate, because the gate it needs already ran inside the compose. Three things
nonetheless refuse.

**An unsupported symmetry is 400.** `rot_90` and `mirror_x` answer
`{"error": "unsupported symmetry 'rot_90'"}` rather than composing something wrong, and the page renders those
two chips disabled with the reason on the tooltip. An invalid parameter combination — a bad team count, a
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
| `POST /compose/pin` | the stored `PlanDetail` — re-composes from the descriptor body, annotates its boxes, saves it as a generated row (idempotent by content hash) | 400 invalid descriptor · 422 composition failed |
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
`PgmStudio.Pgm` directly — `matrix.cs` composes a fixed grid of cases and prints a verification matrix,
`gallery-gen.cs` renders a curated set to one HTML page, `reproduction-gate.cs` and `fingerprints.cs` are the
determinism gate. They build the project rather than talking to the API, which makes them the right tool for
measuring a change to composition and the wrong one for fetching a board. Their cache is keyed on the
*script*, so an unchanged script re-runs its old binary against old project output and reports pre-change
numbers with no error — `CLAUDE.md`'s runfile note is load-bearing before any before/after measurement.

## Limits

**Nothing composed can be adjusted here.** There is no way to nudge a hub, re-roll one wool, or ask for the
same board a little wider. The unit of work is a whole board, and the only response to a board that is nearly
right is to author it and fix it in the Plan tool.

**Two of the four symmetries and one of the two team counts are unreachable.** The feed composes `rot_180` and
`mirror_z` at two teams. `mirror_x` and the four-team `rot_90` are legal at the type level and refused at the
endpoint, so the four-team board the model describes cannot be produced through this tool at all.

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
no water lane, no iron, and no defensive wall — every one of those is a later tool's or an unbuilt pass.
`cliffs` and `walls` exist in the format as schema waiting for an elevation pass that is not written.

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

**The picture has no key.** The board render colours by role and says so nowhere, and blue is the universal
visual code for water — a generated board's central build zone has already been read as water on a map that
carries none. Until `B95` lands, a card answers *did this compose*, never *what is this*.
