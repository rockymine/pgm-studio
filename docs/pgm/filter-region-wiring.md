# Filter ↔ Region Wiring

How behavior attaches to regions, and the v1 suggestion templates the editor offers. This doc owns
the **wiring relationship** and the **template catalog**. It does not restate:
- the **Filter / ApplyRule shapes** → the types themselves, `Domain/Filter.cs` and `Domain/MapModel.cs`;
- the filter **vocabulary** + the *event × filter-type* and *event × region-geometry* matrices →
  `filter-patterns.md` (Appendix A.2–A.5) + the corpus recipes (Clusters 1–6);
- how wiring **surfaces per region** as `roles` → `region-categorization.md` §3.


## The relationship

A region is **inert geometry**. Behavior comes from an **apply-rule**: `region × event → filter`
(plus optional actions `kit`/`lend_kit`/`velocity`/`message`). Events: `enter`, `leave`, `use`,
`block`, `block_place`, `block_break`, `block_physics`, `block_place_against`, and the kit/velocity
condition `filter`. One rule may carry several `event→filter` keys at once (canonical, not a
normalization target); filters compose (`all`/`any`/`not`/`deny`) and reference children by id.

**This introduces no new persisted type.** Wiring is `apply_rules` + `filters` (`Domain/MapModel.cs`, `Domain/Filter.cs`)
referencing regions by id; in the region view it appears as the `roles` `<event>=<filter_id>` entries
(`region-categorization.md`).

## What attaches where

The sensible defaults (a **soft-warning** source for the UI, never a hard validator — `filter-patterns.md` A.4):

- `enter` / `use` → **team**(-based) filters, on **area/compound** regions only (never a single
  `block`/`point` — you can't "enter" a 1-block region).
- `block` / `block_place` / `block_break` → `never` / `material` / `all`/`any`/`deny`/`not` over those;
  on areas, void wrappers, occasionally a single block.
- `block_physics` → `deny`. The kit/velocity `filter` condition → team / `all`.

Full matrices: `filter-patterns.md` A.2 (event × filter-type) and A.5 (event × region-geometry).

## v1 templates (suggest + confirm)

Five templates, grounded in the corpus's most common shapes. Each is **suggested from a map signal**
and **confirmed by the author** — never auto-applied or silently mutated. Each emits standard
`Filter` + `ApplyRule` entries (no special persisted form).

| # | Template | Trigger (signal) | Emitted wiring | Recipe |
|---|---|---|---|---|
| 1 | **Build / void enforcement** | positive build region(s) in the Build Regions step (+ `layer_y0.parquet`) | group buildable regions → apply `block_place=deny(void)` (or `never`) to the **complement** | Cluster 2.4 |
| 2 | **Spawn protection** | a team spawn region (`spawns[].region`) | on the protection zone, apply `enter=only-<team>` **and** `block=never` (anti-grief; `never` is built-in — no new filter); optionally `use=only-<team>` | Cluster 1.1 |
| 3 | **Wool-room defense** | a wool-room region with a derived owner (§6 owner) | apply `enter=not-<owner>` (defender excluded) | Cluster 1.2 |
| 4 | **Wool-room build/break** | a wool-room region | apply `block=<team>-woolrooms-filter` (team check + material whitelist) | Cluster 2.2 |
| 5 | **Wool-room chest lockout** | a wool-room region with a derived owner | apply `use=deny(all(only-<owner>,chest-filter))` — the defenders cannot open the attackers' supply chest in their own room | Cluster 1.2 |

Template 5 exists because template 3 does not cover it. Denying *entry* is not denying *use*: a
defender standing at the room's edge is outside the region but within reach of a chest inside it, and
the supply in a wool room is for the team attacking it. The rule is written as an inline filter
expression rather than a named filter — it is a two-term composition used once per team, and at the
apply is where a reader of the XML looks for it.

Template 1 is the canonical *suggest + confirm* flow: detect the positive build regions, propose
"auto-group and apply the void filter to the complement?", let the author confirm/adjust/decline.

## Interaction stance

**Suggest + confirm, never silent.** Detect a signal → propose the wiring → author confirms,
adjusts, or declines. The C3/C4 editors reject only **dangling references** (a child filter/region
that doesn't exist); "sense" (event/region/intent fit) is a **soft warning**, not a block.

## For B1

Wiring adds **no new typed shape**: the typed models are `Filter` + `ApplyRule` (`Domain/Filter.cs`, `Domain/MapModel.cs`),
and the region view exposes its attached rules via `roles` (`region-categorization.md`). The
templates above are pre-built `Filter` + `ApplyRule` combinations the C9 feature *emits* — not a
persisted entity. So B1 types filters/rules straight from §9 + the `roles` view; the C9 feature
(routes, suggestion engine, templates) builds on those types and cannot reshape them.
