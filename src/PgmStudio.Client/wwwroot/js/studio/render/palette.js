/** PGM / Minecraft color palettes. */

export const PGM_CHAT_COLORS = [
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

export function chatColorHex(name) {
  const n = (name ?? "").replace(/_/g, " ").toLowerCase();
  return PGM_CHAT_COLORS.find(c => c.value === n)?.hex ?? "#475569";
}

const _normDye = name => (name ?? "").replace(/_/g, " ").toLowerCase();

export function dyeColorHex(name) {
  return MINECRAFT_DYE_COLORS.find(c => c.value === _normDye(name))?.hex ?? "#475569";
}

export function dyeColorLabel(name) {
  return MINECRAFT_DYE_COLORS.find(c => c.value === _normDye(name))?.label
    ?? name.replace(/_/g, " ");
}

// ── Team colour assignment ──────────────────────────────────────────────────

// Preferred order for auto-assigning a new team's colour: the bright, readily
// distinguishable colours first (typical CTW uses red/blue, then green/yellow…).
export const TEAM_COLOR_PRIORITY = [
  "red", "blue", "green", "yellow", "aqua", "gold", "light purple", "dark purple",
  "dark aqua", "dark green", "dark red", "dark blue", "gray", "dark gray", "white", "black",
];

const _normChat = name => (name ?? "").replace(/_/g, " ").toLowerCase();

/** Pick the next unused team colour in priority order, or null when all 16 are
 *  taken. `usedColors` is the existing teams' colour values (any case / `_`-form). */
export function nextTeamColor(usedColors = []) {
  const used = new Set([...usedColors].map(_normChat));
  for (const value of TEAM_COLOR_PRIORITY) {
    if (!used.has(value)) return PGM_CHAT_COLORS.find(c => c.value === value);
  }
  return null;
}
