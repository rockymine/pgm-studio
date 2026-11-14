// Undo in the sketch takes back only what the phase in view edited. Each phase owns a slice of the layout —
// Draw the shapes and layers, Palette the finish and each shape's theme, Terraform the modifiers and each
// shape's relief scope, Decoration the dressing — and what must hold is that a step back in one phase never
// changes another phase's slice, and that Undo is greyed in a phase that has no step of its own.
import { test } from "node:test";
import assert from "node:assert/strict";
import { mountSketchBridge } from "./_sketch-bridge-stub.js";
import { stepBetween, stepBack, documentParts }
  from "../../src/PgmStudio.Client/wwwroot/js/studio/bridge/sketch-undo.js";
import { newModifier } from "../../src/PgmStudio.Client/wwwroot/js/studio/relief/modifier-doc.js";

const SQUARE = [[0, 0], [10, 0], [10, 10], [0, 10]];
const rect = (id, min_x, min_z) =>
  ({ id, type: "rectangle", operation: "add", override: false, min_x, min_z, max_x: min_x + 10, max_z: min_z + 10, base_height: 9 });

const shapesOf = (state) => state.layers.flatMap(layer => layer.layout.shapes);

/** Each phase's slice of a document, read straight off `getState()`. A shape's theme and relief scope are read
 *  for the shapes `ids` names: a shape Draw takes away or brings back carries its own with it. */
function slices(state, ids = new Set(shapesOf(state).map(shape => shape.id))) {
  const shapes = shapesOf(state).filter(shape => ids.has(shape.id));
  const drawn = state.layers.map(layer => ({
    ...layer, layout: { ...layer.layout, shapes: layer.layout.shapes.map(({ theme, relief_scope, ...rest }) => rest) },
  }));
  return {
    draw: JSON.stringify(drawn),
    theme: JSON.stringify({
      themes: state.themes, mapTheme: state.mapTheme, paint: state.paint, roomStyles: state.roomStyles,
      biome: state.biome, shapes: shapes.map(shape => [shape.id, shape.theme]),
    }),
    relief: JSON.stringify({ modifiers: state.modifiers, shapes: shapes.map(shape => [shape.id, shape.relief_scope]) }),
    dressing: JSON.stringify(state.dressing),
  };
}

const lastHistory = (firedOf) => firedOf("OnHistory").at(-1);

async function board() {
  const mounted = await mountSketchBridge();
  const hill = newModifier([], "mound", SQUARE);
  mounted.handle.load({
    layers: [{ id: "ground", name: "Ground", base_y: 0, layout: { shapes: [rect("a", 0, 0), rect("b", 40, 0)], groups: [] } }],
    modifiers: [hill],
    dressing: { props: [{ kind: "tree", id: "t1", x: 1, z: 2 }] },
  });
  return { ...mounted, hill };
}

/** One edit in each phase, in an order that interleaves them. */
function editEveryPhase({ handle, hill }) {
  handle.setPhase("draw");
  handle.setHeight("a", 14, null);
  handle.setPhase("theme");
  const rock = handle.defineTheme("rock");
  handle.assignShape("a", rock);
  handle.setPhase("relief");
  handle.updateModifier(hill.id, JSON.stringify({ rise: 20 }));
  handle.setReliefScope("b", "hold");
  handle.setPhase("dressing");
  handle.selectProp("t1");
  handle.updateProp(JSON.stringify({ x: 9 }));
  handle.setPhase("draw");
  handle.deleteShape("b");
  handle.setPhase("theme");
  handle.setPaint(JSON.stringify([{ id: "sand", material: "sand" }]));
}

for (const phase of ["draw", "theme", "relief", "dressing"]) {
  test(`undo in ${phase} walks back its own steps and leaves every other phase's slice as it stands`, async () => {
    const mounted = await board();
    editEveryPhase(mounted);
    const { handle, firedOf } = mounted;
    handle.setPhase(phase);
    const start = handle.getState();
    const others = (state) => {
      const kept = new Set(shapesOf(state).map(shape => shape.id));
      const both = new Set(shapesOf(start).map(shape => shape.id).filter(id => kept.has(id)));
      return [Object.entries(slices(state, both)).filter(([owner]) => owner !== phase),
              Object.entries(slices(start, both)).filter(([owner]) => owner !== phase)];
    };
    const own = slices(handle.getState())[phase];

    let steps = 0;
    while (lastHistory(firedOf)[0]) {
      await handle.undo();
      steps++;
      const [now, held] = others(handle.getState());
      assert.deepEqual(now, held, `step ${steps} back in ${phase} changed no other phase's slice`);
      assert.ok(steps < 10, "the steps run out");
    }
    assert.ok(steps > 0, `${phase} had steps of its own`);
    assert.notEqual(slices(handle.getState())[phase], own, `and ${phase}'s own slice was walked back`);
    assert.deepEqual(lastHistory(firedOf), [false, true], "Undo is greyed once the phase's own steps are taken back");

    while (lastHistory(firedOf)[1]) await handle.redo();
    assert.equal(slices(handle.getState())[phase], own, "redo walks them forward again");
    assert.deepEqual(...others(handle.getState()));
  });
}

test("a phase with no step of its own greys Undo, whatever the others did", async () => {
  const { handle, firedOf } = await board();
  handle.setPhase("draw");
  handle.setHeight("a", 14, null);
  assert.deepEqual(lastHistory(firedOf), [true, false]);
  for (const phase of ["theme", "relief", "dressing", "info", "review", "history"]) {
    handle.setPhase(phase);
    assert.deepEqual(lastHistory(firedOf), [false, false], `${phase} has no step`);
    const before = JSON.stringify(handle.getState());
    await handle.undo();
    assert.equal(JSON.stringify(handle.getState()), before, `Undo in ${phase} takes back nothing`);
  }
  handle.setPhase("draw");
  assert.deepEqual(lastHistory(firedOf), [true, false], "and Draw's step is still there");
});

test("a shape taken back in Draw comes back with the theme it carried", async () => {
  const { handle } = await board();
  handle.setPhase("theme");
  const rock = handle.defineTheme("rock");
  handle.assignShape("b", rock);
  handle.setPhase("draw");
  handle.deleteShape("b");
  await handle.undo();
  const shape = handle.getState().layers[0].layout.shapes.find(entry => entry.id === "b");
  assert.equal(shape?.theme, rock);
});

test("a part another phase owns is put back only where nothing has changed it since", () => {
  const before = { layers: "[]", "relief_scope:b": null };
  const after = { layers: "[1]", "relief_scope:b": '"hold"' };
  const step = stepBetween("draw", before, after);
  assert.deepEqual(Object.keys(step.before).sort(), ["layers", "relief_scope:b"]);

  const untouched = stepBack({ ...after }, step).restore;
  assert.deepEqual(untouched, { layers: "[]", "relief_scope:b": null }, "both parts go back while nothing has moved them");

  const changedSince = stepBack({ layers: "[1]", "relief_scope:b": '"ride"' }, step).restore;
  assert.deepEqual(changedSince, { layers: "[]" }, "Terraform's later relief scope stands");
});

test("no step is kept for an edit that changed nothing, and the setup is in no part", () => {
  const state = { setup: { mirror_mode: "rot_180" }, layers: [] };
  const moved = { setup: { mirror_mode: "mirror_x" }, layers: [] };
  assert.equal(stepBetween("draw", documentParts(state), documentParts(moved)), null);
});
