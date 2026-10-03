// What a bridge does with an event the host cannot take. A host that has gone is tolerated; a name the host
// declares no `[JSInvokable]` for is a feed that goes nowhere, and on a development host that is said aloud.
// The second half reads the sources: every event either bridge fires must be a method some host declares.
import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync, readdirSync } from "node:fs";

import { fireTo } from "../../src/PgmStudio.Client/wwwroot/js/studio/bridge/fire.js";

const UNDECLARED = "The type 'PgmStudio.Client.Features.Sketch.SketchTool' does not contain a public invokable method with [JSInvokableAttribute(\"OnNothing\")].";

/** Run `fireTo` against a host answering `answer`, on a page at `hostname`, and collect what reached the console. */
async function fireOn(hostname, answer) {
  const errors = [];
  const original = console.error;
  console.error = (...args) => errors.push(args.join(" "));
  globalThis.location = { hostname };
  try {
    fireTo({ invokeMethodAsync: answer }, "OnNothing", 1);
    await new Promise((resolve) => setTimeout(resolve, 0));
  } finally {
    console.error = original;
    delete globalThis.location;
  }
  return errors;
}

test("an event the host declares nothing for is named in the console on a development host", async () => {
  const errors = await fireOn("localhost", () => Promise.reject(new Error(UNDECLARED)));
  assert.equal(errors.length, 1);
  assert.match(errors[0], /OnNothing/);
});

test("the same event is silent on a deployed host", async () => {
  assert.deepEqual(await fireOn("pgmstudio.de", () => Promise.reject(new Error(UNDECLARED))), []);
});

test("a host that has gone is tolerated, whether it throws or rejects", async () => {
  assert.deepEqual(await fireOn("localhost", () => Promise.reject(new Error("The JS interop call was disposed"))), []);
  assert.deepEqual(await fireOn("localhost", () => { throw new Error("disposed"); }), []);
});

test("a host that is not wired at all is tolerated", () => {
  fireTo(null, "OnNothing");
  fireTo(undefined, "OnNothing");
});

const read = (path) => readFileSync(path, "utf8");
const filesUnder = (directory, extension) => readdirSync(directory, { recursive: true, withFileTypes: true })
  .filter((entry) => entry.isFile() && entry.name.endsWith(extension))
  .map((entry) => `${entry.parentPath}/${entry.name}`);

test("every event a bridge fires is a [JSInvokable] some host declares", () => {
  const declared = new Set(filesUnder("src/PgmStudio.Client/Features", ".cs")
    .flatMap((path) => [...read(path).matchAll(/\[JSInvokable\]\s*public\s+[\w<>?.]+\s+(\w+)\s*\(/g)].map((match) => match[1])));
  assert.ok(declared.size > 20, "the hosts' invokable methods were found");

  for (const bridge of ["sketch-bridge.js", "plan-bridge.js"]) {
    const fired = [...read(`src/PgmStudio.Client/wwwroot/js/studio/bridge/${bridge}`).matchAll(/\bfire\("(\w+)"/g)].map((match) => match[1]);
    assert.ok(fired.length > 5, `${bridge} fires events`);
    for (const name of new Set(fired)) assert.ok(declared.has(name), `${bridge} fires ${name}, which no host declares`);
  }
});
