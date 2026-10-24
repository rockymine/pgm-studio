// The words a sketch shape states about how it meets the modifier stack and the ground. They mirror the C# sets
// `PgmStudio.Vocabulary.ReliefScopes` and `HeightModes`, which the gate, the rasterizer and the inspector
// spell; tests/js/relief-words.test.js reads those sources and fails when either list differs from them.

/** Whether the modifiers leave a shape's columns out (`hold`) or move the shape with the ground (`ride`). Absent is the default, which they reshape, and has no word. */
export const RELIEF_SCOPES = ["hold", "ride"];

/** How a shape's top is decided against the ground it stands on. Absent is ordinary ground. */
export const HEIGHT_MODES = ["level", "raise", "sink", "drape"];

export const isReliefScope = (word) => RELIEF_SCOPES.includes(word);
export const isHeightMode = (word) => HEIGHT_MODES.includes(word);
