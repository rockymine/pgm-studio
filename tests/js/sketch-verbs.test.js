// The sketch bridge's verb table: which verbs are an undo step, which are refused while the page is read-only,
// and which the host never calls. The claim under test is that the table is the one place an edit is gated, so
// a verb in it behaves the same whichever phase called it.
import { test } from "node:test";
import assert from "node:assert/strict";
import { mountSketchBridge } from "./_sketch-bridge-stub.js";

const RECIPE = JSON.stringify({ shape: "gable" });

async function boardWithTree() {
  const mounted = await mountSketchBridge();
  mounted.handle.load({ dressing: { props: [{ kind: "tree", id: "d1", x: 1, z: 2 }] }, layers: [] });
  mounted.fired.length = 0;
  return mounted;
}

test("a recipe pulled into a board that has placements is one undo step", async () => {
  const { handle, firedOf } = await boardWithTree();
  const key = handle.pullRecipe("tower", RECIPE);
  assert.equal(key, "tower");
  assert.ok(firedOf("OnDirty").length > 0, "the board is dirty");
  assert.deepEqual(firedOf("OnHistory").at(-1), [true, false], "and the pull can be taken back");

  handle.undo();
  assert.equal(handle.getState().dressing.styles, undefined, "undo takes the recipe out of the registry");
});

test("while the page is read-only no verb that edits the document runs", async () => {
  const { handle, fired, firedOf } = await boardWithTree();
  handle.setReadOnly(true);
  fired.length = 0;

  assert.equal(handle.pullRecipe("tower", RECIPE), undefined);
  handle.setMode("mirror_x");
  handle.setCenter(3, 3);
  handle.addLayer();
  handle.defineTheme("rock");

  assert.deepEqual(firedOf("OnSetup"), [], "the setup was not touched");
  assert.deepEqual(firedOf("OnDirty"), [], "nothing was marked dirty");
  assert.deepEqual(firedOf("OnHistory"), [], "and no step was taken");
  const state = handle.getState();
  assert.equal(state.setup.mirror_mode, "rot_180");
  assert.equal(state.layers.length, 1);
  assert.equal(state.themes, undefined);
  assert.equal(state.dressing.styles, undefined);

  handle.setReadOnly(false);
  assert.equal(handle.pullRecipe("tower", RECIPE), "tower", "the same verb runs once the page may write");
});

test("a read verb is not wrapped as a step, and reads while read-only", async () => {
  const { handle, firedOf } = await boardWithTree();
  handle.setReadOnly(true);
  assert.deepEqual(JSON.parse(handle.getBiome()), { field: null, source: 0 });
  assert.equal(handle.themeFromLibrary(7), "");
  assert.deepEqual(firedOf("OnHistory"), []);
});

test("the verbs nothing calls are not on the handle", async () => {
  const { handle } = await mountSketchBridge();
  for (const verb of ["getDressing", "getRelief", "groupCount", "renameTheme", "resize", "setBbox"])
    assert.equal(handle[verb], undefined, verb);
});
