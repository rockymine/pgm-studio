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

The pipeline has two entry points. `PgmStudio.Api` exposes **167 endpoint classes** over 41 files, and
`tools/mapgen` links `Pgm`, `Minecraft`, `Export` and `Analysis` directly — it opens no `HttpClient` and
speaks no HTTP at all. Both compile a plan, rasterize a layout, dress a world and write a `map.xml`.

That is one pipeline with two independent front doors, and the consequence is structural rather than
incidental: **a gate belongs to whichever door someone put it behind.** `MapExportComposer.Compose` runs the
unknown-gamemode refusal and the traversability judgement and then hands on to `ComposeSketch`, which runs
the rest; `mapgen` calls `ComposeSketch` and so runs the second half only. The board files that as `RP3` and
frames it as a chain to be reordered. It is not — reordering the chain fixes this instance and leaves the
mechanism, because nothing about the arrangement stops the next gate from landing on one door again.

## The boundary carries no schema

Of the 167 endpoints, **110 are `EndpointWithoutRequest`** and 22 declare a typed request; **51 call sites
read the body as `Dictionary<string, object?>`** through `RawBody` and `JsonTree`. There is no OpenAPI
document and no Swagger generation configured.

Three things follow, and they are the same fact seen from three sides.

**The one global input gate covers an eighth of the surface.** `RequiredFields` refuses anything a request
DTO declares non-nullable and the body did not supply — and its first line is
`if (context.Request is not { } request) return;`, so it is a no-op for every endpoint that has no request
type. The promise it makes holds for 22 routes out of 167.

**A validation that cannot live in a schema lives in the code by hand.** The Edit tool's 74 refusal sites
across `Pgm/Editing` are, read as a group, a request schema: a field is absent, a value is outside a closed
set, a number is not one. Those are declarations, written as 74 `throw` statements because the request they
guard has no declared shape to hang them on.

**The wire contract exists in three hand-maintained copies.** The route attributes in `Api/Endpoints`, **152
route strings** written out in the Blazor client, and the endpoint tables in the eight `docs/tools/`
documents. The client reads **59 responses as `JsonElement`** against 16 typed reads, across 38 files, so
the response shape is a fourth copy living in per-component parsing code. `Contracts` carries 15 DTOs for
167 endpoints. `TC1` — three heavily used analysis routes appearing in no endpoint table — is not a
documentation lapse; it is what a hand-maintained copy of a machine-readable fact does.

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

The taxonomy it serves is not. A family prefix names **which subsystem asked**, not **what kind of fault it
is**, and the two are different questions. The catalogue's own stated principle, in `refusals.md`, is that
"ids are grouped by what they are about, never by which gate happens to ask" — and the catalogue breaks it:

| Rule | Its own sentence |
|---|---|
| `PL2` | *No spawn: PGM has nowhere to put a player and the map cannot be entered.* |
| `EX2` | *Nobody can enter the map: it declares no spawn of any kind.* |

One fault, two ids, because the plan gate and the export gate each asked it. The same happens to *a name that
resolves to nothing*, which is `PL5`, `PL10`, `SK3`, `ED1` and `RQ4` depending on who noticed.

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
| a machine-readable contract | OpenAPI, generated from the endpoints; a generated client; endpoint tables rendered from the schema | the three hand-kept copies of the wire contract, `TC1`, and most of the doc-rot rule's hardest half |
| a declared request shape | a request record per route, bound at the edge — parse rather than validate | 145 unguarded routes, and most of the Edit tool's 74 hand-written checks |
| a use case that is not an HTTP handler | ports and adapters: an application layer of request-in / `Findings`-out operations, with HTTP, the CLI and tests as three adapters | the second pipeline in `tools/mapgen`, `RP3`, and the 49-fold load-or-404 prologue |
| a fault category beside the fault id | a closed category set (`malformed`, `not_found`, `conflict`, `unresolved`, `unsatisfiable`, `internal`) carried beside the rule, as gRPC, Stripe and RFC 9457 all do | five ids for one fault, `PL2` against `EX2`, and every caller that has to learn 71 ids to branch once |
| a refusal envelope that is a standard | RFC 9457 Problem Details — `type` as a URI that dereferences to the rule, `title`, `status`, `detail`, findings as an extension | a bespoke envelope every client must be taught, and a rule catalogue that is already a lookup service but is not linked as one |
| a lifecycle that is enforced | a state machine over `MapStage` with a transition table, and the allowed transitions on the map's own response | `capabilities.md` as the only answer to a runtime question, and an agent that learns the pipeline by trying it |
| a pre-flight for a late gate | run each gate at the earliest stage that has the facts, and report it as a complaint there | `RP4`, and the build an agent pays to hear a refusal |

**They depend on each other in one order.** The contract comes first, because a declared request shape and a
generated client both hang off it and because nothing else can be verified until the surface is described.
The application layer comes second, because it is where a gate stops belonging to a door. The fault category
is third and is a change to `Finding` plus a sweep of 71 constants. The lifecycle is last, because a state
machine over a pipeline whose steps are still HTTP handlers has nothing to hold.

None of this is a rewrite. Every one is a shape the codebase already half-has, stated once instead of by
hand at each of the places that need it — which is the rule `CLAUDE.md` opens with, applied to the boundary
rather than to a type.
