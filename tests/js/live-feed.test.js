// The live feed and the iso preview built on it: a request asks only for the latest edit, a reply to an
// older one is dropped, and a refusal carries the sentence out of the studio's envelope.
import { test, beforeEach, afterEach } from "node:test";
import assert from "node:assert/strict";

import { liveFeed, refusalText, UNREACHABLE } from "../../src/PgmStudio.Client/wwwroot/js/studio/bridge/live-feed.js";
import { isoPreview } from "../../src/PgmStudio.Client/wwwroot/js/studio/bridge/iso-preview.js";

const realFetch = globalThis.fetch;
let calls;      // every request made, with the deferred reply the test settles by hand

/** A reply the test settles itself, so an older request can be answered after a newer one. */
function deferred() {
  let settle;
  const promise = new Promise((resolve) => { settle = resolve; });
  return { promise, settle };
}

const reply = (status, body) => ({
  ok: status >= 200 && status < 300, status, json: async () => body,
});

beforeEach(() => {
  calls = [];
  globalThis.fetch = (url, options) => {
    const pending = deferred();
    calls.push({ url, body: options.body, settle: pending.settle });
    return pending.promise;
  };
});

afterEach(() => { globalThis.fetch = realFetch; });

const tick = () => new Promise((resolve) => setTimeout(resolve, 0));
const wait = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

test("a reply to an older request is dropped when a newer one is in flight", async () => {
  const answered = [];
  const feed = liveFeed({ url: "/x", body: () => "doc", onAnswer: (data) => answered.push(data) });
  const first = feed.request();
  const second = feed.request();
  calls[1].settle(reply(200, "new"));
  await second;
  calls[0].settle(reply(200, "old"));
  await first;
  assert.deepEqual(answered, ["new"]);
});

test("an answer carries the body it answers", async () => {
  let version = 1;
  const seen = [];
  const feed = liveFeed({ url: "/x", body: () => `v${version}`, onAnswer: (data, sent) => seen.push([data, sent]) });
  const asked = feed.request();
  version = 2;
  calls[0].settle(reply(200, "answer"));
  await asked;
  assert.deepEqual(seen, [["answer", "v1"]]);
});

test("edits within the delay make one request, asked about the document as it stands when they settle", async () => {
  let version = 0;
  const feed = liveFeed({ url: "/x", body: () => `v${version}`, delay: 20, onAnswer: () => {} });
  for (version = 1; version <= 3; version++) { feed.schedule(); await wait(5); }
  assert.equal(calls.length, 0);
  await wait(40);
  assert.equal(calls.length, 1);
  assert.equal(calls[0].body, "v4");
});

test("a scheduled request with now fires on the next tick rather than after the delay", async () => {
  const feed = liveFeed({ url: "/x", body: () => "doc", delay: 10_000, onAnswer: () => {} });
  feed.schedule({ now: true });
  await tick();
  assert.equal(calls.length, 1);
});

test("a request made directly cancels the wait in progress", async () => {
  const feed = liveFeed({ url: "/x", body: () => "doc", delay: 20, onAnswer: () => {} });
  feed.schedule();
  feed.request();
  await wait(40);
  assert.equal(calls.length, 1);
});

test("cancel drops both the wait and the answer to a request already in flight", async () => {
  const answered = [];
  const feed = liveFeed({ url: "/x", body: () => "doc", delay: 20, onAnswer: (data) => answered.push(data) });
  const asked = feed.request();
  feed.schedule();
  feed.cancel();
  calls[0].settle(reply(200, "late"));
  await asked;
  await wait(40);
  assert.equal(calls.length, 1);
  assert.deepEqual(answered, []);
});

test("a scheduled request is not sent when its gate is closed at the moment it fires", async () => {
  let open = true;
  const feed = liveFeed({ url: "/x", body: () => "doc", delay: 10, when: () => open, onAnswer: () => {} });
  feed.schedule();
  open = false;
  await wait(30);
  assert.equal(calls.length, 0);
});

test("an empty url sends nothing", async () => {
  const feed = liveFeed({ url: () => null, body: () => "doc", onAnswer: () => assert.fail("answered") });
  await feed.request();
  assert.equal(calls.length, 0);
});

test("a refusal reaches onRefused with the envelope's sentence and the status", async () => {
  const refused = [];
  const feed = liveFeed({ url: "/x", body: () => "doc", onAnswer: () => assert.fail("answered"),
    onRefused: (sentence, status) => refused.push([sentence, status]) });
  const asked = feed.request();
  calls[0].settle(reply(422, { error: "E1", message: "Nothing to build." }));
  await asked;
  assert.deepEqual(refused, [["Nothing to build.", 422]]);
});

test("a refusal overtaken by a newer request is dropped", async () => {
  const refused = [];
  const feed = liveFeed({ url: "/x", body: () => "doc", onAnswer: () => {}, onRefused: (sentence) => refused.push(sentence) });
  const first = feed.request();
  const second = feed.request();
  calls[0].settle(reply(400, { message: "old" }));
  await first;
  calls[1].settle(reply(200, "fine"));
  await second;
  assert.deepEqual(refused, []);
});

test("a request that cannot be made reaches onUnreachable, and is silent without one", async () => {
  globalThis.fetch = async () => { throw new Error("offline"); };
  const unreachable = [];
  const feed = liveFeed({ url: "/x", body: () => "doc", onAnswer: () => assert.fail("answered"),
    onUnreachable: (sentence) => unreachable.push(sentence) });
  await feed.request();
  assert.deepEqual(unreachable, [UNREACHABLE]);
  await liveFeed({ url: "/x", body: () => "doc", onAnswer: () => assert.fail("answered") }).request();
});

test("refusalText falls back from message to error to the status", async () => {
  assert.equal(await refusalText(reply(400, { message: "m", error: "e" })), "m");
  assert.equal(await refusalText(reply(400, { error: "e" })), "e");
  assert.equal(await refusalText(reply(502, {})), "The studio returned an error (HTTP 502).");
  assert.equal(await refusalText({ status: 500, json: async () => { throw new Error("not json"); } }),
    "The studio returned an error (HTTP 500).");
});

// ── the iso preview ──────────────────────────────────────────────────────────

function recordingCanvas({ webgl = true } = {}) {
  const drawn = [];
  const record = { drawn, hidden: 0 };
  return Object.assign(record, {
    enterIso: async () => webgl,
    drawIso: (mesh, yaw, bounds) => drawn.push({ mesh, yaw, bounds }),
    hideIso: () => { record.hidden++; },
  });
}

function previewOn(canvas, events, { url = "/columns" } = {}) {
  let document = "doc-1";
  const preview = isoPreview({
    canvas, url: () => url, missing: "No map.", state: () => document, bounds: () => "bounds",
    onBuilt: (payload, hidden) => events.push(["built", payload.layers, hidden]),
    onUnavailable: (reason) => events.push(["unavailable", reason]),
  });
  return { preview, edit: (next) => { document = next; } };
}

const payload = (layers) => ({ layers, palette: [], runs: [] });

test("a browser without WebGL reports an empty reason and asks nothing", async () => {
  const events = [];
  const { preview } = previewOn(recordingCanvas({ webgl: false }), events);
  await preview.show();
  assert.deepEqual(events, [["unavailable", ""]]);
  assert.equal(calls.length, 0);
});

test("a refused build leaves the preview with the studio's sentence", async () => {
  const events = [];
  const canvas = recordingCanvas();
  const { preview } = previewOn(canvas, events);
  const shown = preview.show();
  await tick();
  calls[0].settle(reply(422, { message: "SK13 refused it." }));
  await shown;
  assert.deepEqual(events, [["unavailable", "SK13 refused it."]]);
  assert.equal(canvas.hidden, 1);
});

test("a missing target names itself rather than asking", async () => {
  const events = [];
  const { preview } = previewOn(recordingCanvas(), events, { url: null });
  await preview.show();
  assert.deepEqual(events, [["unavailable", "No map."]]);
  assert.equal(calls.length, 0);
});

test("a build that lands after the preview was left draws nothing and reports nothing", async () => {
  const events = [];
  const canvas = recordingCanvas();
  const { preview } = previewOn(canvas, events);
  const shown = preview.show();
  await tick();
  preview.hide();
  calls[0].settle(reply(200, payload(["ground"])));
  await shown;
  assert.deepEqual(events, []);
  assert.equal(canvas.drawn.length, 0);
});

test("re-entering an untouched document draws the kept mesh; an edit asks again", async () => {
  const events = [];
  const canvas = recordingCanvas();
  const { preview } = previewOn(canvas, events);
  let shown = preview.show();
  await tick();
  calls[0].settle(reply(200, payload(["ground"])));
  await shown;
  assert.equal(canvas.drawn.length, 1);

  preview.hide();
  await preview.show();
  assert.equal(calls.length, 1);
  assert.equal(canvas.drawn.length, 2);

  preview.drop();
  preview.hide();
  shown = preview.show();
  await tick();
  assert.equal(calls.length, 2);
  calls[1].settle(reply(200, payload(["ground"])));
  await shown;
});

test("a hidden layer stays hidden across a rebuild unless the board no longer has it", async () => {
  const events = [];
  const canvas = recordingCanvas();
  const { preview } = previewOn(canvas, events);
  await preview.setLayerShown("deck", false);

  let shown = preview.show();
  await tick();
  calls[0].settle(reply(200, payload(["ground", "deck"])));
  await shown;
  assert.deepEqual(events.at(-1), ["built", ["ground", "deck"], ["deck"]]);

  preview.drop();
  preview.hide();
  shown = preview.show();
  await tick();
  calls[1].settle(reply(200, payload(["ground"])));
  await shown;
  assert.deepEqual(events.at(-1), ["built", ["ground"], []]);
});

test("rotating turns the preview a quarter and redraws it", async () => {
  const events = [];
  const canvas = recordingCanvas();
  const { preview } = previewOn(canvas, events);
  const shown = preview.show();
  await tick();
  calls[0].settle(reply(200, payload(["ground"])));
  await shown;
  preview.rotate();
  assert.deepEqual(canvas.drawn.map((draw) => draw.yaw), [30, 120]);
});
