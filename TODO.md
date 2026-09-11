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

## Which face a bucket paints, and what a block is chosen against

Four findings the author read off ten painted boards. `BlockLook` states the division the group turns on — the
surface and rim buckets write what a player sees **from above**, the wall and fill buckets what they see **from
the side** — and a material that resolves without asking which of the two it landed in is the cause the first
two shared. That half has shipped (`FEATURES.md`): the gate now asks the wall, `PT4` names a field with no
`rise` on a face, and both log patterns stand a log up where there is no run to lay it along.

The two that remain are each their own: one is about who resolves a block (a stated solid where a theme
resolution belongs), the other about what a block is chosen *against* (the ground it stands on). The second
carries a threshold that is the author's and is not yet stated — it is measured before it is asked.

- [ ] **WE63 — The room plate stamps raw stone under every room.** A shell's `Foundation.Plate` is a stated
  solid — stone brick, sandstone, cobblestone, depending on the preset — so the courses under a room keep that
  block however the ground around them is themed. On a board where the rooms stand on raised pieces the whole
  outside face of each platform reads as a grey core in a coloured map: `specs/probe-plains-1` and
  `specs/probe-plains-2` both left the undersides of their spawn and wool rooms unthemed, and the author
  reported it on both. Two other agents found it and fixed it by hand — one resolved the plate through the
  board's own strata at extent 24, the other dropped the `footing` because a proud ring left the outermost
  column unpainted. That is the right answer and it should be the default: the plate resolves through the
  theme under it unless a style states otherwise.

- [ ] **WE64 — A boulder in the ground's own tone family disappears.** The author's ruling, from three boards:
  a rock must not be built from the family it sits on. Sandstone rocks on sand vanish into the sandstone
  structures behind them (`specs/probe-desert-1`, `specs/probe-desert-2`), and red clay against hardened clay
  on one rock is noise rather than variation (`specs/probe-badlands-2`). The stated fallback is stone, andesite
  and cobblestone, which works against sand, grass, dirt and red sand, or a single clay, since no two clay
  colours are close. Seed that rock as the library's default so a placement naming nothing gets it, and raise a
  finding where a boulder's blocks and the theme under it share a tone family.

## A convention is measured and nothing complains

The group `docs/backlog-strategy.md` names as the next one up. Its measurements are taken and its numbers are
the author's, so each entry is a predicate and a threshold rather than an investigation. Two have landed
(`FEATURES.md`): `WE48`'s brush floor and the first of `WE45`'s three faults.

**The line the group is worked against.** A complaint closes when its domain closes — a house has a fixed
parts list, which is why `HS1`–`HS10` could be finished — and terrain has none, so a catalogue over every pair
of blocks never closes. What is built here is the closable half: a number on a bounded field, a geometric
measurement, a local predicate over one compiled object. `WE46` and `WE41` are the other half and are parked
below on the palette rather than filed as more complaints.

- [ ] **WE47 — A board wears a theme per piece.** A theme is a *place* and a board has two or three; giving
  every piece of the plan its own is the plan leaking into the paint. The half worth building is the **local**
  one: complain where a flight of steps compiled from a plan does not share one theme with itself. The
  registry count is a proxy for "the paint is incoherent" and would fire on 23 of 51 boards, which is a
  symptom rather than a fault — `CLAUDE.md`'s reporting rule prefers a local predicate to a proxy measure.
  `docs/tools/sketch.md`.

  *51 boards carry a registry: 16 hold three, but 11 hold five, 7 hold six, and five hold between sixteen and
  twenty-four — `opus5-interchange` has 24.*
