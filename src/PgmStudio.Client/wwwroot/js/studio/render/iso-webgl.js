/**
 * WebGL isometric preview of the sketch — a depth-buffered 3-D renderer built directly on the WebGL
 * API (no scene-graph library). Occlusion is resolved per-pixel by the GPU z-buffer rather than a
 * painter's algorithm, so overlapping masses and the rot_180 mirror image stay mutually consistent
 * (a depth-key sort cannot, because per-object keys don't commute with the mirror's depth reflection).
 * Read-only.
 *
 * Consumes the same "solids" the bridge already builds (one per shape, plus rot/mirror copies):
 *   - prism:   { exterior, top, floor, mirror } — footprint extruded floor→top (caps + wall quads).
 *   - terrain: { vertices, heights, floor, mirror } — per-anchor: TIN top + walls following heights.
 * World (x,z) map to scene (x,z); height maps to scene y. An orthographic camera at a fixed isometric
 * elevation (yaw is user-rotatable) keeps it a true axonometric view. A single owned WebGL context,
 * one shader program, and two vertex buffers are reused across renders.
 */

import { earClip, earClipWithHoles } from "../geometry/triangulation.js";

const ELEV = Math.atan(1 / Math.SQRT2);   // true-isometric elevation (~35.26°)
const COL = {
  island: hexRgb(0x6d7ce8),
  mirror: hexRgb(0xaab1dd),
  ground: hexRgb(0xccd4e6),
};

const VERT_SRC = `
  attribute vec3 aPos;
  attribute vec3 aNormal;
  uniform mat4 uMVP;
  varying vec3 vNormal;
  void main() {
    vNormal = aNormal;
    gl_Position = uMVP * vec4(aPos, 1.0);
  }`;

// Flat Lambert: a key light from the upper-right + a soft fill + ambient, so walls read two-tone
// (lit from above) without per-face shading code. Normals are flipped toward the camera (uViewDir is
// the constant camera-forward of the orthographic view) so both sides of a double-wound face light
// correctly — the equivalent of a two-sided material. Intensities are tuned for the preview, not a
// physical match. The ground plane is drawn unlit (uUnlit) and translucent (uOpacity).
const FRAG_SRC = `
  precision mediump float;
  varying vec3 vNormal;
  uniform vec3 uColor;
  uniform vec3 uViewDir;
  uniform float uUnlit;
  uniform float uOpacity;
  void main() {
    if (uUnlit > 0.5) { gl_FragColor = vec4(uColor, uOpacity); return; }
    vec3 n = normalize(vNormal);
    if (dot(n, uViewDir) > 0.0) n = -n;
    vec3 keyDir  = normalize(vec3( 2.5, 4.0,  1.2));
    vec3 fillDir = normalize(vec3(-2.0, 1.5, -1.5));
    float lit = 0.55 + 0.60 * max(dot(n, keyDir), 0.0) + 0.14 * max(dot(n, fillDir), 0.0);
    gl_FragColor = vec4(uColor * lit, 1.0);
  }`;

export class IsoScene {
  #wrap; #canvas; #gl; #prog; #posBuf; #nrmBuf;
  #aPos; #aNrm; #uMVP; #uColor; #uViewDir; #uUnlit; #uOpacity;

  constructor(wrapEl) {
    this.#wrap = wrapEl;
    const canvas = document.createElement("canvas");
    canvas.style.cssText = "position:absolute;inset:0;width:100%;height:100%;display:none;pointer-events:none;";
    wrapEl.appendChild(canvas);
    this.#canvas = canvas;

    const opts = { antialias: true, alpha: true, premultipliedAlpha: false, preserveDrawingBuffer: true };
    const gl = canvas.getContext("webgl2", opts) || canvas.getContext("webgl", opts);
    if (!gl) throw new Error("WebGL unavailable");
    this.#gl = gl;

    this.#prog = linkProgram(gl, VERT_SRC, FRAG_SRC);
    this.#aPos = gl.getAttribLocation(this.#prog, "aPos");
    this.#aNrm = gl.getAttribLocation(this.#prog, "aNormal");
    this.#uMVP = gl.getUniformLocation(this.#prog, "uMVP");
    this.#uColor = gl.getUniformLocation(this.#prog, "uColor");
    this.#uViewDir = gl.getUniformLocation(this.#prog, "uViewDir");
    this.#uUnlit = gl.getUniformLocation(this.#prog, "uUnlit");
    this.#uOpacity = gl.getUniformLocation(this.#prog, "uOpacity");
    this.#posBuf = gl.createBuffer();
    this.#nrmBuf = gl.createBuffer();

    gl.enable(gl.DEPTH_TEST);
    gl.depthFunc(gl.LEQUAL);
    gl.disable(gl.CULL_FACE);   // faces are drawn two-sided (lighting handled in the shader)
    gl.clearColor(0, 0, 0, 0);
  }

  show() { this.#canvas.style.display = ""; }
  hide() { this.#canvas.style.display = "none"; }
  dispose() {
    const gl = this.#gl;
    gl.deleteBuffer(this.#posBuf);
    gl.deleteBuffer(this.#nrmBuf);
    gl.deleteProgram(this.#prog);
    this.#canvas.remove();
  }

  /** Rebuild the scene from `solids` and render at `w×h`, framed to `bbox`, rotated by `yawDeg`. */
  render(solids, w, h, yawDeg, bbox) {
    const gl = this.#gl;
    const ratio = Math.min(globalThis.devicePixelRatio || 1, 2);
    this.#canvas.width = Math.max(1, Math.floor(w * ratio));
    this.#canvas.height = Math.max(1, Math.floor(h * ratio));
    gl.viewport(0, 0, this.#canvas.width, this.#canvas.height);
    gl.clear(gl.COLOR_BUFFER_BIT | gl.DEPTH_BUFFER_BIT);

    // Split the triangle soup by colour (one draw each) and track the scene extents for framing.
    const island = [], mirror = [];
    let minX = Infinity, maxX = -Infinity, minZ = Infinity, maxZ = -Infinity, maxY = 0;
    const grow = (x, z, y) => { if (x < minX) minX = x; if (x > maxX) maxX = x; if (z < minZ) minZ = z; if (z > maxZ) maxZ = z; if (y > maxY) maxY = y; };

    for (const s of (solids || [])) {
      const pos = s.vertices ? terrainPositions(s) : prismPositions(s);
      if (!pos) continue;
      (s.mirror ? mirror : island).push(...pos);
      const ring = s.vertices || s.exterior;
      const top = s.vertices ? Math.max(...s.heights) : s.top;
      for (const [x, z] of ring) grow(x, z, top);
    }

    let ground = null;
    if (bbox) {
      for (const [x, z] of [[bbox.min_x, bbox.min_z], [bbox.max_x, bbox.max_z]]) grow(x, z, 0);
      ground = [
        bbox.min_x, 0, bbox.min_z,  bbox.max_x, 0, bbox.min_z,  bbox.max_x, 0, bbox.max_z,
        bbox.min_x, 0, bbox.min_z,  bbox.max_x, 0, bbox.max_z,  bbox.min_x, 0, bbox.max_z,
      ];
    }

    if (!isFinite(minX)) return;   // nothing to draw

    gl.useProgram(this.#prog);
    const { mvp, viewDir } = this.#frame(minX, maxX, minZ, maxZ, maxY, w / h, yawDeg);
    gl.uniformMatrix4fv(this.#uMVP, false, mvp);
    gl.uniform3fv(this.#uViewDir, viewDir);

    // Opaque lit geometry first (depth write on), then the translucent ground (depth test on,
    // depth write off) so it blends against whatever is in front of it.
    gl.disable(gl.BLEND);
    gl.depthMask(true);
    this.#draw(island, COL.island, false, 1);
    this.#draw(mirror, COL.mirror, false, 1);
    if (ground) {
      gl.enable(gl.BLEND);
      gl.blendFunc(gl.SRC_ALPHA, gl.ONE_MINUS_SRC_ALPHA);
      gl.depthMask(false);
      this.#draw(ground, COL.ground, true, 0.5);
      gl.depthMask(true);
      gl.disable(gl.BLEND);
    }
  }

  #draw(pos, color, unlit, opacity) {
    if (!pos.length) return;
    const gl = this.#gl;
    const p = new Float32Array(pos);
    gl.bindBuffer(gl.ARRAY_BUFFER, this.#posBuf);
    gl.bufferData(gl.ARRAY_BUFFER, p, gl.DYNAMIC_DRAW);
    gl.enableVertexAttribArray(this.#aPos);
    gl.vertexAttribPointer(this.#aPos, 3, gl.FLOAT, false, 0, 0);

    gl.bindBuffer(gl.ARRAY_BUFFER, this.#nrmBuf);
    gl.bufferData(gl.ARRAY_BUFFER, unlit ? new Float32Array(p.length) : flatNormals(p), gl.DYNAMIC_DRAW);
    gl.enableVertexAttribArray(this.#aNrm);
    gl.vertexAttribPointer(this.#aNrm, 3, gl.FLOAT, false, 0, 0);

    gl.uniform3fv(this.#uColor, color);
    gl.uniform1f(this.#uUnlit, unlit ? 1 : 0);
    gl.uniform1f(this.#uOpacity, opacity);
    gl.drawArrays(gl.TRIANGLES, 0, p.length / 3);
  }

  // Place the orthographic camera at the iso angle, fit its frustum to the scene, and return the
  // view-projection matrix plus the camera-forward direction (for two-sided shading).
  #frame(minX, maxX, minZ, maxZ, maxY, aspect, yawDeg) {
    const center = [(minX + maxX) / 2, maxY / 2, (minZ + maxZ) / 2];
    const radius = Math.hypot(maxX - minX, maxY, maxZ - minZ);

    const az = (yawDeg * Math.PI) / 180 + Math.PI / 4;
    const dir = [Math.cos(ELEV) * Math.sin(az), Math.sin(ELEV), Math.cos(ELEV) * Math.cos(az)];
    const eye = [center[0] + dir[0] * (radius + 10), center[1] + dir[1] * (radius + 10), center[2] + dir[2] * (radius + 10)];
    const view = lookAt(eye, center, [0, 1, 0]);

    // Fit: project the 8 scene-box corners into camera space, take extents, add a margin, match aspect.
    let l = Infinity, r = -Infinity, b = Infinity, t = -Infinity;
    for (const x of [minX, maxX]) for (const y of [0, maxY]) for (const z of [minZ, maxZ]) {
      const cx = view[0] * x + view[4] * y + view[8] * z + view[12];
      const cy = view[1] * x + view[5] * y + view[9] * z + view[13];
      if (cx < l) l = cx; if (cx > r) r = cx; if (cy < b) b = cy; if (cy > t) t = cy;
    }
    const mx = (r - l) * 0.06, my = (t - b) * 0.06;
    l -= mx; r += mx; b -= my; t += my;
    let cw = r - l, ch = t - b;
    if (cw / ch > aspect) ch = cw / aspect; else cw = ch * aspect;
    const mcx = (l + r) / 2, mcy = (b + t) / 2;
    const proj = ortho(mcx - cw / 2, mcx + cw / 2, mcy - ch / 2, mcy + ch / 2, -radius - 50, radius * 3 + 50);

    return { mvp: multiply(proj, view), viewDir: [-dir[0], -dir[1], -dir[2]] };
  }
}

// ── geometry builders (world x→x, z→z, height→y) — flat triangle soup ─────────────

function prismPositions(s) {
  const ext = openRing(s.exterior);
  if (ext.length < 3) return null;
  const holes = (s.holes ?? []).map(openRing).filter(h => h.length >= 3);
  const top = s.top ?? 0, floor = s.floor ?? 0;
  const pos = [];
  // Caps: triangulate the footprint MINUS its holes (a subtract carved into this shape), top + floor.
  const cap = holes.length
    ? earClipWithHoles(ext, holes)
    : earClip(ext).map(([a, b, c]) => [ext[a], ext[b], ext[c]]);
  for (const [a, b, c] of cap) {
    pos.push(a[0], top, a[1], b[0], top, b[1], c[0], top, c[1]);
    pos.push(a[0], floor, a[1], c[0], floor, c[1], b[0], floor, b[1]);
  }
  // Walls: a top→floor quad per edge of the outer ring and of every hole (the inner walls of the well).
  for (const ring of [ext, ...holes]) {
    for (let i = 0, n = ring.length; i < n; i++) {
      const j = (i + 1) % n;
      const xi = ring[i][0], zi = ring[i][1], xj = ring[j][0], zj = ring[j][1];
      pos.push(xi, top, zi, xj, top, zj, xj, floor, zj);
      pos.push(xi, top, zi, xj, floor, zj, xi, floor, zi);
    }
  }
  return pos;
}

function terrainPositions(s) {
  const V = s.vertices, H = s.heights, n = V.length, floor = s.floor ?? 0;
  if (n < 3) return null;
  const pos = [];
  const tri = (ax, ay, az, bx, by, bz, cx, cy, cz) => pos.push(ax, ay, az, bx, by, bz, cx, cy, cz);
  // Sloped TIN top.
  for (const [a, b, c] of earClip(V)) tri(V[a][0], H[a], V[a][1], V[b][0], H[b], V[b][1], V[c][0], H[c], V[c][1]);
  // Walls: each footprint edge from its vertex heights down to the floor (two windings so the wall
  // shows regardless of ring orientation).
  for (let i = 0; i < n; i++) {
    const j = (i + 1) % n;
    const xi = V[i][0], zi = V[i][1], xj = V[j][0], zj = V[j][1];
    tri(xi, H[i], zi, xj, H[j], zj, xj, floor, zj);
    tri(xi, H[i], zi, xj, floor, zj, xi, floor, zi);
  }
  return pos;
}

// Per-face (flat) normals: each triangle's three vertices share its geometric normal.
function flatNormals(pos) {
  const nrm = new Float32Array(pos.length);
  for (let i = 0; i < pos.length; i += 9) {
    const ux = pos[i + 3] - pos[i], uy = pos[i + 4] - pos[i + 1], uz = pos[i + 5] - pos[i + 2];
    const vx = pos[i + 6] - pos[i], vy = pos[i + 7] - pos[i + 1], vz = pos[i + 8] - pos[i + 2];
    let nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx;
    const len = Math.hypot(nx, ny, nz) || 1; nx /= len; ny /= len; nz /= len;
    for (let k = 0; k < 9; k += 3) { nrm[i + k] = nx; nrm[i + k + 1] = ny; nrm[i + k + 2] = nz; }
  }
  return nrm;
}

// Drop a trailing closing-vertex duplicate so the ring is a clean open loop.
function openRing(r) {
  if (!r || r.length < 3) return r || [];
  const a = r[0], b = r[r.length - 1];
  return (a[0] === b[0] && a[1] === b[1]) ? r.slice(0, -1) : r;
}

// ── minimal column-major mat4 / vec3 math ────────────────────────────────────────

function ortho(l, r, b, t, n, f) {
  return new Float32Array([
    2 / (r - l), 0, 0, 0,
    0, 2 / (t - b), 0, 0,
    0, 0, -2 / (f - n), 0,
    -(r + l) / (r - l), -(t + b) / (t - b), -(f + n) / (f - n), 1,
  ]);
}

function lookAt(eye, center, up) {
  const f = norm([center[0] - eye[0], center[1] - eye[1], center[2] - eye[2]]);
  const s = norm(cross(f, up));
  const u = cross(s, f);
  return new Float32Array([
    s[0], u[0], -f[0], 0,
    s[1], u[1], -f[1], 0,
    s[2], u[2], -f[2], 0,
    -dot(s, eye), -dot(u, eye), dot(f, eye), 1,
  ]);
}

function multiply(a, b) {
  const o = new Float32Array(16);
  for (let c = 0; c < 4; c++) for (let r = 0; r < 4; r++) {
    o[c * 4 + r] = a[r] * b[c * 4] + a[r + 4] * b[c * 4 + 1] + a[r + 8] * b[c * 4 + 2] + a[r + 12] * b[c * 4 + 3];
  }
  return o;
}

const cross = (a, b) => [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]];
const dot = (a, b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
const norm = (a) => { const l = Math.hypot(a[0], a[1], a[2]) || 1; return [a[0] / l, a[1] / l, a[2] / l]; };

function hexRgb(h) { return [((h >> 16) & 255) / 255, ((h >> 8) & 255) / 255, (h & 255) / 255]; }

// ── WebGL program helpers ────────────────────────────────────────────────────────

function linkProgram(gl, vs, fs) {
  const prog = gl.createProgram();
  gl.attachShader(prog, compile(gl, gl.VERTEX_SHADER, vs));
  gl.attachShader(prog, compile(gl, gl.FRAGMENT_SHADER, fs));
  gl.linkProgram(prog);
  if (!gl.getProgramParameter(prog, gl.LINK_STATUS)) throw new Error("iso shader link: " + gl.getProgramInfoLog(prog));
  return prog;
}

function compile(gl, type, src) {
  const sh = gl.createShader(type);
  gl.shaderSource(sh, src);
  gl.compileShader(sh);
  if (!gl.getShaderParameter(sh, gl.COMPILE_STATUS)) throw new Error("iso shader compile: " + gl.getShaderInfoLog(sh));
  return sh;
}
