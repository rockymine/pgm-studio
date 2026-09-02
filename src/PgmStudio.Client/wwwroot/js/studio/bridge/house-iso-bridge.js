// The 3-D view of a building an editor has open.
//
// A house is drawn by the same renderer a board is: the preview answers the world's own per-column runs, and
// `meshColumns` turns those into the geometry `IsoScene` draws. There is no second isometric free to disagree
// with the map's, and no picture the export would not build — what is meshed is what was stamped.
//
// Smaller than the plan and sketch bridges because there is no canvas underneath: nothing is drawn in 2-D on
// this surface, so the scene owns its wrap outright and there is no layer to hide or restore. The mesh is kept
// against the payload it came from, so rotating and resizing re-render rather than re-mesh.

/**
 * Attach a 3-D surface to `wrapEl`. Resolves null when WebGL cannot run at all, which is the caller's cue to
 * say so rather than to show an empty box: the editor's three flat views work regardless.
 */
export async function mount(wrapEl) {
  let scene, meshColumns;
  try {
    const [iso, columns] = await Promise.all([
      import("/js/studio/render/iso-webgl.js"),
      import("/js/studio/render/column-mesh.js"),
    ]);
    meshColumns = columns.meshColumns;
    scene = new iso.IsoScene(wrapEl);
  } catch (e) {
    console.warn("[house-iso] 3-D preview unavailable:", e?.message ?? e);
    return null;
  }

  let payload = null, mesh = null, yaw = 30;

  function paint() {
    if (!mesh) return;
    const { width, height } = wrapEl.getBoundingClientRect();
    scene.render(mesh, Math.max(1, width), Math.max(1, height), yaw, payload);
  }

  // The wrap is a panel the author can drag wider, and a WebGL canvas does not reflow the way the SVG views
  // beside it do.
  const observer = new ResizeObserver(paint);
  observer.observe(wrapEl);

  return {
    /** Draw a world a preview call answered — the `columns` node of a RoomStylePreviewDto. */
    draw(json) {
      try {
        payload = typeof json === "string" ? JSON.parse(json) : json;
      } catch (e) {
        console.warn("[house-iso] the preview's columns could not be read:", e?.message ?? e);
        return;
      }
      mesh = meshColumns(payload);
      paint();
    },

    show() { scene.show(); },
    hide() { scene.hide(); },

    /** A quarter turn, so four presses come back to where they started. */
    rotate() { yaw = (yaw + 90) % 360; paint(); },

    dispose() { observer.disconnect(); scene.dispose(); },
  };
}
