/**
 * Block-layer rendering: a block-pixel payload → a PNG data URL, and the shared
 * SVG <image> wrapper that places it. Used by EditorCanvas, ConfigureRenderer and
 * OverviewRenderer so the block overlay is painted one way.
 */

import { svgEl } from "./svg.js";

/**
 * Convert a block layer payload into a PNG data URL.
 */
export function blockDataToDataUrl({ xs, zs, colors, min_x, min_z, max_x, max_z }) {
  const imgW = max_x - min_x + 1;
  const imgH = max_z - min_z + 1;
  const offscreen = document.createElement("canvas");
  offscreen.width  = imgW;
  offscreen.height = imgH;
  const ctx2d  = offscreen.getContext("2d");
  const pixels = ctx2d.createImageData(imgW, imgH);
  const data   = pixels.data;
  for (let i = 0; i < xs.length; i++) {
    const rgb      = parseInt(colors[i].slice(1), 16);
    const pixelIdx = ((zs[i] - min_z) * imgW + (xs[i] - min_x)) * 4;
    data[pixelIdx]     = (rgb >> 16) & 0xff;
    data[pixelIdx + 1] = (rgb >> 8)  & 0xff;
    data[pixelIdx + 2] =  rgb        & 0xff;
    data[pixelIdx + 3] = 255;
  }
  ctx2d.putImageData(pixels, 0, 0);
  return offscreen.toDataURL("image/png");
}

/**
 * Build a pixelated <image> for a block layer and append it to `g` (cleared first).
 * The payload covers world cells [min_x..max_x] × [min_z..max_z]; the image spans to the
 * far edge of the max cell (+1) so pixels align to block centres.
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
