# How the studio is driven

`project-structure.md` says where a piece of code lives and which project may reference which. This document
says the other half: how a request becomes work, what the studio is able to say about itself, and where the
shape of that answer is decided. The two do not overlap — one is the package map, this is the boundary.

It matters because the studio has two audiences and only one of them can read prose. A person drives it
through a Blazor client that renders panels and a canvas; an agent drives it through `/api` and a CLI, and
everything it can learn about the map it has to learn from a response body. Every finding below is a place
where the studio knows something and cannot say it, or says it in a shape that has to be learned rather than
parsed.

## Two drivers, and neither is the other's client

The pipeline has two entry points. `PgmStudio.Api` exposes **154 endpoint classes** over 43 files, and
`tools/mapgen` links `Pgm`, `Minecraft`, `Export` and `Analysis` directly — it opens no `HttpClient` and
speaks no HTTP at all. Both compile a plan, rasterize a layout, dress a world and write a `map.xml`.

That is one pipeline with two independent front doors, and the consequence is structural rather than
incidental: **a gate belongs to whichever door someone put it behind.** The export is where that was visible
and where it is now closed — every gate a sketch map is judged by sits inside `MapExportComposer.ComposeSketch`,
so `mapgen`, which links that method directly, meets the chain the HTTP export meets. But closing an instance
is not closing the mechanism: nothing about the arrangement stops the next gate from landing on one door
again, because a use case that *is* an HTTP handler has no other place for one to live. That is `RP13`.

## The boundary carries no schema

The surface describes itself. `GET /api/openapi/v1.json` is generated from the routes and the DTOs — 121
paths, **154 operations**, 169 schemas — and `/api-docs` is the page over it, where a route can be expanded and
sent without writing a client. Both are served from the app's own assets.

What that document can say is bounded by what is declared, and most of the write surface declares nothing.
**133 of the 154 endpoints declare no typed request**, 21 do, and **25 call sites read the body as
`Dictionary<string, object?>`** through `RawBody` and `JsonTree`. Those routes appear in the
document with their path and verb and no body schema at all: the generator can only publish what the code
states.

Three things follow from that, and they are the same fact seen from three sides.

**The one global input gate covers an eighth of the surface.** `RequiredFields` refuses anything a request
DTO declares non-nullable and the body did not supply — and its first line is
`if (context.Request is not { } request) return;`, so it is a no-op for every endpoint that has no request
type. The promise it makes holds for 21 routes out of 154.

**A validation that cannot live in a schema lives in the code by hand.** The Edit tool's 74 refusal sites
across `Pgm/Editing` are, read as a group, a request schema: a field is absent, a value is outside a closed
set, a number is not one. Those are declarations, written as 74 `throw` statements because the request they
guard has no declared shape to hang them on.

**A response is described even less than a request, and where it is undescribed it is misdescribed.** An
endpoint that declares no response type is published as **204 No Content** — the generator's default, and a
claim rather than a silence: `GET /map/{slug}` answers a whole map document under it. **39 of the 154
operations** publish that 204 without answering it, and seven more publish it truthfully, every one a delete
whose answer is that the thing is gone. `SchemaCompletenessTests` holds both numbers, the second as a named
list, so a route that grows a body cannot leave the count quietly. The media types are declared: the six
`image/png` routes, the three `text/plain` ones and the export's `application/zip` all say so, so
`/api-docs` renders a theme swatch beside the route that draws it.

**The wire contract is generated once and kept by hand twice more.** The route attributes in
`Api/Endpoints` are the generator's source. Beside them sit **152 route strings** written out in the Blazor
client and the endpoint tables in the eight `docs/tools/` documents, neither of which is derived from the
schema. The client reads **59 responses as `JsonElement`** against 16 typed reads, across 38 files, so the
response shape is a third copy living in per-component parsing code; `Contracts` carries its DTOs for 154
endpoints. The split across the client's features says which half of the studio was built against a declared
shape: **Catalog 3 typed / 0 untyped and Generator 3 / 0**, against **Configure 1 / 27** and **Edit 0 / 14**.
The two pages with a typed contract are the two newest, and they are what the rest would look like read
through a generated client. The tables are the same problem seen from the prose side: three heavily used
analysis routes had drifted out of every one of them, which is not a documentation lapse but what a
hand-maintained copy of a machine-readable fact does. They are back, and `DocumentedRouteTests` now fails on
the next one — but a test that catches drift is not the same as a table that cannot drift, and the schema is
what a generated one would read.

## The use case has no name

`Api/Endpoints` holds **4,753 lines of code**; `Api/Services` holds **1,169**, and its contents are read-model
builders — `TerrainPreview`, `DressingPreview`, `PlanStructurePreview`, `WorldColumnPayload`. There is no
type in the tree whose job is *finish a sketch* or *export a map*.

`SketchFinishEndpoint.HandleAsync` is the finish use case: load the map, refuse a missing artifact, run the
document gate, rasterize, detect islands, refuse an empty board, write the feature artifacts, advance the
stage, answer. Every one of those steps is a decision about the domain, and all of them are reachable only by
sending an HTTP request. `tools/mapgen` needs the same steps and cannot call them, so it has its own copy —
which is the mechanism behind the previous section, stated at the level where it is fixable.

The load-the-map-or-404 prologue appears **49 times** in `Api`, 42 of them ending in the same refusal.

## The lifecycle is data, and nothing reads it

`map.stage` holds one of `plan`, `sketch`, `configure`, `edit`. It is written at map creation by the four
originating endpoints and once more by `sketch/finish`. Every other read is the dashboard's: the maps list
groups by it and the client's stage chips filter on it. **No endpoint refuses anything because of it, and no
endpoint answers what a map at a given stage may be asked for.**

`docs/tools/capabilities.md` is 716 lines answering exactly that question in prose. `flow.md` states the four
levels of description and the five hand-offs between them, and states them well — but only as prose. There is
no type for a level, no transition table, and no way for a caller to ask what it may do next. An agent
learns the pipeline by reading markdown that nothing verifies, or by trying a call and reading the refusal.

That is also what makes `RP4` expensive. An agent pays a full world build to discover that a goal overhangs
void, because the only thing that will tell it is the gate at the end.

## A fault has an id but not a class

The studio declares **71 rule constants in 14 families**, and answers `GET /api/rules` by reading each
constant's own XML docstring — so a rule's meaning has one home and no catalogue can fall out of step with
it. That mechanism is right, and it is the best thing in the codebase.

What it lacks is a **class**. A caller that wants to know whether to fix the request, change the design,
change the map or report a bug has to know all 71 ids to find out, because the only machine-legible thing a
finding carries is the id itself. That is the defect, and it is the whole of it.

The family prefix is *not* the defect, though it reads like one at first. `PL2` and `EX2` carry nearly the
same sentence — *no spawn, nobody can enter the map* — and look like one fault under two ids until their
`fix` lines are read: `PL2` says *add an entry in `placements.spawns`*, `EX2` says *give the intent at least
one spawn*. Two documents, two things to go and change, correctly two rules. The same holds for the five ids
that all mean *a name that resolves to nothing* — `PL5` and `PL10` over the plan, `SK3` over the layout,
`ED1` over the map document, `RQ4` over the request. One category, four documents, and the prefix is doing
real work.

So the prefix is close to a **subject** axis already, because for most families the subsystem that asks *is*
the document at fault. What is missing beside it is the category, and the two together are what let a caller
read `PL2` as *unplayable, in the plan* and `EX2` as *unplayable, in the intent* without knowing either id.

**Rules are stated three ways, and only two of them can be checked.** The 71 constants are one; the layout
law in `docs/generator/rules.md`, embedded and parsed, is a second, and both are answered by `/api/rules`.
The third is a bare string literal at the throw site — the plan lint cites fourteen ids that way, `SP2`,
`EL1`, `ST8`, `WL1`, `CT12`, `BZ5` and the rest. Thirteen of those resolve, because they are layout rules
that `rules.md` states. Nothing checks that they do: a typo produces a finding citing a rule nobody has, and
the catalogue is not consulted.

The fourteenth is `WX8`, and it is what that gap looks like once it has happened. It is fired by the lint,
stated as a rule in `docs/world-export/structures.md`, cited as one in `docs/tools/plan.md` — and declared in
no `*Rules` class and absent from `rules.md`, so **`GET /api/rules?rule=WX8` answers an empty list.**
`WX9`, beside it in the same document, is never fired at all. `RulesEndpointTests` cannot catch either: it
checks that every declared rule carries a sentence, which is the opposite direction.

The missing distinction is between the **category** of a fault, which is a small closed set an agent
branches on, and the **rule**, which is specific, stable and for a reader. A caller that wants to know
whether to retry, to fix the document or to give up currently has to know all 71 ids to find out.

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

**The studio has the mechanism that catches every one of those, and it runs on two endpoint files.**
`DocumentShape.Unread` walks a parsed document beside the value it deserialized to and names every property
nothing could keep — `RQ3`, a complaint on the success response, by path. It is wired to the room-style
library and the terrain previews. The **sketch layout, the plan and the intent** — the three documents an
agent actually authors — have no unread check, which is why a misspelled field in one of them is silence.

**A refusal that cannot say what would satisfy it is a refusal a driver works around.** Run 4's board hit
`PL11`, `WX6` and `SP1` on a capture-the-wool plan, and the run's own report says what happened next:
*"Rather than iterate extensively, I simplified to a basic destroy board."* The brief asked for CTW; a
different map shipped, and the report names what would have prevented it — "a working example to follow".
Every gate can say what is wrong with a document. None can answer with one that would be right.

Read together, none of that is new: it is the six findings above, seen from the driver's side. The eleven-call
loop and its load-bearing order are what an absent application layer looks like to a caller. The fifteen
required documents and the errata file are what an absent contract looks like. And the silent 200s are what
an absent request shape looks like, on exactly the documents that matter most.

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
| a declared request shape | a request record per route, bound at the edge — parse rather than validate | 145 unguarded routes, and most of the Edit tool's 74 hand-written checks |
| a use case that is not an HTTP handler | ports and adapters: an application layer of request-in / `Findings`-out operations, with HTTP, the CLI and tests as three adapters | the second pipeline in `tools/mapgen`, and the 49-fold load-or-404 prologue |
| a fault category beside the fault id | a closed category set (`malformed`, `not_found`, `conflict`, `unresolved`, `unsatisfiable`, `internal`) carried beside the rule, as gRPC, Stripe and RFC 9457 all do | five ids for one fault, `PL2` against `EX2`, and every caller that has to learn 71 ids to branch once |
| a refusal envelope that is a standard | RFC 9457 Problem Details — `type` as a URI that dereferences to the rule, `title`, `status`, `detail`, findings as an extension | a bespoke envelope every client must be taught, and a rule catalogue that is already a lookup service but is not linked as one |
| a lifecycle that is enforced | a state machine over `MapStage` with a transition table, and the allowed transitions on the map's own response | `capabilities.md` as the only answer to a runtime question, and an agent that learns the pipeline by trying it |
| a pre-flight for a late gate | run each gate at the earliest stage that has the facts, and report it as a complaint there | `RP4`, and the build an agent pays to hear a refusal |

**They depend on each other in one order, and the first of them is in place.** The surface is described, so
a declared request shape and a generated client now have something to hang off. The application layer comes
next, because it is where a gate stops belonging to a door. The fault category is third and is a change to
`Finding` plus a sweep of 71 constants. The lifecycle is last, because a state machine over a pipeline whose
steps are still HTTP handlers has nothing to hold.

None of this is a rewrite. Every one is a shape the codebase already half-has, stated once instead of by
hand at each of the places that need it — which is the rule `CLAUDE.md` opens with, applied to the boundary
rather than to a type.
