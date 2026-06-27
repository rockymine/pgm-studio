# Sketch Tool — UX/UI Design Critique

> Outside-eye design review of the Sketch tool (`/maps/{slug}/sketch`) after the
> depth pass (per-shape/per-vertex height, 3D isometric preview, stacked layers,
> shape library, body-drag move, snap guides, measure tool). Read-only assessment;
> no code was changed to produce it.

## Executive summary

The Sketch tool is genuinely well-engineered and the recent depth pass (height, iso, layers, library, snap, measure) is impressive in scope. But it has crossed the line where *capability count* outpaced *interaction coherence*: the left sidebar now stacks four unrelated panels into one scroll well, so the island tree — the thing you look at constantly — is buried below Setup + Layers + a 13-tile palette and scrolls off-screen exactly as reported. The mode model is the bigger problem: tool, operation (add/sub), and five visibility toggles are three orthogonal axes rendered as one flat strip of look-alike buttons, and the most consequential state (add vs. subtract) is the easiest to lose track of. Height editing — the headline feature — is almost entirely form-driven and discoverable only by accident. And the "size legibility" goal, while partially met by the dimension readout, is undercut because the marquee measure tool doesn't actually do what it was specced to do (island-gap distance); it measures free cursor drag. The bones are excellent; the work now is editorial, not additive.

---

## Top issues (prioritized)

### P0 — Left sidebar is a single overloaded scroll column; the island tree is unreachable
`SketchEditor.razor:34-78` stacks Setup (1 select + 5 number/select fields), `SketchLayers`, `SketchLibrary` (a 13-item 4-col grid + two help paragraphs), and `SketchPanel` (the island tree) into one `workspace-scroll` at a fixed `--sidebar-width: 280px` (`tokens.css:116`). The island tree — the live structural view you reference on every edit — is *last*, below ~6 form fields, a layer list, and the entire palette. On any normal viewport it's scrolled out of sight.

Why it hurts: the tree is the only place to see/select islands and their shape membership; pushing it below static setup chrome inverts frequency-of-use. Setup is touched once per map; the tree is touched constantly.

Fix: make these **collapsible accordion sections** (Setup and Library default-collapsed once the map has shapes), or move Library to a popover/flyout off the toolbar (it's a "reach for a primitive" action, not persistent state). At minimum, reorder to put the Islands tree first/pinned and let Setup + Library collapse. The fastest win: collapse Setup after first edit and make Library a toggleable drawer — that alone reclaims most of the column.

### P0 — Add/Subtract is the highest-stakes state and the least legible
`op` lives as two more buttons in the same `canvas-toolbar` strip as the seven tools (`SketchEditor.razor:98-101`), styled with `draw-tool-btn--op-add`/`--op-sub` (`editor.css:234-243`) — visually a *peer* of the tool buttons, not an orthogonal mode. Nothing on the canvas, the cursor, or near the drawing tells you the next shape will carve rather than build until you've drawn it. The draw preview does color by op (`sketch-draw-controller.js:156-157`), but only once you're mid-draw.

Why it hurts: drawing a subtract when you meant add (or vice versa) is the single most common destructive mistake in a boolean editor, and here the only persistent signal is one of nine same-shaped buttons being tinted. Add/sub is conceptually a *property of what you're about to draw*, not a sibling tool.

Fix: visually separate the operation toggle from the tool group (a divider plus a distinct control shape — a segmented two-state pill labeled "Build / Carve"), and reflect it in the cursor or a small persistent badge near the cursor coordinate readout while a draw tool is active. The op buttons are also only meaningful when a draw tool is selected — consider disabling/dimming them in move/select/measure.

### P1 — Three orthogonal state axes rendered as one undifferentiated button row
The subbar mixes (a) tool mode (radio: move/select/rect/circle/polygon/lasso/measure), (b) operation (radio: add/sub), and (c) five independent toggles (mirror/shapes/chunks/snap/3D) plus fit + iso-rotate. Tools and op use `draw-tool-btn`; toggles use `filter-chip` — two vocabularies, but the *grouping* doesn't communicate "pick one" vs. "pick one" vs. "flip any." `measure` sits in the radio group but is really a transient mode; `3D` is a chip but is actually a *view-mode radio* against 2D (and it suppresses all tool input — `sketch-canvas.js:193,221` — a far bigger state change than "mirror visible").

Why it hurts: the user can't tell at a glance which controls are mutually exclusive, which are sticky toggles, and which (3D) silently disable everything else. 3D-as-a-chip especially hides that it's a modal view swap.

Fix: three visually distinct clusters with dividers — Tools (radio) · Operation (2-state) · View (2D/3D radio) · Overlays (mirror/shapes/chunks/snap toggles, ideally collapsed under one "Layers/Overlays" popover since four chips is a lot of persistent noise). Promote 3D out of the overlay chips into a 2D/3D segmented control so its modality reads.

### P1 — Per-vertex height is effectively undiscoverable
This is the marquee feature and the path is: select polygon → notice it's a polygon → read the hint "Click a vertex to set its height" (`SketchInspector.razor:54`, only shown when no vertex is selected) → click a vertex on canvas → a numeric field appears in the inspector. The on-canvas affordance is a per-vertex *text label* of the current height (`sketch-edit-controller.js:301-311`) and the same square handle used for dragging — clicking selects for height *only if you don't move* (`:182-184`). There's no visual cue that a vertex handle is also a height target.

Why it hurts: the entire verticality story — the stated core win — is gated behind reading one conditional sentence in a panel and knowing that a click-without-drag on a handle does something different from a drag. New users will never find it.

Fix: make the height labels visibly interactive (e.g. a small pill behind the number, or show "set height" on hover), and/or let the on-canvas height label be clicked/scrolled directly to edit rather than round-tripping to the inspector. Add a one-line affordance on the handle hover ("drag to move · click to set height"). Consider a dedicated "height" sub-mode so the click semantics aren't overloaded onto the move handle.

### P1 — The measure tool doesn't do what it's for (size-judging / void gaps)
The contract (§1c) specs measure as "shortest distance between two island bodies" with a dimension line across the gap. The shipped tool measures raw cursor drag distance: `#updateDim` does `Math.hypot(m.bx-m.ax, m.bz-m.az)` between two free-dragged points (`sketch-canvas.js:432-434`). It's a generic ruler, not a void-gap measurer, and it requires manually aiming both endpoints — precisely the "you draw blind, can't aim for the band that matters" problem it was meant to solve.

Why it hurts: judging a 10–15-block lane or a jump gap is the core stated motivation. A free ruler that you eyeball-snap to two edges is barely better than reading the cursor coordinates twice. There's no snapping of the ruler endpoints to island edges, no auto nearest-gap.

Fix: at minimum snap measure endpoints to nearby island/shape edges (you already have `#snapTargets`/`bestSnap` — reuse them). Better, implement the specced behavior: hover/select two islands → auto-draw the nearest-point gap line + block count. The diagonal distance readout should also decompose into ΔX × ΔZ, which is what actually maps to lane width.

### P1 — Inspector is split-personality and form-heavy; no direct height manipulation
The inspector (`SketchInspector.razor`) shows either a shape *or* an island, never both, and the shape view alone packs: a type/op/override/dim header, two action buttons, a conditional convert button, Height + Floor fields, a conditional vertex-height field, a delete button, and a movement hint. Everything is numeric input + full-width buttons. Height/Floor have `step="1"` number boxes only — no slider, no drag-on-canvas, no scroll-to-adjust. The 3D preview that makes height legible is a *separate modal view* you have to toggle to and which then ignores all input, so you can't see height change as you type.

Why it hurts: editing height is the feature, and it's the most indirect part of the UI — type a number, toggle to 3D, look, toggle back, retype. That's a form, not a manipulation. Direct-manipulation editors let you drag a height handle and watch it.

Fix: add a draggable height affordance (even a simple +/- stepper or scrub on the number, or a side-elevation mini-view in the inspector like the Configure `slice-view` already provides). Longer-term, let the iso preview be a live companion (side-by-side or PiP) rather than a modal swap so feedback is immediate.

### P2 — Convert-to-polygon has three entry points with inconsistent discoverability
Promotion is reachable via the inspector button (`SketchInspector.razor:30-34`), the `P` key (`sketch-canvas.js:359`), and is auto-triggered by edits a rectangle can't represent (per the contract). The `P` shortcut is mentioned only in the button tooltip and a conditional footnote (`SketchInspector.razor:60`). Auto-promotion is invisible — a rectangle silently becomes a polygon with no toast/feedback, so the 8-handle resize disappears and the user may not know why.

Why it hurts: silent type changes that remove a familiar affordance (corner-resize handles) are disorienting. And `P` is undiscoverable.

Fix: on auto-promote, flash a brief "Converted to polygon" affordance so the handle-set change is explained. Surface key shortcuts in one place (see quick wins).

### P2 — No keyboard-shortcut surface, and shortcuts are scattered and partly hidden
Shortcuts exist (Esc cancel, Delete/Backspace remove, `P` promote, arrows nudge, Shift+arrow = 16, Ctrl+drag-handle = Bézier, Alt = bypass snap, double-click closes polygon) but they're spread across `sketch-canvas.js`, `sketch-bridge.js`, and `sketch-edit-controller.js`, surfaced only in a couple of tooltips and inspector footnotes. Ctrl-to-make-Bézier (`sketch-edit-controller.js:325`) and Alt-to-bypass-snap are completely unadvertised in the UI.

Why it hurts: powerful affordances no one can find. Bézier editing in particular is essentially a secret.

Fix: a small "?" / shortcuts popover in the subbar, and tool-contextual hints (when polygon tool active, show "click to add · dbl-click to close" near the cursor; this echoes the tooltips already written at `SketchEditor.razor:92-94`).

### P2 — Layers, height (Floor/Base Y), and symmetry-centre Y are an unexplained coordinate soup
The user now juggles: per-shape `Height` + `Floor` (inspector), per-layer `Base Y` (`SketchLayers.razor:35`), and the iso preview stacks them via `baseY + floor..baseY + top` (`sketch-bridge.js:175`). Nothing in the UI explains how Floor relates to Base Y, or that a new layer auto-offsets +10 (`sketch-bridge.js:240`). The layer list shows "y 10" with no units context and the active-layer ghosting of other layers is silent.

Why it hurts: three Y concepts with overlapping meaning and no model explanation. Authors will conflate shape Floor with layer Base Y.

Fix: label units consistently ("Base Y +10 blocks"), and either explain or visually relate the stack (the iso already does — lean on it). A tiny inline diagram or a "this layer sits 10 blocks above Ground" caption would dissolve most of the confusion.

### P2 — Library palette is dense and its drop interaction is a hidden two-step
13 items in a `repeat(4, 1fr)` grid (`components.css:386`) with 9px labels (`:394`) — thumbnails are 26px tall, labels truncate. The interaction is click-to-arm → click-canvas-to-place (`sketch-bridge.js:80-103`), explained only by a `section-desc` paragraph and the per-item title. The contract (§8d) intended drag-from-thumbnail-onto-canvas; that wasn't built, so the discoverable affordance (drag) is absent and the actual one (click-arm) is text-only.

Why it hurts: tiny targets, truncated names, and a non-obvious arm/place flow for a feature whose whole point is "save me tedious vertex placement." When armed, there's also no persistent indicator *in the toolbar* that you're in place-mode (the tool becomes "place" internally but the toolbar shows no active button — none of the seven tool buttons map to it).

Fix: show the armed state (e.g. highlight the armed library item + a subbar "Placing: Scythe — click to drop, Esc to cancel" hint). Bump thumbnail/label size or switch to a 3-col grid. Consider implementing the specced drag-to-canvas, which is the affordance users expect from a "library."

### P2 — `move` vs `select` distinction is subtle and partially redundant
`move` is pan; `select` is select/edit *and* pans on empty-canvas drag (`canvas-base.js:188` only pans for `move`, but `select` body-drags shapes and middle-mouse pans everywhere). New users won't know that select-mode supports body-drag of a shape (`sketch-canvas.js:236-247`) since it's unadvertised, while the dedicated `move` (hand) tool only pans. Two hand-like concepts, one hidden capability.

Fix: advertise body-drag (cursor change to grab on hover over a selected shape's body already partially happens via `grabbing` on move — extend to a `grab` cursor on hover). Consider whether `move` needs to be a separate tool at all when middle-mouse + space-drag panning is standard.

### P2 — Selecting a shape vs. island is mutually exclusive and silently clears the other
`selectShape` nulls `selectedIslandId` and vice versa (`sketch-bridge.js:125-137`), and arrow-nudge moves whichever is selected. Because the tree and canvas both drive selection, and the inspector flips wholesale between shape and island panels, the user can lose their island selection by clicking a shape on canvas without realizing the nudge target changed. The footnote "Move: arrow keys" appears in both panels but doesn't say *what* moves.

Fix: clarify the nudge target ("Arrows move this island" / "this shape"), and consider keeping the island context visible (breadcrumb in the shape inspector: "Island: North Base › rectangle").

---

## What's working well

- **The dimension readout** (`canvas-dim`, accent-colored, `components.css:363`) showing live `W × D` while drawing and on selection (`sketch-canvas.js:429-443`) is exactly the right, cheap, high-value affordance — it directly addresses the size problem and it's well-placed.
- **Tight preset footprints** (120×80 default, `sketch-bridge.js:16`) with zoom-to-fit genuinely fix the "15-block lane is 3 pixels" problem at the scale level.
- **Operation-colored draw previews + add/sub colored handles** are a thoughtful touch.
- **The auto-switch to select after a draw** (`sketch-bridge.js:62-63`) with the toolbar staying truthful via `OnToolChanged` is the correct, low-surprise behavior.
- **The iso preview as read-only SVG extrusion** is a pragmatic, dependency-free win and reads well (ground plane + opaque occlusion + lit tops).
- **Snap with alignment guides + Alt-bypass** is a real pro-grade feature, and reusing the snap-target machinery is clean.
- **Code architecture** — the controller/canvas/bridge separation and the single-source symmetry leaf are exemplary; none of the UX issues above are caused by tangled code, which means they're cheap to fix.

---

## Quick wins (low effort, high impact)

1. **Collapse Setup and Library by default** (accordion `<details>`), pinning the Islands tree near the top — directly fixes the P0 scroll-off problem with no new components.
2. **Add a divider + relabel the operation toggle** as a 2-state "Build / Carve" pill, separated from the tool group — cheap legibility win for the highest-stakes state.
3. **Show armed/place-mode and measure-mode hints** in the existing `canvas-dim`/subbar area ("Placing: Scythe — click to drop, Esc cancels") — the slot already exists.
4. **Snap measure endpoints to island/shape edges** by reusing `#snapTargets`/`bestSnap`, and split the readout into ΔX × ΔZ — turns a generic ruler into the lane-width tool it's meant to be.
5. **A "?" shortcuts popover** listing the (currently hidden) keys: Esc, Del, P, arrows/Shift-arrows, Ctrl-drag = Bézier, Alt = no-snap, dbl-click = close.
6. **Flash a small "Converted to polygon" confirmation** on auto-promote so the disappearing resize handles are explained.
7. **Promote 3D from a chip to a 2D/3D segmented control** so its modality (suppresses all tool input) reads as a view switch, not a toggle.
8. **Make per-vertex height labels look interactive** (hover state / pill background) so the headline feature is discoverable on the canvas instead of via a conditional inspector sentence.

**Relevant files:** `SketchEditor.razor` (sidebar order + toolbar grouping), `SketchInspector.razor` (height/vertex discoverability), `SketchLibrary.razor` + `components.css:384-395` (palette density), `sketch-canvas.js:412-443` (measure semantics), `sketch-bridge.js:80-112` (place-mode feedback), `editor.css:234-243` (op-button styling), `tokens.css:116-117` (panel widths).
