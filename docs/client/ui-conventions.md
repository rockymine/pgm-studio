# The component vocabulary

The studio's Blazor UI is built from a shared set of components rather than copy-pasted class markup. This is
the reference for which component to reach for, what each one takes, and the handful of rules that keep Blazor
and the CSS from fighting each other. Where a component lives is `CLAUDE.md`'s Client folder rule
(`Pages/`, `Features/<Tool>/`, `Components/`); this is what is *in* `Components/`.

How the copy on a panel is worded is `writing-for-the-ui.md`.

Read alongside:
- `../../src/PgmStudio.Client/wwwroot/css/studio/tokens.css` — the custom properties. A component never
  hardcodes a colour, a space or a radius; it emits classes that resolve to tokens.
- `../../src/PgmStudio.Client/wwwroot/css/studio/components/` — one stylesheet per shared component
  (`buttons.css`, `form-fields.css`, `canvas-dock.css`, …). `components.css` beside it is only the list of
  imports, in cascade order: `icons.css` first, because a context rule such as `.thing svg` has the
  specificity of `svg.lucide` and only source order decides between them. A new component gets its own file
  and a line in that list; restyling a component means opening its file.
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
variants, an optional lucide `Icon` name, an `Href` that switches it to an `<a>`, and `Writes`/`Deletes`/`Builds`, which
close it where the caller may not, below), `WriteGate` (the same answer for a control that is not a
`Button`), `Badge`, `Chip`
(`filter-chip`), `HelpMark` (the hover explainer a `Section` can carry), `Toast`, `Icon`, and `PlayerHead` —
a person's initial on a neutral tile, with the front of an account's head over it as the studio serves it at
`/api/minecraft/player/{uuid}/head` — eight pixels drawn large, the face with the hat over it the way the game
draws it — so a browser asks no third party and a head the studio cannot get leaves the initial showing.

**Forms** — `Field` is the atom the whole system is built from: it owns the label, the required mark, the
error line and the hint slots, and the input itself is `ChildContent`. Three controls carry every number and
every pick from a list. `NumberField` is the number box: it clamps what is typed to its `Min`/`Max` and snaps
the box back to the clamped value, a null `Value` is a blank box, and `OnCleared` answers a box emptied where
blank means "none". `RangeField` is the slider: `ValueChanged` follows the thumb and `OnCommit` fires once on
release, so a live preview binds the first, a save or a reload the second, and a readout that moves while the
work waits binds both — the readout itself is the `Field`'s `LabelEnd`. `Select` is the dropdown, taking its
rows as `SelectOption` values — a value, the word it is offered under, the note it carries on hover and the
heading it sits under — so grouping and labelling are decided once rather than at each site that offers a
list; `Slim` is the narrower panel-row box and `Canvas` the floating control of the canvas chrome. `CoordField`
is the labelled coordinate cell; `SwatchRow` is the control a **colour** is chosen with, generic in what a swatch stands for — a block's data
value in the library, a colour word in Configure — because a set of sixteen colours is picked by clicking the
colour and a dropdown of their names makes the author read what they can already see; and
`AuthorsEditor` is the shared author/contributor block every tool's Identity step uses — each row's mark
is an initial over a hue hashed from the row's own uuid or name, so a page carrying authors fetches nothing
from outside the studio to draw them.

**No raw `<input type="number">`, `<input type="range">` or `<select>` stands outside `Components/`.** A
number, a slider and a dropdown go through `NumberField`, `RangeField` and `Select`, so restyling every one of
them is an edit to those components and their stylesheets (`form-fields.css`, `range-field.css`, and
`canvas-chrome.css` for the canvas dropdown) rather than a hunt through the tools. A site that needs something
a component does not offer — an event, an id, a class — gets a parameter on the component, not raw markup
beside it.

**Data** — `Section` (`panel-section` plus its header, description, help, actions and footer), `SectionHeader`
on its own, `BoardKey` (the role and zone colours a page of server-drawn boards is read by, drawn once beside
them), `ListRow` (the list row with its swatch, label, tag, go-arrow and a `Trailing` slot for a control
the row carries), and `DetailHeader` (an inspector head: icon, label, trailing badges).

**Layout** — the shells. `StudioShell` is the page skeleton (`editor-page` + the two bars + body + footer);
`AppNav` is the studio's own bar on every page — home, a link per tool, `TextSizeMenu`, the theme and
`AccountMenu`, who is signed in; `Topbar` is the tool's bar under it — the home link, a `Crumbs` slot composed from `Crumb`, a *View
only* tag on a read-only page, and the tool's actions; `AppFooter` closes every page with the keyboard
shortcuts, the design reference and the repository; `NavRail` and `NavButton` are the
left rail; `Workspace`, `Sidebar`, `Inspector` and `ContentColumn` are the four content shells every tool
arranges itself from; `FlowBar` is the phase/step nav the stepped tools share; `AppFooterLink` and `SideDrawer`
finish the set.

**Canvas** — the floating chrome over a `WorldCanvas`: `CanvasReadout`, `CanvasLayerBar` with `LayerChip`,
`CanvasDock` with `DockGroup`, `DockButton`, `DockModeButton`, `DockChoice` (one option of a set the dock
picks between, where `DockModeButton` flips a two-state mode) and `DockFlyoutGroup`, plus `CanvasRoundButton`
and `ViewModeToggle`.

**Terrain** — the material vocabulary shared by the Sketch tool's Dressing phase and the library:
`MaterialEditor`, `BlockPicker`, `SlotSelect` (filling a slot with one block or a saved pattern — one list
offering nothing, *A single block* or the patterns grouped by kind, the block picker under it once a block is
chosen, and the bound pattern's own picture beside the control) and `HouseViews`.

**Editor** — feature components that are not vocabulary but have more than one consumer: `SmartSuggestion`,
which the Configure tool and the design showcase both mount. The world canvas and the bodies drawn beside it —
`WorldCanvas`, `RegionTree`, `SliceView`, `BuildHeightSideview` — are the Configure tool's alone and live in
`Features/Configure/`.

## A page that may not write

**Whether a page may write is the shell's to decide, and the panels' to show.** `StudioShell` asks
`StudioAccess.ReadOnlyReasonAsync` about the address it is on — a page under `/maps/{slug}/` edits that map and
asks `GET /api/map/{slug}/access`; the map list, the catalogue, the design page and the whitelist page write
nothing; every other page needs someone on the whitelist — and where the answer is no it hands the reason to
the tool's bar as `StudioReadOnlyReason`, which shows *View only* with the reason on hover, and cascades
`StudioReadOnly` to everything beneath it. No tool states its own read-only-ness, so a new tool is read-only for a visitor the
moment it sits in the shell.

Three shells answer the cascade. `Inspector` and `ContentColumn` put their content in a disabled `<fieldset>`,
which is the browser's own way of greying every input, select and button inside it at once; the library
editor does the same over its name, fields and save. `DockGroup` is authoring unless it is marked `Viewing`,
and an authoring group is not drawn on a read-only page — so select and pan stay, and every tool that draws
goes.

**An action that writes is marked as one, and closes where the caller may not write.** A `Button` marked
`Writes` (a save, a *New*, an import, a build, a remove) reads the shell's `StudioWriteReason` cascade: where it
is set the button is disabled and the reason is its tooltip, and a `Button` with an `Href` is drawn without
its `href` and with `aria-disabled`, so a *New* that opens an editor goes nowhere. The reason is
`StudioAccess.WriteReasonAsync`: on a page that writes it is the page's own read-only reason, and on a page
that writes nothing, such as the map list, it is whether the caller may write at all, since an action there
starts something on a page that does. Until the shell has asked, every such action is closed.

**Deleting a shared row is an admin's, and `Deletes` says so.** A library entry or a pinned layout is shared
by everyone, so a `DELETE` of one is refused to anyone but an admin; a `Button` marked `Deletes` reads
`StudioDeleteReason` and closes the same way. A control that is not a `Button` (the generator's pin, the
layer strip's add and remove) reads the same two answers through `WriteGate`, and a `FlowBar` whose Next
writes takes `NextWrites`. Reading is never marked, so it stays open to a visitor.

**A sidebar is greyed a control at a time, because it holds what is picked as well as what is changed.** A
`Section` marked `Writes` (a sketch layer's name and base, the plan's reference trace, Configure's build height,
team size and symmetry centre) puts its content in a disabled fieldset with the reason on hover. A `SwatchRow`
marked `Writes` (adding a team or a wool) disables its swatches, and a `ListRow` marked `Writes` (a suggested
core to confirm, the map's symmetry to choose) goes inert. The rows that only pick stay live.

**A canvas is told whether it may change anything, and refuses at the source.** A component inside the shell
reads `StudioWriteReason` and hands it to its canvas's `setReadOnly` (`WorldCanvas`, `SliceView`, the build
height side view); a tool that renders the shell reads it through a `WriteGate`'s `OnChanged`. Read-only, a
canvas pans, zooms, selects and measures, and begins no draw, drag, handle, placement, paint or chord that
would change the document; the sketch marks nothing dirty, so nothing is saved or discarded
(`canvas-interaction.md` §3).

**Downloading a map builds it, so it is closed to a visitor too.** A map's export is made on request — the
world written, the ZIP packed — and the server gives it the `member` policy (`docs/access.md`). A `Button`
marked `Builds` (the sketch's *Download map*, the plan's world ZIP, Configure's export, where a `FlowBar`
takes `NextBuilds`) reads `StudioBuildReason`, which is `StudioAccess.BuildReasonAsync`: open to anyone on the
whitelist whichever map the page is on, and closed with the reason to anyone else.

The server refuses every write the caller may not make whatever the page shows (`docs/access.md`); this is
what lets the page say so before an edit rather than after it.

## A filter looks like a filter

**A set the reader can tick several of leads each chip with a checkbox.** `filter-group-options--multi` on the
chip row draws the box, ticked when the chip is on, and moves the chip's count to its right edge; a row where
exactly one option holds (a symmetry, a panel switch) carries no box. The generator and the shape catalog
title their rails for what the rail does — *Layout settings*, *Filter by shape*, *Filter shapes* — rather
than *Filters*, and every change in them applies at once: there is no Apply button to forget.

## Text size

**Every size in the client is one design size times `--ui-scale`.** Type, icons, the control height, the
spacing steps and the two panel widths in `tokens.css` are each `calc(<px> * var(--ui-scale))`, and the
reader picks the scale from `TextSizeMenu` in the studio bar: *Small* 0.9, *Default* 1, *Large* 1.15 and
*Larger* 1.3, kept in `localStorage` as `pgm-text-size` and applied by the inline script in `index.html`
before any stylesheet loads. A rule that writes a raw pixel size escapes the setting, so a new size is a
token or a `calc` over the scale. Text drawn on a canvas is outside every stylesheet and takes its size from
`labelPx` in `js/studio/shared/ui-scale.js`, which reads the same scale.

The default type scale is 15 / 14 / 13 / 12 / 11px, and a button is at least 28px tall at the default size,
above the 24 × 24px minimum target size of WCAG 2.2 (2.5.8).

## The words a form writes, and where they are declared

A Razor markup lambda cannot contain a string literal, so every wire word an inspector writes has to be a
constant somewhere. Where that somewhere is decides whether the form and the reader can drift apart.

**A kind is declared in `PgmStudio.Vocabulary` and taken from there by everyone who spells it.** `ShapeKinds`
(rectangle, circle, polygon, lasso, polyline), `PropKinds` (stroke, fluid, flora, tree, boulder, house, chest) and
`MarkKinds` (point, line, area, scarp, rim) are the three the client writes. `Client` reaches `Vocabulary`
through `Contracts`, so the picker takes the same constant the reader does: `PlacedProp`'s
`[JsonDerivedType]` attributes name `PropKinds`, the sketch gate judges against `ShapeKinds.All`, and
`ReliefMarkJson.ToMark` switches on `MarkKinds`. There is one list, not two that agree.
The plan editor's words are the same: a piece's roles (`PlanRoles`) and a box's kinds (`PlanBoxKinds`) are
declared there, and `PlanPalette` adds only the label and the canvas token each is drawn in.

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

## What a panel says, and where each kind of saying goes

An inspector has four places to put a sentence and they are not interchangeable. Picking the wrong one is how
a column that edits eleven numbers came to carry four hundred words about what those numbers mean.

**An option is a name, and its sentence is its note.** A `<select>` row has to be read at the width of the
control and scanned against its siblings, so what belongs there is the word — `Ground`, `Level`, `Raise`,
`Sink` — and what a row *is* rides as its `Note`, which `Select` renders as the option's title. An option
reading *"a monolith — this far above the ground"* makes the list a paragraph with a dropdown around it: it
cannot be scanned, it cannot be compared, and the same sentence is usually repeated under the control anyway.

**The word an option writes is the word the document uses.** `Inherit`, `Hold` and `Exclude` are what
`Participation`, `relief_scope` and `docs/tools/sketch.md` all call them, so what an author picks can be
grepped for in `docs/` and cited in a finding. A label invented for the picker — *"yes — its ground is the
group's ground"* — is a third vocabulary for a thing that already had one, and it is the reason a reader
cannot look up what they just chose.

**Under a control goes what the numbers work out to, not what the control is for.** `Top at 8`; `Drops 12 over
a 2-block face: 6 a block, only ever descended`; `The edge slopes over 5 blocks. 6 of its 16 blocks stay flat`.
A readout is short because a fact is short, it is always true because it is computed, and it changes when the
author changes something, which is the only way a panel teaches a knob. A paragraph explaining what a bevel is
does none of the three, and the argument for why the knob is shaped that way is a document's job — `docs/` is
named for subjects so that there is somewhere to put it.

**A control that is doing nothing says nothing.** A readout returns the empty string and renders no line: a
block step of one, a group with no rim, a push whose corners agree. Two selects at their defaults with an
explanatory paragraph each is thirty words to say that nothing is happening, and it is the first thing a
reader learns to skip — which is what then hides the line that matters.

**Undo restores the document; every phase reading a part of it has to be told.** A history step is the whole
`getState()` value and a restore is `load()`, so the document comes back correct whichever phase is up — and
only the Draw phase *shows* it, because `load` puts the shapes back on the canvas and the restore re-announces
the shape selection. The marks, the props and the theme registry are rebuilt by the same call and then never
announced, so their panels go on listing what the step undid and the server-drawn overlays go on showing the
surface it was solved for. A restore therefore fires what each phase's own edits fire — `OnThemes`,
`OnDressing`, `OnRelief`, the relief and paint refreshes — and drops the meshed board, which is a picture of a
document no longer open. Rebuilding a document also clears its selection, so the mark and the prop are taken
back where the step left them standing, the way the shape already was.

**A control looks like a control, and a two-state one names both states.** A checkbox hidden behind a line of
text styled like a readout is a control nobody presses: it sits in a column of readouts, in the readouts' own
class, saying only the state it is already in. Two `Chip`s say it instead — the current state marked, the other
one visibly available — which is pressable, keyboard-reachable, and shows what the alternative is before it is
chosen. The pattern it replaces (`control-input--hidden` under a `plan-readout` span) was in three panels and
is in none.

**What survives as prose is a keymap, an empty state, or a fact with nowhere else to live.** *Move: arrow keys ·
Shift+arrow = 16 blocks* is a keymap. *Nothing is terraformed yet* is an empty state. *Turns with the building at
every mirror image* is behaviour a reader cannot see and no number states. Everything else is a note, a
`LabelHint`, or a readout.

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

**`Busy` is held by the component that runs the action.** A flag set by a parent reaches the button only when
the parent renders, and a parent whose action was handed down as a plain delegate does not render until the
action returns — so the button stays live and unlabelled for exactly the time it should be neither. The notes
column's Send is the case: the column owns its sending state, and the phase it calls only answers whether the
note landed.

**A disabled button looks off, not lighter.** `:disabled` drops the variant's colour along with its strength and
takes no hover, since a faded primary is still a tinted fill with an accent edge and reads as a lighter button
that can be pressed. A busy button is disabled too but working, so `action-btn--busy` keeps its colour.

The swap is **keyed on `Busy`**, which is what lets a busy button carry an icon at all. It is *An icon cannot
change in place* below, applied to the one control that changes its own content: keyed, the whole button is
replaced rather than walked, so the `<i>` lucide has already detached goes with its parent instead of being
removed on its own.

**A label states what the control can do now, not what its subject is.** Where a button's word is read off one
fact and its enabled-ness off another, the two contradict each other the moment they disagree — a label naming
the map's next build over a control disabled by a compile that was refused. So the label follows whatever
disables it: the plan drawer's footer reads *Compile first* or *Fix 2 problems first*, and the map's
own word only in the state where the button can act.

**A named slot forces the others to be named too.** Blazor stops treating loose markup as `ChildContent` the
moment a component call uses one named `RenderFragment`, so a `Section` that carries `Actions` must wrap its
body in an explicit `<ChildContent>`.

**`Field` owns the label, never the input.** The input is a slot, so a field can hold anything — a raw
text `<input class="field-input">`, a `NumberField`, a `RangeField`, a `Select`, a pair of coordinate cells:

```razor
<Field Label="Map name" Required Error="@nameError" For="map-name">
  <input id="map-name" class="field-input" value="@name" @onchange="OnName" />
</Field>
```

**Modifiers are params, not inline styles.** A width, a `margin-left:auto`, a max-width — each is a modifier
class the component should carry (`Fill`, `Full`, `Class="action-btn--push-end"`), not an inline `style`, so a
redesign restyles the client from its stylesheets and tokens alone. A spacing modifier is named for what it
does to the block it sits on: `--separated` sets it apart from the block above (`panel-list--separated`,
`section-desc--separated`, `ctrl-row--separated`).

**The one inline style is a runtime value, passed as a custom property a class reads.** A team's colour, a
confidence, a pin's position or a column's width is data rather than design, so the markup hands it over as
`style="--swatch: @hex"` and the class decides what to paint with it: `--swatch` fills a colour swatch
(`list-swatch`, `block-swatch`, `biome-swatch`, `canvas-dock-swatch`, `badge--team`, …), `--icon-tint` colours a `geo-type-icon`,
`--meter-level` sizes a `meter-fill`. A component that takes such a value takes it as a param (`DockButton`'s
`Swatch`, `DetailHeader`'s `IconTint`, `ContentColumn`'s `MaxWidth`) and sets the property itself. A `url()` in
a custom property resolves against the stylesheet that reads it, so one carries an absolute address.

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
