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

