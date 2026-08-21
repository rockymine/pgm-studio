# The Edit tool

## What it is

Edit opens a map that already exists. Its route is `/maps/{slug}/edit`, and what it shows is a `map.xml` that
has been parsed into the database — the teams, spawns, wools and the whole region tree, laid out as panels and
a canvas so they can be read and adjusted directly.

It is the studio's **first tool**, and knowing that explains its shape. It was built to read the corpus back:
open a map somebody else authored, see what its regions actually are, work out how a real map is put together.
Authoring through it was possible but technical — a map is a few dozen regions, a filter registry and a stack
of apply-rules, and building one meant typing all of that. That is the job `MapIntent` and the Configure tool
took over, and Edit kept the one it was built for.

So the two tools are opposites and it is worth stating plainly. **Configure edits a stated intent and
regenerates the document from it; Edit edits the document itself.** One is a projection, the other is the
projection's output. Nothing in Edit is derived, nothing is validated, and nothing is regenerated: a change
here is a change to the map.

**It is CTW only, and that is settled rather than pending.** The Objective phase reads a map's wools and
nothing else; a destroyable or a core is invisible in it. DTC authoring lives in Configure, which has a Cores
phase with a detector behind it, and DTM will land there too — not here.

## What it writes

The map's own entities, through the entity endpoints, one edit at a time. There is no draft and no artifact:
most fields commit as they change, and the two forms that batch — Identity and the build height — save on
their own button.

Each write follows the same path — read the whole document, apply one targeted edit, save the whole document
back through the codec (`WriteSupport.RunEditAsync`). That is what makes the tool safe on a map it does not
fully understand: everything it does not touch rides through unchanged. A map with eight destroyables keeps
all eight across an Edit save even though no panel shows one.

What it can write: the map's metadata and authors, its teams, its spawns and the observer spawn, its wools and
their monuments, its maximum build height, and its regions — renamed, re-coordinated, deleted, grouped into a
union or ungrouped back out.

It writes **no world and no intent**. A map edited here has no `map_intent_json`, so nothing downstream treats
it as an authored map: it is not pre-flighted, and it exports unconditionally.

## What it edits

The PGM document, as the studio's relational model of it. There is no second format and no document of Edit's
own — the panels are views onto `GET /api/map/{slug}`, which is the map as the codec reads it: `teams`,
`spawns`, `wools`, `regions`, `filters`, `apply_rules`, `kits`, `renewables`, `spawners`,
`block_drop_rules`, `destroyables`, `max_build_height` and the metadata around them.

A **region** is the unit most of the tool works in, and its shape decides what the inspector offers: a
`cuboid` and a `rectangle` take min/max corners, a `cylinder` a base plus radius and height, a `circle` and a
`sphere` a centre and a radius, a `point` and a `block` a single position. A compound region — a union, a
complement — carries children rather than coordinates, and the inspector lists them so the tree can be walked
downward from any node.

## Phases

Six phases on the nav rail, and unlike Configure's wizard **there is no order and no gate**: any phase can be
opened at any time, because a finished map has no incomplete state to protect. Each phase carries a flow bar
whose Back and Next simply walk the rail, and past the last one Next leaves for the maps list.

Two rail buttons sit beside them permanently disabled — **Filters** and **Export**. Neither has a phase behind
it.

### Identity

The map's name, version, objective text and authors, edited against the stored metadata directly and saved by
an explicit button. The difference from Configure's Identity is the point: there, version and objective are
derived by the generator and not editable; here they are the map's own fields and freely typed. The
**gamemode** is shown disabled in both, and for the same reason — it is derived from which objective modules
the map carries, so it cannot be set by hand.

### Setup — Islands · Symmetry

Two confirm steps over the shared world canvas, the same pair Configure's World phase uses. **Islands** lists
the detected landmasses and excludes the strays; **Symmetry** shows the detection over the base layer and
takes a confirmation. Detection runs on the studio's cleaned base — there is no scan-layer choice, no block
exclusion, and no world re-scan — so excluding an island recomputes symmetry from the islands already
detected rather than reading the world again.

### Teams

The teams as a list with an inspector: id, name, colour, dye colour, min and max players, added and removed
directly. Beside them the spawn regions, split by subtype into the **spawn points** where players materialise
and the **protection** zones around them, drawn on a spawn-filtered canvas.

Selecting a spawn region gives it a team, a yaw and a **kit** — a free-text box naming the kit the spawn
grants. That box is the only kit surface anywhere in the studio, and it names a kit rather than defining one.

When the map's detected symmetry suggests a team count, a smart-suggestion callout offers to create them.
Region **drawing** is not in this phase: a spawn region has to exist before it can be assigned.

### Build Regions

Two steps. **Build height** is the `max_build_height` cap, dragged on a side-view of the terrain or typed.
**Buildable layer** is the build-area region tree with the canvas and the inspector.

This is the one phase whose inspector is fully wired — the only place in Edit where a region can be **renamed
or deleted** from the panel.

### Objective

The map's wools: colour, owning team, the `<wool location>` where it dispenses, and the monuments that capture
it, each with a team and a location. Beside them the wool regions, collected into the three groups an author
thinks in — **rooms**, **monuments** and **spawners** — which takes some work, because objective regions do
not sit in one place in a real map's tree: on a traced map the monuments are carved-out children of a spawn
complement rather than roots of a wool group, so the collector walks every group and keeps the top-most node
of each subtype.

Drawing is deferred here too, and this phase is where the CTW-only boundary is: no destroyable, no core.

### Regions

The whole region tree, and the phase that comes closest to authoring. The sidebar is the tree, the middle is
the canvas, the right is the inspector. Selection is multiple — Ctrl or ⌘ click adds to it — and **Ctrl+G is
the one authoring verb**: two or more selected regions group into a union, and a single selected compound
ungroups back into its children, which are then selected so Ctrl+G again re-groups them.

Geometry is editable two ways that stay in step: dragging a shape on the canvas persists its new footprint,
and typing a coordinate in the inspector pushes the recomputed footprint back to the canvas. A server refusal
reloads the canvas rather than leaving the screen ahead of the map.

## What it refuses

Almost nothing, because there is nothing to complete.

**A wired compound will not ungroup.** A region with an apply-rule wired to it answers
*"…" has a rule wired to it — unwire it before ungrouping*, because ungrouping would leave the rule pointing
at nothing. That one is the **client's** guard rather than the server's: the endpoint would dissolve it, and
the panel is what knows a rule is attached. A primitive will not ungroup either — it has no children — and
fewer than two selected regions will not group.

**The endpoints refuse what the document model refuses, in the studio's one envelope.** Every write route
answers `{error, message, findings[]}` — the shape `docs/refusals.md` states — so the toast reads the
findings' sentence rather than a status, and an agent keys on a rule id. There are eight faults behind the
write routes, and six of them are the request's own rather than the editors': an id naming nothing in the
document is `RQ4` at **404**, a required field absent or a value outside a closed set or a number that is not
one is `RQ1` at **400**, an id already taken is `RQ5` at **409** — and `RQ5`'s finding names what is in the
way in its `subjects`, so the id already holding the name comes back with the refusal rather than having to
be hunted.

Two faults are the editors' own, because both are about the **document** rather than the request: the payload
is well-formed and everything it names exists, and the edit still cannot be applied.

| Rule | Refused | Status |
|---|---|---|
| `ED1` | a reference the document cannot resolve — an apply-rule or filter naming a region or filter that is not in the registry and is not a builtin, or a filter naming itself. No route here raises it: the two editors that do are reached from the intent generators | 400 |
| `ED2` | an edit the document is not in a state to take — a group with fewer children than its type takes, an apply-rule carrying no region, filter or action | 400 |

A refusal shows as a toast and leaves the screen as it was; a rejected canvas edit reloads the canvas so the
shape on screen goes back to the shape on the map.

There is no pre-flight, no completeness gate and no export gate. Nothing here asks whether the map is
playable.

## The API

Every endpoint is anonymous and rooted at `/api`. These are the **entity** endpoints — they edit a map's
document directly, which is what separates them from Configure's single intent PUT.

**Reading**

| Endpoint | Answers |
|---|---|
| `GET /map/{slug}` | the whole parsed document — teams, spawns, wools, regions, filters, apply-rules, kits, the lot |
| `GET /map/{slug}/regions/tree` | the region tree, grouped by category, which is what the sidebars render |
| `GET /map/{slug}/regions` | the flat registry |
| `GET /map/{slug}/islands` · `/symmetry` · `/configure/{slug}/state` | the Setup phase's detection |

**Writing**

**Their success shapes are as uniform as their failures.** Most of them hand back nothing — the caller posted
what is in the row, and the read that answers the stored form is a route of its own — so a delete, a spawn
link and both intent writes all answer `{}`. Four answer the id the caller now names the thing by: a created
region, a grouped compound (with the footprint it covers), a dissolved one (with the children it freed) and a
fanned one (with the counterparts it made). Three answer the row itself — a wool, a monument, a team — **in
the same shape `GET /map/{slug}` carries it**, because it is the same wool. Every one is declared in
`Contracts/EditDtos.cs` and published in the schema; the editors that build them sit a project below, so the
routes declare the shape rather than mapping it, and `EditAnswerShapeTests` holds each record to what its
editor writes.

**And each one says what it takes.** The sixteen bodies are declared in `Contracts/EditRequests.cs` — every
field with what it means, what an absent one falls back to and which refusal it draws — so `/api-docs` offers
the form and a generated client carries the names. They are declared rather than bound: the route still reads
the body key by key and hands the dictionary to the editor, which is what keeps a **partial** edit partial. An
absent field is not a null one. On a create it takes the editor's default; on an update it leaves the value
alone, and an explicit `null` clears a location, a team or a region reference. `EditRequestShapeTests` holds
each record to the editor that reads it, by posting the record's own serialization through the editor and
asserting the edit landed — the mirror of the answer gate, and the necessary one, because a request field
spelled wrong is a key the editor never sees and a 200 that changed nothing.

Their failure codes are uniform too, because they all run through one path — `WriteSupport.RunEditAsync`, which
turns the editor's refusal into the envelope: **400** (`RQ1`, `ED1`, `ED2`) for a payload the document will
not take, **404** (`RQ4`) for an unknown map, region, team, wool, monument, spawn, filter or apply-rule, and
**409** (`RQ5`) for an id already in use. Payload validation runs **before** the lookup, so a malformed body
aimed at something that does not exist answers 400 rather than 404.

**Every one of them rewrites the whole document**, which is what makes two editors a problem: an edit reads
the map, patches it and writes all of it back, so two callers working on different parts at once keep only
the second, with no conflict and no finding. `GET /map/{slug}` answers the map's **revision** as an `ETag`,
and any write here may state it back as an `If-Match`; one naming a revision the map is no longer at is
refused as `RQ5` — the third 409 in that list — and one stating nothing writes as it always did.
`docs/refusals.md` carries the rule and why it is 409 rather than 412.

| Endpoint | Does |
|---|---|
| `PATCH /map/{slug}/metadata` | name, version, objective, max build height, authors |
| `POST` · `PATCH` · `DELETE /map/{slug}/teams[/{teamId}]` | the teams |
| `POST` · `PATCH` · `DELETE /map/{slug}/spawns[/{regionId}]` | a spawn's region, team, yaw and kit |
| `PATCH` · `DELETE /map/{slug}/observer-spawn` | the `<default>` spawn |
| `POST` · `PATCH` · `DELETE /map/{slug}/wools[/{woolId}]` | the wool objectives |
| `POST` · `PATCH` · `DELETE /map/{slug}/wools/{woolId}/monuments[/{monId}]` | their capture points |
| `POST` · `PATCH` · `DELETE /map/{slug}/regions[/{regionId}]` | create, re-coordinate or rename, delete |
| `POST /map/{slug}/regions/group` · `/ungroup` | union two or more, dissolve a compound |
| `POST /map/{slug}/regions/{regionId}/counterpart` · `/orbit` | mirror a region onto the other team, or round the orbit |

**Getting the XML out** is not Edit's, and the tool's own Export button is disabled. `GET /map/{slug}/xml`
answers the rendered `map.xml` for any map, corpus maps included, and `GET /map/{slug}/export` the world ZIP;
the Configure tool's Review phase is the surface over them.

## Driving it without the UI

An agent should almost never drive this tool. Authoring a map through the entity endpoints means writing every
region, filter and apply-rule by hand, and the whole reason Configure exists is that stating an intent and
letting the generator project it is both shorter and checkable. `configure.md` is the one to read.

Where the entity endpoints are the right answer is a **surgical change to a finished map**: renaming a region,
nudging a spawn's yaw, correcting a monument's coordinates on a corpus map. One PATCH does it, and the rest of
the document is untouched.

```
GET   /api/map/sentient                            → the whole document
GET   /api/map/sentient/regions/tree               → the tree: the region's id, type and current numbers
PATCH /api/map/sentient/regions/blue-spawn-point   {"bounds": {"min_x": 234, "min_z": 149,
                                                               "max_x": 238, "max_z": 151}}
GET   /api/map/sentient/xml                        → the rendered map.xml
```

**A region patch takes one of three envelopes and a flat body is refused**, which is the trap worth naming.
`{"id": …}` renames — cascading through the categories, every compound's child list, and any spawn or wool
room pointing at it. `{"bounds": {min_x, min_z, max_x, max_z}}` moves the 2-D footprint of any region and
answers the new bounds. `{"coords": {…}}` sets the type's own fields, and **for a `cuboid` that means `min_y`
and `max_y` only** — sending `min_x` under `coords` to a cuboid is accepted and silently ignored, because a
cuboid's footprint is `bounds`. A flat payload is a 400 carrying `RQ1` and the sentence *provide 'id',
'bounds', or 'coords'*, and that check runs before the region lookup, so a flat body aimed at a region that
does not exist answers 400 rather than 404.

**A refusal comes back in the studio's one envelope, whichever route produced it**, so one
parser reads them all and the rule id says which fault it was:

```
POST /api/map/sentient/regions/group  {"type": "union", "child_ids": ["blue-spawn-point"]}
→ 400 { "error":    "edit not applicable",
        "message":  "union requires at least 2 region(s)",
        "findings": [ { "rule": "ED2", "message": "union requires at least 2 region(s)",
                        "severity": "refusal" } ] }
```

`GET /api/rules?rule=ED2` answers what that id means and what to do about it, and the same holds for the
`RQ*` ids the write routes cite.

The ids in that example are real — `sentient` carries `red-spawn-point`, `blue-spawn-point`, `spawns`,
`obs-spawn-point`, `wool-rooms` and a monument per colour — and `blue-spawn-point` is a `cuboid`.

Reading is the other honest use. `GET /map/{slug}` is the fastest way to see how a real authored map is put
together — which is what the tool was built for, and what it is still best at.

## Limits

**It is CTW only and will stay that way.** The Objective phase models wools and monuments; a map's
destroyables and cores are invisible in it. `sentient` in the corpus carries four wools and eight
destroyables, and Edit shows the four. They survive an edit untouched, because every write saves the whole
document back — but nothing here can see them. DTC is Configure's Cores phase today, and DTM is Configure's
too once `N12` lands; neither is coming here.

**It cannot export.** The topbar's Export XML button is disabled and the rail's Filters and Export entries
have no phase behind them, so a map opened here cannot be turned back into a file from this tool. The XML and
the world ZIP are API endpoints, surfaced by Configure's Review phase.

**It cannot draw.** Teams and Objective both defer region drawing, so a spawn region or a wool room must
already exist before it can be assigned. Build Regions is the only phase with a canvas that creates anything.

**The inspector is only partly wired.** Renaming and deleting a region are offered in Build Regions and
nowhere else — Regions, Teams and Objective pass the inspector coordinate editing but no delete or rename
handler, so those controls are absent rather than broken (`C11`).

**Filters and apply-rules are not reachable here at all.** They are the machinery that makes a PGM map mean
anything — which team may break which block, when — and this tool was to be where an author typed them. The
intent model is the answer to that instead: a protection drawn in Configure applies the filters it needs, and
the plan tool states the intent that produces them, so the hand-authoring surface was never filled and its
routes are gone, reads included. `GET /map/{slug}` still carries both under `filters` and `apply_rules`,
which is where anything wanting to see them looks. A map whose rules need changing needs a text editor.

**Kits are named, not authored.** The free-text kit box on a spawn says which kit that spawn grants; nothing
in the studio states what a kit contains (`C9`).

**Nothing is validated.** There is no pre-flight, no traversability check and no export gate, because a map
opened here is one that already exists. Editing it into something PGM will not load is entirely possible, and
the tool will not say so.
