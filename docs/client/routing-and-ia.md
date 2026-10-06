# Routing and information architecture

Where everything lives in the URL, what each surface is called, and how a reader gets from the landing to a
map and back. It owns four things nothing else does: the **URL law**, the **route table**, the **labels** the
UI shows against the names the code uses, and the **filters** of the maps page. It does not
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
phase a tool opens on (`?phase=info`). The maps page's filters fit the rule: they narrow which maps are
shown, never name one.

## The routes

| Route | Component | Is |
|---|---|---|
| `/` | `Index` | the landing — a hero, the maps last changed, the four levels of a map |
| `/maps` | `Maps` | every map in one table; `?stage=`, `?author=`, `?gamemode=` and `?sort=` are its filters |
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
| `/rules` | `Rules` | every rule the studio can cite, filtered and opened one at a time; `?rule=` opens on one (`docs/refusals.md`) |
| `/tokens` | `Tokens` | the signed-in person's API tokens |
| `/admin/users` | `Users` | who may sign in, and with which role |
| `/design` | `Design` | the design system: tokens and every shared component, grouped by role |
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

## The maps page

`/maps` is every map in the studio in one table, newest written first, with its filters open in the sidebar
beside it. A map is one thing at different stages, so stage is a filter over the one list rather than a page
of its own. Every option carries how many maps it would show with the other filters as they are, and
the **Clear** beside the sidebar's *Filters* heading drops them all. The sidebar is a `FilterSidebar` of
`FilterGroup`s, the same rail the shape catalog and the generator use (`ui-conventions.md`).

| Filter | Address | Keeps |
|---|---|---|
| **Stage** | `?stage=plan\|sketch\|configure` | the maps standing at that stage |
| **Author** | `?author=` (repeated) | maps credited to any one of the ticked authors |
| **Gamemode** | `?gamemode=ctw\|dtm\|dtc\|none` (repeated) | maps played for any one of the ticked gamemodes, `none` for a map with none |
| **Sort** | `?sort=name\|author\|stage` | the order; absent is last changed |

**A map's gamemodes are read off its objectives, never off its `<gamemode>` label.** Its wools, shown
destroyables, cores, control points and kill-paying scores each name a mode. A sketch map's destroyables and
cores have no block volume until its world is built, so the ones its intent states count as well, and a DTM or
DTC board is listed as one from the moment it states its goals.

**Configure is the last stage the page offers.** It is where a map is downloaded. A map whose stage is `edit`
was read in by the corpus import, which reads a world that already has a `map.xml`; no authoring tool sets it,
and the page lists such a map under Configure, which is the tool its row opens.

**The Author filter lists every credited author in two groups, Agents and People,** each with **Select all**,
and a search box narrows both. A credit with an account is a person and shows the player's head; a credit by
name alone, with no account behind it, is how an agent is credited, and shows the agent mark. Contributors are
left out of the filter and the column. The Author column shows the first credited author, with a count of the
rest.

A search box over the table narrows it by name, slug or author and stays out of the address. The header
carries **New plan**, **New sketch** and **Import a world** whatever the filters are.

**A row opens the tool at its map's stage.** A map at `edit` has no tool at its stage, so its row opens the
last layer it holds — Configure, where it has a world — and one holding no layer opens nothing. The **Open in**
column is three fixed slots, Plan · Sketch · Configure, in the same place on every row. A layer the map holds is
a link straight into its tool, so a configured map's plan and sketch are one click away whatever stage filter is
on; the one the row opens is outlined and carries the stage's dot, which is how the table says where a map
stands; a layer it lacks is an empty dashed slot. Below 1200px the table drops Gamemode and keeps the slots.

`GET /api/maps` serves the table: every map, each with its stage, layers, credited authors, when it was last
written, and `youWroteAt` — when the caller last changed its documents themselves, in a browser rather than
through a token, read off the change log by account (or, for an open studio's local admin, by name) — filtered on
the client. Its own `?stage=` keeps the collection meaning an agent drives it by —
`plan` and `sketch` list the maps holding that layer — while the page's chips filter by where a map stands.
`GET /api/maps/stage-counts` gives the landing tallies, each counting what the page shows when that card opens
it.

## The landing

**The landing is not a tool, so its shell has no tool bar.** `Index` gives `StudioShell` no `Bar`, which leaves
only the studio's own bar above the page; every tool is linked from there, so the page carries no card for any
of them.

**The hero names what the studio makes and offers two ways in.** The title and lead describe the pipeline from
layout to `map.xml` across Capture the Wool, Destroy the Monument and Destroy the Core. **Start a map** opens the
maps page at the plan stage; **Open maps** opens it unfiltered and carries the map count from
`GET /api/maps/stage-counts`. Beside the text sits an inline SVG of a symmetric two-team board, filled and
stroked with the theme's tokens so it follows light and dark.

**Below the hero, two panels fit a 1920x1080 viewport without scrolling.** The first lists up to five maps the
reader changed themselves, newest first (`youWroteAt` from `GET /api/maps`), each linking into the tool its row
on the maps page opens; a reader who has changed none, or is signed out, sees the five most recently updated maps
under *Latest maps*. The second names the four levels a map is described at, from `../tools/flow.md`. Under
760px the hero and the panels stack in one column.

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
everywhere: home, a link to each tool — Maps, Generator, Catalog, Library, and Users for an admin
— lit on the page it names and every page under it (Maps is lit on every `/plans/…` route too, because a plan is entered from the
Maps page's *New plan*); after a divider the two reference pages, **Rules** (`/rules`) and **API docs** (`/api-docs`,
which opens in a new tab, being the API's own page rather than the client's); and at the right the theme and
the account. The tool's bar
(`Topbar`) under it is the trail to where the page is and the tool's own state and actions. So getting to
another tool is always one click in the top bar, and the tool's bar holds nothing that is not the tool's.

**A map tool leaves through the maps page, filtered to its stage.** Its bar's home link is that exit, and each
of the three map tools names its own view: Sketch → *Sketches*, Plan → *Plans*, Configure → *Configuring*. The
surfaces that hold no map — a plan row, the generator, the catalog, the library, the design showcase and the
maps page itself — carry no home link, because the studio's bar above them is already the way home.

Beside that link the tool's bar carries the trail — the map's name, then the tool or phase, dimmed. Neither is a
link: the map is already open, so a second way to it would be a way to nowhere.

A finished sketch does not leave its tool to be handed over: **Download map** in the sketch's bar builds the
world and saves the export where the author is (`docs/tools/sketch.md`). Configure is reached from the map's
row, or from the bar when the export is refused for something only Configure states.
