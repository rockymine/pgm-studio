/**
 * Pure world↔SVG coordinate math. No DOM — the single bridge between the geometry
 * layer (point arrays / bounds) and the render layer (SVG elements / path strings)
 * is the `toSvg` function these build. Accepts bounding_box as {min_x,min_z,max_x,max_z}.
 */

const PAD = 20;

/**
 * Build a world→SVG transform from a bounding box.
 * @param {{min_x,min_z,max_x,max_z}} bbox
 * @returns {(wx:number,wz:number)=>{x:number,y:number}}
 */
export function buildTransform(bbox, svgW, svgH) {
  const { min_x, min_z, max_x, max_z } = bbox;
  const worldW = max_x - min_x, worldH = max_z - min_z;
  const drawW = svgW - 2 * PAD, drawH = svgH - 2 * PAD;
  const scale = Math.min(drawW / worldW, drawH / worldH);
  const offX = PAD + (drawW - worldW * scale) / 2;
  const offY = PAD + (drawH - worldH * scale) / 2;
  return (wx, wz) => ({
    x: offX + (wx - min_x) * scale,
    y: offY + (wz - min_z) * scale,
  });
}

export function buildInverseTransform(bbox, svgW, svgH) {
  const { min_x, min_z, max_x, max_z } = bbox;
  const worldW = max_x - min_x, worldH = max_z - min_z;
  const drawW = svgW - 2 * PAD, drawH = svgH - 2 * PAD;
  const scale = Math.min(drawW / worldW, drawH / worldH);
  const offX = PAD + (drawW - worldW * scale) / 2;
  const offY = PAD + (drawH - worldH * scale) / 2;
  return (px, py) => ({
    x: (px - offX) / scale + min_x,
    z: (py - offY) / scale + min_z,
  });
}
