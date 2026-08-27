// The keyboard registry. What these hold is the property the whole module exists for: a binding cannot be
// registered without the words that list it, so the sheet and the palette — both rendered from this
// registry — describe exactly the chords the dispatcher will run, and nothing else.
import { test } from "node:test";
import assert from "node:assert/strict";

import { register, unregister, all, live, match, sheet, commands, normalize, display, inTextField, chordOf }
  from "../../src/PgmStudio.Client/wwwroot/js/studio/shared/keys.js";

const entry = (over = {}) => ({ id: "t.one", keys: "mod+z", label: "Undo", group: "Everywhere", run() {}, ...over });

function clear() { for (const owner of ["a", "b", "c"]) unregister(owner); }

// ── one spelling ──────────────────────────────────────────────────────────────
test("a chord has one spelling however it is written", () => {
  assert.equal(normalize("Ctrl+Shift+Z"), normalize("shift+ctrl+z"));
  assert.equal(normalize("MOD+K"), "mod+k");
  assert.equal(normalize("Escape"), "escape");
  assert.equal(normalize("?"), "?");
});

test("a key event and a written chord meet in that spelling", () => {
  const event = { key: "z", ctrlKey: true, metaKey: false, altKey: false, shiftKey: true };
  assert.equal(chordOf(event), normalize("ctrl+shift+z"));
});

test("a shifted letter is folded down — the chord already carries the shift", () => {
  assert.equal(chordOf({ key: "P", shiftKey: true, ctrlKey: false, metaKey: false, altKey: false }),
               normalize("shift+p"));
});

test("space is a named key rather than a blank", () => {
  assert.equal(normalize(" "), "space");
  assert.equal(chordOf({ key: " ", ctrlKey: false, metaKey: false, altKey: false, shiftKey: false }), "space");
});

test("a chord shown to a reader names its keys", () => {
  assert.match(display("mod+z"), /Z$/);
  assert.equal(display("arrowleft"), "←");
  assert.equal(display("escape"), "Esc");
  assert.equal(display("shift+p"), "Shift+P");
});

// ── the enforcement ───────────────────────────────────────────────────────────
test("a binding without the words that list it is refused", () => {
  clear();
  assert.throws(() => register("a", [entry({ label: undefined })]), /label/);
  assert.throws(() => register("a", [entry({ group: undefined })]), /group/);
  assert.throws(() => register("a", [entry({ run: undefined })]), /run/);
  assert.throws(() => register("a", [entry({ keys: "" })]), /chord/);
});

test("a refused set leaves nothing behind", () => {
  clear();
  try { register("a", [entry(), entry({ id: "t.two", keys: "mod+y", label: undefined })]); } catch { /* expected */ }
  assert.equal(match(normalize("mod+z"), false), null);
});

// ── what runs ─────────────────────────────────────────────────────────────────
test("a registered chord matches, and an unregistered one does not", () => {
  clear();
  register("a", [entry()]);
  assert.equal(match(normalize("mod+z"), false)?.label, "Undo");
  assert.equal(match(normalize("mod+q"), false), null);
  clear();
});

test("an owner's set replaces its own and drops with it", () => {
  clear();
  register("a", [entry()]);
  register("a", [entry({ id: "t.other", keys: "mod+y", label: "Redo" })]);
  assert.equal(match(normalize("mod+z"), false), null, "the replaced set is gone");
  assert.equal(match(normalize("mod+y"), false)?.label, "Redo");
  unregister("a");
  assert.equal(match(normalize("mod+y"), false), null);
});

test("a hidden owner runs nothing — `when` is what keeps a hidden canvas quiet", () => {
  clear();
  let visible = false;
  register("a", [entry({ when: () => visible })]);
  assert.equal(match(normalize("mod+z"), false), null);
  visible = true;
  assert.equal(match(normalize("mod+z"), false)?.label, "Undo");
  clear();
});

test("the later owner wins a contested chord, and priority beats registration order", () => {
  clear();
  register("a", [entry({ label: "First" })]);
  register("b", [entry({ label: "Second" })]);
  assert.equal(match(normalize("mod+z"), false).label, "Second", "most recently registered wins");
  register("c", [entry({ label: "Loud", priority: 5 })]);
  register("b", [entry({ label: "Late" })]);
  assert.equal(match(normalize("mod+z"), false).label, "Loud", "priority outranks order");
  clear();
});

test("typing in a field silences every chord but the ones that asked to survive it", () => {
  clear();
  register("a", [entry({ id: "t.tool", keys: "p", label: "Polygon" }),
                 entry({ id: "t.save", keys: "mod+s", label: "Save", inField: true })]);
  assert.equal(match(normalize("p"), true), null, "a bare letter is a letter while typing");
  assert.equal(match(normalize("mod+s"), true)?.label, "Save");
  assert.equal(match(normalize("p"), false)?.label, "Polygon");
  clear();
});

test("what counts as a field is the thing that takes typing", () => {
  assert.equal(inTextField({ tagName: "INPUT" }), true);
  assert.equal(inTextField({ tagName: "TEXTAREA" }), true);
  assert.equal(inTextField({ tagName: "SELECT" }), true);
  assert.equal(inTextField({ tagName: "DIV", isContentEditable: true }), true);
  assert.equal(inTextField({ tagName: "DIV" }), false);
  assert.equal(inTextField(null), false);
});

// ── the sheet and the palette are the registry ────────────────────────────────
test("the sheet lists every live binding, by group", () => {
  clear();
  register("a", [entry({ id: "t.undo", keys: "mod+z", label: "Undo", group: "Everywhere" }),
                 entry({ id: "t.poly", keys: "p", label: "Polygon", group: "Tools" })]);
  const groups = sheet();
  assert.deepEqual(groups.map(g => g.group).sort(), ["Everywhere", "Tools"]);
  assert.equal(groups[0].items[0].label, "Undo");
  assert.match(groups[0].items[0].keys[0], /Z$/, "the sheet shows the chord as a reader meets it");
  clear();
});

test("a binding that cannot run now is still listed, marked as unavailable", () => {
  clear();
  register("a", [entry({ id: "t.gone", label: "Delete", when: () => false })]);
  // Listed: a chord hidden until it happens to work is a chord nobody learns.
  const [group] = sheet();
  assert.equal(group.items[0].label, "Delete");
  assert.equal(group.items[0].available, false);
  // But not offered as something to run, and not dispatched.
  assert.deepEqual(commands(), []);
  assert.equal(match(normalize("mod+z"), false), null);
  clear();
});

test("all() carries availability; live() is what the dispatcher chooses among", () => {
  clear();
  register("a", [entry({ id: "t.on", keys: "p", label: "Polygon" }),
                 entry({ id: "t.off", keys: "l", label: "Lasso", when: () => false })]);
  assert.equal(all().length, 2);
  assert.deepEqual(all().map(row => row.available).sort(), [false, true]);
  assert.deepEqual(live().map(row => row.label), ["Polygon"]);
  clear();
});

test("the palette offers a runnable entry for every live binding", () => {
  clear();
  let ran = 0;
  register("a", [entry({ label: "Fit", run: () => { ran++; } })]);
  const [cmd] = commands();
  assert.equal(cmd.label, "Fit");
  cmd.run();
  assert.equal(ran, 1, "the palette runs the same function the chord does");
  clear();
});

test("a chord bound to several keys is listed once, with both", () => {
  clear();
  register("a", [entry({ id: "t.del", keys: ["delete", "backspace"], label: "Delete", group: "Canvas" })]);
  const [group] = sheet();
  assert.equal(group.items.length, 1);
  assert.deepEqual(group.items[0].keys, [display("delete"), display("backspace")]);
  assert.equal(match(normalize("backspace"), false)?.label, "Delete");
  clear();
});

test("live() is ordered so the first match is the one that runs", () => {
  clear();
  register("a", [entry({ label: "Low" })]);
  register("b", [entry({ label: "High", priority: 3 })]);
  assert.equal(live()[0].label, "High");
  clear();
});
