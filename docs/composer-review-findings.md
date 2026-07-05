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


Author remarks (05/07/2026):

I have created frontline / mid examples for you that are applicable to rot-180 and mirror-x and mirror-z modes. For the plan I deliberately chose Mirror Z as that let me lay out the samples neatly side by side. I modeled flush with the mirror line, only ever on one side of it. I labeled the pieces and build zones of each example with proper ids. I used <type>-<example-number><optional: letter> as the labeling system. For the types I used step, frontline, hub, band, and bridge: frontline-0 and step-0a. Hub connects to frontlines, frontlines connect to bands and bridges (build zones), and steps are inside bands or touch bridges. I am going to describe the examples by their number now:

frontline-dos-and-donts-rot-180-mirror:

Examples 0, 1, and 12 are a NEGATIVE smell seen inside the generated seeds. It's the long thin band connecting to a often times thin frontline. 

Examples 2, 3, and 4 show examples of wide frontlines. example 2 and 4 show multiple stepping stones sitting in the zone, parallel to each other. example 3 shows a large stepping stone sitting inside the band, with slight padding, while still keeping the band aligned with the corners of the frontline piece.

Thus far the examples described all have wide frontline with islands that enable rotation. The generated seeds display a widespread use of double frontlines.

Example 6 is the most common example in the set: A split frontline connected to a hub where the band sits flush with the two tips of the frontline pieces. Example 13 is a variation where the band contains steps. There are multiple variations that would make more interesting and better playing frontlines.

Example 7 showcases how the two frontlines can have their individual band that then aligns with the opposing team's frontlines. This needs to be used carefully and the frontline-band-groups cannot be too long as otherwise these would be the negative examples 0, 1, and 12 again.

We can improve these parallel bands by adding in another bridge-region bordering the frontline as example 8 shows (and still leaving a gap / buffer to the hub creating a hole for rotation). Example 9 shows how adding a stepping stone between the parallel bands connects them to offer a rotation as well. Example 10 shows that we can also place stepping stones inside the middle of each band (and optionally connect these stepping stones with a bridge-region).

Example 11 shows how the frontline and hub can form an L as well and only one band holds a stepping stone. 

I did also author some real interesting variations in Examples 14 that extend the L idea of Example 11 into a Z-tetris-piece. hub and frontline form the Z. hub and frontline have their own parallel running and non-touching bands with stepping stones. the frontline also connects to another piece forming a rotation point. Example 15 shows essentially the same concept but simplified a little bit with fewer stones and a bridge connection to a different part of the map.

As you can see there are a lot of options.



