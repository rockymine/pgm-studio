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
phase and step a tool is on (`?phase=draw&step=layout`, which the sketch rewrites as the author moves). The maps page's filters fit the rule: they narrow which maps are
shown, never name one.

## The routes

| Route | Component | Is |
|---|---|---|
| `/` | `Index` | the landing — a hero with an in-game picture, the maps last changed as cards, the tools, a way into the glossary |
| `/maps` | `Maps` | every map in one table; `?stage=`, `?author=`, `?gamemode=` and `?sort=` are its filters |
| `/maps/{slug}/sketch` | `SketchTool` | the sketch tool |
| `/maps/{slug}/configure` | `ConfigureTool` | the configure wizard |
| `/maps/new` | `ConfigureTool` | the same tool with no slug: its Import phase, which is how a world becomes a map row |
| `/generator` | `GeneratorTool` | the composer's browse gallery, whose one exit makes a map of a board and opens it in the sketch tool |
| `/catalog` | `CatalogTool` | the shape catalog; not in the studio bar, it is linked from the generator |
| `/library` | `LibraryTool` | the chooser — one card per library kind |
| `/library/{kind}` | `LibraryTool` | one library's browse grid; `kind` is `styles\|themes\|roofs\|storeys\|porches\|houses` |
| `/library/{kind}/{entry}` | `LibraryTool` | one entry's editor; `entry` is a row id or `new` |
| `/rules` | `Rules` | every rule the studio can cite, filtered and opened one at a time; `?rule=` opens on one (`docs/refusals.md`) |
| `/glossary` | `Glossary` | every word the studio uses, defined once (`GET /api/glossary`): A to Z under a letter bar, every retired name filed as a see entry, `?q=` searches name first, and `#hub` lands on a term through any search |
| `/tokens` | `Tokens` | the signed-in person's API tokens |
| `/admin/users` | `Users` | who may sign in, and with which role |
| `/design` | `Design` | the design system: tokens and every shared component, grouped by role |
| `/not-found` | `NotFound` | 404 |

Configure is carried twice, and the slug-less route is the origination surface: a map has no id until Import
creates its row. The client has no plan surface — a plan reaches the studio only as an import
(`POST /api/plan/compile`) and is otherwise read-only history. Blazor discovers routes from `@page`, so this
table is the only inventory there is — nothing in the app enumerates them.

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
| **Stage** | `?stage=sketch\|configure` | the maps standing at that stage |
| **Author** | `?author=` (repeated) | maps credited to any one of the ticked authors |
| **Gamemode** | `?gamemode=ctw\|dtm\|dtc\|none` (repeated) | maps played for any one of the ticked gamemodes, each shown by its full name, `none` for a map with none |
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
carries **New map**, which makes a sketch and opens it on its Identity phase, and **Import a world** whatever the
filters are.

**A row opens the tool at its map's stage.** A map at `edit` has no tool at its stage, so its row opens the
last layer it holds — Configure, where it has a world — and one holding no layer opens nothing. The **Open in**
column is two fixed slots, Sketch · Configure, in the same place on every row. A layer the map holds is
a link straight into its tool, so a configured map's sketch is one click away whatever stage filter is
on; the one the row opens is outlined and carries the stage's dot, which is how the table says where a map
stands; a layer it lacks is an empty dashed slot. Below 1200px the table drops Game mode and keeps the slots.

`GET /api/maps` serves the table: every map, each with its stage, layers, credited authors, when it was last
written, and `youWroteAt` — when the caller last changed its documents themselves, in a browser rather than
through a token, read off the change log by account (or, for an open studio's local admin, by name) — filtered on
the client. Its own `?stage=` keeps the collection meaning an agent drives it by —
`sketch` lists the maps holding a sketch, `configure` and `edit` the maps standing at that stage — while the
page's chips filter by where a map stands.
`GET /api/maps/stage-counts` gives the landing tallies, each counting what the page shows when that card opens
it.

## The landing

**The landing is not a tool, so its shell has no tool bar.** `Index` gives `StudioShell` no `Bar`, which leaves
only the studio's own bar above the page. The page is one centred column, 1240px wide, with sections 56px apart.

**The hero names what the studio makes and offers two ways in.** Two equal columns: an eyebrow naming the game
modes, a title and lead describing the pipeline from layout to `map.xml`, and two 44px buttons (`Button` with
`Size="lg"`) — **New map** (`NewMapButton`, the same button the map list's header carries) makes a sketch and opens it on its Identity phase, and **Browse maps** opens the maps page unfiltered. Beside the
text sits a 16:9 `MapThumb` of the newest built map — a sketch layout and its world — drawn in game; with no such map it is
the studio mark on a token-coloured panel.

**A map picture is `GET /api/map/{slug}/render/picture`, over a placeholder.** `MapThumb` holds a fixed 16:9 box
with the map's initial in it, loads the picture lazily on top and fades it in. A map that is not built (a sketch
layout and the world compiled from it) asks for no picture, since only a built one is sure to have ground to frame; a refused one (422 no ground, 503 `RQ10` without textures, a failed request) leaves the
placeholder, and a 429 from the build queue is asked again after a wait that doubles from one second, never
longer than its `Retry-After`, up to four times, so a broken-image icon never shows. The hero asks for 960 × 540 and
a recent map's card for 480 × 270, the size it is shown at.

**Your recent maps lists up to three maps.** They are the ones the reader changed themselves, newest first
(`youWroteAt` from `GET /api/maps`), each card linking into the tool its row on the maps page opens, with the
stage pill of its furthest layer and a line of first credited author and when it was edited. A reader who has
changed none, or is signed out, sees the three most recently updated maps under *Latest maps*.

**Tools and the guide close the page.** Four tiles — Generator, Shape catalog, Library, Rules — each an icon, a
name and a line; a map's own tools start from the maps page, so neither has a tile. A bordered row sends a new reader
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
| **Terraform** | **modifiers** — `../tools/sketch.md` | the sketch's terraform phase |
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

**Every page that is not an editor carries a studio bar and a bar of its own, and each answers one question.** The
studio's own bar (`AppNav`) is the same everywhere: the studio's mark and name, which go home, then **Maps ·
Generator · Library · Rules · Glossary**, each lit on the page it names and every page under it, so Maps is lit in a
map's tools; and at the right a search that opens the command palette (what `Ctrl K` also opens), icon buttons for
the keyboard shortcuts (the sheet `?` also opens) and the design reference (`/design`), then the text size, the theme
and the account. The account menu carries Tokens, and for an admin Users and **API docs** (`/api-docs`, which
opens in a new tab, being the API's own page rather than the client's); the page and its schema stay reachable to
anyone who has the address, and only the link is an admin's. In an open studio the `local admin` button opens the
menu with Users and API docs. A visitor is shown *Sign in with Discord* alone, with no menu beside it (`SignInButton`); it is the only
sign-in they are offered, and an editor draws the same button in its own bar, where the studio bar is not drawn. The page's
bar under it is the trail to where the page is and the page's own state and actions: a `Topbar`, drawn where the
page has a trail of more than its own name, actions, or a *View only* tag to show someone signed in. A page whose bar would hold only its own name —
Maps, the generator, the rules, the glossary — draws none, since its heading names it and the lit link above says
where it is. So getting to another tool is one click in the top bar, and the page's bar holds nothing that is not
the page's.

**Below 960px the studio bar folds into a menu.** The bar keeps the mark, the search and the account, and a menu
button at its left opens the links and the four tools as a panel sliding in from the left, over a dimmed page.
The panel takes focus when it opens; its ✕, the dimmed page, Escape and a move to another page close it, and
focus goes back to the button. In the panel each tool is a row with its name beside its glyph, and on a touch
screen the keyboard shortcuts and the search's *Ctrl K* are not offered. The breakpoints a narrow window passes through are `ui-conventions.md`'s *A narrow
window*.

**An editor draws one bar and one chapter list.** Sketch and Configure are focused pages (`StudioShell Focused`):
they draw neither the studio bar nor the footer, and their `EditorBar` stands in place of a `Topbar`, so the trail
and the work's commands are one row and the canvas has the window's full height. Left to right the bar holds the
studio's mark, with the studio's name beside it from 1280 px, which opens the same studio menu — carrying the
search and the account as well, since there is no studio bar to hold them — then the path ending in the tool, and
at the right the state, Undo and Redo, the tool's other commands, a divider and Back/Next. The bar is present in
every phase, Identity included, so the path never moves. **Where the work is, is the rail's alone**: the bar
names no phase and lists no step. The rail (`NavRail` with a `FoldKey`) is the tool's chapters — each phase a row
with its glyph and name, the open one in the accent, and under it, where it has more than one, its steps on a
thread with a dot each, the current one filled. A step is opened there; Back and Next walk them. The round control
on the rail's edge, level with a sidebar's first heading, folds it to its glyphs and the open phase's dots, each
name on hover, and it is remembered per tool in the browser (`studioRailFold`); the rail opens unfolded.

**A map tool leaves through the unfiltered maps list.** The crumb's first segment is *Maps*, a link to `/maps`
with no stage filter: the segment names its parent, so the link goes to the parent, and which stage a person was
looking at is a view of that list rather than a place. The map's name follows as plain text, because the map is
already open and a link to it would go nowhere. The phase is not in the crumb; it and its steps are the rail's.
The segments are separated by slashes, here and in every `Topbar`, so the tool's down chevron is the only chevron
in the bar. The surfaces that hold no map — the generator, the catalog, the
library, the design showcase and the maps page itself — draw no home link, because the studio's bar above them
is already the way home; on an editor the way home is the crumb's *Maps*, or the menu its mark
opens.

**The tool is the path's last word, and a switcher.** After the map's name `StageSwitch` shows the tool the page
is — *Sketch* or *Configure* — after its stage's dot, in the colour the map list gives that stage, as a button
whose menu lists the two. A tool is a link where the map holds that layer
and a dashed, disabled entry where it does not, the same distinction the Maps table draws in its *Open in*
column and read from the same place: `GET /api/maps` and `MapLayers.Holds`, fetched when the menu opens, because
saving a sketch is what makes the layer. The page's own tool is always offered, marked as current.
Where the page has no map (the import on `/maps/new`) the word is plain text in the same box. The
button is set in the crumb's own font, size, weight and line height with no vertical padding, so its text shares
the crumb's baseline; its hover and focus are a shadow that paints around the text without moving it
(`tests/e2e/editor-bar.mjs` measures the baseline to half a pixel).

**Below 960 px the move is a foot of its own.** The bar keeps one row pinned to the window's top — where the page
is, and the state of the work — and Back, Undo, Redo and Next move to a foot pinned to its bottom, where a thumb
reaches them, so scrolling to a panel under the canvas never takes the move or the save state off the screen.
Beside the page the rail is a column held in view, open or folded as on a wide window. Below 600 px the rail is not
drawn: the foot's middle is a button naming the phase and the step (*Draw*, *Ground*), and it opens a sheet drawing
the rail itself, unfolded, so the phase and its steps are one list in one place. The sheet takes focus; Escape, the
dimmed page and a pick close it. Nothing scrolls sideways at any width.

A finished sketch does not leave its tool to be handed over: **Download** — an icon in the editor bar's command
group, beside Undo and Redo — builds the world and saves the export where the author is (`docs/tools/sketch.md`).
Configure is reached from the map's row, from the stage switcher, or from the Problems popover when the export is
refused for something only Configure states.
