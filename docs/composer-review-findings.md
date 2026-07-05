# Composer review findings — build-zone round (B2 gallery)

Author's visual review of the 17 generated maps in the B2 gallery (seed ids
`gen-p{P}-t{T}-{sym}-s{seed}`). Overall verdict: *"does not look too terrible … nothing
that is too breaking."* Recorded here as **notes for later fixes + teaching sets** — **not
yet rules**. A deeper author sample set is coming; these become `layout-rules.md` amendments
only after the teaching material lands (the CT8 / BZ6–BZ9 path).

## Solid (keep)
- **Mid-band clearly improved.**
- For mid-lane gaps the build zone **consistently docks, does not overlap**.
- **No mid zones overflow into the void.** *"That work is solid."*
- **Positive wool-lane — `gen-p30-t2-rot_180-s7`, `wool-lane-a-*`:** a clean **Z shape** with a
  small build region inside it. *"The first twist is important to not have it too far out of the
  map; the second twist still offsets it a little bit — that is a great example."* Future addition
  (author will show in a later seed): a **second bridge connection to `wool-lane-a-2`**, parallel
  to the bridge region.

## Issues to fix later (none breaking)
1. **Mid-band asymmetry under rot_180** — *possible correctness bug; investigate first.* Some maps
   render as if the mid-band is asymmetric: on one team it overlaps the frontline, on the other it
   does not — **`gen-p30-t2-rot_180-s7`**, **`gen-p30-t2-rot_180-s13`**. On
   **`gen-p20-t2-rot_180-s7`** it overlaps the frontline on **both** sides. rot_180 must be
   symmetric, so first verify whether the band rect is genuinely off-centre or it is a render
   artefact.
2. **Spawn↔wool / wool↔wool lane spacing too tight** — e.g. the whole **`gen-p30-t2-rot_180-s7`**
   family: spawn and wool lanes run tight next to one another. The **buffer annotation** (buffer
   tile) on the 12 seeds will help mark intended spacing.
3. **High-budget maps always force 3 wool lanes** → *"super long L or I shaped extrusions of the
   hub"* (p30 family). The composer does **not fold** lanes or add **alternate attack routes**
   (additional rings with holes) the way the corpus does. Fold long lanes / add ringed alternates
   rather than extruding.
4. **Uniform structure — one square hub.** Every map is frontline → **one square hub**, with every
   spawn/wool lane attaching to that single hub. Corpus hubs are *"shaped differently, made of more
   pieces sometimes."* Reduction to lanes + frontlines + hub is fine and expected, but the **hub
   shape should vary**.
5. **Spawn-lane over-growth** — **`gen-p30-t2-rot_180-s13`**: the spawn lane is a *"really really
   large L, it grew WAY too much."* Needs a growth cap (cf. the LN2 chain cap, applied to spawn
   lanes).
6. **Mid as a 1-D chain funnels everything through one gap** — **`gen-p30-t2-rot_180-s1`**: the full
   mid is **4 vertically-aligned stepping stones + one thin frontline**; *"EVERYTHING has to FUNNEL
   THROUGH that SMALL GAP."* Direction:
   - A **double frontline** helps and lets the stones sit in a **2×2 grid**.
   - IF a single frontline piece, it must be **wider** and the build zone should **dock to it
     fully**.
   - The centre *"should never be a chain of islands into one direction — players need to be able to
     pick their way."* A **grid-based** stepping-stone setup is good for that.
   - On a wide frontline, stepping-stone edges **align with the build-zone border** (or inset
     slightly).
7. **Always double-frontline now** — *"a bit boring, but okay."* Add variety. Author will provide
   work-around examples.

## Teaching sets the author will author next (see the shopping list)
- **Frontlines** — from #6/#7 (wide vs double frontline; grid stepping stones; docking).
- **Buffer / intentional-gap** — from #2 + the rot_90 centre buffer / border-reservation.

Home + running list: `tools/seeds/teaching/SHOPPING-LIST.md`.
