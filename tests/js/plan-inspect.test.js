// Unit tests for the plan-editor inspect presentation helpers (pure, no DOM).
import { test } from "node:test";
import assert from "node:assert/strict";

import {
  DEFAULT_OVERLAYS, sortFindings, parseOverlays,
} from "../../src/PgmStudio.Client/wwwroot/js/studio/plan/plan-inspect.js";

test("sortFindings puts errors before lint, stable within a group", () => {
  const input = [
    { severity: "lint", rule: "G2", message: "a" },
    { severity: "error", message: "b" },
    { severity: "lint", rule: "G5", message: "c" },
    { severity: "error", message: "d" },
  ];
  const out = sortFindings(input);
  assert.deepEqual(out.map((f) => f.message), ["b", "d", "a", "c"]);
});

test("sortFindings tolerates an empty / missing list", () => {
  assert.deepEqual(sortFindings([]), []);
  assert.deepEqual(sortFindings(undefined), []);
});

test("sortFindings does not mutate its input", () => {
  const input = [{ severity: "lint", message: "a" }, { severity: "error", message: "b" }];
  sortFindings(input);
  assert.deepEqual(input.map((f) => f.message), ["a", "b"]);
});

test("DEFAULT_OVERLAYS keeps labels off, interfaces and frontline on", () => {
  assert.deepEqual(DEFAULT_OVERLAYS, { interfaces: true, labels: false, frontline: true });
});

test("parseOverlays defaults interfaces/frontline on and labels off", () => {
  assert.deepEqual(parseOverlays(null), DEFAULT_OVERLAYS);
  assert.deepEqual(parseOverlays("{}"), { interfaces: true, labels: false, frontline: true });
  assert.deepEqual(parseOverlays('{"interfaces":false}'), { interfaces: false, labels: false, frontline: true });
});

test("parseOverlays turns labels on only when explicitly true, and persists it", () => {
  assert.deepEqual(parseOverlays('{"labels":true}'), { interfaces: true, labels: true, frontline: true });
  assert.deepEqual(parseOverlays('{"labels":false}'), { interfaces: true, labels: false, frontline: true });
});

test("parseOverlays ignores a legacy gaps key and defaults labels off", () => {
  assert.deepEqual(parseOverlays('{"interfaces":true,"gaps":true,"frontline":true}'), { interfaces: true, labels: false, frontline: true });
  assert.deepEqual(parseOverlays('{"interfaces":true,"gaps":false,"frontline":false}'), { interfaces: true, labels: false, frontline: false });
});

test("parseOverlays falls back to defaults on garbage", () => {
  assert.deepEqual(parseOverlays("not json"), DEFAULT_OVERLAYS);
});
