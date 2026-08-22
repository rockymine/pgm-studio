# How the studio is driven

`project-structure.md` says where a piece of code lives and which project may reference which. This document
says the other half: how a request becomes work, what the studio is able to say about itself, and where the
shape of that answer is decided. The two do not overlap — one is the package map, this is the boundary.

It matters because the studio has two audiences and only one of them can read prose. A person drives it
through a Blazor client that renders panels and a canvas; an agent drives it through `/api` and a CLI, and
everything it can learn about the map it has to learn from a response body. Every finding below is a place
where the studio knows something and cannot say it, or says it in a shape that has to be learned rather than
parsed.

## A gate belongs to whichever door it was put behind

The pipeline has one entry point: `PgmStudio.Api`, **149 endpoint classes** over 45 files. Everything that
authors a map arrives through it — the browser, the headless drivers agents write, and the catalogue map,
which is emitted as a layout and an intent and loaded through `POST /map/from-documents` like any other.

That is not an arrangement the code enforces; it is where the doors happen to be. A second driver linking
`Pgm`, `Minecraft` and `Export` directly is a few lines away at any time, and the moment one exists, a gate
raised behind one door is a gate the other never meets. The export answers that by construction: **every
gate a sketch map is judged by sits inside `MapExportComposer.ComposeSketch`**, so which door a caller came
through cannot change what its map is held to.

Doing that for one chain is not doing it for the pipeline. A step whose only home is an HTTP handler can only
ever be reached by sending a request, so the next gate lands behind whatever route needs it, and the next
driver reaches around it. Every step that reads stored state, does work and writes it back now has a home
below the door — the seven operations below — so a gate is raised where the work is rather than where the
request arrived.

## The boundary carries no schema

The surface describes itself. `GET /api/openapi/v1.json` is generated from the routes and the DTOs — 118
paths, **149 operations**, 291 schemas, **257 of them carrying the docstring beside the type** — and
`/api-docs` is the page over it, where a route can be expanded and sent without writing a client. Both are served from the app's own assets.

What that document can say is bounded by what is declared, and the write surface states all of it. Of the
**67 POST/PUT/PATCH routes**, **64 publish a request body** — 25 by binding a request type, 39 by naming the
shape they take while still reading it themselves — and the three that do not read no body at all, which is
the truth rather than a gap. `SchemaCompletenessTests` holds both halves as counts that only move down, and
both are now zero.

One of the 42 is why the count could reach zero. A material is a discriminated union with nothing above its
discriminator — every field a body carries belongs to the leaf its `kind` names — so `TerrainMaterial`
declares no property of its own, and the generator's default reads that as an empty request and refuses the
whole document. `c.Endpoints.AllowEmptyRequestDtos` is what says otherwise: the rendered schema is complete
either way, carrying the `kind` mapping over all fourteen leaves, so the refusal was about the base's shape
rather than about what a caller may post.

Three things follow from that, and they are the same fact seen from three sides.

**The one global input gate covers a sixth of the surface, and that is the shape of the thing rather than a
shortfall.** `RequiredFields` refuses anything a request DTO declares non-nullable and the body did not
supply — and its first line is `if (context.Request is not { } request) return;`, so it is a no-op for every
endpoint that has no request type. The promise it makes holds for the **25 routes that bind one**. A declared
shape is not a bound one: the 39 that name their shape to the generator still read it themselves, so the
document is true about them and the gate does not run.

**Binding is not a sweep, because two of the three things a body can be wrong about are not the binder's.**
A field the JSON cannot carry into its type and a field that is missing are; a value outside a closed set is
the gate's, and — decisively — an *update* body needs to tell an absent field from a null one, which a bound
record cannot do once every field is optional. Every edit update reads `ContainsKey` to tell "leave this
alone" from "clear it", so every edit update stays hand-read. What binds is the creates whose record has a
field the caller must supply: a spawn's and an observer spawn's `region_id`, a team's `id`, and with them the
`yaw` and `max_players` a binder refuses for not being a number. `WriteSupport.Stated` is how a bound record
reaches the editors, which live in `Pgm` and cannot see `Contracts`: written back out through the wire's own
serializer into the same doc-tree dict the hand-read path builds, so an editor reads one shape either way.

**A refusal from the edge is a refusal like any other.** A binder that will not read a body used to answer
FastEndpoints' `{statusCode, message, errors}` — the one shape on the surface a caller needed a second parser
for, against what `docs/refusals.md` says. `Refusals.UseRefusalEnvelope` makes it `RQ1` per field in the one
envelope, and both halves of "this body will not read" now name the field **as the wire spells it**: a
property stating its own JSON name reports `region_id`, not the `regionId` the record declares.

**A validation that cannot live in a schema still lives in the code by hand.** Of the Edit tool's 53 refusal
sites across `Pgm/Editing`, **15 are `Unreadable`** — a field is absent, a value is outside a closed set, a
number is not one. Three of those are now unreachable over HTTP, shadowed by the bindings above, and stay as
the library guards they are: `SpawnEditor` and `TeamEditor` are public API in `Pgm` and cannot assume a bound
caller. The other 38 sites are a different thing entirely: `NoSuchSubject`, `Conflict`, `Unresolved` and
`Inapplicable` read the map the edit lands on, and no binding will ever replace them.

**Every operation now says what it answers.** An endpoint that declares no response type is published as
**204 No Content** — the generator's default, and a claim rather than a silence, so an undeclared route does
not leave a caller guessing but misleads it. **Nought of the 149 operations** publish that 204 without
answering it; seven publish it truthfully, every one a delete whose answer is that the thing is gone.
`SchemaCompletenessTests` holds the count at zero and the seven as a named list, so a route added without a
response type fails there, and one on the list that grows a body cannot leave it quietly. The media types
are declared too: the six `image/png` routes, the three `text/plain` ones and the export's `application/zip` all say so, so
`/api-docs` renders a theme swatch beside the route that draws it.

**The wire contract is generated once and kept by hand twice more, and only one of the two still matters.**
The route attributes in `Api/Endpoints` are the generator's source. Beside them sit the route strings written
out in the Blazor client and the endpoint tables in the eight `docs/tools/` documents, neither derived from
the schema.

The response half is finished. The client now reads **71 responses as a typed shape from `Contracts` against
2 as `JsonElement`**, so a renamed DTO field is a compile error rather than a null at run time, and the third
copy that lived in per-component parsing code is gone. What is left hand-written is the path: **75 literal and
98 interpolated route strings across 33 files**, where a typo is a runtime 404 that reads like a missing map.

**That is why the studio has no generated client, and will not get one.** A generated client's whole value is
the response types, and those already come from `Contracts` at 71 of 73 call sites; what it would still buy is
the path check, at the price of a build-time package and a second copy of the whole surface committed to the
tree — the "second accepted shape" that `CLAUDE.md` forbids for exactly the reason it would rot here.
`ClientRouteTests` buys the same check for nothing: every route string in the client is a route the schema
serves, with one named exception for the twenty-one that three Edit phases compose out of a prefix and a tail
(`C47`). The tables are the same problem seen from the prose side: three heavily used
analysis routes had drifted out of every one of them, which is not a documentation lapse but what a
hand-maintained copy of a machine-readable fact does.

**The tables are now checked in both directions, and generating them is still the wrong fix.** A row's
`Answers` column is editorial prose — what the route is *for*, which of two bodies it takes, why a field is
not carried — and a sentence assembled from a schema would say less in more words. So three tests hold the
hand-written tables to the generated document instead: every path a row names is a route the API serves,
every route the API serves is in some row or on a named list of three that belong to another document, and a
row's `Fails with` column names exactly the refusals its operation publishes. All three read a row through
one `EndpointTables`, and they read it differently on purpose — strictly where a parser's mistake would be a
false failure over prose, loosely where it could only ever weaken the check.

Making the third of those pass is what put the codes in the schema. The tables had been right and the
document poor: **44 of 54 rows** named a 404, a 409 or a 422 that `/api/openapi/v1.json` did not publish,
because the only refusals declared were the 400 and 500 every route carries from one place. **95 routes now
declare their own**, through `Answers.Refuses`, per route rather than derived from the path — a path holding
`{slug}` nearly always answers 404, and nearly is a guess.

## The use case has no name

`Api/Endpoints` holds **4,427 lines of code** against `Api/Services`' **1,348**, but the line counts measure
the file rather than the use case. The 149 handler bodies are **2,146 lines between them** — a median of
**10**, with 91 of 149 at twelve lines or fewer and four over forty. The rest of `Endpoints/` is 149 class
declarations, their `Configure` blocks and their constructors.

**One refusal shape, and one question.** An operation hands back what it produced and, where the work did
not happen, a `Vocabulary.Refusal` — the status, the gate's short label, the findings — which the layer that
speaks HTTP renders into the envelope. Three results had declared that triple themselves and each carried its
own `IsError`, so a caller asked three types the same question three ways; they compose it now, and a caller
asks `result.Refusal is { } refusal` whichever operation answered. The status lives with the refusal rather
than with the route because what went wrong is what decides it.

**And one prologue.** Loading the map a route names and answering 404 if it is not stored was written out
**forty-seven times** — thirty-seven as two lines in the endpoint, ten more through a local loader in
`AnalysisEndpoints` that added the document read. `MapOfRoute.OfRouteAsync` and `WithDocOfRouteAsync` are the
one line each is now: the slug is read off the request, because a route that loads a map by anything other
than its own `{slug}` is doing something else and should say so.

**All ten the board named are in `Api/Services` now, as operations rather than handlers** — and they came to
seven, because several of the ten were one operation seen through different doors. `DocumentWrite` is the
guarded replace behind `PUT …/plan`, `PUT …/sketch`, `PUT …/sketch/from-plan` and the intent write: a plan
and a layout are stored the same way and refuse the same two things, and what differs is what each document
says about *itself*, which stays with the document that has it. `MapOrigin` is the row every one of the six
ways into the studio writes. `IntentWrite` was already an operation and was simply misfiled. `SketchDiscard`,
`MapMetadata`, `SymmetryConfirm` and `WorldFolderImport` are the four that were only ever one route's, and
are now reachable without one. `MapEdit` is the thirty-six edit routes' one path, moved off `Endpoints` and
made HTTP-free with it.

`Api/Services` is **32 files and 3,620 lines** against `Api/Endpoints`' 46 and 6,185.

**The revision now crosses as a value.** An operation is handed the revision the caller stated and answers
the one it landed at; that the first arrives in an `If-Match` and the second leaves in an `ETag` is
`Revisions`' business and nothing below it knows. That is what let `Writes` go: its whole content was
carrying an `HttpContext` down to where the store is.

**The use cases are the thirteen handlers that read state and write it back**, and three of those also run a
gate: `SketchFinish`, `SketchFromPlan`, `SketchPut`. Everything else is a read, or a thin pass-through to an
editor in `Pgm/Editing`. So the problem is not volume; it is that a step of the pipeline has nowhere to live
except behind the door it is reached through, and a second driver cannot call it.

Three operations now do live somewhere: `MapExportLoader` loads what the pure composer needs and calls it,
`SketchFinish` rasterizes a drawing and advances the stage, and `MapFromDocuments` turns a plan, a layout and
an intent back into a whole map. Each is HTTP-free — it answers findings and lets the layer above render the
envelope — and each has more than one caller or is written to take one. They sit in `Api/Services` because
that is the lowest project reaching everything they need, and that is where they stay: a project of their own
would buy separation and no second consumer, since the driver that would have been one speaks HTTP. The ten
handlers of the same shape have joined them, as seven operations — several of the ten turned out to be one
operation reached through different doors.

**The order between steps is the part that has no home at all.** Storing an intent projects the map document
from the intent's own `meta`, so authors written before it are overwritten — a rule stated in `flow.md`, in
the driver that authors maps against this API, in that driver's README and in its generation notes, and
enforced by nothing until `MapFromDocuments` made the sequence itself the answer.

The load-the-map-or-404 prologue appears **37 times** verbatim in `Api/Endpoints`, out of 44 slug loads.

## The lifecycle is data, and nothing reads it

`map.stage` holds one of `plan`, `sketch`, `configure`, `edit`. It is written at map creation by
`MapOrigin` and once more by `sketch/finish`. **No endpoint refuses anything because of it**, and that is the
product statement rather than a gap: a stage is a progress marker, so the one-way flow means nothing reads
back up and not that a built map may never be re-planned.

**What a map at a stage may be asked for is answered by the map**, on `GET /map/{slug}/layers`: the stage,
the layers it holds, and the moves those allow, each with the route that performs it. `capabilities.md` is
709 lines answering the same question in prose, and `flow.md` states the four levels and the hand-offs
between them well — but a driver reads the map rather than the markdown now, and what the markdown says is
checked against the routes the moves name.

That was also what made the export gates expensive, and the fix there is the shape the lifecycle wants at
scale: `OB17` is asked at the preview that already paid for the build, and `OB19` stopped being a gate at all
— the prop it indicts is declined and the map ships.

**Two reads finish it.** `GET /map/{slug}/findings` asks every gate the stored documents can answer, at once,
by calling the same methods the steps themselves call — so a fault authored at one step is heard where it was
authored rather than three calls later. It does not build: the export gates need the rasterized world, and
each is named in `unasked` with the route that does pay, because a list silent about what it skipped reads as
*nothing is wrong*. `GET /map/{slug}/layers` answers the other half — where the map has got to and what may be
done to it from here, each move with its route. A driver's loop is *act, then ask*.

**A stage is a progress marker and not a lock**, which is the product statement the transition table rests
on: `flow.md`'s one-way flow means nothing reads back up, not that a built map may never be re-planned. So no
endpoint refuses on `map.stage`, and what decides whether a move is offered is which documents are stored —
rebuilding a drawing from a plan needs a plan, whatever stage the map is at. The stage only says which of the
open moves is the one being waited on.

## A fault carries an id, a class and what it is about

The studio declares **77 rule constants in 14 families**, and answers `GET /api/rules` by reading each
constant's own XML docstring and the `[Rule]` attribute beside it — so a rule's meaning, its fix and its
classification all have one home and no catalogue can fall out of step with any of them. That mechanism is
the best thing in the codebase.

What it lacked was a **class**. A caller that wants to know whether to fix the request, change the design,
change the map or report a bug had to know all 77 ids to find out, because the only machine-legible thing a
finding carried was the id itself. It now reads a **category** — one of eight words, each defined by the
action it implies — and a **concerns** list of one to several of thirteen words saying what the rule is about.
Both belong to the rule rather than to the finding: a category is fixed by the id, and the 77 constants are
raised from 97 sites, so a field on the finding would have 25 of them restating what another site already
fixed with nothing checking they agree. A caller joins on the id, which is what the catalogue is for.

The family prefix is *not* the defect, though it reads like one at first. `PL2` and `EX2` carry nearly the
same sentence — *no spawn, nobody can enter the map* — and look like one fault under two ids until their
`fix` lines are read: `PL2` says *add an entry in `placements.spawns`*, `EX2` says *give the intent at least
one spawn*. Two documents, two things to go and change, correctly two rules. The same holds for the five ids
that all mean *a name that resolves to nothing* — `PL5` and `PL10` over the plan, `SK3` over the layout,
`ED1` over the map document, `RQ4` over the request. One category, four documents, and the prefix is doing
real work.

So the prefix is close to a **subject** axis already, because for most families the subsystem that asks *is*
the document at fault. What was missing beside it is the category, and the two together are what let a caller
read `PL2` as *unplayable, in the plan* and `EX2` as *unplayable, in the intent* without knowing either id.
The prefix stays a prefix, though: it names one token, and a rule concerns a combination — `WX6` is a plan, a
structure and an objective at once — which is why `concerns` is a list and why the escalation
`refusals.md` § *One question, asked at every grain* states in prose is now a query,
`?concerns=objective&concerns=plan`.

**Rules are stated three ways, and only two of them can be checked.** The 77 constants are one; the layout
law in `docs/generator/rules.md`, embedded and parsed, is a second, and both are answered by `/api/rules`.
The third is a bare string literal at the throw site — the plan validator cites fifteen ids that way, `SP1`,
`SP2`, `EL1`, `ST8`, `WL1`, `CT12`, `BZ5` and the rest. All fifteen resolve today, because they are layout
rules `rules.md` states as their own bullet. Nothing checks that they do: a typo produces a finding citing a
rule nobody has, and the catalogue is not consulted.

`PC-C` is what that gap looks like once it has happened, and is the reason the check is worth building. The
lint fired it for a corner contact between separate areas and `LayoutEvaluator` rejected boards for it, while
`rules.md` named it only *inside* another rule's bullet — the retired `PC-S`, which is the id the parser took
that line for — so `GET /api/rules?rule=PC-C` answered an empty list for as long as both were true. It is a
constant now. `RulesEndpointTests` could not have caught it: it checks that every declared rule carries a
sentence, which is the opposite direction, and the fifteen that remain are literals no reflection can see.

The distinction the catalogue now draws is between the **category** of a fault, which is a small closed set an
agent branches on, and the **rule**, which is specific, stable and for a reader. What remains unanswered is
the envelope: a refusal is the studio's own `{error, message, findings}` rather than RFC 9457 Problem Details,
whose `type` URI would be the `/api/rules` lookup that now exists.

## The measurement is next door

`pgm-studio-mapgen` is where agents drive the studio, and its tooling is a reading of how drivable the studio
is. Almost nothing in that repository is about map craft; most of it is about reaching the pipeline at all.

`tools/drive.py` is the **seventh** driver written against this API, and its own README says what the six
before it did: they "read the status code and threw the findings away". What it does instead is print every
finding the pipeline raises, with its rule id, **at the four places one can appear** — the evaluator, the
compile, the sketch write, the dressing read. That is a client-side assembly of a report the pipeline already
produces four times and never joins up.

The loop it drives is eleven calls, and two of them have a load-bearing order that the brief has to state
because nothing else does: `POST …/sketch/columns` answers a shorter decline list before the intent is stored
than after it, because `DR-KEEP` reads spawn doors and goal rings the intent carries; and a
`PATCH …/metadata` before the intent is overwritten by the projection the intent triggers. The authoring
brief beside it requires **fifteen documents read in order** before an agent draws anything, and a 47 KB
errata file whose entries each cost an earlier run a build cycle.

**The sharpest reading in it is one sentence.** `GENERATION-NOTES.md` §11 collects the faults found by
writing documents against the documentation and posting them cold: *"Every fault below is one the author
could not have seen, because each of them answers 200."* A rectangle whose fields are `x`/`z`/`w`/`h` rather
than `min_x`/`min_z`/`max_x`/`max_z` — fourteen of them across two maps — covers no ground and answers
`{"ok": true}`. `relief` written inside `layout` rather than at the document root is dropped. A house prop
over `HouseProp.MaxFootprint`, or a wing under three blocks, is dropped without a word; seven of seven houses
vanished that way.

**The studio has the mechanism that catches every one of those.** `DocumentShape.Unread` walks a parsed
document beside the value it deserialized to and names every property nothing could keep — `RQ3`, a complaint
on the success response, by path. It runs on nine write routes, the room-style library and the terrain
previews among them, and on all three of the documents an agent authors: the sketch layout, the plan and the
intent each answer a misspelled field rather than dropping it in silence.

**A refusal that cannot say what would satisfy it is a refusal a driver works around.** Run 4's board hit
`PL11`, `WX6` and `SP1` on a capture-the-wool plan, and the run's own report says what happened next:
*"Rather than iterate extensively, I simplified to a basic destroy board."* The brief asked for CTW; a
different map shipped, and the report names what would have prevented it — "a working example to follow".
Every gate can say what is wrong with a document. None can answer with one that would be right.

Read together, none of that is new: it is the six findings above, seen from the driver's side. The eleven-call
loop and its load-bearing order are what an absent application layer looks like to a caller. The fifteen
required documents and the errata file are what an absent contract looks like. And the silent 200s were what
an absent request shape looked like, on exactly the documents that matter most — the half of that answered by
`RQ3` and by every write route that reads a body now declaring the shape it takes.

## Three things the request layer does not do

Beside the shape questions above sit three that are about the *behaviour* of a write, and all three matter
more now that a second caller drives the same map.

**A delete-then-write lands whole or not at all, and one verb says so.** The studio stores by replacing —
`MapWriter.SaveDocAsync` drops a map's entities and rewrites them, `WorldFeatureWriter` drops six tables and
fills five, `MapArtifactStore.SaveAsync` deletes a row and inserts one — so the window between the two halves
is a state no author authored and no read can tell from a map that really has less in it. All three ask
`PgmDb.InOneWriteAsync`, which owns the boundary. It joins rather than opens where one is already running,
because a connection carries a single transaction and a finished sketch legitimately writes its segment rows
and its three artifacts through two writers over one of them. The nine library-store writes still open theirs
by hand (`RP27`); each is a leaf nothing calls into, so they are a second shape rather than a second
guarantee.

**Every document a caller can replace carries a revision.** A read answers it as an `ETag` and a write may
state it back as an `If-Match`; one naming a revision the document is no longer at is refused as `RQ5`, and
one stating nothing writes unguarded, because protection is opted into by having read first. The map
document and each artifact are counted apart — a caller holding the sketch layout's revision has said nothing
about the map's. The compare is one statement with the revision in its `where`, so the database decides which
of two writers wins rather than a read-then-write that both can pass.

**The most expensive operation in the studio is a `GET`.** `GET /map/{slug}/export` composes the map,
synthesises the entire voxel world, writes it to a temp directory and zips it in memory before answering.
There is no job id and nothing to ask afterwards, so a caller whose connection drops cannot learn whether the
build succeeded and repeats it to find out.

## What is already right

A survey that lists only faults describes a worse system than the one that exists.

`PgmStudio.Geom` references nothing — not even `Domain` — and every symmetry and distance computation routes
through it. A true leaf that the whole tree can reach is rare and it is the reason the geometry
consolidation was possible at all.

`Finding` and `Findings` are one shape with one verb, in the lowest project every gate reaches.
`MapArtifactStore` is one row per `(map_id, kind)` and the only place that table is touched. `RuleCatalog`
reading docstrings is documentation-as-data, and it is the pattern the rest of this document keeps asking
for. `DocumentedBodyTests` extracts request bodies out of the markdown and posts them, so a document
carrying an example the API stopped accepting fails a test — the strongest anti-rot mechanism here, currently
covering 8 bodies against 93 write routes.

And the standing refusal of backward compatibility is what makes every finding above fixable in one commit
rather than negotiable across a deprecation window.

## The patterns this is reaching for

Each of these is a solved problem outside this repository, and in every case the studio has built most of the
answer already and stopped one step short of the form that makes it machine-readable.

| What is missing | The established shape | What it dissolves |
|---|---|---|
| a generated client and generated endpoint tables | the schema at `/api/openapi/v1.json` is the source both should read | the two hand-kept copies that remain, and most of the doc-rot rule's hardest half |
| a request shape that is bound, not only declared | a request record per route, bound at the edge — parse rather than validate | the 15 `Unreadable` throws that stand where a binding would have refused, and the one global input gate covering a third of the write surface |
| a use case that is not an HTTP handler | ports and adapters: an application layer of request-in / `Findings`-out operations, with HTTP, the CLI and tests as three adapters | a step of the pipeline reachable only through its own door, and the 37-fold load-or-404 prologue |
| a fault category beside the fault id | a closed category set carried beside the rule, as gRPC, Stripe and RFC 9457 all do | five ids for one fault, `PL2` against `EX2`, and every caller that had to learn 77 ids to branch once — **shipped**, as `category` and `concerns` on `/api/rules` |
| a refusal envelope that is a standard | RFC 9457 Problem Details — `type` as a URI that dereferences to the rule, `title`, `status`, `detail`, findings as an extension | a bespoke envelope every client must be taught, and a rule catalogue that is already a lookup service but is not linked as one |
| a lifecycle a caller can read | the stage, the layers and the moves they allow, on the map's own read — a marker rather than a lock, since nothing reading back up is not the same as nothing going back | **shipped** on `GET /map/{slug}/layers`; `capabilities.md` had been the only answer to a runtime question |
| a pre-flight for a late gate | run each gate at the earliest stage that has the facts, and report it as a complaint there | the build an agent paid to hear a refusal — **shipped**, for the two objective gates at their own steps and for every readable gate at once on `GET /map/{slug}/findings` |

**They depend on each other in one order, and the first of them is in place.** The surface is described, so
a declared request shape and a generated client now have something to hang off. The application layer comes
next, because it is where a gate stops belonging to a door. The fault category is third and is a change to
an attribute plus a sweep of 77 constants, and is done. The lifecycle is last, because a state machine over a
pipeline whose steps are still HTTP handlers has nothing to hold.

None of this is a rewrite. Every one is a shape the codebase already half-has, stated once instead of by
hand at each of the places that need it — which is the rule `CLAUDE.md` opens with, applied to the boundary
rather than to a type.
