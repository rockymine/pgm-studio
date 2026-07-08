# Wool-lane training set

Hand-labelled wool-lane examples — the ground truth the deriver's lane-shape classifier
(`WoolLaneShape`, `src/PgmStudio.Pgm/Plan/WoolLaneShape.cs`) is calibrated against. This is the
**derive-then-override** loop (`docs/contracts/layout-evaluator.md` §5.4): you author the example and
pin the label *you* intend; the classifier proposes its own; the **disagreements are the fix list** —
either the classifier is wrong, or the vocabulary needs a new term.

## Format

- **One example per file**, `*.plan.json`, `symmetry: "none"` — a single team unit with **one wool
  lane**: a `hub` piece (the junction the lane docks into, ≥3 cells each side so the corridor stops
  there), the lane pieces, and the `wool-room` at the dead end. Nothing else needed — no spawn, no
  symmetry, no zones. Author it however you like (the `/plan` editor, or by hand).
- **`labels.json`** — `"<file-name-without-.plan.json>": "<your label>"`. The label is a **free-form
  string**: use `I` / `L` / `Z` to match the current vocabulary, or write your own (`L-cut`, `U`,
  `approach`, …). Anything the classifier doesn't emit is guaranteed to show as a MISMATCH, which is
  exactly what you want when the vocabulary is missing a term.

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
