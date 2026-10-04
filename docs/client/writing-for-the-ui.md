# Writing for the UI

The studio's interface is read by a mapmaker, not by someone who knows the code. Every word on screen — a
button, a heading, a description, a tooltip, a status line, an error — is written for that reader. This
document is the standard the copy is held to; `ui-conventions.md` says *where* a sentence goes (an option's
note, a readout, a section description), and this says *how it is written*.

The documents under `docs/` are written in a different voice on purpose. They argue for a design and are read
start to finish, so they can afford long sentences, defined terms and the codebase's own vocabulary. The
interface is scanned in a second, by someone in the middle of a task. Copy that reads like the docs is the
most common fault in the client, and the first thing a new user notices.

## The rules

**Say it the way a person would say it.** Write complete, ordinary sentences: subject, verb, object. Do not
write a noun phrase and hang the rest of the meaning off a dash. *"Every map that holds a plan — including
ones already built and configured."* is a label pretending to be a sentence; *"Maps that have a plan,
including finished ones."* is a sentence.

**Cut what the control already says.** A button labelled *Export* needs no tooltip saying *Export this*. A
section headed *Spawns* needs no description saying it holds the spawns. If deleting a sentence loses
nothing the reader needs to act, delete it. Most section descriptions should not exist.

**One idea per sentence, one or two sentences per block.** A description under a heading is at most two
short sentences (about 25 words). A tooltip is a few words, or one sentence. Anything longer is
documentation and belongs in `docs/` or a help page, not on the panel.

**Use the reader's words, not the code's.** Never put an internal name on screen. The table below maps
the common ones; when in doubt, describe what the thing *does* for the map.

| Do not write | Write |
|---|---|
| composer, pipeline, emitter, emit, emitted | the generator, generate, generated |
| board (meaning the arrangement) | layout |
| seed (meaning a plan) | plan |
| intent | game settings (teams, spawns, objectives) |
| rasterize, realize | build |
| evaluator, gate, refusal, finding | check, problem, issue, "can't … because …" |
| knob | setting |
| descriptor | layout code, JSON |
| bucket (of a theme) | part (rim, wall, surface, fill) |
| style, theme, relief, dressing | pattern, palette, terraform, decoration |
| mouth-up | with its opening at the top |
| `BoxFiller`, `PlanModel`, any type or class name | nothing; describe the behaviour |
| HTTP status codes in the main message | the problem in words; the code may follow in brackets |

**No em dashes.** Use a period, a comma, a colon or brackets. Two sentences are almost always clearer than
one sentence joined by a dash. Ranges use an en dash (`2–4`), and a separator in a compact readout uses a
middle dot (`12 blocks · 3 islands`).

**Use the Oxford comma.** *Pieces, zones, and objectives.*

**No metaphor, no personification, no wit.** *"A refusal is an answer, not an error — it is the emitter's
own words"* asks the reader to decode a figure of speech before they learn anything. Say what happens:
*"If the shape can't be built at this size, the reason is shown here."*

**Don't hard-code counts into prose.** *"Six libraries, in the order they compose"* is wrong the day a
seventh is added. Show the count as data (a badge) or leave it out.

**Headings are names.** Sentence case, no trailing punctuation, no commas, ideally one to three words:
*Libraries*, *Spawn points*, *Change history*.

**Buttons are verbs.** Sentence case, one to three words, naming the action: *Save*, *New plan*,
*Copy JSON*, *Start a map*. Never a sentence. A busy button names what it is doing: *Saving…*.

**Status messages are short and past tense.** *Saved.* *Couldn't save. Try again.* A status line does not
explain consequences unless the reader must act on them: *"Saved. Every theme binding it now paints this."*
becomes *"Saved. Palettes using this pattern are updated."*

**Errors say what happened and what to do.** *"Couldn't load the catalog. Reload the page to try again."*
Not *"compile failed (HTTP 500). {body}"*. Technical detail (status code, server text) may follow the
sentence, never replace it.

**Empty states say what is missing and how to add it.** *"No plans yet. Create one to get started."*

**Tooltips name, then add the shortcut.** *"Pan (H)"*, *"Zoom to fit (F)"*. A tooltip on an icon-only
button is required; a tooltip that repeats a visible label is not.

**Placeholders show an example, not an instruction.** *"e.g. Harbour Run"*, not *"Type a name here"*.

**Address the reader as "you" only when it helps; never "we".** Most UI copy needs neither: *"Draw the
islands, then build the world"* reads better than *"You can draw the islands and then we'll build the
world"*.

**Spell numbers and units the same way everywhere.** Digits for quantities (*3 islands*), `×` for
dimensions (*20 × 30 blocks*), and the unit after the number with a space.

## Before and after

These come from a reviewer reading the studio for the first time.

| Before | After |
|---|---|
| Every map that holds a plan — including ones already built and configured. Open one to keep planning. | Maps that have a plan, including finished ones. |
| Every shape the pipeline can put in a box, emitted once and drawn mouth-up. | Every shape the generator can place, drawn with its opening at the top. |
| Saved. Every theme binding it now paints this. | Saved. Palettes and buildings that use this pattern are updated. |
| Emitted through BoxFiller, so the profile check and the docking gate run exactly as they do in composition. A refusal is an answer, not an error — it is the emitter's own words. | Generated with the same checks the generator uses. If the shape can't be built at this size, the reason is shown here. |
| Six libraries, in the order they compose | Libraries |
| Author a coarse cell-grid seed — pieces, zones, symmetry and objectives — and compile it straight into a sketch draft. | Block out a map on a grid: its areas, symmetry, and objectives. Then build it into a sketch. |

## Terms a new user has to learn

Some words are the vocabulary of mapmaking itself and cannot be avoided: a hub, a front line, a wool
room. These are not jargon to be removed but terms to be defined, once, in the same words everywhere. Each
term has two levels. Its **tooltip** is one line, shown where the word first appears in a tool. Its **guide
entry** is the longer account a help page gives, and only some terms need one.

**The definitions live in one place: `Glossary` in `PgmStudio.Vocabulary`, served at `GET /api/glossary`.**
Every word the rules, the findings and the screens use is a term there, with its one-line definition, the
other names a reader may meet it under (an older word, a field's name, PGM's own name) and the terms its
definition leans on. `?term=board` answers `layout`: a word is found by its own name or by another one. A rule
or a tooltip uses these words and never explains one, and a new word joins the glossary before it is used.

Four terms replace the studio's earlier words, which mapmakers do not use, and the glossary keeps each old word as another name for its term: *pattern* for style, *palette*
for theme, *terraform* for relief, and *decoration* for dressing. The old words remain in the code and the
`docs/`; the client shows only the new ones, and text the server writes takes the same pass in `RP113` and `RP114`.

### Guide entries

**Plan.** The plan is the most abstract layout of a map, and the studio writes the map's XML from it. Once the
plan is done, the map is already playable, only flat. A smaller cell size gives a finer plan. A map built
around terrain can stay flat here and get its shape in the sketch; a map built around geometry sets its base
heights in the plan and is only painted in the sketch.

**Sketch.** A sketch is built from the bottom up, in layers: the ground, then its shape, then its paint, then
what stands on it, which differs a little from building in game. The map can be previewed from a player's eye
at any point. Block-level edits are possible but fiddly by hand, so most of the work is shaping larger areas.
Some details are best finished in game; the sketch is for iterating fast.

**Configure.** A map made in the studio is configured automatically and is ready to play once its sketch is
done. Configure is for a map built elsewhere, such as on a mapmaking server: import the world, answer a few
questions, and the studio detects and writes the rest.

**Library.** The library holds what is reused: block patterns and the palettes that give a map its look,
schematics such as hand-cut trees, brushes that generate boulders and trees from a few settings, and parts
that build houses. The same entry always gives the same result, and a map built this way records which
patterns it uses, so anyone can see how it was made.

**Cell.** Planning on cells is laying out a map on squared paper, except that every square has a meaning.
Cells keep sizes easy to judge and parts aligned; they carry the big shapes, and the sketch carries exact
positions. A plan is blocking only: one cell size everywhere makes every gap the same width, and the map can
look gridded, which a finer grid or the sketch breaks up. A grid that is too fine turns into editing blocks.
The default of 4 suits the common Capture the Wool lane widths of 8, 12, 16, and 20 blocks. Destroy the
Monument maps usually need less detail in a plan, since they are more about terrain than routes.

**Symmetry.** Only one part of the map is designed, a half or a quarter, and the studio copies and rotates the
rest. Game settings work the same way: one team is set up and the others follow. An imported world has its
symmetry detected.

**Wool room.** The other team has to reach the room and take the wool. Taking it is a *touch*; placing it on
their monument is a *capture*.

**Generator.** The generator knows how a map's parts relate: hubs, approaches, wool rooms, and the gap
between the teams. It arranges them into layouts that follow those rules.

**Palette.** A palette sets a map's look, such as a desert or a snowy cliff. Builders already call a set of
blocks used together a block palette.

A term that is the studio's own invention (an *approach family*, a *body form*, a *profile check*) is either
replaced by a plain description or kept off the screen. If a reader needs it, it needs a definition here
first.

## Checking copy

Before a change to client copy lands:

- Read every changed string aloud. If it does not sound like something a person would say, rewrite it.
- `grep -rn ' — ' src/PgmStudio.Client --include=*.razor` should not grow.
- Search `tests/e2e/` and `docs/` for any visible string that changed, and update them in the same commit.
