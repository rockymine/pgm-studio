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
| `/` | `Index` | the landing — a hero with an in-game picture, the maps last changed as cards, the tools, a way into the glossary |
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
only the studio's own bar above the page. The page is one centred column, 1240px wide, with sections 56px apart.

**The hero names what the studio makes and offers two ways in.** Two equal columns: an eyebrow naming the three
modes, a title and lead describing the pipeline from layout to `map.xml`, and two 44px buttons (`Button` with
`Size="lg"`) — **New map** opens the maps page at the plan stage, **Browse maps** opens it unfiltered. Beside the
text sits a 16:9 `MapThumb` of the newest map that holds a sketch layout, drawn in game; with no such map it is
the studio mark on a token-coloured panel.

**A map picture is `GET /api/map/{slug}/render/picture`, over a placeholder.** `MapThumb` holds a fixed 16:9 box
with the map's initial in it, loads the picture lazily on top and fades it in. A map with no sketch layout asks
for no picture; a refused one (422 no ground, 503 `RQ10` without textures, a failed request) leaves the
placeholder, and a 429 from the build queue is asked again after its `Retry-After`, so a broken-image icon never
shows.

**Continue working lists up to three maps.** They are the ones the reader changed themselves, newest first
(`youWroteAt` from `GET /api/maps`), each card linking into the tool its row on the maps page opens, with the
stage pill of its furthest layer and a line of first credited author and when it was edited. A reader who has
changed none, or is signed out, sees the three most recently updated maps under *Latest maps*.

**Tools and the guide close the page.** Four tiles — Generator, Shape catalog, Library, Rules — each an icon, a
name and a line; plans start from the maps page, so the plan editor has none. A bordered row sends a new reader
to `/glossary`. Under 1000px the cards and tiles take two columns, and under 760px everything stacks in one with
the hero picture first.

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

**Every page carries a studio bar and a bar of its own, and each answers one question.** The studio's own bar (`AppNav`) is the same
everywhere: home, a link to each tool — Maps, Generator, Catalog, Library, and Users for an admin
— lit on the page it names and every page under it (Maps is lit on every `/plans/…` route too, because a plan is entered from the
Maps page's *New plan*); after a divider the two reference pages, **Rules** (`/rules`) and **API docs** (`/api-docs`,
which opens in a new tab, being the API's own page rather than the client's); and at the right the theme and
the account. The page's bar under it is the trail to where the page is and the page's own state and actions: a
`Topbar` on a browse or admin page, and the one `EditorBar` on the three editors. So getting to another tool is
always one click in the top bar, and the page's bar holds nothing that is not the page's.

**The editors draw one bar, not two.** Plan, Sketch and Configure draw `EditorBar` in place of a `Topbar` and a
step strip, so the trail, the phase and the work's commands are one row of 44 px. Left to right it holds the
crumb and the stage switcher, the phase's icon and name with its steps, and at the right the state, the tool's
commands, a divider and Back/Next. The bar is present in every phase, Info included, so the crumb never moves.

**A map tool leaves through the unfiltered maps list.** The crumb's first segment is *Maps*, a link to `/maps`
with no stage filter: the segment names its parent, so the link goes to the parent, and which stage a person was
looking at is a view of that list rather than a place. The map's name follows as plain text, because the map is
already open and a link to it would go nowhere. The phase is not in the crumb; the phase and its steps are the
bar's middle, and the rail names them again. The surfaces that hold no map — the generator, the catalog, the
library, the design showcase and the maps page itself — draw a `Topbar` with no home link, because the studio's
bar above them is already the way home. A plan row (`/plans/{id}`, `/plans/new`) is an editor with no map: it
keeps the `Maps` crumb and puts the plan's name after it.

**The tool is a switcher, not a crumb.** After the map's name `StageSwitch` shows the tool the page is — *Plan*,
*Sketch* or *Configure* — as a button whose menu lists the three. A tool is a link where the map holds that layer
and a dashed, disabled entry where it does not, the same distinction the Maps table draws in its *Open in*
column and read from the same place: `GET /api/maps` and `MapLayers.Holds`, fetched when the menu opens, because
saving a plan or a sketch is what makes the layer. The page's own tool is always offered, marked as current.
Where the page has no map (a plan row, the import on `/maps/new`) the word is plain text in the same box. The
button is set in the crumb's own font, size, weight and line height with no vertical padding, so its text shares
the crumb's baseline; its hover and focus are a shadow that paints around the text without moving it
(`tests/e2e/editor-bar.mjs` measures the baseline to half a pixel).

**Below 1100 px the steps become a menu.** The step strip is replaced by *Step n of m*, a button whose menu lists
the steps, so the bar stays one row; at phone width the crumb takes a row of its own and the bar wraps, and
nothing scrolls sideways.

A finished sketch does not leave its tool to be handed over: **Download** — an icon in the editor bar's command
group, beside Undo and Redo — builds the world and saves the export where the author is (`docs/tools/sketch.md`).
Configure is reached from the map's row, from the stage switcher, or from the Problems popover when the export is
refused for something only Configure states.
