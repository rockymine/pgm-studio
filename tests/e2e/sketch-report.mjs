/**
 * The Sketch tool's Report phase: the board's report opens on its three numbers, every reading is listed under
 * its name and folds open to its text, and the pictures are drawn one at a time — the first on arrival, any
 * other on picking its name — with nothing on the page refused.
 */

import { openBrowser, newPage, Checks, readSeed, api, BASE } from "./lib/harness.mjs";

const seed = await readSeed();
const checks = new Checks("sketch report");
const report = await api(`/map/${seed.sketchSlug}/report`);

const browser = await openBrowser();
const page = await newPage(browser);
await page.goto(`${BASE}/maps/${seed.sketchSlug}/sketch?phase=report`, { waitUntil: "networkidle" });

checks.section("the report opens on its three numbers");
await page.waitForSelector(".report__numbers", { timeout: 90000 });
const numbers = (await page.locator(".report__numbers").textContent()) ?? "";
checks.add("the numbers are the report's own", report.headline.says.every((line) => numbers.includes(line.trim())),
  numbers.trim().split("\n").join(" | "));

checks.section("every reading is listed and opens to its text");
const names = await page.locator(".report__read-name").allTextContents();
checks.add("one row a reading", names.length === report.reads.length, `${names.length} rows for ${report.reads.length}`);
await page.locator(".report__read-name", { hasText: /^slopes$/ }).click();
const slopes = (await page.locator(".report__read[open] .report__text").first().textContent()) ?? "";
checks.add("the slopes open to the slope grid", slopes.startsWith("SLOPES"), slopes.split("\n")[0]);

checks.section("the pictures are drawn one at a time");
await page.waitForSelector(".report__picture img.is-loaded", { timeout: 60000 });
const first = await page.locator(".report__picture img").getAttribute("src");
checks.add("the first is drawn on arrival", first?.includes(report.pictures[0].route) ?? false, first ?? "");
await page.locator(".report__picture-name", { hasText: /^heightmap$/ }).click();
await page.waitForFunction(() => document.querySelector(".report__picture img")?.getAttribute("src")?.includes("render/heightmap"));
await page.waitForSelector(".report__picture img.is-loaded", { timeout: 60000 });
checks.add("another is drawn on picking its name", true);

checks.add("sketch report is clean", page.faults.length === 0, page.faults.slice(0, 3).join(" | "));
checks.finish();
await browser.close();
