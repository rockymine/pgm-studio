/**
 * Island polygon helpers — coerce the API's GeoJSON island polygon into the
 * {exterior, holes} "simplified" form the canvas renders, and normalise a list of
 * islands so each carries a simplified_polygon. No DOM.
 */

export function geojsonToSimplified(polygon) {
  if (!polygon?.coordinates?.length) return null;
  return {
    exterior: polygon.coordinates[0] || [],
    holes:    polygon.coordinates.slice(1),
  };
}

export function normalizeIslands(islands) {
  return (islands ?? []).map(isl => ({
    ...isl,
    simplified_polygon: isl.simplified_polygon ?? geojsonToSimplified(isl.polygon),
  }));
}
