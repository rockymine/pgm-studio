# The match — how it ends, and how the dead come back

A PGM match ends when a team completes its objectives, reaches the score limit, is the last one standing, or
runs out of time; and between the start and the end its players die and come back. Five modules state those
rules — `<time>`, `<respawn>`, `<blitz>`, `<rage>` and `<kill-rewards>` — none of them an objective, and a
sixth rule, the kill height, is a portal rather than a module. This document is the contract, read from
`tc.oc.pgm.timelimit`, `spawns`, `blitz`, `rage` and `killreward` in `PGMDev/PGM`, what the corpus builds, and
how a board states them.

## 1. The time limit

`TimeLimitModule` reads one `<time>` element at the root: its text is the duration, in PGM's notation (`10m`,
`8m30s`, `1h`), and `result` names what decides the match when the clock runs out. A result is `default` — the
match's own order, the team furthest along its objectives and then the higher score — `objectives`, `score`,
`tie`, or a team's id, which wins outright. `overtime`, `max-overtime` and `end-overtime` extend a match whose
objectives are in contention at the bell, and `show="false"` hides the clock. On an older proto the limit may sit
inside `<score>` or `<blitz>` instead, and PGM reads it as the same limit; it refuses one there from
`REMOVE_SCORE_TIME_LIMIT` on, and refuses two.

Of the 1,522 corpus maps the studio reads, 439 state a time limit. Across both corpora five minutes is the commonest
duration (108 maps), then ten (86) and eight (65); 79 state `result="score"`, 20 `objectives`, and
51 an overtime.

## 2. Respawning

A dead player waits out a **delay** — two seconds where nothing states one, and never less than PGM's minimum —
then comes back on a click, or on their own where `auto` is true. `spectate` lets them watch the match as an
observer while they wait, `blackout` darkens the screen, `bed` respawns a player at the bed they last slept in,
and `message` is what they are shown once they may come back, as an attribute or a child.

`SpawnModule` reads every `<respawn>` at the root as one rule, each element overriding what the last stated,
with the legacy `<autorespawn/>` turning `auto` on; that rule may not carry a filter. Beside it, every `<respawn>`
under a `<respawns>` group is a rule of its own, flattened with the group's attributes as `<flags>` and `<wools>`
are, and each takes a `filter`: a player's rule is the first whose filter allows them, and the root rule
catches everyone else. 306 corpus maps state a root rule, 222 of them `auto="true"`, and three seconds is the
commonest stated delay; 32 state filtered rules.

## 3. Blitz and rage

`<blitz>` gives every player a number of `lives` — one where the element states none, and one on 97 of the 123
blitzes that state a number — and a team with nobody left alive is out. `broadcastLives`, a child only and true by
default, tells the match how many lives a dying player has left; `filter` limits the lives to the players it
allows, `join-filter` lets a player join once the match has started, and `scoreboard-filter` decides which
teams the sidebar counts. PGM reads every `<blitz>` element and keeps the last one whole.

`<rage/>` makes every hit with a weapon kill. Neither changes what a map is played for, so PGM tags each as an
**auxiliary** gamemode, sorted after every other: a blitz CTW map is CTW and blitz. `Domain.Gamemodes.From`
follows it. 164 corpus maps carry a blitz and 127 rage.

## 4. Kill rewards

A `<kill-reward>` pays a killer for a kill its `filter` allows — every kill where it states none — with the
items it lists, a `kit`, an `action` run on the killer and a `victim-action` run on the victim. PGM reads the
element under either spelling, `kill-reward` or `killreward`, grouped under `<kill-rewards>` or `<killrewards>`
or straight under the root, a group's attributes reaching each reward in it; every reward that allows a kill
pays it. The kit is a name, or a kit written inside the reward, and a childless `<kit id>` there is a reference.
Of the 1,393 rewards in both corpora 1,237 list items, 404 state a filter — most often a `<kill-streak>` — and
171 a kit.

## 5. The kill height

PGM has no module for the height a player falls out of the board at: the world's own void kills below the
floor, and a board floating high over it, or over ground nobody should reach, states one with a portal. A
`<portal>` over a `<below y>` region moving the player to `y="@-64"` drops whoever passes under the height into
the void, where they die with what they carry; `observers="never"` lets an observer fly on. Eleven corpus portals
send a player to a negative `@` height.

## 6. What the studio reads and writes

`MapParser` reads the five modules into `MapXml.TimeLimit`, `Respawn`, `Respawns`, `Blitz`, `Rage` and
`KillRewards` (`Domain/MapModel.cs`: `TimeLimit`, `RespawnRule`, `BlitzConfig`, `KillReward`). Every duration is
kept as written and every knob is the map's or unset. A filter written inline is read under a synthetic id and
written inline again; a kit written inside a reward joins `MapXml.Kits`, under a synthetic id where it states
none, and is written back inside the reward. A legacy time limit is written at the root, where every proto
reads it. An action written inside a reward rather than named is not read, since the studio does not read
`<actions>`; six corpus rewards do it.

In the map document the rules are `time`, `respawn`, `respawns`, `blitz`, `rage` and `kill_rewards`
(`Serializer.EncodeMatchRules`), and in the database one row of that JSON per map in `match_rules` (migration
`M0074`). A map imported before the table existed holds none of them until it is imported again.

Swept over both corpora, every one of the 1,290 parseable maps carrying any of them — 439 time limits, 296 root
respawns, 25 respawn groups, 162 blitzes, 120 rages and 973 kill reward blocks — writes them back unchanged
through the XML and the JSON codec.

## 7. Authoring a board's match

`MapIntent` states the rules beside `ScoreLimit`, and `MatchGenerator` writes them after the score:

| member | what it states | written as |
|---|---|---|
| `timeLimit` | `duration` (`10m`) and `result`, one of `TimeResults` | `<time result>`; an empty duration removes it |
| `respawn` | `delay`, `auto`, `spectate` | the root `<respawn>` |
| `lives` | the lives each player has | `<blitz><lives>`; zero removes it |
| `rage` | every hit kills | `<rage/>` |
| `killHeight` | the height a player falls into the void below | a `kill-height` region below it and a portal on it to `@-64` |

`KillPoints` beside them states what a kill pays, and `ScoreGenerator` writes it as the score's `<kills>`: a
board whose kills pay is a team deathmatch, stated `tdm`, reading "Get the most kills!", and played to a clock —
147 of the 167 corpus maps scoring kills pay 1 a kill, 139 state no score limit, and ten minutes is their
commonest time — so one stating neither limit ends after ten minutes.

A member the intent leaves null is left as the map has it, so a board built from an imported map keeps the
rules it came with; the kill height is the generator's own, rebuilt on every write and removed when the intent
states none. The rules are map-wide, so nothing fans across a symmetry. A board stating `lives` or `rage` states
`blitz` or `rage` among its gamemodes, after the ones its objectives make it. A plan-built board states the same
members through its refinement (`tools/flow.md`).
