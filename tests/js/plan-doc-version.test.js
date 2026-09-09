// The plan document's shape version, held against the C# constant that reads it.
//
// `PlanModel.CurrentVersion` decides what the server accepts and `plan-doc.js` decides what the editor
// writes, so the two are one number in two files and the editor is the half nobody compiles. When they
// last disagreed every plan authored in the UI was born stale and `PL15` refused it at the compile gate —
// the document was already in the current unit and only its label was wrong. This reads the constant out
// of the C# source rather than restating it, so a bump on either side fails here.
import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

import { PLAN_VERSION, emptyDoc, normalizeDoc }
  from "../../src/PgmStudio.Client/wwwroot/js/studio/plan/plan-doc.js";

const MODEL = "src/PgmStudio.Pgm/Plan/PlanModel.cs";

test("the editor writes the version the server reads", () => {
  const cs = readFileSync(MODEL, "utf8");
  const m = cs.match(/CurrentVersion\s*=\s*(\d+)/);
  assert.ok(m, `no CurrentVersion in ${MODEL}`);
  assert.equal(PLAN_VERSION, Number(m[1]),
    `plan-doc.js states ${PLAN_VERSION} and ${MODEL} reads ${m[1]} — a plan authored in the editor ` +
    "would be refused PL15");
});

test("a new document is born current", () => {
  assert.equal(emptyDoc().plan, PLAN_VERSION);
});

test("a stated version is preserved, and only an omitted one reads as current", () => {
  // A stale document has to stay stale: relabelling it would measure cells as blocks and move every
  // marker without saying so, which is the whole reason the gate exists.
  assert.equal(normalizeDoc({ plan: 1 }).plan, 1);
  assert.equal(normalizeDoc({}).plan, PLAN_VERSION);
});
