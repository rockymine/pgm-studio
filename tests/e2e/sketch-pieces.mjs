/**
 * Draw's Layout step places and moves the play pieces on a board drawn from nothing.
 *
 * The board is a fresh sketch under `rot_180` whose intent states no teams — the map a person starts with. A spawn
 * and a wool room dragged out on it are stored with their images; a wall dragged on the canvas is stored and drawn
 * where it was dropped; a room is drawn with the building it is raised as, and its region and its building resize
 * from an edge, the image following; and only the authored image of an orbit is picked, the image it draws moving
 * with it.
 */

import { openBrowser, newPage, clearFaults, Checks, api, worldAimer, BASE } from "./lib/harness.mjs";

const checks = new Checks("sketch pieces");

// ── the board: one ground rectangle across the axis, no teams, one wall placed over the route ──────────────
const { slug } = await api("/sketch", { method: "POST", body: { name: "E2E pieces" } });
await api(`/map/${slug}/sketch`, {
  method: "PUT",
  body: {
    setup: { mirror_mode: "rot_180", center: { cx: 0, cz: 0 } },
    layers: [{ id: "ground", layout: { shapes: [
      { id: "land", type: "rectangle", operation: "add", min_x: -60, min_z: -30, max_x: 60, max_z: 30, base_height: 9 },
    ], groups: [] } }],
  },
});
const { id: wallId } = await api(`/map/${slug}/intent/pieces`, { method: "POST", body: { kind: "wall", ends: [-40, -10, -40, 10] } });

const pieces = () => api(`/map/${slug}/intent/pieces`);
const byKind = async (kind) => (await pieces()).filter(piece => piece.kind === kind);

const browser = await openBrowser();
const page = await newPage(browser, { width: 1600, height: 1000 });
const written = [];
const builds = [];
page.on("response", res => {
  if (res.url().includes("/intent/pieces") && res.request().method() !== "GET") written.push(res.status());
  if (res.url().includes("/sketch/columns")) builds.push(res.status());
});

let drove = false;
try {
  await page.goto(`${BASE}/maps/${slug}/sketch`, { waitUntil: "networkidle", timeout: 30000 });
  await page.locator(".flow-step--active", { hasText: "Ground" }).waitFor({ timeout: 60000 });
  await page.click(".flow-step >> text=Layout");
  await page.locator(".flow-step--active", { hasText: "Layout" }).waitFor({ timeout: 30000 });
  await page.waitForTimeout(800);
  const at = await worldAimer(page, page.locator(".svg-area"));
  if (!at) throw new Error("the canvas readout gave no world position to aim with");

  const drag = async (from, to) => {
    const a = at(from.x, from.z), b = at(to.x, to.z);
    await page.mouse.move(a.x, a.y);
    await page.mouse.down();
    await page.mouse.move(b.x, b.y, { steps: 10 });
    await page.mouse.up();
    await page.waitForLoadState("networkidle");
    await page.waitForTimeout(400);
  };
  const pick = async (family, option) => {
    await page.click(`.canvas-dock-chevron[aria-label="${family}"]`);
    await page.click(`.canvas-dock-flyout-row:has-text("${option}")`);
    await page.waitForTimeout(150);
  };

  checks.section("a spawn and a wool room are placed on a board stating no teams");
  await pick("Pieces", "Spawn piece");
  await drag({ x: -55, z: -6 }, { x: -45, z: 6 });
  const spawns = await byKind("spawn");
  checks.add("the spawn is stored with its image", spawns.length === 2, JSON.stringify(spawns.map(p => p.id)));
  await pick("Pieces", "Wool room");
  await drag({ x: -30, z: 14 }, { x: -22, z: 22 });
  const wools = await byKind("woolRoom");
  checks.add("the wool room is stored with its image", wools.length === 2, JSON.stringify(wools.map(p => p.id)));
  checks.add("and every write was answered 2xx", written.every(status => status < 300), written.join(","));
  const intent = await api(`/map/${slug}/intent`);
  checks.add("the map states the two teams that own them", intent.teams?.length === 2,
    JSON.stringify(intent.teams?.map(team => team.id)));

  checks.section("the authored wall moves where it is dropped, and the image moves with it");
  await page.keyboard.press("Escape");
  const wall = (await byKind("wall")).find(piece => piece.id === wallId);
  const middle = { x: (wall.minX + wall.maxX) / 2, z: (wall.minZ + wall.maxZ) / 2 };
  const grab = at(middle.x, middle.z);
  await page.mouse.click(grab.x, grab.y);
  await page.waitForTimeout(300);
  const before = await page.locator(".svg-area").screenshot();
  // The 3-D preview is built once before the move, so the move has a cached picture to make stale.
  const toggle = page.locator(".canvas-mode-toggle").first();
  const preview = async () => {
    await toggle.click();
    await page.waitForLoadState("networkidle");
    await page.waitForTimeout(400);
    await toggle.click();
    await page.waitForTimeout(400);
  };
  await preview();
  const builtBefore = builds.length;
  await page.mouse.click(grab.x, grab.y);
  await page.waitForTimeout(300);
  await drag(middle, { x: middle.x + 6, z: middle.z });
  const after = (await byKind("wall")).find(piece => piece.id === wallId);
  checks.add("the wall is stored six blocks east", after.minX === wall.minX + 6, `${wall.minX} → ${after.minX}`);
  const image = (await byKind("wall")).find(piece => piece.id !== wallId);
  checks.add("its image is stored six blocks west", image.minX === -after.maxX, `${image.minX} vs ${-after.maxX}`);
  checks.add("and the canvas drew the move", !before.equals(await page.locator(".svg-area").screenshot()));
  await preview();
  checks.add("and the 3-D preview is built again rather than drawn from before the move",
    builtBefore === 0 || builds.length > builtBefore, `${builtBefore} build(s) before, ${builds.length} after`);

  checks.section("a room is drawn with its building, and its region and its wing resize from an edge");
  await page.keyboard.press("Escape");
  const sketch = await api(`/map/${slug}/sketch`);
  const drawn = sketch.layers[0].layout.shapes.map(shape => shape.id);
  const room = spawns.find(piece => !piece.carried);
  checks.add("the placed spawn draws the building it is raised as", drawn.includes(`${room.id}-building`),
    JSON.stringify(drawn.filter(id => id.includes("building"))));
  // A quarter in from the corner: the room's middle is its marker, which is a piece of its own.
  const middleZ = room.minZ + (room.maxZ - room.minZ) / 4;
  const inside = at(room.minX + (room.maxX - room.minX) / 4, middleZ);
  await page.mouse.click(inside.x, inside.y);
  await page.waitForTimeout(300);
  await drag({ x: room.maxX, z: middleZ }, { x: room.maxX + 4, z: middleZ });
  const grown = (await byKind("spawn")).find(piece => piece.id === room.id);
  const grownImage = (await byKind("spawn")).find(piece => piece.id !== room.id);
  checks.add("the region is stored four blocks wider, its west side held",
    grown.minX === room.minX && grown.maxX === room.maxX + 4, `${room.minX}..${room.maxX} → ${grown.minX}..${grown.maxX}`);
  checks.add("and its image four blocks wider the other way", grownImage.minX === -grown.maxX,
    `${grownImage.minX} vs ${-grown.maxX}`);

  const spawnHouse = async () => (await api(`/map/${slug}/intent`)).spawns.find(entry => entry.id === room.id).house;
  const roomShapes = (await api(`/map/${slug}/sketch`)).layers[0].layout.shapes;
  const wing = roomShapes.find(shape => shape.id === `${room.id}-building`);
  const wingMiddle = at(wing.min_x + (wing.max_x - wing.min_x) / 4, wing.min_z + (wing.max_z - wing.min_z) / 4);
  await page.mouse.click(wingMiddle.x, wingMiddle.y);   // the room is picked, so a press on its building picks the wing
  await page.waitForTimeout(400);
  // A quarter along the wall, clear of the add-a-wing marker its middle shows.
  const wingZ = wing.min_z + (wing.max_z - wing.min_z) / 4;
  await drag({ x: wing.min_x, z: wingZ }, { x: wing.min_x + 2, z: wingZ });
  const house = await spawnHouse();
  const corners = house?.wings?.[0]?.corners ?? [];
  checks.add("the wing is stored two blocks narrower from its west edge",
    corners.length === 2 && Math.min(corners[0][0], corners[1][0]) === wing.min_x + 2
      && Math.max(corners[0][0], corners[1][0]) === wing.max_x - 1, JSON.stringify(corners));

    checks.section("Escape steps a picked wing back to its room, then lets the room go");
  const pickedTitle = () => page.locator(".workspace-inspector .detail-header").first().innerText().catch(() => "");
  await page.mouse.click(wingMiddle.x, wingMiddle.y);
  await page.waitForTimeout(300);
  checks.add("a press inside the picked room's building picks the wing", /building/i.test(await pickedTitle()),
    await pickedTitle());
  await page.keyboard.press("Escape");
  await page.waitForTimeout(300);
  checks.add("the first press picks the room", /spawn/i.test(await pickedTitle()) && !/building/i.test(await pickedTitle()),
    await pickedTitle());
  await page.keyboard.press("Escape");
  await page.waitForTimeout(300);
  checks.add("and the second picks nothing", !/spawn|building/i.test(await pickedTitle()), await pickedTitle());

    checks.section("an orbit's image is drawn and not picked");
  checks.add("the read marks the wall's image carried", image.carried === true);
  checks.add("and the spawn's and the wool room's", spawns.some(p => p.carried) && wools.some(p => p.carried),
    JSON.stringify([...spawns, ...wools].map(p => `${p.id}:${p.carried}`)));
  const imageMiddle = { x: (image.minX + image.maxX) / 2, z: (image.minZ + image.maxZ) / 2 };
  const count = written.length;
  const press = at(imageMiddle.x, imageMiddle.z);
  await page.mouse.click(press.x, press.y);
  await page.waitForTimeout(300);
  await drag(imageMiddle, { x: imageMiddle.x + 6, z: imageMiddle.z });
  const still = (await byKind("wall")).find(piece => piece.id === image.id);
  checks.add("a drag on the image moves nothing", still.minX === image.minX && written.length === count,
    `${image.minX} → ${still.minX}, ${written.length - count} write(s)`);

  drove = true;
} catch (e) {
  page.faults.push(`pieces: ${String(e).split("\n")[0]}`);
}
checks.add("the piece checks ran", drove, page.faults.slice(0, 3).join(" | "));
checks.add("and raised nothing", page.faults.length === 0, page.faults.slice(0, 3).join(" | "));

await browser.close();
await api(`/map/${slug}`, { method: "DELETE" }).catch(() => {});
checks.finish();
