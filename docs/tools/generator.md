# The Generator tool

## What it is

The generator shows whole boards. It is the one tool in the studio that authors nothing: a board here is not
drawn, it is **composed** — out of a size band, a team count, a symmetry mode and a seed, and out of nothing
else. The boards are composed ahead of time into a **library**, 500 for every size band and symmetry, and the
tool browses it. Its route is `/generator`, and what it shows is a feed of those candidates, each one a
complete board — a sketch and the game settings it is played by — rendered as a picture of the fanned board.

The work it supports is sieving, not editing. Every knob on the page decides which boards are *shown*; none
of them changes a board. A candidate worth taking leaves the tool by being **opened in Sketch**, which makes a
map of it and opens the Sketch tool on that map. From there the map is an ordinary sketched map — its ground
shaped, painted and dressed in Sketch, its play finished in Configure — and the generator is done with it.

Its companion page is the shape catalog at `/catalog`, which shows the vocabulary the composer fills boxes
with. That is `shapes.md`'s subject, and the two sit side by side in the studio's bar.

What the composer *is* — the pipeline, the shape model, the rule kinds — is `docs/generator/model.md`, which
governs. This document is the tool: what a request is, what comes back, what the numbers on a card mean, and
how to drive the whole thing without a browser.

## What it writes

**Browsing writes nothing, and composes nothing.** Every card is read from the library, a `composed_board`
row the fill wrote (below); the feed has no way to compose a board on request.

**Filling writes the library.** `PgmStudio.Import --compose-library` composes what the running composer
version is missing, up to `ComposedBoardLibrary.PerBand` — 500 — boards for each of the four size bands and
the two symmetries the feed offers, seed 0 upward, and stores each as a `composed_board` row: the board as it is
handed over — its sketch layout and its intent (*What a compose produces*, below) — the
picture of the pieces the composer laid, its score, wool count and structure as columns the feed filters and
orders on, and the rest of its card — the fired hard terms, the top three soft terms and the land spend — as
`card_json`. A seed that composes nothing is skipped. Once every band and symmetry holds its 500, the
boards any other composer version made are deleted; they are the composer's output under settings the library
states, so nothing is lost that the fill cannot make again.

**Making a map writes a map**, through the road a map's source takes (`PUT /api/map/{slug}/source`,
`docs/tools/flow.md`): the library board the card names, labelled for the card's player count, is stored as the
base of a map — its layout and its intent, each island given its team — under the first free slug its name gives, `composed-p12-t2-2` and then `composed-p12-t2-2-2`. The world is built
and the intent projected in the same call, so the map stands at `stage=configure` and the Sketch tool opens on it.
The board stays in the library: the map holds its own copy from that moment on, and a second map of the same
card is a second map.

**Nothing is kept that is not a map.** There is no store of candidates between the feed and a map, and no
second editor to look at one in: a board is seen in the feed and its drawer, and worked on once it is a map.
The plan rows a candidate was once kept as are history (`docs/tools/plan.md`, *What it is*).

## The request

A composed board is named by five values and no geometry.

| Field | Default | Is |
|---|---|---|
| `players` | 12 | Players per team, clamped 6–47. The only size input, and its job is to name a **size band** — nano 6–13, micro 14–21, milli 22–31, centi 32 and up — which is what the land budget and every structural ladder read. Two counts in one band compose the **same board** from the same seed, differing only in the plan's name and `maxPlayers` (`Composer.Label`), so the library holds a board once per band and a card is labelled with the count asked for. A count above centi's range is clamped into it because centi is the top of the ladder. |
| `teams` | 2 | 2 or 4 at the plan tier. The library holds two-team boards only and the feed answers 400 `RQ1` on any other count, naming the field — a board that is not the one asked for is worse than no board. |
| `symmetry` | `rot_180` | `rot_180` or `mirror_z`, the two the library holds. `mirror_x` and `rot_90` are legal `ComposeRequest` values but the feed answers 400. |
| `cell` | 4 | Blocks per proxy cell — the plan grid's scale. The library holds the default cell only. |
| `seed` | — | Any unsigned 64-bit integer. Drives every draw the composer makes; the library holds seeds from 0 up. |

The feed holds players and symmetry fixed and pages through the library's boards for them, **best score first,
the seed breaking ties**, so a request is those two values, the filters, and a position: `from` is where the
page starts and `count` how many boards it returns (clamped 1–48; the page asks for 9).

**A seed reproduces its board exactly, within one composer version.** The generator behind it is a small
deterministic one chosen for that reason rather than the platform's, and no clock or identifier enters it.
Sampling *order* is part of the promise: draws come off in one fixed sequence, so inserting a draw anywhere
re-rolls every seed downstream of it. That is why any change to composition geometry bumps
`ComposerVersion.Current` — `walled-5` today — and why the version rides on every library board.

The descriptor is the card's identity and the whole of what making a map needs:

```json ComposeRequestDto
{ "players": 12, "teams": 2, "symmetry": "rot_180", "cell": 4, "seed": 0,
  "composerVersion": "walled-5", "schema": 1 }
```

`schema` is the descriptor's own shape version, bumped if these fields change, so an old stored descriptor
still reads.

## What a compose produces

`Composer.ComposeStages` runs one direction and never reopens what an earlier step settled. The **envelope**
turns the player count into a land budget, a fanned board extent and the cell bounds one team unit may fill.
The **crossing** fixes the gap between the two fronts while the board is still empty, because the allocator
takes it as the axis margin everything else is laid out behind: one hop either side of the stone the band will
carry, or a flat 30 blocks front to front where it will carry none. It decides once whether this board wants a
split band, which is a crossing that carries no stone where the face grants it. **Allocation** places the hub,
chooses its form, works out what hangs off it, and seats each neighbour on the hub's real free surface,
producing typed boxes and the joints between them. Every unit carries a frontline on the hub's front edge, and
a spawn on a side edge is seated in line with the hub's hole, or with its middle on a hub without one; a unit
with a donut moves its spawn to the back edge's end nearer the donut. **Filling** emits the hub first as the constraint source
and each neighbour to the width its own joint was granted. The finished unit is then **re-anchored on its
face**, so the band it will meet is the face itself rather than the hull of two offset copies. The **carve**
lays the mid band flush against the fronts. **Walling** then gives each wool approach its defence walls (below).
**Assembly** turns labelled pieces into a plan, dropping the labels, and the plan is put to the evaluator's
hard-term gate. A plan that passes is **handed over** as a sketch (below).

A rejected attempt is resampled whole; sixty are allowed before the compose throws, and a throw is skipped
rather than reported (below). The gate is nine hard terms — structural integrity, the `PC-C` corner-contact
and `G2` narrow-corridor lints, the `G5` void-hop band, the mid band's two-cell wool clearance (`BZ6`), the
20-block spawn-to-wool floor (`WL2`) and the wool room that shares an edge with its own spawn (`WL14`; a composed
wool unit is a room behind its own lane, so the second never fires on a composed board), and two floors on the crossing: the spawn at least 55 blocks by the walk
from the build band (`SP10`) and every wool at least 59 (`WL19`). It runs the **composer profile**, every term
on at flat weight, and short-circuits on the first that fires. The two crossing floors are the author's
judgement of composed boards and bind them alone: the default profile the editor lint runs leaves them off.

**Every hub hole is at least 12 blocks across.** A ring's hole, and the ring inside a P, double-hole or G, keeps
`LN6`'s floor for a plain hole, so the composer never draws a slit a player jumps. **Every wool approach gets
one defence wall** where a seam qualifies: across the route the attack takes into the approach, on a seam with
no land beyond either end so it is crossed rather than rounded (`PL17`), a lane mouth wide and 10–20 blocks in
front of the room (`ST8`). A straight lane is cut in two to make that seam, which is why a walled approach
carries a `-inner` piece, and a back-room lane is built at least four cells long so one fits. A two-legged
approach whose room lies deeper than the window takes the nearest qualifying seam its attack crosses. A donut
is two ways round its hole and takes **two walls**, one across each leg as near the entry bar as a seam
qualifies, since one wall would leave the other way open. A unit with a donut stands its spawn behind the
hub, at the end of the back edge nearer the donut, because a donut draws a unit lopsided toward its side.

**What a compose hands over is a sketch, not a plan.** The plan is the composer's own working document: the
gate is asked of it, and then it is compiled once into what a map is made from (`ComposedSketch`) — a sketch
layout and the intent it is played by. The compile is the plan import's (`PlanCompiler`) with one
difference: abutting pieces fuse **by box** rather than by height. The hub's pieces are one shape, the front
line's another, each wool approach and the spawn their own, each named for its box and its surface; a piece in no
box — a mid stone — fuses with its own component as a plan's does. Every shape stays in the one mirrored group, so
the Sketch tool moves the unit as a whole and a box is picked and reshaped alone.

This is seed 2 at twelve players under `rot_180`, as `POST /api/compose/map` stores it:

```text
shape            operation  height  vertices (blocks)
frontline-9      add        9       (-12,16) (12,16) (12,36) (-12,36)
hub-9            add        9       (-20,36) (24,36) (24,72) (-20,72)
spawn-9          add        9       (24,52) (36,52) (36,64) (24,64)
wool-a-9         add        9       (-12,72) (0,72) (0,84) (12,84) (12,96) (-12,96)
mid-stone-0-9    add        9       (-8,-4) (8,-4) (8,4) (-8,4)
void-1-cut       subtract   —       (-8,48) (12,48) (12,60) (-8,60)
group team (mirrored): every shape above
```

Its hub is a ring, so it is two things: the hub's outline and the cut that keeps its hole void, five cells by
three. The spawn docks the hub's right side level with the hole, its room drawn from the intent over the far two thirds
of `spawn-9`; the one wool approach is an L whose room closes its far end, with a defence wall on the seam the
attack crosses twelve blocks in front of the room. The intent carries two spawns and two wools, one of each a team,
named *Composed p12 t2 #2* for twelve players a team.

It also shows what a composed board invariably lacks. **Every shape stands at the global 9**, so a generated
board is flat and every height on it arrives later, in the Sketch tool's Terraform phase. **No iron, no destroyable
and no core**: the composer makes CTW boards and places wools and one spawn, nothing else. There is exactly **one
build zone**, the mid band — no water lanes and no centre island.

## The board a request produces

The four numbers do not scale a board smoothly; the player count lands in a band and the band is what changes
the board's shape. Each band carries its measured land per team — 2250 blocks² at nano, 4025 at micro, 7075 at
milli and 8730 at centi — its corridor width in blocks — 12 · 14 · 16 · 16, with the wool
approach one rung under — and its wool count: one a team at nano, two from micro up, sometimes three. Beside
the ladders sit roughly a dozen sampling weights — how often a wool bends, how often a bent wool is a donut, how often a big
square hub takes the donut — which steer the output's character more than anything else in the generator and
are, by `docs/generator/audit.md`'s own account, the least principled part of the model.

What that produces is measurable rather than arguable, and the feed reports it. Every response carries an
`observed` tally of the forms the library holds for the band and symmetry, counted **before** the filters, so
asking for something the settings never make still says what they do make. Four hundred composed boards per
row, `rot_180`:

| Players | Wool shapes | Hub forms | Frontline |
|---|---|---|---|
| 8 | I 340 · L 66 · clamp 4 · H 2 · U 1 | donut 295 · L 60 · I 39 · U 6 | I 175 · L 117 · U 108 |
| 12 | I 340 · L 66 · clamp 4 · H 2 · U 1 | donut 295 · L 60 · I 39 · U 6 | I 175 · L 117 · U 108 |
| 20 | I 364 · L 127 · H 22 · donut 21 · U 20 · clamp 11 | donut 204 · I 137 · L 37 · U 22 | I 241 · L 90 · U 69 |
| 30 | I 368 · L 134 · donut 26 · U 23 · clamp 22 · H 19 | donut 190 · U 81 · G 39 · I 38 · double-hole 31 · P 13 · L 8 | L 161 · I 134 · U 105 |

Read down the columns and the ladders are visible as behaviour. Eight and twelve players compose the same
boards, because both are the nano band. Every board carries a frontline. The donut is the commonest hub at every
size, and the wide holed compounds — double-hole, G and P — arrive only at thirty, where a hub is wide enough to
keep a bar beside a ring whose hole is still 12 blocks. A wool count sums past the board count because a form
is counted once per board however many of its wools take it.

## The library

**The server composes its own library, after each deploy, at the lowest priority it has.** `deploy.sh` starts
`PgmStudio.Import --compose-library` from the release it has just brought up, as a transient unit of its own
(`pgm-studio-library`, `journalctl -u pgm-studio-library` to watch it) running as the studio's user at
`Nice=19`, a tenth of the default CPU weight and idle I/O, so the live studio always has the cores first. A
release whose composer already has its library composes nothing and exits, and a fill still running from the
release before is stopped, since the new composer is the one the library is for (`docs/deployment.md`).

**What it costs is the scoring, not the composing.** Evaluating a board is most of its cost and grows with the
board, so a band's 500 take longer the bigger it is. On a four-core cloud container, one core, a release build:

| Band | 500 boards, one symmetry |
|---|---|
| nano | 34–40 s |
| micro | 66–67 s |
| milli | 146–151 s |
| centi | 182–196 s |

The whole library of 4,000 boards is fifteen minutes there, and 23 MB in the database with its indexes. The
deployed studio's cores are slower and yield to every request, so its fill takes longer; it runs in full only
when a deploy brings a new composer version.

**While a new composer's library is being filled, the feed shows the version before it.** For each band and
symmetry the feed reads the running composer's boards once they number 500, else the boards of the version
stored most recently, else whatever part of the running composer's set exists
(`ComposedBoardStore.ServedVersionAsync`). A card carries the version that made its board, so a map made of one from the older set is the board it showed.
Once the running composer's set is complete, the older boards are deleted.

**The library is fixed at 500 per band and symmetry, and nothing composes more on request.** That is the
author's decision: the feed is for inspiration, finite by design, and every filter reads the same stored set.
The command's `--per-band`, `--bands` and `--symmetries` narrow a fill for a test or a local look; the deploy
passes none of them.

## The feed

One workspace, no phases. The rail on the left holds the filters and the grid in the middle holds the cards.

**The rail has two sections, and both apply at once.** *Layout settings* — players per team, symmetry and
wools per team — restart the page from the library's first board when a value changes. Players per team is
one chip per **size band**, labelled with the band's name and its range (`Nano · 6–13`, `Centi · 32+`), read
from `SizeBands` in the vocabulary leaf rather than restated in the client; exactly one band holds, and the
request names it by the count in the middle of its range. A band is one at a time because the feed is one band's
library; a request is still bound only by the endpoint's own clamp, 6–47. Wools per team is one chip per count, `1` to `3` (`WoolCounts` in the vocabulary leaf, the range the composer draws from), and
several may be ticked at once: a board matches any one ticked count, and none ticked takes any. The chips carry no
counts, because the census holds forms, not wool counts. *Filter by shape* — wool shapes, hub and front line — is a set of ticked
chips, each with the count of boards in the library that have it, and *Clear* empties all three. Wool shapes
are **must-include**: every family ticked has to be present on the board. Hub and front line are **any-of**.
A side always has a front line; where the composer places no piece for it (the `none` form) the hub's own edge
facing the mid is that front line, so its chip reads *Hub edge*.
Under the shape chips a link, *Browse every shape in the catalog*, opens `/catalog`, the vocabulary the
composer fills boxes from.

The Z and scythe chips render disabled with the reason on the tooltip, because neither is in the production
mix — the Z is on the fill menu and asked for by no sampler, the scythe is off the menu outright. That is the
same distinction the shape catalog badges as *reachable* against *emitter only*, and `shapes.md` has the
reasons.

**Every filter is a query over the library.** The score, the wool count, the wool families, the hub form and
the frontline form are columns of `composed_board`, so a filter narrows the stored set in the database and a
strict conjunction costs what a loose one does. The response says how many boards match, and the page shows
`M of N layouts match` above the grid. **The feed ends where the library does**: scrolling and *Load more* stop
at the last matching board, and the page says *No more layouts for these settings*.

**The census is what makes an empty grid legible.** Every page carries the census over every board the library
holds for the band and symmetry, counted before the filters, so picking a filter cannot hide the forms it
filters against. Past 150 boards an absence is reported as an absence: a chip nothing in the library has is
dimmed, and an empty grid says *these players and symmetry don't produce it* rather than *none of the layouts
match these filters*. A library with nothing for the settings says it is still being generated.

**A card is the board and its id.** The picture is the whole fanned board in a square frame on the
theme's board ground (`--board-bg`: white in the light theme, near-black in the dark one), server-rendered from
the same scene the PNG endpoint draws, in four inks. Spawn rooms are violet and wool rooms green, each a pale fill
with a strong edge, in the hues the sketch's canvas draws those rooms in (`--canvas-role-spawn`,
`--canvas-role-wool-room`: the page's `--board-spawn*` and `--board-wool*` tokens are `color-mix`es of them, so the
two cannot drift apart); a build zone is a dashed blue outline; and everything else — hub, front line, approaches, any
other piece — is one ground grey, a tint of the canvas's piece colour (`--canvas-role-piece`), so a board
and the sketch's pieces read as one. Every half of the fan is drawn at full strength: the mirrored and rotated images are the same ground as the base
unit, and no ink fades with the symmetry (a deliberate difference from the whitepaper's figures). The names of the grey roles
live in the structure line of the detail drawer, not in colour. The inks are `PlanBoardPalette`'s
`Ground`, `Spawn`, `WoolRoom` and `Zone`; each reads its `--board-*` tokens first (`--board-ground`,
`--board-ground-edge`, `--board-spawn`, `--board-spawn-edge`, `--board-wool`, `--board-wool-edge`,
`--board-zone`, `--board-iron`) and falls back to the paper values, which is why one SVG serves both themes. A
water lane draws exactly as a build zone: the composer builds none, and the key has one outline.
The picture carries no text: the page draws the key once above the grid (*Spawn*, *Wool room*, *Ground (rest)*,
*Build zone*), from the `key` the feed returns. Under the picture the card shows only the layout's id,
`composed-p{players}-t{teams}-{seed}`, in the same name label the library's cards carry (`PictureCard`); the
score, wool count and structure line are not on the card, and the score is shown nowhere on the page.

**The drawer is the larger view, and it can tint one role at a time.** Opening a card gives a drawer 600 px wide that
reads top to bottom. The header carries the layout's name (`composed-p{players}-t{teams}-{seed}`) as its title,
with the close button at its right. Under it sit the picture, at most 48% of the viewport high; a *Highlight* row of six toggles (*Hub*, *Front line*,
*Approaches*, *Spawn*, *Wool rooms*, *Build zone*) with the key below it; a written description of the layout;
three facts (players per team, wools per team, symmetry); and a footer with the one primary action, *Open in Sketch*, a text button
beside a note saying it makes a new map of the layout and opens it in the Sketch tool.
Every piece in the SVG carries a `role-*` class from `BoardRoles` (`hub`, `frontline`, `approach`, `spawn`,
`wool`, `other`, and `zone` for a zone), and a toggle adds `board-hl-{role}` to the picture's frame, which
restyles those pieces in the accent by CSS: the picture is not requested again. None are on when the drawer
opens, several may be on together, and the rest of the board stays as drawn.

**The description is composed from the card.** `LayoutDescription.Of` (in `PgmStudio.Vocabulary`, beside the
words it spells) turns the descriptor and the structural read into two or three plain sentences: the team count,
the size band's player range and the symmetry; the wools each team has and the shapes of their approaches; and
the hub and front line forms. It returns runs of text, and a run that names a glossary word (*size band*, *wool*,
*wool room*, *approach*, *hub*, *front line*, *mid*) carries that term, which the drawer links to
`/glossary#{slug}`. For a twelve-player, rotate-180 board with two wools: "A two-team layout for 6–13 players a
team, copied by a half turn about the centre. Each team has two wools, each kept in its own wool room and reached
by I-shaped and L-shaped approaches. The team's side is built around a donut-shaped hub, with a U-shaped front line
facing the mid." The drawer shows no raw numbers, no seed, no descriptor JSON and no hard-term list; the card's
`descriptor`, `spend`, `hardTerms` and `topSoft` remain on `GET /compose` for an agent.

**Land spend is two currencies, and the compose answer says so.** *Footprint* is the box rectangle, fixed when the box
was seated; *land* is what the filled pieces actually cover, which is what the spend gate holds against the
budget. The per-box rows are footprints — a box does not know what its compound left standing until it is filled —
and the total land is the unit's own, for **one team unit**, the board being that unit fanned.

The band's land buys two things, and the answer's `spend` reports both against their own shares. The **unit** takes nine
tenths of it; the **mid** takes the tenth each unit gave up, twice over, because the crossing's stones are one
piece of ground both teams stand on. So a twelve-player board reads `nano 104/81 · 128% · mid 16`: the unit
against the unit's budget, then the stones the crossing carries, counted once for the board. **The budget is
eaten.** The spawn, the frontline and each wool claim a fixed share as they are sized and the hub takes what
is left, never under a third; a unit whose built land falls outside 70–130% of *its* budget is resampled
rather than shipped.

**The score is a distance, not a grade.** Zero means the board sits inside every envelope the authored corpus
occupies — of 240 boards each at twelve, twenty and thirty players, 175, 58 and 25 respectively scored exactly
zero, with the ninetieth percentile at 1.17, 2.92 and 5.67. The terms that fire are almost always
`spawn-wool-ratio` and `wool-front-ratio`, then `thin-middle` and `frontline-width`: a spawn beside the hub
stands nearer the wool at the back than the one across the hub, however squarely it faces the hole, and about
one board in five crosses a middle thinner than its size's floor or longer than twice its width (`MD7`, `MD8`). A hard violation would add 1000 and dominate any
soft sum.

**Making a map is the one exit.** *Open in Sketch* makes a map of the library board the descriptor names
(`POST /api/compose/map`) and opens it at `/maps/{slug}/sketch`; the button says *Making the map…* while the
world is built, and a refusal leaves the drawer open with a sentence saying the map could not be made.

## What it refuses

The generator has almost no gate, because the gate it needs already ran inside the compose, when the library
was filled. Three things nonetheless refuse.

**An unsupported symmetry is 400.** `rot_90` and `mirror_x` answer the refusal envelope every gate answers in
— `{"error": "unsupported symmetry", "message", "findings": [{"rule": "RQ1", "field": "symmetry", …}]}` —
rather than composing something wrong, and the page renders those two chips disabled with the reason on the
tooltip. An invalid parameter combination — a bad team count, a
symmetry that team count cannot fan — is 400 the same way, thrown where the request is made rather than
surfacing deep inside generation.

**A seed that composes nothing is skipped, silently.** Sixty attempts that all fail the acceptance gate raise
a `ComposeException`, and the fill catches it and moves to the next seed, so the library simply holds no board
for that seed. One unusable seed is not a failure of the fill, and none has been seen in thousands.

**A descriptor the library does not hold is 404.** `POST /api/compose/map` makes a map of the stored board the
descriptor names rather than of anything the client sends, so a descriptor for a seed never filled, or from a
composer version whose boards have been deleted, answers 404; a malformed one is 400. The map is stored as a
source is, so what refuses a source refuses it too, and nothing is stored when it does.

Nothing else is refused. There is no minimum board, no rule about what a candidate must contain, and no check
that a board is any good — the score is advice, and a board scoring 12 makes a map as one scoring 0 does.

## The API

Every endpoint is rooted at `/api`; a read is open to anyone and a write needs someone on the whitelist
([`docs/access.md`](../access.md)), which is what the 401 and 403 no row repeats are. The page greys *Open in
Sketch* for anyone off the whitelist.

| Endpoint | Answers | Fails with |
|---|---|---|
| `GET /compose?players=&symmetry=&from=&count=` | `{cards, next, end, matching, observed, key}` — a page of the library, best score first: each card its descriptor, score, wool count, structural read, hard terms, top three soft terms, board SVG and land spend; `next` the position to ask from, `end` whether this page reaches the last matching board, `matching` how many match, `observed` the census of every board held for the band and symmetry, `key` the four inks the SVGs are drawn in | 400 unsupported symmetry · 400 unsupported team count |
| … `&maxScore=&woolCount=` | the same, sieved on the evaluator score and the wool count; `woolCount` is CSV and any-of (`1,3` takes boards with one or three wools), and the page sends it only, `maxScore` is for a script | — |
| … `&wools=&hub=&front=` | the same, sieved structurally — `wools` must-include, `hub` and `front` any-of, all CSV | — |
| `POST /compose/map` | `{slug}` — makes a map of the library board a **descriptor body** names, the `{players, teams, symmetry, seed, …}` record a card carries: its layout and intent labelled for its player count and its islands given their teams, stored as a source is under the first free slug its name gives, with its world built | 400 `RQ1` invalid descriptor · 404 a board the library does not hold · 422 a board the source road refuses, as `PUT /map/{slug}/source` does |

Every endpoint a map made here is put to after that is the Sketch tool's and Configure's, by its slug.

## Driving it without the UI

The whole loop is two calls.

```
GET  /api/compose?players=20&symmetry=rot_180&count=9
POST /api/compose/map        <the chosen card's descriptor verbatim>   → {"slug": "composed-p20-t2-42"}
```

From there the map is an ordinary sketched map, read and written by its slug — `GET /api/map/{slug}/sketch`,
and `GET /api/map/{slug}/intent` hand over its two documents, and
`PUT /api/map/{slug}/source` stores it again with a refinement on it. The slug is the board's name slugified —
`Composed p20 t2 #42` becomes `composed-p20-t2-42`.

**The feed never hands over a board's documents**, which is the one thing worth knowing before scripting
against it: a card carries its descriptor and a picture, so a page of 48 stays light, and the only call that
hands over the documents is the one that makes a map of them.

Three habits make the feed usable from a script. **Walk with the position**: pass the previous response's
`next` as the next `from` and stop on `end`, rather than guessing a stride. **Read the census first**: every
response carries `observed` for every board held for the band and symmetry, so the first page already says
what those settings produce. And **read `matching`**: it counts the whole library's matches for the filters,
so a strict conjunction says at once how rare it is.

The composer is also reachable without the server. `tools/compose/` holds file-based scripts that reference
`PgmStudio.Pgm` directly — `reproduction-gate.cs` checks every composed board reads back as producible, and
`fingerprints.cs` with `unit-fingerprint.cs` are the determinism gate. They build the project rather than
talking to the API, which makes them the right tool for measuring a change to composition and the wrong one
for fetching a board. Their cache is keyed on the
*script*, so an unchanged script re-runs its old binary against old project output and reports pre-change
numbers with no error — `CLAUDE.md`'s runfile note is load-bearing before any before/after measurement.

## Limits

**The library is all there is.** The feed shows the 500 boards per band and symmetry the fill composed and
nothing else: no seed past them, no cell but the default, and no board composed on request. That is the
author's decision rather than a gap, and `--per-band` exists for tests and local looks, not for the deploy.

**Nothing composed can be adjusted here.** There is no way to nudge a hub, re-roll one wool, or ask for the
same board a little wider. The unit of work is a whole board, and the only response to a board that is nearly
right is to make a map of it and reshape it in the Sketch tool, where each box is a shape of its own.

**Two of the four symmetries and one of the two team counts are unreachable.** The feed composes `rot_180` and
`mirror_z` at two teams. `mirror_x`, `rot_90` and every team count but two are legal at the type level and
refused at the endpoint, so the four-team board the model describes cannot be produced through this tool at
all — it is authored at the plan tier instead, which is where a four-team capture board is built.

**The composer reaches less of the shape vocabulary than the emitter builds**, and the gap is plumbing rather
than geometry. `ShapeEmitter.Emit` takes five placement knobs — a second donut attachment, a moved attachment,
an extended wool, and both scythe endpoint shifts — which `WoolBoxEmitter.Fill`, the only path the compose
pipeline uses, forwards none of (`G145`). Two families are in
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

**The mid is one band and one row of stones.** The band spans the axis flush against both fronts and carries
up to three stones, astride the axis or as a facing pair of ranks (`docs/generator/model.md` §5.13), and no
richer middle than that row. The one variation in the band itself is the split, drawn on about a third of
laterally-flipping boards and granted only where the face can host it: a granted split carries no stone, its
bay being the island, and a refused one carries the single rank its empty gap has room for.

**The drawer's hard-term list is structurally unreachable.** The browse endpoint evaluates with the same
profile the composer's acceptance gate used, so a board with a hard violation was already resampled away:
across 300 cards at twenty players, none carried one. The panel is honest but dead, and a board that reaches
the feed carries soft distance only.

**The census counts boards, not seeds.** Re-sieving the same request re-scans seeds already counted and adds
them again, so the denominator inflates across a long session of chip-toggling. The proportions stay right;
the absolute number does not, and it is the number the confidence threshold reads.

**The picture is read with a key.** Every page that shows boards draws `PlanBoardPalette.Key` beside them,
while the PNG an agent reads appends the same key under its raster (`B95`), on white paper in the light-theme
inks. A card still answers *did this compose*, never *what is this*: the grey carries no role, so a reader cannot
take a colour for an answer to the second question.
