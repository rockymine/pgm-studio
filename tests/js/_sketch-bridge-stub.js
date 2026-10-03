// Mounts the real sketch bridge — and the real SketchCanvas under it — over stand-in DOM elements and a recording
// stand-in for the Blazor host, so a test can drive the handle verbs and read back every event the host would
// have been sent. Nothing is painted: the 2-D context accepts every call and draws none of them.
import { installDomStub } from "./_dom-stub.js";

installDomStub();

const context = new Proxy(function () {}, {
  get: (_target, key) => key === "canvas" ? {} : key === "measureText" ? () => ({ width: 1 }) : () => ({ addColorStop() {} }),
  set: () => true,
  apply: () => undefined,
});

const makeElement = globalThis.document.createElement;
globalThis.document.createElement = (tag) => {
  const element = makeElement(tag);
  element.getContext = () => context;
  return element;
};
globalThis.document.addEventListener = () => {};
globalThis.document.removeEventListener = () => {};
globalThis.document.activeElement = null;
globalThis.window = { addEventListener() {}, removeEventListener() {}, devicePixelRatio: 1 };
globalThis.requestAnimationFrame = (frame) => setTimeout(frame, 0);
globalThis.cancelAnimationFrame = clearTimeout;
globalThis.ResizeObserver = class { observe() {} disconnect() {} };
globalThis.getComputedStyle = () => ({ getPropertyValue: () => "" });
globalThis.Path2D = class {};
globalThis.matchMedia = () => ({ matches: false, addEventListener() {}, addListener() {} });

const standIn = () => ({
  style: {}, children: [], parentNode: null, firstChild: null, clientWidth: 600, clientHeight: 600,
  classList: { add() {}, remove() {}, toggle() {} },
  setAttribute() {}, getAttribute() {}, appendChild: (child) => child, removeChild() {}, insertBefore: (child) => child,
  addEventListener() {}, removeEventListener() {}, querySelector: () => null, contains: () => false,
  setPointerCapture() {}, releasePointerCapture() {},
  getBoundingClientRect: () => ({ left: 0, top: 0, width: 600, height: 600 }),
});

const { mount } = await import("../../src/PgmStudio.Client/wwwroot/js/studio/bridge/sketch-bridge.js");

/** A mounted bridge: `handle` is what the host calls, `fired` is every `[event, ...args]` the host was sent,
 *  and `firedOf(name)` is the argument lists of one event. `reject(name)` makes the host refuse that event, as
 *  a host that declares no such method does. */
export async function mountSketchBridge({ slug = null } = {}) {
  const fired = [];
  const rejecting = new Set();
  const host = {
    invokeMethodAsync(name, ...args) {
      fired.push([name, ...args]);
      return rejecting.has(name) ? Promise.reject(new Error(`no ${name}`)) : Promise.resolve();
    },
  };
  const svg = standIn();
  svg.parentNode = standIn();
  const handle = await mount(svg, standIn(), standIn(), standIn(), standIn(), host, slug);
  return {
    handle, fired,
    firedOf: (name) => fired.filter((entry) => entry[0] === name).map((entry) => entry.slice(1)),
    reject: (name) => rejecting.add(name),
  };
}
