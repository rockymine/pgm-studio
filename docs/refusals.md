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

Two fields exist for cases the first three do not cover. **Severity** is `refusal` or `complaint`: a refusal
stops the work, and a complaint rides along with work that succeeded, which is how a compile that produced a
playable-but-goalless map says so without blocking. And **cites** is what to look up next where the rule id is
not it — the layout rule the fault falls under, or the open task that would resolve it. That last one is why
`cites` is a field of its own rather than a second id: a rule is stable forever and a task id is a debt with a
due date, and one field holding either would make the two indistinguishable to a reader.

`Finding` is in `PgmStudio.Domain`, which is the lowest project every gate can reach — `Minecraft`, `Pgm`,
`Analysis` and `Export` all see it, and `Contracts` carries the wire mirror the WASM client reads. Nothing that
is not a finding is forced into the shape: a `TermScore` is a distance that *carries* a finding, and a distance
is not a fault.

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
about the request rather than about the fault, so each endpoint names its own — the tool documents' endpoint
tables carry which.

**The edge answers it too, not only the gates.** No route under `/api` answers a failure in a shape of its
own, and none answers one with no body at all: `Refusals` writes every one of them, so a caller writes one
parser for a refusal — which is the only reason to have one envelope. The Edit tool's thirty-six write routes
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
| `DC*` · `OB*` | destroyables, cores, goal placement, gamemodes. Two are **complaints on a built world**: `DC3` a goal built in a material its own size is wrong for, `OB23` a goal topping out over the build ceiling — the world is built and the goal stands | `Domain/ObjectiveRules.cs` |
| `WX*` | room frames — the shell, the pad, the doors, the iron, and the shell's height against the build ceiling | `Domain/RoomFrames.cs` → `RoomFrameRules` |
| `HS*` | a house style's own materials | `Minecraft/Houses/HouseStyleValidation.cs` → `HouseStyleRules` |
| `HP*` | a placed building's shape | `Minecraft/Dressing/PlacedProp.cs` → `HousePropRules` |
| `HJ*` | how two wings meet | `Minecraft/Houses/WingJoints.cs` → `WingJointRules` |
| `DR-*` | the dressing pass's own — `DR-DOC` a document that will not parse, `DR-ROAD` a prop resting nearer to the road than its kind's standoff (the numbers live on `PlacedProp.RouteStandoff`), `DR-PASS` a building leaving no five-block passage beside any side, `DR-SIZE` a building whose box is under 5×5 blocks, `DR-KEEP` a prop resting on ground the map keeps clear (a spawn, a wool room, a stated structure, a built column, a door's approach), `DR-CLAIM` a prop resting on ground something already standing holds (a building holds what it stamps plus a block of ring beyond it), `DR-SITE` a prop with no ground to rest on — a building is held to every cell of its footprint, and the finding names the first bare column. Every one of them is a **complaint on a built world**: the prop does not land, the world does | `Minecraft/Dressing/DressingJson.cs` · `Minecraft/Dressing/GroundClaims.cs` → `DressingRules` |
| `EX*` | the export gate's own — `EX1` not traversable, `EX2` no spawn to enter the map by, `EX3` what the intent stated and the document did not carry, `EX4` an objective with no team to contest it | `Export/MapExportComposer.cs` → `ExportRules` |
| `SK*` | the sketch document's own — `SK1` a recompile fused the board differently, so an island the author had drawn relief onto no longer exists to carry it; `SK2` a board whose extent is past what the studio will realize (the one refusal; the ceiling is a constant and deliberately appears in no message — a stated one is a target); `SK3` a name matching nothing (a shape kind, a mirror mode, an island's shape id, a relief's island, a shape's or the map's theme); `SK4` a shape that draws no ground; `SK5` a column the world cannot hold; `SK6` nothing stored to finish and `SK7` a stored board that rasterizes to no ground — the two the finish stage owns, and the one place the sketch's complaints become fatal, since finishing is what declares the drawing done. `SK3`–`SK5` are **complaints on a built board**: the rasterizer is set algebra, so what it cannot read contributes no ground rather than failing, and without these a defect in the document reads as a smaller drawing | `Pgm/Sketch/SketchRules.cs` · `Pgm/Sketch/SketchLayoutCheck.cs` |
| `IM*` | the import's own — `IM1` a host the import does not fetch from (the SSRF allowlist), `IM2` an archive the host did not serve, `IM3` one past the download cap, `IM4` one that is not a zip, `IM5` one carrying no `region/*.mca`, `IM6` a folder that is a map already rather than a world to originate from | `Api/Endpoints/ImportEndpoints.cs` → `ImportRules` |
| `CO1` | the composer's — a well-formed descriptor naming a board it cannot emit; the sentence carries which knob and which value, because the emitter that stopped is the only thing that knows | `Pgm/Compose/ComposeException.cs` → `ComposeRules` |
| `RQ*` | the request itself — a document that could not be read, a subject the route names and the studio does not have, a request conflicting with what is stored, a stored document that will not read back, a field that went unread, and a fault that is the studio's own | `Domain/RequestRules.cs` |
| `ED*` | the document editors' own two — `ED1` a reference the document cannot resolve (an apply-rule naming an unknown filter, a filter naming itself), `ED2` an edit the document is not in a state to take (a group with fewer children than its type takes, an apply-rule with no region, filter or action). The other six an edit is refused for are the request's, above | `Pgm/Editing/EditRules.cs` |
| `CT` `SP` `WL` `LN` `HB` `FR` `MD` `BZ` `EL` `G*` `PC-*` `ST*` | the layout-rules checklist, cited by the plan lint and the producibility read | `docs/generator/rules.md` |

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
| `EX1` | is the ground the rasterizer built connected? | the built world |
| `DR-PASS` | does this building leave five blocks to get past it? | one prop on that ground |
| *coverage* | is the ground that **can** be reached ground any journey actually uses? | the built world, as a measurement rather than a gate |

Read down the column and the escalation is the point. `WX6` is local and structural; `PL9` is the same
question at board scale; `EX1` asks it of ground rather than of rectangles, because a plan that passed can
rasterize into something that does not; `DR-PASS` re-opens it after a house lands on ground that was already
walkable; and coverage asks the question the four gates never do — not *can* a player get there, but *does
any route go*. A board can pass every gate above it and read a third dead.

**Where a gate is asked decides who meets it.** The export's whole chain for a sketch map — `OB20`, `SK2`,
`OB17`, `EX1`, `OB19`, then `EX2`/`EX3`/`EX4` — is inside `MapExportComposer.ComposeSketch`, the method the
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

**One key, and one rule for when it appears.** A 2xx JSON object answers `warnings` when something was
complained about and carries no such key when nothing was. The single rule is what makes an absent `warnings`
readable: without it the key's absence covers four states a caller cannot tell apart — an endpoint with no
gate, one whose gate found nothing, one that dropped what its gate found, and one answering a shape with
nowhere to put it. A caller reads `warnings ?? []` and is done.

Two cases sit outside that rule, and both are honest. A **refusal** carries refusals only, in the envelope's
`findings`: the work did not happen, so nothing rode along with it. And a success that is **not JSON** — a
rendered PNG, an ASCII board, a parquet stream — has nowhere to put one; where that loses a complaint the
studio logs it as an error against the route rather than dropping it in silence.

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
`PUT …/plan`, `POST /plans` and `POST /plan/compile` — over the body **as posted**, before any of them merges
what it was sent into what the map already holds, because the posted document is the only one the caller can
correct.

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
one family, `?rule=SP7` to one rule; a name nothing matches is an empty list rather than a 404, so a caller
asking "is there a rule called that" does not have to tell an absent rule from a mistyped route by the status
code. Each row is `{rule, family, owner, means, fix, evidence}`, and `owner` is the file to read next.

```json
{
  "rule": "PL9", "family": "PL", "owner": "PgmStudio.Pgm.Plan.PlanRules.WoolUnreachable",
  "means": "A wool cannot be reached from a capturing team's spawn at all.",
  "fix": "Nothing walkable connects the capturing team's spawn to this wool: add a piece bridging the gap, or widen a border narrower than a corridor. Distance here is the walk over the surface, not the straight line."
}
```

**Nothing in that answer is written twice.** A gate rule's `means` is the `<summary>` beside its own `const`
and its `fix` is the `<remarks>` of the same docstring, read out of the XML documentation file the compiler
emits — so the sentence a caller is shown is the sentence in the source, and there is no catalogue to fall out
of step with it. A layout rule comes out of `docs/generator/rules.md`, embedded in `PgmStudio.Domain` and
parsed, because that document is the rule law and copying its statements into C# would have made a second law.

**A layout rule has no `fix`, and that is not an omission.** The gate rules are mechanical — a doorway too
short, a document that will not parse — so what to do about one follows from what it refuses. The layout rules
are claims about how a map is *played*, which `CLAUDE.md` says are the author's to state and not this
repository's to infer. What they carry instead is `evidence`: `corpus`, `expert`, `open` or `guess`, in
`rules.md`'s own terms, which says how far to trust each one.

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
`RulesEndpointTests` fails. Add its row above, and answer through `Refusals.StopAsync` (an endpoint),
`Refusals.Of` (a typed body) or `Finding.Envelope` (the same shape, built by hand for a composer below `Api`
that cannot reach `Contracts`). A rule about a map *as it is played* is the
author's to state before any of that: see the human-oracle rule in `CLAUDE.md`.
