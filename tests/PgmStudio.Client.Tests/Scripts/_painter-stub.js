// A recording stand-in for CanvasPainter, so the painted render layer is testable without a 2-D context.
// Every primitive records `[name, ...args]`; the coordinate helpers behave like the real painter at scale
// 1 with an identity world→surface transform, which is all the stateless painters depend on.

export function recordingPainter({ scale = 1, toSurface = (x, z) => ({ x, y: z }) } = {}) {
  const calls = [];
  const record = (name) => (...args) => { calls.push([name, ...args]); };
  return {
    calls,
    /** Every call of one kind, as its argument list — the usual thing an assertion wants. */
    of(name) { return calls.filter(c => c[0] === name).map(c => c.slice(1)); },
    /** The style object of the single call of that kind (fails the caller's assertion if there are none). */
    styleOf(name) { const hit = this.of(name)[0]; return hit?.[hit.length - 1]; },

    rect: record("rect"),
    line: record("line"),
    segments: record("segments"),
    circle: record("circle"),
    dot: record("dot"),
    ellipse: record("ellipse"),
    path: record("path"),
    ring: record("ring"),
    poly: record("poly"),
    text: record("text"),
    image: record("image"),

    point: (x, z) => toSurface(x, z),
    screenPx: (px) => px / scale,
    color: (value) => value,
    token: (_name, fallback) => fallback,
    begin() { calls.length = 0; },
    layer(name, paint) { calls.push(["layer", name]); paint(null); },
    get layers() { return calls.filter(c => c[0] === "layer").map(c => c[1]); },
  };
}
