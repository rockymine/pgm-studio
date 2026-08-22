# pgm-studio — TODO (current focus)

The **Now & Next** board — only the *current focus theme* lives here. Everything not in the immediate
slice is in **`BACKLOG.md`** (the long tail); shipped capabilities are in **`FEATURES.md`** (the Done
column). The three move left → right: **`BACKLOG.md` → `TODO.md` → `FEATURES.md`**.

**Holds only open work:** `[ ]` to-do, `[~]` in progress — **never `[x]`.** When a task ships, a commit
lands (its message references the id), the task **leaves this file**, and a line is added to `FEATURES.md`.
Board rules live in `CLAUDE.md` (§ "Status & task board").

## What the front door still cannot say, and the copies that outlived their reason

Not a headed group in `BACKLOG.md` — these were scattered across three of them, and they are one concept all
the same: the residue of *The boundary*. That programme made the surface describe itself and stopped three
steps short. **One key rides on every success and no schema names it**, which is the half of the contract the
last two entries never reached. **A route is still written in more than one place**, on the client this time —
the same question asked of the caller rather than the server. **An answer shape says what type a field is and
not what it is**, the mirror of the request side.

Beside them, three duplications the same rule catches, each already named as one: one write verb asked nine
ways, one team record declared four times, and one runtime answer written out as prose in two repositories,
which `GET /map/{slug}/layers` unblocked.

**One decision was taken rather than filed.** RFC 9457 Problem Details was weighed against the studio's own
refusal envelope and declined — the interoperability it buys needs a caller outside this deployment and there
is none, while the dereference it is prized for is already reachable from the `rule` each finding carries.
The reasoning is `docs/design-decisions.md` § *The HTTP surface*; the entry that asked it is retired.

- [ ] **TC4 — Three Configure steps parse the intent's teams a fourth, fifth and sixth time.**
  `AuthoringContext.LoadTeams(intent)` reads the teams out of the intent document and six steps call it.
  `SpawnStep`, `TeamAssignStep` and `ProtectionStep` each declare their own
  `private sealed class Team { Id, Name, Color }` — the same three fields `Ctx.Team` carries — and walk the
  same `JsonObject` themselves. Delete the three and call the helper, adding to it only what a caller
  genuinely reads beyond those fields. Same disease as the six private island types `RP11` collapsed, on the
  other document: the intent rather than `GET /map/{slug}`.

- [ ] **RP23 — Two documents answer "what can I ask for", in two repositories, and neither is verified.**
  `docs/tools/capabilities.md` is 707 lines of it here; `pgm-studio-mapgen`'s `AUTHORING-BRIEF.md` is 20 KB
  of the same question next door, and the two were written by different hands from the same code. `GET /map/{slug}/layers` puts the allowed
  moves on the map's own response, so that question has a runtime answer and neither document should be
  trying to hold it. What is left is the half prose is actually good at and neither file
  is organised around: **how to make a good map** — the art direction, what an objective needs around it,
  what the corpus does — as against **what the system can be asked for**, which is the API's to say. Split
  them on that line: the capability half goes, the craft half moves to where its subject lives under
  `docs/gameplay/`.
