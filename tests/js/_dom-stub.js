// Minimal DOM stub so render-layer functions (which call document.createElementNS)
// can be unit-tested under `node --test` without a full DOM library. Elements record
// their tag + attributes; that's all the shape/SVG renderers touch.

function makeEl(tag) {
  const attrs = {};
  const listeners = {};
  return {
    tagName: tag,
    attrs,
    style: {},
    children: [],
    listeners,
    setAttribute(k, v) { attrs[k] = String(v); },
    getAttribute(k) { return attrs[k]; },
    appendChild(c) { this.children.push(c); return c; },
    get firstChild() { return this.children[0] ?? null; },
    removeChild(c) { this.children = this.children.filter(x => x !== c); return c; },
    // Enough of the event surface for the chrome that emits grabbable elements: a test can fire what a
    // pointer would and read back which grip the caller was handed.
    addEventListener(type, fn) { (listeners[type] ??= []).push(fn); },
    fire(type, event = {}) { for (const fn of listeners[type] ?? []) fn({ button: 0, stopPropagation() {}, preventDefault() {}, ...event }); },
  };
}

export function installDomStub() {
  globalThis.document = {
    createElementNS(_ns, tag) { return makeEl(tag); },
    createElement(tag) { return makeEl(tag); },
  };
}
