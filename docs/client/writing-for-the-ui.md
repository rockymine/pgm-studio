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
*Copy JSON*, *Open in plan editor*. Never a sentence. A busy button names what it is doing: *Saving…*.

**Status messages are short and past tense.** *Saved.* *Couldn't save. Try again.* A status line does not
explain consequences unless the reader must act on them: *"Saved. Every theme binding it now paints this."*
becomes *"Saved. Themes using this style are updated."*

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
| Saved. Every theme binding it now paints this. | Saved. Themes using this style are updated. |
| Emitted through BoxFiller, so the profile check and the docking gate run exactly as they do in composition. A refusal is an answer, not an error — it is the emitter's own words. | Generated with the same checks the generator uses. If the shape can't be built at this size, the reason is shown here. |
| Six libraries, in the order they compose | Libraries |
| A placement names this recipe. Retuning it retunes every boulder wearing it. | Changes apply to every boulder that uses this recipe. |
| Author a coarse cell-grid seed — pieces, zones, symmetry and objectives — and compile it straight into a sketch draft. | Block out a map on a grid: its areas, symmetry, and objectives. Then build it into a sketch. |

## Terms a new user has to learn

Some words are the vocabulary of mapmaking itself and cannot be avoided: a hub, a front line, a wool
room. These are not jargon to be removed but terms to be defined, once, in the same words everywhere. Where
one appears on screen for the first time in a tool, it carries a tooltip with its definition below.

| Term | Means |
|---|---|
| **Plan** | A map blocked out on a coarse grid: which areas exist, what each is for, and where the objectives sit. |
| **Sketch** | The map's ground at block scale: island outlines, heights, terrain paint, and props. |
| **Configure** | Setting up the game on a built world: teams, spawns, protected areas, and objectives. |
| **Library** | Reusable materials and buildings — styles, themes, houses, trees — shared by every map. |
| **Cell** | One square of the plan grid. Its size in blocks is set per plan. |
| **Symmetry** | How one team's half is copied to make the other's: rotated or mirrored. |
| **Hub** | The central area of a team's side, which the spawn, the wool approaches, and the front line connect to. |
| **Front line** | The edge of a team's land that faces the enemy across the gap. Players build bridges from here. |
| **Mid** | The open gap between the teams' front lines, where players build to cross. |
| **Approach** | The path of land leading from a team's side to a wool room. |
| **Wool room** | The room a wool is kept in. The other team has to reach it to take the wool. |
| **Monument** | Where a team places a captured wool to score it. |
| **Build region** | An area where players may place blocks during a match. |
| **Protection** | An area players may not build in, usually around a spawn. |
| **Box** | A rectangle in a plan that marks one part of the layout (a hub, an approach, a spawn, a front line). |
| **Generator** | The tool that creates whole layouts from a few settings (players, symmetry, size). |
| **Style** | One material recipe: a single block, a stack of layers, a team colour, or a pattern. |
| **Theme** | A full terrain finish: one style each for the rim, wall, surface, and fill. |
| **Relief** | The shape of the ground: hills, slopes, and cliffs. |
| **Dressing** | Things placed on the ground: trees, boulders, paths, water, and buildings. |

A term that is the studio's own invention (an *approach family*, a *body form*, a *profile check*) is either
replaced by a plain description or kept off the screen. If a reader needs it, it needs a definition here
first.

## Checking copy

Before a change to client copy lands:

- Read every changed string aloud. If it does not sound like something a person would say, rewrite it.
- `grep -rn ' — ' src/PgmStudio.Client --include=*.razor` should not grow.
- Search `tests/e2e/` and `docs/` for any visible string that changed, and update them in the same commit.
