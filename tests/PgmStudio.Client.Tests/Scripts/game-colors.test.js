// Drift check: the JS colour tables in render/palette.js must stay identical to the canonical
// game-colors.json (which the C# Models/GameColors.cs reads directly). One source of truth, two languages —
// this test is what stops them diverging, since the JS copy is inlined (not fetched) for interop robustness.
import { test, readRepoFile } from "./_harness.js";
import assert from "./_assert.js";

import { MINECRAFT_CHAT_COLORS, MINECRAFT_DYE_COLORS }
  from "../../../src/PgmStudio.Client/wwwroot/js/studio/render/palette.js";

const canonical = JSON.parse(readRepoFile("src/PgmStudio.Client/wwwroot/js/studio/render/game-colors.json"));

test("palette.js chat table matches game-colors.json (org.bukkit.ChatColor)", () => {
  assert.equal(MINECRAFT_CHAT_COLORS.length, 16);
  assert.deepEqual(MINECRAFT_CHAT_COLORS, canonical.chat);
});

test("palette.js dye table matches game-colors.json (org.bukkit.DyeColor)", () => {
  assert.equal(MINECRAFT_DYE_COLORS.length, 16);
  assert.deepEqual(MINECRAFT_DYE_COLORS, canonical.dye);
});
