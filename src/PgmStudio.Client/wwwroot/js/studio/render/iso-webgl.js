/**
 * WebGL isometric preview — a depth-buffered 3-D renderer built directly on the WebGL API (no scene-graph
 * library). Occlusion is resolved per-pixel by the GPU z-buffer rather than a painter's algorithm, so
 * overlapping masses and the rot_180 mirror image stay mutually consistent (a depth-key sort cannot, because
 * per-object keys don't commute with the mirror's depth reflection). Read-only.
 *
 * It draws the world the export builds, meshed from the server's per-column runs by `column-mesh.js`: three
 * parallel arrays of positions, normals and per-vertex colours, ready to hand to the GPU. Everything the
 * picture knows arrives that way — the terrain's own materials, the structures standing on it, and the
 * mirror copies, which are columns like any other rather than a second pass with a paler colour.
 *
 * Per-vertex colour is what lets a whole board draw in one call. Everything stays opaque: translucency would
 * need a back-to-front sort, which is exactly what the mirror image defeats — and it is also why water is an
 * opaque blue surface here rather than something to see through.
 *
 * World (x,z) map to scene (x,z); height maps to scene y. An orthographic camera at a fixed isometric
 * elevation (yaw is user-rotatable) keeps it a true axonometric view. A single owned WebGL context, one
 * shader program, and the mesh's three vertex buffers are reused across renders; a mesh is uploaded once, so a
 * redraw for a zoom or a pan only changes the camera.
 *
 * The camera is fitted to the board, then zoomed by a factor of that fit and panned by a shift of the point it
 * looks at, along the ground. The shift is in world blocks rather than on the screen, so a quarter turn keeps the
 * same place in the middle of the picture.
 */

const ELEV = Math.atan(1 / Math.SQRT2);   // true-isometric elevation (~35.26°)
const ZOOM_MIN = 1;                       // the fitted board: there is nothing to see further out
const ZOOM_MAX = 40;
const ZOOM_STEP = 1.15;                   // one wheel notch
const UP = [0, 1, 0];
const GROUND = [0.8, 0.83, 0.90];         // the datum plane under everything, drawn unlit and translucent

const VERT_SRC = `
  attribute vec3 aPos;
  attribute vec3 aNormal;
  attribute vec3 aColor;
  uniform mat4 uMVP;
  varying vec3 vNormal;
  varying vec3 vColor;
  void main() {
    vNormal = aNormal;
    vColor = aColor;
    gl_Position = uMVP * vec4(aPos, 1.0);
  }`;

// Flat Lambert over the vertex colour: a key light from the upper-right + a soft fill + ambient, so a wall
// reads darker than the ground it stands on without per-face shading code. Normals are flipped toward the
// camera (uViewDir is the constant camera-forward of the orthographic view) so both sides of a face light
// correctly — the equivalent of a two-sided material. Intensities are tuned for the preview, not a physical
// match. The ground plane is drawn unlit (uUnlit) in uColor at uOpacity.
const FRAG_SRC = `
  precision mediump float;
  varying vec3 vNormal;
  varying vec3 vColor;
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
    gl_FragColor = vec4(vColor * lit, 1.0);
  }`;

/** The camera's forward-from-the-scene direction and its screen right (`s`) and up (`u`) axes at a yaw. */
export function cameraBasis(yawDeg) {
  const az = (yawDeg * Math.PI) / 180 + Math.PI / 4;
  const dir = [Math.cos(ELEV) * Math.sin(az), Math.sin(ELEV), Math.cos(ELEV) * Math.cos(az)];
  const f = [-dir[0], -dir[1], -dir[2]];
  const s = norm(cross(f, UP));
  return { dir, s, u: cross(s, f) };
}

/** The shift along the ground, in world blocks, that moves the look-at point by (`dx`, `dy`) on the screen,
 *  in camera units. A screen-right move is along the ground already; a screen-up move is a longer step back
 *  across it, because the ground is seen at an angle. */
export function groundShift(dx, dy, yawDeg) {
  const { s, u } = cameraBasis(yawDeg);
  const across = norm(cross(UP, s));
  const rise = dot(across, u);
  return [s[0] * dx + across[0] * dy / rise, 0, s[2] * dx + across[2] * dy / rise];
}

/** The zoom a factor leads to, kept between the fitted board and close enough to count blocks. */
export function nextZoom(zoom, factor) {
  return Math.min(ZOOM_MAX, Math.max(ZOOM_MIN, zoom * factor));
}

/** How far, in camera units, the look-at point moves so the point under the pointer stays under it: the
 *  pointer's offset from the middle of the picture, `offset`, shrinks by the zoom's ratio. */
export function anchorShift(offset, zoom, next) {
  return offset * (1 - zoom / next);
}

export class IsoScene {
  #wrap; #canvas; #gl; #prog; #posBuf; #nrmBuf; #colBuf; #groundBuf; #zeroBuf;
  #aPos; #aNrm; #aCol; #uMVP; #uColor; #uViewDir; #uUnlit; #uOpacity;
  #uploaded = null;          // the mesh the three buffers hold
  #zoom = 1;
  #pan = [0, 0, 0];          // the look-at point's shift from the board's middle, in world blocks
  #fit = null;               // the last fitted frame, in camera units: { cw, ch, w, h, yaw, span }
  #last = null;              // the last render's arguments, for a redraw that only moves the camera
  #frame = 0;

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
    this.#aCol = gl.getAttribLocation(this.#prog, "aColor");
    this.#uMVP = gl.getUniformLocation(this.#prog, "uMVP");
    this.#uColor = gl.getUniformLocation(this.#prog, "uColor");
    this.#uViewDir = gl.getUniformLocation(this.#prog, "uViewDir");
    this.#uUnlit = gl.getUniformLocation(this.#prog, "uUnlit");
    this.#uOpacity = gl.getUniformLocation(this.#prog, "uOpacity");
    this.#posBuf = gl.createBuffer();
    this.#nrmBuf = gl.createBuffer();
    this.#colBuf = gl.createBuffer();
    this.#groundBuf = gl.createBuffer();
    this.#zeroBuf = gl.createBuffer();

    gl.enable(gl.DEPTH_TEST);
    gl.depthFunc(gl.LEQUAL);
    gl.disable(gl.CULL_FACE);   // faces are drawn two-sided (lighting handled in the shader)
    gl.clearColor(0, 0, 0, 0);
  }

  show() { this.#canvas.style.display = ""; }
  hide() { this.#canvas.style.display = "none"; }
  get shown() { return this.#canvas.style.display !== "none"; }
  dispose() {
    const gl = this.#gl;
    if (this.#frame) cancelAnimationFrame(this.#frame);
    for (const buf of [this.#posBuf, this.#nrmBuf, this.#colBuf, this.#groundBuf, this.#zeroBuf]) gl.deleteBuffer(buf);
    gl.deleteProgram(this.#prog);
    this.#canvas.remove();
  }

  /** How far the picture is zoomed past the fitted board: 1 is the whole board. */
  get zoom() { return this.#zoom; }

  /** Back to the whole board, centred. */
  resetView() {
    this.#zoom = 1;
    this.#pan = [0, 0, 0];
    this.#redrawSoon();
  }

  /** Zoom by `factor`, keeping the point under the pixel (`x`, `y`) of the canvas where it is. Zoomed all
   *  the way out, the board is centred again. */
  zoomAt(factor, x, y) {
    const fit = this.#fit;
    if (!fit) return;
    const next = nextZoom(this.#zoom, factor);
    if (next === this.#zoom) return;
    const ox = (x / fit.w - 0.5) * fit.cw / this.#zoom;
    const oy = (0.5 - y / fit.h) * fit.ch / this.#zoom;
    this.#shift(anchorShift(ox, this.#zoom, next), anchorShift(oy, this.#zoom, next), fit);
    this.#zoom = next;
    if (next === ZOOM_MIN) this.#pan = [0, 0, 0];   // all the way out is the whole board, centred
    this.#redrawSoon();
  }

  /** Move the picture by (`dx`, `dy`) pixels, so a drag carries the board with the pointer. */
  panBy(dx, dy) {
    const fit = this.#fit;
    if (!fit) return;
    this.#shift(-dx * fit.cw / this.#zoom / fit.w, dy * fit.ch / this.#zoom / fit.h, fit);
    this.#redrawSoon();
  }

  /**
   * Listen on `target` for the ways the picture is moved while it is shown: the wheel zooms at the pointer, a
   * drag pans, a double-click goes back to the whole board. `onZoom` hears the zoom after each change. Returns
   * the function that stops listening.
   */
  navigate(target, { onZoom } = {}) {
    let drag = null;
    const told = () => onZoom?.(this.#zoom);
    const point = (e) => { const r = this.#canvas.getBoundingClientRect(); return [e.clientX - r.left, e.clientY - r.top]; };
    const wheel = (e) => {
      if (!this.shown) return;
      e.preventDefault();
      const [x, y] = point(e);
      this.zoomAt(e.deltaY < 0 ? ZOOM_STEP : 1 / ZOOM_STEP, x, y);
      told();
    };
    const down = (e) => {
      if (!this.shown || e.button > 1) return;
      e.preventDefault();
      drag = { x: e.clientX, y: e.clientY };
    };
    const move = (e) => {
      if (!drag) return;
      this.panBy(e.clientX - drag.x, e.clientY - drag.y);
      drag = { x: e.clientX, y: e.clientY };
    };
    const up = () => { drag = null; };
    const reset = (e) => {
      if (!this.shown) return;
      e.preventDefault();
      this.resetView();
      told();
    };
    target.addEventListener("wheel", wheel, { passive: false });
    target.addEventListener("mousedown", down);
    target.addEventListener("dblclick", reset);
    document.addEventListener("mousemove", move);
    document.addEventListener("mouseup", up);
    return () => {
      target.removeEventListener("wheel", wheel);
      target.removeEventListener("mousedown", down);
      target.removeEventListener("dblclick", reset);
      document.removeEventListener("mousemove", move);
      document.removeEventListener("mouseup", up);
    };
  }

  /** Move the look-at point by a camera-space offset, kept over the board so the picture never loses it. */
  #shift(dx, dy, fit) {
    const [sx, , sz] = groundShift(dx, dy, fit.yaw);
    const clamp = (v, half) => Math.max(-half, Math.min(half, v));
    this.#pan = [clamp(this.#pan[0] + sx, fit.span[0]), 0, clamp(this.#pan[2] + sz, fit.span[1])];
  }

  /** Draw the last render again on the next frame, so a burst of wheel notches is one draw. */
  #redrawSoon() {
    if (this.#frame || !this.#last) return;
    this.#frame = requestAnimationFrame(() => {
      this.#frame = 0;
      const { mesh, w, h, yawDeg, bbox } = this.#last;
      this.render(mesh, w, h, yawDeg, bbox);
    });
  }

  /**
   * Draw a meshed world at `w×h`, rotated by `yawDeg`, over a ground plane spanning `bbox`.
   * `mesh` is `column-mesh.js`'s output: positions, normals, colors and the extent they cover.
   */
  render(mesh, w, h, yawDeg, bbox) {
    this.#last = { mesh, w, h, yawDeg, bbox };
    const gl = this.#gl;
    const ratio = Math.min(globalThis.devicePixelRatio || 1, 2);
    this.#canvas.width = Math.max(1, Math.floor(w * ratio));
    this.#canvas.height = Math.max(1, Math.floor(h * ratio));
    gl.viewport(0, 0, this.#canvas.width, this.#canvas.height);
    gl.clear(gl.COLOR_BUFFER_BIT | gl.DEPTH_BUFFER_BIT);

    let minX = Infinity, maxX = -Infinity, minZ = Infinity, maxZ = -Infinity, maxY = 0;
    const grow = (x, z, y) => { if (x < minX) minX = x; if (x > maxX) maxX = x; if (z < minZ) minZ = z; if (z > maxZ) maxZ = z; if (y > maxY) maxY = y; };

    if (mesh && mesh.positions?.length) {
      grow(mesh.minX, mesh.minZ, mesh.maxY);
      grow(mesh.maxX, mesh.maxZ, mesh.maxY);
    }

    let ground = null;
    if (bbox) {
      for (const [x, z] of [[bbox.min_x, bbox.min_z], [bbox.max_x, bbox.max_z]]) grow(x, z, 0);
      ground = new Float32Array([
        bbox.min_x, 0, bbox.min_z,  bbox.max_x, 0, bbox.min_z,  bbox.max_x, 0, bbox.max_z,
        bbox.min_x, 0, bbox.min_z,  bbox.max_x, 0, bbox.max_z,  bbox.min_x, 0, bbox.max_z,
      ]);
    }

    if (!isFinite(minX)) return;   // nothing to draw

    gl.useProgram(this.#prog);
    const { mvp, viewDir } = this.#camera(minX, maxX, minZ, maxZ, maxY, w, h, yawDeg);
    gl.uniformMatrix4fv(this.#uMVP, false, mvp);
    gl.uniform3fv(this.#uViewDir, viewDir);

    // Opaque lit geometry first (depth write on), then the translucent ground (depth test on,
    // depth write off) so it blends against whatever is in front of it.
    gl.disable(gl.BLEND);
    gl.depthMask(true);
    if (mesh?.positions?.length) {
      if (this.#uploaded !== mesh) this.#upload(mesh);
      this.#drawMesh(mesh.positions.length / 3);
    }
    if (ground) {
      gl.enable(gl.BLEND);
      gl.blendFunc(gl.SRC_ALPHA, gl.ONE_MINUS_SRC_ALPHA);
      gl.depthMask(false);
      this.#drawGround(ground);
      gl.depthMask(true);
      gl.disable(gl.BLEND);
    }
  }

  /** Hand a mesh to the GPU. Done once per mesh: a zoom, a pan or a turn redraws what is already there. */
  #upload(mesh) {
    const gl = this.#gl;
    for (const [buf, data] of [[this.#posBuf, mesh.positions], [this.#nrmBuf, mesh.normals], [this.#colBuf, mesh.colors]]) {
      gl.bindBuffer(gl.ARRAY_BUFFER, buf);
      gl.bufferData(gl.ARRAY_BUFFER, data, gl.STATIC_DRAW);
    }
    this.#uploaded = mesh;
  }

  #attrib(location, buf) {
    const gl = this.#gl;
    gl.bindBuffer(gl.ARRAY_BUFFER, buf);
    gl.enableVertexAttribArray(location);
    gl.vertexAttribPointer(location, 3, gl.FLOAT, false, 0, 0);
  }

  /** The board, lit in its vertex colours. */
  #drawMesh(count) {
    const gl = this.#gl;
    this.#attrib(this.#aPos, this.#posBuf);
    this.#attrib(this.#aNrm, this.#nrmBuf);
    this.#attrib(this.#aCol, this.#colBuf);
    gl.uniform3fv(this.#uColor, GROUND);
    gl.uniform1f(this.#uUnlit, 0);
    gl.uniform1f(this.#uOpacity, 1);
    gl.drawArrays(gl.TRIANGLES, 0, count);
  }

  /** The datum plane, unlit and translucent; six vertices, so it is sent with each draw. */
  #drawGround(positions) {
    const gl = this.#gl;
    gl.bindBuffer(gl.ARRAY_BUFFER, this.#groundBuf);
    gl.bufferData(gl.ARRAY_BUFFER, positions, gl.DYNAMIC_DRAW);
    gl.bindBuffer(gl.ARRAY_BUFFER, this.#zeroBuf);
    gl.bufferData(gl.ARRAY_BUFFER, new Float32Array(positions.length), gl.DYNAMIC_DRAW);
    this.#attrib(this.#aPos, this.#groundBuf);
    this.#attrib(this.#aNrm, this.#zeroBuf);
    this.#attrib(this.#aCol, this.#zeroBuf);
    gl.uniform3fv(this.#uColor, GROUND);
    gl.uniform1f(this.#uUnlit, 1);
    gl.uniform1f(this.#uOpacity, 0.5);
    gl.drawArrays(gl.TRIANGLES, 0, positions.length / 3);
  }

  // Place the orthographic camera at the iso angle, fit its frustum to the scene, then zoom and pan it, and
  // return the view-projection matrix plus the camera-forward direction (for two-sided shading).
  #camera(minX, maxX, minZ, maxZ, maxY, w, h, yawDeg) {
    const aspect = w / h;
    const middle = [(minX + maxX) / 2, maxY / 2, (minZ + maxZ) / 2];
    const radius = Math.hypot(maxX - minX, maxY, maxZ - minZ);
    const { dir } = cameraBasis(yawDeg);
    const eyeFor = (at) => [at[0] + dir[0] * (radius + 10), at[1] + dir[1] * (radius + 10), at[2] + dir[2] * (radius + 10)];
    const fitted = lookAt(eyeFor(middle), middle, UP);

    // Fit: project the 8 scene-box corners into camera space, take extents, add a margin, match aspect.
    let l = Infinity, r = -Infinity, b = Infinity, t = -Infinity;
    for (const x of [minX, maxX]) for (const y of [0, maxY]) for (const z of [minZ, maxZ]) {
      const cx = fitted[0] * x + fitted[4] * y + fitted[8] * z + fitted[12];
      const cy = fitted[1] * x + fitted[5] * y + fitted[9] * z + fitted[13];
      if (cx < l) l = cx; if (cx > r) r = cx; if (cy < b) b = cy; if (cy > t) t = cy;
    }
    const mx = (r - l) * 0.06, my = (t - b) * 0.06;
    l -= mx; r += mx; b -= my; t += my;
    let cw = r - l, ch = t - b;
    if (cw / ch > aspect) ch = cw / aspect; else cw = ch * aspect;
    const mcx = (l + r) / 2, mcy = (b + t) / 2;
    this.#fit = { cw, ch, w, h, yaw: yawDeg, span: [(maxX - minX) / 2, (maxZ - minZ) / 2] };

    // The pan moves the camera with the point it looks at, so the board's place in camera space moves the other
    // way; the frustum stays centred where the fit put it, and shrinks by the zoom.
    const at = [middle[0] + this.#pan[0], middle[1], middle[2] + this.#pan[2]];
    const view = lookAt(eyeFor(at), at, UP);
    const hw = cw / (2 * this.#zoom), hh = ch / (2 * this.#zoom);
    const proj = ortho(mcx - hw, mcx + hw, mcy - hh, mcy + hh, -radius - 50, radius * 3 + 50);

    return { mvp: multiply(proj, view), viewDir: [-dir[0], -dir[1], -dir[2]] };
  }
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
