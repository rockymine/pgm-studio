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

- [ ] **RP35 — `/api/rules` answers 169 rules and 88 of them are raised.** `RuleCatalog.Read` concatenates
  the **77 gate rules** declared as `const string` in the fourteen `*Rules` classes with **92 layout rules**
  parsed out of `docs/generator/rules.md`, of which the plan validator fires **fifteen**:
  `BZ5 BZ11 CT12 EL1 FR8 G2 G5 SP1 SP2 SP8 SP9 ST2 ST8 ST9 WL1`. The other 77 are law nothing checks,
  published in rows identical to the ones a caller can actually fail on.

  **Four gate rules are in the same position and were put there deliberately** — `WX1`, `WX5`, `WX7` and
  `WX9` state how a room frame is derived rather than refusing anything, and they are constants because a
  rule may not live only in a markdown file (the author's ruling). So the cut below needs a line drawn that
  is not *is it raised*: those four are the studio's own answer to "what is `WX7`", asked by a reader of
  `structures.md` rather than of a finding.

  **Cut them rather than label them** (the author's ruling): the catalogue answers *what is this finding*, and
  a rule nothing raises has no finding to explain. `rules.md` keeps all 92 as the generator's law; it is not
  the API's to publish. Three go from `rules.md` too, being history rather than law — `BZ1` "Superseded by
  FR1+FR2", `EL6` "[retired 2026-08-14]", `PC-S` "retired — the old per-seam sliver lint", which is free to
  delete now that `PC-C`'s law is no longer stated inside its body. `BZ5` stays: retired as a prohibition,
  still fired.

  **`GO1` is the one exception and is not deleted** — the author's amendment is ahead of what the studio
  measures, and it rejoins the catalogue when *Distance, and the walk every measure is taken with* enforces
  it. `RulesEndpointTests` gains the assertion from the other side: every row answered has an emit site.
  `RP46` is the same seam from the other direction.

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

