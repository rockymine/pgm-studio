# The Configure tool

## What it is

Configure turns terrain into a playable map. It is where a world stops being ground and becomes a match:
teams, spawns, the space players may build in, the objectives and what capturing one means. It is also the
only tool that **validates a map properly** and the only one that writes the `map.xml` and the region tree,
which is why every route through the studio ends here.

**It is entered two ways, and they are genuinely different jobs.** A map that came through Plan and Sketch
arrives carrying an intent already — its teams, spawns, wools and build zones were compiled from the plan —
and Configure is where that is finished: the objective colours picked, the observer placed, anything the plan
could not state added. A world built in Minecraft **outside** the studio arrives with nothing: it is
imported, scanned into the database, and configured from an empty intent by drawing the regions over terrain
that already exists. The phases are the same either way; what differs is how much is already filled in.

The routes are `/maps/{slug}/configure` for a map that exists and `/maps/new` for the import, which is phase
zero and has no slug because a map has no identity until its world has been scanned.

**What made all of this possible is `MapIntent`.** Before it, authoring a map meant editing the PGM document
itself — regions, filters and apply-rules, by hand, in the Edit tool. The intent is a declarative statement of
what the author wants, and a generator projects it into that document. So an author states *this team spawns
here, this wool is red and lives in that room*, and the twenty-odd regions, twenty filters and eleven
apply-rules that make PGM agree are derived rather than typed. Every phase below edits one slice of that one
object.

## What it writes

**One artifact: `map_intent_json` on the map row**, read and written through `MapArtifactStore`
(`src/PgmStudio.Data/Map/MapArtifactStore.cs`), the one store every map artifact goes through. It sits outside the entity-replace codec that
carries the rest of a map, exactly as the plan and sketch artifacts do, so saving the map's document cannot
destroy it.

**And a projection, every time it is saved.** `PUT /api/map/{slug}/intent` stores the intent and then runs
`IntentGenerator.Apply` over the PGM document, writing the teams, kits, regions, filters, apply-rules and
spawns it implies into the real tables. The write is a **wholesale replace** of the stored intent, which is
what makes a deletion in Configure stick, and the projection is **idempotent** — the generator clears its own
prior output first, so re-saving a corrected intent rewrites cleanly rather than accumulating.

**It never touches the world.** No block is placed, moved or removed by anything in this tool. That is not an
omission but the line that decides what is editable here: a core's casing on an imported map is obsidian that
already exists, so its dimensions are read out rather than set, and on a plan-built map they are what the
world export is about to stamp, which is the plan's statement to make.

The save model is deferred and phase-boundary shaped. A phase body patches its slice on the working intent and
marks it dirty; the wizard re-PUTs the whole object when the phase is left, and the top bar reads
Saved · Saving… · Unsaved. Two things save immediately instead, because they are not intent:
island exclusion (`PATCH /api/configure/{slug}/exclude-island`) and the import itself.

## The intent document

`MapIntent` (`src/PgmStudio.Pgm/Authoring/MapIntent.cs`) is the typed model; the wire form is camelCase JSON.
Every slice is optional, and **null means "leave it alone"** rather than "empty" — a map with no cores has a
null `cores` and nothing about cores is generated or cleared.

| Slice | Holds |
|---|---|
| `meta` | name, `created` (`yyyy-mm-dd`), authors, contributors — an account (a `uuid`) or a pseudonym (a bare name) |
| `symmetry` | the confirmed mode and centre; drives orbit-fill |
| `teams` · `maxPlayers` | the teams to generate and the shared per-team cap |
| `islandTeams` | island id → team; an authoring aid, read by the spawn step, not by the generator |
| `spawns` | per team: the point, the yaw, and the protection zone as a union of rects |
| `observer` | the `<default>` spawn — where spectators and pre-match players stand |
| `build` | `maxHeight` plus the buildable `areas` and the no-build `holes` cut out of them, and a separate `voidEnforcement` (null = none) stating whether the void is permanent — independent of whether `areas` is populated |
| `wools` | per wool: owner, colour, room rects, the source point, and one monument per capturing team |
| `cores` | per core: owner, anchor, the casing's measurements, and `leak` |
| `destroyables` | the DTM objectives — carried, never authored here (below) |
| `waterLanes` | the late-opening gaps — carried, never authored here (below) |
| `structures` · `spawns[].piece` · `wools[].piece`/`entries` | written only by the plan compiler; consumed by the world export |

**Map identity is half derived and half stated.** `MetaGenerator` writes the version (`1.0.0`) and the phase
(`development` — every map the studio authors is one; a map is promoted by the server that serves it, not by
the tool that drew it), and derives the `<gamemode>` elements and the objective line from the objective slices
the intent actually carries. What it cannot derive is the map's name, its people and its **creation date**: an
intent stating no `meta.created` produces a map with no `<created>` element, because a date nobody stated is
not a fact about the map.

Here is a complete hand-authored intent — enough to generate a two-team CTW map, and the shape an agent
writes:

```json
{
  "meta": {
    "name": "Voidwatch",
    "created": "2026-08-25",
    "authors": [ { "name": "rockymine", "contribution": "Layout" } ],
    "contributors": []
  },
  "symmetry": { "mode": "rot_180", "centerX": 0, "centerZ": 0 },
  "teams": [
    { "id": "red",  "name": "Red",  "color": "red" },
    { "id": "blue", "name": "Blue", "color": "blue" }
  ],
  "maxPlayers": 12,
  "islandTeams": { "1": "red", "2": "blue" },
  "spawns": [
    { "team": "red",  "point": { "x": 0.5, "y": 10, "z": -60.5 }, "yaw": 0,
      "protection": [ { "minX": -8, "minZ": -68, "maxX": 8, "maxZ": -52 } ] },
    { "team": "blue", "point": { "x": -0.5, "y": 10, "z": 60.5 }, "yaw": 180,
      "protection": [ { "minX": -8, "minZ": 52, "maxX": 8, "maxZ": 68 } ] }
  ],
  "observer": { "point": { "x": 0.5, "y": 30, "z": 0.5 }, "yaw": 0 },
  "build": {
    "maxHeight": 30,
    "areas": [ { "minX": -30, "minZ": -70, "maxX": 30, "maxZ": 70 } ],
    "holes": [],
    "voidEnforcement": { "exclusions": [] }
  },
  "wools": [
    { "owner": "red", "color": "red",
      "room": [ { "minX": -25, "minZ": -45, "maxX": -10, "maxZ": -30 } ],
      "spawn": { "x": -17.5, "y": 10, "z": -37.5 },
      "monuments": [ { "team": "blue", "location": { "x": 12.5, "y": 11, "z": -37.5 } } ] },
    { "owner": "blue", "color": "blue",
      "room": [ { "minX": 10, "minZ": 30, "maxX": 25, "maxZ": 45 } ],
      "spawn": { "x": 17.5, "y": 10, "z": 37.5 },
      "monuments": [ { "team": "red", "location": { "x": -12.5, "y": 11, "z": 37.5 } } ] }
  ]
}
```

Read against the model, three things in it are worth naming. **A protection zone and a wool room are both
unions of rectangles**, not single boxes, because a real footprint is often L-shaped; a one-rect list is the
common case and the format tolerates a legacy single object on read. **`owner` is the defending team** on a
wool, a destroyable and a core alike — the team that must stop it being taken — while a wool's `monuments`
name the teams that capture it, one each, which on a two-team map is the other one. And **a wool's `color` is
optional**: left empty it takes the owner team's colour.

**A step overwrites only what it owns.** The plan compiler writes keys no Configure step models — a spawn's
`piece` and `iron`, which size the stamped spawn room and place its renewable ore; a wool's `piece` and
`entries`, which size the cage and cut its doors — and rebuilding an entry from the fields a step knows about
would delete them silently, showing up only in the exported world. So each step starts from the entry it is
replacing and carries the rest through (`IntentSlice`). The same mechanism is why a slice the tool has no UI
for survives a save untouched.

### Symmetry and the orbit

When `symmetry` is set, the generator **orbit-fills by default**: the author defines one team's unit and
`SymmetryExpander` rotates or reflects it onto the others before projection, mapping orbit positions to
`teams` in list order. That is why the steps below talk about placing team 0's spawn and getting the rest —
the mirrored copies are generated, listed read-only with an *orbit* badge, and not separately editable. With
no symmetry the intent passes through as authored and every team's units must be stated explicitly.

## What a save produces

`IntentGenerator.Apply` runs the slices in a fixed order, and the order is load-bearing: symmetry expands
first, then meta, teams, wools, destroyables, cores, water lanes, and **build last** — because the build
slice's broad "not a void block" rule allows editing any solid block and PGM stops at the first apply-rule
that decides, so a build rule emitted before the spawn and wool-room protections would short-circuit them.

The output is a real PGM document. The intent above generates **19 regions, 20 filters and 11 apply-rules**
from its 2 teams and 2 wools. The Review phase's tree shows the eight of those regions that carry a name —
`red-spawn-point`, `blue-spawn-point`, `observer-spawn`, `red-wool-spawn`, `blue-wool-spawn`, `woolrooms`,
`not-spawns`, `not-build-area` — and the rest are the anonymous unions and complements holding them together.
None of it is typed by an author.

Posted to a map with no scanned world, that intent passes round-trip and mirror, skips buildability and fails
traversability, so its export gate stays shut — which is the tool's shape rather than a fault in the example,
and is the subject of *What it refuses* below.

## Phases

Eight phases on the rail, walked in order. A phase unlocks when the phases before it have their slice; the
rail locks anything further on, and jumping back is always allowed.

### Import — phase zero

Only on `/maps/new`, and only for a world that has no `map.xml`. Three steps. **Source** lists the importable
world folders under the imports root — a folder with `region/*.mca`, no `map.xml`, and no map already using
its slug — or takes a download link, which is fetched server-side from an allowlisted host and extracted
`region/*.mca`-only, so the browser never sees the archive. Next scans the chosen world into MariaDB, which is
what creates the map row. **Found** is the detection brief over the scanned world: islands, wool blocks,
monument candidates, resource blocks, chests, spawners, and the detected symmetry with its suggested team
count, each selectable for a detail explanation. **Plan** hands off to the wizard at Identity.

A folder carrying a `map.xml` is refused with 422 — it is not a new-map candidate. That is the line between
this tool and the Edit tool.

### Identity

One form: the map's display name and its authors and contributors. PGM takes a person as an **account** — a
`uuid` it resolves to a player — **or** a **pseudonym**, the element's own text, and either alone is a whole
author; `PUT /map/{slug}/intent` and `PATCH /map/{slug}/metadata` both accept either, and so does the editor.

A typed row settles on blur, and the first question asked of it is the shape of the name rather than
Mojang's opinion of it: an account name is three to sixteen of letters, digits and underscore, which is
Mojang's own rule (`AuthorNames.IsAccountName`). A value of that shape is looked up, and where an account
answers the row takes its canonical uuid and spelling — the uuid is what the `map.xml` prefers to carry, so
it is worth the request. Everything else is a **pseudonym**, in three cases that are deliberately one: a
value no account could be called, so nothing is asked; a value that is shaped like one and answers nothing;
and a lookup that could not be made at all. In each the stated name stands, the uuid stays empty, and the row
is saved. An author working with no route to Mojang states the people on their map and the credits hold.

What the field refuses is narrower, and it refuses out loud. A string that is not a name anybody could be
called — empty, past thirty-two characters, opening or closing on a space, carrying two in a row, or holding
anything outside letters, digits, spaces and `.,-_'` — is marked on the row, and `AuthorNames.Refuse` gives
the sentence under it. That is also the only row the phase drops from what it saves, because there is nobody
in it to credit. The rule is one constant in `PgmStudio.Vocabulary`, read by the browser and by the API, so
the two cannot disagree about what a name is.

A row's mark is drawn from what the row already holds — an initial over a hue hashed from its identity, the
uuid where an account answered and the name otherwise — rather than fetched as a player head from a host
outside the studio. One person keeps their colour across every tool that draws this editor.

The phase is complete with a name and at least one author, and that is the one gate that blocks the very
first Next.

### World — Scan · Islands · Symmetry

Three read-and-confirm steps over the already-scanned world; nothing here re-scans.

**Scan** is a read-only look at the extracted world on the shared world canvas, with its island-base and
surface layer toggle. **Islands** is where the strays are excluded — decorative rocks, observer towers —
selected from the list or by clicking the canvas; excluding one re-runs symmetry detection server-side over
the already-detected islands rather than touching the world. **Symmetry** confirms the mode and centre: the
detection is pre-selected and the author clicks another mode, or `none`, only to change it. That choice is the
World slice, and it is what every later orbit-fill reads.

A plan-built map arrives with **no** symmetry in its intent — the plan compiler leaves it empty — so this step
is where a generated map's symmetry is confirmed, not merely reviewed. **A rebuild clears it again**, along
with the island tags from the next phase, because neither is carried across a recompile (below). Since the
World phase's gate is exactly the presence of a confirmed symmetry, a rebuilt map re-locks the rail behind it
until the step is walked once more.

### Teams — Teams & islands · Spawn point · Protection

**Teams & islands** creates the teams, with the confirmed symmetry suggesting the count (`rot_90` → four,
everything else → two), sets the shared `maxPlayers`, and tags each island to a team by selecting a team and
clicking the island, which tints it. The tags are an authoring aid rather than map data: the spawn step reads
them so a marker takes the team of the island it lands on.

**Spawn point** drops team 0's spawn with the point tool and orbit-fills the rest, reassigning each copy by
the island it actually lands in — so a slightly-off rotation still gives the right team its own spawn. The
**observer spawn is placed here too**, as a spawn like any other, and it matters because it is the one thing
no plan authors: a plan puts the observer at the map origin at a computed height and offers no marker, so
unless it is moved here every map's spectators stand at `0, 0`. Every placed point seats on the terrain
column under it rather than floating.

**Protection** draws the anti-grief rectangles over a spawn. A zone is a union, so the first rect fixes the
team and further rects extend it; authored rects carry a stable id and resize on the canvas, and the symmetry
orbits them onto the other teams as read-only copies that are listed so the union reads coherently.

### Build — Build height · Buildable layer

**Build height** is the Y cap above which nothing may be placed, set on a side-view of the terrain or typed.
**Buildable layer** draws the over-void bridges as `areas` and the no-build cutouts as `holes` with the
rectangle tool. The islands' own terrain needs no rectangle — it is buildable through the void filter — so
what is drawn here is the crossing: the places a player may bridge a gap. The generator unions the areas,
subtracts the holes as a complement, and wires the void enforcement over the result (`not-build-area`,
`block-place=no-void`).

**Breaking out there is a second rule, and it is not the same rule.** `block-break=over-void-breakable`
allows what placing allows, and over the void additionally admits what the dressing stage leaves hanging
there — a tree's log and leaves, and every plant the flora overlay scatters. Without it a canopy reaching
past a coast is sealed for the whole match, since no column out there has a block at y=0. Terrain materials
are deliberately not in that list: a crag is a shape the author built, and a team may not mine it away.

The **Editable** overlay on the canvas is the read-back of all of it — one colour per column saying what
makes it editable, and `EZ1` on `GET …/editability` naming any patch of standing ground nobody can touch.

This is the phase the pre-flight sends an author back to, because an unbridged gap is what breaks the map.

**`build.voidEnforcement` is a second, independent knob (B132)**, with no canvas step of its own yet — an
author states it by posting the intent field directly. Declaring `areas` says nothing about whether the void
elsewhere may be bridged; a map with no `areas` at all got no enforcement of any kind, because the wiring sat
behind one early return keyed on `areas` being non-empty. `voidEnforcement` breaks that coupling: setting it
(even with `areas` empty, even with `exclusions` empty) wires the corpus idiom —
`block-place="deny(void)"` over everywhere minus the stated exclusions — which denies *placing* a block over
open void without denying *breaking* one that already hangs there, exactly as `alpine_mining_ii` does it. Null
(the default) leaves the void bridgeable outside any declared build area, which is every map today; the studio
does not default it to enforced, because the corpus itself is split on whether a map restricts building at
all through the void or through a hard region (`new-map-authoring.md` §5b measures both).

### Wools — Objectives · Spawn · Monuments · Room

The CTW phase, and the one that does the most work on an imported map, because **it reads the world rather
than asking**.

**Objectives** scans on entry and proposes the map's wools. Two independent readings are combined: signed
monuments — a sign reading *place the X wool here* — name each objective's colour and, by the island the
monument sits on, the team that captures it, which makes the owner the complement; and the physical wool
clusters in the world give each objective its source location. A colour named by a monument is an objective; a
physical wool no monument names, or one sitting inside its own team's spawn protection, is decorative and
excluded by default with the reason shown. The author confirms, fixes an owner, recolours, or adds a wool by
hand. **This is the step that makes an imported map configurable at all** — everything after it refines what
the scan proposed.

**Spawn** confirms where each wool dispenses: the `<wool location>` and the spawner's point, seeded from the
detected source centroid and seated on the floor the pile rests on, with the side-view setting Y. Editing the
anchor team's wool re-derives its symmetric partners; editing an orbit copy nudges that one alone.

**Monuments** confirms one monument per capturing team. The scan usually pre-fills them from signed pedestals;
a gap is filled by drawing a box around a cluster, which routes each hit to its colour's wool with the
capturing team read off the island. An empty box drops one manual monument at its centre. **This step is
dropped entirely on a sketch-origin map**, where monuments are derived at export from the plan's geometry
rather than authored.

**Room** draws the rectangles the wool lives in — again a union, again orbited onto the partner wools as
read-only copies. A wool with no room still generates its objective and its monuments; what it loses is the
room region, the spawner and the room wiring.

### Cores — Objectives · Casing

**Objectives** is confirmation rather than detection: a lava volume sealed in obsidian is a signature nothing
but a core produces, so the ingest scan already found this map's cores and stored them. The author accepts a
proposal and fixes the team defending it, and the casing the detector measured becomes the core's own region —
which is what keeps the goal's blocks and the region scoping them the same box. The detector finds about three
in four, so a core can also be placed by drawing its casing footprint over obsidian the author can see; the
volume is then derived the way the world-export stamper derives one, so a core described here and one built
from a plan sit at the same height over the same terrain.

**Casing** reads out the footprint, height, wall thickness, capped-or-flush lava and float, and edits exactly
one field: **`leak`**. The reasoning is the tool's own boundary. The others describe blocks — on an imported
map they are measurements of obsidian that exists, and changing them would recompute a region that no longer
scopes it; on a plan-built map they are what the export is about to place, which is the plan's statement.
Leak is not a block: it is an attribute on the `<core>` element and nowhere else, so it belongs to the tool
that authors the element. Paired with the measured float it states the dig — a leak greater than the float
means breaching the casing is not enough and players must cut the ground out from under it.

### Review & Export — Pre-flight · Region tree · XML

**Pre-flight** runs four checks server-side over the generated map and reports the export verdict. Two are
blocking and two advisory:

| Check | Asks | Blocks export |
|---|---|---|
| Round-trip | codec parity — does the document survive XML ↔ dict with nothing lost | yes |
| Mirror | do the symmetric halves agree — spawn, protection, wool, room, build | no |
| Buildability | does every spawn, wool source and monument sit over solid ground | no |
| Traversability | is the spawn↔wool chain connected across terrain and build geometry | yes |

They are drawn as check rows over a single static top-down picture of the map — real island geometry, the
orbit-filled build bridges, and the spawn and objective nodes in their team and dye colours — which is the
playability question in one image. Every objective gates: a destroyable or core whose approach ground the
spawns cannot reach fails the same check an unreachable wool does. A failed traversability links back to Build, because a bridge is the fix.

**Pre-flight's `exportReady` is not the whole export gate on a sketch-origin map.** The four checks above run
against the stored document and the scanned/cached surface; they do not build a world and so cannot see the
four refusals below, which only fire from `GET /map/{slug}/xml` and `/export` themselves (`OB20` needs no
world and could in principle run here too, but lives with the other three so a caller checks one place for
everything Pre-flight cannot see). A sketch-origin map can therefore read pre-flight-ready and still 409 at
Export — the honest answer is in *What
it refuses*, not in this table.

**Region tree** is the read-only inspect view of everything the generator produced. On an intent map the
shaping steps deliberately do not show a tree — structure is a generated artifact, not a thing to maintain —
so this is where it can be looked at.

**XML** shows the generated `map.xml`, segmented into containers picked on the left, and the flow bar's Next
becomes **Export**. It is enabled only when the pre-flight gate is open.

## What it refuses

**Each phase gates the next**, and the gate is the presence of its slice rather than a form validation:
Identity needs a name and one author, World a confirmed symmetry, Teams a non-empty team list, Build
a build slice. The objective phases share **one** gate — the map needs *an* objective, of any kind — so a DTC
map is never held up by an empty wool slice and a CTW map is never asked for a core.

**A person stated under something that is not a name refuses the write.** `PUT /map/{slug}/intent` answers
**400** `RQ1` for an author or contributor whose name is empty, past thirty-two characters, carries two
spaces in a row, or holds a character outside letters, digits, spaces and `.,-_'`, with `field` naming the
person — `meta.authors[0].name` — and no part of the write happening. A name Mojang cannot answer for is not
this: that is a pseudonym and it stores. The refusal exists because the alternative is a 200 that quietly
credits fewer people than the author listed, which nothing downstream can notice.

**The export gate is two of the four checks.** `GET /api/map/{slug}/xml` answers **409** with the isolated
points named when traversability fails, and the same document would throw on a round-trip failure. Both the
preview and the download are blocked by it. A map with no stored intent — a corpus map — is not gated at all
and exports unconditionally, because there is nothing to pre-flight.

**And the export gate asks whether the document is a map at all, and whether anyone is playing it.** Every other check it runs quantifies over a
collection — a goal standing in void, a prop crowding one, spawn and wool points reaching each other — so a
board with nothing on it satisfies all of them, and two authoring-trial boards exported a ten-line `map.xml`
with no team, no spawn and no objective while every stage answered 200. **EX2** refuses a document declaring no
spawn of any kind, team or observer: nobody can enter it. **EX3** compares what the resolved intent states
against what the document carries and refuses when a kind went in and did not come out — the harder failure of
the two, because those boards' plan carried two spawns and two destroyables that were lost somewhere between
the plan and the export, which no gate reading one side alone can see. **EX4** refuses a map that states an objective and no team:
the three gamemodes the studio authors — CTW, DTM, DTC — are played by teams, so something to win with nobody
to contest it is not a map, and that one is the author's ruling rather than arithmetic. It is asked of the
objectives rather than of the `<gamemode>` element, which is derived from exactly those three lists and which
PGM does not read to decide what runs (`OB7`); a board with no objective at all is not asked, because that is
unfinished rather than wrong and `PL3` already says it as a complaint.

EX2 applies to every intent-authored map, EX3 and EX4 wherever the objectives have been written; a corpus map
is exempt from all three, because 281 of the 1,616 maps in the two corpora declare no team and an FFA map with
none is not a broken map.

**A sketch-originated map's export also gates on its dressing document.** `DressingJson` reads every placed
prop rather than reading what it can and dropping the rest — a document that fails to parse anywhere (an
unrecognized `kind`, a field of the wrong shape) refuses the whole export rather than shipping the map with
fewer props than it was asked for, since a bare board built from a document that quietly lost its dressing is
worse than a refusal naming what broke. `GET /api/map/{slug}/xml` and `GET /api/map/{slug}/export` both answer
**422** `{error, rule: "DR-DOC", message, subject, field}` — `subject` names the prop (by id, or by its position
when it never got far enough to have one), `field` names the property inside it, and `message` reads as one
sentence naming both, e.g. *"prop 'field-road' (#1): field 'kind' names kind 'boulderr', which is not one of
path, water, tree, boulder, flora, house."* A prop's own enum fields (`style`, `form`) are read
case-insensitively, so this never fires on a case difference — only on a `kind` the reader does not know, a
`kind` missing outright, or a field of the wrong JSON shape (`docs/tools/sketch.md`'s Dressing section).

**Which gate runs does not depend on which door the caller came through.** A sketch map's whole chain —
`OB20`, `SK2`, `OB17`, `EX1`, `EX2`/`EX3`/`EX4` — is inside `MapExportComposer.BuildAndCompose`, so the
headless driver, which links that method and speaks no HTTP, is judged by exactly what `GET /export` is
judged by. The traversability judgement is asked there over the ground **this build** rasterizes rather than
over the segments the last `sketch/finish` stored, which is the same reason `OB17` is: a subtract cut, a
relief solve or an edit after the finish each move where ground is without re-entering that stage.

**The gate cannot open on a map with no scanned world.** Without a world there are no surface or `y=0`
columns, so buildability reports *skip* and traversability has no walkable ground to connect anything
across — every spawn and wool reads as isolated whatever the build areas say. Measured on a hand-authored
intent with no world: round-trip and mirror pass, buildability skips, traversability fails on both spawns, and
`GET /xml` answers 409. Configure writes the XML for a world that exists; it cannot author one in a vacuum.

**Every export checks its declared `<gamemode>` first, regardless of origin.** `MapExportComposer` reads the
document's own `gamemode` list before anything else on either leg — no world, no resolved intent, nothing
sketch-specific — and answers **409** with an `OB20` finding the moment one of the author's own
ids falls outside PGM's closed enum (`destroyables-and-cores.md` §1, `OB7`, which states it in full: PGM
parses `<gamemode>` as a repeated element and fails the whole map to load on the first id `Gamemode.byId`
cannot resolve). The studio's own generator never writes an id outside that set; the check exists for a
hand-edited label or a corpus-derived one the export would otherwise ship straight into a load failure.

**A sketch-origin map's export answers two more 409s, from `MapExportComposer` itself rather than from
pre-flight.** They exist because `BuildAndCompose` already builds that map's world and holds its resolved
intent, so it can ask them against the ground the rasterizer actually produced instead of the plan's
rectangles — the case a subtract cut, a relief solve, or a post-compile sketch edit opens, none of which
re-enters the compile gate, and the case a map begun in Sketch never reaches at all.

- **`OB17` — objective placement**, asked again here the same way the compile gate asks it
  (`ObjectivePlacement.Check`, `destroyables-and-cores.md`): a destroyable or a core may not overhang the
  void, or reach into a spawn's or a wool room's frame. `POST …/sketch/columns` asks the same read
  (`MapExportComposer.CheckGoalPlacement`) of the same build and answers it as a **complaint**, so an author
  hears it while drawing rather than at the door.

- **`OB24` — two goals built into the same blocks**, asked over the resolved boxes and therefore only here:
  a plan reads two placements at two coordinates and the volume around each is the stamper's to settle. The
  cause is nearly always the symmetry rather than the author's hand — a goal occupies its own position *and
  every image of it*, so a second goal drawn where the orbit maps the first lands inside it, and nothing
  earlier looks wrong. A destroyable stamped inside a core is one structure serving two objectives: breaking
  either is breaking the other. The finding names both goals and the two boxes that overlap.

**`OB19` is not among them any more.** A tree, a boulder or a building inside a goal's clearance is
**declined** by the dressing pass — the prop is not in the world, the finding names it and carries the
prop's id, and the map exports. The author's ruling is the reason: `OB17` indicts the objective itself and
there is nothing to drop, while `OB19` indicts a prop, and a prop is removable. It reaches a caller the way
every other decline does — `warnings` from `POST …/sketch/columns`, `Pgm-Warnings` from `/xml` and
`/export`, and `region/dressing-report.json` inside the zip. Ground cover is still exempt: only a placed prop
is turned away.

`OB18` briefly refused a kit/material mismatch as an unwinnable goal (an obsidian destroyable
against an iron pickaxe); the premise was false — an iron pickaxe breaks obsidian, it just does not drop it,
so the mismatch made a raid slow, not impossible — and the refusal was removed (`destroyables-and-cores.md`
§8). `TeamsGenerator` still pairs the generated kit's pickaxe to the goal's material, which is why the case
is rare in practice, but a hand-edited kit that leaves the pairing off no longer blocks export.

Each answers the one refusal envelope — `{error, message, findings[]}`, `docs/refusals.md` — the gate named in `error` and the fault in the findings
(every declared id PGM's enum did not recognize) and `findings` for OB17, one per goal, each carrying its own
`rule`/`message`/`subjects`. Neither applies to a map with no stored sketch layout: they read the sketch's own
rasterized ground, which only a sketch-origin map carries.

**Import refuses a folder with a `map.xml`** (422), a non-allowlisted host (403), an archive with no
`region/*.mca` (422), and a slug already taken (409).

## The API

Every endpoint is anonymous and rooted at `/api`. The striking thing about the list is how little of it
writes: apart from the import and one island toggle, **Configure has exactly one write** — the intent PUT.

**The one write**

| Endpoint | Body | Answers | Fails with |
|---|---|---|---|
| `GET /map/{slug}/intent` | — | the stored intent, or an empty one | 404 unknown map |
| `PUT /map/{slug}/intent` | the whole intent | `{}` — stores it and re-projects the document; `warnings` carries any field the intent reader has nowhere to keep (`RQ3`), which is a slice the author stated and the map will not carry. An `If-Match` names the **intent's** revision, which is what `GET …/intent` answered; the projection that follows rewrites the map and is not guarded by it | **400 `RQ1`** a person named something that is not a name, `field` naming them · **409 `RQ5`** a stale `If-Match` · 404 · 422 the stored map will not carry the projection |
| `PUT /map/{slug}/intent/from-plan` | a compiled intent | the projected map, carrying the stored **authors and contributors** onto it and nothing else — a rebuild clears the confirmed symmetry and the island-team tags; `warnings` carries any field of the **posted** intent the reader had nowhere to keep (`RQ3`) | **400 `RQ1`** a person named something that is not a name · 404 · **409 `RQ5`** a stale `If-Match` · 422 the stored map will not carry the projection |

**Getting a world in**

| Endpoint | Body | Answers | Fails with |
|---|---|---|---|
| `GET /maps/import-candidates` | — | the importable folders: `{folder, slug, region_files}` | — |
| `POST /map/import-folder` | `{folder, slug?}` | the slug and one count per kind of feature row the scan wrote — creates the row and scans into MariaDB | 400 `RQ1` · 404 `RQ4` no such folder · 409 `RQ5` slug taken · 422 `IM6` it is a map already · 422 `IM5` no `.mca` |
| `POST /map/import-url` | `{url, slug?}` | the same, fetched server-side | 400 `RQ1` · 403 `IM1` host · 413 `IM3` too large · 415 `IM4` not a zip · 422 `IM5` no region · 502 `IM2` the host did not serve it |
| `POST /map/{slug}/scan-world` | — | the same counts, over a world already on disk: re-reads `<root>/<slug>/region` and rewrites the map's feature rows. What `import-folder` runs at the end, reachable on its own for a world that changed | 404 unknown map, or no world folder for it under the configured roots |
| `GET /map/{slug}/scan-summary` · `/islands` · `/symmetry` | — | the detection brief, the island polygons, the detected symmetry | 404 |
| `PATCH /map/{slug}/symmetry` | `{status, confirmed_type?, centre?}` | confirms or rejects what was detected — `confirmed` or `none`, with an optional override of the mode and centre | 404 |
| `GET /configure/{slug}/state` · `PATCH /configure/{slug}/exclude-island` | `{island, excluded}` | the scan config; excluding re-runs symmetry without re-scanning | 404 |

**Reading the world while authoring.** These read the built world rather than a posted document, so all but
one are `GET`. The exception is `wool-sources`, whose body is the rectangle to look inside, **nested under a
`bounds` object**: `{"bounds": {"min_x": 0, "min_z": 0, "max_x": 16, "max_z": 16}}` — the `bounds_2d` every
other surface answers, spelled the same way. It is posted rather than queried because a box does not fit a
query string legibly. The refusal names the object as well as its corners, because a caller who sent the
four flat and a caller who sent none at all otherwise read the same sentence back; a `bounds` present and
short of a side says so as well.

```json POST /api/map/{slug}/wool-sources
{"bounds": {"min_x": 0, "min_z": 0, "max_x": 16, "max_z": 16}}
```

| Endpoint | Answers |
|---|---|
| `GET /map/{slug}/column-floor?x=&z=` | the floor a marker seats on — what makes a placed point land on terrain. `{y: null}` where the column has no segment data |
| `GET /map/{slug}/segments[?axis=&xmin=&xmax=&zmin=&zmax=]` | the vertical section through the world along one axis — what the side view draws. 404 when the map has no segments |
| `POST /map/{slug}/wool-sources` | wool colours and their source clusters inside a drawn rectangle |
| `POST /map/{slug}/resources` | the iron, gold and diamond blocks — optionally inside a drawn rectangle, the same `bounds` object — and how many of them a declared `<renewable>` already covers |
| `GET /map/{slug}/wool-suggestions` | the wool colours the **world** holds that the intent has not declared as objectives: the gap between what was built and what was stated |
| `GET /map/{slug}/monument-suggestions?box=&style=` | scored monument candidates in a box, each with its colour, confidence and evidence. `box` is required — the author marks the area |
| `GET /map/{slug}/core-suggestions[?box=]` | the detected casings, plus the generator's casing defaults. The box is optional and narrows the list; one that is stated and cannot be read is refused (`RQ1`, `field: box`) rather than skipped, because skipping it answers every casing the map has and reads as the volume holding them all |
| `GET /map/{slug}/origin` | whether the map came from a sketch — which drops the Monuments step |

### Asking whether the map can be played

**These are the reads that answer a question a gate will later ask, and they answer it before the gate
does.** Every one is cheap — none builds a world — and each corresponds to something the export refuses or
the pre-flight reports, so an author or an agent can hear the answer while the map is still editable rather
than at `GET /export` with a world already synthesised behind it.

**They answer over the goals the author has stated, not only the ones the document carries.** A wool, a
destroyable and a core each state where they stand from the moment they are authored, but a destroyable's
region is the box the stamper built its blocks from, so one whose box is not cast yet is kept out of the
document rather than given a guessed region. Reading the document alone therefore answered over a destroy
map's spawns and nothing else — `elderwold-10`, a two-cairn DTM, reported **2** navigation points and a
coverage read that traced one journey and called **57%** of the board dead. The reads take the intent's goals
beside the document now: 4 points, 6 journeys, **30.4%** dead, and the dead patches fall in mirrored pairs the
way a `rot_180` board's should.

**Every one of them needs ground, and says whether it had any** rather than guessing. Four answer
`haveLayers` — `traversability`, `kit-reach`, `wool-availability` and `monument-obstruction` — false on a map
with no scanned world; `editability` answers `hasY0` for the same question, and `coverage` answers
`haveRoutes`, which is the narrower one of whether there were journeys to trace. Read the flag first: without
columns there is nothing to connect anything across, so traversability reads every spawn and wool as isolated
and buildability reports *skip*, and that is a fact about the map's state rather than a verdict on its
design.

| Endpoint | Answers | Fails with |
|---|---|---|
| `GET /map/{slug}/traversability` | whether every spawn reaches every objective **over the ground a walk runs on** — the same places `/walk` measures a distance across, so a verdict and a distance cannot disagree about whether there is a way. A column offers a place for each surface with two clear blocks over it, so a building is not a shortcut through itself and a deck over a gallery is one component with it only where something joins them. Answers `connected`, the component count, each navigation point with the component it landed in, and every point that is cut off — with `for` naming the team an entry denial shut out, where that is the cause. A team that must **take** a goal has to stand on it; a team that **defends** one only has to reach the border of the barred ground it stands in, since its own wool room's `enter` rule keeps it out by design. This is `EX1` asked early: the export refuses on the same walk | 404 |
| `GET /map/{slug}/editability` | which columns a player may edit and **what makes each one editable**, as digit rows over a bounding box with a zone legend and the counts. The four zones are `build_zone` (a rectangle the author drew), `ground` (nothing forbids it — on a void-enforced map exactly the columns with a block at y=0), `filtered` (a spawn's ore, a wool room's team-and-material whitelist) and `sealed`. Place and break are read as the separate scopes PGM makes them, so a canopy over the void that is breakable and not placeable-on reads as a permission rather than a refusal. `findings` carries `EZ1` — a patch of standing ground nobody can edit, with its box | 404 |
| `GET /map/{slug}/kit-reach` | the harder version of traversability: can a fresh spawn reach each wool with **only the placeable blocks its kit grants**? A map can be connected on paper and unreachable with the blocks players actually hold. `blocksNeeded` counts both halves of what a player builds — one a cell for void bridged, and Δ−1 for a rise of Δ — and beside it `blocks` says how far round the cheapest crossing goes and `drops` what it falls down on the way. Each team walks **its own** ground, with whatever an `enter` rule bars it from subtracted, so a wool behind an oversized protection reads unreachable here and not merely expensive. A spawn and a wool are each walked from the **storey their region states** — the floor of the spawn box, the wool's own `y` — so a spawn on a deck is priced along the deck rather than along whatever lies under it. Every wool is reported with the `owner` that defends it, and a team's **own** wool is never held against it — this budget is what a capture costs and a defender makes none, which is a narrower reading than the traversability verdict's, where a defender still has to reach its own room's border | 404 |
| `GET /map/{slug}/wool-availability` | per declared wool, whether it can be obtained at all, and whether the source is repeatable or one-time — a wool nobody can pick up is a match nobody can finish | 404 |
| `GET /map/{slug}/monument-obstruction` | each monument's block, and whether something already stands there. PGM warns on load and the wool cannot be placed, so this is the one read whose fault is invisible in every render | 404 |
| `GET /map/{slug}/coverage` | where the ground is lived on: every ground cell classed reached/decorated/dead (digit rows + legend), the shares, and each dead patch with its area, centroid and walk to the nearest used ground — the corridors between every waypoint pair, widened `GroundCoverage.CorridorMargin`, plus each waypoint's `PoiRadius` ring and each prop's `PropRadius` fringe. The journeys are walked storey by storey on the same ground the traversability verdict is taken over; the picture is one pixel a cell, so a stacked column is drawn once however many storeys it carries. `?format=png` answers the same grid as a picture. A measurement, not a gate — nothing refuses on it yet | 404 |

**Finishing**

| Endpoint | Answers | Fails with |
|---|---|---|
| `GET /map/{slug}/preflight` | `{intentMap, exportReady, checks[], log[], traversability}` | 404 |
| `GET /map/{slug}/regions/tree` | the generated region tree, grouped | 404 |
| `GET /map/{slug}/xml` | the `map.xml`, with `Pgm-Warnings` carrying the count and rule ids of every prop the dressing pass dropped building it — `OB19` among them | every refusal is `{error, message, findings[]}` (`docs/refusals.md`), the gate in `error`: **409** `unknown gamemode` OB20 (every map, checked first) · **409** `not traversable` EX1 · **409** `objective placement` OB17 · **409** `not a playable map` EX2/EX3/EX4 · **422** `dressing document invalid` DR-DOC · 404 |
| `GET /map/{slug}/export` | the world ZIP, with `Pgm-Warnings` as above and `region/dressing-report.json` inside it carrying the rule, the cell and the prop for each | 404 unknown map · the same 409 and 422 as `/xml` (OB17/DR-DOC/EX3/EX4 sketch-origin maps only; EX1/EX2 every intent-authored map; OB20 regardless of origin), plus non-2xx with a message on a zip/IO failure |

## Driving it without the UI

An agent authors the intent document and PUTs it. Nothing else in the tool has to be touched.

**From a world built outside the studio:**

```
GET  /api/maps/import-candidates                      → [{folder, slug, region_files}]
POST /api/map/import-folder    {"folder": "voidwatch"} → {slug, islands, monument_candidates, …}
GET  /api/map/voidwatch/islands                        → the island polygons, to tag teams against
GET  /api/map/voidwatch/symmetry                       → the detected mode and centre
POST /api/map/voidwatch/wool-sources  {"bounds": {…}}  → the wool colours actually in the world
GET  /api/map/voidwatch/monument-suggestions?box=…     → the monument candidates, with colours
PUT  /api/map/voidwatch/intent  <the intent document>  → {}
GET  /api/map/voidwatch/preflight                      → {exportReady, checks[], log[]}
GET  /api/map/voidwatch/xml                            → the map.xml, or 409
```

**From a map that came through Plan and Sketch**, the intent is already stored: `GET …/intent`, patch the
slices, `PUT` it back. Only patch — a PUT replaces the stored intent wholesale, so a document that omits
`spawns[].piece` or `wools[].entries` deletes the plan's room sizing, and the loss shows up nowhere until the
world is exported.

Two habits make this reliable. **Read the world before writing the intent**: `wool-sources` and
`monument-suggestions` say what colours and capture points actually exist, which is the difference between
authoring a map and guessing at one. And **treat pre-flight as the answer for its four checks, not the XML**:
it names which one failed and why, where `GET /xml` only says 409 — but on a sketch-origin map, a
pre-flight-clean document can still 409 on `OB17` (*What it refuses*, above), since it reads the world
`GET /xml` itself builds rather than anything pre-flight inspects — and `POST …/sketch/columns` is where to
hear it first, as a complaint on a build that was going to happen anyway.

**Ask the playability reads before paying for a build.** `GET /export` is the most expensive call in the
studio — it synthesises the whole voxel world before it answers — so hearing `EX1` from it is hearing, after
the build, something `GET …/traversability` would have said for nothing. Expensive is relative rather than
long: a 100×140 board carrying some 9,000 ground columns answers in **0.3–0.7 s**, against 0.2 s for the
traversability walk and 0.4 s for pre-flight, and the cost tracks the board's area up to
`SketchRules.MaxBoardColumns`. The reads are cheaper because they answer off less, not because the build
drags:

```
GET  /api/map/voidwatch/traversability   → {connected, isolated[]}   the walk EX1 refuses on
GET  /api/map/voidwatch/kit-reach        → per wool, reachable with the blocks the kit grants
GET  /api/map/voidwatch/wool-availability → per wool, obtainable at all, and repeatable or once
GET  /api/map/voidwatch/monument-obstruction → whether anything already stands where a wool is delivered
GET  /api/map/voidwatch/editability      → the per-column grid of what makes each column editable
```

The first four carry `haveLayers` and `editability` carries `hasY0`. **False means the map has no scanned
world**, and every one of these answers over ground: without it traversability reads each spawn and wool as
isolated and the pre-flight's buildability check skips, which says the map has not been scanned rather than
that its design is wrong.

The map that goes with this document is any sketch-origin map in the corpus of built maps. On
`no-blocks-placed-verify` — 2 teams, 4 wools — pre-flight reports 26 regions, 20 filters and 11 apply-rules,
all four checks passing, and the gate open.

## Limits

**There is no destroyable phase.** Wools and cores each have one; DTM does not (`N12`). A destroyable authored
in the Plan tool rides through Configure untouched and exports correctly — the slice is carried, generated and
mirrored — but it cannot be seen or edited here, and there is no detection for one either, because unlike a
core a destroyable has no signature of its own (`B58`).

**Kits cannot be edited.** Every generated team gets the fixed Standard preset, and the intent carries no kit
field at all. The one kit surface in the studio is a free-text box in the **Edit** tool naming which kit a
spawn uses; nothing anywhere authors what a kit *contains* (`C9`), so a map wanting different starting gear
has to be finished outside the studio.

**A team's id does not follow its colour.** The id is seeded from the colour first picked, and recolouring a
team afterwards changes only the colour — so a team switched from red to purple keeps `id="red"` and every id
derived from it (`only-red`, `red-spawn-point`, the `…-red-monument` blocks). PGM resolves the id, so the map
plays correctly; it reads wrong everywhere (`N09`).

**Water lanes cannot be authored here, and are invisible even when present.** The Buildable-layer step draws
`build.areas` and `build.holes` and nothing else, so a lane compiled in from a plan does not render. It does
survive, though, and that is worth knowing: measured on the example intent above, adding a `waterLanes` rect
reads back verbatim after a save and generates a `water-lanes` region beside the other named ones — the tree
goes from eight to nine — while nothing anywhere in the tool shows it. Lanes are the Plan tool's to author.

**The buildings are not choosable.** Spawn points, wool sources, their protections and their rooms are all
placed here, and on a plan-built map each of them gets a stamped shell at export — a spawn building, a wool
cage. Which shell that is comes from the room styles bound in the **Sketch** tool's Theme phase, and Configure
offers no control for it.

**Opening a corpus map does not make it configurable.** Any map in the database can be opened, but a corpus
map has no intent: the phases show empty slices, nothing is pre-filled, and there is no path that derives one
from an existing `map.xml`. Configure authors an intent; it does not read one back out of a finished map. To
look at a corpus map's XML the tool is **Edit** — which is a technical inspector rather than an authoring
surface, supports **CTW only**, and is not going to grow DTC or DTM. That gap is exactly what the intent model
closed for new maps.

**A monument's Y is not editable.** The side-view seats team spawns, the observer and wool sources on their
terrain column, but monuments are neither seated nor adjustable, and moving any placed point through the
coordinate inputs rewrites X and Z without re-snapping Y to the new column (`N08`, `N11`).

**Nothing here judges how a map plays.** Pre-flight asks whether the document is well-formed, whether the
halves agree, whether placements stand on ground, and whether the objectives are reachable. Whether the map is
any good to play is a human's.
