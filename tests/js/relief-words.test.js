// The relief words the sketch bridge validates against, held against the C# sets they mirror.
// `ReliefScopes` and `HeightModes` decide what the server and the inspector accept and `relief-words.js` decides
// what the bridge writes into the document, so a word added on one side only is a shape silently reset to
// its default on the other. This reads the C# constants rather than restating them.
import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

import { RELIEF_SCOPES, HEIGHT_MODES, isReliefScope, isHeightMode }
  from "../../src/PgmStudio.Client/wwwroot/js/studio/shared/relief-words.js";

/** The `public const string` values of a vocabulary class, in declaration order. */
function wordsOf(path) {
  const cs = readFileSync(path, "utf8");
  return [...cs.matchAll(/public const string \w+\s*=\s*"([^"]+)"/g)].map((match) => match[1]);
}

test("RELIEF_SCOPES is ReliefScopes.All", () => {
  const words = wordsOf("src/PgmStudio.Vocabulary/ReliefScopes.cs");
  assert.ok(words.length > 0, "no constants found in ReliefScopes.cs");
  assert.deepEqual(RELIEF_SCOPES, words);
});

test("HEIGHT_MODES is HeightModes.All", () => {
  const words = wordsOf("src/PgmStudio.Vocabulary/HeightModes.cs");
  assert.ok(words.length > 0, "no constants found in HeightModes.cs");
  assert.deepEqual(HEIGHT_MODES, words);
});

test("every stated scope is accepted, including follow, and nothing else is", () => {
  for (const word of ["follow", "hold", "exclude"]) assert.ok(isReliefScope(word), word);
  for (const word of ["", "inherit", "Hold", null, undefined]) assert.ok(!isReliefScope(word), String(word));
});

test("every height mode is accepted and nothing else is", () => {
  for (const word of ["level", "raise", "sink", "drape"]) assert.ok(isHeightMode(word), word);
  for (const word of ["", "flat", "Level", null, undefined]) assert.ok(!isHeightMode(word), String(word));
});
