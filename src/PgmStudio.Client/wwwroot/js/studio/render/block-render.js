/**
 * Block-layer rendering: a block-pixel payload → a PNG, placed the same way whichever dialect a surface
 * draws in. This is the rasterize-once discipline the whole canvas work rests on — a 45k-block layer is
 * one bitmap and one blit, so its cost is independent of how many blocks it holds, which is why the block
 * overlays never suffered the artifact that the vector layers did.
 *
 * `blockImageBounds` is the placement rule and belongs here rather than at each call site: the payload
 * covers world cells [min_x..max_x] × [min_z..max_z] and the image spans to the FAR edge of the max cell,
 * so pixels align to block extents rather than to their corners.
 */

import { svgEl } from "./svg.js";

/**
 * Convert a block layer payload into a PNG data URL. Two encodings are accepted, because the two things that
 * produce these payloads have opposite shapes:
 *
 *   cells  { xs, zs, colors }            one entry per painted cell — a scanned world's surface, arbitrary
 *   runs   { palette, runs }             row-major [paletteIndex, length, …] over the whole bounding box,
 *                                        -1 for a cell outside the footprint — a painted sketch, which is
 *                                        patches of a few blocks and long stretches of void
 *
 * A sketch board emits well under one run per painted cell, so the run form is several times smaller on the
 * wire; the sender picks whichever is smaller and this decodes both into the same bitmap.
 */
export function blockDataToDataUrl(payload) {
  const { min_x, min_z, max_x, max_z } = payload;
  const imgW = max_x - min_x + 1;
  const imgH = max_z - min_z + 1;
  const offscreen = document.createElement("canvas");
  offscreen.width  = imgW;
  offscreen.height = imgH;
  const ctx2d  = offscreen.getContext("2d");
  const pixels = ctx2d.createImageData(imgW, imgH);
  const data   = pixels.data;

  const write = (pixelIdx, hex) => {
    const rgb = parseInt(hex.slice(1), 16);
    data[pixelIdx]     = (rgb >> 16) & 0xff;
    data[pixelIdx + 1] = (rgb >> 8)  & 0xff;
    data[pixelIdx + 2] =  rgb        & 0xff;
    data[pixelIdx + 3] = 255;
  };

  if (payload.runs) {
    const { runs, palette } = payload;
    let cell = 0;
    for (let i = 0; i < runs.length; i += 2) {
      const index = runs[i], length = runs[i + 1];
      if (index < 0) { cell += length; continue; }        // void — leave those pixels transparent
      const hex = palette[index];
      for (let end = cell + length; cell < end; cell++) write(cell * 4, hex);
    }
  } else {
    const { xs, zs, colors } = payload;
    for (let i = 0; i < xs.length; i++) write(((zs[i] - min_z) * imgW + (xs[i] - min_x)) * 4, colors[i]);
  }

  ctx2d.putImageData(pixels, 0, 0);
  return offscreen.toDataURL("image/png");
}

/** The world box a block payload's bitmap covers. */
export function blockImageBounds({ min_x, min_z, max_x, max_z }) {
  return { min_x, min_z, max_x: max_x + 1, max_z: max_z + 1 };
}

/**
 * Decode a block payload into an image a painter can blit, calling `onReady` when it is decodable. A
 * canvas draws a decoded bitmap rather than a data URL, so the PNG is built and decoded once here and
 * blitted on every frame thereafter.
 */
export function loadBlockImage(data, onReady) {
  const image = new Image();
  image.onload = () => onReady(image);
  image.src = blockDataToDataUrl(data);
  return image;
}

/**
 * Build a pixelated <image> for a block layer and append it to `g` (cleared first) — the retained dialect,
 * for the fixed-fit previews that have no viewport transform to go stale.
 */
export function renderBlockImage(g, data, toSvg) {
  const { min_x, min_z, max_x, max_z } = data;
  const p1 = toSvg(min_x,     min_z);
  const p2 = toSvg(max_x + 1, max_z + 1);
  const img = svgEl("image");
  img.setAttribute("href",   blockDataToDataUrl(data));
  img.setAttribute("x",      Math.min(p1.x, p2.x));
  img.setAttribute("y",      Math.min(p1.y, p2.y));
  img.setAttribute("width",  Math.abs(p2.x - p1.x));
  img.setAttribute("height", Math.abs(p2.y - p1.y));
  img.setAttribute("pointer-events", "none");
  img.style.imageRendering = "pixelated";
  while (g.firstChild) g.removeChild(g.firstChild);
  g.appendChild(img);
}
