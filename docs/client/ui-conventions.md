# The component vocabulary

The studio's Blazor UI is built from a shared set of components rather than copy-pasted class markup. This is
the reference for which component to reach for, what each one takes, and the handful of rules that keep Blazor
and the CSS from fighting each other. Where a component lives is `CLAUDE.md`'s Client folder rule
(`Pages/`, `Features/<Tool>/`, `Components/`); this is what is *in* `Components/`.

Read alongside:
- `../../src/PgmStudio.Client/wwwroot/css/studio/tokens.css` — the custom properties. A component never
  hardcodes a colour, a space or a radius; it emits classes that resolve to tokens.
- `canvas-interaction.md` — the canvas primitive palette, a separate visual system for things drawn on a
  canvas rather than laid out in the DOM.
- The `/design` page (`Pages/Design.razor`) is the living style guide, and it renders the **real** components
  rather than hand-written examples, so the showcase cannot drift from production. It is the visual-regression
  oracle for any change here.

## Why components at all, given global CSS

The studio styles by **global class names**, not scoped `.razor.css`, and that is load-bearing rather than
incidental: JavaScript reaches elements by class, the theme system swaps tokens under those classes, and
`/design` documents them. What it costs is that the markup using a class is pure structure with no logic — so
it gets copy-pasted, and a page that needs a modifier reaches for an inline `style` because no component
carries one.

The failure that argues for the vocabulary already happened once: `/generator` grew a parallel set
(`gen-filters`, `gen-field`, `gen-chip`, `gen-grid`) re-implementing `workspace-sidebar`, `field`,
`filter-chip` and `card-grid`. Adopting a component is a **zero-visual-diff** refactor — it emits the same
classes the markup did — which is what makes it reversible per file and checkable against `/design`.

## The vocabulary

By tier, each grounded in the classes it emits.

**Primitives** — leaf, style-only. `Button` (`action-btn` plus its `--primary`/`--danger`/`--warn`/`--icon`
variants, an optional lucide `Icon` name, and an `Href` that switches it to an `<a>`), `Badge`, `Chip`
(`filter-chip`), `HelpMark` (the hover explainer a `Section` can carry), `Toast`, and `Icon`.

**Forms** — `Field` is the atom the whole system is built from: it owns the label, the required mark, the
error line and the hint slots, and the input itself is `ChildContent`. `NumberField` and `CoordField` are the
two inputs with enough shape of their own to be components; `Select` is the dropdown, taking its rows as
`SelectOption` values — a value, the word it is offered under, the note it carries on hover and the heading it
sits under — so grouping and labelling are decided once rather than at each site that offers a list; and
`AuthorsEditor` is the shared author/contributor block every tool's Identity step uses — each row's mark
is an initial over a hue hashed from the row's own uuid or name, so a page carrying authors fetches nothing
from outside the studio to draw them.

**Data** — `Section` (`panel-section` plus its header, description, help, actions and footer), `SectionHeader`
on its own, `ListRow` (the list row with its swatch, label, tag, go-arrow and a `Trailing` slot for a control
the row carries), and `DetailHeader` (an inspector head: icon, label, trailing badges).

**Layout** — the shells. `StudioShell` is the page skeleton (`editor-page` + topbar + body + optional footer);
`Topbar` carries the home link and a `Crumbs` slot composed from `Crumb`; `NavRail` and `NavButton` are the
left rail; `Workspace`, `Sidebar`, `Inspector` and `ContentColumn` are the four content shells every tool
arranges itself from; `FlowBar` is the phase/step nav shared by the Configure wizard and Edit's stepped
phases; `AppFooter`/`AppFooterLink` and `SideDrawer` finish the set.

**Canvas** — the floating chrome over a `WorldCanvas`: `CanvasReadout`, `CanvasLayerBar` with `LayerChip`,
`CanvasDock` with `DockGroup`, `DockButton`, `DockModeButton`, `DockChoice` (one option of a set the dock
picks between, where `DockModeButton` flips a two-state mode) and `DockFlyoutGroup`, plus `CanvasRoundButton`
and `ViewModeToggle`.

**Terrain** — the material vocabulary shared by the Sketch tool's Dressing phase and the library:
`MaterialEditor`, `BlockPicker`, `StyleSelect` (binding a saved style — the same question, grouped by kind,
with the bound style's own picture beside the control) and `HouseViews`.

**Editor** — feature components that are not vocabulary but are shared by more than one tool: `WorldCanvas`,
`RegionTree`, `SliceView`, `SmartSuggestion`, `BuildHeightSideview`.

## The words a form writes, and where they are declared

A Razor markup lambda cannot contain a string literal, so every wire word an inspector writes has to be a
constant somewhere. Where that somewhere is decides whether the form and the reader can drift apart.

**A kind is declared in `PgmStudio.Vocabulary` and taken from there by everyone who spells it.** `ShapeKinds`
(rectangle, circle, polygon, lasso, polyline), `PropKinds` (stroke, water, flora, tree, boulder, house) and
`MarkKinds` (point, line, area, scarp, rim) are the three the client writes. `Client` reaches `Vocabulary`
through `Contracts`, so the picker takes the same constant the reader does: `PlacedProp`'s
`[JsonDerivedType]` attributes name `PropKinds`, the sketch gate judges against `ShapeKinds.All`, and
`ReliefMarkJson.ToMark` switches on `MarkKinds`. There is one list, not two that agree.

**A field name stays the client's.** `PropFields`, `MarkFields`, `SpecFields`, `PushFields`, `ReliefFields`
and `GrainFields` name the JSON keys a form writes into a document the client holds as a `JsonObject`. They
have one consumer and are that consumer's constants, which is the rule `WordSet` states: a word set with only
one party spelling it is not vocabulary.

**`MarkKinds.Push` is the exception that proves where the line is.** A push is placed and edited through the
relief phase like a mark, so the canvas needs a word for it, but the stored form carries no kind — the array a
push sits in already says what it is. It sits in `MarkKinds` beside its siblings and outside `MarkKinds.All`,
which is the set the reader answers for.

**What a shared constant cannot check is whether a word still has a reader**, and that is what
`KindVocabularyTests` is for: every prop kind must name a derived type and answer the pass's order question,
every shape kind must rasterize to ground, and every mark kind must read as a mark. Adding a word to a set
without teaching a reader fails there rather than in a board that quietly lost a shape.

## The API rules

**Param-first, with a slot escape hatch.** A component takes typed params for the common case and a
`RenderFragment` for the rest. `Section` is the model — `Title`, `Variant`, `Description`, `Required` and the
`Actions` right slot cover almost every use, and a `Header` slot replaces the whole title cluster when a page
needs something bespoke:

```razor
<Section Title="Spawn Points" Description="@desc">
  <Actions><Button Variant="primary" OnClick="Add">Add</Button></Actions>
  <ChildContent>…</ChildContent>
  <Footer>…</Footer>
</Section>
```

`Variant` is the header context — `ruled` for a right-panel inspector section, `list` for a left-panel list
header, `plain` for neither — and both of the first two are canonical rather than one being a special case.

**A control running its own action is disabled and says so, and `Button` owns both halves.** `Busy` is the
one state the button holds rather than reports: it disables the control and swaps `BusyLabel` in for the icon
and the child content, because the verb a button offers and the verb it is performing are different words —
*Save* becomes *Saving…*, *Export* becomes *Exporting…*. Pairing them in the primitive is what stops a call
site doing one without the other, and each half alone is its own fault: undisabled, a second click fires the
action twice; unlabelled, the control sits inert with no sign the first click landed. `FlowBar` forwards the
pair as `NextBusy`/`NextBusyLabel`, since the last step's Next is a verb rather than a move.

The swap is **keyed on `Busy`**, which is what lets a busy button carry an icon at all. It is *An icon cannot
change in place* below, applied to the one control that changes its own content: keyed, the whole button is
replaced rather than walked, so the `<i>` lucide has already detached goes with its parent instead of being
removed on its own.

**A label states what the control can do now, not what its subject is.** Where a button's word is read off one
fact and its enabled-ness off another, the two contradict each other the moment they disagree — a label naming
the map's next build over a control disabled by a compile that was refused. So the label follows whatever
disables it: the plan drawer's footer reads *Compile first* or *Fix 2 blocking problems first*, and the map's
own word only in the state where the button can act.

**A named slot forces the others to be named too.** Blazor stops treating loose markup as `ChildContent` the
moment a component call uses one named `RenderFragment`, so a `Section` that carries `Actions` must wrap its
body in an explicit `<ChildContent>`.

**`Field` owns the label, never the input.** The input is a slot, so a field can hold anything — a raw
`<input class="field-input">`, a `NumberField`, a select, a pair of coordinate cells:

```razor
<Field Label="Map name" Required Error="@nameError" For="map-name">
  <input id="map-name" class="field-input" value="@name" @onchange="OnName" />
</Field>
```

**Modifiers are params, not inline styles.** A width, a `margin-left:auto`, a max-width — each is a modifier
class the component should carry (`Fill`, `Full`, `Class="action-btn--push-end"`), not an inline `style`. 84
inline styles remain across the client, and most of them are a missing param.

**Pass-through is deliberate where it exists.** `Section` captures unmatched values so `style`, `id` and a
`@key` reach the rendered element; `Icon` does the same for a class or a title. `@key` itself is a native
Blazor directive and needs no capture — but a component that does not capture unmatched values will refuse an
`id` outright, which is the usual cause of a mixed-content attribute error: build the value as a single
`@(...)` expression rather than mixing literal text and a Razor expression in one attribute.

**An icon cannot change in place.** `lucide.createIcons` replaces each `<i data-lucide>` with an `<svg>`, so
Blazor patching that node corrupts the reconciler. Any icon-bearing element whose glyph can change must be
`@key`ed by the glyph name, and the key belongs on the **containing element** — the button, the row — not on
the `<i>`, which lucide has already replaced. `Icon` carries that discipline in one place; it is built and
**not yet adopted**, with 156 raw `<i data-lucide>` still standing across the client.

## What stays raw, and why

Adoption is near-total for the atoms — two raw `action-btn`s and two raw `list-row`s remain, each a genuine
exception (a `<label class="action-btn">` wrapping an `InputFile`, and a header-embedded field). Four things
stay raw by decision rather than by backlog:

**The `sidebar-handle` bars**, in 26 files. `panel-resize.js` finds each panel by DOM sibling, so wrapping the
handle would break the resize without breaking the render — the worst kind of regression.

**The `ctrl-row` coordinate triples.** They vary too much to be one component (XYZ, XZ, radius-and-height), so
`CoordField` is the atom and the row stays markup.

**The `ds-*` set** in `design.css` — the `/design` gallery's own frame (nav, headings, example cards). It is
page-only by design; the examples *inside* it render production components.

**The `gen-*` set** in `/generator` is the one piece of real drift left, and it is the largest thing here: the
filter rail, the card grid, the candidate cards and their badges, the tray and the census tables are around
forty classes backed by `generator.css`, re-implementing `workspace-sidebar`, `card-grid`, `badge` and
`filter-chip` under their own names. The atoms inside them have been picked up where they fit; the layout has
not. It is drift rather than a decision, and it is the next thing to fold in.
