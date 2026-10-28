// The sketch bridge's undo over play pieces: a piece write is one step, kept once the studio has answered it, and
// an undo pressed before that is kept waits for it, so it takes back the write just made and not the one before.
import { test } from "node:test";
import assert from "node:assert/strict";
import { mountSketchBridge } from "./_sketch-bridge-stub.js";

/** A stand-in studio holding one map's intent as text: a GET reads it, a PUT replaces it, every other route
 *  answers an empty list. */
function studio(intent) {
  const held = { intent };
  globalThis.fetch = async (url, init = {}) => {
    const method = init.method ?? "GET";
    if (url.endsWith("/intent") && method === "PUT") held.intent = init.body;
    const body = url.endsWith("/intent") ? held.intent : "[]";
    return { ok: true, status: 200, text: async () => body, json: async () => JSON.parse(body) };
  };
  return held;
}

test("an undo pressed while a piece write is open takes back that write, not the one before it", async () => {
  const held = studio('{"pieces":"none"}');
  const { handle } = await mountSketchBridge({ slug: "m" });

  await handle.beginPieceStep();
  held.intent = '{"pieces":"spawn"}';
  await handle.endPieceStep();

  await handle.beginPieceStep();
  held.intent = '{"pieces":"spawn and wool room"}';
  const undoing = handle.undo();
  await handle.endPieceStep();
  await undoing;

  assert.equal(held.intent, '{"pieces":"spawn"}', "the wool room is taken back and the spawn stays");

  await handle.redo();
  assert.equal(held.intent, '{"pieces":"spawn and wool room"}', "and redo puts the wool room back");
});

test("two piece writes made close together are two steps, and one undo takes back only the second", async () => {
  const held = studio('{"pieces":"none"}');
  const { handle } = await mountSketchBridge({ slug: "m" });

  await handle.beginPieceStep();
  held.intent = '{"pieces":"spawn"}';
  const second = handle.beginPieceStep();     // the wool room's write starts before the spawn's step has closed
  await handle.endPieceStep();
  await second;
  held.intent = '{"pieces":"spawn and wool room"}';
  await handle.endPieceStep();

  await handle.undo();
  assert.equal(held.intent, '{"pieces":"spawn"}', "the wool room alone is taken back");
  await handle.undo();
  assert.equal(held.intent, '{"pieces":"none"}', "and a second undo takes the spawn");
});
