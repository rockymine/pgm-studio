# Wool-lane training set

Hand-labelled wool-lane examples — the ground truth the deriver's lane-shape classifier
(`WoolLaneShape`, `src/PgmStudio.Pgm/Plan/WoolLaneShape.cs`) is calibrated against. This is the
**derive-then-override** loop (`docs/contracts/layout-evaluator.md` §5.4): you author the example and
pin the label *you* intend; the classifier proposes its own; the **disagreements are the fix list** —
either the classifier is wrong, or the vocabulary needs a new term.

## Format

Two ways to author, both `symmetry: "none"`:

**Many examples per file (preferred)** — pack independent lanes into one file and let the *piece id*
carry the intent, via the convention `<group>-<role>[-<ord>]`:

- **`group`** = `<shape><variant>` — leading **letters** are the intended shape, trailing **digits**
  the instance (`i1`, `l1`, `z1`, `z2`, `plaza1`). Everything before the first `-` is the **join key**;
  strip its trailing digits to get the intended label. The id *is* the ground truth, so `labels.json`
  is optional here.
- **`role`** — a closed token set: `wool` (the `wool-room`, one per group; the wool placement points
  at it) · `lane` (a corridor `piece`) · `room` (a wide plaza `piece`) · `dock` (a terrain hub `piece`,
  make it **≥ corridorWidth+1 wide** so the flood stops there) · `end` (a terminus `piece`). Split/cut
  marks live in `zones[]` (`entry` / `cut`) and are **orthogonal to the shape** — a cut Z is still shape
  `Z`, so name it `z2-cut`, not a new shape prefix.
- **`ord`** = `a`,`b`,`c`… when several pieces share group+role (`z1-lane-a`, `z1-lane-b`).
- Offset each group in `x` so their terrain never touches — every group is classified in isolation from
  its own wool room.

`_reference.plan.json` is the worked demo (I / L / Z / Z-with-cut / plaza), each group verified to
classify as its prefix.

**One example per file (starter form)** — a single lane per `*.plan.json` with a `hub` piece, the lane
pieces, and the `wool-room` at the dead end, labelled in **`labels.json`**
(`"<file-name-without-.plan.json>": "<your label>"`, a free-form string). The starter files
(`i-straight`, `l-back-corner`, `z-double-bend`) use this form. Anything the classifier doesn't emit
shows as a MISMATCH — exactly what you want when the vocabulary is missing a term.

## Check

```
dotnet run tools/deriver/lane-audit.cs
```

Prints `author | deriver | OK/MISMATCH` per wool and a `FIX` list of every disagreement. A clean run
means the classifier agrees with you; a MISMATCH is a to-do (fix the classifier, or extend the
vocabulary + re-classify).

## Current vocabulary (what `WoolLaneShape` emits)

`I` straight · `L` one bend · `Z` two bends · `complex` ≥3 bends (wool on a chunky island) ·
`plaza` a chunk right at the room · `none` no terrain corridor (the room docks a build zone / is the
whole island — the SpawnWoolRooms stretched-room bug). Bends are counted as reflex corners of the
corridor; a junction is a block wider than the corridor (`(W+1)×(W+1)`).

The starter files (`i-straight`, `l-back-corner`, `z-double-bend`) are the format demo — replace or
extend them with your own cases, especially the ones where the classifier gets it **wrong**.
