// The Terraform phase's reads of the ground at a step: the step strip sets which ground the contours and the 3-D
// picture are read from, and the bridge asks for what each modifier changed. What must hold is that the reads
// carry the step the strip stands at, that a step past the last modifier is no step, and that a server which
// does not answer the change counts leaves them out rather than breaking the phase.
import { test, afterEach } from "node:test";
import assert from "node:assert/strict";
import { mountSketchBridge } from "./_sketch-bridge-stub.js";
import { newModifier } from "../../src/PgmStudio.Client/wwwroot/js/studio/relief/modifier-doc.js";

const SQUARE = [[0, 0], [10, 0], [10, 10], [0, 10]];
const settle = () => new Promise((resolve) => setTimeout(resolve, 30));

const realFetch = globalThis.fetch;
// The reads are debounced by 140ms; one still waiting when a test ends would ask the next test's recorder.
afterEach(async () => {
  await new Promise((resolve) => setTimeout(resolve, 200));
  globalThis.fetch = realFetch;
});

/** Stand in for the studio: record every request and answer each route with `answers[route]` (or 404). */
function recordFetch(answers = {}) {
  const asked = [];
  globalThis.fetch = async (url, init) => {
    asked.push({ url, body: init?.body });
    const route = Object.keys(answers).find((key) => String(url).includes(key));
    return route === undefined
      ? { ok: false, status: 404, json: async () => ({}) }
      : { ok: true, status: 200, json: async () => answers[route] };
  };
  return asked;
}

async function terraformBoard(modifiers) {
  const board = await mountSketchBridge({ slug: "board" });
  board.handle.load({ layers: [], modifiers });
  board.handle.setReliefMode(true);
  await settle();
  return board;
}

const stackOf = () => {
  const hill = newModifier([], "mound", SQUARE);
  const ridge = newModifier([hill], "ridge", [[0, 0], [20, 0]]);
  return [hill, ridge, { ...newModifier([hill, ridge], "noise", SQUARE), off: true }];
};

const lastState = (board) => JSON.parse(board.firedOf("OnModifiers").at(-1)[0]);

test("the contours are read at the step the strip stands at, and all of them when it stands at the last", async () => {
  const asked = recordFetch({ "sketch/relief": { groups: [] } });
  const board = await terraformBoard(stackOf());
  const reliefReads = () => asked.filter((read) => read.url.includes("sketch/relief")).map((read) => read.url);
  assert.ok(reliefReads().at(-1).endsWith("sketch/relief?heights=true"), "the phase opens on the whole stack");

  board.handle.setStep(1);
  await settle();
  assert.ok(reliefReads().at(-1).endsWith("sketch/relief?heights=true&step=1"));
  assert.equal(lastState(board).step, 1);

  board.handle.setStep(0);
  await settle();
  assert.ok(reliefReads().at(-1).endsWith("&step=0"), "the shapes alone is a step");

  board.handle.setStep(2);
  await settle();
  assert.ok(!reliefReads().at(-1).includes("step"), "two modifiers are on, so step 2 is all of them");
  assert.equal(lastState(board).step, null);
});

test("a step that no longer exists falls back to the whole stack when a modifier is switched off", async () => {
  recordFetch({ "sketch/relief": { groups: [] } });
  const board = await terraformBoard(stackOf());
  board.handle.setStep(1);
  assert.equal(lastState(board).step, 1);
  board.handle.updateModifier(stackOf()[0].id, JSON.stringify({ off: true }));
  assert.equal(lastState(board).step, null, "one modifier is on, so step 1 is all of them");
});

test("leaving the phase leaves the step behind", async () => {
  const asked = recordFetch({ "sketch/relief": { groups: [] } });
  const board = await terraformBoard(stackOf());
  board.handle.setStep(1);
  board.handle.setReliefMode(false);
  await settle();
  assert.equal(lastState(board).step, null);
  assert.ok(!asked.filter((read) => read.url.includes("sketch/relief")).at(-1).url.includes("step"));
});

test("what each modifier changed is asked of the live layout and handed to the host by id", async () => {
  const stack = stackOf();
  const asked = recordFetch({
    "sketch/relief": { groups: [] },
    "sketch/modifiers/changes": { modifiers: [{ id: stack[0].id, columns: 412 }, { id: stack[1].id, columns: 0 }] },
  });
  const board = await terraformBoard(stack);
  const read = asked.find((entry) => entry.url.endsWith("sketch/modifiers/changes"));
  assert.ok(read, "the change counts are read while the phase is up");
  assert.deepEqual(JSON.parse(read.body).modifiers.map((entry) => entry.id), stack.map((entry) => entry.id));
  assert.deepEqual(lastState(board).changes, { [stack[0].id]: 412, [stack[1].id]: 0 });
});

test("a studio that cannot answer the change counts leaves them out", async () => {
  recordFetch({ "sketch/relief": { groups: [] } });
  const board = await terraformBoard(stackOf());
  assert.deepEqual(lastState(board).changes, {});
});

test("with no modifier there is nothing to count and nothing is asked", async () => {
  const asked = recordFetch({ "sketch/relief": { groups: [] } });
  await terraformBoard([]);
  assert.ok(!asked.some((entry) => entry.url.includes("modifiers/changes")));
});

test("a shape's relation is any of the stated scopes, and follow clears it", async () => {
  recordFetch({});
  const board = await mountSketchBridge({ slug: "board" });
  board.handle.load({
    layers: [{ id: "main", name: "main", base_y: 0, layout: { shapes: [{ id: "a", type: "rectangle", operation: "add", min_x: 0, min_z: 0, max_x: 8, max_z: 8 }], groups: [] } }],
  });
  const scopeOf = () => board.handle.getState().layers[0].layout.shapes[0].relief_scope;
  board.handle.setReliefScope("a", "ride");
  assert.equal(scopeOf(), "ride");
  board.handle.setReliefScope("a", "hold");
  assert.equal(scopeOf(), "hold");
  board.handle.setReliefScope("a", "");
  assert.equal(scopeOf(), undefined);
});
