// The words a sketch shape states about how it meets its group's relief. They mirror the C# sets
// `PgmStudio.Vocabulary.ReliefScopes` and `HeightModes`, which the gate, the rasterizer and the inspector
// spell; tests/js/relief-words.test.js reads those sources and fails when either list differs from them.

/** How a shape's ground joins the relief its group is solved over. Absent is the default and has no word. */
export const RELIEF_SCOPES = ["follow", "hold", "exclude"];

/** How a shape's top is decided once its group carries a relief. Absent is ordinary ground. */
export const HEIGHT_MODES = ["level", "raise", "sink", "drape"];

export const isReliefScope = (word) => RELIEF_SCOPES.includes(word);
export const isHeightMode = (word) => HEIGHT_MODES.includes(word);
