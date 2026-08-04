/**
 * Dressing model shared by the sketch tool's Dressing phase (decoration.md). A dressing is the wire JSON the
 * pass (`DressingJson`/`DressingRecipe`) deserialises — no second model — so this holds only what a new
 * dressing starts as. Pure, no DOM.
 */

/** What a freshly defined dressing grows: a light meadow and nothing else.
 *
 *  Flora alone on purpose. It is the part that reads immediately at any density, it is entirely cosmetic so
 *  nothing about the map's fairness changes the moment a dressing is defined, and it is the cheapest to
 *  undo — an author who wants rock and trees turns them on, rather than having to turn off a forest they
 *  never asked for. The knobs mirror `FloraSpec`'s own defaults so the form and the pass agree. */
export function defaultDressingJson() {
  return {
    flora: {
      coverage: 0.45,
      scale: 12,
      octaves: 3,
      fernShare: 0.25,
      flowerShare: 0.18,
      flowerScale: 18,
      tallShare: 0,
      seed: 7,
    },
  };
}

/** A fresh boulder field, for the moment an author switches rock on. */
export function defaultBoulderJson() {
  return { density: 0.35, sizeSpread: 0.5, form: "round", blockId: 1, blockData: 0, mossy: true, seed: 17 };
}

/** A fresh stand of trees — grove-clumped by default, since an evenly spaced forest reads as an orchard. */
export function defaultTreeJson() {
  return { density: 0.3, groveScale: 28, groveThreshold: 0.45, sizeSpread: 0.4, species: [], seed: 23 };
}
