# Flags — the CTF and King of the Flag objective

A flag is a banner players carry. It stands at a **post** until someone picks it up; while carried it is worn
as the carrier's helmet and can pay its team a rate every tick; dropped, it lies where it fell until it is
picked up again or recovered home; and brought into a **net**, it is captured, paying the net's points and
going back to a post. Capture the Flag is played with owned flags and nets. King of the Flag is played with
one unowned flag, no net, and a points rate: the team holding it scores for as long as it holds it.

The studio reads and writes the whole `<flags>` module — flags, posts single and composite, nets nested and
declared beside them — and the five flag filters. This document is the contract: what PGM does, read from
`tc.oc.pgm.flag` in `PGMDev/PGM`, and what authors build, measured over both corpora.

## 1. The module and the order it is read in

`FlagParser.parse` (`flag/FlagParser.java:329`) reads three passes over the document: every `<post>` declared
directly under a `<flags>` group, then every `<flag>`, then every `<net>` declared beside the flags. The order
is what lets a flag name a post and a net name a flag without a forward reference. `<flags>` groups nest, and a
group's attributes are inherited by every flag, post and net under it, the nearest declaration winning — the
same cascade `<wools>` and `<hills>` use. Of the 157 corpus maps carrying the module, 45 groups nest inside
another.

A map with no flag loads no flag module at all, whatever posts or nets it declares.

## 2. A flag

A flag (`FlagParser.parseFlag`, `:250`) names its post either by an attribute, `post="id"`, or by one child
`<post>`; more than one child post is refused by PGM, which asks for one composite instead (`:65`). Every other
attribute is optional and defaults as the table says.

| attribute | default | what it does |
|---|---|---|
| `owner` | none | the team defending it; unowned, the carrier's team takes what it pays |
| `color` | the banner's | the dye its name, chat colour and wool take |
| `points` | 0 | paid once, on capture |
| `points-rate` | 0 | paid every tick while carried (`state/Carried.java:208`) |
| `shared` | false | several carriers at once |
| `pickup-filter`, `capture-filter`, `drop-filter` | allow | who may pick it up, capture it, drop it |
| `pickup-kit`, `drop-kit`, `carry-kit` | none | given on pick-up, on drop, and while carried |
| `required` | true on current protos | whether capturing it is needed to win |
| `drop-on-water`, `beam`, `allow-manual-drop` | true | |
| `show-respawn-on-pickup` | false | |
| `flag-proximity-metric`, `net-proximity-metric` | closest kill, closest player | the sidebar's distance |

A flag is not loaded unless a **banner** stands at its starting post. `Flag` looks at the block under each
point of the default post's fallback, and failing to find a standing or wall banner throws "must have a banner
at its default post" (`flag/Flag.java:104`–`:124`). The banner's base colour, patterns and facing are copied
into the flag players carry, and an unset `color` is the banner's base colour (`:157`). At runtime the flag is
always placed as a standing banner, on a sea lantern where the block under it cannot hold one.

## 3. A post

A post is where a flag stands, and where it comes back to. A **single** post is one or more places: a point
written as its text (`<post>11.5,9,24.5</post>`), or child regions — a `<block>`, a `<region>`, any shape —
each of which is one place (`:120`). A **composite** post holds single `<post>` children, each inheriting the
composite's attributes (`:85`). The flag starts at the composite's `fallback`, or its first child; the next post
is the first one whose `respawn-filter` allows it, in order where `sequential`, at random otherwise.

| attribute | default | what it does |
|---|---|---|
| `recover-time` (`return-time`) | 30s | how long a dropped flag lies before it goes home |
| `respawn-time` / `respawn-speed` | 8 blocks a second | how long the flag takes to come back; both at once is refused |
| `respawn-filter` | allow | whether this post may take the flag next |
| `pickup-filter` | allow | who may pick it up here |
| `points-rate` | 0 | paid while the flag stands here and the post has an owner |
| `owner`, `permanent`, `sequential`, `yaw`, `pitch` | | |

A post with no `yaw` faces the way the original banner faced.

## 4. A net

A net (`FlagParser.parseNet`, `:181`) is a region a carrier stands inside to capture. It takes the flags its
`flags` attribute names, or, written inside a flag, that flag alone; a net beside the flags naming none takes
every flag. Capture pays the net's `points` to its `owner`, or to the carrier's team, and sends the flag to the
net's `post`, or back to its own; `rescue` names flags the capture sends home, and `respawn-together` holds every
flag until all are captured. `sticky`, true by default, lets a carrier refused by the capture filter step out
and capture later when it allows.

## 5. The gamemode

The flag module tags a map `ctf` and nothing else (`flag/FlagModule.java:28`). King of the Flag is a gamemode a
map states, `<gamemode>kotf</gamemode>`, and PGM never derives it; 29 of the 38 corpus `kotf/` maps state
none, so PGM lists them as CTF. `Domain.Gamemodes.From` follows PGM — a flag is `ctf` — and a map's declared
`<gamemode>` elements round-trip as they were written.

## 6. What the studio reads and writes

`MapParser.ParseFlags` reads the module in PGM's order into `MapXml.Flags`, `MapXml.FlagPosts` and
`MapXml.FlagNets` (`Domain/MapModel.cs`: `Flag`, `FlagPost`, `FlagNet`). Every knob is the map's or unset: an
unwritten attribute stays null or empty, because the value PGM would put in its place is PGM's to decide.
Group attributes are resolved onto each flag, post and net as PGM resolves them, so the written module is flat.
A composite's children are written with only the attributes they do not share with it, and a child does not
take its composite's `id` or `fallback`.

A filter, region or message the map wrote **inline** is read under a synthetic id and written inline again; one
it named stays a name. That covers a net's child `<region>`, a post's child `<block>`, a child
`<capture-filter>`, and a child `<carry-message>`. The five flag filters — `flag-carried`, `flag-dropped`,
`flag-returned` and `flag-captured`, each with an optional `post`, and `carrying-flag` — are read and written
with the rest of the filters.

In the map document a flag is `flags[]`, the posts and nets beside them `flag_posts[]` and `flag_nets[]`
(`Serializer.EncodeFlag` and its two siblings). In the database each is one row of that JSON (`flag`,
`flag_post`, `flag_net`, migration `M0069`), read back in document order.

Swept over every corpus map carrying `<flags>`, the 148 the studio reads come back with every flag, post, net
and filter unchanged through both the XML and the JSON codec.

## 7. What it refuses

A flag that defines its `pickup-kit`, `drop-kit` or `carry-kit` as a child element rather than naming a kit is
refused (`UnsupportedMapException`): the studio reads kits by id, and an inline kit would be dropped. Three
corpus maps write one.

## 8. What the corpus builds

Of the 157 corpus maps carrying `<flags>`, three shapes cover all but four:

| shape | maps |
|---|---|
| owned flags and nets — CTF | 65 |
| one or more unowned flags, a points rate, no net — King of the Flag | 45 |
| unowned flags carried into a net | 43 |

A King of the Flag flag pays `points-rate="1"` on 47 of the 55 that state one, and 0.5 on the rest. Its
pickup kit, where the map states one, lowers the carrier's health — health boost at −3 (12 health) on 6 maps,
at −2 (16) on 2, and `<max-health>14` on 1 — or shields it with absorption III for three seconds, on 5; none
raises it. Of the 41 composite posts in the corpus, 38 hold two or more places, and three is the commonest
count (22).

Nets come in pairs: 87 of the 108 maps with nets carry exactly two, one a team, and a net that states points
pays 1 on 165 of 185. 135 of the 287 nets state a capture filter, `only-red` and `only-blue` the commonest.
