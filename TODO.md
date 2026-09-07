# pgm-studio — TODO (current focus)

The **Now & Next** board — only the *current focus theme* lives here. Everything not in the immediate
slice is in **`BACKLOG.md`** (the long tail); shipped capabilities are in **`FEATURES.md`** (the Done
column). The three move left → right: **`BACKLOG.md` → `TODO.md` → `FEATURES.md`**.

**Holds only open work:** `[ ]` to-do, `[~]` in progress — **never `[x]`.** When a task ships, a commit
lands (its message references the id), the task **leaves this file**, and a line is added to `FEATURES.md`.
Board rules live in `CLAUDE.md` (§ "Status & task board").

**Three numbers are the author's and are not to be re-derived.** A protection region is at most **20×30**
blocks (`ST10`), a building footprint at most **20×20** (`ST9`), and the smallest room with no building over
it is **4×4** (`WX2`). A dressed prop's 192-cell ceiling (`HP3`) and a room building's 20×20 measure the same
concept since `WE71`, and holding them apart is a deliberate not-yet.

## The dressing key: what a recipe pulled from the library is named by

A map holds a **snapshot** of a library row and never a foreign key, so a library edit can never rebuild a
shipped map, and nothing beside the snapshot names the row it came from — `docs/design-decisions.md` says why
that is the whole model rather than a gap. The room shells now read that way end to end: the select resolves
a snapshot by content, the gates measure from the room that stands, and the wool room is spelled `wool` on
the wire. What is left is the one binding keyed by something the library does not make unique, and it is a
ruling rather than a task. `docs/tools/sketch.md`'s finish model is what the group leaves correct.

- [ ] **TS103 — Pulling a library recipe replaces whatever holds its name.** *Parked on a ruling.*
  `SketchDressingInspector.PickRecipe`/`PickShell` call `pullRecipe(recipe.Name, json)` and
  `dressing-doc.js:pull` writes `#styles[key] = recipe`. Replacing is the point — re-pulling a retuned row
  updates every placement wearing it — but no library table indexes `name` uniquely (`M0011`, `M0012`), so
  two rows sharing a name are the same event as one row retuned, and the second silently becomes the first
  under every placement already put down. Nothing tells them apart without the row id the group refused.
  **Which way:** make a kind's names unique — an index, and a refusal on save — so a name identifies a row
  and the refresh survives; or mint the key from the recipe's own content on a collision, which ends the
  overwrite and the refresh together. `DressingJson.KeyFor` already mints one for a lifted recipe and its keys read
  `oak-12` and `round-4`, so a minted key costs no legibility.
