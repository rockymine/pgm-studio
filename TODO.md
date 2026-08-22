# pgm-studio — TODO (current focus)

The **Now & Next** board — only the *current focus theme* lives here. Everything not in the immediate
slice is in **`BACKLOG.md`** (the long tail); shipped capabilities are in **`FEATURES.md`** (the Done
column). The three move left → right: **`BACKLOG.md` → `TODO.md` → `FEATURES.md`**.

**Holds only open work:** `[ ]` to-do, `[~]` in progress — **never `[x]`.** When a task ships, a commit
lands (its message references the id), the task **leaves this file**, and a line is added to `FEATURES.md`.
Board rules live in `CLAUDE.md` (§ "Status & task board").

## The board is empty

*The boundary: one contract, one use case, one class of fault* has drained. What the front door says about
itself is now checked rather than asserted:

**One shape per concept, on the wire.** A footprint in block coordinates is one `Bounds2dDto`, spelled the
way the contract's own `bounds_2d` is; a region's numbers travel nested under `coords` on a create as well as
a patch; a write that lands and has nothing to hand back answers `{}`, once (`RP34`, `RP38`, `RP33`).

**The document says what a caller can act on.** A field taking one of a closed set of words publishes them —
26 of them, the ten `Vocabulary` sets plus the plan's roles, zone kinds and box kinds — and 189 of the 190
fields a write route reads say what they are rather than only what type they are (`RP37`, `RP36`).

**The catalogue answers only what can be met.** `GET /api/rules` cut from 169 rows to 111: every gate
constant, and the 34 layout rules a plan lint, an evaluator term or a producibility finding can name. The
set is held to `src/` in both directions, which is the check `RP46` was filed for (`RP35`, `RP46`).

Each of the six turned out to be a duplication wearing the shape of a feature, and three of them were a
declared shape disagreeing with what a handler actually sent — a class of fault no test could see until one
was written to read the response rather than the declaration.

## What to pull up next

`BACKLOG.md` holds the long tail, grouped by concept. **Pull up a whole group rather than a task**, per
`CLAUDE.md` § *Status & task board*.

Three findings were filed while this group ran and are waiting there: `RP48` (the **answer** shapes describe
the type and not the fields — 466 of 1,283, the mirror of `RP36`), `C47` (three Edit phases each carrying
their own HTTP half) and `RP47` (the history sweep's grep was one phrasing of several).
