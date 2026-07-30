// CanvasPainter — the decidable half: backing-store sizing, the screen-space width conversion,
// and the token cache. Drawing itself needs a real 2-D context and is covered by the e2e sweep.
import { test } from "node:test";
import assert from "node:assert/strict";

// A canvas + context stub that records what the painter did to it.
function stubCanvas() {
  return {
    width: 0, height: 0, style: {},
    getContext: () => ({
      calls: [],
      setTransform(...a) { this.calls.push(["setTransform", ...a]); },
      clearRect(...a)    { this.calls.push(["clearRect", ...a]); },
      save()             { this.calls.push(["save"]); },
      restore()          { this.calls.push(["restore"]); },
    }),
  };
}

/** Install the globals CanvasPainter reads, with a settable theme + token table. */
function installEnv({ dpr = 1, tokens = {} } = {}) {
  globalThis.devicePixelRatio = dpr;
  const observers = [];
  globalThis.MutationObserver = class {
    constructor(cb) { this.cb = cb; observers.push(this); }
    observe() {}
    disconnect() {}
  };
  globalThis.document = { documentElement: {} };
  globalThis.getComputedStyle = () => ({
    getPropertyValue: (name) => tokens[name] ?? "",
  });
  return { observers, flipTheme: () => observers.forEach(o => o.cb()) };
}

const { CanvasPainter } = await (async () => {
  installEnv();
  return import("../../src/PgmStudio.Client/wwwroot/js/studio/render/canvas-painter.js");
})();

test("resize sizes the buffer by DPR and the element by the CSS box", () => {
  installEnv({ dpr: 2 });
  const el = stubCanvas();
  const painter = new CanvasPainter(el);
  painter.resize(300, 150);
  assert.equal(el.width, 600);          // buffer — where the sharpness comes from
  assert.equal(el.height, 300);
  assert.equal(el.style.width, "300px"); // layout box — unchanged
  assert.equal(el.style.height, "150px");
});

test("DPR is clamped so the buffer does not grow past twice the box", () => {
  installEnv({ dpr: 4 });
  const painter = new CanvasPainter(stubCanvas());
  assert.equal(painter.dpr, 1);          // not yet measured
  painter.resize(100, 100);
  assert.equal(painter.dpr, 2);
});

test("a fractional DPR still yields a whole-pixel buffer", () => {
  installEnv({ dpr: 1.5 });
  const el = stubCanvas();
  new CanvasPainter(el).resize(101, 51);
  assert.equal(el.width, 152);           // 101 * 1.5 = 151.5, rounded
  assert.equal(el.height, 77);           // 51 * 1.5 = 76.5, rounded
  assert.ok(Number.isInteger(el.width) && Number.isInteger(el.height));
});

test("screenPx divides out the scale in force, so a width is a screen width at any zoom", () => {
  installEnv();
  const painter = new CanvasPainter(stubCanvas());
  painter.resize(100, 100);
  painter.begin(1, 0, 0);
  assert.equal(painter.screenPx(2), 2);
  painter.begin(8, 0, 0);
  assert.equal(painter.screenPx(2), 0.25);   // 8× zoom → a quarter world unit is 2 screen px
  painter.begin(0.25, 0, 0);
  assert.equal(painter.screenPx(2), 8);
});

test("begin folds DPR and pan into the viewport transform", () => {
  installEnv({ dpr: 2 });
  const el = stubCanvas();
  const painter = new CanvasPainter(el);
  painter.resize(100, 100);
  painter.begin(3, 10, -20);
  const last = painter.ctx.calls.filter(c => c[0] === "setTransform").pop();
  assert.deepEqual(last, ["setTransform", 6, 0, 0, 6, 20, -40]);  // scale*dpr, pan*dpr
});

test("a zero or missing scale does not divide by zero", () => {
  installEnv();
  const painter = new CanvasPainter(stubCanvas());
  painter.resize(10, 10);
  painter.begin(0, 0, 0);
  assert.equal(painter.screenPx(1), 1);
});

test("token resolves a custom property and caches it", () => {
  const env = installEnv({ tokens: { "--canvas-axis": " #a78bfa " } });
  let reads = 0;
  globalThis.getComputedStyle = () => ({ getPropertyValue: (n) => { reads++; return n === "--canvas-axis" ? "#a78bfa" : ""; } });
  const painter = new CanvasPainter(stubCanvas());
  assert.equal(painter.token("--canvas-axis"), "#a78bfa");
  assert.equal(painter.token("--canvas-axis"), "#a78bfa");
  assert.equal(reads, 1, "resolving forces a style recalc — it must happen once, not per draw");
  void env;
});

test("token hands back a color-mix() unevaluated, which is what a 2-D context wants", () => {
  installEnv({ tokens: { "--canvas-chunk": "color-mix(in oklab, #a78bfa 38%, transparent)" } });
  const painter = new CanvasPainter(stubCanvas());
  assert.equal(painter.token("--canvas-chunk"), "color-mix(in oklab, #a78bfa 38%, transparent)");
});

test("an absent token falls back rather than returning empty, which would keep the previous colour", () => {
  installEnv({ tokens: {} });
  const painter = new CanvasPainter(stubCanvas());
  assert.equal(painter.token("--nope", "#123456"), "#123456");
});

test("a theme flip drops the cache so the next read re-resolves", () => {
  let value = "#ffffff";
  const env = installEnv();
  globalThis.getComputedStyle = () => ({ getPropertyValue: () => value });
  const painter = new CanvasPainter(stubCanvas());
  assert.equal(painter.token("--canvas-ink"), "#ffffff");
  value = "#0f172a";
  assert.equal(painter.token("--canvas-ink"), "#ffffff", "cached until the theme moves");
  env.flipTheme();
  assert.equal(painter.token("--canvas-ink"), "#0f172a");
});

test("layer records paint order and brackets each phase in save/restore", () => {
  installEnv();
  const painter = new CanvasPainter(stubCanvas());
  painter.resize(10, 10);
  painter.begin(1, 0, 0);
  painter.layer("grid", () => {});
  painter.layer("piece", () => {});
  assert.deepEqual(painter.layers, ["grid", "piece"]);
  const bracket = painter.ctx.calls.filter(c => c[0] === "save" || c[0] === "restore").map(c => c[0]);
  assert.deepEqual(bracket, ["save", "restore", "save", "restore"]);
});

test("a phase that throws still restores, so it cannot leak style into the next", () => {
  installEnv();
  const painter = new CanvasPainter(stubCanvas());
  painter.resize(10, 10);
  painter.begin(1, 0, 0);
  assert.throws(() => painter.layer("bad", () => { throw new Error("boom"); }), /boom/);
  const bracket = painter.ctx.calls.filter(c => c[0] === "save" || c[0] === "restore").map(c => c[0]);
  assert.deepEqual(bracket, ["save", "restore"]);
});

test("begin resets the recorded layer list each frame", () => {
  installEnv();
  const painter = new CanvasPainter(stubCanvas());
  painter.resize(10, 10);
  painter.begin(1, 0, 0);
  painter.layer("grid", () => {});
  painter.begin(1, 0, 0);
  assert.deepEqual(painter.layers, []);
});
