/**
 * Minecraft 1.8 colour tables — Bukkit's own: org.bukkit.ChatColor (the chat/formatting colours) and
 * org.bukkit.DyeColor (the dye/wool colours). hex is display-only; the value/name is what the wire carries.
 *
 * These tables are the single source of truth's JS face. The source of truth is the sibling game-colors.json,
 * which the C# (Models/GameColors.cs) reads directly; the arrays below mirror it and are held identical by
 * tests/js/game-colors.test.mjs (a drift check), so the two languages cannot diverge. They are inlined rather
 * than fetched because these modules load through Blazor's JS interop, where an async (top-level-await) module
 * is fragile — a single aborted fetch poisons it — and the data is a frozen 1.8 constant not worth that risk.
 *
 * Team-colour assignment below is a studio convenience, not a Bukkit thing, so it stays in code either way.
 */

/** org.bukkit.ChatColor — the 16 colours Minecraft 1.8 renders in chat, in enum-ordinal order. */
export const MINECRAFT_CHAT_COLORS = [
  { value: "black",        label: "Black",        hex: "#000000" },
  { value: "dark blue",    label: "Dark Blue",    hex: "#0000AA" },
  { value: "dark green",   label: "Dark Green",   hex: "#00AA00" },
  { value: "dark aqua",    label: "Dark Aqua",    hex: "#00AAAA" },
  { value: "dark red",     label: "Dark Red",     hex: "#AA0000" },
  { value: "dark purple",  label: "Dark Purple",  hex: "#AA00AA" },
  { value: "gold",         label: "Gold",         hex: "#FFAA00" },
  { value: "gray",         label: "Gray",         hex: "#AAAAAA" },
  { value: "dark gray",    label: "Dark Gray",    hex: "#555555" },
  { value: "blue",         label: "Blue",         hex: "#5555FF" },
  { value: "green",        label: "Green",        hex: "#55FF55" },
  { value: "aqua",         label: "Aqua",         hex: "#55FFFF" },
  { value: "red",          label: "Red",          hex: "#FF5555" },
  { value: "light purple", label: "Light Purple", hex: "#FF55FF" },
  { value: "yellow",       label: "Yellow",       hex: "#FFFF55" },
  { value: "white",        label: "White",        hex: "#FFFFFF" },
];

/** org.bukkit.DyeColor — the 16 dye/wool colours, indexed by data value (white 0 … black 15). */
export const MINECRAFT_DYE_COLORS = [
  { value: "white",      label: "White",      hex: "#FFFFFF" },
  { value: "orange",     label: "Orange",     hex: "#D87F33" },
  { value: "magenta",    label: "Magenta",    hex: "#B24CD8" },
  { value: "light blue", label: "Light Blue", hex: "#6699D8" },
  { value: "yellow",     label: "Yellow",     hex: "#E5E533" },
  { value: "lime",       label: "Lime",       hex: "#7FCC19" },
  { value: "pink",       label: "Pink",       hex: "#F27FA5" },
  { value: "gray",       label: "Gray",       hex: "#4C4C4C" },
  { value: "silver",     label: "Silver",     hex: "#999999" },
  { value: "cyan",       label: "Cyan",       hex: "#4C7F99" },
  { value: "purple",     label: "Purple",     hex: "#7F3FB2" },
  { value: "blue",       label: "Blue",       hex: "#334CB2" },
  { value: "brown",      label: "Brown",      hex: "#664C33" },
  { value: "green",      label: "Green",      hex: "#667F33" },
  { value: "red",        label: "Red",        hex: "#993333" },
  { value: "black",      label: "Black",      hex: "#191919" },
];

const _norm = name => (name ?? "").replace(/_/g, " ").toLowerCase();

export function chatColorHex(name) {
  return MINECRAFT_CHAT_COLORS.find(c => c.value === _norm(name))?.hex ?? "#475569";
}

export function dyeColorHex(name) {
  return MINECRAFT_DYE_COLORS.find(c => c.value === _norm(name))?.hex ?? "#475569";
}

export function dyeColorLabel(name) {
  return MINECRAFT_DYE_COLORS.find(c => c.value === _norm(name))?.label
    ?? name.replace(/_/g, " ");
}

// ── Team colour assignment ──────────────────────────────────────────────────

// Preferred order for auto-assigning a new team's colour: the bright, readily
// distinguishable colours first (typical CTW uses red/blue, then green/yellow…).
export const TEAM_COLOR_PRIORITY = [
  "red", "blue", "green", "yellow", "aqua", "gold", "light purple", "dark purple",
  "dark aqua", "dark green", "dark red", "dark blue", "gray", "dark gray", "white", "black",
];

/** Pick the next unused team colour in priority order, or null when all 16 are
 *  taken. `usedColors` is the existing teams' colour values (any case / `_`-form). */
export function nextTeamColor(usedColors = []) {
  const used = new Set([...usedColors].map(_norm));
  for (const value of TEAM_COLOR_PRIORITY) {
    if (!used.has(value)) return MINECRAFT_CHAT_COLORS.find(c => c.value === value);
  }
  return null;
}
