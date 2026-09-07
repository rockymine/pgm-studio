# The board, and how it empties

`BACKLOG.md` holds 64 open entries and `TODO.md` five — the interaction slice of the programme that put a
room's building and a dressed one on one model. This document is the reading that says which of them are
defects, which are questions, which share a cause, and what order drains them. Its subject is the board
itself, and it expires when the board it describes is gone. It is the one document this work adds:
`CLAUDE.md`'s standing rule is that a change updates the document that already covers its subject rather than
growing a new one, and the board had no such document.

Every claim below is measured on the board as it stands. Where a figure the board states and a figure
measured today disagree, the measured one is here.

## Where the board stands

The two boards carry **57 open entries over 10,015 words**: a median entry of 154 words and **8 above 250** —
the length at which `CLAUDE.md` says an entry "is not a task yet and wants investigating until it is". `WE77`
is the longest at 436 words, then `G163` at 390 and `WS3` at 356. The prefix spread is `WE` 12, `TS` 9,
`B` 9, `G` 7, `S` 5, `N` 4, `C` 3, and seven others in ones and twos. `TODO.md` holds none of them: the
mapgen-authoring programme drained and no group has been pulled up in its place.

**An entry is measured from its bullet to the next bullet *or the next heading*, at any indent.** Counting to
the next bullet alone folds a section's preamble into whichever entry precedes it and inflates the long tail.
Requiring the bullet at column zero is the same fault from the other side: a stray two-space indent is a
formatting slip that costs an entry its existence in the count and hands its words to the entry above, so one
entry reads at four hundred words and the next at nothing at all. Both readings must be indent-tolerant, and
the board is kept flat. Every figure in this paragraph is retaken by:

```sh
python3 - <<'PY'
import re, pathlib, collections
rows = []
for f in ('BACKLOG.md', 'TODO.md'):
    text = pathlib.Path(f).read_text()
    for m in re.finditer(r'(?m)^\s*- \[[ ~]\] \*\*([A-Z]+\d+[a-z]?)', text):
        nxt = re.search(r'(?m)^\s*(?:- \[[ ~]\] \*\*|#{2,} )', text[m.end():])
        rows.append((m.group(1), len(text[m.start(): m.end() + nxt.start() if nxt else len(text)].split())))
lens = sorted(n for _, n in rows)
print(len(rows), 'entries,', sum(lens), 'words, median', lens[len(lens) // 2],
      '| 150-250:', sum(1 for n in lens if 150 <= n <= 250), '| over 250:', sum(1 for n in lens if n > 250))
print('prefixes:', collections.Counter(re.match(r'([A-Z]+)', i).group(1) for i, _ in rows).most_common())
print('longest:', sorted(((n, i) for i, n in rows), reverse=True)[:5])
PY
```

The board's own id discipline holds exactly. No id appears in two of the three files, none is duplicated
inside one, and none collides with a rule id served by `GET /api/rules` — the two checks `CLAUDE.md` rule 4
asks for both pass. What has decayed is not the identity of the entries but their contents, and — since the
retirement pass — what other documents say about the entries that are gone.

## Every defect on this board passes every gate the repository has

The tree is green, and it is green on every gate at once. `dotnet build` is clean over the solution.
**3,557 C# tests pass** — Vocabulary 43, Geom 298, Domain 135, Analysis 119, Minecraft 1,041, Pgm 1,277,
Export 184, Import 1, Data 40, Api 419 — with nothing failed and nothing skipped. The JS suite passes 436.
`./tools/census.sh --check` answers *census is current*. `./tools/build-scripts.sh` builds 8 of 8.

Twenty-two verified defects were live behind that when this board was first read. A core that stamps nothing
exported 200; a stacked board's paint ran down a column it should have stopped at; a degenerate polygon
cleared every sketch gate and drew no ground. The suite said none of it. Most have since landed with a test
each, and three stand below.

That is the fact the whole strategy turns on: **a defect nobody can fail on is a defect that comes back.**
The board is currently the only place these are written down, and a board entry is not a gate. So the fix
rule for the next section is one sentence — *a defect is fixed when a test fails on the old behaviour*, and
`LibrarySeedTests` is the worked example already in the tree: it pins the exact set of knobs five house
presets lose through the library, so a preset that starts losing something new fails there. One entry of 140
words did that.

## Four ways an entry leaves, and one of them keeps no record

An entry leaves the board four ways. It **ships**, and a line joins `FEATURES.md` under the id that delivered
it. It is **relocated** to a subject's `ideas.md`, where an entry with no end condition is allowed to sit. It
is **reworded down** to the slice that remains, under rule 7. Or it is **withdrawn** — deleted outright,
because the work is not wanted.

Withdrawal is what has emptied this board. **Twenty entries are gone and are in no other file**: `B21`,
`B249`, `RP63`, `B55`, `B56`, `B24e`, `G9`, `G12`, `B96`, `B154`, `B174`, `G173`, `G176`, `C12`, `C14`,
`CV15`, `B35`, `B36`, `C28` and `CV12`. Not one is an entry in `BACKLOG.md`, `TODO.md` or any `ideas.md`, and
no document names any of them. That is a fifth of the board, and it went in two commits — more than the whole
of the last programme shipped.

Withdrawal is the cheapest close there is and the board is better for it, but it is the one disposition with
**no record outside the commit message**, and that costs twice.

**A withdrawn id greps to zero, so the uniqueness check reads it as free.** `CLAUDE.md` rule 4 tests a new id
with `grep <id> TODO.md BACKLOG.md docs/generator/ideas.md` plus `GET /api/rules?rule=<id>`, and both now
answer empty for `B249` — which is exactly what a never-issued number answers. A withdrawn id is retired,
never re-issued, and the commit that removed it is the only place that says so.

**A withdrawal owes the same document sweep a ship owes.** Adding is noticed and shipping is noticed;
deleting an entry leaves prose behind that reads perfectly and points at nothing, which is the failure
*Documents rot silently* describes, arriving through the one door that section does not watch. The sweep that
answers it is the next section.

**And the sweep runs over the board, not only over `docs/`.** `CLAUDE.md` rule 2 asks for the id to be swept
across `docs/`, and `BACKLOG.md` is where an entry most often names another entry — a dependency, a twin, the
half that has to be settled first. An entry naming a shipped id as an open dependency stalls on work that is
finished, which is the direction that costs an entry rather than losing it, and no document sweep would ever
see it.

## The sweep that keeps a retired id out of the documents

`docs/` names no id that resolves to nothing. Holding that is one command, and it is the command the
withdrawal rule below is written around:

```sh
python3 - <<'PY'
import re, pathlib
board = pathlib.Path('BACKLOG.md').read_text() + pathlib.Path('TODO.md').read_text()
known  = set(re.findall(r'(?m)^\s*- \[[ ~]\] \*\*([A-Z]+\d+[a-z]?)', board))
known |= set(re.findall(r'\b([A-Z]{1,2}\d{1,3}[a-z]?)\b', pathlib.Path('FEATURES.md').read_text()))
for f in pathlib.Path('docs').rglob('ideas.md'):
    known |= set(re.findall(r'\b([A-Z]{1,2}\d{1,3}[a-z]?)\b', f.read_text()))
for f in sorted(pathlib.Path('docs').rglob('*.md')):
    if f.name == 'backlog-strategy.md': continue
    for i, line in enumerate(f.read_text().split('\n'), 1):
        for m in re.finditer(r'`([A-Z]{1,2}\d{1,3}[a-z]?)`', line):
            if m.group(1) not in known: print(f'{f}:{i}  {m.group(1)}')
PY
```

Its residue is not all debt, and reading it wants three distinctions.

**A rule id is not a task id**, and the two have the same shape. `SK11`, `TP4`, `WX1` and the `G1`–`G8` family
answer `GET /api/rules` and never appear on a board, so every one of them lands in the residue and none of
them is a fault. What separates them is the family, not the number.

**A citation for provenance is stable and stays.** `FEATURES.md` says what a commit delivered — "(from
`C12`)", "`B59` · `B21` · `B37`" — and those sentences remain true after the rest of an entry is withdrawn,
so the sweep runs over `docs/` and leaves the Done column alone. The exception is a `FEATURES.md` line that
states a **caveat** rather than a delivery: a limitation tracked by a dead id is tracked by nothing.

**The blind spot is a range.** `FEATURES.md` records shipped work as `G179–G183` and `docs/tools/sketch.md`
cites `G179`–`G181`, so `G180` and `G181` are literal tokens nowhere and read as dangling when they are not.
A range is legible to a person and invisible to a grep; where an id has to survive a sweep, it is written out.

Two directions of failure survive all three, and both have been found here. A **withdrawn** id cited as a gap
makes a document claim a limitation is tracked when nothing tracks it. A **shipped** id cited as filed makes
it claim a defect is open when it is fixed — `docs/client/canvas-interaction.md` listed the point-primitive
inconsistency as filed work under an id that had shipped, beside a bridge-wrapper entry whose id had been
withdrawn: two bullets, two opposite errors, one list. Neither is visible to a reader, and both read as
current.

## The verified defects

Confirmed by reading the code at the cited site. One stands.

**`B145` shipped** (`FEATURES.md`), and the reading it needed was not the one filed. The annotation being
skipped was half of it: a role shape is now a paint scope over ground it did not place, so a room's floor has
a shape to be stated on. The other half was that the plinth the build levels under a footprint stands *above*
the surface map the painter reads, so the courses a room actually shows were ones no pass addressed — the
levelling reports its own surface now and the world build folds it in.

**`B57` — `FeatureExtractors.Segments` → `scan_segment` applies neither of the two exclusion rules
`CleanColumns` applies, so a build-region floor sheet at `y=0` persists as solid ground and everything reading
it at query time walks on a marker. Reported with measured evidence and not re-confirmed here, because
confirming it wants a built world rather than a reading.

## The board's numbers rot faster than its prose

Nine figures were re-measured against the tree. **Seven had moved.** Six of the seven are a count or a line
number, which leaves the entry's prose true and the fix mechanical; the seventh is not, and it is the
dangerous kind.

| entry | the board says | measured today | the command that retakes it |
|---|---|---|---|
| `C51` | 28 selects, the plan tool 7 | **30**, the plan tool **10** | `grep -rc "<select" src/PgmStudio.Client --include=*.razor` |
| `B261` | 422 hand-maintained lines in `ThemeVocabulary.cs` | **542** | `wc -l src/PgmStudio.Client/Components/Terrain/ThemeVocabulary.cs` |
| `A8` | the generator is 85 files, 11.5k lines | **90 files, 13,385 lines** | `find src/PgmStudio.Pgm/{Compose,Evaluate,Shapes,Derive,Plan} -name '*.cs'` |
| `WE70` | six callers hardcode `true`; `DressingScope:216,218` | **ten** sites — `WorldBuilder:662` is a seventh in the entry's own scope; `RoomStylePreview:41` and `PieceRoom.cs:66,88` are three the entry does not discuss; `DressingScope` is at `:218,220` | `grep -rn "shellBound: true" src --include=*.cs` |
| `C62` | `components.css:989–1004`; the grep "hits only that CSS" | **985–1001**; the grep hits **202**, and a fourth site is `editor.css:756` | `grep -rn "map-author-" src/ tests/` |
| `TE2` | `ObjectivePhase.razor.cs:201`, `:211`; `.razor:56` | **`:204`**, **`:212`**; **`:55`** | `grep -n DyeColors src/PgmStudio.Client/Features/Edit/ObjectivePhase.razor.cs` |
| `RP59` | every authored board takes six calls, because the one-call path reads as a re-import | `drive.py:582` stores through **`POST /map/from-documents`**, one call, under a stated slug | `grep -n 'call("P' /media/sf_repos/pgm-studio-mapgen/tools/drive.py` |

One held: `G143`'s "handful of consumers" for the four misnamed edge lists, which is five sites. `B260`'s
count was the other way round — it named three room-style fields with no control and there were six, plus a
window's host block. `C51` has now moved on **every** reading it
has been given — 28, 29, 31, 30 — which is what a hand-maintained count does.

**`RP59` was the kind that does not survive its drift.** Its ask was sound — `flow.md` presented
`POST /map/from-documents` under *The three documents are also the way back in*, which reads as a re-import
when it is also the authoring call, and `architecture.md` said nothing else; that is what shipped
(`FEATURES.md`). But its evidence argued from a six-call path the driver had already left, so the entry read
as work that was mostly done and was worth doing for a reason it did not give. A stale count still points at
the right code; a stale premise points at nothing, and it reads exactly as convincing as a live one.

**`C62` is the instructive one on the mechanical side**, because what rotted is not the number but **the
command the entry gives for retaking it**: `grep -r "map-author"` matches `new-map-authoring.md`, cited in
comments across the tree, so the entry's own evidence line now reads 202 hits for a claim of zero. A retake
command is as much a measurement as the number it produces.

This is the same failure `project-structure.md`'s size table had before `RP8` gave it `tools/census.sh`, and
it has the same fix: **a measurement a script can retake is retaken by a script, or it is not written down.**
`B220` and `RP47` were the worked examples and shipped that way — the first ends with the four warning ids out
of `NoWarn`, so the compiler holds the line, and neither entry's count had to be re-measured because neither
entry survives.

## The causes under the entries

The board is grouped by concept, which is what makes these visible. Each cause below is one change; the
entries listed after it are what stop being separate work once it lands.

**A building is a footprint and a shell, and the studio has two models of it.** A room states one `Rect`, a
dressed `HouseProp` states `AuthoredWing[]`, and the author's ruling is that a room's building *is* the
single-wing case of the dressed one. `WE71` landed that model — one least span, one ink on all three canvases.
The interaction it makes possible is built, and the group is empty (`FEATURES.md`): the theme scope needed
no hit-test at all (`B145`), the hit-test itself with the rail it opens is `B107`, and the drag that rides on
that selection is `S25b` — a write back to the intent rather than a canvas affordance, since the sketch draws
those rectangles out of the intent and the release therefore asks rather than tells. This is what a programme
worked to its end looks like: one foundation, three entries, and the cause named before the entries were.

**A layer is first-class in the export and an afterthought everywhere else.** `DressingDoc.add` stamps the
storey; `SketchDressingInspector` has no field for it. `TerrainPainter` is handed a surface per layer and
sorts by nothing. `B263` · `B264` · `WE28` · `TS64`. The board now carries this cause as a section
preamble of its own, which is the shape a cause should reach before it is worked.

**The client mirrors the server's schema by hand.** `GET /api/terrain/patterns` answers every material kind
and field, typed, and the client keeps 422 lines of `ThemeVocabulary.cs` instead — which is why a kind added
server-side reaches no editor, why three room-style fields have no control, and why a band stack is
authorable only over HTTP. `C51`. Four of it have landed. `B260`: the six room-style fields
the editor could not reach are controls now, and the load that wrote them away goes through the one mapping
that states them all. `B200`: the Theme phase's layered form wrote a shape the reader had to carry forward,
and carrying it forward is what pinned every stack to `repeat` — it writes the wire shape now, so a ring stack
is authorable. And `B261`: the material editor reads `GET /api/terrain/patterns` for the kinds, their names,
their help, their fields and their defaults, so a kind added server-side is offered with a seed that works and
`laidLog` stopped silently becoming a stone block. And `WE54`: the biome the export writes had a route and no
control, and the bridge held no field at all, so the editor's own save dropped one written over HTTP — the
Dressing phase authors it now, from the ids `GET /api/terrain/biomes` serves. All four were the same fault in
different clothes — the client holding its own account of a shape the server publishes, or no account of it —
and `B261` is the one that removes the class rather than an instance of it.

**`PlanCompiler` fuses by surface height and drops every identity in the plan.** One `GroupBy(p => p.Surface)`
followed by a rectilinear union fuses several pieces into one shape, so a wall's seam has no two pieces left
to sit between. Fusing is deliberate — a compiled layout is a layout, not a copy of the plan — and two of the
three things that rode on it have since been paid: a shape is named for its component's first piece and the
surface it stands at rather than for where it was emitted (`TS82`, `FEATURES.md`), so a piece name does
address what it produced; and a coast is bent by the studio rather than restated beside the plan (`TS30`).
What is left is the seam itself. `B213`.

**A convention is measured and nothing complains.** Four entries in one section each name a predicate, a
corpus number and the document that would carry the rule, and none of them is a gate: a building walled in
the ground's own family (`WE46`, 9 of 50 buildings), a board wearing a theme per piece (`WE47`, 24 themes on
one board), a pattern brushed smaller than what it dresses (`WE48`), and a pattern showing off a family
rather than being a ground (`WE41`). `WE45` is the same cause from the other end — a rule that exists,
measures the wrong rectangle and asks for the widest side instead of every side. This is the largest cause on
the board and the one closest to being work, because the measurements are already taken and the shape of the
answer — a complaint pass over a themed board, with the floor stated by the author — is common to all five.

**A read answers the picture and not the knob that made it.** This is the cause the relief track has been
draining: a wall between two marks reported as terrain with nothing's name on it shipped as `WE33`, a push's
two gradients as `WE32`, and a declared route walked back as `WS14` (`FEATURES.md`). What is left of it is
`WS3` alone — `RouteFork` reports one fork where a board has several, and reports it against no demand set.
The two entries that read as this cause and were not it left the board on the reading below.

**A gate's verdict reaches the author under a name they cannot look up.** This cause is drained. It was
never that `PlanValidator` ran only at the compile gate — `/plan/evaluate` builds its context from the same
`Check` and has carried the complaints as `lint` throughout; what an author missed mid-edit was the refusals'
own sentences and ids, which the evaluator's one hard term replaced with the sentinel `STRUCT`. That shipped
as `TN2`, and `WE34` — the same rule set never run forwards — as the seat read (`FEATURES.md`); the other two
entries were withdrawn.

## The questions no reading of this repository can answer

`CLAUDE.md` is explicit that these cannot be derived — the corpus shows what authors did, the code shows what
the tool does, and neither says what is correct — so **no amount of work drains this part of the board.** It
drains in one sitting of answers, and it is the single highest-leverage move available.

**Withdrawal is a legitimate answer, and it is the cheapest one.** Five of the thirteen questions on the
previous reading left the board unanswered: `B55` (which API paths read a map as played), `RP63` (the Mojang
404 allowlist), `B249` (a per-call refusal override), `B96` (canopy share) and `B154` (the dark oak species).
Deleting the entry answers the question by declining to have it, and four of those five were questions about
work nobody was blocked on.

**Four of the mapgen-authoring five went the same way, and reading them against the code is what withdrew
them.** Each had been filed from a symptom that was real; none survived being measured.

| entry | asked for | what the code said |
|---|---|---|
| `B262` | a browser page per map over the eleven world reads | the pictures carry no coordinate frame — `TopDownRender` labels a scale and a size and never `minX`/`minZ`, while `TextGrid.Frame` rules every text read — so a gallery answers *whether* and never *where*, which is the half the author already has |
| `B265` | `--provenance <path>` on the reads that take a region directory | nothing takes a render off a shipped world that way. Every read in `pgm-studio-mapgen` goes over HTTP, where the build holds `Built.Provenance`; the CLI's own uses are `--goldens` and `--scan-out`, over a corpus that never had a sidecar |
| `B150` | a route that evaluates a stored map's sketch | `EvalContext` is a `PlanModel` and nothing else, and `BoardStructure` is piece-keyed throughout — a sketch would need a second deriver inventing a piece decomposition for polygons, not a rasterize-and-reuse. The author does not want a sketch scored |
| `B171` | how a wool approach attaches, in the shapes endpoint's terms | `/shapes/catalog` states no attachment at all, and `/plan/feasibility` — which does answer it — is a critic for *generated* boards, so a hand-drawn plan gets `producible: false` every time (`pgm-studio-mapgen/reports/opus5-coldharbour-authoring.md`) |

**Two more left by being done rather than withdrawn (2026-09-07).** `TS79` asked whether `SK18` could read a
course instead of a column, and it could: both spans were already to hand, so the rule was fixed and the entry
left with it rather than being re-filed. `WE13` was a ruling waiting to be carried out, and the catalogue map
is gone.

**A second pass of the same reading withdrew five more (author, 2026-09-07).** Three were rulings and two
were premises the code no longer holds.

| entry | why it left |
|---|---|
| `WE95`, `WE96` | ruled out. The two boards that prompted them had been asked for trees with stated features, and the agent built those out of layers; a vine on a crown is something the tree generator could state, and nothing ever asked for a mushroom under one. Checking the studio against everything an agent might do has no end |
| `WS1` | ruled off. The thin-lane case has not come up again, because no board since has been authored at that scale |
| `TS75` | both halves measurably false. The sketch draws a destroyable and a core from the intent's anchor (`sketch-render.js` `paintObjectives`), read-only by a decision stated there — *Configure is where one is edited*; and `PlanBoardScene` fans both, `B128`'s empty-`piece` case included. Rendered on the entry's own evidence board, `tallow-mirefast`'s wardstone draws at both orbit images |
| `WE79` | fixed. `PlaceStroke` seats through `DressingContext.GroundFor` and claims on the prop's own layer. Measured on two storeys — a street at y10 under a deck at y25 — a stroke naming `street` paves at **y9**, one naming nothing paves at y24, and one naming a layer the board lacks paves nothing and declines `DR-LAYER` |

What survived the same pass is `WE78`, which reads like `WE79`'s twin and is not: `B144` settles which shape
owns a column's paint, and `WE78` is how far down that shape's bands then run.

`B171`'s evidence outlived its entry. `PL13` — a bedrock wall on the wool room's own interface — is five of
the ten refusals the authored corpus carries, the commonest structural refusal there is, and it already names
its own remedy. What kept that sentence from an agent was the sentinel the evaluator answered under, and
`TN2` — the one of the five that survived the reading — shipped the fix (`FEATURES.md`).

**The sitting has happened, and it drained the whole list.** Seven questions were put to the author in one
pass and seven came back, which is what this section claimed would happen and had never been tested. What the
answers cost is worth recording, because it is not what the board's shape suggested:

| question | answer | what it left |
|---|---|---|
| `C11` — is the Edit tool kept? | **no** | the entry withdrawn, and `TE3` — retire the tool — filed in its place. `TE2` went with it |
| `WE28` — how is a relief keyed? | **layer + island** | work, with the reason: it is the reading that lets a relief be solved on a storey *under* the board |
| `WE48` — the floor on a brush? | **2** | work. A guard against a brush finer than the blocks it paints, not a style rule — every board on the shelf passes |
| `B144` — height against paint? | **delegated** | ruled below, on `TS23`'s precedent |
| `B70` — which view on a card? | **the section, as it is** | the entry withdrawn: an author knows a house by name, and the one worth looking at is a click from a 3-D view |
| `WE13` — is a catalogue a map? | **no** | and the catalogue itself was not wanted either, so it went rather than gaining an exemption (`FEATURES.md`) |
| `A8` — where does `PlanCompiler` belong? | **not yet** | deferred, with the reason stated: the answer is not known, and it is the one thing the split turns on |

`TODO.md`'s eighth — whether a building's two ceilings should be one number — came back the same way: they
stay apart for now, deliberately.

**Three of the seven were answered by declining to have the question**, which is the pattern the withdrawal
section already names: `C11` and `B70` left the board, and `WE13` turned into a question about whether to keep
the thing at all. Two became work with a stated reason. Two were deferred with the reason for deferring. Not
one of them needed more measurement, which is the whole claim this section makes.

**`B144` was handed back, and the ruling is: paint follows the shape that forms the surface** (shipped, `FEATURES.md`)**.** Among the shapes
covering a column, only those reaching the visible top may own its paint; among those the smallest area still
wins. Patch-scoping is untouched — two shapes at one height are a theme scoped to a patch — and a shape running
*under* another stops painting a surface it does not form, which is the whole of the defect. It is `TS23`'s
rule read within a layer rather than across layers: each surface shows its own.

Two entries carry an answer in their prose and still read as open, which costs a reader the same as an
unanswered one. `WE41` opens *"Parked on a ruling: nothing is built until one is chosen"* and then states the
predicate the author chose — two members of a `TerrainPalette` family, a voronoi in the fill and made of
stone, a field pattern near shades of one ground. `TS63` asks which forms earn a place and then names them:
the arch, the ziggurat, the ellipse wall, the tapered tower and the domed roof are wanted, the amphitheatre
and the colonnade are not. Both are work; their entries want the parked sentence taken off.

And `S47` is a decision of scheduling rather than of gameplay: what a pressure budget is, which the entry
itself says needs labelled bad maps rather than more measurement.

## Three different things live in one file

`BACKLOG.md` is a Kanban *Later* column, and three populations inside it do not behave alike. They do not
partition it — a third of the entries are cleanup, naming and consistency work that is none of the three —
but each of the three wants a different treatment, which is what makes them worth naming despite the
ambiguity at their edges.

**Blocked decisions** are the twelve questions above, plus `S47`, which waits on material rather than on a
ruling. Each has a definite end and none of it is work until the answer arrives.

**Reach gaps** — the backend exists and the browser cannot say it: `B261`, `B263`, `B264`,
`N08`, `N12`, `TS64`, `S59`, `B44`. Four more — `B107`, `S25b`, `B145`, `WE54` — were
this population until the building's one model turned them from three reaches into one, and all four have
since shipped. That is the reading's own claim tested: a reach gap is drained by naming the cause under it,
not by working the entries in turn. This is the population the studio's own shape produces, and every one of them is the same thing: a
document, a route or a solver that already answers, and a surface that never asks.

**A roadmap** — capabilities the studio does not have and nobody is blocked on: `S46`,
`S34`, `TS51`, `TS63`, `G164`, `B54`, `B9`, `B58`. `G187` left this population by being read against the
code rather than built: plan-tier flow is served and `Cells.WaysRound` is in use, so what its entry called a
project is one missing primitive and one term. Four left it by shipping —
`B221` and `B258`, the library's pictures, `WE34`, the seat read, and `TS30`, the bend — which is the
population behaving as intended: a roadmap entry is drained by being built, not by being triaged again.
`WE52`, the drawn biome patch, left it the other way: withdrawn on the author's call once the field became a
library row, since an area drawn per board is not what a named, reusable pattern is for.

**Relocating the roadmap to the ideas files was tried and refused.** Seventeen of the eighteen entries named
on the previous reading are still on the board; only `B21` left, and it left by withdrawal rather than by
relocation. What went instead was the hygiene population — `C12`, `C14`, `CV12`, `CV15`, `B35`, `B36`, `C28`
— which the previous reading had scheduled as a phase to *gate* rather than to delete.

The reorganisation that followed supersedes the relocation and is the better answer. Each roadmap entry now
sits under the foundation it spends rather than in a bucket of its own, so `S46` reads beside the relief
entries whose model it needs and `G187` beside the walk it would run on. A capability with no end condition is
legible when it is filed against the thing that would make it possible, and unreadable in a list of its peers.
The cost is that a section's length no longer says how much *work* is in it, which is what the split above is
for.

## The order the board empties in

**Phase 0 — ask, then cut. Done, and it worked.** Seven questions went to the author in one pass and seven came
back: three by declining to have the question, two as work with a stated reason, two deferred with the reason
for deferring. It cost one message. This phase is the one nobody can do alone, and it is the one to run first
whenever the list refills — the causes below each had a question sitting in them, and none of them could move
until it was answered.

**Phase 1 — the verified defect run.** Drained but for `B57`, which is blocked in practice by re-import.
Each lands with a test that fails on the old behaviour — that is the deliverable, not the fix. `B145` left
this way (`FEATURES.md`), pinned at three tiers: the levelling reporting the surface it leaves, the painter
finishing a plinth folded into its map, and a built room's floor course carrying no raw stone. `TS74` and `B144` left this way (`FEATURES.md`), and `TS31`
left it by being withdrawn: the ruling it needed says a sketch cannot judge reachability at all, so the
reading it wanted shipped as `WS61` (`FEATURES.md`) at the tier that holds the build zones — and as
information rather than as a defect, which is the disposition the whole question turned on.

**Phase 2 — fix the causes, not the entries.** The building's one model (3 entries) was `TODO.md`'s programme
and is drained (`FEATURES.md`); the mapgen-authoring group it rode beside is what `TODO.md` now holds alone.
Then, in the order that maximises what each closes: the measured convention with no complaint (5), the read
that reports a symptom (6), the layer word (5), the client reading its own schema (5), the compiler's lost
identity (3), the live findings feed (2). This is `CLAUDE.md`'s own doctrine — *"the board is emptied concept
by concept"* — applied to groups the board has already named, and the building programme is the worked
example: one foundation named, three entries, each small because the cause was settled first.

**The rule over Phases 1 and 2.** A programme pulled up from `BACKLOG.md` is worked to its end before
anything above interrupts it, except a Phase 1 defect in the surface it is building. The board runs one
programme at a time and says at the top which one, so the phase order above is what to pull up next rather
than a queue that runs beside the one already open.

**Phase 3 — put the surviving measurements under a script.** `C51`, `B261`, `A8`, `WE70`, `C62` and `TE2` each
carry a count or a line number that has drifted, and `C62` carries a retake command that no longer measures
its own claim. Where the number is load-bearing it earns a `census.sh`-shaped generator; where it is not, it
comes out of the entry and the prose stands alone. `RP59` was not part of this phase — what drifted there was
its premise, so it was written rather than rewritten (`FEATURES.md`).

## What keeps it empty

Five rules, each of which the repository already believes and none of which it enforces.

**Nothing is filed without a reproduction.** An entry that cannot say what fails, at which coordinates, is the
five-paragraph entry `CLAUDE.md` rule 10 warns about. Eight entries are over 250 words, down from twelve, and
the longest is now 390 words rather than 1,068 — the retirement pass took the tail off the distribution
rather than trimming the entries in it, which is the faster of the two moves and the one that needs the
author.

**Nothing is closed without a test.** The verified defects above are invisible to a suite of 3,557 tests.
`LibrarySeedTests` is the shape: pin what is wrong, so it fails when it changes in either direction.

**No measurement is written by hand.** Seven of nine re-measured figures had drifted, one entry's retake
*command* had drifted with them, and one entry's whole premise had. `census.sh` is the precedent.

**A question is asked before the entry is filed, not after.** The oracle rule already says this. No entry now
carries a question nobody has answered, and each has been costing the board's readability ever since. The house
group is what one sitting of answers looks like: five entries, of which one became a clamp, three were
withdrawn outright, and one turned into the surface the other four had been standing in the way of.

**A withdrawal is a close, and it carries a close's obligations.** `CLAUDE.md` rule 2 spells out what shipping
owes — a line in `FEATURES.md`, and every document naming the id as a gap fixed in the same commit — and the
same grep is owed when an entry is deleted instead, which is the door the last pass came through. The id is
retired and never re-issued, and the commit that removed it is the only record that it existed, so the commit
message names every id it withdraws. **And the grep runs over the board, not only over `docs/`**: an entry
naming another entry is the commonest citation there is, and it is the one the rule as written does not
cover.

**This document is re-measured when a programme drains, not when someone notices.** Every figure in it is a
hand-taken reading of a board that moves under it, so it decays exactly the way the entries in *The board's
numbers rot faster than its prose* do — and for the same reason, since it is the same kind of writing. A
programme finishing is what changes the counts, the causes and which phase is next all at once, and so is a
retirement pass, which changes them further and faster.
