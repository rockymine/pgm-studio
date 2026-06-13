/**
 * Convert a block layer payload into a PNG data URL.
 * Used by overview-renderer (SVG image element) and configure page (img element).
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
