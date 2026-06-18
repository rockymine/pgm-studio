// Minimal DOM stub so render-layer functions (which call document.createElementNS)
// can be unit-tested under `node --test` without a full DOM library. Elements record
// their tag + attributes; that's all the shape/SVG renderers touch.

function makeEl(tag) {
  const attrs = {};
  return {
    tagName: tag,
    attrs,
    style: {},
    children: [],
    setAttribute(k, v) { attrs[k] = String(v); },
    getAttribute(k) { return attrs[k]; },
    appendChild(c) { this.children.push(c); return c; },
    get firstChild() { return this.children[0] ?? null; },
    removeChild(c) { this.children = this.children.filter(x => x !== c); return c; },
  };
}

export function installDomStub() {
  globalThis.document = {
    createElementNS(_ns, tag) { return makeEl(tag); },
    createElement(tag) { return makeEl(tag); },
  };
}
