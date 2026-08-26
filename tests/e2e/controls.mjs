/**
 * The control row and the icon scale — the two things a stylesheet states but nothing checks.
 *
 * A row that mixes a select, a text input, a coord-field and an icon button gets four different
 * intrinsic heights for one type size (31 / 29 / 26px in Chrome), so the library rows state the height
 * once as `--control-height` and the square controls read their WIDTH off it. Both halves of that are
 * quiet when they break: a new control in the row escapes the rule and sits short, a square control
 * silently draws as a pill, or the token stops matching the type scale and a select's text clips inside
 * a box that no longer fits it. None of that raises an error, and the smoke sweep only sees errors — so
 * this spec measures the rendered boxes instead.
 *
 * Comparing a row's controls against the token alone would be a tautology: the token is what sized them.
 * The check that gives the number meaning is the UNCONSTRAINED one — a `select.field-input` outside any
 * stated-height row still renders at its own intrinsic height, so if the type scale moves and the token
 * does not, that select and `--control-height` part company and this fails.
 *
 * The icon half is the same shape of problem. Glyph sizes were eight arbitrary numbers across twenty
 * rules, six of which targeted `i` and had been dead since the vendored shim started REPLACING the
 * placeholder with an `<svg>`. Asserting every rendered glyph lands on the five-step scale is what stops
 * a stray hardcoded size — or another dead selector — going unnoticed, so the sweep has to reach the
 * surfaces that only exist once a rail is open, not just the routes' resting state.
 */

import { openBrowser, newPage, clearFaults, Checks, api, BASE } from "./lib/harness.mjs";

const checks = new Checks("controls");
const browser = await openBrowser();
const page = await newPage(browser);

// ── the library rows need something to draw, so this spec builds its own ─────────────────────────
// A layer stack over a voronoi: nested entries, a band list, and a block picker inside each — every
// row shape the material editor has, in one style.
const solid = { kind: "solid", id: 1, data: 0 };
const voronoi = {
  kind: "voronoi", cellSize: 9, rise: 0, seed: 3,
  bands: [{ depth: 1, material: solid }, { depth: 2, material: { kind: "solid", id: 2, data: 0 } }],
};
const style = await api("/styles", { method: "POST", body: {
  name: "E2E control row", kind: "layered",
  params: JSON.stringify({ kind: "layered", layers: [{ thickness: 1, material: voronoi }] }),
} });
const room = await api("/room-styles", { method: "POST", body: {
  name: "E2E control room", floorDepth: 1, wallHeight: 5, roofThickness: 1,
  roofForm: "flat", roofHole: false, door: "oak_door", doorHeight: 3,
  windows: { form: "none", block: 0, data: 0, sill: 0, width: 0, height: 0, spacing: 0 },
  storeyStack: [],
  courses: [{ part: "wall", ordinal: 0, styleId: style.id, height: 2 }],
} });

const ROWS = ".material-editor-head, .block-picker-row, .lib-bind";

/** Measure every control in every row on the page, against the token the stylesheet declares. */
async function measureRows(label) {
  const state = await page.evaluate(ROWS => {
    const declared = parseFloat(
      getComputedStyle(document.documentElement).getPropertyValue("--control-height"));
    const SQUARE = ".action-btn--icon, .block-chip, .lib-bind-swatch";
    const name = el => `${el.tagName.toLowerCase()}.${(el.className || "").toString().split(" ")[0]}`;
    const wrong = [], notSquare = [];
    let controls = 0, squares = 0;

    for (const row of document.querySelectorAll(ROWS)) {
      for (const el of row.children) {
        // The help mark is a round marker beside a control, not a control — it keeps its own size.
        if (el.classList.contains("help-mark")) continue;
        const box = el.getBoundingClientRect();
        controls++;
        if (Math.abs(box.height - declared) > 0.5) wrong.push(`${name(el)} h=${box.height}`);
        if (el.matches(SQUARE)) {
          squares++;
          if (Math.abs(box.width - box.height) > 0.5) notSquare.push(`${name(el)} ${box.width}×${box.height}`);
        }
      }
    }

    return { declared, controls, squares, wrong, notSquare };
  }, ROWS);

  // Vacuity guard: a renamed selector would otherwise turn this whole spec green by measuring nothing.
  checks.add(`${label}: rows were found to measure`, state.controls > 0, `${state.controls} controls`);
  checks.add(`${label}: every control is one --control-height`, state.wrong.length === 0,
    state.wrong.length ? state.wrong.join(" | ") : `${state.controls} × ${state.declared}px`);
  checks.add(`${label}: square controls were found`, state.squares > 0, `${state.squares} square`);
  checks.add(`${label}: every square control is square`, state.notSquare.length === 0,
    state.notSquare.length ? state.notSquare.join(" | ") : `${state.squares} checked`);
}

/**
 * The one measurement the token did not produce. Every select inside a row is sized BY --control-height,
 * so comparing those against it proves nothing; a select that no row constrains still renders at its own
 * intrinsic height, and that is what ties the token to the type scale rather than to itself. Move the type
 * scale without moving the token and these part company here — which is the drift that otherwise shows up
 * only as a select's text quietly clipping inside a box that no longer fits it.
 */
async function measureUnconstrainedSelect(label) {
  const state = await page.evaluate(ROWS => {
    const declared = parseFloat(
      getComputedStyle(document.documentElement).getPropertyValue("--control-height"));
    const free = [...document.querySelectorAll("select.field-input")]
      .find(el => !el.parentElement?.matches(ROWS));
    return { declared, height: free ? free.getBoundingClientRect().height : null };
  }, ROWS);

  checks.add(`${label}: an unconstrained select was found`, state.height !== null,
    state.height === null ? "nothing outside a stated-height row" : `${state.height}px`);
  checks.add(`${label}: --control-height is what a select actually wants`,
    state.height !== null && Math.abs(state.height - state.declared) <= 1,
    `token ${state.declared}px vs unconstrained select ${state.height}px`);
}

/** Every rendered glyph on whatever is currently on screen must land on a step of the icon scale. */
async function measureGlyphs(label) {
  await page.waitForFunction(() => document.querySelector("svg.lucide") !== null, { timeout: 20000 })
    .catch(() => {});
  const sizes = await page.evaluate(() => {
    const root = getComputedStyle(document.documentElement);
    const scale = ["xs", "sm", "md", "lg", "xl"]
      .map(step => parseFloat(root.getPropertyValue(`--icon-${step}`)));
    const off = [];
    let seen = 0;
    for (const svg of document.querySelectorAll("svg.lucide")) {
      const box = svg.getBoundingClientRect();
      if (box.width === 0) continue;   // an icon in a collapsed/hidden panel measures nothing
      seen++;
      if (!scale.some(step => Math.abs(box.width - step) < 0.5)) {
        off.push(`${svg.getAttribute("data-lucide")} ${box.width}px`);
      }
    }
    return { scale, seen, off: [...new Set(off)] };
  });
  checks.add(`${label}: glyphs rendered`, sizes.seen > 0, `${sizes.seen} glyphs`);
  checks.add(`${label}: every glyph is a scale step`, sizes.off.length === 0,
    sizes.off.length ? sizes.off.join(", ") : `on ${sizes.scale.join(" / ")}px`);
}

checks.section("a control row is one row of one height");

clearFaults(page);
await page.goto(`${BASE}/library/styles`, { waitUntil: "networkidle", timeout: 30000 });
await page.waitForSelector(".lib-card", { timeout: 20000 });
await page.locator(".lib-card", { hasText: "E2E control row" }).locator(".lib-card-fig").click();
await page.waitForSelector(".material-editor-head", { timeout: 20000 });
await measureRows("style editor");
const stylesEditorGlyphs = "style editor";

clearFaults(page);
await page.goto(`${BASE}/library/houses`, { waitUntil: "networkidle", timeout: 30000 });
await page.waitForSelector(".lib-card", { timeout: 20000 });
await page.locator(".lib-card", { hasText: "E2E control room" }).locator(".lib-card-fig").click();
await page.waitForSelector(".lib-outline-row", { timeout: 20000 });
await page.locator(".lib-outline-row", { hasText: "Walls" }).click();
await page.waitForSelector(".lib-bind", { timeout: 20000 });
await measureRows("house editor");
await measureUnconstrainedSelect("house editor");

// ── the icon scale ───────────────────────────────────────────────────────────────────────────────
// The house editor is still open, so this pass covers the outline and the inspector — surfaces the resting
// routes below never render.
checks.section("every glyph lands on the icon scale");
await measureGlyphs("house editor (open)");

for (const route of ["/", "/library/styles", "/maps", "/catalog"]) {
  clearFaults(page);
  await page.goto(`${BASE}${route}`, { waitUntil: "networkidle", timeout: 30000 });
  await measureGlyphs(route);
}

// The style editor, for the same reason — and it carries the material editor's own glyphs.
clearFaults(page);
await page.goto(`${BASE}/library/styles`, { waitUntil: "networkidle", timeout: 30000 });
await page.waitForSelector(".lib-card", { timeout: 20000 });
await page.locator(".lib-card", { hasText: "E2E control row" }).locator(".lib-card-fig").click();
await page.waitForSelector(".material-editor-head", { timeout: 20000 });
await measureGlyphs(`${stylesEditorGlyphs} (open)`);

checks.finish();
await browser.close();
