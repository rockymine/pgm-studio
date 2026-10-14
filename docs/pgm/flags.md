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

A flag is not loaded unless a **banner** stands at its starting post. `Flag` looks at the block each point of
the default post's fallback is in, or every block of a post's region, and failing to find a standing or wall
banner throws "must have a banner at its default post" (`flag/Flag.java:104`–`:124`). The banner's base colour, patterns and facing are copied
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

## 7. Authoring a King of the Flag board

`MapIntent.Flags` states King of the Flag with flags no team owns: each `FlagIntent` is a name, a colour, its
posts — each a point, a name and an optional facing — a `PointsRate` (1 by the author's ruling, and on 47 of 55
corpus flags), and what carrying it does to the carrier. `FlagGenerator` writes it as one unowned flag over one post: a single
post where it stands at one place, a composite where it stands at several, starting at the first and coming
back to one of them at random. Like a capture point a flag belongs to nobody, so under a symmetry its posts fan
by position — each post off the centre gains its images, named on and turned — and a post at the centre stays
one.

The carrier is one of `FlagCarriers`. `lowered`, the default on a flag nobody owns, gives a pickup kit of `<max-health>` at
`CarrierHearts` — 6 hearts unstated, 12 health, the corpus's commonest King of the Flag carrier — and a drop kit
of `<max-health>20`; PGM sets max health back to 20 on respawn too. `absorption` gives absorption III for three
seconds and needs no drop kit. `boost` gives health boost raising the carrier to `CarrierHearts`, two hearts at a
time above ten (12 at least), and a forced drop kit takes it away again: a forced kit's effect at duration 0
removes that effect (`kits/PotionKit.java:28`). `none` gives no kit at all. Kits carry `<max-health>` through
every codec for this.

A board whose flags pay states a `<score>` limit of 200 where the author states none — the corpus's commonest,
on 5 of the 10 King of the Flag maps that state one — through `ScoreGenerator`, which writes the one `<score>`
a capture point and a flag both pay into. Its `<gamemode>` is `kotf`, since PGM would call it CTF, and its
objective reads "Hold the flag!". It carries no wool, destroyable or core, so it is not built on
(`new-map-authoring.md` §5c).

PGM loads none of it until a banner stands at each flag's first post, so the world build stands one at every
post (`FlagStamper`, `WorldBuilder.StampFlags`). Flags are stated on the intent alone, so the build fans their
posts across the board's symmetry itself — the intent's, or the layout's where the intent states none — before
it stamps, and every image is a banner too. Each banner is a standing banner in the flag's colour, turned to the
post's yaw in sixteenths of a turn from south, in the first air over the ground under the post's column; the
block is recorded on the post as `BannerBox`, and the post's point is that block's centre, so the point PGM
reads and the banner it looks for are one block. The ground around a post is a goal's ground to the dressing
pass, kept clear of cover and of trees, boulders and buildings (`world-export/decoration.md` §3.1). A post is a goal
the walk has to reach: the export refuses a board whose post no spawn can walk to (`EX1`,
`world-scan/read-backs.md`).

On a map read from a world, the Configure tool's Flags phase states the intent: a scanned banner becomes a post,
and the export refuses a flag that does not start on one (`OB36`, `tools/configure.md`).

## 8. Authoring a Capture the Flag board

A `FlagIntent` naming a `Team` is that team's flag, and `FlagGenerator` writes it as Capture the Flag is played
on 65 corpus maps. It is owned by its team and named for it — "Red Flag" where the intent names none — and its
colour is the team's, as a dye, unless the intent states one. PGM lets anyone pick a flag up, its own team
included, so the flag carries a `pickup-filter` allowing everyone but its team, as 152 of the 166 corpus team
flags do. It pays nothing while carried, since no corpus team flag states a points rate, and carrying it does
nothing to the carrier unless the intent states a `Carrier`, as 98 of 166 do nothing. A dropped team flag goes
home after `RecoverTime`, 15 seconds unstated — the corpus's commonest stated time, on 42 flags.

**A team captures in its own base.** Each team owning a flag gains one net, declared beside the flags and taking
every flag another team owns; a net naming no flag takes every flag, so a team with nobody else's flag to take
gains none. The net is the flag's `Net` box, or a box reaching two blocks either side of the block the flag's
first post stands in and three blocks up — 66 of the 116 corpus nets a sweep could place sit at the capturing
team's own post, none at the captured flag's, and a cylinder of radius 2 or 3 or a 5×3×5 box is the common
size. A capture pays 1 (122 of 140 nets) to the carrier's team, and the board ends at 3 where the author states
no score limit, the commonest of the 30 CTF limits. Its `<gamemode>` is `ctf` and its objective reads "Capture
the enemy's flag!".

**When a team may capture is the author's choice.** `Capture` on a team's flag is one of `FlagCaptures`:
`anytime`, the default, or `home`, under which its team's net captures only while every flag that team owns
stands at a post. The net then carries a `capture-filter` of `<flag-returned>` on each of them — PGM's state
for a flag standing at a post, recovered or never taken — under an `<all>` where the team owns several; a
carrier refused by it waits in the net, which is sticky, and captures the moment the flag is back. The corpus
rarely states a capture rule at all: 30 of its 142 team-flag nets carry a capture filter, most of them naming
the team that may capture there.

A team's flag fans by team as a core does: under a symmetry each other team gains its image — every post and the
net turned, named and coloured for that team — and a team already owning a flag keeps its own. The world build
fans it the same way before it stamps, so each team's banner stands in its team's colour.

## 9. What it refuses

A flag that defines its `pickup-kit`, `drop-kit` or `carry-kit` as a child element rather than naming a kit is
refused (`UnsupportedMapException`): the studio reads kits by id, and an inline kit would be dropped. Three
corpus maps write one.

## 10. What the corpus builds

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
