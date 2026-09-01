# The board, and how it empties

`BACKLOG.md` holds 99 open entries and `TODO.md` four — the programme that pulls a room's building and a
dressed one into one model. This document is the reading that says which of them are defects, which are
questions, which share a cause, and what order drains them. Its subject is the board
itself, and it expires when the board it describes is gone. It is the one document this work adds:
`CLAUDE.md`'s standing rule is that a change updates the document that already covers its subject rather than
growing a new one, and the board had no such document.

Every claim below was measured on the board as it stands, not taken from an earlier reading of it. Where a
figure the board states and a figure measured today disagree, the measured one is here and the entry has been
corrected to match.

## Where the board stands

The two boards carry **103 open entries over 17,597 words**: a median entry of 149 words, 37 between 150 and
250, and **12 above 250** — the length at which `CLAUDE.md` says an entry "is not a task yet and wants
investigating until it is". `B21` alone is 1,068 words, `B249` 504, `WS3` 356 and `G154` 354. The prefix
spread is `B` 35, `WE` 16, `G` 12, `TS` 9, `S` 8, `C` 7, and seven others in single figures.

**An entry is measured from its bullet to the next bullet *or the next heading*.** Counting to the next
bullet alone folds a section's preamble into whichever entry precedes it and inflates the long tail: the
same board reads as 18 entries over 250 words rather than 12, and puts three entries on the longest list that
do not belong there. Every figure in this paragraph is retaken by:

```sh
python3 - <<'PY'
import re, pathlib, collections
rows = []
for f in ('BACKLOG.md', 'TODO.md'):
    text = pathlib.Path(f).read_text()
    for m in re.finditer(r'(?m)^- \[[ ~]\] \*\*([A-Z]+\d+[a-z]?)', text):
        nxt = re.search(r'(?m)^(?:- \[[ ~]\] \*\*|#{2,} )', text[m.end():])
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
asks for both pass. What has decayed is not the identity of the entries but their contents.

## Every defect on this board passes every gate the repository has

The tree is green, and it is green on every gate at once. `dotnet build` is clean over the solution.
**3,153 C# tests pass** — Vocabulary 42, Geom 260, Domain 113, Analysis 115, Minecraft 919, Pgm 1,184,
Export 127, Import 1, Data 40, Api 352 — with nothing failed and nothing skipped. The JS suite passes 413.
`./tools/census.sh --check` answers *census is current*. `./tools/build-scripts.sh` builds 7 of 7.

Twenty-two verified defects were live behind that when this was read. A core that stamps nothing exported
200; a stacked board's paint ran down a column it should have stopped at; a degenerate polygon cleared every
sketch gate and drew no ground. The suite said none of it.

Most have since landed with a test each, one was withdrawn on the author's ruling (`TS32`: fusing pieces by
surface is what the compiler is for), and what stands below is the one that remains — `FEATURES.md` carries
the rest under the ids that delivered them.

That is the fact the whole strategy turns on: **a defect nobody can fail on is a defect that comes back.**
The board is currently the only place these are written down, and a board entry is not a gate. So the fix
rule for everything in the next section is one sentence — *a defect is fixed when a test fails on the old
behaviour*, and `TL12` is the worked example already in the tree. `LibrarySeedTests` pins the exact set of
knobs five house presets lose through the library, so a preset that starts losing something new fails there.
One entry of 140 does this.

## The verified defects

Confirmed by reading the code at the cited site. One is left of the twenty-two, and it is the one whose filed
fix is contradicted by the code it would change: **`TS31`** — `SketchRasterizer.DetachedMasses`
(`SketchRasterizer.cs:978`) drops any component sharing no column with a second one, so it reports a storey
whose stair was never drawn and never an island standing *beside* the board, which is the case an author
actually draws by accident. Fixing it as filed catches the first and still misses the second, and whether a
mass beside the board is a fault at all is the author's to say — `SK11` already complains about ground
standing *over* other ground and stays silent for one merely beside it.

Three more are reported with measured evidence and were not re-confirmed here, because confirming them wants
a built world rather than a reading: `B145` (a spawn or wool piece's interior is unthemed), `B154`
(`species: "dark_oak"` selects the right template and the wrong material), and `B57` (`scan_segment` counts a
build-region marker as solid ground).

## The board's numbers rot faster than its prose

Six figures were re-measured against the tree. Every one had moved, and four of the six had moved the wrong
way. Four of the entries have since been fixed and carried their corrected figures out with them; the two
that remain are here:

| entry | the board says | measured today | the command that retakes it |
|---|---|---|---|
| `C51` | 29 hand-rolled selects | **31** | `grep -rn "<select" src/PgmStudio.Client --include=*.razor \| wc -l` |
| `CV12` | 55 studio modules, 12,694 lines | **59 modules, 16,198 lines** | `find src/PgmStudio.Client/wwwroot/js/studio -name '*.js' \| wc -l` |

Both have moved **again** since they were last corrected here — `C51` from 28 to 31 and `CV12` from 16,013
lines to 16,198 — in the time it took to run one programme. The commands are given so the next reader retakes
them rather than trusting these, which is the whole of the point below.

The prose in each of those entries is still true; only the quantities lie. That is the same failure
`project-structure.md`'s size table had before `RP8` gave it `tools/census.sh`, and it has the same fix:
**a measurement a script can retake is retaken by a script, or it is not written down.** `B220` and `RP47`
were the worked examples and shipped that way: the first ends with the four warning ids out of `NoWarn`, so
the compiler holds the line, and neither entry's count had to be re-measured because neither entry survives.

## Six causes under twenty entries

The board is already grouped by concept, which is what made these visible. Each cause below is one change;
the entries listed after it are what stop being separate work once it lands.

**`PlanCompiler` fuses by surface height and drops every identity in the plan.** One `GroupBy(p => p.Surface)`
followed by a rectilinear union produces `s0`, `s1`, `s2` — so a piece name cannot address anything
downstream, a wall's seam has no two pieces left to sit between, and a coast has to be restated beside the
plan that already described it. Fusing is deliberate — a compiled layout is a layout, not a copy of the plan
— so what rides on it is the seam a wall needs and the addressing four of `drive.py`'s fourteen keys
(`themeByHeight`, `themeById`, both `shapeProps`) exist to work around. `B213` · `TS30` · `B107`.

**A layer is first-class in the export and an afterthought everywhere else.** `DressingDoc.add` stamps the
storey; `SketchDressingInspector` has no field for it. `TerrainPainter` is handed a surface per layer and
sorts by nothing. `B263` · `B264` · `WE28` · `TS64` · `B107`. `TN7` and `WE30` were this cause and have
landed.

**A gate's verdict exists only at the compile boundary.** `PlanValidator` runs at compile and not in the live
inspect feed, so an author sets a number, sees nothing, and meets the refusal a phase later. `TN2` · `G163` ·
`WE34` · `B249`. `TN6` and `B79` were the surfaces around that boundary misleading in their own way and have
landed: the plan's compile controls now read the operation rather than the map, which is one cause and not
two — `B79`'s race is how `TN6`'s state was reachable at all.

**A term measures the artifact it can reach rather than the one the claim is about.** `G8` measures the plan
where the ground is in the sketch; the top-down frames on every column where the height profile frames on
ground. `B96` and `B150` are what is left of it — the second because every evaluator caller is plan-tier by
construction, so the sketch's own ground is never the thing scored. `G231`, `B103`, `B143` and `WS17` were
this cause and have landed: each moved a term onto the artifact its claim is about rather than the one
nearest to hand.

**A building is a footprint and a shell, and the studio has two models of it.** A room states one `Rect`, a
dressed `HouseProp` states `AuthoredWing[]` — so a room is the single-wing case, and the author's ruling is
that it *is* one: a footprint on a wool or a spawn is a special case of the building an author draws in the
Dressing phase. Nothing in the code says so. A wing is refused under 3 blocks across, a room under 4, a
shelled room under 6; the covered area is capped at 192 cells against `ST9`'s 400; and two renderers draw
them, one filled, ghosted and selectable, the other dashed and locked. `WE71` is that change, and `B107`,
`S25b` and `B145` are what stop being separate work once a room's building is drawn and hit-tested by the
code that already does both for a prop.

Three docstrings assert the opposite in prose, which is the tell `CLAUDE.md` names: `HouseProp`'s *"nothing
else is shared, and the difference is worth stating"*, `MaxFootprint`'s *"nothing a dressing limit has any
business refusing"*, and `decoration.md` §8's *"the two share a stamper and nothing else"* — the last of
which opens by conceding that the stamper knows nothing about where a footprint came from.

**The client mirrors the server's schema by hand.** `GET /api/terrain/patterns` answers every material kind
and field, typed, and the client keeps 422 lines of `ThemeVocabulary.cs` instead — which is why a kind added
server-side reaches no editor, why three room-style fields have no control, and why a band stack is
authorable only over HTTP. `B261` · `B260` · `B200` · `WE54` · `C51`.

## The questions no reading of this repository can answer

Thirty-one entries name a ruling or a decision in their own text, and 13 of those still carry the
question unanswered. `CLAUDE.md` is explicit that these cannot be derived — the corpus shows what authors
did, the code shows what the tool does, and neither says what is correct — so **no amount of work drains this
part of the board.** It drains in one sitting of answers, and it is the single highest-leverage move
available.

The questions, stated so they can be answered in a line each:

1. `TS49` — should a prop recipe be library material, and for which kinds (trees, boulders, paths, water)?
2. `TS50` — what may a placement say on its own, once recipes are rows? (follows `TS49`)
3. `WE28` — should a relief be keyed by layer *plus* island, or ride on the layer that carries it?
4. `TS63` — is the wanted form list final at arch, ziggurat, ellipse wall, tapered tower and domed roof?
5. `B144` — how do height and paint resolve an overlap, where one takes the taller shape and the other the
   smaller?
6. `WE2` — should a roof's eave descend by `pitch` at all?
7. `B225` — does a course marching under a neighbour's verge stop at the verge or at the wall?
8. `B55` — which API paths read a map *as played*, and which read it as written?
9. `A8` — does `PlanCompiler` belong to the generator or to authoring? (gates the `PgmStudio.Compose` split)
10. `B70` — which view should a library card carry?
11. `WE13` — does a catalogue map move its wools onto one plot, or is a catalogue exempt from `EX1`?
12. `RP63` — a route-scoped allowlist entry for the Mojang 404, or stop the smoke check naming a player?
13. `B92` — does a house fill respect the storey stack, and how deep behind an opening does it start?

Two more are decisions of scheduling rather than of gameplay and can be taken without the oracle: `B249`
(whether a per-call refusal override is wanted at all) and `S47` (what a pressure budget is, which the entry
itself says needs labelled bad maps rather than more measurement).

## Three different things live in one file

`BACKLOG.md` is a Kanban *Later* column holding three populations that do not behave alike:

- **Defects and blocked decisions** — the two sections above, about 45 entries once the two that appear in
  both are counted once. These genuinely belong on a board, because each has a definite end.
- **Reach gaps** — the backend exists and the browser cannot say it. `B261`, `B260`, `B263`, `B264`,
  `B200`, `WE54`, `N08`, `TS64`. Three more — `B107`, `S25b`, `B145` — were of this population until the
  building's one model turned them from three reaches into one; they are on `TODO.md` with `WE71`.
- **A roadmap** — `B21` (an MCP head, 1,070 words), `B262`, `B258`, `B221`, `S46`, `S56`, `S60`, `S47`,
  `S34`, `TS51`, `TS63`, `G187`, `G164`, `G178`, `B92`, `B54`, `B9`, `B265`, `WE34`. Nineteen
  entries — describing capabilities the studio does not have and nobody is blocked on. They have no end
  condition, and they are why the board reads as unemptiable.

`CLAUDE.md` rule 1 already provides for the third population — *"tasks that are retired for the moment can
live inside `docs/<project>/ideas.md`"* — and `docs/generator/ideas.md` and `docs/world-export/ideas.md` both
exist and are used. Moving the roadmap there breaks no id and abandons no task, and it takes a fifth of the
board's weight off a column whose job is to say what is next.

## The order the board empties in

**Phase 0 — ask, then cut.** The thirteen unanswered questions, in one sitting. Each answer either turns its
entry into work small enough to state in a paragraph or withdraws it. Nothing else on this list is worth
starting first, because five of the causes in the section above have a question sitting in them. It is the
one phase nobody can do alone, and it has not been done.

**Phase 1 — the verified defect run.** The confirmed defects, in the order they are tabled above: the wrong
verdicts first, then the studio's own faults, then the record. Each lands with a test that fails on the old
behaviour — that is the deliverable, not the fix, and the tests the landed fixes carry were each run red
against the unfixed source before being trusted, with one stated exception: `C45`/`TC2` added types that did
not exist, so their tests cannot be run against a tree without them, and the guarantee they pin — that a name
no account could carry reaches no request — is one the code could not previously make. What is left of the
run is `TS31` (whose filed fix is contradicted by a measurement in the code it would change) and the three
measurement entries that belong to Phase 4.

**Phase 2 — fix the causes, not the entries.** The six foundations, each in the order that maximises what it
closes: the building's one model (4 entries), the compiler's lost identity (3), the layer word (5), the live
findings feed (4), the term's subject (2), and the client reading its own schema (5). This is `CLAUDE.md`'s
own doctrine — *"the board is emptied concept by concept"* — applied to groups it has already named.

**The building's one model is first among them, and is `TODO.md`'s current programme.** The split just
finished is what makes it small: a room's region and its building are separate rectangles on the intent, on
the plan canvas and in the sketch alike, so *what* a drag would write back is already settled. Once a room's
building is the prop it is a special case of, selection, the drag and the theme scope are three things one
hit-test unlocks rather than three features to build.

**Phase 3 — whatever `TODO.md` holds.** It currently holds Phase 2's first foundation, pulled up early
because the split that preceded it is what made the entries small. A programme pulled up from `BACKLOG.md` is worked to its end before
anything above interrupts it, except a Phase 1 defect in the surface it is building. The board runs one
programme at a time and says at the top which one.

**Phase 4 — gate the hygiene so it stops returning.** `B220` and `RP47` are done and were done this way:
the four warning ids are out of `NoWarn`, so the compiler refuses the next doc comment pointing at something
that is not there rather than silencing it. What is left of the phase is the comment-id sweep, which ends
with its grep in the same place `census.sh --check` sits, and `C51`/`C12`, which end by adopting the
component or deleting it — a component that exists and is unadopted is the drift, not the markup.

**Phase 5 — the roadmap, moved out.** Not emptied: relocated to the ideas files, where an entry with no end
condition is allowed to sit.

## What keeps it empty

Four rules, each of which the repository already believes and none of which it enforces:

**Nothing is filed without a reproduction.** An entry that cannot say what fails, at which coordinates, is
the five-paragraph entry `CLAUDE.md` rule 10 warns about. Twelve entries are over 250 words today, one fewer
than when this was written, and `B21` is a third of the excess by itself.

**Nothing is closed without a test.** The verified defects above are all invisible to a suite of 3,153 tests.
`LibrarySeedTests` is the shape: pin what is wrong, so it fails when it changes in either direction.

**No measurement is written by hand.** Six of six re-measured figures had drifted. `census.sh` is the
precedent and there should be three more scripts like it.

**A question is asked before the entry is filed, not after.** The oracle rule already says this. Thirteen
entries are on the board carrying a question nobody has answered, and each of them has been costing the
board's readability ever since.

**This document is re-measured when a programme drains, not when someone notices.** Every figure in it is a
hand-taken reading of a board that moves under it, so it decays exactly the way the entries in *The board's
numbers rot faster than its prose* do — and for the same reason, since it is the same kind of writing. A
programme finishing is what changes the counts, the causes and which phase is next all at once, so that is
the moment: the `spawn`/`wool-room` split closed six entries and left this document naming two of them as
open work and a `TODO.md` programme that had already been replaced twice.
