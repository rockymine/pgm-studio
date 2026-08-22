# pgm-studio — TODO (current focus)

The **Now & Next** board — only the *current focus theme* lives here. Everything not in the immediate
slice is in **`BACKLOG.md`** (the long tail); shipped capabilities are in **`FEATURES.md`** (the Done
column). The three move left → right: **`BACKLOG.md` → `TODO.md` → `FEATURES.md`**.

**Holds only open work:** `[ ]` to-do, `[~]` in progress — **never `[x]`.** When a task ships, a commit
lands (its message references the id), the task **leaves this file**, and a line is added to `FEATURES.md`.
Board rules live in `CLAUDE.md` (§ "Status & task board").

## The boundary: one contract, one use case, one class of fault

`docs/architecture.md` is the survey these came out of. The studio has one front door — 149 HTTP endpoints —
and a pipeline behind it whose steps have no home but the routes that reach them, and every entry here is a
fact the studio knows and cannot say in a shape a caller can parse. They depend on each other
in the order listed: the contract first, because the request shape and the client both hang off it; the
application layer second, because it is where a gate stops belonging to a door; the fault class third; the
lifecycle last, because a state machine over a pipeline of HTTP handlers has nothing to hold.

- [ ] **RP36 — 108 schemas describe the type and none of its fields.** The schema publishes the docstrings
  the DTOs carry: 201 of 222 schemas have a description, but only 174 fields do, and in 108 of those schemas
  the type's prose is doing the fields' work. `PlanPiece` is the worked case — its blurb explains `rect`,
  `surface` and `mirrors` and says nothing about `role`, which is the one field a caller has to fill and the
  one whose allowed words it cannot guess. Write a `<param>` per field on the records a driver posts or
  reads: the write-route requests first (`RP12` authors them), then `PlanModel` and the plan pieces. Twenty-one
  schemas still carry nothing at all and are the second pass. The measure is the field percentage at
  `/api/openapi/v1.json`, not the schema one.

- [ ] **RP37 — The closed word sets are published as free strings.** `PgmStudio.Vocabulary` exists so three
  parties spell a `map.stage`, a `style.kind`, a theme bucket, a room part and a roof form identically — ten
  sets, `MapStage` plus the nine in `TerrainVocabulary` — and every one of them reaches the wire as a bare
  `string`. `Severity`, `RuleCategory` and `RuleConcern` are the document's only `enum`s, because they
  are the only ones declared as C# enums. So a generated client types `role` as `string` and an agent learns
  the four stages by being refused. Publish each set as a schema `enum` — a `JsonStringEnumConverter`-backed
  enum where the set is genuinely closed, or NSwag's schema processor reading the `All` array where the
  `const string` shape has to stay for `Minecraft`.

