# pgm-studio — TODO (current focus)

The **Now & Next** board — only the *current focus theme* lives here. Everything not in the immediate
slice is in **`BACKLOG.md`** (the long tail); shipped capabilities are in **`FEATURES.md`** (the Done
column). The three move left → right: **`BACKLOG.md` → `TODO.md` → `FEATURES.md`**.

**Holds only open work:** `[ ]` to-do, `[~]` in progress — **never `[x]`.** When a task ships, a commit
lands (its message references the id), the task **leaves this file**, and a line is added to `FEATURES.md`.
When this board drains, pull the next theme up from `BACKLOG.md`. Board rules live in `CLAUDE.md`
(§ "Status & task board").

Task ids are a section letter + number (`S13`, `B10`, `G15`) — **globally unique and stable** across all
three files. Moving a task between files never changes its id; never renumber or reuse.

## Current focus — Generated XML conventions (B)

Bring the generated `map.xml` in line with the corpus / `docs/template.xml` conventions — team-id naming
and indentation — so exported maps read like real PGM maps. Both are small, well-scoped backend changes
with one dependent consumer to retune. The Sketch-tool depth pass has shipped (`FEATURES.md`); its parked
polish (`S12`, `S16`) and the long backend/authoring/generation tail live in `BACKLOG.md`.

- [ ] **B10 — Generated team ids need the `-team` suffix.** Team ids are emitted **bare** (`red`, `blue`) from
  the colour (`TeamsPhase.razor.cs:101,109` and `SymmetryExpander.cs:67`, both `color.Replace(' ','-')`), but the
  corpus/template convention (`docs/template.xml`) is `red-team` / `blue-team`. The plumbing already supports it —
  `IntentNaming.Slug()` strips `-team`, so the derived ids stay colour-based (`only-red`, `red-spawn-point`,
  `reds-woolrooms`, `…-red-monument`). So just append `-team` at the two derivation sites. Coordinate with `N09`
  (its colour-change re-derivation must produce the suffixed id too) and reuse the same collision guard.
- [ ] **B14 — Spawn protection: apply a protection kit in-spawn + reset it on leave.** The generated spawn
  wiring has the enter block (`enter=only-<enemy>`) + edit protection, but not the kit apply/reset the template
  uses — a resistance/protection kit while in the spawn, and a `reset-resistance-kit` (a `force` kit) applied on
  **leave** via `<apply kit="reset-resistance-kit" region="not-spawns"/>` (`docs/template.xml` L66 + L176). Emit
  the reset kit + the `not-spawns` apply (and the in-spawn protection kit) in the generator (`TeamsGenerator`).
- [ ] **B17 — `wood` + `stained clay` belong in `<itemkeep>`, not `<itemremove>`.** `CtwStandards` puts the
  kit's build blocks in `ItemRemove` (`CtwStandards.cs:77`, `armor + blocks`), but `docs/template.xml` keeps them
  in `<itemkeep>` (L210–211) — otherwise the `<block-drops>` `chance="0"` rule doesn't suppress farming as
  intended. Move the build blocks to `ItemKeep`; keep armor (+ the terrain drops) in `ItemRemove`.
- [ ] **B18 — Fix kit item slot placement.** The generated `spawn-kit` puts blocks / tools / water bucket /
  golden apple in the wrong slots. Match `docs/template.xml` (L41–57): tools in slots 0–3 (sword / bow / pickaxe
  / axe), blocks in 4–6 (wood / stained clay / vine), golden apple slot 8, utility (arrow / shears / spade) in
  28–30. Fix the slot assignment where the spawn kit is assembled (`TeamsGenerator`, `SpawnKitId`).
