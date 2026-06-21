/**
 * Pure world↔SVG coordinate math. No DOM — the single bridge between the geometry
 * layer (point arrays / bounds) and the render layer (SVG elements / path strings)
 * is the `toSvg` function these build. Accepts bounding_box as {min_x,min_z,max_x,max_z}.
 */

const PAD = 20;

/**
 * Compute the shared fit parameters (origin + uniform scale) for a bbox→viewport mapping.
 * Tolerates a missing/degenerate bbox: a null bbox or a zero/negative/non-finite world extent
 * (e.g. an xml-only map with no bounding_box, or a single-region map where min == max) falls back
 * to unit scale so the transform stays finite instead of producing Infinity/NaN coordinates.
 * Callers that want to skip rendering on a missing bbox should test it before calling.
 */
function fit(bbox, svgW, svgH) {
  const b = bbox ?? {};
  const min_x = b.min_x ?? 0, min_z = b.min_z ?? 0;
  const worldW = (b.max_x ?? min_x) - min_x, worldH = (b.max_z ?? min_z) - min_z;
  const drawW = svgW - 2 * PAD, drawH = svgH - 2 * PAD;
  let scale = Math.min(drawW / worldW, drawH / worldH);
  if (!Number.isFinite(scale) || scale <= 0) scale = 1;
  return {
    min_x, min_z, scale,
    offX: PAD + (drawW - worldW * scale) / 2,
    offY: PAD + (drawH - worldH * scale) / 2,
  };
}

/**
 * Build a world→SVG transform from a bounding box.
 * @param {{min_x,min_z,max_x,max_z}|null} bbox
 * @returns {(wx:number,wz:number)=>{x:number,y:number}}
 */
export function buildTransform(bbox, svgW, svgH) {
  const { min_x, min_z, scale, offX, offY } = fit(bbox, svgW, svgH);
  return (wx, wz) => ({
    x: offX + (wx - min_x) * scale,
    y: offY + (wz - min_z) * scale,
  });
}

export function buildInverseTransform(bbox, svgW, svgH) {
  const { min_x, min_z, scale, offX, offY } = fit(bbox, svgW, svgH);
  return (px, py) => ({
    x: (px - offX) / scale + min_x,
    z: (py - offY) / scale + min_z,
  });
}
