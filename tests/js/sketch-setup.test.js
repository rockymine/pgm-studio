// The sketch's symmetry setup, as the host is told it. The host holds no copy of the setup of its own: the
// bridge announces `OnSetup(mirrorMode, cx, cz)` whenever the document's setup changes, so Info and the Setup
// controls cannot show a mode the document no longer has.
import { test } from "node:test";
import assert from "node:assert/strict";
import { mountSketchBridge } from "./_sketch-bridge-stub.js";

const lastSetup = (board) => board.firedOf("OnSetup").at(-1);

test("a load announces the setup it carries", async () => {
  const board = await mountSketchBridge();
  await board.handle.load({ setup: { mirror_mode: "mirror_x", center: { cx: 8, cz: -4 } }, layers: [] });
  assert.deepEqual(lastSetup(board), ["mirror_x", 8, -4]);
});

test("an edit of the mode or the centre is announced", async () => {
  const board = await mountSketchBridge();
  board.handle.setMode("rot_90");
  assert.deepEqual(lastSetup(board), ["rot_90", 0, 0]);
  board.handle.setCenter(12, 6);
  assert.deepEqual(lastSetup(board), ["rot_90", 12, 6]);
});

test("a setup edit is no step: Identity takes no undo, and no other phase's undo takes it back", async () => {
  const board = await mountSketchBridge();
  board.handle.setPhase("info");
  board.handle.setMode("mirror_z");
  board.handle.setCenter(5, 5);
  assert.deepEqual(board.firedOf("OnHistory").at(-1), [false, false], "Identity's Undo is greyed");

  board.handle.setPhase("draw");
  board.handle.undo();
  assert.deepEqual(lastSetup(board), ["mirror_z", 5, 5], "the setup stands");
});
