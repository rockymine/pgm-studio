# How the studio says no

Every gate in the studio answers in one shape. A plan refused for its structure, a house style refused for its
blocks, a building refused for how its wings meet, a map refused at export for its gamemode: all of them
produce **findings**, and a client rendering a refusal never has to know which gate produced it before it can
read one.

## A finding

A finding is a **rule id**, a **sentence**, and **what it is about**.

The id is the machine-legible half and is stable forever — an agent or a canvas reads it back and acts on it,
so it outlives the task that added it and never doubles as a task-tracking id. The sentence is the human half
and carries the measured numbers, because "invalid" tells an author nothing they can fix; a refusal says *this
one is 2 × 8*, not *this one is too small*. What it is about is a **field** where a document field is
nameable and **subjects** where the fault indicts pieces, props, zones or wings the editor can highlight on
click — and a finding may carry either, both or neither, because different gates know different things about
what they are refusing.

Two fields exist for cases the first three do not cover. **Severity** is one of three, in descending order of
what it took. A **refusal** stops the work. A **decline** says the work happened and one piece of what the
author wrote is not in it — the tree, boulder or building the dressing pass could not seat is not in the
world, and no amount of ignoring it puts it back. A **complaint** rides along with work that lost nothing,
which is how a compile that produced a playable-but-goalless map says so without blocking. The distinction
that matters is the middle one: a caller reading a 2xx has no other way to answer *did what I posted survive*.
And **cites** is what to look up next where the rule id is
not it — the layout rule the fault falls under, or the open task that would resolve it. That last one is why
`cites` is a field of its own rather than a second id: a rule is stable forever and a task id is a debt with a
due date, and one field holding either would make the two indistinguishable to a reader.

**A finding states its edit where the fix is mechanical.** A seam that steps wants a ramp mark; a house on
falling ground wants a bench held flat under it; a prop inside a road's standoff wants moving a stated number
of blocks the other way. Where the gate can say that in the document's own vocabulary it does, as an
**edit** beside the sentence: which document (`plan`, `layout` or `intent`), the path the change lands on,
spelled the way an unread field is (`relief.team.marks`; an array element by its `id` in brackets where it
carries one, by its index otherwise — `dressing.props[erratic-broken]`), one of three operations — `add`
appends the value to the array at the path, `set` replaces the value there, `move` sets the `x` and `z` the
value carries on the object there — the value as the document would carry it, and the change in words. A
reader applies it rather than re-deriving it from the rule's prose, which is the half of a finding a model
acts on where the sentence alone is inspected and left. `SP8` and `EL1` state the ramp, `WX11` the bench and
`DR-ROAD` the move; a finding whose fix is a decision rather than a change carries none.

```json
{
  "rule": "SP8",
  "message": "spawn egress steps 5 blocks at 'crag'–'brow' — use 1-level steps or a ramp against the spawn",
  "severity": "complaint",
  "subjects": ["crag", "brow"],
  "edit": {
    "document": "layout",
    "path": "relief.team.marks",
    "op": "add",
    "value": {"id": "ramp-crag-brow", "kind": "line", "width": 6, "points": [[-51, -50], [-39, -50]], "h": [27, 22]},
    "says": "a line mark six wide from (-51, -50) at 27 to (-39, -50) at 22, so the seam grades over 12 blocks"
  }
}
```

**What a finding does not carry is the rule's own classification.** A category and a concerns list are fixed
by the id, not by the raising site, so they live on the rule and a caller joins them from `/api/rules` — which
is the same lookup an id was already worth keying on for. Putting them on the finding would put one fact at 96
sites.

`Finding` is in `PgmStudio.Vocabulary`, a leaf that references nothing and that all three parties reach: the
gates below `Api` that raise one — `Minecraft`, `Pgm`, `Analysis`, `Export` — the HTTP surface that answers it,
and the WASM client that renders it. That is what makes the JSON above the record itself rather than a copy of
it: the three optional fields are written only when they have a value and the two computed properties are not
written at all, wherever the serialization happens. Nothing that is not a finding is forced into the shape: a
`TermScore` is a distance that *carries* a finding, and a distance is not a fault.

```json
{
  "rule": "HJ2",
  "message": "they touch over 3 blocks of edge but not over all of the shorter one, so part of the wing's end meets its neighbour and the rest hangs over open ground — neither joint can happen",
  "severity": "refusal",
  "field": "wings",
  "subjects": ["0", "1"]
}
```

## A refusal

A refusal is the gate's short label, one line for a caller that wants a sentence, and the findings themselves
for one that wants to act:

```json
{
  "error": "invalid house style",
  "message": "doorHead.block (5) is not a stair. …",
  "findings": [ { "rule": "HS1", "message": "…", "severity": "refusal", "field": "doorHead.block" } ]
}
```

`error` names the **gate** — `objective placement`, `unknown gamemode`, `plan not compilable` — and never the
fault itself, which is what the findings are for; it exists so a client can show something useful before it has
looked at a single finding. `message` is the findings' sentences joined with `; `.

The status code stays the gate's own: **400** for a document that is wrong as posted, **404** for a subject
the route names and the studio does not have, **409** for one that is well-formed but conflicts with the map's
state, **422** for one that cannot be processed, **500** for a fault that is the studio's own. That is a fact
about the request rather than about the fault, so each endpoint names its own.

**And each endpoint declares which**, so the codes reach a caller from the schema rather than only from a
document. The 400 and 500 are published once, by the configurator every route passes through; the other three
are this route's own and are stated at it, through `Answers.Refuses` — **95 of the 149 operations** carry at
least one. Declared per route rather than derived from the path: a path holding `{slug}` nearly always answers
404, and a schema that guesses is the one thing a caller cannot act on. The `Fails with` column of every
endpoint table in `docs/tools/` names exactly the same set, and `DocumentedFailureTests` fails if a row and its
route stop agreeing.

**The edge answers it too, not only the gates.** No route under `/api` answers a failure in a shape of its
own, and none answers one with no body at all: `Refusals` writes every one of them, so a caller writes one
parser for a refusal — which is the only reason to have one envelope. **The binder is part of the edge**:
a bound request that will not read is refused before any handler runs, and
`Refusals.UseRefusalEnvelope` turns the framework's own `{statusCode, message, errors}` into `RQ1` per field
under `error: "request will not read"`. `RequiredFields` answers the other half — a field that is missing
rather than unreadable — as `RQ1` under `error: "incomplete request"`. Both name the field **as the wire
spells it**, which is the only name a caller can look for: a property stating its own JSON name reports
`region_id`, not the `regionId` its record declares. The Edit tool's thirty-six write routes
answer it through one path: `EditException` carries the finding, and `WriteSupport.RunEditAsync` writes
`Refusals.Of` with it.

Complaints travel the same way but on a **success** response, under `warnings`, since nothing was refused —
and the response carries them whether or not the endpoint thought to, which is *What a success carries* below.

## The rule ids

Ids are grouped by what they are about, never by which gate happens to ask. The same objective rule is asked at
the compile gate against a plan's pieces and at the export gate against the ground the rasterizer produced, and
a rule that changed its name between the two would be two rules.

| Family | Owns | Where |
|---|---|---|
| `PL*` | the plan's own structure | `Pgm/Plan/PlanValidator.cs` → `PlanRules` |
| `DC*` · `OB*` | destroyables, cores, goal placement, gamemodes. `OB24` is the one that needs the built world rather than the plan: two goals stamped into the same blocks, which the plan cannot see because it reads two placements at two coordinates and the volumes around them are the stamper's. Two are **complaints on a built world**: `DC3` a goal built in a material its own size is wrong for, `OB23` a goal topping out over the build ceiling — the world is built and the goal stands, which is what makes them complaints rather than declines. `OB19` is the decline beside them: the prop the pass turned away is not in the world at all. `OB25` is a third complaint on a built world and the only one about a field rather than a shape: a wool monument's location is filled from the air cell the capturing team's spawn structure stamped, so an authored one is replaced — three write paths take a location and every read answers it, which is what makes the replacement worth a sentence rather than a silence. On a map the studio did not build there is no stamp and the stated location is authoritative | `Domain/ObjectiveRules.cs` |
| `WX*` | room frames — the footprint and where it sits on its piece, the pad, the doors, the iron, and the shell's height against the build ceiling | `Domain/RoomFrames.cs` → `RoomFrameRules` |
| `HS*` | a house style's own materials | `Minecraft/Houses/HouseStyleValidation.cs` → `HouseStyleRules` |
| `HP*` | a placed building's shape | `Minecraft/Dressing/PlacedProp.cs` → `HousePropRules` |
| `HJ*` | how two wings meet | `Minecraft/Houses/WingJoints.cs` → `WingJointRules` |
| `DR-*` | the dressing pass's own — `DR-DOC` a document that will not parse, `DR-ROAD` a prop resting nearer to the road than its kind's standoff (the numbers live on `PlacedProp.RouteStandoff`), `DR-PASS` a building leaving no five-block passage beside any side, `DR-SIZE` a building whose box is under 5×5 blocks, `DR-KEEP` a prop resting on ground the map keeps clear (a spawn, a wool room, a stated structure, a built column, a sketch shape marked `keepClear`, a door's approach), `DR-CLAIM` a prop resting on ground something already standing holds on the same layer (a building holds what it stamps plus a block of ring beyond it; the claim book is keyed on the layer, so a prop over a channel it does not share ground with is not this), `DR-CUT` a prop the clip cut in two — a prop is asked about at its **feet** and written wherever the rest of it meets air, so one that seats clear of a building may still reach into it, and where the obstruction takes out the blocks joining a limb to the body the far half of that limb is written into open air with nothing holding it; counted against the prop as it would have stood on open ground, since a crown gathered at the branch tips is several pieces to begin with, and raised off what was **severed** rather than how much was lost — a rock flattened along a wall loses a third of itself and severs none, while a twenty-course tree eight blocks clear of a wall places 429 of its 430 blocks and leaves a limb floating. A second arm reads the share the ground **blocked**, which is the same fault from underneath: a prop seats on the lowest column its feet cover, so on stepped ground its body is written into terrain standing over it, the remnant that lands rests on real ground and severs nothing, and past half the body blocked what stands there is not the prop that was placed. A complaint: the prop is in the world as the clip left it. `DR-LAYER` a prop naming a layer the board has no ground on — declined rather than seated on the top surface, which is the layer the author was saying they did not mean; `DR-SITE` a prop with no ground to rest on — a building is held to every cell of its footprint, and the finding names the first bare column; `DR-SLOPE` a building whose footprint is not level enough to stand on — a building seats on its lowest column and the ground over that floor is carved out of it, so a site rising by as much as the building itself stands builds a house whose uphill side is under the ground beside it, roof and all. Every one but `DR-DOC` is a **decline on a built world**: the world is built and the prop is not in it | `Minecraft/Dressing/DressingJson.cs` · `Minecraft/Dressing/GroundClaims.cs` → `DressingRules` |
| `EX*` | the export gate's own — `EX1` not traversable, `EX2` no spawn to enter the map by, `EX3` what the intent stated and the document did not carry, `EX4` an objective with no team to contest it, `EX5` an observer platform standing higher than the intent asked because the board builds over the point it named, `EX6` a map naming no author, which leaves the observer platform's authors board off rather than standing a heading over three blank lines. `EX5` and `EX6` are complaints on a built world | `Export/MapExportComposer.cs` → `ExportRules` |
| `SK*` | the sketch document's own — `SK1` a relief the recompile does not keep — a **refusal** where the new geometry has no group at all for terrain the author drew, and a **complaint** where the merge carries the stored relief over one the body posted for the same group, since the second case builds the terrain the board already had while both reads of it disagree and are correct; `SK2` a board whose extent is past what the studio will realize (the one refusal; the ceiling is a constant and deliberately appears in no message — a stated one is a target); `SK3` a name matching nothing (a shape kind, a mirror mode, a group's shape id, a relief's group, a relief's landform, a shape's or the map's theme) — the landform because the gate that judges a group's ground against its stated word skips a relief stating none, so an unknown word or the wrong case turns that gate **off** rather than failing it; `SK4` a shape that draws no ground; `SK5` a column the world cannot hold; `SK6` nothing stored to finish, `SK7` a stored board that rasterizes to no ground, and `SK8` a board finished carrying no theme registry, no relief and no props — the three the finish stage owns, and the one place the sketch's complaints become fatal, since finishing is what declares the drawing done; `SK9` two shapes stacking over one another **on a single layer**, which a layer cannot hold — it keeps one span per column and a taller add replaces a shorter outright, floor included, so the lower shape is not in the world and this is a **decline**, the severity for an input that did not survive; `SK10` two **layers** claiming the same blocks in one column, so the layers build as one solid mass and the gap drawn between them is not in the world; `SK11` a mass of standable ground that stands **over** other ground, under open sky, and that nothing joins to the rest of the board — a mass merely *beside* another is a landmass and stays silent, as does roofed ground, being a room. `SK12` two groups of one layer answering to the same id — a group id is the key a relief is stored under and the handle a placement names, so a board carrying it twice has no single answer to either and the terrain lands on whichever group is solved first. `SK13` a shape drawn over ground a **subtract** takes away — a subtract states the board's negative space and a hole is never scenery, so an add that **fills** one is **refused at `finish`** (no sketch-stage route refuses on the board's geometry — the write, the merge and every preview take the board and ride the findings back on `warnings`, so an edit passing through a state the finished board would not allow does not cost the author the shapes they drew to get there) (an override add, or any add on another layer, since a subtract reaches only the layer it is on); an override add resting **above** the subtract's own floor is a lid rather than a fill — a layer holds one span per column, so the span moves up and the void under it stays void and an add that draws **nothing** there merely complains; a subtract *following* an add **on that add's own layer** is its hole and says nothing, which is what separates a fill from a donut, while across layers the order is a height rather than a sequence. `SK14` an **override add** stating a top its group's relief will solve straight through — an override add says the column is its own, floor and all, and a relief replaces the top of every column of its group, so a made thing naming neither a `height_mode` (which stands it out of the field) nor a `relief_scope` (which keeps its ground out of the solve) builds to whatever the field says and its own number is nowhere in the world — a top has to have been stated to be discarded, so an override add carrying no `base_height`, `floor` or `anchor_heights` is a footprint holding a theme and is outside this. `SK15` one shape building a column and another painting it — two override adds over a column is not a fault, the taller wins it, but a theme is scoped by **area** and not by height, so where the smaller of the two is also the shorter the world holds the taller shape's ground in the smaller one's material; two shapes at one height are a theme scoped to a patch and are not this; a shape in a mirroring group is judged at every image of its orbit, since a patch contests another patch's reflection as readily as the patch itself. `SK16` a **made thing seated on nothing** — a layer stating `seat: ground` takes its floors from the lowest solid column under its own footprint, so a thing whose footprint covers no ground at all has nothing to measure against and stays at the height it was drawn; a complaint, since a balloon, a ship in the air or a statue on a spire is a legitimate board and the way to say so is to state no seat. `SK17` a shape **no group on its layer lists** — a group is the unit the symmetry orbit is fanned by, the build reading each mirroring group's `shapeIds` and copying those shapes onto their images, so a shape no list names is built once on the side it was drawn on and has no image anywhere; the group's relief and its keep-clear travel on the same list, so an unlisted shape takes neither; a layer stating no groups at all is outside it, the whole of such a layer mirroring, as is a role-tagged room piece, which is never listed by design. `SK18` a **made thing standing in something built** — a made thing is laid by the rasterizer before anything is stamped, and every stamper seats on the terrain's surface, which is every column's top with the made things taken out, so neither half asks about the other: their blocks interleave in the columns they share, the later pass winning each cell it writes, and what stands there is a balloon with a house inside it. Nothing else reaches it — `SK10` skips a made-thing layer by design and a stamped structure is not a layer at all — so it is read off the finished world's **provenance**, the one place all four passes have registered, and grouped per made thing and per built thing so one gantry through one shed is one sentence. A complaint: a thing deliberately drawn around a structure is a legitimate board, and which of the two moves is the author's call. `SK19` a placement **naming a recipe the document does not state** — a tree, a boulder and a building each carry a key into the layout's own `dressing.styles`, and a key the registry has no entry for names nothing at all. Every read of the dressing refuses it already, so what the gate adds is *when*: it is a **refusal**, which the finish acts on and the save reports as a warning while still storing the board, since a save that fails halfway through authoring is worse than a board carrying a fault. A placement naming *nothing* — an empty key — is outside it: that is a prop put down before a recipe was picked, and it builds the kind's own default the way a sketch binding no room style stamps the built-in shell. `SK20` a **stack whose list order and `base_y` disagree** — a layer's position in the list is its draw order and `base_y` is the height its ground starts at, so a later layer starting below an earlier one reads as a stack that is not the one it builds: a reader walking the list meets the storeys out of order and a strip drawn from it puts the cellar above the roof. A complaint, never a refusal — the world is built from `base_y` and comes out exactly as stated, so what is wrong is the document, and a board mid-authoring is allowed to have a layer added before it is raised. `made` layers are outside it, a sculpture's slices sharing one footprint and having no stacking order between them. `SK22` a **height per vertex the shape's kind has no reader for** — `anchor_heights` is interpolated across a footprint as a TIN over the shape's own ring, which only a polygon and a lasso have, so a rectangle, a circle or a polyline stating one builds its `base_height` the whole way and the author's numbers are nowhere in the world; a polygon whose array is not the length of its ring is the same silence for the same reason, the TIN being built one height to one vertex. A complaint, since the board still builds and what it builds is flat rather than absent. `SK21` a **bend that could not pull every point it cut** — a coast is drawn by resampling an outline's long edges and pulling each inserted point to the side the bend asks for, and a point whose two offsets both land on the wrong side, a neck or a notch narrower than twice the wander, stays on the edge it was cut from. The outline is still drawn and every other point moves; what is lost is that those stretches come out as straight as the plan drew them, so the count rides on the answer rather than being a fault in the document. `SK23` a **theme scoped to a shape it cannot show itself on** — a theme chooses its bucket per column by whether that column is an edge, so a shape with no interior column (no cell with ground on all eight sides) is an edge everywhere under every `rimEdges` setting and only its rim and its wall ever paint: a two-block stilt themed like the platform under it is one course of the rim material over the wall material and the theme's surface is nowhere on it. A complaint, and grouped **per layer and theme** rather than per shape, since the decision that answers it is one — turn that theme's rim off, or state what those shapes are made of with `material` (`TP22`) — and a board drawn out of small pieces raises hundreds; judged only where the rim paints, a rim turned off letting the top fall to the surface exactly as written. Raised by the theme gate rather than by the sketch check, needing the registry as well as the ground, and named in the sketch check's ground rules so a shallow reading says it did not walk it. `SK24` a shape stating both a **`theme` and a `material`** — the two answer one question at two grains, the build takes the material as the narrower statement, and the theme is read by nothing; a refusal, the world being fine and the document being what says two things. `SK9`–`SK11`, `SK13`–`SK16` and `SK23` are the eight read off the **rasterized spans** rather than off the document, because none of them is visible in what a layout says, only in what it builds; `SK10`, `SK11` and `SK16` are complaints, since a two-thick plinth, a detached landmass and a thing hanging in open sky may each be what was drawn; `SK10` and `SK11` also skip a layer stating `kind: made`, a made thing being neither a deck losing its gap nor standable ground missing a stair. `SK3`–`SK5` are **complaints on a built board**: the rasterizer is set algebra, so what it cannot read contributes no ground rather than failing, and without these a defect in the document reads as a smaller drawing | `Pgm/Sketch/SketchRules.cs` · `Pgm/Sketch/SketchLayoutCheck.cs` |
| `IM*` | the import's own — `IM1` a host the import does not fetch from (the SSRF allowlist), `IM2` an archive the host did not serve, `IM3` one past the download cap, `IM4` one that is not a zip, `IM5` one carrying no `region/*.mca`, `IM6` a folder that is a map already rather than a world to originate from | `Api/Endpoints/ImportEndpoints.cs` → `ImportRules` |
| `CO1` | the composer's — a well-formed descriptor naming a board it cannot emit; the sentence carries which knob and which value, because the emitter that stopped is the only thing that knows | `Pgm/Compose/ComposeException.cs` → `ComposeRules` |
| `RL*` | a solved relief measured against what its island says it is — `RL1` ground that is not the landform stated, `RL2` elevation that was never graded. Complaints: a relief is authored ground, and the studio measures it rather than overruling it | `Analysis/Playability/ReliefRules.cs` → `ReliefRules` |
| `EZ1` | ground a player can stand on and nobody can edit, outside the zones a map seals on purpose — a canopy over the void past every build zone, a crag the void rule closed with the air around it. Recognised as protected by the `enter` rule that protects it rather than by a name, and reported per patch with its box. A complaint: inert ground may be exactly what was drawn | `Analysis/Playability/EditZoneRules.cs` → `EditZoneRules` |
| `RQ*` | the request itself — a document that could not be read, a subject the route names and the studio does not have, a request conflicting with what is stored, a stored document that will not read back, a field that went unread, and a fault that is the studio's own | `Domain/RequestRules.cs` |
| `ED*` | the document editors' own two — `ED1` a reference the document cannot resolve (an apply-rule naming an unknown filter, a filter naming itself), `ED2` an edit the document is not in a state to take (a group with fewer children than its type takes, an apply-rule with no region, filter or action). The other six an edit is refused for are the request's, above | `Pgm/Editing/EditRules.cs` |
| `CT` `SP` `WL` `LN` `HB` `FR` `MD` `BZ` `EL` `G*` `ST*` `GO*` | the layout-rules checklist, cited by the plan lint, the evaluator terms and the producibility read | `docs/generator/rules.md` |
| `PC-C` | a corner contact between pieces nothing else joins — the one of that checklist a gate raises under its own name, so it is declared rather than stated | `Pgm/Plan/PlanValidator.cs` → `PlanRules` |

## One question, asked at every grain

The families read as a flat list and they are not one. **The same question is asked at each level a map is
described at, and each level's answer can be undone by the level above it.** That is the whole reason the
studio has four documents rather than one, and it is what lets a refusal be precise about *which* thing is
wrong rather than saying a map is bad.

Reachability is the worked example. Five rules ask it, over five grains, and nothing but this paragraph says
they are one family:

| Rule | Asks | Over |
|---|---|---|
| `WX6` | can a door be cut into this room at all — is anything abutting it? | one piece of the plan |
| `PL9` | can a capturing team's spawn reach this wool at all? | the whole plan |
| `EX1` | is the ground the rasterizer built connected? | the built world, as places a walk runs over |
| `DR-PASS` | does this building leave five blocks to get past it? | one prop on that ground |
| `DR-SLOPE` | is this building's own footprint level enough to stand on? | one prop on that ground |
| *coverage* | is the ground that **can** be reached ground any journey actually uses? | the built world, as a measurement rather than a gate |

Read down the column and the escalation is the point. `WX6` is local and structural; `PL9` is the same
question at board scale; `EX1` asks it of ground rather than of rectangles, because a plan that passed can
rasterize into something that does not; `DR-PASS` re-opens it after a house lands on ground that was already
walkable; and coverage asks the question the four gates never do — not *can* a player get there, but *does
any route go*. A board can pass every gate above it and read a third dead.

**Where a gate is asked decides who meets it.** The export's whole chain for a sketch map — `OB20`, `SK2`,
`OB17`, `EX1`, then `EX2`/`EX3`/`EX4` — is inside `MapExportComposer.BuildAndCompose`, the method the
headless driver links directly, so a gate cannot fire on one front door and not the other. `Compose` keeps
only the leg a map that ships its own world takes, and asks `EX1` there over the scanned segments; the sketch
leg asks it over the ground that build just rasterized, because that is the world about to be written.

**A finer grain exists only because a coarser one does.** `DR-PASS`'s five blocks are meaningful because the
plan already said this ground is walked; `PL9`'s answer is meaningful because the intent already said which
team captures which wool. Collapsing the four documents into one would not simplify the rules — it would
remove the vocabulary each rule uses to say precisely what is wrong.

The structural plan rules, in full:

| Rule | Refused |
|---|---|
| `PL1` | no generating piece — there is no land to build |
| `PL2` | no spawn — PGM has nowhere to put a player |
| `PL3` | no objective of any kind (**complaint** — which goal a map carries is the author's) |
| `PL4` | two pieces claim the same ground at incompatible heights |
| `PL5` | a placement names a piece the plan does not have |
| `PL6` | a placement stands on a buffer, which produces no terrain |
| `PL7` | a placement falls outside the piece it names |
| `PL8` | a spawn room cannot seat every monument its team will capture |
| `PL9` | a wool cannot be reached from a capturing team's spawn |
| `PL10` | a destroyable style names something that is not a style |
| `PL11` | a wall is drawn on a pair sharing no land interface |
| `PL12` | a connected landmass mixes fanned and non-fanned pieces, so it has no coherent orbit image |
| `PL13` | a bedrock wall is drawn on the wool room's own interface — the wall and the room stamp through each other; place it ~15 blocks out, on the approach piece's outer interface |
| `PL14` | a wool colour names something that is not a dye |
| `PL15` | the document states a shape version this build does not read — every coordinate under it would be measured in the wrong unit, so it is refused rather than guessed at, and it is reported alone |

And the building rules, which the dressing document and the room library are both held to:

| Rule | Refused |
|---|---|
| `HP1` | no rectangles at all |
| `HP2` | a wing is not two corners, or is thinner than a room |
| `HP3` | the wings cover more ground than a placed building may take |
| `HJ1` | two rectangles share blocks — wings touch and never overlap |
| `HJ2` | they touch over part of an edge only |
| `HJ3` | both ridges run along the shared edge — two ranges side by side |
| `HJ4` | both run into it — one longer range |
| `HJ5` | the wing stands taller than the hall it meets |
| `HS1` | a block named for a geometric role is not that kind of block |
| `HS2` | a doorway does not clear the least height a door may |
| `HS3` | a roof's materials are wrong for its pitch or its family |
| `HS4` | a part built of two blocks is built of two materials |
| `HS5` | an ore is used as a building material |
| `HS6` | a door is cut through a wall that is not there |
| `HS7` | a footing rings a plate one course deep — a complaint |
| `HS8` | a porch's canopy climbs past the wall it is attached to — a complaint |
| `HS9` | beams over a wall with no laid-log course to be the ends of |
| `HS10` | a house on stilts standing on a floor — a complaint |

## How a gate is called

**Every gate is `Check`, and answers `Findings`.** One verb, because seven — `Faults`, `Check`, `Refusals`,
`Validate`, `Findings`, `Errors`, `Completeness` — is six too many for a caller to remember, and one return
type, because the interesting question is not how many findings there are.

| Gate | Call |
|---|---|
| a plan's structure | `PlanValidator.Check(plan)` · `.Completeness(plan)` |
| a goal's ground | `ObjectivePlacement.Check(goals, isLand, keepOuts)` |
| a house style | `HouseStyleValidation.Check(style)` |
| a placed building | `house.Check()` |
| how two wings meet | `WingJoints.Check(plan)` |
| a sketch's bound room styles | `SketchRoomStyleGate.Check(layoutJson)` |
| a bound shell against the build ceiling | `RoomStyleScope.Check(style, field)` |
| a roof's own materials | `HouseStyleValidation.CheckRoof(roof)` |

A gate takes whatever it needs to answer — a plan alone, or goals plus the ground they stand on — so there is
no interface, and forcing one would only make the context-carrying gates lie about what they read. What is
uniform is the answer.

**`Findings.Refuses` is the question, and `Count` is not.** A gate reporting only refusals answers an empty
list for a clean document, so `Count > 0` reads as "refused" and happens to be right; a gate reporting
complaints as well answers a non-empty list for a document that is perfectly good, and the same expression
blocks it. Both kinds were being read the same way. Beside it, `Refusals` and `Complaints` split the list,
`Summary` is every sentence, `And` joins two gates' answers, and `Under(root)` prefixes a field so a style
bound twice onto one sketch reports `roomStyles.cage.doorHead.block` rather than a `doorHead.block` an author
cannot place.

`AsComplaints` is for the case that is not a gate at all. A **derivation** — the producibility read is the one —
answers a question no compile depends on, and its findings are the reasons the answer is no. Inside it those
are refusals, because that is the severity `Refuses` must see for the derivation to ask its own question by
name; they are downgraded where they cross to a caller, who gets them on a 200 beside an answer rather than
instead of one. Writing them as complaints at their source is what left `PlanProducibility` counting a list for
a release, which is the shape this whole section exists to remove.

An endpoint gates in one line — `if (await Refusals.StopAsync(http, 400, "invalid house style", findings, ct))
return;` — which writes only the refusals, so a complaint never arrives dressed as one.

## What a success carries

**Running the gate is the whole of an endpoint's duty.** `StopAsync` writes the refusals and answers false,
which is half the sentence: an endpoint that ran a gate is left holding the complaints too. That half is not
left to the endpoint, because a dropped complaint fails nothing — the status is the same, the body is still
valid, and the board reads as cleaner than it is, so nothing catches one. The complaints are handed to
`Complaints` (`Api/Endpoints`) and one middleware puts them on whatever success the endpoint goes on to
answer, which is how an endpoint gets the guarantee without knowing the channel exists. A finding produced
away from a gate — the props a dressing pass declined, the fields a reader had nowhere to keep — is handed
over the same way, by `Complaints.Add`/`Complaints.Unread`, and travels the same road from there.

**Two severities ride the key, and telling them apart is the point.** A complaint is a remark; a decline says
a piece of what was posted is not in what was built. Both sit on a success, so both go under `warnings`, and a
caller reads `severity` on each finding — `warnings.some(w => w.severity === "decline")` is *something I sent
was dropped*, which no status code and no other field says.

**One key, and one rule for when it appears.** A 2xx JSON object answers `warnings` when something was
complained about or declined, and carries no such key when nothing was. The header keeps the same rule: it is
there when something was, and absent otherwise. The single rule is what makes an absent `warnings`
readable: without it the key's absence covers four states a caller cannot tell apart — an endpoint with no
gate, one whose gate found nothing, one that dropped what its gate found, and one answering a shape with
nowhere to put it. A caller reads `warnings ?? []` and is done.

**A success that is not JSON answers the header instead.** `Pgm-Warnings` carries the count and then each
rule id once — `1 DR-SITE`, `3 DR-CLAIM DR-KEEP` — and is set when the findings are handed over, which is
before the body is written and therefore before a header is too late. It is what `GET /map/{slug}/export` and
`GET /map/{slug}/xml` answer: both build a world through the dressing pass, both drop props, and a zip and an
XML document have nowhere to put a key. The zip still carries `region/dressing-report.json` with the rule,
the cell and the prop for each; the header is what tells a caller that never unzips there is something in
there to read, and on the XML route it is the only answer there is.

**A rule that was never asked rides on its own key.** A partial write to the sketch — a shape, a vertex, a
layer, a group, a prop, a theme — takes the sketch gate's *document* reading: eight of the `SK` rules (`SK9`,
`SK10`, `SK11`, `SK13`–`SK16`, `SK23`) are read off the rasterized spans, and answering them walks every column
of the board, so a write names them on `Pgm-Unwalked` instead and `GET /map/{slug}/findings` answers them on demand.
It is a second header rather than a fold into `Pgm-Warnings` for the reason that key works at all: `warnings`
means *these were found* and its absence means *nothing was*, so a rule nobody asked cannot appear there
without making both readings of an absence ambiguous again.

The header rides on a JSON success too, beside the key rather than instead of it. `POST …/sketch/columns`
answers megabytes of column runs, and a caller deciding whether to look at the warnings should not have to
parse the payload to find out there are none.

One case sits outside both, and it is honest: a **refusal** carries refusals only, in the envelope's
`findings`, because the work did not happen and nothing rode along with it. Where a non-JSON success loses a
finding anyway — nothing handed it over before the response started, so neither carrier was available — the
studio logs it as an error against the route rather than dropping it in silence. A success whose JSON body
is an **array** is in that position by construction: twenty-one routes answer a list at the root and a list
has nowhere to hold a key, so those name the header alone. Every one of them is a plain read with no posted
document to complain about, and one that grew a gate would be reporting into a log rather than onto the
wire.

**The document says all of this, and no record does.** The key is written by middleware over whatever the
route answered, so declaring it on a response DTO would mean a field on a hundred records that no handler
ever fills — one fact restated a hundred times, and wrong the moment one of them is missed. It is published
once instead, by an operation processor: every 2xx JSON object's schema becomes `allOf` the answer the route
names plus the optional `warnings`, and every 2xx names `Pgm-Warnings` as a response header. `allOf` rather
than a member added to the referenced schema, because that schema is also a request body and a nested field
elsewhere, and a complaint rides on neither. `ComplaintChannelTests` holds the claim to a real answer — a
write posted with a field the reader cannot keep — and to the whole surface in the schema.

**The client reads it in one place too.** `ServerWarnings` is the mirror of `ServerRefusal`, on the other
side of the status line: `Carried` reads the rule ids off the header without touching the body, and
`AnsweredAsync<T>` takes the body once and hands back the declared shape and the findings beside it — once,
because a response's content is a stream and the answer and the key cannot be read in two passes.

**A report whose subject is findings is not this.** `/plan/evaluate` answers a `lint` list and
`/plan/feasibility` a per-box one, and in both the findings are the answer being asked for rather than a remark
alongside one, so they stay named fields of the documents they belong to.

## The six the request asks

None of these is a gate's. A gate reads a document it understood and says what is wrong with the map; these
six are about the **request**, and they exist because the shape above held everywhere except at the door.

They are stated in `Domain` rather than at the door that raises most of them, because the door is not the only
place that knows one. The document editors behind the Edit tool refuse an id naming nothing, an id already
taken and a value outside a closed set from inside `Pgm.Editing`, and copying the ids down to reach them would
be the second `const` aliasing one that exists — the failure *Adding one* names below.

**`RQ1` — the document could not be read.** Absent, empty, malformed, or naming a kind that does not exist. It
is 400, and it carries the field where the reader knew one: a part stated as `null` where the record cannot
hold one reports `roof.gableWindows`, not a sentence an author has to search their document for. Two readers
raise it that way — `HouseStyleJson` through `DocumentFault`, which subclasses `JsonException` so the thirteen
call sites already catching that keep working, and `TerrainThemeJson`, which carries a polymorphic `kind`
failure across because System.Text.Json reports that one as a `NotSupportedException` and the difference is in
the reporting rather than in what went wrong.

A **missing field** is the same rule asked earlier: `RequiredFields` refuses anything a request DTO declares
non-nullable and the body did not supply, naming every one rather than the first, before any handler runs. Null
alone counts — an empty list is a value and a blank name is a fault about the name, which is a gate's to judge.

**A route's own parameters are the same rule again**, and they arrive through `Refusals.UnreadableAsync` with
a sentence rather than an exception: a query parameter a route cannot work without (`box`, `bounds`, the `x`
and `z` of a column), a value outside a closed set (`symmetry=rot_90` where the composer builds two, a shape
family that is not one), a body that is not the document the route takes. All of them are one fault — *the
request could not be acted on* — so all of them are `RQ1`, at 400, with `field` naming the parameter where it
has a name. A caller writing one parser for the studio is the whole point of there being one envelope, and a
route phrasing its own parameter fault costs exactly that.

**A person stated under something that is not a name** is the same rule over a field inside the document.
`PUT /map/{slug}/intent` refuses an author or contributor whose name is empty, past `AuthorNames.MaxLength`,
carries two spaces in a row, or holds a character outside letters, digits, spaces and `.,-_'`, with `field`
naming the person — `meta.authors[0].name` — and nothing written. A name **Mojang cannot answer for is not
this**: PGM takes a person as an account or as a pseudonym and either alone is a whole author, so an
unresolvable name stores as the second kind. What is refused is a string nobody could be called, and it is
refused rather than skipped because the alternative is a 200 crediting fewer people than the author listed.

**`RQ3` — a field went unread.** The document carried a property the reader had nowhere to keep. It is a
**complaint**, so it rides on the success response under `warnings` and the work still happens: the readers
upgrade stored documents in place and carry retired names forward, so a stored document legitimately holds
properties the current record does not, and refusing an unknown one would refuse every snapshot written before
the last shape change. What is left *after* the upgrade has run — a name no record has and no upgrade claimed
— is the honest remainder, and that is what is reported, by path (`roof.pich`, `wings[1].ptch`).

The walk goes down the **value** rather than the declared type, so a polymorphic member is checked against
what it actually became, and it matches a field by the name the **document** uses rather than the property's
own — a sketch shape states `min_x` and the property is `MinX`. Four things are deliberately exempt, and every
one of them is a false positive that would have made the warning worthless: a dictionary's keys, which are the
author's words rather than the record's — a theme names its own buckets; the **type discriminator**, which the
serializer reads to choose the very type being walked and which no concrete record carries a property for;
everything under a member held as **raw JSON**, which keeps whatever was written into it — a sketch's dressing
and each entry of its theme registry are held that way; and a property the serializer **ignores** outright,
which is not a landing place at all, so a document naming one is naming something nothing reads.

**It is asked on every write that takes one of the documents an author writes.** The two library readers take
it inside themselves, after their upgrade has run, because only they know when the retired names have been
carried forward. The plan, the sketch layout and the intent have no upgrade step, so the reading is taken at
the edge — `PUT …/sketch`, `PUT …/sketch/from-plan`, `PUT …/intent`, `PUT …/intent/from-plan`,
`PUT …/plan`, `POST /plans`, `POST /plan/compile` and `POST /map/from-documents` — over the body **as
posted**, before any of them merges what it was sent into what the map already holds, because the posted
document is the only one the caller can correct.

`POST /map/from-documents` carries all three documents at once, so each path is prefixed with the member it
was posted under — `layout.setupp`, `intent.teamz` — since a bare `meta.athors` cannot say which of the three
said it.

**`RQ4` — the route names a subject the studio does not have.** A slug no map is stored under, an id no
library row carries, an artifact a stage has not produced yet. It is **404 with a body**, because an empty one
cannot say whether the identifier was wrong or the route was — `PUT /map/typo/sketch` and
`PUT /map/voidwatch/skecth` are otherwise the same answer, and the second is the one a caller cannot guess at.
The finding names what was looked for and the identifier it was looked for under.

**`RQ5` — the request conflicts with what is stored.** A slug already taken, a library row something still
binds, a write against a document somebody else has already replaced. **409**, and the finding's subjects name
what is in the way — the styles still binding a roof, the map already holding the slug — so a caller can act
rather than guess. It is the one refusal where nothing is wrong with the request at all.

**The stale write is the third of those, and it is the one a caller opts into.** Every write in the studio
replaces: an edit reads the whole map document, patches it and writes the whole document back, and an artifact
is one row a save replaces. Two callers doing that at once keep only the second, with the same status and the
same body either way — so the loss is invisible from the response, which is why it needs a mechanism rather
than care. Each document carries a **revision**; a read answers it as an `ETag`, and a write may state it back
as an `If-Match`. A write whose `If-Match` names a revision the document is no longer at is refused, naming
both numbers and saying to read it again and re-apply — the studio cannot merge two whole-document writes, and
guessing at one would lose whichever half it guessed against.

A request with **no** `If-Match` states no precondition and writes, exactly as it did before there was a
revision to state. Protection is opted into by having read first, which is the only way it can mean anything;
requiring the header would make every caller read before every write. An `If-Match` that is not a revision at
all is refused rather than read as an absent one — a caller that meant to guard its write is not quietly left
unguarded.

**It is 409 and not 412.** `412 Precondition Failed` is the header's own status, and this answers `RQ5`
because the studio has one refusal envelope and `RQ5` already means *the request conflicts with what is
stored*, which is exactly what a stale write is. A caller writing one parser for the studio is the whole point
of there being one shape, and a second status carrying a different body for the same class of fault costs
precisely that.

**The map document and each artifact are versioned apart**, because they are separate documents with separate
writers: a caller holding the sketch layout's revision has said nothing about the map's, and one counter would
refuse a metadata patch because somebody saved a drawing.

**`RQ6` — a document the studio stored will not read back.** A plan row, an artifact, a snapshot written under
a shape no reader claims. **422** rather than 500, because it is data rather than a defect and writing the
document again clears it; and deliberately not `RQ1`, which would blame the request that merely asked to read
it — an agent told its own posted document is unreadable looks in the wrong place.

**`RQ2` — the fault is the studio's own.** Something escaped an endpoint that no gate refused. It stays a
**500**, because dressing a defect as a bad request sends an author hunting a mistake they did not make; what
it buys is that the caller gets this envelope instead of a .NET stack trace, and the trace goes to the log. It
should never be seen, and one appearing is a bug report rather than an authoring problem.

**A caller that hangs up is not `RQ2`, and is not answered at all.** A request the client abandons cancels
`RequestAborted`, every endpoint takes that token, and whatever it was awaiting throws — so a disconnect
reaches the middleware wearing the same clothes as a defect. It is neither logged nor answered: there is
nothing to report and nobody to answer, and the envelope would be written to a socket that has gone. What
separates the two is the token rather than the exception, because a server-side timeout throws the same type
while the caller is still waiting and *is* worth reporting; `Api/Http/ClientDisconnect` takes both halves, so
a defect that merely coincides with a disconnect stays a defect. The sketch tool produces these deliberately —
its autosave cancels the previous in-flight write on every edit — and the abandoned write is inside a
transaction that never commits, so the document is untouched and the next save carries the newer state.

**A `catch` around a build or a solve names the faults a document can cause, and nothing else.** Catching
everything and answering 400 dresses a defect as the author's mistake: a null dereference in the rasterizer
reaches them as a fault in their board, and the exception's own sentence goes nowhere. So each of the five
preview and render endpoints filters by type — `JsonException`, `ArgumentException`,
`InvalidOperationException`, `FormatException`, `OverflowException`, `KeyNotFoundException` — and answers
`RQ1` **carrying the reader's message**; anything else reaches the middleware, which logs the trace and
answers `RQ2`. The export's composer lets the exception propagate for the same reason: it has no logger of its
own, so swallowing one loses the only copy.

**A gate never returns null and never throws.** The one exception is a document that will not parse, which
cannot carry on to collect a second fault; it throws, and the exception carries its finding so the gate above
it answers in this shape anyway. A **resolve** is a different thing again and keeps its own shape:
`RoomFrames.ResolveRoom` answers a room *or* a refusal, because it is producing a value and stops at the first
thing that makes producing it impossible, where a gate reads a document and collects everything wrong with it.

## Looking one up

`GET /api/rules` answers **every rule the studio can cite**, with what it means and what to do about it — the
question a reader has on meeting an id in a refusal and the one nothing else answers. `?family=PL` narrows to
one family, `?rule=WL2` to one rule; a name nothing matches is an empty list rather than a 404, so a caller
asking "is there a rule called that" does not have to tell an absent rule from a mistyped route by the status
code. Each row is `{rule, family, owner, means, fix, evidence, category, concerns}`, and `owner` is the file
to read next.

```json
{
  "rule": "PL9", "family": "PL", "owner": "PgmStudio.Pgm.Plan.PlanRules.WoolUnreachable",
  "means": "A wool cannot be reached from a capturing team's spawn at all.",
  "fix": "Nothing walkable connects the capturing team's spawn to this wool: add a piece bridging the gap, or widen a border narrower than a corridor. Distance here is the walk over the surface, not the straight line.",
  "category": "unplayable", "concerns": ["plan", "objective", "spawn"]
}
```

**Nothing in that answer is written twice.** A gate rule's `means` is the `<summary>` beside its own `const`
and its `fix` is the `<remarks>` of the same docstring, read out of the XML documentation file the compiler
emits — so the sentence a caller is shown is the sentence in the source, and there is no catalogue to fall out
of step with it. A layout rule comes out of `docs/generator/rules.md`, embedded in `PgmStudio.Domain` and
parsed, because that document is the rule law and copying its statements into C# would have made a second law.

**Every gate rule is answered; the layout rules are the ones a caller can meet.** `rules.md` states 92 and
the catalogue answers the 34 something can name — a plan-validator lint, an evaluator term's `RuleId`, a
producibility finding's `Cites`. The rest are the generator's law, and `rules.md` is where the law lives:
publishing a rule nothing raises in a row identical to one a caller can fail on makes every row less
informative, and there is no finding to explain. `RuleCatalog.Raised` states which, and
`RulesEndpointTests` holds it to the source both ways — a row nothing names fails, and an id named that is
not answered fails too.

**A layout rule has no `fix`, and that is not an omission.** The gate rules are mechanical — a doorway too
short, a document that will not parse — so what to do about one follows from what it refuses. The layout rules
are claims about how a map is *played*, which `CLAUDE.md` says are the author's to state and not this
repository's to infer. What they carry instead is `evidence`: `corpus`, `expert`, `open` or `guess`, in
`rules.md`'s own terms, which says how far to trust each one.

**The category is what a caller branches on, and the concerns are what a prefix could never carry.** An id is
specific, stable and for a reader; `category` is the closed set an agent reads instead of learning 77 ids —
`malformed` (fix the shape), `unknown` (fix a name), `conflict` (choose which wins), `unsatisfiable` (change
the design), `unplayable` (change the map), `forbidden` (ask for something else), `unavailable` (try again, or
look upstream), `internal` (report it). Each word is defined by the action it implies rather than by how the
fault sounds, which is what lets a caller act before reading a sentence. `?category=unplayable` answers every
rule they would treat the same way, whichever gate asks it.

`concerns` is what the rule is about, one word or several, from a closed thirteen: `request`, `plan`, `intent`,
`objective`, `spawn`, `terrain`, `structure`, `feature`, `material`, `style`, `theme`, `world`, `studio`. A
rule concerns a **combination** — `WX6` is a plan, a structure and an objective at once, `PL8` those three and
a spawn — and a family prefix is one token, so it names the loudest and the rest goes unsaid. The list is
uncapped. `?concerns=objective` answers every rule that touches one; repeating it **narrows**, so
`?concerns=objective&concerns=plan` is the plan half of *One question, asked at every grain* above — `WX6` and
`PL9`, without the two that ask reachability of built ground. A word outside either set is refused as `RQ1` at
400 rather than answered with an empty list, which a caller would read as "no rules do that".

Both come off a **`[Rule]` attribute beside the constant**, so they are declared once per rule rather than
restated at each site that raises one: the 77 constants are raised from 97 sites, and a field on the finding
would have 25 of those restating what another site already fixed with nothing checking they agree.

**A layout rule carries neither**, having no declaration site to write one on. Nor do four gate rules —
`WX1`, `WX5`, `WX7` and `WX9` — which state how a room frame is derived and refuse nothing: no finding cites
one, so there is no caller to branch and nothing to do. They are constants because a rule may not live only in
a markdown file, and what they answer is a reader who met the id in `structures.md`.

**The numbers have their own endpoint.** `GET /api/rules/terms` answers every evaluator term with the band it
is scoring against right now — `{term, rule, kind, band, bandSource, learnsFromTraced}` — read through the
same resolution the scorer uses, so the number served is the number enforced. `bandSource` says where a band
came from: `authored` (a ruling stated on the term itself, e.g. `goal-spawn-ratio`'s `[3.0, 4.0]`),
`envelope` (learned from the teaching seeds by `envelope-stats`), or `none` (a dormant soft term, or a hard
term, which refuses rather than scores). The one authored number that is not a term — the road standoff — is
`DR-ROAD`'s, whose catalogue sentence carries the per-kind values from `PlacedProp.RouteStandoff` and is
drift-pinned to them by `DressingRulesTests`, as `GO1`'s prose is pinned to its term's band by
`RuleBandDriftTests`.

## Adding one

A new gate writes findings; it does not write a shape. Name it `Check`, return `Findings`, put the rule id in
the `*Rules` class beside the rule that fires it — never as a literal at the throw site, which is how `OB20`
spent a release as a bare string and `OB19` with no id at all, and **never as a second `const` aliasing one
that exists**, which is what `ObjectivePlacement.Rule` and `DressingScope.Rule` were until the catalogue listed
their ids twice. Give it a **`<summary>` saying what it refuses and a `<remarks>` saying what to do about it** —
those two are what `/api/rules` answers with, so a rule written with only the first is listed with no fix and
`RulesEndpointTests` fails. Give it a **`[Rule]` attribute** beside the constant with its category and the one
to several things it is about; a rule added without one is listed with neither and fails there too. A rule that
refuses nothing takes the concerns-only form, and `RulesEndpointTests` names the four that may. Add its row above, and answer through `Refusals.StopAsync` (an endpoint) or
`Refusals.Of` (a typed body). A gate below `Api` hands its findings up and the HTTP layer renders the
envelope, so there is nothing to build by hand. A rule about a map *as it is played* is the
author's to state before any of that: see the human-oracle rule in `CLAUDE.md`.
