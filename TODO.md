# pgm-studio — TODO (current focus)

The **Now & Next** board — only the *current focus theme* lives here. Everything not in the immediate
slice is in **`BACKLOG.md`** (the long tail); shipped capabilities are in **`FEATURES.md`** (the Done
column). The three move left → right: **`BACKLOG.md` → `TODO.md` → `FEATURES.md`**.

**Holds only open work:** `[ ]` to-do, `[~]` in progress — **never `[x]`.** When a task ships, a commit
lands (its message references the id), the task **leaves this file**, and a line is added to `FEATURES.md`.
When this board drains, pull the next theme up from `BACKLOG.md`. Board rules live in `CLAUDE.md`
(§ "Status & task board").

Task ids are a section letter + number (`S13`, `B10`, `G15`) — **globally unique and stable** across all
three files. Moving a task between files never changes its id; never renumber or reuse.

## The focus: a map an agent can author, and that a player can win

Sixteen maps were designed by an agent driving the system end to end, and every one of them was built. That
is the result worth reading first — the machinery reaches from a prose description to a loadable world with
no human in the middle. What the boards then showed is where the reach is thinner than it looks:
**`tools/mapgen/review.md`** is the measured record of it, thirty-four `MG` entries in pipeline order, and
**`tools/mapgen/surface.md`** is the map of the documents the tool should have been written against.

The entries stay in `review.md` as evidence, the way `docs/generator/audit.md` holds the generator's; an
entry **leaves it when its fix lands**. What is promoted onto the board below is ordered by the review's own
severity rather than by the pipeline.

**The first six ship a map that cannot be played as intended, and four of those cannot be won at all.**
They come first for that reason and for no other. The two after them are wrong on every board and cheap.
The next two are what a goal needs to be legible and defensible on the ground. The last two are the loop
itself: an agent that cannot see what it built cannot correct it, and one that cannot read what the system
will accept reaches for a random answer where an author would reach for a deliberate one — which is the
single thread running through most of `review.md`.

What is deliberately **not** here: the design entries (a destroy board composed for destroy topology, a
forest placed rather than scattered, per-shape paint, houses that differ from each other). They are the
difference between a rough map and a good one, they are real, and they are a second wave — `review.md`
holds them until one becomes the focus.

## Backend, pipeline & internals (B / P / A)

- [ ] **B80 — A generated map goes out through the export composer** (`review.md` MG14, MG15, MG16, MG17).
  `tools/mapgen` builds its document and calls `XmlWriter.ToXml(Deserializer.FromDict(doc))` directly, so it
  skips `MapXmlComposer.Compose(doc, isIntent: true, …)` and everything the composer applies: `CtwStandards`
  (the keep / repair / remove rules derived from the spawn kit, hunger depletion off, the shared
  golden-apple kill-reward include), `WaterLaneGenerator.EnsureInclude`, `ResourceRenewables.Apply`,
  `StructureRenewables.Apply`, and the reordering that puts the `not-build-area` rule **last**. That
  ordering is load-bearing: PGM stops at the first applicator that decides, and that rule is what holds
  players out of the void, which is the same void B82 refuses a goal over. Measured against the 365 corpus
  capture maps outside the batch, a generated map is missing `<itemremove>` (99% carry it), `<toolrepair>`
  (94%), `<itemkeep>` (81%) and `<hunger><depletion>off` (81%). One call is most of the fix. Two things ride
  with it: everything `CtwStandards` derives sits behind `if (m.Kits.FirstOrDefault() is { } kit)`, so a
  kitless map comes out with no loadout rules and **no warning** — that wants a report line rather than
  silence (MG17); and the generated kit marks its tools `unbreakable="true"`, which leaves `toolrepair`
  nothing to repair, so whether the generated kit keeps `unbreakable` once repair is present is a real
  question to answer **against what the corpus kits do**, not by assumption.

- [ ] **B81 — The kit's pickaxe is paired to the goal it has to break, and a goal nothing can break is
  refused** (`review.md` MG18). The plan compiler defaults a destroyable's material to obsidian and the
  generated spawn kit carries an **iron** pickaxe, which does not mine obsidian slowly — it does not mine it
  at all. Every monument and every core in the seven destroy boards is therefore indestructible and every one
  of those maps is unwinnable. The corpus never breaks the pairing: of 312 destroy maps, **86 name obsidian
  in a goal's materials and all 86 carry a diamond pickaxe**, while 65% of the 226 with a softer goal carry
  one anyway; only 30 carry both a diamond and an iron, so the usual shape is a **substitution** — the
  destroy kit is the capture kit with its pickaxe upgraded, a kit variant rather than a second kit. Two ways
  to be right and both are wanted: derive the pickaxe from the goal material when a destroy map is built, and
  **refuse to write a map whose goal material no tool in its kit can break** — the second is what survives
  someone later choosing a different material. Which is the third half of this: the material is already a
  knob (`PlanModel`'s empty-means-obsidian, `MapIntent`'s destroyable materials, end stone and the softer
  families the corpus uses) and the spec has no word for it, so a destroy map cannot ask for anything but the
  default. Give the spec the word, then pair the kit to whatever it says — for a **destroyable** only, since a
  core has no material field anywhere in the pipeline and DC1 fixes obsidian by design.

  The knob has a landmine under it that the refusal must cover too. `DestroyableMaterials.All` limits the
  **stamped** block to four words — obsidian, emerald block, gold block, ender stone — and silently falls back
  to obsidian for anything else, while `DestroyableGenerator` writes whatever was authored **verbatim** into
  the XML. So any fifth word ships a `<destroyable>` whose declared `materials` matches nothing inside its own
  region: a goal with zero health, silent, and the map builds and loads. That is the same failure class as the
  iron pickaxe and it wants the same answer, so the refusal has two halves — a material no tool in the kit can
  break, and a material the stamper cannot stamp.

- [ ] **B82 — A goal standing over void is refused** (`review.md` MG3). An objective with no ground under it
  cannot be reached or mined: there is nothing to stand on and nothing to break through, so the map is
  unwinnable — and silent, because it builds and loads. This is an invariant rather than a quality note, and
  it belongs in the tool as a **refusal**: a spec that sites a goal off the ground fails to build rather than
  writing a board that cannot be played. The check is cheap, because the rasterized ground is already
  computed before dressing and the goal's anchor either has a column under it or does not.

- [ ] **B83 — The relief leaves the ground a room stands on alone** (`review.md` MG5). The surface is solved
  after the spawn and wool rooms are sited, so a room ends up cut into a slope the relief invented under it.
  Two rules in strength order: the ground a spawn or a wool room stands on is **left out of the solve**, and
  failing that it is **never carved below** the room's floor — a surface solved downward under a stamped room
  leaves the floor cut through or hanging over a hole, which is the version that breaks the map rather than
  merely spoiling it. The machinery exists and is unused: a shape states `relief_scope` — `hold` pins it at
  its own stated top so the surrounding surface is solved *knowing where it has to arrive*, `exclude` keeps
  it out of the solve altogether — and `height_mode` with `skirt` sits a stated platform into the terrain
  rather than on it. Every structural shape `PlanCompiler` projects (`spawn-*`, `wool-*`) is exactly the case
  those words were written for, and none of them sets either.

- [ ] **B84 — A spawn faces the board, not the drop** (`review.md` MG4). The spawn's yaw decides which wall
  its door is cut through, so a spawn on the edge of a piece with its yaw pointing outward puts its only exit
  over the void. The direction is settable and is currently inherited from whatever the compiler fanned; it
  should be chosen against the ground — at the board rather than off it.

- [ ] **B85 — Nothing is placed inside anything else** (`review.md` MG7). Buildings stand inside buildings,
  trees grow inside trees, and trees grow through buildings — not merely ugly, since a structure through a
  room's wall is a hole in it and a tree through a doorway is a blocked route. The village pass keeps
  buildings a margin apart and the forest pass keeps trees a margin from buildings placed before it, but the
  margins are small, the checks are site-against-site rather than footprint-against-footprint, and the
  symmetry fan places an orbit image nothing tested. Overlap has to be decided against the **occupied cells**
  of everything already standing, **after** fanning, rather than against the anchor a prop was requested at.
  B86 is the other half of the same seam and the two want reading together.

- [ ] **B86 — A prop is decided once for its whole orbit** (`review.md` MG26). A mirrored board whose trees
  differ side to side reads as broken however good each side is, and the mechanism is known: `Decorator.Fan`
  loops the symmetry orbit and runs `Seats` per image, so a `continue` on failure drops **that image only**.
  A tree whose mirror lands a block nearer a protected column, or on ground the relief left slightly steeper,
  is built on one side and missing on the other. A prop has to be decided once for the whole orbit — seat
  every image or none — and the same applies to anything else placed per-cell rather than per-orbit.

- [ ] **B87 — A wool-room chest opens into the room** (`review.md` MG20). The chests stamped in a wool room
  are not turned to the room they open into, so some present their back to the player and can only be opened
  from inside the wall. A chest's facing is a block data value resolved against which wall it sits on and
  which way the room is entered — the same question the room's door already answers, which is where the
  answer should come from.

- [ ] **B88 — A destroyable stands on a platform, and the platform is one block thick** (`review.md` MG23).
  A 5×5 bedrock platform **one block thick**, seated one block beneath the ground under each destroyable, so
  the monument cannot be undermined from below and the ground it stands on cannot be mined out from under the
  goal. One block is the whole of it — a thicker slab is a wall growing out of the floor and reads as one.
  Nothing stamps a platform today; `StructureStamper` is where it goes, and `DressingScope` already protects
  the ground under a stamped structure.

- [ ] **B89 — Every goal carries a marker above build height** (`review.md` MG24). A wool room, a destroyable
  and a core each want a mark high above them — a small cube, or a letter picked out in blocks — so a player
  crossing open ground knows where the goal is without a map. It is the cheapest legibility a board can
  carry. **Above build height** is the part that matters: `BuildIntent.MaxHeight` already caps building, so a
  marker placed over it is out of reach and cannot be griefed by construction.

- [ ] **B90 — Every stage answers with a picture.** The system renders itself at every stage and the sixteen
  maps used none of it: they were judged from one top-down at three pixels to the block, at the end, after
  every decision had already been made (`review.md` MG13, MG30). Every fault in that document about
  *appearance* — the rim everywhere, the identical walls, the asymmetric trees, the closed canopy — was
  visible in an image nobody rendered. The working rule is that **a stage that produced something is looked
  at before the next stage consumes it**, and what makes that rule followable is a picture per stage that is
  one call away. Three families already exist and are the material: the thirteen API previews that answer a
  document without building a world (`/terrain/theme-preview`, `/terrain/theme-map-preview`,
  `/terrain/material-preview`, `/terrain/prop-preview`, the room / roof / porch / storey style previews,
  `/themes/preview`, `GET /plans/{id}/svg`, `GET /shapes/probe`); the world read-backs in
  `tools/PgmStudio.RoundTrip` (`--topdown`, `--heightmap`, `--contour`, `--surface`, `--traversability`,
  `--buildings`, `--structures`); and the relief prototype's section and step map. The work is not new
  renderers — it is that each pipeline stage has **one named PNG** an agent can ask for by name, that the
  plan renders as a raster and not only as SVG (the `plan_render` half of `B21`, which is the piece with no
  code behind it), and that `tools/mapgen` emits the set as it builds rather than leaving them to be
  remembered. A rendered board also wants reading from more than one view, since a top-down hides everything
  about the third dimension — whether a drop is walkable, whether a room's floor sits where the relief left
  it, whether a goal has ground under it, which is B82's fault seen rather than asserted.

- [ ] **B92 — A building can be a solid volume, not only a shell.** `HouseStamper` raises walls, a roof and
  their openings, and the volume they enclose is left as air — "fill" appears in the house model only as a
  wall's infill between posts and as the gable's, never as the interior. That makes a building somewhere to
  walk into, which is right for a village and wrong for the other thing a building is good for: a **boundary**.
  A house authored with `RoofForm.Flat` and bedrock courses, stood tall enough to clear `max_build_height`, is
  a tower that divides a board while wearing a skin that reads as architecture rather than as a wall — except
  that it is hollow, so it is enterable and it is not a barrier. One stated material packing the enclosed
  volume is the whole feature, and it wants to be a `HouseStyle` field rather than a stamper flag, so a style
  carries whether it is a place or a mass. Two things to settle while doing it: what happens to a filled
  building's door and windows, which are openings into solid rock and are probably better refused than
  emitted; and whether the fill respects the storey stack, since a tower filled to its top course and a tower
  filled only to its first floor are different buildings. `DressingScope` already protects the ground under a
  stamped building, so nothing downstream needs teaching.

- [ ] **B93 — The traversability stage image asks a different question from the one its name owns.**
  `TraversabilityRender` reads a column as navigable when it has ground and two blocks of headroom, splits
  those into 4-connected components and colours each goal by the component it lands in. Its docstring is
  honest that this is "no build-region awareness, no bridgeable-gap classification" — but that is what makes
  it wrong rather than merely light, because it is blind to the one thing that joins a board together. A
  capture map connects its islands **with build regions**, which is what `ruediger` does, and a **water lane**
  is the staggered form of the same idea: `docs/contracts/water-lanes.md` defines it as a gap between islands
  that becomes **bridgeable** part-way through a match, driven by PGM's void filter reading y=0 live — land
  that opens a late second approach to a tucked-away wool, not water and not a hazard. So a render that stops
  at walkable ground reports a false disconnection on exactly the boards whose connectivity was authored, and
  it reports it under a name this project has already given to the other question: `Analysis.Playability.
  Traversability` asks whether the spawn↔wool chain shares one component over **walkable surface ∪ bridgeable
  buildable**, which is the check the Configure tool runs. `CLAUDE.md`'s naming rule settles which way to
  resolve it — a name must not promise the wrong category — so the render either asks the map's own question
  or gives up the word. Asking it is the smaller change: the renderer already parses the `map.xml` for its
  markers, so the build regions are in reach, and treating a buildable empty column as bridgeable is the whole
  of the difference. A lane that opens at 45 minutes wants distinguishing from ground that is walkable at
  match start, since a board connected only after 45 minutes is not a connected board.

- [ ] **B94 — A `dtcm` core is placed two cells along and may land off the board.** `Retarget` in
  `tools/mapgen` gives a destroy map's core `At = [goal.At[0] + 2, goal.At[1]]` — two cells along the piece
  from the monument, unconditionally and in one direction. Where the monument already sits near its piece's
  edge that offset walks the core off the land, and three of the seven shipped destroy specs (`goldhollow`,
  `mourncrag`, `spinebreak`) are refused by B82's void check for exactly this reason, with the anchor's
  floored column genuinely absent from the rasterized ground rather than off by one. B82 makes the fault loud;
  it does not fix it, so every `dtcm` board this tool has written carried a core that was unreachable whenever
  the offset happened to run outward. The offset wants choosing against the piece rather than stated: toward
  its interior, or along whichever axis has room, with the two structures still close enough to read as one
  place to defend. The three refused specs are the gate — they should build, and their cores should stand on
  ground.

- [ ] **B79 — `map-layers` e2e: the plan editor's Compile button never arrives (13/14).** The suite drives to
  `/maps/{slug}/plan` on the seed's built map, then clicks `button:has-text("Compile")` to check that a
  *rebuild* states the trade before replacing a board someone has worked on. The click times out at 30s and
  the page records `HTTP 422 /api/plan/compile`, so the confirmation half of the suite never runs. **Not a
  regression from the island gate or the author fix**: it reproduces identically at `f42ec58`, the commit
  before either landed, and it is not the corpus either — re-running with `MapsRoots` pointed at an empty
  directory fails the same way, so the 15 generated maps under `CommunityMaps/ctw` are not the cause. It is
  also not contention: it reproduces with the suite run alone and no dev server up. It did pass once, on the
  first post-merge run, which is what makes it worth a task rather than a revert. The 422 is the plan
  **validator** refusing what the page posts, while the same map's stored plan compiles 200 through curl —
  so the first thing to find is what the editor sends that the stored document does not, and the finding
  ids in the 422 body name the rule.

- [ ] **B78 — A grown tree gets taller by not being built.** The dressing pass seats a prop only if *no*
  cell it occupies falls on a protected column, at any height, and a grown crown is wide — so the taller the
  tree, the likelier some leaf clips protection and the whole tree is dropped. Measured over one composed
  board at twenty-four sites: a grown oak lands 590 leaves at height 8, 364 at 12 and **0 at 20**, while a
  template oak on the same sites climbs 1584 → 3424 → 7194. Height is silently inverted — asking for a
  bigger tree empties the forest — and it bites hardest on exactly the boards worth dressing, since
  protection grows with the objectives on them. `tools/mapgen` holds grown trees to 14 to work around it;
  the fix belongs in `Decorator.Seats`. The question to settle first is what protection is *for* here: a
  trunk on a monument is the fault, a canopy overhanging one at y+15 is not obviously anything, so the
  candidate is to test the resting cells against protection and let the crown overhang — which is already
  the rule `Seats` applies to *ground* ("what is above may overhang nothing at all"), just not to
  protection. Gate it on the corpus: a hand-built map's trees do overhang its structures.
