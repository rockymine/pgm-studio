// keys.js — the studio's one keyboard owner. Every binding in the app is registered here, one document
// listener serves them all, and the help sheet and the command palette are rendered from the same registry
// the dispatcher reads, so neither can describe a key the app does not have.
//
// An entry is `{ id, keys, label, group, run, when?, priority?, inField?, passive? }`. `label` and `group`
// are REQUIRED and `register` throws without them: a binding that cannot be listed is a binding nobody finds,
// which is the state this module exists to end.
//
//   keys      one chord or several — "mod+z", ["Delete", "Backspace"], "?"
//   run(ev)   what the chord does; the dispatcher preventDefaults unless `passive`
//   when()    whether the entry is live right now — a canvas that is hidden answers false
//   priority  higher wins a contested chord; ties break on registration order, latest first
//   inField   true to fire while a text field has focus (Escape, save); default false
//   passive   true to let the browser keep the event after `run`
//
// An owner registers a whole set and drops it by name, so a tool that unmounts cannot leave a chord behind
// pointing at a canvas that is gone.

/** `mod` is the platform's own command key: ⌘ where the platform has one, Ctrl everywhere else. */
export const isApple = typeof navigator !== "undefined" && /Mac|iPhone|iPad/.test(navigator.platform || "");

/** The chord a key event is, in the one spelling `normalize` also produces. Order is fixed — mod, alt,
 *  shift — so "shift+mod+z" and "mod+shift+z" are the same chord and are looked up as one. */
export function chordOf(e) {
  const mod = isApple ? e.metaKey : e.ctrlKey;
  // The other platform's modifier is not "mod" but is still a modifier: Ctrl+click on a Mac is a context
  // menu, so a Mac chord that names ctrl is written "ctrl+" and matched here.
  const parts = [];
  if (mod) parts.push("mod");
  if (isApple ? e.ctrlKey : e.metaKey) parts.push("ctrl");
  if (e.altKey) parts.push("alt");
  if (e.shiftKey) parts.push("shift");
  parts.push(keyName(e));
  return parts.join("+");
}

/** The key's own name, lowercased for letters and digits and left as-is for the named keys, so "Escape"
 *  stays "escape" and "ArrowLeft" stays "arrowleft". A shifted letter reports its uppercase form, and the
 *  chord already carries "shift", so the letter is folded down rather than listed twice. */
function keyName(e) {
  const k = e.key;
  if (!k) return "";
  return k === " " ? "space" : k.toLowerCase();
}

/** One written chord in the spelling `chordOf` produces. Accepts any modifier order and any case. */
export function normalize(chord) {
  // A literal space is the key named "space" — trimming it away first would leave the chord empty.
  const raw = String(chord).split("+")
    .map(part => (part === " " ? "space" : part.trim().toLowerCase()))
    .filter(Boolean);
  if (!raw.length) return "";
  if (raw.length === 1) return raw[0];
  const key = raw[raw.length - 1];
  const mods = new Set(raw.slice(0, -1).map(m => (m === "cmd" || m === "meta" || m === "ctrl+cmd" ? "mod" : m)));
  // "ctrl" written on a platform where ctrl IS the command key means the command key.
  if (!isApple && mods.has("ctrl")) { mods.delete("ctrl"); mods.add("mod"); }
  const order = ["mod", "ctrl", "alt", "shift"].filter(m => mods.has(m));
  return [...order, key].join("+");
}

/** How a chord is shown to a reader — ⌘ on Apple, Ctrl elsewhere, and a named key in Title case. */
export function display(chord) {
  const parts = normalize(chord).split("+");
  const key = parts.pop();
  const shown = parts.map(m => (m === "mod" ? (isApple ? "⌘" : "Ctrl")
                             : m === "ctrl" ? (isApple ? "⌃" : "Ctrl")
                             : m === "alt" ? (isApple ? "⌥" : "Alt")
                             : "Shift"));
  const named = { arrowleft: "←", arrowright: "→", arrowup: "↑", arrowdown: "↓",
                  escape: "Esc", " ": "Space", space: "Space", delete: "Del", backspace: "⌫",
                  enter: "Enter", tab: "Tab" };
  shown.push(named[key] ?? (key.length === 1 ? key.toUpperCase() : key[0].toUpperCase() + key.slice(1)));
  return shown.join(isApple ? "" : "+");
}

/** Whether the event landed in something that takes typing — where a bare letter is a letter, not a tool. */
export function inTextField(target) {
  const el = target;
  if (!el || !el.tagName) return false;
  if (el.isContentEditable) return true;
  return ["INPUT", "TEXTAREA", "SELECT"].includes(el.tagName);
}

const owners = new Map();   // ownerId → { seq, entries: [normalized entry] }
let seq = 0;

function check(entry) {
  if (!entry || typeof entry.run !== "function") throw new Error("[keys] an entry needs a run()");
  if (!entry.label) throw new Error(`[keys] entry ${entry.id ?? "?"} has no label — a binding nobody can list is a binding nobody finds`);
  if (!entry.group) throw new Error(`[keys] entry ${entry.id ?? "?"} has no group`);
  const keys = Array.isArray(entry.keys) ? entry.keys : [entry.keys];
  if (!keys.length || keys.some(k => !k)) throw new Error(`[keys] entry ${entry.label} has no chord`);
  return { ...entry, chords: keys.map(normalize), priority: entry.priority ?? 0 };
}

/** Register an owner's whole set, replacing whatever it had. */
export function register(ownerId, entries) {
  owners.set(ownerId, { seq: ++seq, entries: (entries ?? []).map(check) });
}

/** Drop an owner's set — called when a tool unmounts. */
export function unregister(ownerId) { owners.delete(ownerId); }

/** Every registered entry, most recently registered first, higher priority before lower, each carrying
 *  whether it can run right now. An owner drops its set when it unmounts, so what is here is what exists. */
export function all() {
  const rows = [];
  for (const [ownerId, owner] of owners)
    for (const entry of owner.entries) rows.push({ ...entry, ownerId, seq: owner.seq, available: !entry.when || entry.when() });
  return rows.sort((a, b) => (b.priority - a.priority) || (b.seq - a.seq));
}

/** Every entry that can run right now — what the dispatcher chooses among. */
export function live() { return all().filter(entry => entry.available); }

/** The entry a chord runs right now, or null. */
export function match(chord, typing) {
  return live().find(entry => entry.chords.includes(chord) && (!typing || entry.inField)) ?? null;
}

/** The help sheet: one block per group, in the order the groups were first registered. It lists every
 *  registered binding rather than only the ones that can run now — a chord that needs a selection is still
 *  a chord the tool has, and a sheet that hid it would teach it to nobody. `available` says which is which. */
export function sheet() {
  const groups = new Map();
  for (const entry of all()) {
    if (!groups.has(entry.group)) groups.set(entry.group, []);
    const rows = groups.get(entry.group);
    if (!rows.some(row => row.id === entry.id)) {
      rows.push({ id: entry.id, keys: entry.chords.map(display), label: entry.label, available: entry.available });
    }
  }
  return [...groups].map(([group, items]) => ({ group, items }));
}

/** What the palette offers — every live entry, by name, with the chord that also runs it. */
export function commands() {
  return live().map(entry => ({ id: entry.id, label: entry.label, group: entry.group,
                                keys: entry.chords.map(display), run: entry.run }));
}

let installed = false;
let onDispatch = null;

/** Called after any entry runs — the overlay uses it to close itself. */
export function onAfterRun(fn) { onDispatch = fn; }

// A held arrow is meant to repeat, so a repeat is dispatched like any other press; what is never dispatched
// is an event something else already claimed.
function handle(e) {
  if (e.defaultPrevented || e.isComposing) return;
  const typing = inTextField(e.target);
  const entry = match(chordOf(e), typing);
  if (!entry) return;
  if (!entry.passive) e.preventDefault();
  entry.run(e);
  onDispatch?.(entry);
}

/** Install the one listener. Guarded on there being a document, so the registry — which is the half worth
 *  testing — imports under Node's test runner without a DOM. */
export function install() {
  if (installed || typeof document === "undefined") return;
  installed = true;
  document.addEventListener("keydown", handle);
}

install();
