# pgm-studio — TODO (current focus)

The **Now & Next** board — only the *current focus theme* lives here. Everything not in the immediate
slice is in **`BACKLOG.md`** (the long tail); shipped capabilities are in **`FEATURES.md`** (the Done
column). The three move left → right: **`BACKLOG.md` → `TODO.md` → `FEATURES.md`**.

**Holds only open work:** `[ ]` to-do, `[~]` in progress — **never `[x]`.** When a task ships, a commit
lands (its message references the id), the task **leaves this file**, and a line is added to `FEATURES.md`.
Board rules live in `CLAUDE.md` (§ "Status & task board").

## The board is empty

The programme `docs/architecture.md` named has run. It was a dependency chain and it drained in order:

**Say what the surface is.** Every operation declares what it answers, what it takes and which of 404, 409
and 422 are its own; the endpoint tables are held to all three in both directions; every route the client
calls is held to the schema. There is no generated client and will not be one — the response types already
come from `Contracts`, and what a generated one would still buy is a path check that costs nothing
(`RP41`, `RP42`, `RP43`, `RP40`, and `RP29`/`RP12`/`RP11` before them).

**One place a use case lives.** The handlers that read stored state, do work and write it back are seven
operations in `Api/Services` rather than bodies inside the door they were reached through. Under them: one
refusal shape, one load-or-404 prologue where 47 had been written out, one slug derivation where three had
drifted apart (`RP13`).

**The loop answers for itself.** `GET /map/{slug}/findings` asks every gate the stored documents can reach
and names the ones it did not; `GET /map/{slug}/layers` says where a map has got to and what may be done to
it next. A driver acts, then asks (`RP32`, `RP16`).

**And the drift the survey measured beside it** is gone: the world builder is named for what it builds, a
world is read where the format is and derived from where the derivations are, the board draws as characters
off a posted plan, the comments describe the code as it stands, and the census counts itself (`WE11`, `WS5`,
`TN4`, `TN5`, `RP10`, `RP8`, `RP19`).

## What to pull up next

`BACKLOG.md` holds the long tail, grouped by concept. **Pull up a whole group rather than a task**, per
`CLAUDE.md` § *Status & task board*: a group's entries are gathered because they spend the same foundation,
and reading that foundation for duplication is what makes the entries small enough to do. That is what this
programme kept proving — nearly every entry on it turned out to be a duplication wearing the shape of a
feature, and naming the duplication is what did the work.

Two findings were filed while it ran and are waiting there: `C47` (three Edit phases each carrying their own
HTTP half, so the routes they reach are written in no single place) and `RP47` (the history sweep's grep was
one phrasing of several).

**The obvious group to pull up is `The boundary: one contract, one use case, one class of fault`** — six
entries, and the one this programme came out of. `RP33`–`RP38` are its remainder: three names in `Contracts`
that say the wrong thing, two records naming one box two ways, a rule catalogue publishing more than is
raised, 108 schemas describing a type and none of its fields, closed word sets crossing as free strings, and
a region stating its numbers flat to create and nested to patch. Every one of those is a shape said twice.
