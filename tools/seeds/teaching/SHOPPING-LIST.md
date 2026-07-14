# Teaching-seed shopping list

Curated `*.plan.json` examples that exist **only to inform `layout-rules.md`** — do's and
don'ts, like the mid `build-interface-dos-and-donts` set. They are **not** part of the stat
corpus and live here in `tools/seeds/teaching/`, kept out of the corpus so they never drive
generated-seed validation (the seed tests enumerate `tools/seeds/` top-level only, so this
subfolder is excluded — see the composer handoff). Deliberate anti-patterns and disclaimed
dead-ends are allowed. Each authored set feeds a `layout-rules.md` amendment via the
correction protocol.

This is the running list so features don't get lost — the analogue of the general-map
"shopping list" that produced the 12-seed corpus, but for **individual layout features**.

Per row: **feature · rule ids it teaches · status.** (Author the set → visual review → the
do/don't findings become a rule amendment.)

| Feature | Teaches | Status |
|---|---|---|
| **Build-zone interface** — dock vs overlap, connector readability, fit | BZ6–BZ9 | ✅ shipped — `build-interface-dos-and-donts.plan.json` |
| **Frontlines** — wide vs double frontline; **grid** stepping stones (2×2) not a 1-D chain; stones aligned to / slightly inset from the build-zone border; single wide frontline the build zone docks to fully | new (CT/BZ) | ✅ rules landed — FR6/FR7/CT9/MD6/BZ10/HB4 (`mirror-mid-examples`) |
| **Buffer / intentional-gap** — the non-generating buffer tile; spacing between spawn↔wool & wool↔wool lanes; rot_90 **centre buffer** + ≥1-cell **border reservation** so the quarter-turn image can't self-collapse | new | 🧩 buffer piece shipped (FEATURES); teaching examples + rules pending |
| **Wool-lane shaping** — fold long lanes into **Z/S** (the `gen-p30-s7` Z is the positive) instead of long L/I extrusions; a second bridge parallel to the first into a deep wool segment; **alternate attack routes via rings-with-holes** | CT8 / new | queued |
| **Hub shaping** — multi-piece / varied hub, not one square (L/Z composition) | HB4 | ✅ rule landed (HB4); composer impl in G36 |
| **Mid interface forms** — clean / parallel-approaches / hash `#`; **rot_90 archetypes** (grid / window-frame / plus / axis-sitting) | CT1 · CT10/CT11 | rot_90 rules landed (`rot-90-mid-example-*`); 2-team forms queued |
| **Isolation cuts** — wool/spawn severed behind a fragile bridge | CT5, SP6 | queued |
| **Closure holes / rings** — frontline-ringed (the device) vs forbidden wool-ringed WL8 | CT8 | queued |
| **Stones gradient** — thinning toward the team side, grid-aligned | CT4, CT7 | queued |
| **Wool-room approaches** — stepped climb, marker distances, two-approach | WL2/WL5/WL7/WL8 | queued |
| **Spawn discipline** — raised + facing, iron, wool never behind spawn | SP1/SP3/SP4/SP7 | queued |
| **Elevation transitions** — plateau defaults, odd-height stepped palette | EL1/EL6 | queued |
| **Walls** — marks on terrain↔wool seams | ST1/ST4 | queued |

Findings that motivated the near-term sets came from the author's B2 composer review.
