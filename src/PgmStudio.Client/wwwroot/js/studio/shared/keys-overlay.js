// keys-overlay.js — the two ways a reader meets the keymap: the sheet that lists it and the palette that
// runs it. Both are rendered from `keys.js`'s registry rather than from a written list, so neither can offer
// a chord the app does not have or omit one it does.

import * as Keys from "./keys.js";

let open = null;   // { root, kind, cleanup } while a panel is showing

/** Close whatever is open. Safe to call when nothing is. */
export function close() {
  if (!open) return;
  open.cleanup();
  open.root.remove();
  open = null;
}

function shell(kind, title) {
  close();
  const root = document.createElement("div");
  root.className = "keys-overlay";
  root.innerHTML = `<div class="keys-backdrop"></div><div class="keys-panel keys-panel--${kind}" role="dialog"
      aria-modal="true" aria-label="${title}"></div>`;
  document.body.appendChild(root);
  const panel = root.querySelector(".keys-panel");
  const onKey = (e) => { if (e.key === "Escape") { e.preventDefault(); e.stopPropagation(); close(); } };
  root.querySelector(".keys-backdrop").addEventListener("click", close);
  // Capture, so Escape closes the panel before any registered binding sees it.
  document.addEventListener("keydown", onKey, true);
  open = { root, kind, cleanup: () => document.removeEventListener("keydown", onKey, true) };
  return panel;
}

const escape = (text) => String(text).replace(/[&<>"]/g, ch => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" }[ch]));

const chordHtml = (keys) => keys.map(chord => `<kbd class="keys-chord">${escape(chord)}</kbd>`).join(" ");

/** Every live binding, by group — what `?` shows. */
export function openSheet() {
  const groups = Keys.sheet();
  const panel = shell("sheet", "Keyboard shortcuts");
  panel.innerHTML = `
    <header class="keys-head"><h2>Keyboard</h2><span class="keys-hint">Esc to close</span></header>
    <div class="keys-groups">${groups.map(group => `
      <section class="keys-group">
        <h3>${escape(group.group)}</h3>
        ${group.items.map(item => `<div class="keys-row ${item.available ? "" : "keys-row--off"}"
          title="${item.available ? "" : "Not available right now"}"><span class="keys-row-keys">${chordHtml(item.keys)}</span>
          <span class="keys-row-label">${escape(item.label)}</span></div>`).join("")}
      </section>`).join("")}</div>`;
  if (!groups.length) panel.querySelector(".keys-groups").innerHTML = `<p class="keys-empty">Nothing is bound here.</p>`;
}

/** Every live binding by name, filtered as it is typed — what Ctrl/⌘K shows. Enter runs the highlighted row. */
export function openPalette() {
  const all = Keys.commands();
  const panel = shell("palette", "Command palette");
  panel.innerHTML = `
    <div class="keys-search"><input class="keys-search-input" type="text" placeholder="Run a command…"
         aria-label="Filter commands" autocomplete="off" spellcheck="false" /></div>
    <div class="keys-list" role="listbox"></div>`;
  const input = panel.querySelector(".keys-search-input");
  const list = panel.querySelector(".keys-list");
  let shown = all;
  let at = 0;

  const draw = () => {
    list.innerHTML = shown.length
      ? shown.map((cmd, i) => `<button class="keys-item ${i === at ? "keys-item--at" : ""}" data-at="${i}"
           role="option" aria-selected="${i === at}"><span class="keys-item-label">${escape(cmd.label)}</span>
           <span class="keys-item-group">${escape(cmd.group)}</span>
           <span class="keys-item-keys">${chordHtml(cmd.keys)}</span></button>`).join("")
      : `<p class="keys-empty">Nothing matches.</p>`;
    list.querySelector(".keys-item--at")?.scrollIntoView({ block: "nearest" });
  };

  const runAt = (index) => {
    const cmd = shown[index];
    if (!cmd) return;
    close();
    cmd.run();
  };

  input.addEventListener("input", () => {
    const needle = input.value.trim().toLowerCase();
    shown = needle ? all.filter(cmd => `${cmd.label} ${cmd.group}`.toLowerCase().includes(needle)) : all;
    at = 0;
    draw();
  });
  input.addEventListener("keydown", (e) => {
    if (e.key === "ArrowDown") { e.preventDefault(); at = Math.min(at + 1, shown.length - 1); draw(); }
    else if (e.key === "ArrowUp") { e.preventDefault(); at = Math.max(at - 1, 0); draw(); }
    else if (e.key === "Enter") { e.preventDefault(); runAt(at); }
  });
  list.addEventListener("click", (e) => {
    const item = e.target.closest?.(".keys-item");
    if (item) runAt(Number(item.dataset.at));
  });

  draw();
  input.focus();
}

/** The bindings that reach the overlay itself, registered as an owner like any other so they are listed in
 *  the sheet they open. Held at a low priority: a tool that genuinely needs one of these chords wins. */
export function registerOverlayKeys() {
  Keys.register("overlay", [
    { id: "keys.sheet", keys: ["?", "shift+/"], label: "Show the keyboard shortcuts", group: "Everywhere",
      priority: -10, run: openSheet },
    { id: "keys.palette", keys: "mod+k", label: "Run a command by name", group: "Everywhere",
      priority: -10, inField: true, run: openPalette },
  ]);
}
