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

**The eight that broke a map have shipped**, along with the platform and the marker a goal wants, a picture
per stage and the handbook (`FEATURES.md`; `review.md` records which entry each closed). What is left below
is the residue that work turned up, and the shape of it is worth noticing: every one was found by a fix
rather than by the original review. A refusal caught three shipped specs whose cores stand in void. A stage
image turned out to answer a different question from the one its name owns, and to hand a reader a wrong
answer through an unlabelled colour. A density threshold turned out to be stated in the wrong unit. And a
building turned out to be the one composition the system cannot express.

What is deliberately **not** here: the design entries (a destroy board composed for destroy topology, a
forest placed rather than scattered, per-shape paint, houses that differ from each other). They are the
difference between a rough map and a good one, they are real, and they are a second wave — `review.md`
holds them until one becomes the focus.

## Backend, pipeline & internals (B / P / A)

- [ ] **B92 — A building can be a solid volume behind its own facade.** `HouseStamper` raises walls, a roof
  and their openings, and the volume they enclose is left as air — "fill" appears in the house model only as a
  wall's infill between posts and as the gable's, never as the interior. That makes every building somewhere
  to walk into, which is right for a village and wrong for the two things a building is also good for: a
  **scenery building inside the map that is not enterable**, and a **run of buildings sealing the edge of the
  board**, which is how scenery does the work of a boundary — and the only way it can do that work at all in a
  mode where nothing may be placed.

  **The facade is kept, and that is the whole trick.** A filled building is not a solid block wearing a
  house's outline: its windows and its door stay exactly where they were, because they are what makes it read
  as a building rather than as a lump, and the fill sits **behind** them. The idiom is a dark fill — black
  wool being the obvious one — so a window reads as an unlit interior rather than as a hole into rock, which
  is a house with its lights off and is what an eye expects at the edge of a map. So the fill material is a
  knob rather than a constant, and the openings are untouched by it.

  It wants to be a `HouseStyle` field rather than a stamper flag, so a style carries whether it is a place or
  a mass. What still has to be settled: whether the fill respects the storey stack, since a building filled to
  its top course and one filled only to its first floor are different buildings; and how deep behind a door or
  window the fill starts, since flush against the opening and one course back read differently through the
  gap. `DressingScope` already protects the ground under a stamped building, so nothing downstream needs
  teaching.

- [ ] **B95 — A stage image has no key, and its colours are read as materials.** The plan render colours by
  **role** — hub violet, spawn green, wool amber, frontline orange, anything else slate, and a zone in blue
  (`#38bdf8` for a build zone, `#2563eb` for a water lane, separated only by shade, opacity and dash pattern).
  Nothing on the image says any of that. Blue is the universal visual code for water, so a reader who has the
  picture and not the key is handed a wrong answer rather than no answer: a generated board's central build
  zone was read as water on a map that carries none, and the misreading was then used to explain away a
  connectivity result. An image that invites a confident wrong reading is worse than one that is merely
  unclear, and the pictures exist precisely so that things get read off them.

  Two fixes, and the second matters more than the first. **A legend on the image** — the role swatches and the
  two zone kinds named on the plan render, and a scale on every world read-back — costs little and removes the
  guess. **A distinction that survives being looked at**: a build zone and a water lane are the same gap with
  different crossing rules, one open from the first minute and one opening part-way through a match, and
  encoding a difference that large as two shades of blue means it does not survive the render. Hatching, a
  label, or an outright different hue would.

  The general rule this teaches belongs beside the images in `surface.md`: **an image is a check, not a
  source of meaning.** A render answers "did the thing that was authored actually come out", and the document
  underneath answers "what is it". Reading semantics off pixels is how a build zone becomes water.

- [ ] **B96 — Density wants measuring as canopy share, not as a leaf count.** The leaf count is the only
  honest measure of *whether a forest was planted* — nothing but a tree lays a leaf, and a building's corner
  posts are logs — but it is a poor measure of **how wooded a board reads**, and two measurements on one board
  size prove it: a spruce forest at 17,600 leaves over many sites rendered as one solid mass with the routes
  buried, while `thornwake` at 17,897 leaves over 72 trees renders as a wood a player walks through. Nearly
  the same count, opposite maps, because the leaves are divided among a tenth as many trees. The number that
  would decide it is the share of ground columns standing under a leaf, which is a cheap read over the same
  voxels the census already walks, and it is scale-free in a way a raw count is not — a 120×240 board and a
  240×240 board do not want the same leaf count to read the same. Report it beside the leaf count and give the
  README a band in those terms; the two numbers disagreeing is itself informative, since a high count at a low
  share is a few enormous trees and a low count at a high share is scrub.

- [ ] **B97 — Leaves may lie against a building and never inside it.** A prop already writes only into air —
  `Decorator` skips any unburied cell whose target is not `Blocks.Air`, so a tree can never replace a wall, a
  roof or a post, and a canopy resting against a house is correct and wanted. What that mask does not catch is
  the **enclosed volume**: a building's interior is air, so a crown overhanging a roof drops leaves through it
  into the room below, and the room is then a room with a tree in it. The authoring convention this is
  imitating is exact — a map author pastes a tree beside a house masked against the house's own blocks and
  then **removes the leaves that landed inside it**, so the building stays empty. Only the second half is
  missing here.

  So the rule is three-way rather than two-way, and `B85` implemented only two thirds of it: a prop may not
  **root** inside a structure (done), a prop may not **replace** a structure's blocks (done), and a prop's
  cells falling in a structure's **enclosed interior** are dropped rather than written. The last wants the
  volume a stamped building encloses, which `HouseStamper` knows at stamp time and nothing records afterwards
  — recording it is most of the work, and `DressingScope` is where a stamped thing's extent already lives.
  Worth doing with `B92`, which fills that same volume with a stated material and therefore has to describe it
  anyway.

- [ ] **B98 — A stage image is a diagram, not a photograph.** The renders imitate what a map looks like —
  grass green, leaves green, stone grey — and that is the one thing an image for reasoning must not do,
  because a green tree on green ground is invisible in it. Legibility beats realism here: a reader, human or
  model, needs to tell foliage from surface from structure at a glance, and the surest way is deliberate
  false colour that no terrain would ever wear. Foliage in a vibrant purple against a muted ground says more
  in one look than an accurate render says in ten.

  Two things follow, and the second is the larger. **Contrast is the requirement**, so every render picks its
  palette to separate the categories it is drawing rather than to depict them — the same reasoning `B95`
  applies to the plan's role colours, applied to the world read-backs. And **a layer is worth seeing alone**:
  the top-down today is the finished rasterized map with everything on it at once, including things a given
  question does not want — the redstone lines marking a build region's edge in red, the observer platform
  floating over the middle — so the reading is a search rather than a look. One image per layer (ground ·
  relief · paint · structures · foliage · buildings · objectives), each in its own high-contrast scheme, plus
  the combined view that already exists, makes each pass answerable on its own, which is what a pipeline that
  is meant to be reviewed between stages actually needs. `B90` built the set; this is what makes it readable.

- [ ] **B99 — A composed wool room can sit off the board's own navigable component.** Made visible the moment
  three `dtcm` specs built for the first time (`B94`): `goldhollow` and `spinebreak` render several objective
  markers isolated — four and eight — and the cause is not a goal standing over void, which `B82` already
  refuses, but rooms that have real ground and no walkable join to the main component. A goal a player cannot
  reach is unwinnable in the same way as one that cannot be mined, and it went unseen until the void refusal
  stopped hiding it behind an earlier failure. The read that shows it now exists (`B93`'s bridgeable-void
  traversability), so the first move is to run it over every shipped spec and find how wide the fault is,
  rather than assuming it is those two. Whether the fix belongs in the composer's seating or in the compiler's
  build regions is the question that measurement answers: a room joined only by a build region is connected
  and reads as connected, so a genuinely isolated one means neither ground nor buildable ground reaches it.

- [ ] **B100 — A destroy board carries one or two goals a team, not one per composed slot.** `Retarget`
  turns **every** goal the composer sited into a destroyable and, for `dtcm`, adds a core beside each — so a
  two-wool-a-side board becomes four monuments and four cores, eight objectives, which is past the top of the
  corpus distribution entirely. Measured over the 127 corpus maps carrying a destroy objective, per team:
  **one monument in 59 maps (55%), two in 40 (37%), three in 5 (5%)** and four or more in four outliers;
  **one core in 24 (77%), two in 6 (19%), three in exactly one map in the corpus.** Of the 17 carrying both,
  **16 have a single core a team**, and the common combined shape is one monument and one core. So the count
  is a design decision with a narrow real range, and it is currently a side effect of how many wools the
  budget happened to place.

  Count is not the whole of it, because the goals are **sited relative to each other** rather than scattered:
  where a board carries several they are deliberately spaced and named by where they stand — a west and an
  east monument, or two forward and one back near the spawn, or two back and one forward. That spacing is the
  board's shape, since each goal is a place a team must hold and their arrangement decides whether a defence
  is one line or three. A core is rarer than a monument for a reason worth carrying into the choice: leaking
  one is a harder, longer job, so a board wants fewer of them. Pair this with `MG2` — the areas those goals
  stand in are themed apart in the corpus, so the ground around a west monument reads differently from the
  ground around an east one, and that distinction is expressible today and was used on none of the sixteen.

  One naming fix belongs with it, because the confusion is already in the code. In-game the mode is
  *destroy the monument*, so a `<destroyable>` is colloquially a monument — but **`monument` is already taken**
  in this codebase and in PGM, for the block a wool is placed on in a capture map. `Retarget` nonetheless
  names its destroyables `monument-0`, `monument-1`, which is the one word that means the other thing.
  `CLAUDE.md`'s naming rule applies directly: a name must not promise the wrong category.

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

