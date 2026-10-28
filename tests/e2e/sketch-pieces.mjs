/**
 * Draw's Layout step places and moves the play pieces on a board drawn from nothing.
 *
 * The board is a fresh sketch under `rot_180` whose intent states no teams — the map a person starts with. A spawn
 * and a wool room dragged out on it are stored with their images; a wall dragged on the canvas is stored and drawn
 * where it was dropped; a room is drawn with the building it is raised as, and its region and its building resize
 * from an edge, the image following; a control point clicked down is laid round from its rail; a wall bends at a
 * corner a shift-press adds; and only the
 * authored image of an orbit is picked, the image it draws moving with it.
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
const { id: wallId } = await api(`/map/${slug}/intent/pieces`, { method: "POST", body: { kind: "wall", points: [[-40, -10], [-40, 10]] } });

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
  await pick("Pieces", "Spawn room");
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

  checks.section("a placed piece is a step of the board's undo");
  // A step over a piece writes the intent and reads the board back; a press while one is on its way is dropped,
  // so each is waited out by the control it opens: Redo once the undo is done, Undo once the redo is.
  const offered = (title) => page.locator(`.editor-bar-history button[title="${title}"]:not([disabled])`)
    .waitFor({ timeout: 20000 }).catch(() => {});
  await page.keyboard.press("Control+z");
  await offered("Redo");
  const undone = await byKind("woolRoom");
  checks.add("undo takes the wool room back out", undone.length === 0, JSON.stringify(undone.map(p => p.id)));
  checks.add("and leaves the spawn placed before it", (await byKind("spawn")).length === 2);
  await page.keyboard.press("Control+Shift+z");
  await offered("Undo");
  for (let tries = 0; tries < 40 && (await byKind("woolRoom")).length < 2; tries++) await page.waitForTimeout(250);
  const redone = await byKind("woolRoom");
  checks.add("redo puts it back", redone.length === 2, JSON.stringify(redone.map(p => p.id)));

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

  checks.section("a room is picked before its marker, named above itself, and held at a height it is given");
  await page.keyboard.press("Escape"); await page.keyboard.press("Escape");
  const held = (await byKind("spawn")).find(piece => piece.id === room.id);
  const centre = at((held.minX + held.maxX) / 2, (held.minZ + held.maxZ) / 2);
  await page.mouse.click(centre.x, centre.y);
  await page.waitForTimeout(300);
  checks.add("a press on the room's marker picks the room", /spawn room/i.test(await pickedTitle()), await pickedTitle());
  const named = await page.$$eval(".svg-area svg text", (els, id) => els.some(el => el.textContent === id), room.id);
  checks.add("the picked room is named by its id above it", named);
  const height = page.locator(".workspace-inspector input[type=number]").first();
  checks.add("a room stating no height reads Auto, and says it stands on the ground the modifiers leave",
    (await height.inputValue()) === "" && /Auto: stands level on the ground the modifiers leave under it/i.test(await page.locator(".workspace-inspector").innerText()),
    await height.inputValue());
  await page.mouse.click(centre.x, centre.y);
  await page.waitForTimeout(300);
  checks.add("and the press after it picks the marker", /spawn(?! room)|marker/i.test(await pickedTitle()), await pickedTitle());
  await page.keyboard.press("Escape");
  await page.mouse.click(centre.x, centre.y);
  await page.waitForTimeout(300);
  await height.fill("14");
  await height.press("Enter");
  await page.waitForTimeout(2500);
  const stored = (await api(`/map/${slug}/sketch`)).layers[0].layout.shapes.find(shape => shape.id === room.id);
  checks.add("a height given to it is stored with hold", stored?.base_height === 14 && stored?.relief_scope === "hold"
    && stored?.height_authored === true, JSON.stringify({ h: stored?.base_height, scope: stored?.relief_scope }));

  checks.section("a control point is put down with a click and laid round from its rail");
  await page.keyboard.press("Escape"); await page.keyboard.press("Escape");
  await pick("Markers", "Capture point");
  const hill = at(20.5, 20.5);
  await page.mouse.click(hill.x, hill.y);
  await page.waitForLoadState("networkidle");
  await page.waitForTimeout(600);
  const points = await byKind("controlPoint");
  checks.add("the point is stored with its image, a 7-block square", points.length === 2
    && points.every(point => point.maxX - point.minX === 7 && point.shape === "square"), JSON.stringify(points));
  checks.add("and the rail names it", /capture point/i.test(await pickedTitle()), await pickedTitle());
  await page.locator(".workspace-inspector button:has-text('disc')").first().click();
  await page.waitForLoadState("networkidle");
  await page.waitForTimeout(600);
  const round = await byKind("controlPoint");
  checks.add("disc on the rail lays the point and its image round", round.every(point => point.shape === "disc"),
    JSON.stringify(round.map(point => point.shape)));

  checks.section("a wall gains a corner with a shift-press on its leg, and the corner drags out into a bend");
  await page.keyboard.press("Escape"); await page.keyboard.press("Escape");
  const straight = (await byKind("wall")).find(piece => !piece.carried);
  const [[ax, az], [bx, bz]] = straight.line;
  const onLeg = (t) => at(ax + (bx - ax) * t, az + (bz - az) * t);
  const pickAt = onLeg(0.25);
  await page.mouse.click(pickAt.x, pickAt.y);
  await page.waitForTimeout(300);
  const addAt = onLeg(0.5);
  await page.keyboard.down("Shift");
  await page.mouse.click(addAt.x, addAt.y);
  await page.keyboard.up("Shift");
  await page.waitForLoadState("networkidle");
  await page.waitForTimeout(600);
  const cornered = await byKind("wall");
  checks.add("the wall and its image are stored with a third corner", cornered.every(piece => piece.line.length === 3),
    JSON.stringify(cornered.map(piece => piece.line)));
  const bend = cornered.find(piece => !piece.carried).line[1];
  await drag({ x: bend[0], z: bend[1] }, { x: bend[0] + 6, z: bend[1] });
  const bent = (await byKind("wall")).find(piece => !piece.carried);
  checks.add("dragging the new corner bends the wall there", bent.line[1][0] === bend[0] + 6 && bent.line.length === 3,
    JSON.stringify(bent.line));

  checks.section("a picked wall's rail switches its bedrock off");
  const stampRow = page.locator(".workspace-inspector label.check-row", { hasText: "Its bedrock" }).first();
  checks.add("the rail offers the wall's one stamp, on", (await stampRow.count()) === 1
    && await stampRow.locator("input").isChecked());
  await stampRow.locator("input").click();
  await page.waitForLoadState("networkidle");
  await page.waitForTimeout(600);
  const switched = (await api(`/map/${slug}/intent`)).structures.walls;
  checks.add("and switching it off writes it off on the wall and its image",
    switched.every(wall => JSON.stringify(wall.stampsOff) === '["wall"]'), JSON.stringify(switched.map(wall => wall.stampsOff)));

  checks.section("Layout picks the pieces and Ground the ground");
  await page.keyboard.press("Escape"); await page.keyboard.press("Escape");
  const bare = at(20, -25);
  await page.mouse.click(bare.x, bare.y);
  await page.waitForTimeout(300);
  checks.add("in Layout a press on bare land picks nothing", !/rectangle|polygon|group/i.test(await pickedTitle()),
    await pickedTitle());
  await page.mouse.dblclick(bare.x, bare.y);
  await page.waitForTimeout(300);
  checks.add("and a double press on it picks no shape either", !/rectangle|polygon|group/i.test(await pickedTitle()),
    await pickedTitle());

  checks.section("Layout's panel lists the pieces, not the shapes");
  const rail = page.locator(".workspace-sidebar");
  checks.add("the panel holds no shape tree", (await rail.locator(".region-tree").count()) === 0);
  const row = rail.locator(".list-row", { hasText: room.id }).first();
  checks.add("the panel lists the spawn room by its id", (await row.count()) === 1);
  await row.click();
  await page.waitForTimeout(300);
  checks.add("choosing it picks the room", /spawn room/i.test(await pickedTitle()), await pickedTitle());
  checks.add("and its row is lit", /list-row--selected/.test(await row.getAttribute("class") ?? ""));
  const imageRow = rail.locator(".list-row.list-row--muted").first();
  checks.add("an orbit's image is listed and muted", (await imageRow.count()) === 1);
  // The row takes no pointer, so the click is sent to it directly: what is checked is that the list refuses it.
  await imageRow.dispatchEvent("click");
  await page.waitForTimeout(300);
  checks.add("and choosing it leaves the pick where it was", /spawn room/i.test(await pickedTitle()), await pickedTitle());
  await page.keyboard.press("Escape");
  await page.click(".flow-step >> text=Ground");
  await page.locator(".flow-step--active", { hasText: "Ground" }).waitFor({ timeout: 30000 });
  await page.mouse.click(centre.x, centre.y);
  await page.waitForTimeout(300);
  checks.add("in Ground a press on a room reaches the ground under it", !/spawn/i.test(await pickedTitle()),
    await pickedTitle());

  drove = true;
} catch (e) {
  page.faults.push(`pieces: ${String(e).split("\n")[0]}`);
}
checks.add("the piece checks ran", drove, page.faults.slice(0, 3).join(" | "));
checks.add("and raised nothing", page.faults.length === 0, page.faults.slice(0, 3).join(" | "));

await browser.close();
await api(`/map/${slug}`, { method: "DELETE" }).catch(() => {});
checks.finish();
