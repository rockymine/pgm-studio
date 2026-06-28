/**
 * WebGL isometric preview of the sketch — a real depth-buffered 3-D renderer (three.js), so occlusion
 * is resolved by the GPU z-buffer rather than a painter's algorithm. Replaces the bespoke SVG iso
 * renderer (which couldn't order overlapping/mirrored faces correctly). Read-only.
 *
 * Consumes the same "solids" the bridge already builds (one per shape, plus rot/mirror copies):
 *   - prism:   { exterior, holes, top, floor, mirror } — footprint extruded floor→top.
 *   - terrain: { vertices, heights, floor, mirror }    — per-anchor: TIN top + walls following heights.
 * World (x,z) map to scene (x,z); height maps to scene y. An orthographic camera at a fixed isometric
 * elevation (yaw is user-rotatable) keeps it a true axonometric view. A single owned WebGL context is
 * reused across renders.
 */

import * as THREE from "../vendor/three.module.js";
import { earClip } from "../geometry/triangulation.js";

const ELEV = Math.atan(1 / Math.SQRT2);   // true-isometric elevation (~35.26°)
const COL = {
  island: 0x6d7ce8,
  mirror: 0xaab1dd,
  ground: 0xccd4e6,
};

export class IsoScene {
  #wrap; #canvas; #renderer; #scene; #camera; #group; #ground;

  constructor(wrapEl) {
    this.#wrap = wrapEl;
    const canvas = document.createElement("canvas");
    canvas.style.cssText = "position:absolute;inset:0;width:100%;height:100%;display:none;pointer-events:none;";
    wrapEl.appendChild(canvas);
    this.#canvas = canvas;

    this.#renderer = new THREE.WebGLRenderer({ canvas, antialias: true, alpha: true, preserveDrawingBuffer: true });
    this.#renderer.setClearColor(0x000000, 0);

    this.#scene = new THREE.Scene();
    this.#camera = new THREE.OrthographicCamera(-1, 1, 1, -1, -1000, 5000);
    this.#group = new THREE.Group();
    this.#scene.add(this.#group);

    // Lighting: a key light from the upper-right + fill + ambient, so walls read two-tone (lit from above)
    // without any per-face shading code — the GPU shades by the real surface normals.
    const key = new THREE.DirectionalLight(0xffffff, 1.45); key.position.set(2.5, 4, 1.2);
    const fill = new THREE.DirectionalLight(0xffffff, 0.35); fill.position.set(-2, 1.5, -1.5);
    this.#scene.add(key, fill, new THREE.AmbientLight(0xffffff, 0.62));
  }

  show() { this.#canvas.style.display = ""; }
  hide() { this.#canvas.style.display = "none"; }
  dispose() {
    this.#disposeMeshes();
    this.#renderer.dispose();
    this.#canvas.remove();
  }

  #disposeMeshes() {
    for (const m of [...this.#group.children]) { m.geometry?.dispose(); m.material?.dispose(); this.#group.remove(m); }
    if (this.#ground) { this.#ground.geometry.dispose(); this.#ground.material.dispose(); this.#scene.remove(this.#ground); this.#ground = null; }
  }

  /** Rebuild the scene from `solids` and render at `w×h`, framed to `bbox`, rotated by `yawDeg`. */
  render(solids, w, h, yawDeg, bbox) {
    this.#renderer.setPixelRatio(Math.min(globalThis.devicePixelRatio || 1, 2));
    this.#renderer.setSize(w, h, false);
    this.#disposeMeshes();

    // DoubleSide so a terrain (TIN) top whose triangle winding faces away from the camera still renders
    // (its sloped top would otherwise be back-face culled); three.js flips the normal per viewed side, so
    // lighting stays correct. Prism interiors are hidden by the depth buffer, so the extra side is free.
    const matIsland = new THREE.MeshLambertMaterial({ color: COL.island, side: THREE.DoubleSide });
    const matMirror = new THREE.MeshLambertMaterial({ color: COL.mirror, side: THREE.DoubleSide });

    let minX = Infinity, maxX = -Infinity, minZ = Infinity, maxZ = -Infinity, maxY = 0;
    const grow = (x, z, y) => { if (x < minX) minX = x; if (x > maxX) maxX = x; if (z < minZ) minZ = z; if (z > maxZ) maxZ = z; if (y > maxY) maxY = y; };

    for (const s of (solids || [])) {
      const geom = s.vertices ? terrainGeometry(s) : prismGeometry(s);
      if (!geom) continue;
      const mesh = new THREE.Mesh(geom, s.mirror ? matMirror : matIsland);
      this.#group.add(mesh);
      const ring = s.vertices || s.exterior;
      const top = s.vertices ? Math.max(...s.heights) : s.top;
      for (const [x, z] of ring) grow(x, z, top);
    }

    // Ground-plane reference.
    if (bbox) {
      for (const [x, z] of [[bbox.min_x, bbox.min_z], [bbox.max_x, bbox.max_z]]) grow(x, z, 0);
      const gw = bbox.max_x - bbox.min_x, gd = bbox.max_z - bbox.min_z;
      const g = new THREE.Mesh(
        new THREE.PlaneGeometry(gw, gd),
        new THREE.MeshBasicMaterial({ color: COL.ground, transparent: true, opacity: 0.5, side: THREE.DoubleSide }),
      );
      g.rotation.x = -Math.PI / 2;
      g.position.set((bbox.min_x + bbox.max_x) / 2, 0, (bbox.min_z + bbox.max_z) / 2);
      this.#scene.add(g);
      this.#ground = g;
    }

    if (!isFinite(minX)) { this.#renderer.clear(); return; }   // nothing to draw

    this.#frame(minX, maxX, minZ, maxZ, maxY, w, h, yawDeg);
    this.#renderer.render(this.#scene, this.#camera);
  }

  // Position the orthographic camera at the iso angle and size its frustum to fit the scene.
  #frame(minX, maxX, minZ, maxZ, maxY, w, h, yawDeg) {
    const cx = (minX + maxX) / 2, cy = maxY / 2, cz = (minZ + maxZ) / 2;
    const center = new THREE.Vector3(cx, cy, cz);
    const radius = Math.hypot(maxX - minX, maxY, maxZ - minZ);

    const az = (yawDeg * Math.PI) / 180 + Math.PI / 4;
    const dir = new THREE.Vector3(Math.cos(ELEV) * Math.sin(az), Math.sin(ELEV), Math.cos(ELEV) * Math.cos(az));
    this.#camera.position.copy(center).addScaledVector(dir, radius + 10);
    this.#camera.up.set(0, 1, 0);
    this.#camera.lookAt(center);
    this.#camera.updateMatrixWorld();

    // Fit: project the 8 scene-box corners into camera space, take extents, add a margin, match aspect.
    const inv = this.#camera.matrixWorldInverse;
    let l = Infinity, r = -Infinity, b = Infinity, t = -Infinity;
    const v = new THREE.Vector3();
    for (const x of [minX, maxX]) for (const y of [0, maxY]) for (const z of [minZ, maxZ]) {
      v.set(x, y, z).applyMatrix4(inv);
      if (v.x < l) l = v.x; if (v.x > r) r = v.x; if (v.y < b) b = v.y; if (v.y > t) t = v.y;
    }
    const mx = (r - l) * 0.06, my = (t - b) * 0.06;
    l -= mx; r += mx; b -= my; t += my;
    let cw = r - l, ch = t - b;
    const aspect = w / h;
    if (cw / ch > aspect) ch = cw / aspect; else cw = ch * aspect;
    const mcx = (l + r) / 2, mcy = (b + t) / 2;
    this.#camera.left = mcx - cw / 2; this.#camera.right = mcx + cw / 2;
    this.#camera.top = mcy + ch / 2; this.#camera.bottom = mcy - ch / 2;
    this.#camera.near = -radius - 50; this.#camera.far = radius * 3 + 50;
    this.#camera.updateProjectionMatrix();
  }
}

// ── geometry builders (world x→x, z→z, height→y) ────────────────────────────────

function prismGeometry(s) {
  const ext = closed(s.exterior);
  if (ext.length < 4) return null;
  // Shape in (x, -z) so that after rotateX(-90°) the footprint lands back on world (x, z), with the
  // extrude axis becoming +y (height). Holes (e.g. hole-square) carve the top the same way.
  const shape = new THREE.Shape(ext.map(([x, z]) => new THREE.Vector2(x, -z)));
  for (const hole of (s.holes || [])) {
    const hr = closed(hole);
    if (hr.length >= 4) shape.holes.push(new THREE.Path(hr.map(([x, z]) => new THREE.Vector2(x, -z))));
  }
  const depth = Math.max(0.001, (s.top ?? 0) - (s.floor ?? 0));
  const geom = new THREE.ExtrudeGeometry(shape, { depth, bevelEnabled: false });
  geom.rotateX(-Math.PI / 2);
  geom.translate(0, s.floor ?? 0, 0);
  geom.computeVertexNormals();
  return geom;
}

function terrainGeometry(s) {
  const V = s.vertices, H = s.heights, n = V.length, floor = s.floor ?? 0;
  if (n < 3) return null;
  const pos = [];
  const tri = (ax, ay, az, bx, by, bz, cx, cy, cz) => pos.push(ax, ay, az, bx, by, bz, cx, cy, cz);
  // Sloped TIN top.
  for (const [a, b, c] of earClip(V)) tri(V[a][0], H[a], V[a][1], V[b][0], H[b], V[b][1], V[c][0], H[c], V[c][1]);
  // Walls: each footprint edge from its vertex heights down to the floor (two triangles, both windings
  // so the wall shows regardless of ring orientation).
  for (let i = 0; i < n; i++) {
    const j = (i + 1) % n;
    const xi = V[i][0], zi = V[i][1], xj = V[j][0], zj = V[j][1];
    tri(xi, H[i], zi, xj, H[j], zj, xj, floor, zj);
    tri(xi, H[i], zi, xj, floor, zj, xi, floor, zi);
    tri(xi, H[i], zi, xj, floor, zj, xj, H[j], zj);
    tri(xi, H[i], zi, xi, floor, zi, xj, floor, zj);
  }
  const geom = new THREE.BufferGeometry();
  geom.setAttribute("position", new THREE.Float32BufferAttribute(pos, 3));
  geom.computeVertexNormals();
  return geom;
}

const closed = r => (r && r.length && (r[0][0] !== r[r.length - 1][0] || r[0][1] !== r[r.length - 1][1])) ? [...r, r[0]] : (r || []);
