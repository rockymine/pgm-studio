# Zones — a region with rules of its own

A zone is a region that states rules for whoever is in it: who may walk in, whether blocks may be placed or broken
there, whether anyone is hurt, and a kit given inside. A spawn's protection is a zone that lets one team in and
keeps every block as it is; a wool room is one that turns its defenders away and lets attackers change only what
the room allows; a build region is one where a player may build over the void; a water lane is a build region
that opens later. This document is how the studio writes every one of them, and how an author states one of
their own.

## 1. What a zone is written as

PGM has no zone element. A zone is a region — `<regions>` — and an apply rule over it for each thing it says, read
in the document's order (`filter-region-wiring.md` §2): PGM stops at the first rule that decides an event, so the
order rules are written in is part of what they mean. A zone that says no player is hurt inside writes no apply
rule for it; it writes a `<deny>` of the region into the `<damage>` module (`damage.md`), which asks every hit.

## 2. One writer

`ZoneWriter` is the one writer every zone goes through. `ZoneWriter.Region` writes a footprint of rects as one
region — the rect itself, or `id-1…id-n` unioned into `id` — and `ZoneWriter.Rules` writes a `ZoneRules` over a
region in one order: who may enter, then what may be placed and broken, then what may be used, then the kit,
then a harmless zone's deny. A rule left null is not written, so a zone says only what it states.

The slices that write zones of their own each go through it, and their intent shapes are unchanged. A spawn's
protection (`TeamsGenerator`) is its rects as one region with an `enter` rule naming the team, and every spawn's
protection gathered into `spawns` carries one `block="never"`. A wool room (`WoolGenerator`) is its rects with an
`enter` rule turning the defenders away, a `block` rule letting attackers edit only the room's materials, and a
`use` rule keeping the defenders' hands off their own chest. The build region (`BuildGenerator`) keeps its own
region ids — `build-area`, `buildable`, `not-build-area` — and writes its void rule through the writer, as a
board that is not built on writes its one rule over `everywhere`. A water lane is a region and an include
(`water-lanes.md`), and states no rule of its own.

## 3. A zone an author states

`MapIntent.Zones` states the rest. A `ZoneIntent` is an `id`, the `area` it covers as rects, an optional `team`,
and what it says: `enter` — `anyone`, `team` or `nobody` (`ZoneEntries`) — with the message a player turned away
reads, whether a block may be `place`d or `break`-broken there with the message a refused edit reads, whether a
player inside may come to `harm`, and the `kit` given inside, by id. Everything defaults to saying nothing, so a
zone stating only a kit gives the kit and changes nothing else.

`ZoneGenerator` writes each as the region `zone-` and its id — and the team's slug, for a team's zone — through the
writer, before the build slice, so a zone's refusal to let blocks be placed decides inside a build region too. A
team's zone fans by team under the board's symmetry, as a spawn's protection does, with the team it lets in
turned with it; a zone with no team orbits by position, as a build region does.

```json
"zones": [
  { "id": "lobby", "area": [{ "minX": -6, "minZ": -6, "maxX": 6, "maxZ": 6 }], "harm": false, "place": false, "break": false },
  { "id": "vault", "team": "red-team", "enter": "team", "area": [{ "minX": 30, "minZ": -4, "maxX": 38, "maxZ": 4 }] }
]
```

A plan states its build regions and water lanes as zones of those two kinds; a plan that states a zone of
rules is `G292`, because the layout rules about zones read every zone a plan states.
