// The terrain glyphs the Terraform dock names, held against the C# that names them. `SketchModifierState.Icon`
// spells an icon per operation, `terrain-icons.js` is where those names are drawn, and `MODIFIER_OPS` is the
// board's list of operations; an operation that one of them lacks is a dock button with no glyph, a blank
// placeholder, or a tool the board cannot trace.
import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

import { MODIFIER_OPS } from "../../src/PgmStudio.Client/wwwroot/js/studio/relief/modifier-doc.js";

const iconsSource = readFileSync("src/PgmStudio.Client/wwwroot/js/studio/icons/terrain-icons.js", "utf8");

/** What the script registers, run against a stand-in for the lucide global the vendored script sets. */
function registered(preloaded = {}) {
  const errors = [];
  const original = console.error;
  console.error = (message) => errors.push(message);
  const lucide = { icons: { ...preloaded } };
  try { new Function("window", iconsSource)({ lucide }); } finally { console.error = original; }
  return { icons: lucide.icons, errors };
}

/** The operation words of `ModifierOps`, in declaration order. */
function modifierOpWords() {
  const cs = readFileSync("src/PgmStudio.Vocabulary/ModifierWords.cs", "utf8");
  const block = cs.slice(cs.indexOf("class ModifierOps"), cs.indexOf("class ModifierModes"));
  return [...block.matchAll(/public const string \w+\s*=\s*"([^"]+)"/g)].map((match) => match[1]);
}

/** `op => icon` out of `SketchModifierState.Icon`. */
function iconsByOp() {
  const cs = readFileSync("src/PgmStudio.Client/Features/Sketch/SketchModifierState.cs", "utf8");
  const block = cs.slice(cs.indexOf("public static string Icon"), cs.indexOf("public static string Blurb"));
  return Object.fromEntries([...block.matchAll(/ModifierOps\.(\w+) => "([^"]+)"/g)].map((match) => [match[1], match[2]]));
}

test("every glyph is an svg node list on lucide's grid, and registers beside the vendored set", () => {
  const { icons, errors } = registered({ circle: [] });
  assert.deepEqual(errors, []);
  const names = Object.keys(icons).filter((name) => name.startsWith("terrain-"));
  assert.ok(names.length >= 16);
  for (const name of names) {
    assert.ok(Array.isArray(icons[name]) && icons[name].length > 0, name);
    for (const [tag, attrs] of icons[name]) assert.ok(typeof tag === "string" && typeof attrs === "object", name);
  }
});

test("a name lucide already defines is reported, not replaced", () => {
  const taken = [["path", { d: "M0 0" }]];
  const { icons, errors } = registered({ "terrain-hill": taken });
  assert.equal(icons["terrain-hill"], taken);
  assert.equal(errors.length, 1);
});

test("the board's operations are the operation words the C# spells", () => {
  const words = modifierOpWords();
  assert.deepEqual([...MODIFIER_OPS.map((entry) => entry.op)].sort(), [...words].sort());
});

test("every operation has a glyph that is drawn", () => {
  const { icons } = registered();
  const byOp = iconsByOp();
  assert.equal(Object.keys(byOp).length, modifierOpWords().length, "an Icon case per operation");
  for (const [op, name] of Object.entries(byOp)) assert.ok(icons[name], `${op} -> ${name}`);
  assert.equal(new Set(Object.values(byOp)).size, Object.keys(byOp).length, "no two operations share a glyph");
});
