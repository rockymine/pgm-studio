// panel-resize.js — drag the `.sidebar-handle` bars to resize the editor panel they border. One delegated
// document-level listener serves every editor at once: the handles are (re)created by Blazor on each render,
// so per-element wiring would be fragile, but a single listener on `document` never goes stale. A handle
// resizes whichever panel it sits against — the left `.workspace-sidebar` before it (drag right → wider) or
// the right `.workspace-inspector` after it (drag right → narrower). The chosen width is written inline,
// overriding the shared `--sidebar-width` / `--inspector-width` token, and clamped to [MIN, MAX] so a panel
// can neither collapse to nothing nor crowd out the canvas. Read-only chrome — no persistence.

const MIN_WIDTH = 200;
const MAX_WIDTH = 560;

// The panel a handle controls, plus the sign mapping a rightward drag to a width delta: a left sidebar grows
// as the handle moves right (+1); a right inspector shrinks (−1). null when the handle borders no panel.
function targetOf(handle) {
  const prev = handle.previousElementSibling;
  if (prev && prev.classList.contains("workspace-sidebar")) return { el: prev, sign: 1 };
  const next = handle.nextElementSibling;
  if (next && next.classList.contains("workspace-inspector")) return { el: next, sign: -1 };
  return null;
}

let drag = null;   // { handle, el, sign, startX, startW } while a resize is live

function onPointerMove(e) {
  if (!drag) return;
  const w = drag.startW + drag.sign * (e.clientX - drag.startX);
  drag.el.style.width = `${Math.max(MIN_WIDTH, Math.min(MAX_WIDTH, w))}px`;
}

function endDrag() {
  if (!drag) return;
  drag.handle.classList.remove("sidebar-handle--dragging");
  document.body.style.cursor = "";
  document.body.style.userSelect = "";
  window.removeEventListener("pointermove", onPointerMove);
  window.removeEventListener("pointerup", endDrag);
  drag = null;
}

function onPointerDown(e) {
  if (e.button !== 0) return;                          // left-drag only
  const handle = e.target.closest?.(".sidebar-handle");
  if (!handle) return;
  const t = targetOf(handle);
  if (!t) return;
  e.preventDefault();
  drag = { handle, el: t.el, sign: t.sign, startX: e.clientX, startW: t.el.getBoundingClientRect().width };
  handle.classList.add("sidebar-handle--dragging");
  document.body.style.cursor = "ew-resize";           // keep the resize cursor even over the canvas
  document.body.style.userSelect = "none";            // no text selection while dragging
  window.addEventListener("pointermove", onPointerMove);
  window.addEventListener("pointerup", endDrag);
}

let installed = false;
export function installPanelResize() {
  if (installed) return;
  installed = true;
  document.addEventListener("pointerdown", onPointerDown);
}

installPanelResize();   // self-install on import
