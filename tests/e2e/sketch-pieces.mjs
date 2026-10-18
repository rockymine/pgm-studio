/**
 * Draw's Layout step places and moves the play pieces on a board drawn from nothing.
 *
 * The board is a fresh sketch under `rot_180` whose intent states no teams — the map a person starts with. A spawn
 * and a wool room dragged out on it are stored with their images; a wall dragged on the canvas is stored and drawn
 * where it was dropped; and only the authored image of an orbit is picked, the image it draws moving with it.
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
page.on("response", res => {
  if (res.url().includes("/intent/pieces") && res.request().method() !== "GET") written.push(res.status());
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
  await drag(middle, { x: middle.x + 6, z: middle.z });
  const after = (await byKind("wall")).find(piece => piece.id === wallId);
  checks.add("the wall is stored six blocks east", after.minX === wall.minX + 6, `${wall.minX} → ${after.minX}`);
  const image = (await byKind("wall")).find(piece => piece.id !== wallId);
  checks.add("its image is stored six blocks west", image.minX === -after.maxX, `${image.minX} vs ${-after.maxX}`);
  checks.add("and the canvas drew the move", !before.equals(await page.locator(".svg-area").screenshot()));

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
