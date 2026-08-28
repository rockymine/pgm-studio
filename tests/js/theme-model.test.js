// The client's statement of unthemed ground. It has a C# twin — `TerrainTheme`'s own field defaults — and the
// two describe the same terrain, so what is asserted here is the property that makes them one statement
// rather than two: every bucket is stone, and nothing in the shape carries an opinion about a finish.
import { test } from "node:test";
import assert from "node:assert/strict";

import { defaultThemeJson, uniqueScopeId }
  from "../../src/PgmStudio.Client/wwwroot/js/studio/theme/theme-model.js";

const STONE = 1;

// The four themeable buckets, as the painter's wire shape spells them: the two top-claiming ones are band
// objects carrying a material, and the other two are bare materials.
const materialOf = (theme, bucket) =>
  bucket === "rim" || bucket === "surface" ? theme[bucket].material : theme[bucket];

test("every bucket of a fresh theme is stone", () => {
  const theme = defaultThemeJson();
  for (const bucket of ["rim", "surface", "wall", "fill"]) {
    const material = materialOf(theme, bucket);
    assert.equal(material.kind, "solid", `${bucket} is a plain block, not a pattern`);
    assert.equal(material.id, STONE, `${bucket} is stone`);
  }
});

test("no bucket carries a finish — no stack, no tint, no data value", () => {
  const theme = defaultThemeJson();
  const json = JSON.stringify(theme);
  for (const opinion of ["layered", "teamTint", "voronoi", "noise", "field"]) {
    assert.ok(!json.includes(opinion), `an unthemed default states no ${opinion}`);
  }
  // A data value is how a shade is chosen out of a block family, which is a finish by another name.
  for (const bucket of ["rim", "surface", "wall", "fill"]) {
    assert.equal(materialOf(theme, bucket).data, undefined, `${bucket} names no shade`);
  }
});

test("the geometry knobs still hold — stone is a material answer, not a switched-off theme", () => {
  const theme = defaultThemeJson();
  assert.equal(theme.rimEdges, "drop", "a rim still runs where the ground falls away");
  assert.equal(theme.wallOnTerrainFaces, true, "a wall still covers a terrain riser");
  assert.equal(theme.wallEnabled, true);
  assert.equal(theme.rim.enabled, true);
  assert.equal(theme.surface.enabled, true);
  assert.deepEqual(theme.bedrock, { relative: false, value: 1 });
});

test("each call answers a fresh object, so editing one theme cannot move another", () => {
  const first = defaultThemeJson();
  first.fill.id = 24;
  assert.equal(defaultThemeJson().fill.id, STONE);
});

// ── the id uniquifier the registry names themes through ───────────────────────
test("a name is slugged, and a taken slug is numbered rather than replaced", () => {
  assert.equal(uniqueScopeId([], "Ash Fall"), "ash-fall");
  assert.equal(uniqueScopeId(["ash-fall"], "Ash Fall"), "ash-fall-2");
  assert.equal(uniqueScopeId(["ash-fall", "ash-fall-2"], "ash fall"), "ash-fall-3");
});

test("a name with nothing sluggable in it still yields an id", () => {
  assert.equal(uniqueScopeId([], "   "), "scope");
  assert.equal(uniqueScopeId([], "!!!"), "scope");
  assert.equal(uniqueScopeId(["scope"], ""), "scope-2");
});
