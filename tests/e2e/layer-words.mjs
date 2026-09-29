/**
 * A board saved from the Sketch tool keeps what its layers state and the editor does not draw.
 *
 * A layer's `kind`, `part_of` and `seat` are stated through the API — a made thing, which prop it is part of,
 * whether its floors are sat onto the ground — and the canvas has no control for any of them. The tool holds
 * them as they were read and writes them back with the layer, so an author drawing one shape in the browser
 * cannot turn a seated ruin back into a floating one.
 */

import { openBrowser, newPage, Checks, readSeed, api, BASE } from "./lib/harness.mjs";

const seed = await readSeed();
const checks = new Checks("layer words");

const layout = await api(`/map/${seed.sketchSlug}/sketch`);
const ruin = {
  id: "ruin", name: "Ruin", base_y: 40, kind: "made", part_of: "ruin", seat: "ground",
  layout: {
    shapes: [{ id: "ruin-wall", type: "rectangle", operation: "add", min_x: 0, max_x: 4, min_z: 0, max_z: 1,
               floor: 0, base_height: 3 }],
    groups: [{ id: "ruin-g", name: "Ruin", mirrors: true, shapeIds: ["ruin-wall"] }],
  },
};
const draft = await api("/sketch", { method: "POST", body: { name: "E2E layer words" } });
await api(`/map/${draft.slug}/sketch`, { method: "PUT", body: { ...layout, layers: [...layout.layers, ruin] } });

const browser = await openBrowser();
const page = await newPage(browser);
let saved = 0;
page.on("response", (response) => {
  const request = response.request();
  if (request.method() === "PUT" && new URL(request.url()).pathname === `/api/map/${draft.slug}/sketch`
      && response.ok()) saved++;
});

await page.goto(`${BASE}/maps/${draft.slug}/sketch`, { waitUntil: "networkidle" });
await page.waitForSelector("svg.map-canvas-svg", { timeout: 20000 });

const box = await page.locator("svg.map-canvas-svg").boundingBox();
await page.click('.canvas-dock button[aria-label="Rectangle"]');
await page.mouse.move(box.x + box.width * 0.45, box.y + box.height * 0.45);
await page.mouse.down();
await page.mouse.move(box.x + box.width * 0.52, box.y + box.height * 0.52, { steps: 6 });
await page.mouse.up();
await page.waitForTimeout(2500);

checks.section("a browser save writes a made layer's words back");
checks.add("the drawing was saved", saved > 0, `${saved}`);
const stored = (await api(`/map/${draft.slug}/sketch`)).layers.find((layer) => layer.id === "ruin");
checks.add("the made layer is still there", !!stored);
checks.add("its kind is still made", stored?.kind === "made", `${stored?.kind}`);
checks.add("it is still part of the ruin", stored?.part_of === "ruin", `${stored?.part_of}`);
checks.add("and still seated on the ground", stored?.seat === "ground", `${stored?.seat}`);

checks.add("sketch tool is clean", page.faults.length === 0, page.faults.slice(0, 3).join(" | "));
checks.finish();
await browser.close();
