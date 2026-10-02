/**
 * The Sketch tool's In game notes: Send is off until something is written, says it is sending and then that it
 * sent, and one press is one note however many clicks land; a reply joins the thread on the author's side; a
 * mark on a picture of a board that has changed since is refused until the pictures are drawn again, and the
 * note then records the change it was drawn at; a thread is resolved, opened from its link, and its "changes
 * since" open History.
 *
 * In game draws with Minecraft's block textures, which a studio has only when given them
 * (`Textures__AcceptMojangEula=true` or `Textures__Jar`). Without them there is no picture to write on, and the
 * spec checks only that the phase says why.
 */

import { openBrowser, newPage, Checks, readSeed, api, BASE } from "./lib/harness.mjs";

const seed = await readSeed();
const checks = new Checks("sketch notes");

const layout = await api(`/map/${seed.sketchSlug}/sketch`);
const draft = await api("/sketch", { method: "POST", body: { name: "E2E notes" } });
await api(`/map/${draft.slug}/sketch`, { method: "PUT", body: layout });

const browser = await openBrowser();
const page = await newPage(browser);
await page.goto(`${BASE}/maps/${draft.slug}/sketch?phase=ingame`, { waitUntil: "networkidle" });
const views = await api(`/map/${draft.slug}/views`);

if (views.undrawable) {
  checks.section("no block textures: the phase says why, and there is nothing to write on");
  await page.waitForSelector(".ingame__note--warn", { timeout: 60000 });
  const said = (await page.locator(".ingame__note--warn").textContent()) ?? "";
  checks.add("the phase names the missing textures", said.includes("textures"), said.trim());
  console.log("  SKIP  the notes themselves — this studio has no block textures to draw a picture with");
} else {
  const send = page.locator(".notes-column__send .action-btn--primary");
  const notes = async () => api(`/map/${draft.slug}/notes`);

  checks.section("a new note: Send is off until something is written, and one press is one note");
  await page.waitForSelector(".notes-column", { timeout: 60000 });
  await page.click("text=New note");
  checks.add("Send is off with nothing written", await send.isDisabled());
  await page.fill("#note-body", "   ");
  checks.add("and with only spaces written", await send.isDisabled());
  await page.fill("#note-body", "The tree on the left floats.");
  checks.add("and on once there is text", !(await send.isDisabled()));
  await send.dblclick();
  await page.waitForSelector(".notes-column__sent", { timeout: 30000 });
  const sent = (await page.locator(".notes-column__sent").textContent()) ?? "";
  checks.add("it says the note was sent", sent.includes("Note sent"), sent.trim());
  const written = await notes();
  checks.add("a double click sends one note", written.length === 1, `${written.length} notes`);
  const first = written[0];
  checks.add("the note keeps the picture it was written on", Boolean(first?.messages[0]?.picture));
  checks.add("and the change its picture was drawn at", first?.messages[0]?.change === views.change,
    `${first?.messages[0]?.change} against ${views.change}`);

  checks.section("a reply joins the thread on the author's side");
  await page.fill("#note-reply", "And the one beside it.");
  await page.keyboard.press("Control+Enter");
  await page.waitForFunction(() => document.querySelectorAll(".note-message").length === 2, null, { timeout: 30000 });
  checks.add("Ctrl+Enter sends it", true);
  const mine = await page.locator(".note-message--mine").count();
  checks.add("both messages sit on the author's side", mine === 2, `${mine} of 2`);
  checks.add("the reply box is empty again", (await page.inputValue("#note-reply")) === "");

  checks.section("a mark on a picture of a board that has changed is refused until it is drawn again");
  await page.click("text=‹ All notes");
  const moved = structuredClone(layout);
  const shape = moved.layers.flatMap((layer) => layer.layout?.shapes ?? []).find((candidate) => !candidate.role);
  for (const key of ["min_x", "max_x", "center_x"]) if (typeof shape[key] === "number") shape[key] += 8;
  if (Array.isArray(shape.vertices)) shape.vertices = shape.vertices.map(([x, z]) => [x + 8, z]);
  await api(`/map/${draft.slug}/sketch`, { method: "PUT", body: moved });
  const latest = (await api(`/map/${draft.slug}/changes`)).changes.at(-1).number;

  const point = async () => {
    await page.click('[aria-label="Point: pin a note to one block"]');
    const trap = page.locator(".ingame__trap");
    const box = await trap.boundingBox();
    await page.mouse.click(box.x + box.width / 2, box.y + box.height / 2);
  };
  await point();
  const redraw = page.getByRole("button", { name: "Draw the pictures again" });
  await redraw.waitFor({ timeout: 30000 });
  const refused = (await page.locator(".notes-column__anchor p").textContent()) ?? "";
  checks.add("the mark says the board has changed", refused.includes("has changed"), refused.trim());
  await page.fill("#note-body", "Still floating after the move.");
  checks.add("and Send stays off", await send.isDisabled());
  await redraw.click();
  await page.waitForSelector(".ingame__trap", { timeout: 60000 });
  checks.add("drawing again keeps the note's text", (await page.inputValue("#note-body")) === "Still floating after the move.");
  const box = await page.locator(".ingame__trap").boundingBox();
  await page.mouse.click(box.x + box.width / 2, box.y + box.height / 2);
  await page.waitForFunction(() => !document.querySelector(".notes-column__send .action-btn--primary")?.disabled, null,
    { timeout: 30000 });
  await send.click();
  await page.waitForSelector(".notes-column__sent", { timeout: 30000 });
  const second = (await notes()).find((note) => note.messages[0].body === "Still floating after the move.");
  checks.add("the note lands at the change the new pictures are of", second?.messages[0]?.change === latest,
    `${second?.messages[0]?.change} against ${latest}`);

  checks.section("a thread is resolved, opened from its link, and its changes since open History");
  await page.click("text=Resolve");
  await page.waitForSelector(".notes-column__threadhead .note-status--resolved", { timeout: 15000 });
  checks.add("Resolve closes the thread", (await notes()).find((note) => note.id === second.id)?.status === "resolved");
  await page.goto(`${BASE}/maps/${draft.slug}/sketch?phase=ingame&note=${first.id}`, { waitUntil: "networkidle" });
  await page.waitForSelector(".notes-column__threadtitle", { timeout: 60000 });
  const opened = await page.locator(".note-message__body").first().textContent();
  checks.add("?note= opens that thread", opened === "The tree on the left floats.", opened ?? "");
  const since = page.locator(".notes-column__since");
  checks.add("the thread counts the change since its last message", (await since.textContent())?.startsWith("1 change"),
    (await since.textContent())?.trim() ?? "");
  await since.click();
  await page.waitForSelector(".change-edit", { timeout: 30000 });
  checks.add("and opens History on it", page.url().includes("phase=history") || (await page.locator(".list-row--selected").count()) > 0);
}

checks.add("sketch notes is clean", page.faults.length === 0, page.faults.slice(0, 3).join(" | "));
checks.finish();
await browser.close();
