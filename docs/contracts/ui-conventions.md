# UI conventions: the studio component system

How the studio's UI is built from a **shared component vocabulary** instead of copy-pasted class
markup. The studio already has a consistent CSS design system (`tokens.css` → `components.css` /
`editor.css`); what it lacks is the Blazor layer that *renders* that vocabulary, so the canonical
skeleton — `panel-section` → `section-header` → `section-title` — is hand-typed in every file. This
doc is the map from the CSS classes to the components that replace them, the API shape those
components take, and the order they get built (task **C12**).

Read alongside:
- `../../src/PgmStudio.Client/wwwroot/css/studio/tokens.css` — the CSS custom properties. Components
  never hardcode a colour/space/radius; they emit classes that resolve to tokens.
- `routing-and-ia.md` — the routes and the information architecture the shells wrap.
- `primitive-styles.md` — the canvas primitive palette (a different, canvas-side visual system).
- The **living style guide** is the `/design` page (`Pages/Design.razor`); this doc is its prose
  contract. Once a component exists, `/design` renders *it* rather than hand-written markup, so the
  showcase can't drift from production.

> **Status:** Phases A–C + D.1 shipped. **A** — the vocabulary atoms + `Section` (`Button`, `Badge`, `Chip`,
> `Field`, `Section`, `SectionHeader`, `ListRow` under `Components/{Primitives,Forms,Data}/`), adopted
> in the `/generator` filter rail (retiring the `gen-*` drift) and the `/maps` list. **B** — the shell
> (`StudioShell`, `Topbar`, `Crumb`, `ActivityRail`, `ActivityButton`, `AppFooter`, `AppFooterLink`
> under `Components/Layout/`), adopted across **all 11 `editor-page` sites** — the copy-pasted topbar /
> rail / footer chrome is gone. **C** — the workspace shells (`Workspace`, `Sidebar`, `Inspector`,
> `ContentColumn` under `Components/Layout/`), adopted across the ~28 activity/phase surfaces (the
> `sidebar-handle` bars stay raw so `panel-resize.js` still finds each panel by DOM sibling — verified
> live). **D.1** — `Section` adopted across every production surface: ~95 hand-typed `panel-section`
> skeletons across 31 Configure / EditorActivity / Sketch / Plan files became `<Section>` (0 raw
> `panel-section` outside the `/concepts` + `/design` leave-raw zone of §5). `Section`/`SectionHeader`
> gained a `Required` asterisk param, and `Section` a `CaptureUnmatchedValues` `style`/`id`/`@key`
> pass-through. **D.2** — the atomic vocabulary adopted across every production surface: `field` →
> `<Field>` (~102), `action-btn` → `<Button>` (~66), `badge` → `<Badge>` (~67), `list-row` →
> `<ListRow>` (~50), `filter-chip` → `<Chip>` (~23). `Field` gained a `LabelHint` slot; dynamic badge
> variants pass the verbatim ternary via `Class`. Two legitimate raw holdouts remain (a
> `<label class="action-btn">` wrapping `InputFile`; `SliceView`'s header-embedded field). All verified
> against `/design`. **D.3** — `CoordField` (coord cell; `ChildContent` slot for the `NumberField`
> variant) and `DetailHeader` (icon + label + trailing badges) built and adopted (~35 + 28 uses); the
> `/design` gallery **regenerated** to render the real components (its `ds-*` frame stays, the examples
> inside are now `<Section>`/`<Field>`/`<Button>`/`<Badge>`/`<ListRow>`/`<CoordField>`/`<DetailHeader>`, so
> the showcase can't drift). `Icon` (`<i data-lucide="@Name" @key="@Name">`) is **built but unadopted** —
> ready for incremental pickup. `FlowBar`/`Console`/`Card`/`CoordRow` were **not** built: each is single-use
> or too varied to fit a component (`ctrl-row` triples vary XYZ/XZ/R·H), so those stay raw. Still open (C12
> backlog): the `Icon` roll-out, folding the 1 `section-heading`, and dropping inline `style=` now
> expressible as params. The other `.razor` components stay at their existing altitude (page fragments like
> `EditorCanvas`; one-off widgets like `NumberField`, `SideDrawer`, `SmartSuggestion`, `Toast`).

---

## 1. Why — the missing tier

The studio styles by **global class names**, not scoped `.razor.css`. That is a deliberate, load-bearing
choice: JavaScript reaches elements by class, the `/design` gallery documents classes, and the theme
system swaps tokens under those classes. It also means the markup that *uses* a class is pure structure
with no logic — and so it gets copy-pasted. Two thirds of the client (44 of 64 `.razor` files) retype
the same section skeleton; the app shell is duplicated across 11 files; and 185 inline `style=`
attributes leak because there is no component to carry a modifier.

The failure this invites already happened: `/generator` grew its **own** parallel vocabulary
(`gen-filters`, `gen-field`, `gen-chip`, `gen-grid`) that re-implements `workspace-sidebar`, `field`,
`filter-chip`, and `card-grid` — the exact drift a shared component prevents.

The fix is **not** new CSS. A component emits the *same classes* the markup does today, so adopting one
is a zero-visual-diff refactor verifiable against `/design` and reversible per file. C12 is markup dedup
plus a typed API — nothing more, nothing riskier.

The frequency that makes it worth doing:

| Class | Occurrences | Becomes |
|---|---|---|
| `list-row` | 373 | `<ListRow>` |
| `action-btn` (+ variants) | 246 | `<Button>` |
| `section-header` (`--ruled` ×112) | 224 | `<SectionHeader>` (inside `<Section>`) |
| `.field` label+input block / `field-label` | 172 / 183 | `<Field>` (the label+input atom) |
| `badge` | 165 | `<Badge>` |
| `panel-section` | 156 | `<Section>` |
| `section-desc` / `section-title` | 118 / 112 | `<Section>` params |
| `editor-page` shell (topbar ×12, rail ×6, footer ×2) | 11 files | `<StudioShell>` |

---

## 2. The atomic ladder

Components are organized by **atomic-design tier**, each grounded in the classes it already emits so
nothing new is invented. `✅` = already a component; everything else is C12 scope.

**Atoms** — leaf, style-only, no layout opinions of their own:

- `Button` ← `action-btn` + `--primary`/`--danger`/`--warn`/`--icon`/`--fill`/`--full`
- `Field` ← `field` + `field-label` + `field-required` + `field-error` wrapping an input slot — **the
  "atom with a label and an input" the whole system is built from**
- `NumberField` ✅ · `CoordField` ← `coord-field`/`coord-prefix`/`coord-input`
- `Badge` ← `badge` + variants · `Chip` ← `filter-chip` · `Swatch` ← `list-swatch`/`team-dot`
- `Meter` ← `meter`/`meter-fill` · `CheckRow` ← `check-row`
- `Icon` ← the `<i data-lucide="…">` glyph. Centralizes the lucide interop footgun (see §4) that is
  hand-managed and re-commented in a dozen files.

**Molecules** — a small fixed arrangement of atoms:

- `SectionHeader` ← `section-header(--ruled|--list)` + `section-title` + optional right-slot action
- `ListRow` ← the 373-use row: `list-row(--selected|--compact)` + swatch + `list-label` + `list-tag` +
  `list-go` arrow · `PanelList` ← the `panel-list` container
- `CoordRow` ← `ctrl-row` of 2–3 `CoordField` (X/Y/Z) · `FieldRow` ← `field-row`
- `Callout` ← `callout` · `PanelWarning` ← `panel-warning` · `EmptyMessage` ←
  `panel-empty-msg`/`list-empty` · `StepRow` ← `step-row`/`step-dot`

**Sections / organisms** — a self-contained content block:

- **`Section`** ← `panel-section` + `SectionHeader` + body + optional `section-footer`. The headline
  target (156×); §4 gives its API.
- `Card` + `CardGrid` ← `card`/`card-grid`/`card-head`/`card-icon`/`card-title`/`card-desc`/`card-foot`
- `DetailHeader` ← `detail-header` (inspector head: icon + label + badges)
- `Console` ← `console-*` · `FlowBar` ← `flow-bar`/`flow-steps`/`flow-step` (wizard sub-steps)
- `SmartSuggestion` ✅ · `RegionTree` ✅ · `Toast` ✅ · `SideDrawer` ✅

**Templates / shells** — page skeleton, content injected by slot:

- `Sidebar` ← `workspace-sidebar` + `workspace-scroll` (+ optional `sidebar-handle` collapse) ·
  `Inspector` ← `workspace-inspector` · `Workspace` ← the three-slot `workspace` row
- `ContentColumn` ← the centered `workspace-scroll` max-width column — **the recurring "vertical
  content page"** (`/`, `/maps?stage=…`, list views all use it)
- `Topbar` ← `topbar` + breadcrumb + right slot · `ActivityRail` + `ActivityButton` ←
  `activity-rail`/`activity-btn` + status dots (the un-componentized rail) · `AppFooter` ← `app-footer`
- **`StudioShell`** ← `editor-page` + `Topbar` + `app-body`(rail + viewport) + optional footer — wraps
  the 11-file chrome copy-paste

Every surface maps cleanly: sidebars → `Sidebar`/`Section`; workspace canvas-or-content →
`Workspace`/`ContentColumn`; the homeless start-page cards → `CardGrid`; the `?stage=` lists →
`ListRow` + `ContentColumn`; the activity rail → `ActivityRail`; toolbars → `CanvasSubbar` +
`DrawToolButton`; inspectors → `Inspector` + `DetailHeader`; the canvas → `EditorCanvas` (already one).

---

## 3. Placement

Vocabulary components live under `Client/Components/`, split by tier (the existing flat `NumberField` /
`SideDrawer` move into it):

```
Components/
  Primitives/   Button, Icon, Badge, Swatch, Meter, Chip, CheckRow
  Forms/        Field, NumberField, CoordField, CoordRow, FieldRow
  Data/         Section, SectionHeader, ListRow, PanelList, Card, CardGrid,
                DetailHeader, StepRow, Console, FlowBar
  Layout/       StudioShell, Topbar, ActivityRail, AppFooter,
                Sidebar, Inspector, Workspace, ContentColumn
  Feedback/     Toast, SmartSuggestion, Callout, PanelWarning, EmptyMessage, SideDrawer
```

Page-fragment components (`EditorCanvas`, `RegionInspector`, `RegionTree`, `SliceView`, the `Sketch*`
panels) stay next to their pages — they are features, not vocabulary. CSS stays global and unscoped;
components do **not** introduce `.razor.css`.

---

## 4. Component API conventions

**Param-first, with a slot escape hatch.** A component takes typed params for the common 80% and a
`RenderFragment` override for the complex 20%. `Section` is the model:

```razor
@* 80% — params carry title, variant, description, and the right-slot action *@
<Section Title="Spawn Points" Variant="Ruled" Description="@desc">
  <Actions><Button Variant="Primary" OnClick="Add">+ Add</Button></Actions>
  @* body is ChildContent *@
  <PanelList>…</PanelList>
  <Footer>…</Footer>          @* optional section-footer *@
</Section>

@* 20% — a Header slot overrides the whole title cluster when a page needs a bespoke header *@
<Section Variant="Ruled">
  <Header>…custom header markup…</Header>
  …
</Section>
```

`Variant` is an enum (`Ruled` for right-panel inspector sections · `List` for left-panel list headers ·
`Plain`) — it maps to the `--ruled`/`--list` modifier and **must not** be hardcoded (both are canonical;
the CSS comment documents the split).

**`Field` is the label+input atom.** It owns the label, the required mark, and the error line; the
input is a slot (or a `TextField`/`NumberField`/`CoordField` variant supplies it):

```razor
<Field Label="Map Name" Required Error="@nameError">
  <input class="field-input" value="@name" @onchange="OnName" />
</Field>
```

**`Icon` centralizes the lucide gotcha.** `lucide.createIcons` replaces each `<i data-lucide>` with an
`<svg>`, so Blazor patching a mutated node corrupts the reconciler. Every icon-bearing region that can
re-render must be `@key`ed by its content to force recreation. `<Icon Name="…" />` carries the `@key`
discipline in one place instead of the current per-site hand-rolling.

**Modifiers are params, not inline styles.** The 185 inline `style=` occurrences are mostly missing
modifiers (a width, a `margin-left:auto`, a max-width). Give the component the param
(`Align="End"`, `MaxWidth`, `Fill`) that maps to the existing `--push-end`/`--fill`/etc. modifier class
rather than an inline style.

---

## 5. Reconciliation (the near-duplicates)

The audit surfaced apparent duplicates. None require deleting CSS — they are canonicalized *through the
API*:

- **`section-heading` (23) vs `section-header` (224)** — not duplicates: `section-header` is the
  title-plus-action row, `section-heading` a title+icon cluster. Fold `section-heading` uses into
  `SectionHeader`'s title slot.
- **`section-body` vs `panel-section`** — different levels: `panel-section` is the outer flex column,
  `section-body` an inner content group. `Section` renders the former; its body slot is the latter.
- **The CSS split (shell in `editor.css`, vocab in `components.css`)** — keep it. Components emit the
  same classes; a header comment records which component owns which classes.
- **The `gen-*` set** — pure drift; retire it entirely by adopting the canon in `/generator` (§6, the
  proof-of-concept migration).
- **The `ds-*` set (`design.css`)** — the `/design` gallery *frame* (nav, section headings, example
  cards) stays design-page-only. The *examples inside* switch to rendering the real components once they
  exist, per `/design`'s own rule #5 ("add a production example with a new component; don't build a
  separate mock").
- **The `/concepts` mockups (`Authoring/*`)** — 14 throwaway `ds-*` demos of the new-map flow, slated to
  be replaced by real Configure phases. Lowest payoff; leave them raw, do not spend migration budget
  there.

---

## 6. Migration path

Incremental, no big-bang. Each phase ships and reverts on its own; because CSS never changes, `/design`
is the visual-regression oracle throughout.

- **A — atoms + `Section`.** Build `Section`, `SectionHeader`, `Field`, `Button`, `Badge`, `ListRow`.
  Adopt them in `GeneratorBrowse` first, retiring `gen-*` — this proves the vocabulary against the real
  drift case. ~80% of the payoff.
- **B — the shell.** `StudioShell` + `Topbar` + `ActivityRail`/`ActivityButton` + `AppFooter`. Adopt
  across the 11 `editor-page` files.
- **C — workspace shells.** `Sidebar`, `Inspector`, `Workspace`, `ContentColumn`. Adopt in
  Configure/Sketch/Editor activities.
- **D — the long tail + cleanup.** `Card`/`CardGrid`, `CoordField`/`CoordRow`, `DetailHeader`,
  `FlowBar`, `Console`, `Chip`, `Icon`. Reconcile the near-duplicates (§5), regenerate `/design` from
  the real components, and delete the inline `style=` attributes replaced by params.
