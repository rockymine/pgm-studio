// Unit tests for the plan-editor inspect presentation helpers (pure, no DOM).
import { test } from "node:test";
import assert from "node:assert/strict";

import {
  DEFAULT_OVERLAYS, parseOverlays,
} from "../../src/PgmStudio.Client/wwwroot/js/studio/plan/plan-inspect.js";

test("DEFAULT_OVERLAYS keeps labels off, interfaces/frontline/violations on", () => {
  assert.deepEqual(DEFAULT_OVERLAYS, { interfaces: true, labels: false, frontline: true, violations: true });
});

test("parseOverlays defaults interfaces/frontline/violations on and labels off", () => {
  assert.deepEqual(parseOverlays(null), DEFAULT_OVERLAYS);
  assert.deepEqual(parseOverlays("{}"), { interfaces: true, labels: false, frontline: true, violations: true });
  assert.deepEqual(parseOverlays('{"interfaces":false}'), { interfaces: false, labels: false, frontline: true, violations: true });
});

test("parseOverlays turns labels on only when explicitly true, and persists it", () => {
  assert.deepEqual(parseOverlays('{"labels":true}'), { interfaces: true, labels: true, frontline: true, violations: true });
  assert.deepEqual(parseOverlays('{"labels":false}'), { interfaces: true, labels: false, frontline: true, violations: true });
});

test("parseOverlays turns violations off only when explicitly false", () => {
  assert.deepEqual(parseOverlays('{"violations":false}'), { interfaces: true, labels: false, frontline: true, violations: false });
  assert.deepEqual(parseOverlays('{"violations":true}'), { interfaces: true, labels: false, frontline: true, violations: true });
});

test("parseOverlays ignores a legacy gaps key and defaults labels off", () => {
  assert.deepEqual(parseOverlays('{"interfaces":true,"gaps":true,"frontline":true}'), { interfaces: true, labels: false, frontline: true, violations: true });
  assert.deepEqual(parseOverlays('{"interfaces":true,"gaps":false,"frontline":false}'), { interfaces: true, labels: false, frontline: false, violations: true });
});

test("parseOverlays falls back to defaults on garbage", () => {
  assert.deepEqual(parseOverlays("not json"), DEFAULT_OVERLAYS);
});
