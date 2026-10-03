// The Theme phase's finish — the registry, the assignments, the room shells and the biome — reaches the host as
// one announcement, on every change of it: an edit, a load, and a step taken back.
import { test } from "node:test";
import assert from "node:assert/strict";
import { mountSketchBridge } from "./_sketch-bridge-stub.js";

const lastFinish = (firedOf) => JSON.parse(firedOf("OnThemes").at(-1)[0]);

test("a load announces the finish it holds, room shells and biome included", async () => {
  const { handle, firedOf } = await mountSketchBridge();
  handle.load({
    themes: { rock: { name: "rock" } },
    roomStyles: { wool: { shell: "tower" }, spawn: null },
    biome: { kind: "flat" }, biomeSource: 4,
    layers: [],
  });
  const finish = lastFinish(firedOf);
  assert.deepEqual(Object.keys(finish.themes), ["rock"]);
  assert.deepEqual(finish.roomStyles, { wool: { shell: "tower" }, spawn: null });
  assert.deepEqual(finish.biome, { field: { kind: "flat" }, source: 4 });
});

test("binding a room shell or a biome announces the finish", async () => {
  const { handle, firedOf, fired } = await mountSketchBridge();
  fired.length = 0;
  handle.setRoomStyle("wool", JSON.stringify({ shell: "tower" }));
  assert.deepEqual(lastFinish(firedOf).roomStyles, { wool: { shell: "tower" } });
  handle.setRoomStyle("spawn", "null");
  assert.deepEqual(lastFinish(firedOf).roomStyles, { wool: { shell: "tower" }, spawn: null });
  handle.setBiome(JSON.stringify({ kind: "flat" }), 9);
  assert.deepEqual(lastFinish(firedOf).biome, { field: { kind: "flat" }, source: 9 });
});

test("a step taken back announces the finish it restored", async () => {
  const { handle, firedOf, fired } = await mountSketchBridge();
  const id = handle.defineTheme("rock");
  handle.setRoomStyle("wool", JSON.stringify({ shell: "tower" }));
  fired.length = 0;

  handle.undo();
  assert.deepEqual(lastFinish(firedOf).roomStyles, {}, "the room shell is not left standing");
  handle.undo();
  assert.deepEqual(lastFinish(firedOf).themes, {}, `${id} is gone from the announced registry`);
});
