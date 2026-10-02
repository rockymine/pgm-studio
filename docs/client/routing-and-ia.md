# Routing and information architecture

Where everything lives in the URL, what each surface is called, and how a reader gets from the landing to a
map and back. It owns four things nothing else does: the **URL law**, the **route table**, the **labels** the
UI shows against the names the code uses, and the **collections** the maps page fans into. It does not
describe what any tool does — that is `../tools/`, starting at `flow.md`, which also owns the difference
between a map's stage and the artifacts it carries.

## The URL law

**The map is the resource, so it lives in the path, and the mode is a trailing segment.** Never `?map=…`: a
query parameter reads as an optional filter, and an editor without a map is not a page at all.

**The id is the on-disk map directory name** — maps are already clean slugs (`thunder`, `pigland`,
`dragons_hearth`), so `/maps/thunder/configure` works and matches the `api/map/{slug}` calls beside it. The pretty
`<name>` out of the XML is shown in the UI and never put in a URL: spaces and capitals force encoding and are
not stable across a rename.

**Query parameters are transient view state only** — selection, zoom, the active layer, an open panel, the
phase a tool opens on (`?phase=info`). The maps page's
`?stage=` fits the rule: it selects which collection is shown, not which map.

## The routes

| Route | Component | Is |
|---|---|---|
| `/` | `Index` | the landing — seven cards over live counts |
| `/maps` | `Maps` | the map collections; `?stage=plan\|sketch\|configure\|edit` selects one, absent lists every map |
| `/maps/{slug}/plan` | `PlanTool` | the plan tool on a map |
| `/maps/{slug}/sketch` | `SketchTool` | the sketch tool |
| `/maps/{slug}/configure` | `ConfigureTool` | the configure wizard |
| `/maps/new` | `ConfigureTool` | the same tool with no slug: its Import phase, which is how a world becomes a map row |
| `/plans/{id}` | `PlanTool` | the same tool on a plan row rather than a map: a pinned generator candidate or a plan saved without one |
| `/plans/new` | `PlanTool` | the same tool on a blank plan, which Save stores as a row and then moves to `/plans/{id}` |
| `/generator` | `GeneratorTool` | the composer's browse-and-pin gallery |
| `/catalog` | `CatalogTool` | the shape catalog |
| `/library` | `LibraryTool` | the chooser — one card per library kind |
| `/library/{kind}` | `LibraryTool` | one library's browse grid; `kind` is `styles\|themes\|roofs\|storeys\|porches\|houses` |
| `/library/{kind}/{entry}` | `LibraryTool` | one entry's editor; `entry` is a row id or `new` |
| `/design` | `Design` | the component showcase |
| `/not-found` | `NotFound` | 404 |

Two tools are carried more than once, and both times the slug-less route is the origination surface: a map has
no id until Import creates its row, and a plan has no map until it is built or authored onto one. A plan row is
a resource of its own, so it lives in the path the way a map does, and `new` stands where its id will be. Blazor discovers routes from
`@page`, so this table is the only inventory there is — nothing in the app enumerates them.

## How the client is served

The Blazor client is **hosted by the API**, and one hosting detail is load-bearing enough to state here:
**do not use `app.MapStaticAssets()`** in this setup. It breaks the framework boot — `_framework/*` answers
500 and the app hangs on "Loading" — because the fingerprinted asset manifest it installs does not match what
the WASM runtime asks for. What serves the client instead is a path-rewrite middleware that maps a
fingerprinted `/js/…<hash>.js` back to the real file name, and JS modules are loaded by a native `import()`
from the classic `studio.js` rather than through the framework's asset pipeline.

**Every hand-written file is revalidated on every use.** The CSS and JS under `wwwroot` keep their names across
deploys, and a module imports its siblings by those names, so nothing in a URL changes when its file does.
`UseStaticFiles` and the `index.html` fallback therefore send `Cache-Control: no-cache` in every environment: a
browser keeps its copy but asks before each use, and gets a 304 when nothing changed. Without it a browser
keeps a file for a freshness window it guesses from `Last-Modified`, a hard reload does not reach a module
imported after load, and a deploy arrives piecemeal: new markup over an old stylesheet, a new module importing
an old one that lacks the export it asks for. `smoke.mjs` checks the header on the page, a stylesheet and a
module.

## The collections

`/maps` lists every map in the studio, each row naming the stage it has reached, and `?stage=` narrows it to
one of four collections that are not the same kind of question. Two list **a layer a map holds** and two list
**a stage a map stands at**, which is why a map appears in more than one.

| List | Shows | Primary action |
|---|---|---|
| **Plans** (`?stage=plan`) | every map holding a plan, including ones long since built | New plan |
| **Sketches** (`?stage=sketch`) | every map holding a drawn sketch, including ones already configured | New sketch |
| **Configuring** (`?stage=configure`) | maps standing at `configure` — terrain but no finished `map.xml` | Import a world |
| **Finished** (`?stage=edit`) | maps standing at `edit` — a finished `map.xml`, which the corpus import writes and no authoring tool sets | — |
| **Maps** (`/maps`) | every map, with its stage on the row | — |

A row opens the tool at its map's stage. A map at `edit` has no tool at its stage, so its row opens the last
layer it holds — Configure, where it has a world — and one holding no layer is listed without a link.

A map keeps every layer it has ever had, so "every map with a plan" and "every map at the plan stage" are
different collections; each list says which in its own blurb. `GET /api/maps[?stage=…]` serves them and
`GET /api/maps/stage-counts` the landing tallies (sketches, configuring, and every map), each counting exactly what its list does — so a card and the
page it opens cannot disagree.

## The landing

Seven cards in two groups. The first four are where authoring starts — **Plan a layout** (the Plans collection),
**Browse generated layouts** (`/generator`), **Shape catalog** (`/catalog`) and **Pattern and palette library**
(`/library`) — three of which need no map at all. The last three are the map lifecycle — **Sketch**,
**Configure**, **Maps** — each deep-linking into its collection and carrying that collection's live count.

## Labels against code names

The visible label is deliberately decoupled from the concept the code is built on, in five places:

| UI label | Code name | Where |
|---|---|---|
| **Configure** | **authoring** — `MapIntent`, the intent model, `../pgm/new-map-authoring.md` | `/maps/{slug}/configure` |
| **Sketch** | sketch | `/maps/{slug}/sketch` |
| **Pattern** | **style** — `StyleDto`, `LibraryKinds.Styles` | `/library/styles` |
| **Palette** | **theme** — `TerrainTheme`, `../world-export/terrain-painting.md` | `/library/themes`, the sketch's `theme` phase |
| **Terraform** | **relief** — `../world-export/relief.md` | the sketch's `relief` phase |
| **Decoration** | **dressing** — `../world-export/decoration.md` | the sketch's `dressing` phase |

The reason is that "authoring" names what the tool *does* to a map and "Configure" names what a person came to
do, and the two audiences are different. Renaming the concept to match the label would have touched the intent
model, its endpoints and every document that reasons about it; renaming the label costs nothing. The other
four are the words mapmakers use (`writing-for-the-ui.md`, *Terms a new user has to learn*); the code, its
routes and the documents keep their own.

**"New" is not a discriminator.** Sketch and Configure both produce a new map, so a label built on "new" would
separate nothing. The axes that do separate them are the artifact (geometry against configuration) and the
lifecycle position (no `map.xml` yet against has one), which is why the labels are verbs.

## Exits

**Every page carries two bars, and each answers one question.** The studio's own bar (`AppNav`) is the same
everywhere: home, a link to each tool — Maps, Plan editor, Generator, Catalog, Library, and Users for an admin
— lit on the page it names and every page under it (Plan editor opens `/plans/new` and is lit on every
`/plans/…` row), and at the right the theme and the account. The tool's bar
(`Topbar`) under it is the trail to where the page is and the tool's own state and actions. So getting to
another tool is always one click in the top bar, and the tool's bar holds nothing that is not the tool's.

**A map tool leaves through the collection it belongs to.** Its bar's home link is that exit, and each of the
three map tools names its own list: Sketch → *Sketches*, Plan → *Plans*, Configure → *Configuring*. The
surfaces that hold no map — a plan row, the generator, the catalog, the library, the design showcase and the
maps page itself — carry no home link, because the studio's bar above them is already the way home.

Beside that link the tool's bar carries the trail — the map's name, then the tool or phase, dimmed. Neither is a
link: the map is already open, so a second way to it would be a way to nowhere.

A finished sketch does not leave its tool to be handed over: **Download map** in the sketch's bar builds the
world and saves the export where the author is (`docs/tools/sketch.md`). Configure is reached from the map's
row, or from the bar when the export is refused for something only Configure states.
