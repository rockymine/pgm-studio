// CanvasPainter — the decidable half: backing-store sizing, the screen-space width conversion, the token
// cache, and the primitives' style application. What a stroke actually looks like needs a real 2-D
// context and is covered by the e2e paint sweep.
import { test } from "node:test";
import assert from "node:assert/strict";

/**
 * A canvas + context stub that records what the painter did to it. `parses` decides which colour
 * strings the fake context accepts — a rejected assignment leaves fillStyle where it was, which is the
 * real behaviour the painter's colour check depends on.
 */
function stubCanvas({ parses = () => true } = {}) {
  const ctx = {
    calls: [], _fill: "",
    strokeStyle: "", lineWidth: 0, lineCap: "", lineJoin: "", globalAlpha: 1,
    font: "", textAlign: "", textBaseline: "", imageSmoothingEnabled: true,
    get fillStyle() { return this._fill; },
    set fillStyle(v) { if (parses(v)) this._fill = v; },
    setTransform(...a) { this.calls.push(["setTransform", ...a]); },
    clearRect(...a)    { this.calls.push(["clearRect", ...a]); },
    save()             { this.calls.push(["save"]); },
    restore()          { this.calls.push(["restore"]); },
    // The drawing calls record the alpha in force, since that is the half of the style the arguments
    // cannot show and the one the fill/stroke split is easiest to get wrong in.
    fillRect(...a)     { this.calls.push(["fillRect", ...a, this.globalAlpha]); },
    strokeRect(...a)   { this.calls.push(["strokeRect", ...a, this.globalAlpha]); },
    beginPath()        { this.calls.push(["beginPath"]); },
    moveTo(...a)       { this.calls.push(["moveTo", ...a]); },
    lineTo(...a)       { this.calls.push(["lineTo", ...a]); },
    arc(...a)          { this.calls.push(["arc", ...a]); },
    ellipse(...a)      { this.calls.push(["ellipse", ...a]); },
    fill(...a)         { this.calls.push(["fill", ...a, this.globalAlpha]); },
    stroke(...a)       { this.calls.push(["stroke", ...a, this.globalAlpha]); },
    setLineDash(d)     { this.calls.push(["setLineDash", d]); this.dash = d; },
    drawImage(...a)    { this.calls.push(["drawImage", ...a, this.imageSmoothingEnabled]); },
    fillText(...a)     { this.calls.push(["fillText", ...a, this.font]); },
    strokeText(...a)   { this.calls.push(["strokeText", ...a]); },
  };
  return { width: 0, height: 0, style: {}, getContext: () => ctx };
}

/** A painter ready to draw: sized, with a viewport in force. */
function drawingPainter(Painter, { scale = 1, tokens = {}, toSurface } = {}) {
  installEnv({ tokens });
  const painter = new Painter(stubCanvas(), toSurface);
  painter.resize(100, 100);
  painter.begin(scale, 0, 0);
  painter.ctx.calls.length = 0;   // drop the frame set-up, leave only what the primitive does
  return painter;
}

const drawn = (painter, name) => painter.ctx.calls.filter(c => c[0] === name);

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

test("a token the context refuses is demoted to the fallback, not left to paint the previous colour", () => {
  installEnv({ tokens: { "--canvas-chunk": "color-mix(in oklab, #a78bfa 38%, transparent)" } });
  globalThis.getComputedStyle = () => ({ getPropertyValue: () => "color-mix(in oklab, #a78bfa 38%, transparent)" });
  const warnings = [];
  const realWarn = console.warn; console.warn = (m) => warnings.push(m);
  // An engine whose canvas parser predates color-mix: the stylesheet takes it, the context does not.
  const painter = new CanvasPainter(stubCanvas({ parses: (v) => !String(v).startsWith("color-mix") }));
  assert.equal(painter.token("--canvas-chunk", "rgba(167,139,250,0.38)"), "rgba(167,139,250,0.38)");
  assert.match(warnings[0] ?? "", /--canvas-chunk/);
  console.warn = realWarn;
});

test("a token the context accepts is used as resolved", () => {
  installEnv();
  globalThis.getComputedStyle = () => ({ getPropertyValue: () => "color-mix(in oklab, #a78bfa 38%, transparent)" });
  const painter = new CanvasPainter(stubCanvas());   // an engine that parses it, as Chromium does
  assert.equal(painter.token("--canvas-chunk", "#fallback"), "color-mix(in oklab, #a78bfa 38%, transparent)");
});

test("a token whose value IS a sentinel colour is not mistaken for a rejection", () => {
  installEnv();
  globalThis.getComputedStyle = () => ({ getPropertyValue: () => "#000000" });
  const painter = new CanvasPainter(stubCanvas());
  assert.equal(painter.token("--canvas-ink", "#ff0000"), "#000000");
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

// ── primitives ────────────────────────────────────────────────────────────────

test("rect fills and strokes a world box, each half at its own alpha", () => {
  const painter = drawingPainter(CanvasPainter);
  painter.rect({ min_x: 2, min_z: 3, max_x: 12, max_z: 8 },
    { fill: "#f00", fillAlpha: 0.2, stroke: "#0f0", strokeAlpha: 0.5, width: 2 });
  assert.deepEqual(drawn(painter, "fillRect"), [["fillRect", 2, 3, 10, 5, 0.2]]);
  assert.deepEqual(drawn(painter, "strokeRect"), [["strokeRect", 2, 3, 10, 5, 0.5]]);
  assert.equal(painter.ctx.globalAlpha, 1, "the alpha in force is restored, not left on the context");
});

test("a box given max-before-min still draws the box between them", () => {
  const painter = drawingPainter(CanvasPainter);
  painter.rect({ min_x: 12, min_z: 8, max_x: 2, max_z: 3 }, { fill: "#f00" });
  assert.deepEqual(drawn(painter, "fillRect"), [["fillRect", 2, 3, 10, 5, 1]]);
});

test("omitting a half skips it — a fill-only primitive never strokes", () => {
  const painter = drawingPainter(CanvasPainter);
  painter.rect({ min_x: 0, min_z: 0, max_x: 1, max_z: 1 }, { fill: "#f00" });
  assert.equal(drawn(painter, "strokeRect").length, 0);
  painter.rect({ min_x: 0, min_z: 0, max_x: 1, max_z: 1 }, { stroke: "#f00" });
  assert.equal(drawn(painter, "fillRect").length, 1, "still just the first call's fill");
});

test("a width is a SCREEN width — the scale in force is divided back out", () => {
  const painter = drawingPainter(CanvasPainter, { scale: 4 });
  painter.line(0, 0, 10, 0, { stroke: "#fff", width: 2, dash: [6, 3] });
  assert.equal(painter.ctx.lineWidth, 0.5);                  // 2 screen px at 4× zoom
  assert.deepEqual(painter.ctx.dash, [1.5, 0.75]);           // and the dash rides the same conversion
});

test("a dash may be written as an SVG dasharray string", () => {
  const painter = drawingPainter(CanvasPainter);
  painter.line(0, 0, 1, 1, { stroke: "#fff", dash: "5 3" });
  assert.deepEqual(painter.ctx.dash, [5, 3]);
});

test("no dash clears the previous one, so a solid line cannot inherit a dashed one", () => {
  const painter = drawingPainter(CanvasPainter);
  painter.line(0, 0, 1, 1, { stroke: "#fff", dash: [4, 2] });
  painter.line(0, 0, 1, 1, { stroke: "#fff" });
  assert.deepEqual(painter.ctx.dash, []);
});

test("a var() colour is resolved through the token cache, fallback and all", () => {
  const painter = drawingPainter(CanvasPainter, { tokens: { "--accent": "#5b9cff" } });
  painter.rect({ min_x: 0, min_z: 0, max_x: 1, max_z: 1 }, { fill: "var(--accent)" });
  assert.equal(painter.ctx.fillStyle, "#5b9cff");
  painter.rect({ min_x: 0, min_z: 0, max_x: 1, max_z: 1 }, { fill: "var(--missing, #123456)" });
  assert.equal(painter.ctx.fillStyle, "#123456");
});

test("a non-token colour passes straight through — only custom properties are the trap", () => {
  const painter = drawingPainter(CanvasPainter);
  const pattern = { kind: "CanvasPattern" };
  painter.rect({ min_x: 0, min_z: 0, max_x: 1, max_z: 1 }, { fill: "rgb(1,2,3)" });
  assert.equal(painter.ctx.fillStyle, "rgb(1,2,3)");
  painter.rect({ min_x: 0, min_z: 0, max_x: 1, max_z: 1 }, { fill: pattern });
  assert.equal(painter.ctx.fillStyle, pattern);
});

test("alphas multiply: a primitive-wide alpha scales the half-specific one", () => {
  const painter = drawingPainter(CanvasPainter);
  painter.rect({ min_x: 0, min_z: 0, max_x: 1, max_z: 1 }, { fill: "#f00", fillAlpha: 0.5, alpha: 0.4 });
  assert.equal(drawn(painter, "fillRect")[0].at(-1), 0.2);
});

test("segments draws many runs as one path — one beginPath, one stroke", () => {
  const painter = drawingPainter(CanvasPainter);
  painter.segments([{ x1: 0, z1: 0, x2: 10, z2: 0 }, { x1: 0, z1: 5, x2: 10, z2: 5 }], { stroke: "#fff" });
  assert.equal(drawn(painter, "beginPath").length, 1);
  assert.equal(drawn(painter, "moveTo").length, 2);
  assert.equal(drawn(painter, "stroke").length, 1);
});

test("a world transform maps the primitives, but not the stroke width", () => {
  const doubled = (x, z) => ({ x: x * 2 + 100, y: z * 2 + 100 });
  const painter = drawingPainter(CanvasPainter, { scale: 2, toSurface: doubled });
  painter.rect({ min_x: 0, min_z: 0, max_x: 5, max_z: 5 }, { fill: "#f00", stroke: "#f00", width: 3 });
  assert.deepEqual(drawn(painter, "fillRect"), [["fillRect", 100, 100, 10, 10, 1]]);
  assert.equal(painter.ctx.lineWidth, 1.5, "only the viewport scale is divided out, not the world fit");
});

test("circle takes a world radius; dot takes a surface or screen one", () => {
  const doubled = (x, z) => ({ x: x * 2, y: z * 2 });
  const painter = drawingPainter(CanvasPainter, { scale: 4, toSurface: doubled });
  painter.circle(0, 0, 3, { stroke: "#fff" });
  assert.equal(drawn(painter, "arc")[0][3], 6, "3 world units through a 2× world fit");
  painter.dot(0, 0, { radius: 5, fill: "#fff" });
  assert.equal(drawn(painter, "arc")[1][3], 5, "surface units — what an SVG r was");
  painter.dot(0, 0, { radiusPx: 8, fill: "#fff" });
  assert.equal(drawn(painter, "arc")[2][3], 2, "8 screen px at 4× zoom");
});

test("image blits with smoothing off, and puts it back", () => {
  const painter = drawingPainter(CanvasPainter);
  painter.ctx.imageSmoothingEnabled = true;
  const bitmap = { kind: "decoded PNG" };
  painter.image(bitmap, { min_x: 0, min_z: 0, max_x: 4, max_z: 8 });
  assert.deepEqual(drawn(painter, "drawImage"), [["drawImage", bitmap, 0, 0, 4, 8, false]]);
  assert.equal(painter.ctx.imageSmoothingEnabled, true);
});

test("text places at the world point plus a surface-space offset", () => {
  const painter = drawingPainter(CanvasPainter);
  painter.text("r=5", 10, 20, { fill: "#fff", size: 11, dx: 6, dy: -4 });
  const [call] = drawn(painter, "fillText");
  assert.deepEqual(call.slice(0, 4), ["fillText", "r=5", 16, 16]);
  assert.match(painter.ctx.font, /^11px /);
});

test("a text halo is stroked under the fill, so a label survives whatever it lands on", () => {
  const painter = drawingPainter(CanvasPainter);
  painter.text("12", 0, 0, { fill: "#fff", halo: "#000", haloWidth: 3 });
  const order = painter.ctx.calls.map(c => c[0]).filter(n => n === "strokeText" || n === "fillText");
  assert.deepEqual(order, ["strokeText", "fillText"]);
});
