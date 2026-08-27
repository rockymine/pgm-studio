// The two-level selection rule both authoring canvases answer a click with. The geometry that finds a group
// and a member is each canvas's own; what is here is the rule over whatever they found, which is the half
// that has to be identical between them — two tools with two grouping models is two things to learn for one
// idea.
import { test } from "node:test";
import assert from "node:assert/strict";

import { resolvePick } from "../../src/PgmStudio.Client/wwwroot/js/studio/shared/pick.js";

const pick = (over) => resolvePick({ group: "i1", member: "s1", scope: null, unit: "group", ...over });

// ── with nothing entered, the caller's unit decides ───────────────────────────
test("a plain click picks the group where the group is the unit", () => {
  assert.deepEqual(pick({}), { pick: "group", id: "i1", scope: null });
});

test("a plain click picks the member where the member is the unit, and enters its group", () => {
  assert.deepEqual(pick({ unit: "member" }), { pick: "member", id: "s1", scope: "i1" });
});

test("a click on empty ground picks nothing, either way", () => {
  assert.deepEqual(pick({ group: null, member: null }), { pick: "none", id: null, scope: null });
  assert.deepEqual(pick({ group: null, member: null, unit: "member" }), { pick: "none", id: null, scope: null });
});

test("a click on a group with no member under it picks the group, member-unit or not", () => {
  assert.deepEqual(pick({ member: null }), { pick: "group", id: "i1", scope: null });
  // Nothing to reach: the click lands on ground the group covers but no member fills.
  assert.deepEqual(pick({ member: null, unit: "member" }), { pick: "none", id: null, scope: null });
});

// ── the modifiers ─────────────────────────────────────────────────────────────
test("deep-select reaches the member and enters its group — no click count involved", () => {
  assert.deepEqual(pick({ deep: true }), { pick: "member", id: "s1", scope: "i1" });
});

test("deep-select with nothing under the cursor picks nothing and enters nothing", () => {
  assert.deepEqual(pick({ deep: true, member: null }), { pick: "none", id: null, scope: null });
});

test("select-parent reaches the group and leaves whatever was entered", () => {
  assert.deepEqual(pick({ up: true, scope: "i1", unit: "member" }), { pick: "group", id: "i1", scope: null });
});

test("select-parent outranks deep-select when both are held", () => {
  assert.deepEqual(pick({ up: true, deep: true }), { pick: "group", id: "i1", scope: null });
});

// ── the scope is what makes entering a state ──────────────────────────────────
test("inside a group, a click on one of its members reaches the member and stays in", () => {
  assert.deepEqual(resolvePick({ group: "i1", member: "s2", scope: "i1", unit: "group" }),
                   { pick: "member", id: "s2", scope: "i1" });
});

test("a second member is one click away, not another drill", () => {
  const first = pick({ deep: true });
  assert.equal(first.scope, "i1");
  const second = resolvePick({ group: "i1", member: "s2", scope: first.scope, unit: "group" });
  assert.equal(second.pick, "member");
  assert.equal(second.id, "s2");
  assert.equal(second.scope, "i1", "and the group is still entered after it");
});

test("a click on another group leaves the entered one, then lands as though nothing were entered", () => {
  assert.deepEqual(resolvePick({ group: "i2", member: "s9", scope: "i1", unit: "group" }),
                   { pick: "group", id: "i2", scope: null });
});

test("a click on empty ground leaves the entered group", () => {
  assert.deepEqual(resolvePick({ group: null, member: null, scope: "i1", unit: "group" }),
                   { pick: "none", id: null, scope: null });
});

test("inside a group, a click on ground it covers but no member fills leaves it", () => {
  assert.deepEqual(resolvePick({ group: "i1", member: null, scope: "i1", unit: "group" }),
                   { pick: "group", id: "i1", scope: null });
});

// ── the defaults are the safe ones ────────────────────────────────────────────
test("called with nothing, it picks nothing and enters nothing", () => {
  assert.deepEqual(resolvePick(), { pick: "none", id: null, scope: null });
  assert.deepEqual(resolvePick({}), { pick: "none", id: null, scope: null });
});
