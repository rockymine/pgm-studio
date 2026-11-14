// sketch-undo.js — what a step back in one phase of the sketch takes back. The layout document is cut into
// parts, each owned by the phase that edits it, and a step keeps the parts it changed as they stood before and
// after it. Taking a step back restores its own phase's parts, and another phase's part only where nothing has
// changed that part since — so an undo never removes work done in another phase.

/** The phases that keep a stack of steps, by the host's word for each. Identity holds words and simple choices
 *  and takes no undo; Review and History edit nothing in the layout. */
export const UNDO_PHASES = ["draw", "relief", "theme", "dressing"];

/** The board's finish, Palette's: the registry, the map default, the room shells, the biome and the paint rules. */
const FINISH = ["themes", "themeSources", "mapTheme", "roomStyles", "biome", "biomeSource", "paint"];

/** The shape fields a phase other than Draw states, by the phase that owns them. */
const SHAPE_FIELDS = { theme: "theme", relief_scope: "relief" };

const OWNERS = { layers: "draw", intent: "draw", finish: "theme", dressing: "dressing", modifiers: "relief" };

/** The phase that owns a part. A shape field's part is `<field>:<shape id>`. */
export function ownerOf(key) {
  const field = key.slice(0, key.indexOf(":"));
  return OWNERS[key] ?? SHAPE_FIELDS[field] ?? null;
}

/** The parts of a layout document (the value `getState()` answers), each as its JSON text; an absent part has
 *  no key. The setup is in none of them, so no step restores it. */
export function documentParts(state) {
  const parts = {};
  const layers = (state.layers ?? []).map(layer => ({
    ...layer,
    layout: {
      ...layer.layout,
      shapes: (layer.layout?.shapes ?? []).map(shape => {
        const drawn = { ...shape };
        for (const field of Object.keys(SHAPE_FIELDS)) {
          if (drawn[field] !== undefined) parts[`${field}:${shape.id}`] = JSON.stringify(drawn[field]);
          delete drawn[field];
        }
        return drawn;
      }),
    },
  }));
  parts.layers = JSON.stringify(layers);
  parts.finish = JSON.stringify(Object.fromEntries(FINISH.map(key => [key, state[key]])));
  if (state.dressing !== undefined) parts.dressing = JSON.stringify(state.dressing);
  if (state.modifiers !== undefined) parts.modifiers = JSON.stringify(state.modifiers);
  return parts;
}

/** The step from `before` to `after` made in `phase`: the parts that differ, each as it stood on either side,
 *  `null` for absent. Null where nothing differs, which costs no step. */
export function stepBetween(phase, before, after) {
  const step = { phase, before: {}, after: {} };
  for (const key of new Set([...Object.keys(before), ...Object.keys(after)])) {
    const was = before[key] ?? null, is = after[key] ?? null;
    if (was === is) continue;
    step.before[key] = was;
    step.after[key] = is;
  }
  return Object.keys(step.before).length ? step : null;
}

/** Take `step` back over the document as it stands (`now`, its parts). Answers the parts to put back — the
 *  step's own phase's, and another phase's only where it still holds what the step left — and the step that
 *  takes this one forward again. A theme put back on a shape is put back only where the registry holds it. */
export function stepBack(now, step) {
  const restore = {};
  const forward = { phase: step.phase, before: {}, after: {} };
  for (const key of Object.keys(step.before)) {
    const current = now[key] ?? null;
    if (ownerOf(key) !== step.phase && current !== step.after[key]) continue;
    restore[key] = step.before[key];
  }
  const themes = JSON.parse(("finish" in restore ? restore.finish : now.finish) ?? "{}").themes ?? {};
  for (const key of Object.keys(restore)) {
    if (!key.startsWith("theme:") || restore[key] === null) continue;
    if (!Object.hasOwn(themes, JSON.parse(restore[key]))) delete restore[key];
  }
  for (const key of Object.keys(restore)) {
    forward.before[key] = now[key] ?? null;
    forward.after[key] = restore[key];
  }
  return { restore, forward };
}

/** The document `state` with its parts replaced by `parts` (each as `documentParts` writes it, `null` or
 *  missing for absent). The setup is the one `state` holds. */
export function composeParts(state, parts) {
  const next = JSON.parse(JSON.stringify(state));
  const finish = JSON.parse(parts.finish ?? "{}");
  for (const key of FINISH) {
    if (finish[key] === undefined) delete next[key]; else next[key] = finish[key];
  }
  for (const key of ["dressing", "modifiers"]) {
    if (parts[key] == null) delete next[key]; else next[key] = JSON.parse(parts[key]);
  }
  next.layers = JSON.parse(parts.layers ?? "[]").map(layer => ({
    ...layer,
    layout: {
      ...layer.layout,
      shapes: (layer.layout?.shapes ?? []).map(shape => {
        const whole = { ...shape };
        for (const field of Object.keys(SHAPE_FIELDS)) {
          const text = parts[`${field}:${shape.id}`];
          if (text != null) whole[field] = JSON.parse(text);
        }
        return whole;
      }),
    },
  }));
  return next;
}
