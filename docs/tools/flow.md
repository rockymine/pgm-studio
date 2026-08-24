# How a map is made — start here

This is the map over the other documents in this folder. It says what the levels of description are, which
tool works at which level, and how a map moves between them. It deliberately says nothing about how any one
tool is used: each has its own document, listed at the end.

## Four levels of description

A map is not one document. It is four, each describing the whole map at a different grain, each owned by a
different tool and stored separately.

| Level | Is | Document | Authored in |
|---|---|---|---|
| **The board** | rectangles on a coarse cell grid, what each is for, and where the objectives sit | `PlanModel` — `*.plan.json` | Plan |
| **The ground** | the real geometry at block resolution: outlines, heights, relief, paint, props | `SketchLayout` — the sketch layout | Sketch |
| **The play** | teams, spawns, protections, build regions, objectives and how they are captured | `MapIntent` | Configure |
| **The map** | the voxel world and the `map.xml` a PGM server loads | `VoxelWorld` + `MapXml` | — (built) |

**These are grains, not stages of completeness.** A plan is not a rough draft of a layout: it states things a
layout cannot — that this rectangle is a wool room, that these two pieces share a defence wall — and cannot
state things a layout can, like a curve or a one-block step. The same is true upward: a layout knows where
every block of ground is and has no idea what any of it is for. That is why a finished map needs all four and
why no one of them is "the" map.

**The flow is one-way.** A plan compiles into a layout and an intent; a layout rasterizes into a world; an
intent projects into the map document; the document writes out as `map.xml`. Nothing reads back up. There is
no path from geometry to a plan, and none from a finished `map.xml` to an intent — both are deliberate. The
moves compose in one direction only, and hypothesising what a finished map's plan would have been is a
person's job rather than a tool's.

Beside the four sits one more thing that is not a level at all: the **library** of materials, themes, house
parts and room styles. It knows nothing about maps — no slug, no stage — and the tools that build worlds reach
into it to pick a recipe. What they take is a *copy*, so a library edit can never rebuild a map that already
shipped.

## The tools

| Tool | Route | Works at | Writes |
|---|---|---|---|
| **Generator** | `/generator` | the board | nothing, until a candidate is kept |
| **Shape catalog** | `/catalog` | — | nothing; it is the vocabulary the generator builds from |
| **Plan** | `/maps/{slug}/plan` | the board | `plan_json` |
| **Sketch** | `/maps/{slug}/sketch` | the ground | `sketch_layout_json` |
| **Configure** | `/maps/{slug}/configure` | the play | `map_intent_json`, and the projected document |
| **Edit** | `/maps/{slug}/edit` | the map | the map document, directly |
| **Library** | `/library` | — | its own tables, shared across every map |

Read the row order as the pipeline. The one exception is Edit, which is not a step in it: it opens a map that
already exists as a `map.xml` and adjusts the document by hand.

## Where a map starts

Four ways in, and the difference between them is what exists before the studio does anything.

**From a plan.** `/maps/{slug}/plan` on a fresh map. The board is drawn first — pieces, roles, markers — and
everything else follows from a compile. This is the route that produces both a shape and an intent from one
document, and it is the one an agent should reach for.

**From a sketch.** `/maps/{slug}/sketch` on a fresh map. Ground first, and only ground: a sketch states no
teams, no spawns and no objective, so a map begun here arrives at Configure with geometry and nothing else.

**From the generator.** `/generator` rolls whole boards from a player count, a symmetry and a seed. Keeping
one stores it as a candidate; authoring it originates a map at the plan stage. From that point it is an
ordinary planned map.

**From a world built outside the studio.** `/maps/new` imports a Minecraft world that has terrain and no
`map.xml`, scans it into the database, and hands it to Configure with an empty intent. Nothing upstream
exists: the regions are drawn over ground somebody else built.

**The front door itself**, which no tool owns because it stands before all of them. Every other endpoint in
this folder takes a map; these are what a caller with no map reaches for first.

| Endpoint | Answers | Fails with |
|---|---|---|
| `GET /maps[?stage=&q=]` | every stored map, newest touched first, each with its slug, name, stage and the artifacts it holds — the list a driver picks a slug out of | — |
| `GET /maps/stage-counts` | how many maps sit at each stage, which is the dashboard's own read | — |
| `POST /map/from-documents` | a whole map stored from a plan, a layout and an intent together, answering the slug it landed under. A map already at that slug is replaced — see *The three documents are also the way back in* below | 422 the layout carries no ground |

## The hand-offs

Five transitions, and each is a single call. This is the part worth knowing precisely, because the merge rules
differ and getting one wrong loses work silently.

```
                ┌──► layout ──finish───► world ────┐
plan ──compile──┤                                  ├──► what a server loads
                └──► intent ──project──► document ─┘
```

The two branches rejoin at the end rather than staying independent. Building the world **resolves** parts of
the intent — a destroyable's and a core's block volume, which only the terrain they float over can fix, and,
on a sketch-built map, the monuments themselves — and the `map.xml` is rendered from that resolved copy. It is
why Configure drops its Monuments step on a map that came from a sketch: the answer is derived at build time
rather than authored.

**Plan → layout and intent.** `POST /api/plan/compile` turns the document into both halves at once, and it is
pure: the same plan compiles to the same pair on the server and in the editor. Abutting pieces of equal height
fuse into single polygons, so what arrives in Sketch is a board rather than a grid of rectangles.

**Layout onto a map.** `PUT /api/map/{slug}/sketch/from-plan` **merges** rather than replaces: the sketch's
themes, room shells, dressing and any author-corrected structural height are carried onto the fresh geometry.
Relief is the exception — it is keyed by island id and island identity is derived from the geometry, so a
recompile that re-fuses the board produces different islands and hand-authored terrain has nowhere correct to
land. That case answers **409** in the refusal envelope, one `SK1` finding per island it would orphan, and
`?force=true` accepts the loss. It is the author's call, not the server's. Both write paths answer what the
stored document names and does not have (`SK3`/`SK4`/`SK5`) as complaints on the success, the merge path over
the document the merge produced rather than the one that was posted.

**Intent onto a map.** `PUT /api/map/{slug}/intent/from-plan` carries much less: the **authors and
contributors the stored intent already held**, and nothing else. The plan owns the map's structure, so a
rebuild is meant to replace its teams, spawns, wools and build zones. What it does not own is who wrote the
map.

**The credits are the metadata route's, and an intent naming nobody says nothing about them.** A compiled
intent states `authors` and leaves it empty, which is not the same as stating that a map has none — so the
projection writes the people an intent names and leaves the map's own alone where it names none. Clearing
them is `PATCH /api/map/{slug}/metadata`'s, where a stated empty list means exactly that. **The order of the
two calls does not matter**, on a first build or any other. The carry is intent-to-intent and does a
different job: it keeps the *stored intent* truthful about the people, so the artifact and the map do not
disagree.

Two slices that look like they should ride across deliberately do not, and it is worth knowing because both
are Configure's work. **`islandTeams`** is a derivation rather than a decision, and island ids are positional,
so a tag made about the old board may name a different island on the new one — carrying it would relabel
territory rather than preserve an answer. **`symmetry`** is absent from a compiled intent on purpose: setting
the field is what switches the orbit expander on, and the expander rebuilds an intent from a fixed property
set that drops the structure directives. So **a rebuild clears both**, and Configure's World and Teams phases
have to be walked again — the World phase's gate is the presence of a confirmed symmetry, so the rail re-locks
behind it until they are.

**Layout → world.** `POST /api/map/{slug}/sketch/finish` rasterizes the layout into world geometry and moves
the map to the configure stage. This is the only stage transition the studio performs at runtime.

**Intent → document → `map.xml`.** `PUT /api/map/{slug}/intent` stores the intent and projects it into the PGM
document — teams, kits, regions, filters, apply-rules, spawns — in one idempotent pass.
`GET /api/map/{slug}/xml` renders that document, gated on the pre-flight checks;
`GET /api/map/{slug}/export` gives the world.

**The three documents are also the way back in.** `POST /api/map/from-documents` takes a plan, a layout and
an intent together and stores a whole map from them — the plan to re-plan from, the drawing rasterized into
geometry, the intent projected into the document — and answers the slug it landed under. A map already stored
under that slug is **replaced**: the documents name one map, so loading them twice is a reload.

It exists because nothing else can take a map back. `POST /map/import-folder` refuses a folder carrying a
`map.xml` outright, `import-url` extracts only `region/*.mca`, and no route in the studio reads a `map.xml` at
all — so a map authored against one studio could reach another only as a world, arriving without its plan, its
drawing or its intent, and could never be re-planned. What the documents carry is more than the world does.

**The authors ride in the body, and the operation applies them.** The three documents say what a map is
made of and a compiled intent names nobody, so the credits are stated beside them and written as part of the
load rather than in a second call the caller has to remember.

**And the plain writes are not merges.** `PUT /api/map/{slug}/sketch` replaces the layout blob verbatim, which
is what makes a deletion stick, and `PUT /api/map/{slug}/intent` replaces the stored intent wholesale for the
same reason. Only the two `…/from-plan` routes merge.

### When the plan stops being the source of truth

While the staged loop runs, the plan is upstream: edit it, recompile, and whatever the downstream tools added
is re-derived. That stops the moment an author does hand work a plan cannot express — a curve, a relief, a
theme, a placed tree. From then on the sketch and the intent are the working artifacts and the plan is
provenance. Nothing enforces this; the 409 above is the one place the system notices and asks.

## Stages and layers

A map row carries a **stage** — `plan`, `sketch`, `configure`, `edit` — and separately the **layers** it
holds. They are not the same thing and the studio treats them differently on purpose.

A stage is where a map has got to. A layer is a document it carries, and it keeps carrying it: a map built
from a plan still has its plan after it has been sketched, configured and exported, which is why the maps list
offers every tool a map has been through rather than only the one it stands in. The tallies follow the same
split — sketches are counted by the layer a map holds, configure and edit by the stage a map stands at.

**`edit` is not a stage the flow advances into.** A map originated in the studio ends at `configure` and stays
there; the only runtime transition anywhere in the tree is sketch → configure, at finish. Maps at `edit` are
the ones that arrived as a parsed `map.xml`, which on a development checkout is most of them — 349 of 425 in
this one. That is the whole difference between the two halves of the studio: one authors a map into existence,
the other opens one that already exists.

**A stage is a progress marker and not a lock.** The one-way flow above means nothing reads back up — a later
level never writes into an earlier one — and that is all it means: a configured map may be re-planned, and no
endpoint refuses on `map.stage`. What the stage is *for* is saying which of the moves already open is the one
the author was about to make.

**`GET /api/map/{slug}/state` answers all of it**: the stage, the artifacts, and the moves they allow, each with
its route. A move is offered because the documents it reads are stored rather than because the stage is right
— rebuilding a drawing from a plan needs a plan, whatever stage the map is at — so a driver reads its options
instead of learning them from this document. Its pair is `GET /api/map/{slug}/findings`, which answers what is
wrong with the map right now from every gate the stored documents can reach, and names the gates it did not
ask along with the route that does pay for them.

## What nothing owns

Worth knowing before looking for a control that is not there.

**The observer spawn** is Configure's alone. A plan puts it at the origin at a computed height and offers no
marker, so unless it is placed in Configure's spawn step, spectators stand at `0, 0`.

**Terrain paint and dressing** are the Sketch tool's alone. A plan carrying theme keys has them dropped on
parse, and Configure has no control for them. That is where the finish belongs rather than where its controls
happened to be built: the sketch rasterizer is what makes the world, so a finish authored a level above it is
authored before the thing it finishes exists, and a scope anchored to a plan piece would freeze at compile
while the ground under it kept being edited. It is also the half of a map a generator cannot reach — a
generator emits a plan, never a theme — so the finish is always hand-authored, at the level where the geometry
is final.

**Water lanes** are the Plan tool's alone. They survive a Configure save and generate their region, but
nothing in Configure renders them.

**The shells stamped over spawns and wool rooms** come from the room styles bound in Sketch's Rooms step.
Configure places the markers and draws the rooms; it cannot choose the building.

**Kits** are nobody's. Every generated team gets one fixed preset, and the only kit control in the studio is a
free-text box in Edit naming which kit a spawn grants — nothing states what a kit contains.

## What a write endpoint takes

**There are two conventions, and which one an endpoint follows is decided by what the endpoint is about.**
Twenty-five of the studio's write endpoints take one shape and fourteen take the other, so an author guessing
gets it right slightly more than half the time — and a wrong guess is silent, because an unknown property is
dropped rather than reported. Posting `{"style": {…}}` where the body should be a bare style answers **200**
with a preview of the defaults.

**A document endpoint takes the document itself, unwrapped.** Anything whose subject is one of the studio's
own documents — a plan, a sketch, an intent, a relief, a paint, a terrain material, a whole theme, a room
style — is posted as that document and nothing else. `POST /plan/compile` takes a plan; `POST
/terrain/material-preview` takes a material; `POST /room-styles/preview-snapshot` takes a `HouseStyle`. There
is no envelope and no field to name.

**A library endpoint takes a wrapper, because it carries more than the document.** Saving into the library
needs a name and a kind beside the thing being saved, so those take a small record: `POST /styles` is
`{name, kind, params}`, `POST /themes/import` is `{name, themeJson}`. Where such a wrapper carries a document,
the document is a **string** in that field rather than an object — the mirror of the `GET` that returns it,
which answers `{themeJson: "…"}` and `{styleJson: "…"}` the same way.

**The rule for telling them apart**: if the endpoint is asking *about a document*, post the document; if it is
asking to *file a document under a name*, post the record that names it. A document endpoint's own tool
document says which document, in the table row.

**A body that cannot be read is refused, never crashed.** An absent, empty or malformed document answers 400
carrying `RQ1` and, where the reader knew one, the field — `roof.gableWindows` rather than a sentence to hunt
through a document for. A missing wrapper field is refused the same way, and every missing field is named at
once rather than one per round trip. `docs/refusals.md` has the envelope.

## Where to read next

| Document | Read it for |
|---|---|
| `plan.md` | the board: the plan document field by field, what a compile produces, the refusals |
| `sketch.md` | the ground: shapes, islands, relief, themes, dressing — the largest tool |
| `configure.md` | the play: the intent, the import path, the objective phases, the export gate |
| `generator.md` | rolling boards: the request, what a compose produces, the browse feed |
| `shapes.md` | the vocabulary the generator fills boxes with, and how far each shape actually gets |
| `library.md` | materials, themes, house parts and room styles — the fourteen material kinds |
| `edit.md` | the inspector for maps that already exist |

Two documents outside this folder carry the rest. `docs/generator/model.md` is the canonical model of layout
generation and governs on any disagreement about it. `capabilities.md` beside this file is the capability
reference — what the system can be asked for at each stage, in far more detail than a tool document goes into
— and is the one to read when the question is *what could this look like* rather than *how do I drive it*.

`docs/gameplay/approaches.md` answers the question neither of those can: what the ground around an objective
does to a match, and therefore what a board should be composed *for*. It is kept separate because every claim
in it is the author's rather than the repository's — no corpus reading and no line of code settles what plays
well — and each one is marked with whether the author has confirmed it. Every claim it currently carries is
confirmed, so it is law rather than advice.
