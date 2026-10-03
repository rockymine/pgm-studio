// A canvas lets go of the page when its tool unmounts. The claims under test: the plan bridge's `dispose`
// reaches the canvas's own dispose without throwing, and the canvas base removes exactly the `document`
// listeners it added, so a mount followed by an unmount leaves the page as it found it.
import { test } from "node:test";
import assert from "node:assert/strict";

// A permissive stand-in for a 2-D context: every property reads as another stand-in and every call answers one.
const sink = () => new Proxy(function () {}, {
  get: (_target, key) => (key === Symbol.toPrimitive ? () => 0 : sink()),
  apply: () => sink(),
  set: () => true,
});

const element = () => ({
  style: {},
  children: [],
  firstChild: null,
  parentNode: { insertBefore() {} },
  classList: { add() {}, remove() {}, toggle() {} },
  addEventListener() {},
  appendChild(child) { return child; },
  remove() {},
  setAttribute() {},
  getAttribute() { return null; },
  querySelector() { return null; },
  getBoundingClientRect() { return { left: 0, top: 0, width: 100, height: 100 }; },
  getContext() { return sink(); },
});

const added = [];
const removed = [];
globalThis.window = globalThis;
globalThis.localStorage = { getItem() { return null; }, setItem() {} };
globalThis.getComputedStyle = () => ({ getPropertyValue: () => "" });
globalThis.devicePixelRatio = 1;
globalThis.matchMedia = () => ({ addEventListener() {}, matches: false });
globalThis.requestAnimationFrame = () => 1;
globalThis.cancelAnimationFrame = () => {};
globalThis.document = {
  createElement: element,
  createElementNS: element,
  documentElement: element(),
  body: element(),
  addEventListener(type, listener) { added.push({ type, listener }); },
  removeEventListener(type, listener) { removed.push({ type, listener }); },
};

const { mount } = await import("../../src/PgmStudio.Client/wwwroot/js/studio/bridge/plan-bridge.js");
const { CanvasBase } = await import("../../src/PgmStudio.Client/wwwroot/js/studio/canvas/canvas-base.js");

const dragTypes = new Set(["mousemove", "mouseup"]);
const dragListeners = (log) => log.filter(entry => dragTypes.has(entry.type));

test("disposing the plan bridge does not throw", async () => {
  const handle = await mount(element(), element(), element(), null);
  assert.doesNotThrow(() => handle.dispose());
});

test("disposing the plan bridge removes every drag listener its canvas put on the document", async () => {
  added.length = 0;
  removed.length = 0;
  const handle = await mount(element(), element(), element(), null);
  const mounted = dragListeners(added);
  assert.deepEqual(mounted.map(entry => entry.type).sort(), ["mousemove", "mouseup"]);
  handle.dispose();
  for (const entry of mounted)
    assert.ok(removed.some(gone => gone.type === entry.type && gone.listener === entry.listener),
      `${entry.type} was left on the document`);
});

test("a canvas base that is disposed twice removes its listeners once and does not throw", () => {
  added.length = 0;
  removed.length = 0;
  const canvas = new CanvasBase(element(), element());
  canvas._disposeCanvasBase();
  assert.doesNotThrow(() => canvas._disposeCanvasBase());
  assert.equal(dragListeners(removed).length, 2);
});
