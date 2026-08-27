// pick.js — the two-level selection rule both authoring canvases answer a click with. The geometry differs
// (an island is a polygon, a box is a cell rect) and stays in each canvas; what is shared is the *rule*, so
// it is written once and both call it with whatever their own hit tests found.
//
// A group is entered as a **scope**. While one is entered a click reaches its members and a click outside it
// leaves — which is what makes entering a state rather than a single gesture, and what lets a second member
// be one click away. With nothing entered the caller's own unit decides which level a plain click picks.
//
// Two modifiers cut across both: `deep` reaches the member under the cursor whatever groups it, and `up`
// reaches the group instead. Neither depends on how fast two presses land.

/**
 * What a click selects, and what the scope becomes.
 *
 * @param {object} hit
 * @param {string|null} hit.group   the group under the cursor, or null
 * @param {string|null} hit.member  the member under the cursor, or null
 * @param {string|null} hit.scope   the group currently entered, or null
 * @param {"group"|"member"} hit.unit  which level a plain click picks with nothing entered
 * @param {boolean} hit.deep        the deep-select modifier is held
 * @param {boolean} hit.up          the select-parent modifier is held
 * @returns {{ pick: "group"|"member"|"none", id: string|null, scope: string|null }}
 */
export function resolvePick({ group = null, member = null, scope = null, unit = "group", deep = false, up = false } = {}) {
  if (up) return { pick: group ? "group" : "none", id: group, scope: null };
  if (deep) return { pick: member ? "member" : "none", id: member, scope: member ? group : null };

  // Inside a scope, a click on one of its members reaches the member; anything else leaves the scope first
  // and then lands as though nothing had been entered.
  if (scope) {
    if (group === scope && member) return { pick: "member", id: member, scope };
    return resolvePick({ group, member, scope: null, unit, deep, up });
  }

  if (unit === "member") {
    return member
      ? { pick: "member", id: member, scope: group }
      : { pick: "none", id: null, scope: null };
  }
  return { pick: group ? "group" : "none", id: group, scope: null };
}
