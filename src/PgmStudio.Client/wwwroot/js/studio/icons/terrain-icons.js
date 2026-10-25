// The studio's own terrain glyphs, in lucide's node format and drawn on lucide's 24-unit grid, registered into
// the set the vendored script exposes so `<i data-lucide="terrain-hill">` renders like any other icon. They are
// profiles and plans of ground, which lucide has no names for.
//
// Loaded after vendor/lucide-icons.js. A name lucide already defines is reported rather than replaced, so a
// future lucide release cannot silently take one over.
(function () {
  "use strict";

  var terrainIcons = {
    "terrain-land": [["path",{"d":"M3 8h18v4l-3 2-1 5-3-3-3 2-2-4-3 1-3-2z"}]],
    "terrain-hill": [["path",{"d":"M3 19h3c1-6 3-9 6-9s5 3 6 9h3"}]],
    "terrain-hollow": [["path",{"d":"M3 8h4c0 6 2 10 5 10s5-4 5-10h4"}]],
    "terrain-level": [["path",{"d":"M2 19h3c2 0 3-7 6-7h2c3 0 4 7 6 7h3"}],["path",{"d":"M8 7h8"}]],
    "terrain-ridge": [["path",{"d":"M3 18C6 9 15 5 21 6C18 14 11 19 3 18z"}],["line",{"x1":"6","y1":"16","x2":"18","y2":"8"}]],
    "terrain-butte": [["path",{"d":"M2 19h2l2-9h12l2 9h2"}]],
    "terrain-spire": [["path",{"d":"M2 19h7l3-15 3 15h7"}]],
    "terrain-scarp": [["path",{"d":"M3 19h7V9h11"}],["path",{"d":"M10 13H7M10 16H5"}]],
    "terrain-canyon": [["path",{"d":"M2 8h5l1 10h8l1-10h5"}],["path",{"d":"M9 13h6"}]],
    "terrain-profile": [["path",{"d":"M3 19h4l1-5h4l1-6h8"}]],
    "terrain-river": [["path",{"d":"M2 10h6l2 8h4l2-8h6"}],["path",{"d":"M9 14q1.5 1.5 3 0t3 0"}]],
    "terrain-lake": [["path",{"d":"M2 11h3c.7 5 3 8 7 8s6.3-3 7-8h3"}],["path",{"d":"M6 14q1.5 1.5 3 0t3 0 3 0 3 0"}]],
    "terrain-road": [["path",{"d":"M4 19h12a3 3 0 0 0 0-6H8a3 3 0 0 1 0-6h12"}]],
    "terrain-roughness": [["polyline",{"points":"3 14 5 11 7 15 9 10 11 16 13 11 15 15 17 12 19 14 21 13"}],["path",{"d":"M3 20h18"}]],
    "terrain-marks": [["path",{"d":"M3 20h18"}],["line",{"x1":"7","y1":"20","x2":"7","y2":"15.5"}],["line",{"x1":"17","y1":"20","x2":"17","y2":"9.5"}],["circle",{"cx":"7","cy":"14","r":"1.5"}],["circle",{"cx":"17","cy":"8","r":"1.5"}],["path",{"d":"M8.5 14c4 0 5-6 7-6"}]],
    "terrain-rooms": [["path",{"d":"M3 19h18"}],["path",{"d":"M6 19v-6l6-5 6 5v6"}],["path",{"d":"M10 19v-3h4v3"}]],
    "terrain-warp": [["path",{"d":"M3 7h18"}],["path",{"d":"M3 15c3-4 6-4 9 0s6 4 9 0"}]],
    "terrain-gullies": [["path",{"d":"M12 20v-8l-5-7"}],["path",{"d":"M12 12l5-7"}],["path",{"d":"M9.5 8.5 5 9"}],["path",{"d":"M14.5 8.5 19 9"}]],
    "terrain-coast": [["path",{"d":"M2 7h5c4 0 5 7 9 7h6"}],["path",{"d":"M11 19q1.5-1.5 3 0t3 0 3 0"}]],
    "terrain-lumps": [["path",{"d":"M2 19h20"}],["path",{"d":"M3 19c1-4 4-4 5 0"}],["path",{"d":"M9 19c1-7 5-7 6 0"}],["path",{"d":"M16 19c1-3 4-3 5 0"}]],
    "terrain-tunnel": [["path",{"d":"M2 20h20"}],["path",{"d":"M2 9c4-5 16-5 20 0"}],["path",{"d":"M7 20v-4a5 5 0 0 1 10 0v4"}]],
    "terrain-chamber": [["path",{"d":"M2 5h20"}],["path",{"d":"M4 19h16"}],["path",{"d":"M5 19c0-6 3-9 7-9s7 3 7 9"}]],
    "terrain-shaft": [["path",{"d":"M2 5h7v15h6V5h7"}],["path",{"d":"M11 8h2M11 12h2M11 16h2"}]],
    "terrain-shatter": [["path",{"d":"M3 5h18v14H3z"}],["path",{"d":"M3 11l6-1 3-5"}],["path",{"d":"M9 10l2 9"}],["path",{"d":"M11 14l5-2 5 1"}],["path",{"d":"M16 12l-1-7"}]]
  };

  var registry = window.lucide && window.lucide.icons;
  if (!registry) {
    console.error("terrain-icons: lucide is not loaded; load vendor/lucide-icons.js first");
    return;
  }
  Object.keys(terrainIcons).forEach(function (name) {
    if (registry[name]) console.error("terrain-icons: \"" + name + "\" is already a lucide icon");
    else registry[name] = terrainIcons[name];
  });
})();
