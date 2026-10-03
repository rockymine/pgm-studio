// iso-preview.js — the read-only 3-D preview of the world a document builds, for a bridge that owns a canvas
// with `enterIso` / `drawIso` / `hideIso`.
//
// The picture is the world the export builds, not a second guess at it: the live document goes to a
// `columns` route, which runs the real build and answers every column's solid runs, and `column-mesh.js`
// turns those into triangles. The build is the cost, so it happens on entering the preview rather than on
// every edit; the mesh is kept against the document it came from, and re-entering an untouched document
// draws the cached one instead of asking again.

import { liveFeed } from "./live-feed.js";

/**
 * @param {object} options
 * @param {object} options.canvas      the canvas the preview is drawn on
 * @param {string|(() => (string|null))} options.url   the columns route, or a function answering it; empty when there is nothing to build
 * @param {string} [options.missing]  the sentence reported when `url` is empty
 * @param {() => string} options.state the document as the wire body, which is also the cache stamp
 * @param {() => object} options.bounds the world rectangle the picture is fitted to
 * @param {(payload: object, hidden: string[]) => void} [options.onBuilt]  a build landed; `hidden` is the layer
 *        ids the mesh will leave out, already pruned to the layers the build has
 * @param {(reason: string) => void} options.onUnavailable  the preview cannot run: an empty reason is WebGL
 *        itself, anything else is the build's own sentence
 */
export function isoPreview({ canvas, url, missing = "", state, bounds, onBuilt, onUnavailable }) {
  let on = false;
  let yaw = 30;
  let mesh = null, payload = null, stamp = null;
  const hidden = new Set();

  const draw = () => canvas.drawIso(mesh, yaw, bounds());
  const redraw = () => { if (on && mesh) draw(); };

  async function meshPayload() {
    const { meshColumns } = await import("../render/column-mesh.js");
    mesh = meshColumns(payload, [...hidden]);
  }

  function leave(reason) {
    canvas.hideIso();
    on = false;
    onUnavailable(reason);
  }

  const feed = liveFeed({
    url,
    body: state,
    async onAnswer(answered, sent) {
      if (!on) return;   // the preview was left while the build ran
      payload = answered;
      stamp = sent;
      // A layer the board no longer has is not left hidden: it would be a switch the host cannot show and
      // the author cannot turn back on.
      const names = payload.layers ?? [];
      for (const id of [...hidden]) if (!names.includes(id)) hidden.delete(id);
      onBuilt?.(payload, [...hidden]);
      await meshPayload();
      if (on) draw();
    },
    onRefused: (sentence) => { if (on) leave(sentence); },
    onUnreachable: (sentence) => { if (on) leave(sentence); },
  });

  return {
    /** Enter the preview, then fill it. Two steps so the toggle answers the click at once and the wait is a
     *  spinner over the 3-D surface rather than a frozen 2-D one. */
    async show() {
      const ok = await canvas.enterIso();
      if (ok === false) { on = false; onUnavailable(""); return; }
      on = true;
      if (mesh && stamp === state()) { draw(); return; }
      if (!(typeof url === "function" ? url() : url)) { leave(missing); return; }
      await feed.request();
    },
    hide() { on = false; canvas.hideIso(); },
    rotate() { yaw = (yaw + 90) % 360; redraw(); },
    /** An edit invalidates the picture: a stale mesh redrawn on rotate would show the document as it was. */
    drop() { mesh = null; payload = null; stamp = null; },
    /** Show or hide one layer of the preview. The document is not rebuilt: the runs say which layer drew
     *  them, so this re-meshes what is in hand. */
    async setLayerShown(id, shown) {
      if (shown) hidden.delete(id); else hidden.add(id);
      if (!payload) return;
      await meshPayload();
      redraw();
    },
  };
}
