// building.js — the law about a building's footprint that both authoring canvases have to apply while a drag
// is still in the pointer, before anything could be asked of the server. A room's footprint is the single-wing
// case of a building's, so the plan canvas dragging a room's building and the sketch canvas dragging a dressed
// one are held to one number rather than to a copy each.
//
// The twin of `PgmStudio.Domain.RoomFrames` (docs/world-export/structures.md WX2, HP2). Restated rather than
// served for the reason above; `RoomFramesTests` pins the C# side and `tests/js/` this one.

/**
 * The least span any building footprint may be, in blocks: two blocks of something with a block on each side
 * of it — a pad and the clear floor it keeps for a room, two walls and an inside for a dressed wing.
 */
export const MIN_FOOTPRINT_SPAN = 4;
