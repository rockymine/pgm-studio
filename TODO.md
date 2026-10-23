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

## The programme: bringing pgmvox into the studio
**The author put this programme first.** `docs/pgmvox-port.md` is the programme: pgmvox's capabilities move
into the studio one phase at a time, each proven against a pgmvox fixture, until a board built on pgmstudio.de
reaches the ceiling a pgmvox board does. Phase 0 has shipped and Phase 1 has closed, its two agent-facing
entries parked, and Phase 2 is open: shapes, modifiers and the ground. Its entries run in the order below, and each leaves the studio usable.

### Phase 2 — shapes, modifiers and the ground

The new ground document replaces the sketch's ground (D2), with no beta beside the live studio (D3). Its model is
settled (D16–D21): shapes on layers with a top and a bottom, then modifiers in a stack, then placements, with the
stored format the sketch's own, changed in three places. What is left on paper is how Terraform looks over it.

- [~] **RP133 — Terraform over the modifier stack, on paper.** The concept page is drawn under D16–D21: a Shapes list
  whose every shape follows, holds or rides, a Modifiers list in run order with masks, leave-outs and what each
  changed, the strip stepping through shapes, modifiers and what stands on them, and the stored format. What
  remains is the author's review of it; the page is linked from `docs/pgmvox-port.md`'s *The shape it lands in*.
