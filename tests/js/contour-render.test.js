// The relief contour overlay: what the server's traced lines look like when they are drawn. The tracing
// itself is C# (PgmStudio.Geom.Relief.Contours) and is tested there — what this holds is the reading the
// overlay adds on top of it, which is entirely a drawing decision: which lines are index lines, which carry
// a label, and where the label sits.
import { test } from "node:test";
import assert from "node:assert/strict";
import { recordingPainter } from "./_painter-stub.js";

import { paintContours } from "../../src/PgmStudio.Client/wwwroot/js/studio/render/sketch-render.js";

// One group's worth of lines, each a flat [x, z, x, z, …] run as the endpoint returns them.
const relief = (levels) => ({
  groups: [{
    group: "i1",
    lines: levels.map(level => ({
      level, closed: false,
      points: Array.from({ length: 12 }, (_, i) => (i % 2 === 0 ? i : level)),
    })),
  }],
});

test("every contour is stroked as one path of segments", () => {
  const painter = recordingPainter();
  paintContours(painter, relief([1, 2, 3]));
  // One segments() call per line — a contour is hundreds of points, and stroking each pair on its own is
  // the cost the batched primitive exists to avoid.
  assert.equal(painter.of("segments").length, 3);
  assert.equal(painter.of("segments")[0][0].length, 5);   // 6 points → 5 runs
});

test("every fifth level is an index line — heavier, and the only one labelled", () => {
  const painter = recordingPainter();
  paintContours(painter, relief([3, 4, 5, 6, 10]));

  const widths = painter.of("segments").map(([, style]) => style.width);
  assert.deepEqual(widths.map(w => w > 1), [false, false, true, false, true]);

  const labels = painter.of("text").map(([content]) => content);
  assert.deepEqual(labels, ["5", "10"]);
});

test("the index spacing is a setting, not a constant", () => {
  const painter = recordingPainter();
  paintContours(painter, relief([1, 2, 3, 4]), { indexEvery: 2 });
  assert.deepEqual(painter.of("text").map(([content]) => content), ["2", "4"]);
});

test("a label lands on the line's straightest stretch", () => {
  // A line that turns a hard corner at its start and then runs straight: the label belongs on the run.
  const painter = recordingPainter();
  paintContours(painter, {
    groups: [{
      group: "i1",
      lines: [{
        level: 5, closed: false,
        points: [0, 0, 1, 4, 2, 0, 3, 4, 4, 0, 20, 0, 36, 0, 52, 0, 68, 0, 84, 0, 100, 0],
      }],
    }],
  });
  const [, x] = [null, painter.of("text")[0][1]];
  assert.ok(x > 20, `label placed at x=${x}, which is back in the zig-zag`);
});

test("a line too short to hold a label window is labelled at its midpoint", () => {
  const painter = recordingPainter();
  paintContours(painter, {
    groups: [{ group: "i1", lines: [{ level: 5, closed: true, points: [0, 0, 4, 0, 4, 4] }] }],
  });
  const [content, x, z] = painter.of("text")[0];
  assert.equal(content, "5");
  assert.deepEqual([x, z], [2, 2]);
});

test("nothing is drawn for an empty payload or a degenerate line", () => {
  const painter = recordingPainter();
  paintContours(painter, null);
  paintContours(painter, { groups: [] });
  paintContours(painter, { groups: [{ group: "i1", lines: [{ level: 5, points: [3, 3] }] }] });
  assert.equal(painter.calls.length, 0);
});
