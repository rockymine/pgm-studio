/**
 * EditorSelectController — the canvas click-select modes (pick a region / island / spawn marker).
 * Each mode is a picker the canvas registers (a hit-test + the host callback); the canvas forwards its
 * _onCanvasClick into the active mode. Mirrors the draw/edit controllers — the interaction wiring lives
 * here, so adding a mode is one register() call, not another branch in _onCanvasClick.
 */
export class EditorSelectController {
  #modes  = new Map();   // name → (world) => void
  #active = null;

  /** Register a named click mode; picker({x,z}) does the hit-test and fires the host callback. */
  register(name, picker) { this.#modes.set(name, picker); return this; }

  /** Activate a mode (an unknown name selects none). */
  setMode(name) { this.#active = this.#modes.has(name) ? name : null; }
  get mode() { return this.#active; }

  /** Dispatch a canvas click (world coords) to the active mode. */
  click(world) { this.#modes.get(this.#active)?.(world); }
}
