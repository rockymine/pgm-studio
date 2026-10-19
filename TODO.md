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
entries parked, and Phase 2 is open: the layer document and the ground. Its entries run in the order below, and each leaves the studio usable.

### Phase 2 — the layer document and the ground

The layer document replaces the sketch's ground (D2), with no beta beside the live studio (D3). It is worked out
on paper before it is built: what a layer is, which of today's shapes stay shapes and which become layers, and
what the Terraform phase looks like over them.

- [ ] **RP133 — The layer document, on paper.** A concept and mock-ups for Phase 2's first two steps, the author's
  to review before any code: the document's fields under D6's eight shape rules, which of today's shapes and
  relief statements become which layer kind and which stay shapes, the Layers tab in Terraform (reorder, toggle,
  an area dragged, what a layer changed), and how today's Terraform faults read under it. Lands in
  `docs/pgmvox-port.md`'s Phase 2 and a mock-up the author can click through. **Open, the author's:** what a
  stored sketch's relief carries into layers, since the migration is what the few maps already made keep.
