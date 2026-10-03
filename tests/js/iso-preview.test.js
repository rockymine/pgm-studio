import { test } from "node:test";
import assert from "node:assert/strict";
import { isoPreview } from "../../src/PgmStudio.Client/wwwroot/js/studio/bridge/iso-preview.js";

// The 3-D preview a canvas enters and fills from its columns route. The route is stated either as a value
// (the plan's fixed route) or as a function (the sketch's, which depends on the map), and both are asked.

function fakeCanvas() {
  return { entered: 0, hidden: 0, enterIso() { this.entered++; return true; }, hideIso() { this.hidden++; }, drawIso() {} };
}

async function showWith(url) {
  const asked = [];
  const original = globalThis.fetch;
  globalThis.fetch = async (target) => { asked.push(target); return { ok: false, status: 422, json: async () => ({}), text: async () => "{}" }; };
  const unavailable = [];
  try {
    const iso = isoPreview({ canvas: fakeCanvas(), url, missing: "no map", state: () => "{}", bounds: () => null,
                             onUnavailable: (reason) => unavailable.push(reason) });
    await iso.show();
  } finally { globalThis.fetch = original; }
  return { asked, unavailable };
}

test("a route stated as a value is asked", async () => {
  const { asked } = await showWith("/api/plan/columns");
  assert.deepEqual(asked, ["/api/plan/columns"]);
});

test("a route stated as a function is asked", async () => {
  const { asked } = await showWith(() => "/api/map/a/sketch/columns");
  assert.deepEqual(asked, ["/api/map/a/sketch/columns"]);
});

test("an empty route leaves the preview with the missing sentence and asks nothing", async () => {
  const { asked, unavailable } = await showWith(() => null);
  assert.deepEqual(asked, []);
  assert.deepEqual(unavailable, ["no map"]);
});
