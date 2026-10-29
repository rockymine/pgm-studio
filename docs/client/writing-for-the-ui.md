# Writing for the UI

The studio's interface is read by a mapmaker, not by someone who knows the code. Every word on screen — a
label, a heading, a `?`, a note, a tooltip, a status line, an error — is written for that reader, has one
place it belongs, one form it takes there, and one wording for one meaning. This document is the standard the
copy is held to: **where** each kind of text goes, and **how** it is written. `ClientTextTests` reads every text
out of the client's source and holds it to the rules below, the way `RuleTextTests` holds every rule's two
texts to `docs/refusals.md`; a form a new screen needs is added here and there before the screen uses it.

The documents under `docs/` are written in a different voice on purpose. They argue for a design and are read
start to finish, so they can afford long sentences, defined terms and the codebase's own vocabulary. The
interface is scanned in a second, by someone in the middle of a task. Copy that reads like the docs is the
most common fault in the client, and the first thing a new user notices.

The rules follow what the field has settled on — Nielsen Norman Group on tooltips, info tips, placeholders and
jargon; GOV.UK's hint text; Carbon's tooltip, toggletip and helper text; Polaris's labels and options;
Microsoft's UI text guide — and the reason for each is stated beside it, because a rule nobody can argue with
is one nobody can apply.

## The one question every text answers first

**Does a person need it to use the control?** If they do, it is on the screen: in the label, the unit after
it, or the note under the control. If they do not — a definition, the reason a value matters, what a term
means in this studio — it may sit one press away, behind a `?`. Nothing a person needs to complete or check a
field lives in a tooltip or behind a `?`, because most people never open either (NN/g, *Why so many info tips
are bad*). And a field that explains itself gets no help at all: a `?` that restates its label teaches the
reader that every `?` is empty, which is how the one that matters stops being opened.

## Where each kind of text goes

A text belongs to exactly one channel, and each channel has exactly one mechanism, so the test can read it and
a reader can predict where it is.

| channel | mechanism | holds | form |
|---|---|---|---|
| label | `Field Label`, `DetailHeader Label` | the name of the thing a control sets | noun phrase, at most 4 words |
| unit | `Field Unit` | the unit or qualifier of the value | one word of `FieldUnits` |
| help | `Field Help`, `Section Help` | a definition or a behaviour the label cannot carry | 1–3 sentences, at most 40 words |
| term | `Field Term`, `Section Term` | a glossary term the label names | the glossary's own definition |
| note | `Field Note` | what an empty value stands for, or the consequence of a value | one sentence, at most 20 words |
| readout | `Readout` | what the values work out to, computed | one sentence carrying a value |
| placeholder | `placeholder`, `Placeholder`, `Unbound` | the name of the unset value | at most 3 words |
| tooltip | `Title` on an icon-only `Button`, `data-tip` | the name of an action that has no visible word | verb phrase, at most 6 words |
| reason | the reason a shell closes a control with | why a control is closed | one sentence, at most 20 words |
| section title | `Section Title` | what the section's controls set | noun phrase, at most 3 words |
| description | `Section Description` | what every reader of the section needs and no control says | sentences, at most 25 words |
| empty state | `EmptyState` | that a list is empty, and how to fill it | opens *No*, at most 20 words |
| status | `StatusLine`, a save state | that something is loading, or has happened | short, past tense |
| option | `SelectOption` label and `Note` | a choice's name, and what it does | a name of at most 3 words; a sentence of at most 12 |
| button | `Button` content | an action | verb phrase, at most 4 words |
| failure | `Toast`, `Field Error`, `StatusLine` | what failed and what to do | *Couldn't* … *.* and the next step |

**A label names; it never explains.** It is a noun phrase in sentence case, at most four words, with no colon,
no final stop and no parentheses — a unit is the unit's channel, so `Base y (blocks)` is two mistakes: a unit
in the label, and the wrong one. It never opens with a question word (*Where*, *Which*, *How*, *What*, *Why*):
a label that asks a question is a sentence the options are made to finish (Polaris). People read labels first
and static text only when they think they must (Microsoft), so the label carries the meaning and everything
else only qualifies it.

**A unit is one word from one list.** `FieldUnits` holds them — *blocks*, *world y*, *°*, *%*, *seconds*,
*ticks*, *players*, *cells*, *optional* — and `Field` renders the one it is given in parentheses after the
label. A height is in *world y* and a length in *blocks*; a count of courses is *blocks* where a course is one
block, because two words for one measure is two measures to a reader. Nothing but a unit goes in parentheses
after a label (Microsoft UI text): `(owns it)`, `(the others break it)` and `(blocks the lava must fall)` are
notes or help that found the one place no rule watched.

**Help defines; it is never the only place a person learns how to use the control.** It is behind a `?` after
the label (`Field Help`) or the heading (`Section Help`), one to three sentences, each ending with a stop, at
most forty words. It says what the thing *is* or what it *does* that the label cannot. It never restates the
label or the unit, never opens with the label's own word, and is never put on a field that explains itself —
`ClientTextTests` lists the labels that never take one (*Name*, *Seed*, *Mode*, *Height*, *Width*, *Depth*,
*Material*, *Count*). Help is a disclosure for what some readers need; what most readers need is on the screen
(GOV.UK).

**A term is defined once, in the glossary, and shown from there.** Where a label is a glossary term — *Skirt*,
*Area*, *Mask*, *Soft rim*, *Palette* — the field says `Term="skirt"` and its `?` shows the glossary's own
definition with a link to its entry, so the same word is never defined two ways on two screens. `Help` on a
field whose label is a glossary term is refused: write the definition in `Glossary.cs`, once, where every
rule, screen and document already reads it (WCAG 3.1.3).

**A note says what the value does, under the control.** One sentence with a stop, at most twenty words, in
`Field Note`: what an empty value stands for (*Auto: the median ground plus 12.*), or what a value commits the
author to. It is not a definition (that is help) and not a computed fact (that is a readout). A field whose
label and unit say everything has no note; Microsoft's rule is that a supplemental explanation is never added
for consistency with a neighbour that needs one.

**A readout is computed, or it is not a readout.** `Readout` renders what the author's numbers work out to —
*Changed 898 columns.*, *Acts inside 2 outlines.*, *Top at 8.* — and it always carries a value the code
computed. A readout returns nothing when nothing is happening, because a line that never changes is the first
thing a reader learns to skip, and it then hides the one that matters. A static sentence styled as a readout
is help, a note or a description in the wrong place.

**A placeholder names the unset value and nothing else.** An input's placeholder is *Auto* where the system
works the value out (and the note says how) and *Any* where an empty condition narrows nothing; a select's is
the name of what it stands for while unset — *Plains*, *Bedrock box*, *Default bank* — or a prompt where
nothing works until one is chosen, *Choose a material*. Never an instruction, never an example, never
`None (…)`: a placeholder disappears when typed into, is skipped by some screen readers and is read as a value
(GOV.UK, NN/g).

**A tooltip names an action that has no visible word.** It belongs on an icon-only button and nowhere else, as
a verb phrase in sentence case, at most six words, no final stop, no colon — *Delete this rule*, *Move up*,
*Switch off*, *Close* — with a keyboard shortcut allowed after it in parentheses: *Pan (H)*, *Close (Esc)*. A
button with visible text has no tooltip, because a tooltip that repeats what is visible is noise (Microsoft,
Apple). A toggle's tooltip names what pressing it will do, not the state it is in. The same icon names the same
action on every screen: `x` is *Close*, `trash-2` is *Delete this …*, `eye` is *Switch off* or *Switch on*,
`chevron-up` and `chevron-down` are *Move up* and *Move down*, `plus` opens *Add*, `hand` is *Pan*, `rotate-cw`
and `rotate-ccw` are *Turn clockwise* and *Turn counterclockwise*, and `scan` is *Zoom to fit*.

**A reason says how to open the control.** Where a control is closed — the caller may not write, a build is
running — the reason `StudioAccess` supplies replaces the tooltip, as one sentence with a stop: *Sign in to change
this map.*, *Ask an admin for access to do this.*, or, where nothing the reader does opens it, *Only its owner, its
authors or an admin can change this map.* A reason is the one sentence a hover shows, and a disabled control's
question is how to enable it, not only why it is off.

**A section title is a noun phrase of at most three words.** Sentence case, no punctuation, no commas, never a
question and never a sentence: *Mask*, *Area*, *Excluded shapes*, *Paint rules*. NN/g measured that people see
about the first two words of a heading, so the meaning goes first.

**A description is what every reader of a section needs and no control says.** Complete sentences with stops,
at most twenty-five words, in `Section Description`. It never paraphrases the title, never defines a term (that
is the glossary) and never holds a readout. A section headed *Spawns* needs no description saying it holds the
spawns; most sections have none.

**An empty state says the list is empty and how to fill it.** `EmptyState` opens with *No* and ends with a
stop; a second sentence names the action: *No modifier yet. Pick one in the dock and drag its outline on the
canvas.* At most twenty words (NN/g, *Designing empty states*).

**A status is short and past tense.** *Saved.* *Loading…* A status line does not explain consequences unless
the reader must act on them: *Saved. Palettes that use this pattern are updated.* `StatusLine` is where a list
will be while it is loading, or the failure that stopped it.

**An option is a name, and its sentence is its note.** A select's options are read at the control's width and
compared against each other, so the label is a name of at most three words with no punctuation, and what the
option does is its `Note`, one sentence with a stop of at most twelve words. A label that is a sentence turns the
list into a paragraph with a dropdown around it.

**A button is a verb.** At most four words in sentence case, opening with the verb: *Add a rule*, *New plan*,
*Make a map*, *Reroll*. Never a sentence. A busy button names what it is doing: *Saving…*. Removing an entry is
*Delete* everywhere; *Remove* is refused, because two verbs for one action read as two actions.

**A failure says what happened and what to do.** *Couldn't save the palette. Try again.* *Couldn't load the
catalog. Reload the page to try again.* Never a status code and never server text in place of the sentence. A
finding from a gate is the rule's own text, from `GET /api/rules`, and is governed by `docs/refusals.md`.

## One form per situation

**A situation that recurs is said one way, whichever screen it is on.** Five screens written separately each
found a reasonable way to say that a list is empty or a save failed, and five reasonable ways read as five
different situations. So each recurring situation has one form, `ClientTextTests` refuses the others, and a new
divergence found on review becomes a row here and an assertion there in the same change.

| situation | the one form | refused |
|---|---|---|
| a list is empty | *No shapes yet.*, the noun in the plural, then the action | *No shape yet.* |
| nothing is selected | *No modifier selected. Pick one …* | *picked*, *chosen*, *Click one* |
| making a document | *Create one …* in prose, *Create map* on a map's button, *New palette* on a library entry's | *Make*, *Build*, *Start*, *Issue one* |
| adding an entry to a list | *Add a storey* | *New storey*, a bare *Add* |
| deleting from an icon | *Delete this rule* | *Delete*, *Remove*, *Delete from …* |
| a failure | *Couldn't save the palette.* | *this palette*, *that palette* |
| a load failed | *Couldn't load the maps. Reload the page to try again.* | *Try again.* after a load |
| the studio is unreachable | *Couldn't reach the studio. Check your connection and try again.* | any other wording |
| what an empty or locked value means | placeholder *Auto*, note *Auto: the median ground plus 12.*, *Auto: one for each kind of objective the map carries.* | *Left empty, …*, *Set automatically.*, *works it out* |
| a placeholder | a name, *Auto*, *Any*, *Choose a …*, *Search …* | a number, an example, an ellipsis |
| an option | one note for one option label, on every screen | a second note for the same option |
| drawing a rectangle | *Drag a rectangle …* | *Draw a box …* |
| a control is closed | *Sign in to …*, *Ask an admin for access to …*, *Only …* | *You are not signed in, so …* |
| a height counted in courses | *4 blocks of wall*, as the field beside it says | *4 courses of wall* |
| two labels for one quantity | one label; the other retired in the glossary's *Also called* | *Players per team* beside *Team size* |

## How it is written

**Say it the way a person would say it.** Write complete, ordinary sentences: subject, verb, object. Do not
write a noun phrase and hang the rest of the meaning off a dash. *"Every map that holds a plan — including
ones already built and configured."* is a label pretending to be a sentence; *"Maps that have a plan,
including finished ones."* is a sentence.

**Cut what the control already says.** A button labelled *Export* needs no tooltip saying *Export this*. If
deleting a sentence loses nothing the reader needs to act, delete it.

**One spelling per meaning.** Every text uses the glossary's term and never a name the glossary has retired
(`AlsoCalled`): *monument*, not *destroyable*; *ground*, not *terrain* or *land*; *canvas*, not *board*;
*point*, not *vertex*; *mid*, not *middle*; *build ceiling*, not *build height*. A plural of a retired word is
the retired word. Where an ordinary word is retired for a studio meaning, the screen uses another word for its
other meaning rather than the glossary losing the entry: the light and dark *theme* is the *Appearance*, a
floor's *border* is its *Edge*, and the terraform *hill* is a *Mound*, because *hill* is what King of the Hill
calls a capture point.

**One label, one meaning, one explanation.** Every field labelled the same carries the same unit, the same help
and the same note, on every screen. Two panels describing one input in two ways is two inputs to a reader, so
the test groups the texts by label and refuses a second wording. Where two things share a word and not a
meaning, the fix is the word: *Shoulder* as a power and *Shoulder* in blocks are two labels.

**No code on the screen.** A wire word, a field name, an id template or an internal type (`{team}-spawn`,
`relief_scope`, `BoxFiller`, `HTTP 404`) is not something a person can act on. Beyond the glossary's retired
names, the code's own words stay off the screen too:

| Do not write | Write |
|---|---|
| pipeline, emitter, emit, emitted | the generator, generate, generated |
| realize | build |
| evaluator, finding | check, problem |
| knob | setting |
| descriptor | layout code, JSON |
| relief | terraform |
| mouth-up | with its opening at the top |
| a type or class name | nothing; describe the behaviour |

A value the author typed or a name the author gave is shown as they wrote it, and is not a text this document
governs.

**Sentence case, plain characters.** Every text opens with a capital and capitalizes nothing else but a proper
name or an acronym (*PGM*, *API*, *ZIP*); no word is in capitals for emphasis. No text carries an em dash, an
arrow, a semicolon, an equals sign or a comparison sign, and apostrophes are straight. Use a period, a comma or
a colon instead of a dash; two sentences are almost always clearer than one joined by a dash. A range between
two numbers takes an en dash (*2–4 blocks*), and a separator in a compact readout a middle dot
(*12 blocks · 3 islands*).

**Use the Oxford comma.** *Pieces, zones, and objectives.*

**No metaphor, no personification, no wit.** *"A refusal is an answer, not an error — it is the emitter's own
words"* asks the reader to decode a figure of speech before they learn anything. Say what happens: *"If the
shape can't be built at this size, the reason is shown here."*

**Don't hard-code counts into prose.** *"Six libraries, in the order they compose"* is wrong the day a seventh
is added. Show the count as data (a badge) or leave it out.

**Address the reader as "you" only when it helps; never "we".** Most UI copy needs neither: *"Draw the islands,
then build the world"* reads better than *"You can draw the islands and then we'll build the world"*.

**Spell numbers and units the same way everywhere.** Digits for quantities (*3 islands*), `×` for dimensions
(*20 × 30 blocks*), and the unit after the number with a space.

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
| Rise (blocks) `?` *blocks the crest stands above the ground* | Rise (blocks) |
| Height `?` *the world y it holds; empty takes the median of the ground* | Top (world y), placeholder *Auto*, note *Auto: the median height of the ground in its area.* |

## Terms a new user has to learn

Some words are the vocabulary of mapmaking itself and cannot be avoided: a hub, a front line, a wool room.
These are not jargon to be removed but terms to be defined, once, in the same words everywhere. Each term has
two levels. Its **definition** is one or two short sentences, shown behind the `?` of every field and section
the term names. Its **guide entry** is the longer account a help page gives, and only some terms need one.

**The definitions live in one place: `Glossary` in `PgmStudio.Vocabulary`, served at `GET /api/glossary`.**
Every word the rules, the findings and the screens use is a term there, with its definition, the other names a
reader may meet it under (an older word, a field's name, PGM's own name) and the terms its definition leans
on. `?term=board` answers `layout`: a word is found by its own name or by another one. A rule or a tooltip uses
these words and never explains one, and a new word joins the glossary before it is used.

**The glossary page is browsed A to Z and searched by name first.** `/glossary` files every term and every
retired name under its own letter — a retired name as a *see* entry pointing at the term that replaced it, so a
reader looking up an old word lands on the current one — under a letter bar that stays in view and shows a
letter with no word as unavailable rather than dropping it. Below 960px the bar sticks under the pinned studio
bar, and its letters are one row that scrolls across, so the two hold one height and a jump lands clear of them. A search ranks a term whose name holds the words
above a retired name that does, and both above a term whose definition does, and marks every match. A link to a
term (`/glossary#hub`, which every `?` carrying a term ends with) always lands on it: a search that would hide
the entry is cleared, and the entry is brought into view below the bar, highlighted and given focus.

The studio's earlier words — *style*, *theme*, *relief*, *dressing* and the others the glossary lists — remain
in the code and the `docs/`; the client shows only the terms, and the rules and findings the server writes are
held to them by `RuleTextTests` and `FindingMessageTests`.

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
The default of 4 suits the common Capture the Wool lane widths of 8, 12, 16, and 20 blocks. A destroy map
usually needs less detail in a plan, since it is more about ground than routes.

**Symmetry.** Only one part of the map is designed, a half or a quarter, and the studio copies and rotates the
rest. Game settings work the same way: one team is set up and the others follow. An imported world has its
symmetry detected.

**Wool room.** The other team has to reach the room and take the wool. Taking it is a *touch*; placing it on
their wool monument is a *capture*.

**Generator.** The generator knows how a map's parts relate: hubs, approaches, wool rooms, and the gap
between the teams. It arranges them into layouts that follow those rules.

**Palette.** A palette sets a map's look, such as a desert or a snowy cliff. Builders already call a set of
blocks used together a block palette.

A term that is the studio's own invention (an *approach family*, a *body form*, a *profile check*) is either
replaced by a plain description or kept off the screen. If a reader needs it, it needs a definition in the
glossary first.

## Checking copy

`ClientTextTests` (in `PgmStudio.Api.Tests`, beside `RuleTextTests`) is the gate: it reads every text the
client writes — the attributes and named arguments above, a `Button`'s, `EmptyState`'s and `Readout`'s content,
every `SelectOption`, every failure sentence — and fails on any that breaks a rule here, naming the file and
line. Before a change to client copy lands, run it, read every changed string aloud, and search `tests/e2e/`
and `docs/` for any visible string that changed, updating them in the same commit.
